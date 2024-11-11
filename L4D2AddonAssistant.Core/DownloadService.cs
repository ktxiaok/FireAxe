using Downloader;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Serilog;
using System;
using System.Reflection;

namespace L4D2AddonAssistant
{
    public class DownloadService : IDownloadService
    {
        public const string DownloadingFileExtension = ".downloading";
        public const string DownloadInfoFileExtension = ".downloadinfo";

        private const int SaveDownloadProgressIntervalMs = 1000;

        private static readonly JsonSerializerSettings s_downloadInfoJsonSettings = new()
        {
            Formatting = Formatting.Indented,
            ContractResolver = DownloadInfoContractResolver.Instance
        };

        private class DownloadInfoContractResolver : DefaultContractResolver
        {
            public static readonly DownloadInfoContractResolver Instance = new();

            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                var property = base.CreateProperty(member, memberSerialization);

                if (property.DeclaringType == typeof(DownloadPackage))
                {
                    if (property.PropertyName == nameof(DownloadPackage.Storage))
                    {
                        property.Ignored = true;
                    }
                }

                return property;
            }
        }

        private class DownloadItem : IDownloadItem
        {
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

            private readonly object _downloadLock = new();

            internal DownloadItem(string url, string filePath, DownloadConfiguration config)
            {
                _url = url;
                _filePath = filePath;

                Task.Run(() =>
                {
                    try
                    {
                        DownloadPackage? downloadPackage = null;
                        string downloadInfoFilePath = filePath + DownloadInfoFileExtension;
                        string downloadingFilePath = filePath + DownloadingFileExtension;
                        if (File.Exists(downloadInfoFilePath))
                        {
                            try
                            {
                                downloadPackage = JsonConvert.DeserializeObject<DownloadPackage>(File.ReadAllText(downloadInfoFilePath));
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
                            if (downloadPackage != null)
                            {
                                downloadPackage.FileName = downloadingFilePath;
                            }
                        }
                        lock (_downloadLock)
                        {
                            if (_status != DownloadStatus.Preparing)
                            {
                                downloadPackage?.Dispose();
                                return;
                            }
                            _download = new(config);
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
                });
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

                lock (_downloadLock)
                {
                    DisposeDownload();
                }
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
                    _status = DownloadStatus.Running;
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
                }
            }

            public void Dispose()
            {
                lock (_downloadLock)
                {
                    DisposeDownload();

                    if (_status == DownloadStatus.Preparing || _status == DownloadStatus.Running || _status == DownloadStatus.Paused)
                    {
                        _status = DownloadStatus.Cancelled;
                    }
                }
            }

            private void DisposeDownload()
            {
                if (_download != null)
                {
                    var download = _download;
                    Task.Run(() =>
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
                            Log.Error(ex, "Exception occurred during the task of DownloadItem.Dispose.");
                        }
                    });
                    _download = null;
                }
            }
        }

        private DownloadConfiguration _config = new()
        {
            ChunkCount = 8,
            ParallelDownload = true
        };

        public DownloadService()
        {

        }

        public IDownloadItem Download(string url, string filePath)
        {
            ArgumentNullException.ThrowIfNull(url);
            ArgumentNullException.ThrowIfNull(filePath);

            return new DownloadItem(url, filePath, _config);
        }

        public void Dispose()
        {

        }
    }
}
