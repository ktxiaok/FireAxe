using Newtonsoft.Json;
using Serilog;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace L4D2AddonAssistant
{
    public class WorkshopVpkAddon : VpkAddon
    {
        public const string MetaInfoFileName = ".workshop";

        internal static readonly JsonSerializerSettings s_metaInfoJsonSettings = new()
        {
            Formatting = Formatting.Indented,
        };

        private static Regex _publishedFileIdLinkRegex = new(@"steamcommunity\.com/sharedfiles/filedetails/\?.*id=(\d+)"); 

        private ulong? _publishedFileId = null;

        private AutoUpdateStrategy _autoUpdateStrategy = AutoUpdateStrategy.Default;

        private WeakReference<PublishedFileDetails?> _publishedFileDetails = new(null);
        private Task<GetPublishedFileDetailsResult>? _getPublishedFileDetailsTask = null;

        private string? _vpkPath = null;

        private Task? _checkTask = null;

        private IDownloadItem? _download = null;

        public WorkshopVpkAddon(AddonRoot root, AddonGroup? group) : base(root, group)
        {

        }

        public override Type SaveType => typeof(WorkshopVpkAddonSave);

        public override string FileExtension => ".workshop";

        public override string? FullVpkFilePath => _vpkPath;

        [DisallowNull]
        public ulong? PublishedFileId
        {
            get => _publishedFileId;
            set
            {
                ArgumentNullException.ThrowIfNull(value);

                if (NotifyAndSetIfChanged(ref _publishedFileId, value))
                {
                    _download?.Cancel();
                    ClearCaches();
                    AutoCheck();
                    Root.RequestSave = true;
                }
            }
        }

        public AutoUpdateStrategy AutoUpdateStrategy
        {
            get => _autoUpdateStrategy;
            set
            {
                if (NotifyAndSetIfChanged(ref _autoUpdateStrategy, value))
                {
                    Root.RequestSave = true;
                }
            }
        }

        public bool IsAutoUpdate => Root.ShouldUpdateWorkshopItem(AutoUpdateStrategy);

        public PublishedFileDetails? PublishedFileDetailsCache
        {
            get
            {
                if (_publishedFileDetails.TryGetTarget(out var target))
                {
                    return target;
                }
                return null;
            }
        }

        public IDownloadItem? DownloadItem
        {
            get => _download;
            set 
            { 
                if (NotifyAndSetIfChanged(ref _download, value))
                {
                    if (value != null)
                    {
                        Root.NotifyDownloadItem(value);
                    }
                }
            }
        }

        public static bool TryParsePublishedFileId(string input, out ulong id)
        {
            if (ulong.TryParse(input, out id))
            {
                return true;
            }

            var match = _publishedFileIdLinkRegex.Match(input);
            if (match.Success)
            {
                if (ulong.TryParse(match.Groups[1].ValueSpan, out id))
                {
                    return true;
                }
            }
            
            return false;
        }

        public Task<GetPublishedFileDetailsResult> GetPublishedFileDetailsAsync(CancellationToken cancellationToken)
        {
            var task = _getPublishedFileDetailsTask;
            if (task == null)
            {
                task = DoGetPublishedFileDetailsAsync(DestructionCancellationToken);
                _getPublishedFileDetailsTask = task;
                _getPublishedFileDetailsTask.ContinueWith((task) =>
                {
                    _getPublishedFileDetailsTask = null;
                    var result = task.Result;
                    if (result.IsSucceeded)
                    {
                        _publishedFileDetails.SetTarget(result.Content);
                    }
                }, Root.TaskScheduler);
            }
            return task.WaitAsync(cancellationToken);
        }

        public Task<PublishedFileDetails?> GetPublishedFileDetailsAllowCacheAsync(CancellationToken cancellationToken)
        {
            var cache = PublishedFileDetailsCache;
            if (cache != null)
            {
                return Task.FromResult(cache)!;
            }
            return GetPublishedFileDetailsAsync(cancellationToken).ContinueWith((task) =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    var result = task.Result;
                    if (result.IsSucceeded)
                    {
                        return result.Content;
                    }
                }
                return null;
            });
        }

        public override void ClearCaches()
        {
            base.ClearCaches();

            _publishedFileDetails.SetTarget(null);
        }

        protected override void OnCheck()
        {
            base.OnCheck();
            
            if (_publishedFileId.HasValue)
            {
                if (_checkTask == null)
                {
                    CreateCheckTask(_publishedFileId.Value);
                }
            }

            if (FullVpkFilePath == null)
            {
                AddProblem(new WorkshopVpkFileNotLoadProblem(this));
            }
        }

        private void CreateCheckTask(ulong publishedFileId)
        {
            string dirPath = FullFilePath;
            var cancellationToken = DestructionCancellationToken;
            var details = PublishedFileDetailsCache;
            Task<GetPublishedFileDetailsResult>? getDetailsTask = null;
            if (details == null)
            {
                getDetailsTask = GetPublishedFileDetailsAsync(cancellationToken);
            }
            bool requestUpdate = IsAutoUpdate;
            string? resultVpkPath = null;
            var problems = new List<AddonProblem>();
            var downloadService = Root.DownloadService;
            var addonRootTaskScheduler = Root.TaskScheduler;
            var addonRootTaskFactory = new TaskFactory(addonRootTaskScheduler);
            IDisposable blockMove = BlockMove();
            try
            {
                Directory.CreateDirectory(dirPath);

                string metaInfoPath = Path.Join(dirPath, MetaInfoFileName);

                // Try to read the meta info file.
                WorkshopVpkMetaInfo? metaInfo = null;
                if (File.Exists(metaInfoPath))
                {
                    try
                    {
                        metaInfo = JsonConvert.DeserializeObject<WorkshopVpkMetaInfo>(File.ReadAllText(metaInfoPath), s_metaInfoJsonSettings);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Exception occurred during reading meta info file: {FilePath}", metaInfoPath);
                    }
                }
                string? vpkPathPreview = null;
                if (metaInfo != null)
                {
                    var path = Path.Join(dirPath, metaInfo.CurrentFile);
                    if (File.Exists(path))
                    {
                        vpkPathPreview = path;
                    }
                }
                SetVpkPath(vpkPathPreview);

                _checkTask = Task.Run(() =>
                {
                    if (getDetailsTask != null)
                    {
                        var result = getDetailsTask.Result;
                        if (result.IsSucceeded)
                        {
                            details = result.Content;
                        }
                        else
                        {
                            if (result.Status == GetPublishedFileDetailsResultStatus.InvalidPublishedFileId)
                            {
                                problems.Add(new InvalidPublishedFileIdProblem(this));
                            }
                        }
                    }

                    if (details != null)
                    {
                        bool needDownload = false;

                        if (metaInfo == null)
                        {
                            needDownload = true;
                        }
                        else
                        {
                            if (metaInfo.PublishedFileId != publishedFileId)
                            {
                                needDownload = true;
                            }
                            if (requestUpdate && metaInfo.TimeUpdated != details.TimeUpdated)
                            {
                                needDownload = true;
                            }
                            if (!File.Exists(Path.Join(dirPath, metaInfo.CurrentFile)))
                            {
                                needDownload = true;
                            }
                        }

                        if (needDownload)
                        {
                            // Delete the old meta info file.
                            if (File.Exists(metaInfoPath))
                            {
                                File.Delete(metaInfoPath);
                            }

                            string downloadFileName = $"{details.Title}-{details.TimeUpdated}.vpk";
                            downloadFileName = FileUtils.SanitizeFileName(downloadFileName);
                            if (downloadFileName == "")
                            {
                                downloadFileName = "UNNAMED.vpk";
                            }
                            downloadFileName = FileUtils.GetUniqueFileName(downloadFileName, dirPath);
                            string downloadFilePath = Path.Join(dirPath, downloadFileName);
                            string url = details.FileUrl;

                            using (var download = downloadService.Download(url, downloadFilePath))
                            {
                                addonRootTaskFactory.StartNew(() => DownloadItem = download).Wait();
                                download.Wait();
                                addonRootTaskFactory.StartNew(() => DownloadItem = null).Wait();
                                var status = download.Status;
                                if (status == DownloadStatus.Succeeded)
                                {
                                    metaInfo = new WorkshopVpkMetaInfo()
                                    {
                                        PublishedFileId = publishedFileId,
                                        TimeUpdated = details.TimeUpdated,
                                        CurrentFile = downloadFileName
                                    };
                                    File.WriteAllText(metaInfoPath, JsonConvert.SerializeObject(metaInfo, s_metaInfoJsonSettings));
                                }
                                else if (status == DownloadStatus.Failed)
                                {
                                    var problem = new DownloadFailedProblem(this)
                                    {
                                        Url = url,
                                        FilePath = downloadFilePath,
                                        Exception = download.Exception
                                    };
                                    problems.Add(problem);
                                }
                            }
                        }
                    }

                    if (metaInfo != null)
                    {
                        resultVpkPath = Path.Join(dirPath, metaInfo.CurrentFile);
                    }
                });

                _checkTask.ContinueWith((task) =>
                {
                    _checkTask = null;
                    blockMove.Dispose();
                    foreach (var problem in problems)
                    {
                        AddProblem(problem);
                    }
                    SetVpkPath(resultVpkPath);

                    if (task.Exception != null)
                    {
                        var exceptions = task.Exception.Flatten().InnerExceptions;
                        foreach (var ex in exceptions)
                        {
                            if (ex is OperationCanceledException)
                            {
                                continue;
                            }
                            Log.Error(ex, "Exception occurred during the check task of WorkshopVpkAddon.");
                        }
                    }
                }, addonRootTaskScheduler);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred during WorkshopVpkAddon.CreateCheckTask.");
            }
        }

        protected override Task<byte[]?> DoGetImageAsync(CancellationToken cancellationToken)
        {
            var httpClient = Root.HttpClient;
            return GetPublishedFileDetailsAllowCacheAsync(cancellationToken).ContinueWith<byte[]?>((task) =>
            {
                if (task.IsCanceled)
                {
                    throw new TaskCanceledException();
                }
                var result = task.Result;
                if (result != null)
                {
                    string url = result.PreviewUrl;
                    try
                    {
                        return httpClient.GetByteArrayAsync(url, cancellationToken).Result;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Exception occurred during getting the preview image of the published file.\nTitle: {Title}\nUrl: {Url}", result.Title, url);
                    }
                }
                return null;
            }).ContinueWith<Task<byte[]?>>((task) =>
            {
                if (task.IsCanceled)
                {
                    throw new TaskCanceledException();
                }
                var result = task.Result;
                if (result == null)
                {
                    return base.DoGetImageAsync(cancellationToken);
                }
                else
                {
                    return Task.FromResult<byte[]?>(result);
                }
            }, Root.TaskScheduler).ContinueWith((task) => 
            {
                if (task.IsCanceled)
                {
                    throw new TaskCanceledException();
                }
                var task2 = task.Result;
                try
                {
                    return task2.Result;
                }
                catch (AggregateException ex)
                {
                    throw ex.InnerExceptions[0];
                }
            });
        }

        protected override Task OnDestroyAsync()
        {
            var baseTask = base.OnDestroyAsync();

            var tasks = new List<Task>();
            tasks.Add(baseTask);

            if (_checkTask != null)
            {
                tasks.Add(_checkTask);
            }

            if (_getPublishedFileDetailsTask != null)
            {
                tasks.Add(_getPublishedFileDetailsTask);
            }

            var download = _download;
            tasks.Add(Task.Run(() =>
            {
                download?.Dispose();
            }));

            return Task.WhenAll(tasks);
        }

        protected override void OnCreateSave(AddonNodeSave save)
        {
            base.OnCreateSave(save);
            var save1 = (WorkshopVpkAddonSave)save;
            save1.PublishedFileId = PublishedFileId;
            save1.AutoUpdateStrategy = AutoUpdateStrategy; 
        }

        protected override void OnLoadSave(AddonNodeSave save)
        {
            base.OnLoadSave(save);
            var save1 = (WorkshopVpkAddonSave)save;
            if (save1.PublishedFileId.HasValue)
            {
                PublishedFileId = save1.PublishedFileId;
            }
            AutoUpdateStrategy = save1.AutoUpdateStrategy;
        }

        private Task<GetPublishedFileDetailsResult> DoGetPublishedFileDetailsAsync(CancellationToken cancellationToken)
        {
            if (!_publishedFileId.HasValue)
            {
                return Task.FromResult(new GetPublishedFileDetailsResult(null, GetPublishedFileDetailsResultStatus.Failed));
            }
            return WebUtils.GetPublishedFileDetailsAsync(_publishedFileId.Value, Root.HttpClient, cancellationToken);
        }

        private void SetVpkPath(string? path)
        {
            _vpkPath = path;
            NotifyChanged(nameof(FullVpkFilePath));
        }
    }
}
