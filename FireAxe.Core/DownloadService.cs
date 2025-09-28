using Downloader;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System;

namespace FireAxe;

public class DownloadService : IDownloadService
{
    public const string DownloadingFileExtension = ".downloading";
    public const string DownloadInfoFileExtension = ".downloadinfo";

    private const int SaveDownloadProgressIntervalMs = 1000;

    private static readonly JsonSerializerSettings s_downloadInfoJsonSettings = new()
    {
        Formatting = Formatting.Indented
    };

    private class DownloadItem : IDownloadItem
    {
        private readonly DownloadService _service;
        private readonly string _url;
        private readonly string _filePath;

        private long _downloadedBytes = 0;
        private readonly object _downloadedBytesLock = new();

        private double _speed = 0;

        private long _lastSaveTimeMs = Environment.TickCount64;

        private long _totalBytes = 0;
        private readonly object _totalBytesLock = new();

        private Exception? _exception = null;

        private DownloadStatus _status = DownloadStatus.Preparing;

        private Downloader.DownloadService? _download = null;

        private bool _downloadCountBorrowed = false;

        private readonly object _downloadLock = new();

        private readonly TaskCompletionSource _waitTaskCompletionSource = new();

        internal DownloadItem(string url, string filePath, DownloadService service)
        {
            _service = service;
            _url = url;
            _filePath = filePath;

            Task.Run(() => Prepare());
            async void Prepare()
            {
                bool downloadCreated = false;

                try
                {
                    DownloadPackage? downloadPackage = null;
                    string downloadInfoFilePath = filePath + DownloadInfoFileExtension;
                    string downloadingFilePath = filePath + DownloadingFileExtension;
                    if (File.Exists(downloadInfoFilePath))
                    {
                        try
                        {
                            var downloadPackageJObj = JObject.Parse(File.ReadAllText(downloadInfoFilePath));
                            downloadPackageJObj["FileName"] = downloadingFilePath;
                            if (downloadPackageJObj.TryGetValue("Storage", out var storageToken))
                            {
                                storageToken["Path"] = downloadingFilePath;
                            }
                            downloadPackage = downloadPackageJObj.ToObject<DownloadPackage>();
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Exception occurred during reading .downloadinfo file: {FilePath}", downloadInfoFilePath);
                        }
                        if (downloadPackage != null)
                        {
                            if (downloadPackage.Urls.Length != 1 || downloadPackage.Urls[0] != url)
                            {
                                downloadPackage.Dispose();
                                downloadPackage = null;
                            }
                        }
                        //if (downloadPackage != null)
                        //{
                        //    downloadPackage.FileName = downloadingFilePath;
                        //}
                    }

                    while (true)
                    {
                        lock (_downloadLock)
                        {
                            if (_status != DownloadStatus.Preparing && _status != DownloadStatus.PreparingAndPaused)
                            {
                                break;
                            }
                        }

                        var availableDownloads = _service._availableDownloads;
                        if (await availableDownloads.WaitAsync(100).ConfigureAwait(false))
                        {
                            _downloadCountBorrowed = true;
                            break;
                        }
                    }

                    lock (_downloadLock)
                    {
                        if (_status == DownloadStatus.Preparing || _status == DownloadStatus.PreparingAndPaused)
                        {
                            _download = new(_service._config);
                            _download.DownloadStarted += OnDownloadStarted;
                            _download.DownloadProgressChanged += OnDownloadProgressChanged;
                            _download.DownloadFileCompleted += OnDownloadFileCompleted;
                            if (downloadPackage == null)
                            {
                                _download.DownloadFileTaskAsync(url, downloadingFilePath);
                            }
                            else
                            {
                                _download.DownloadFileTaskAsync(downloadPackage);
                            }
                            downloadCreated = true;
                        }
                        else
                        {
                            downloadPackage?.Dispose();
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (_downloadLock)
                    {
                        _exception = ex;
                        _status = DownloadStatus.Failed;
                    }
                }

                if (!downloadCreated)
                {
                    OnCompleted();
                }
            }
        }

        public string Url => _url;

        public string FilePath => _filePath;

        public long DownloadedBytes
        {
            get
            {
                lock (_downloadedBytesLock)
                {
                    return _downloadedBytes;
                }
            }
        }

        public long TotalBytes
        {
            get
            {
                lock (_totalBytesLock)
                {
                    return _totalBytes;
                }
            }
        }

        public double BytesPerSecondSpeed
        {
            get
            {
                lock (_downloadedBytesLock)
                {
                    return _speed;
                }
            }
        }

        public Exception Exception
        {
            get
            {
                lock (_downloadLock)
                {
                    if (_exception == null)
                    {
                        throw new InvalidOperationException("exception not set");
                    }
                    return _exception;
                }
            }
        }

        public DownloadStatus Status
        {
            get
            {
                lock (_downloadLock)
                {
                    return _status;
                }
            }
        }

        public void Pause()
        {
            lock (_downloadLock)
            {
                if (_status == DownloadStatus.Running)
                {
                    _download!.Pause();
                    _status = DownloadStatus.Paused;
                }
                else if (_status == DownloadStatus.Preparing)
                {
                    _status = DownloadStatus.PreparingAndPaused;
                }
            }
        }

        public void Resume()
        {
            lock (_downloadLock)
            {
                if (_status == DownloadStatus.Paused)
                {
                    _download!.Resume();
                    _status = DownloadStatus.Running;
                }
                else if (_status == DownloadStatus.PreparingAndPaused)
                {
                    _status = DownloadStatus.Preparing;
                }
            }
        }

        public void Cancel()
        {
            lock (_downloadLock)
            {
                if (_status == DownloadStatus.Preparing || _status == DownloadStatus.PreparingAndPaused || _status == DownloadStatus.Running || _status == DownloadStatus.Paused)
                {
                    _download?.CancelAsync();
                    _status = DownloadStatus.Cancelled;
                }
            }
        }

        public void Wait()
        {
            lock (_downloadLock)
            {
                if (_status.IsCompleted())
                {
                    return;
                }
                Monitor.Wait(_downloadLock);
            }
        }

        public Task WaitAsync()
        {
            return _waitTaskCompletionSource.Task;
        }

        public void Dispose()
        {
            Downloader.DownloadService? downloadToDispose = null;
            lock (_downloadLock)
            {
                downloadToDispose = _download;
                _download = null;

                if (_status == DownloadStatus.Preparing || _status == DownloadStatus.PreparingAndPaused || _status == DownloadStatus.Running || _status == DownloadStatus.Paused)
                {
                    _status = DownloadStatus.Cancelled;
                }
            }
            if (downloadToDispose != null)
            {
                DisposeDownload(downloadToDispose);
            }
        }

        private void OnDownloadFileCompleted(object? sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                lock (_downloadLock)
                {
                    _status = DownloadStatus.Cancelled;
                }
            }
            else if (e.Error != null)
            {
                lock (_downloadLock)
                {
                    _exception = e.Error;
                    _status = DownloadStatus.Failed;
                }
            }
            else
            {
                string downloadingPath = _filePath + DownloadingFileExtension;
                string downloadInfoPath = _filePath + DownloadInfoFileExtension;
                try
                {
                    if (File.Exists(_filePath))
                    {
                        File.Delete(_filePath);
                    }
                    File.Move(downloadingPath, _filePath);
                    try
                    {
                        if (File.Exists(downloadInfoPath))
                        {
                            File.Delete(downloadInfoPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Exception occurred during deleting .downloadinfo file: {FilePath}", downloadInfoPath);
                    }
                    lock (_downloadLock)
                    {
                        _status = DownloadStatus.Succeeded;
                    }
                }
                catch (Exception ex)
                {
                    lock (_downloadLock)
                    {
                        _exception = ex;
                        _status = DownloadStatus.Failed;
                    }
                }
            }

            //Downloader.DownloadService? downloadToDispose = null;
            //lock (_downloadLock)
            //{
            //    downloadToDispose = _download;
            //    _download = null;
            //}
            //if (downloadToDispose != null)
            //{
            //    DisposeDownload(downloadToDispose);
            //}

            OnCompleted();
        }

        private void OnDownloadProgressChanged(object? sender, DownloadProgressChangedEventArgs e)
        {
            bool needSave = false;
            lock (_downloadedBytesLock)
            {
                _downloadedBytes = e.ReceivedBytesSize;
                _speed = e.BytesPerSecondSpeed;
                long currentTime = Environment.TickCount64;
                if (currentTime - _lastSaveTimeMs > SaveDownloadProgressIntervalMs)
                {
                    needSave = true;
                    _lastSaveTimeMs = currentTime;
                }
            }
            if (needSave)
            {
                string savePath = _filePath + DownloadInfoFileExtension;
                try
                {
                    File.WriteAllText(savePath, JsonConvert.SerializeObject(_download!.Package, s_downloadInfoJsonSettings));
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception occurred during writing .downloadinfo file: {FilePath}", savePath);
                }
            }
        }

        private void OnDownloadStarted(object? sender, DownloadStartedEventArgs e)
        {
            lock (_totalBytesLock)
            {
                _totalBytes = e.TotalBytesToReceive;
            }
            lock (_downloadLock)
            {
                if (_status == DownloadStatus.Preparing)
                {
                    _status = DownloadStatus.Running;
                }
                else if (_status == DownloadStatus.PreparingAndPaused)
                {
                    _download?.Pause();
                    _status = DownloadStatus.Paused;
                }
            }
        }

        private void OnCompleted()
        {
            lock (_downloadLock)
            {
                if (_downloadCountBorrowed)
                {
                    _service._availableDownloads.Release();
                    _downloadCountBorrowed = false;
                }

                Monitor.PulseAll(_downloadLock);
            }

            _waitTaskCompletionSource.SetResult();
        }

        private static void DisposeDownload(Downloader.DownloadService download)
        {  
            try
            {
                download.Dispose();
            }
            catch (Exception ex)
            {
                LogException(ex);
            }
            try
            {
                download.Package.Dispose();
            }
            catch (Exception ex)
            {
                LogException(ex);
            }

            void LogException(Exception ex)
            {
                Log.Error(ex, "Exception occurred during DownloadItem.DisposeDownload.");
            }
        }
    }

    private DownloadConfiguration _config = new()
    {
        ChunkCount = 4,
        ParallelDownload = true
    };

    private SemaphoreSlim _availableDownloads = new(5);

    public DownloadService()
    {

    }

    public IDownloadItem Download(string url, string filePath)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(filePath);

        return new DownloadItem(url, filePath, this);
    }

    public void Dispose()
    {
        _availableDownloads.Dispose();
    }
}
