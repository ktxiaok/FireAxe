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
        private readonly Downloader.DownloadConfiguration _config;
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

        private bool _completed = false;

        private readonly TaskCompletionSource _waitTaskCompletionSource = new();

        internal DownloadItem(string url, string filePath, DownloadService service, Downloader.DownloadConfiguration downloaderConfig)
        {
            _service = service;
            _config = downloaderConfig;
            _url = url;
            _filePath = filePath;

            Task.Run(async () =>
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
                            downloadPackageJObj[nameof(DownloadPackage.FileName)] = downloadingFilePath;
                            if (downloadPackageJObj.TryGetValue(nameof(DownloadPackage.Storage), out var storageToken))
                            {
                                storageToken[nameof(ConcurrentStream.Path)] = downloadingFilePath;
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
                            _download = new(_config);
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
            });
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
            Downloader.DownloadService? download = null;
            lock (_downloadLock)
            {
                if (!_status.IsCompleted())
                {
                    _status = DownloadStatus.Cancelled;
                }

                download = _download;
            }
            download?.CancelAsync();
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
            Downloader.DownloadService? download = null;
            lock (_downloadLock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;

                if (!_status.IsCompleted())
                {
                    _status = DownloadStatus.Cancelled;
                }

                download = _download;
                _download = null;
            }
            OnCompleted();
            if (download is not null)
            {
                DisposeDownload(download);
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

            Downloader.DownloadService? download;
            long lastSaveTimeMs;
            lock (_downloadLock)
            {
                download = _download;
                if (download is null)
                {
                    return;
                }
                lastSaveTimeMs = _lastSaveTimeMs;
            }

            bool needSave = false;
            long currentTime = Environment.TickCount64;
            if (currentTime - lastSaveTimeMs > SaveDownloadProgressIntervalMs)
            {
                needSave = true;
                lock (_downloadLock)
                {
                    _lastSaveTimeMs = currentTime;
                }
            }
            
            if (needSave)
            {
                string savePath = _filePath + IDownloadService.DownloadInfoFileExtension;
                try
                {
                    File.WriteAllText(savePath, JsonConvert.SerializeObject(download.Package, s_downloadInfoJsonSettings));
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
                if (_completed)
                {
                    return;
                }

                if (_downloadCountBorrowed)
                {
                    _service._availableDownloads.Release();
                    _downloadCountBorrowed = false;
                }

                Monitor.PulseAll(_downloadLock);

                _waitTaskCompletionSource.SetResult();

                lock (_service._downloadItemsLock)
                {
                    _service._activeDownloadItems.Remove(this);
                }

                _completed = true;
            }
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

    private volatile bool _disposed = false;

    private readonly object _settingsLock = new();
    private DownloadServiceSettings _settings = null!;
    private Downloader.DownloadConfiguration _downloaderConfig = null!;

    private readonly object _downloadItemsLock = new();
    private readonly HashSet<DownloadItem> _activeDownloadItems = new();

    private readonly SemaphoreSlim _availableDownloads = new(5);

    public DownloadService(DownloadServiceSettings settings)
    {
        Settings = settings;
    }

    public DownloadServiceSettings Settings
    {
        get => _settings;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            lock (_settingsLock)
            {
                _settings = value;

                _downloaderConfig = new()
                {
                    ChunkCount = 4,
                    ParallelDownload = true
                };
                if (_settings.Proxy is { } proxy)
                {
                    _downloaderConfig.RequestConfiguration.Proxy = proxy;
                }
            }
        }
    }

    public IEnumerable<IDownloadItem> ActiveDownloadItems
    {
        get
        {
            lock (_downloadItemsLock)
            {
                return [.. _activeDownloadItems];
            }
        }
    }

    public IDownloadItem Download(string url, string filePath)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(filePath);

        ThrowIfDisposed();

        Downloader.DownloadConfiguration downloaderConfig;
        lock (_settingsLock)
        {
            downloaderConfig = _downloaderConfig;
        }
        lock (_downloadItemsLock)
        {
            var downloadItem = new DownloadItem(url, filePath, this, downloaderConfig);
            _activeDownloadItems.Add(downloadItem);
            return downloadItem;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;

            DownloadItem[] downloadItems;
            lock (_downloadItemsLock)
            {
                downloadItems = [.. _activeDownloadItems];
            }
            var downloadItemDisposeTasks = new Task[downloadItems.Length];
            for (int i = 0, len = downloadItems.Length; i < len; i++)
            {
                var downloadItem = downloadItems[i];
                downloadItemDisposeTasks[i] = Task.Run(downloadItem.Dispose);
            }
            await Task.WhenAll(downloadItemDisposeTasks).ConfigureAwait(false);

            _availableDownloads.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }
}
