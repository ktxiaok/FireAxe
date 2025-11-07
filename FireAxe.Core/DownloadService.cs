using Downloader;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System;

namespace FireAxe;

public sealed class DownloadService : IDownloadService
{
    private const int SaveDownloadProgressIntervalMs = 1000;

    private static readonly JsonSerializerSettings s_downloadInfoJsonSettings = new()
    {
        Formatting = Formatting.Indented
    };

    private sealed class DownloadItem : IDownloadItem
    {
        private bool _disposed = false;

        private readonly DownloadService _service;
        private readonly string _url;
        private readonly string _filePath;

        private long _downloadedBytes = 0;

        private double _speed = 0;

        private long _lastSaveTimeMs = Environment.TickCount64;

        private long _totalBytes = 0;

        private Exception? _exception = null;

        // NOTE: The field _status is always Running when the downloader is in state Running or Paused.
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

            Task.Run(Prepare);
            async void Prepare()
            {
                bool downloadCreated = false;

                try
                {
                    DownloadPackage? downloadPackage = null;
                    string downloadInfoFilePath = filePath + IDownloadService.DownloadInfoFileExtension;
                    string downloadingFilePath = filePath + IDownloadService.DownloadingFileExtension;
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
                            Log.Error(ex, $"Exception occurred during reading the {IDownloadService.DownloadInfoFileExtension} file: {FilePath}", downloadInfoFilePath);
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
            get => Volatile.Read(ref _downloadedBytes);
            private set => Volatile.Write(ref _downloadedBytes, value);
        }

        public long TotalBytes
        {
            get => Volatile.Read(ref _totalBytes);
            private set => Volatile.Write(ref _totalBytes, value);
        }

        public double BytesPerSecondSpeed
        {
            get => Volatile.Read(ref _speed);
            private set => Volatile.Write(ref _speed, value);
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
                Downloader.DownloadService download;
                lock (_downloadLock)
                {
                    if (_download is null)
                    {
                        return _status;
                    }
                    download = _download;
                }

                switch (download.Status)
                {
                    case Downloader.DownloadStatus.Running:
                        return DownloadStatus.Running;
                    case Downloader.DownloadStatus.Paused:
                        return DownloadStatus.Paused;
                }

                lock (_downloadLock)
                {
                    return _status;
                }
            }
        }

        public void Pause()
        {
            Downloader.DownloadService? download;
            lock (_downloadLock)
            {
                if (_status == DownloadStatus.Preparing)
                {
                    _status = DownloadStatus.PreparingAndPaused;
                }
                download = _download;
            }
            download?.Pause();
        }

        public void Resume()
        {
            Downloader.DownloadService? download;
            lock (_downloadLock)
            {
                if (_status == DownloadStatus.PreparingAndPaused)
                {
                    _status = DownloadStatus.Preparing;
                }
                download = _download;
            }
            download?.Resume();
        }

        public void Cancel()
        {
            Downloader.DownloadService? downloadToCancel = null;
            lock (_downloadLock)
            {
                if (_download is null)
                {
                    if (_status is DownloadStatus.Preparing or DownloadStatus.PreparingAndPaused)
                    {
                        _status = DownloadStatus.Cancelled;
                    }
                }
                else
                {
                    downloadToCancel = _download;
                }
            }
            downloadToCancel?.CancelAsync();
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
                if (_disposed)
                {
                    return;
                }

                _disposed = true;

                if (_download is null)
                {
                    if (_status is DownloadStatus.Preparing or DownloadStatus.PreparingAndPaused)
                    {
                        _status = DownloadStatus.Cancelled;
                    }
                }
                else
                {
                    downloadToDispose = _download;
                }
            }
            if (downloadToDispose is not null)
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
                string downloadingPath = _filePath + IDownloadService.DownloadingFileExtension;
                string downloadInfoPath = _filePath + IDownloadService.DownloadInfoFileExtension;
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
                        Log.Error(ex, "Exception occurred during deleting the file: {FilePath}", downloadInfoPath);
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

            OnCompleted();
        }

        private void OnDownloadProgressChanged(object? sender, DownloadProgressChangedEventArgs e)
        {
            DownloadedBytes = e.ReceivedBytesSize;
            BytesPerSecondSpeed = e.BytesPerSecondSpeed;

            bool needSave = false;
            long currentTime = Environment.TickCount64;
            if (currentTime - _lastSaveTimeMs > SaveDownloadProgressIntervalMs)
            {
                needSave = true;
                _lastSaveTimeMs = currentTime;
            }
            
            if (needSave)
            {
                string savePath = _filePath + IDownloadService.DownloadInfoFileExtension;
                try
                {
                    File.WriteAllText(savePath, JsonConvert.SerializeObject(_download!.Package, s_downloadInfoJsonSettings));
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Exception occurred during writing {IDownloadService.DownloadInfoFileExtension} file: {FilePath}", savePath);
                }
            }
        }

        private void OnDownloadStarted(object? sender, DownloadStartedEventArgs e)
        {
            TotalBytes = e.TotalBytesToReceive;

            Downloader.DownloadService? downloadToPause = null;
            lock (_downloadLock)
            {
                if (_status is DownloadStatus.PreparingAndPaused)
                {
                    downloadToPause = _download;
                }
                _status = DownloadStatus.Running;
            }
            downloadToPause?.Pause();
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

    private bool _disposed = false;

    private DownloadConfiguration _config = new()
    {
        ChunkCount = 4,
        ParallelDownload = true
    };

    private readonly SemaphoreSlim _availableDownloads = new(5);

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
        if (!_disposed)
        {
            _availableDownloads.Dispose();

            _disposed = true;
        }
    }
}
