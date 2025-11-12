using Newtonsoft.Json;
using Serilog;
using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace FireAxe;

public class WorkshopVpkAddon : VpkAddon
{
    public const string MetaInfoFileName = ".workshop";

    internal static readonly JsonSerializerSettings s_metaInfoJsonSettings = new()
    {
        Formatting = Formatting.Indented,
    }; 

    private ulong? _publishedFileId = null;

    private AutoUpdateStrategy _autoUpdateStrategy = AutoUpdateStrategy.Default;

    private readonly WeakReference<PublishedFileDetails?> _publishedFileDetailsCache = new(null);
    private Task<GetPublishedFileDetailsResult>? _getPublishedFileDetailsTask = null;

    private string? _currentVpkFileName = null;

    private readonly AddonProblemSource<WorkshopVpkAddon> _vpkNotLoadProblemSource;
    private readonly AddonProblemSource<WorkshopVpkAddon> _invalidPublishedFileIdProblemSource;
    private readonly AddonProblemSource _downloadFailedProblemSource;

    private Task? _downloadCheckTask = null;

    private IDownloadItem? _download = null;

    private bool _requestAutoSetName = false;

    private bool _requestApplyTagsFromWorkshop = true;

    protected WorkshopVpkAddon()
    {
        _vpkNotLoadProblemSource = new(this);
        _invalidPublishedFileIdProblemSource = new(this);
        _downloadFailedProblemSource = new(this);

        PropertyChanged += OnPropertyChanged;
    }

    public override Type SaveType => typeof(WorkshopVpkAddonSave);

    public override string FileExtension => ".workshop";

    public override string? FullVpkFilePath
    {
        get
        {
            this.ThrowIfInvalid();

            var currentFile = CurrentVpkFileName;
            if (currentFile is null)
            {
                return null;
            }
            return Path.Join(FullFilePath, currentFile);
        }
    }

    [DisallowNull]
    public ulong? PublishedFileId
    {
        get => _publishedFileId;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            this.ThrowIfInvalid();

            if (value == _publishedFileId)
            {
                return;
            }

            ClearCaches();

            _publishedFileId = value;
            NotifyChanged();

            _download?.Cancel();
            AutoCheck();
            Root.RequestSave = true;
        }
    }

    public AutoUpdateStrategy AutoUpdateStrategy
    {
        get => _autoUpdateStrategy;
        set
        {
            this.ThrowIfInvalid();

            if (NotifyAndSetIfChanged(ref _autoUpdateStrategy, value))
            {
                Root.RequestSave = true;
            }
        }
    }

    public bool IsAutoUpdate
    {
        get
        {
            this.ThrowIfInvalid();

            return Root.ShouldAutoUpdateWorkshopItem(AutoUpdateStrategy);
        }
    }

    public PublishedFileDetails? PublishedFileDetailsCache
    {
        get
        {
            if (_publishedFileDetailsCache.TryGetTarget(out var target))
            {
                return target;
            }
            return null;
        }
        private set
        {
            _publishedFileDetailsCache.SetTarget(value);
            NotifyChanged();
        }
    }

    public IDownloadItem? DownloadItem
    {
        get => _download;
        private set 
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

    public bool RequestAutoSetName
    {
        get => _requestAutoSetName;
        set
        {
            this.ThrowIfInvalid();

            if (NotifyAndSetIfChanged(ref _requestAutoSetName, value))
            {
                Root.RequestSave = true;
            }
        }
    }

    public bool RequestApplyTagsFromWorkshop
    {
        get => _requestApplyTagsFromWorkshop;
        set
        {
            this.ThrowIfInvalid();

            if (NotifyAndSetIfChanged(ref _requestApplyTagsFromWorkshop, value))
            {
                Root.RequestSave = true;
            }
        }
    }

    private string? CurrentVpkFileName
    {
        get => _currentVpkFileName;
        set
        {
            if (value == _currentVpkFileName)
            {
                return;
            }

            _currentVpkFileName = value;
            NotifyChanged();
            NotifyChanged(nameof(FullVpkFilePath));
        }
    }

    public Task<GetPublishedFileDetailsResult> GetPublishedFileDetailsAsync(CancellationToken cancellationToken = default)
    {
        this.ThrowIfInvalid();

        var task = _getPublishedFileDetailsTask;
        if (task == null)
        {
            var addonTaskCreator = this.GetValidTaskCreator();
            var rawTask = DoGetPublishedFileDetailsAsync(DestructionCancellationToken);
            async Task<GetPublishedFileDetailsResult> RunTask()
            {
                var result = await rawTask.ConfigureAwait(false);
                await addonTaskCreator.StartNew(self => self.PublishedFileDetailsCache = result.IsSucceeded ? result.Content : null).ConfigureAwait(false);
                return result;
            }
            task = RunTask();
            _getPublishedFileDetailsTask = task;
            _getPublishedFileDetailsTask.ContinueWith(_ => addonTaskCreator.StartNew(self =>
            {
                if (self._getPublishedFileDetailsTask == task)
                {
                    self._getPublishedFileDetailsTask = null;
                }
            }));
        }
        return task.WaitAsync(cancellationToken);
    }

    public async Task<PublishedFileDetails?> GetPublishedFileDetailsAllowCacheAsync(CancellationToken cancellationToken = default)
    {
        this.ThrowIfInvalid();

        var cache = PublishedFileDetailsCache;
        if (cache != null)
        {
            return cache;
        }
        var result = await GetPublishedFileDetailsAsync(cancellationToken).ConfigureAwait(false);
        if (result.IsSucceeded)
        {
            return result.Content;
        }
        return null;
    }

    protected override void OnClearCaches()
    {
        base.OnClearCaches();

        PublishedFileDetailsCache = null;
    }

    protected override void OnClearCacheFiles()
    {
        base.OnClearCacheFiles();

        string imagePath = GetImageCacheFilePath();
        try
        {
            if (File.Exists(imagePath))
            {
                File.Delete(imagePath);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred during deleting the image cache file: {FilePath}", imagePath);
        }
    }

    public class DeleteRedundantVpkFilesReport
    {
        internal static readonly DeleteRedundantVpkFilesReport Empty = new([]);

        private long _totalFileSize = -1;

        internal DeleteRedundantVpkFilesReport(IReadOnlyCollection<string> files)
        {
            Files = files;
        }

        public bool IsEmpty => Files.Count == 0;

        public IReadOnlyCollection<string> Files { get; }

        public long TotalFileSize 
        {
            get
            {
                if (_totalFileSize <= 0)
                {
                    _totalFileSize = 0;
                    foreach (string file in Files)
                    {
                        try
                        {
                            if (File.Exists(file))
                            {
                                _totalFileSize += new FileInfo(file).Length;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Exception occurred during getting the size of the file {FilePath}", file);
                        }
                    }
                }

                return _totalFileSize;
            }
        }

        public static DeleteRedundantVpkFilesReport Combine(IEnumerable<DeleteRedundantVpkFilesReport> reports)
        {
            ArgumentNullException.ThrowIfNull(reports);

            return new DeleteRedundantVpkFilesReport(reports.SelectMany(report => report.Files).ToArray());
        }

        public void Execute()
        {
            foreach (string file in Files)
            {
                if (File.Exists(file))
                {
                    FileSystemUtils.MoveToRecycleBin(file);
                }
            }
        }
    }

    public DeleteRedundantVpkFilesReport RequestDeleteRedundantVpkFiles()
    {
        this.ThrowIfInvalid();

        string dirPath = FullFilePath;
        if (!Directory.Exists(dirPath))
        {
            return DeleteRedundantVpkFilesReport.Empty;
        }

        var vpks = new List<string>(Directory.EnumerateFiles(dirPath, "*.vpk"));
        if (vpks.Count == 0)
        {
            return DeleteRedundantVpkFilesReport.Empty;
        }

        WorkshopVpkMetaInfo? metaInfo = null;
        string metaInfoPath = Path.Join(dirPath, MetaInfoFileName);
        if (File.Exists(metaInfoPath))
        {
            string metaInfoJson = File.ReadAllText(metaInfoPath);
            try
            {
                metaInfo = JsonConvert.DeserializeObject<WorkshopVpkMetaInfo>(metaInfoJson, s_metaInfoJsonSettings);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred during deserializing json file {FilePath}", metaInfoPath);
            }
        }
        bool currentVpkPresent = false;
        if (metaInfo?.CurrentFile is { } currentVpk)
        {
            for (int i = 0, len = vpks.Count; i < len; i++)
            {
                if (Path.GetFileName(vpks[i]) == currentVpk)
                {
                    vpks.RemoveAt(i);
                    currentVpkPresent = true;
                    break;
                }
            }
        }

        if (!currentVpkPresent)
        {
            int latestVpkIndex = -1;
            long maxTicks = 0;
            for (int i = 0, len = vpks.Count; i < len; i++)
            {
                long ticks = File.GetCreationTimeUtc(vpks[i]).Ticks;
                if (latestVpkIndex < 0 || ticks > maxTicks)
                {
                    latestVpkIndex = i;
                    maxTicks = ticks;
                }
            }

            if (latestVpkIndex >= 0)
            {
                vpks.RemoveAt(latestVpkIndex);
            }
        }

        return new DeleteRedundantVpkFilesReport(vpks);
    }

    public void CheckDownload()
    {
        this.ThrowIfInvalid();

        _vpkNotLoadProblemSource.Clear();

        if (_publishedFileId.HasValue)
        {
            if (_downloadCheckTask == null)
            {
                var addonTaskCreator = this.GetValidTaskCreator();
                _downloadCheckTask = RunDownloadCheckTask(_publishedFileId.Value, addonTaskCreator);
                _downloadCheckTask.ContinueWith(task => addonTaskCreator.StartNew(self =>
                {
                    if (self._downloadCheckTask == task)
                    {
                        self._downloadCheckTask = null;
                    }
                }));
            }
        }

        if (FullVpkFilePath == null)
        {
            new WorkshopVpkNotLoadProblem(_vpkNotLoadProblemSource).Submit();
        }
    }

    protected override void OnCheck()
    {
        base.OnCheck();

        CheckDownload();
    }

    private async Task RunDownloadCheckTask(ulong publishedFileId, ValidTaskCreator<WorkshopVpkAddon> addonTaskCreator)
    {
        IDisposable blockMove = BlockMove();
        string dirPath = null!;
        string metaInfoPath = null!;
        void UpdatePaths()
        {
            dirPath = FullFilePath;
            metaInfoPath = Path.Join(dirPath, MetaInfoFileName);
        }
        UpdatePaths();

        string imageCacheFilePath = GetImageCacheFilePath();
        var httpClient = Root.HttpClient;
        var cancellationToken = DestructionCancellationToken;
        var details = PublishedFileDetailsCache;
        Task<GetPublishedFileDetailsResult>? getDetailsTask = null;
        if (details is null)
        {
            getDetailsTask = GetPublishedFileDetailsAsync(cancellationToken);
        }
        var downloadService = Root.DownloadService;
        bool invalid = false;

        _invalidPublishedFileIdProblemSource.Clear();
        _downloadFailedProblemSource.Clear();

        try
        {
            Directory.CreateDirectory(dirPath);

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

            CurrentVpkFileName = metaInfo?.CurrentFile;

            if (getDetailsTask is not null)
            {
                invalid = await addonTaskCreator.StartNew(self => blockMove.Dispose()).ConfigureAwait(false);
                if (invalid)
                {
                    return;
                }
                var result = await getDetailsTask.ConfigureAwait(false);
                invalid = await addonTaskCreator.StartNew(self =>
                {
                    blockMove = self.BlockMove();
                    UpdatePaths();
                }).ConfigureAwait(false);
                if (invalid)
                {
                    return;
                }

                if (result.IsSucceeded)
                {
                    details = result.Content;
                }
                else
                {
                    if (result.Status == GetPublishedFileDetailsResultStatus.InvalidPublishedFileId)
                    {
                        invalid = await addonTaskCreator.StartNew(self => new InvalidPublishedFileIdProblem(self._invalidPublishedFileIdProblemSource).Submit()).ConfigureAwait(false);
                        if (invalid)
                        {
                            return;
                        }
                    }
                }
            }

            void DeleteDownloadTempFiles(bool all)
            {
                IEnumerable<string> targetDownloadingFiles;
                var downloadingFiles = Directory.EnumerateFiles(dirPath, $"*{IDownloadService.DownloadingFileExtension}");
                if (all)
                {
                    targetDownloadingFiles = downloadingFiles;
                }
                else
                {
                    var downloadingFileList = new List<string>(downloadingFiles);
                    int latestIndex = -1;
                    long maxTicks = 0;
                    for (int i = 0, len = downloadingFileList.Count; i < len; i++)
                    {
                        long ticks = File.GetCreationTimeUtc(downloadingFileList[i]).Ticks;
                        if (latestIndex < 0 || ticks > maxTicks)
                        {
                            latestIndex = i;
                            maxTicks = ticks;
                        }
                    }
                    if (latestIndex >= 0)
                    {
                        downloadingFileList.RemoveAt(latestIndex);
                    }
                    targetDownloadingFiles = downloadingFileList;
                }

                foreach (string downloadingFile in targetDownloadingFiles)
                {
                    string downloadInfoFile = downloadingFile.Substring(0, downloadingFile.Length - IDownloadService.DownloadingFileExtension.Length) + IDownloadService.DownloadInfoFileExtension;
                    if (File.Exists(downloadingFile))
                    {
                        File.Delete(downloadingFile);
                    }
                    if (File.Exists(downloadInfoFile))
                    {
                        File.Delete(downloadInfoFile);
                    }
                }
            }

            if (details is not null)
            {
                invalid = await addonTaskCreator.StartNew(self =>
                {
                    if (self.RequestAutoSetName)
                    {
                        blockMove.Dispose();

                        var name = FileSystemUtils.SanitizeFileName(details.Title);
                        if (name.Length == 0)
                        {
                            name = "UNNAMED";
                        }
                        name = self.Parent.GetUniqueNodeName(name);
                        try
                        {
                            self.Name = name;
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Exception occurred during setting the name of the workshop vpk addon.");
                        }

                        blockMove = self.BlockMove();
                        UpdatePaths();
                    }

                    if (self.RequestApplyTagsFromWorkshop)
                    {
                        var tags = details.Tags;
                        if (tags is not null)
                        {
                            foreach (var tagObj in tags)
                            {
                                self.AddTag(tagObj.Tag);
                            }
                            self.RequestApplyTagsFromWorkshop = false;
                        }
                    }
                }).ConfigureAwait(false);
                if (invalid)
                {
                    return;
                }

                byte[]? image = null;
                string imageUrl = details.PreviewUrl;
                try
                {
                    invalid = await addonTaskCreator.StartNew(self => blockMove.Dispose()).ConfigureAwait(false);
                    if (invalid)
                    {
                        return;
                    }
                    image = await httpClient.GetByteArrayAsync(imageUrl, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception occurred during downloading the image: {Url}", imageUrl);
                }
                finally
                {
                    if (!invalid)
                    {
                        invalid = await addonTaskCreator.StartNew(self =>
                        {
                            blockMove = self.BlockMove();
                            UpdatePaths();
                        }).ConfigureAwait(false);
                    }
                }
                if (invalid)
                {
                    return;
                }
                if (image is not null)
                {
                    try
                    {
                        await File.WriteAllBytesAsync(imageCacheFilePath, image).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Exception occurred during writing the image cache file: {FilePath}", imageCacheFilePath);
                    }
                }

                bool needDownload = false;

                if (metaInfo is null)
                {
                    needDownload = true;
                }
                else
                {
                    if (metaInfo.PublishedFileId != publishedFileId)
                    {
                        needDownload = true;
                    }
                    if (metaInfo.TimeUpdated != details.TimeUpdated)
                    {
                        bool isAutoUpdate = false;
                        invalid = await addonTaskCreator.StartNew(self => isAutoUpdate = self.IsAutoUpdate).ConfigureAwait(false);
                        if (invalid)
                        {
                            return;
                        }
                        if (isAutoUpdate)
                        {
                            needDownload = true;
                        }
                    }
                    if (!File.Exists(Path.Join(dirPath, metaInfo.CurrentFile)))
                    {
                        needDownload = true;
                    }
                }

                if (needDownload)
                {
                    string downloadFileName = $"{details.Title}-time_updated-{details.TimeUpdated}.vpk";
                    downloadFileName = FileSystemUtils.SanitizeFileName(downloadFileName);
                    if (downloadFileName == "")
                    {
                        downloadFileName = "UNNAMED.vpk";
                    }
                    downloadFileName = FileSystemUtils.GetUniqueFileName(downloadFileName, dirPath);
                    string downloadFilePath = Path.Join(dirPath, downloadFileName);
                    string url = details.FileUrl;

                    using (var download = downloadService.Download(url, downloadFilePath))
                    {
                        invalid = await addonTaskCreator.StartNew(self => self.DownloadItem = download).ConfigureAwait(false);
                        if (invalid)
                        {
                            return;
                        }
                        await download.WaitAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
                        invalid = await addonTaskCreator.StartNew(self => self.DownloadItem = null).ConfigureAwait(false);
                        if (invalid)
                        {
                            return;
                        }
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
                            DeleteDownloadTempFiles(true);
                        }
                        else if (status == DownloadStatus.Failed)
                        {
                            invalid = await addonTaskCreator.StartNew(
                                self => new AddonDownloadFailedProblem(self._downloadFailedProblemSource)
                                {
                                    Url = url,
                                    FilePath = downloadFilePath,
                                    Exception = download.Exception
                                }.Submit()).ConfigureAwait(false);
                            if (invalid)
                            {
                                return;
                            }
                        }
                    }
                }
                else
                {
                    DeleteDownloadTempFiles(true);
                }
            }

            if (metaInfo is not null)
            {
                invalid = await addonTaskCreator.StartNew(self =>
                {
                    self.CurrentVpkFileName = metaInfo.CurrentFile;
                    self._vpkNotLoadProblemSource.Clear();
                }).ConfigureAwait(false);
                if (invalid)
                {
                    return;
                }
            }

            DeleteDownloadTempFiles(false);
        }
        catch (OperationCanceledException) 
        {
            return;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred during WorkshopVpkAddon.RunDownloadCheckTask.");
        }
        finally
        {
            if (!invalid)
            {
                invalid = await addonTaskCreator.StartNew(self =>
                {
                    blockMove.Dispose();
                }).ConfigureAwait(false);
            }
        }
    }

    protected override async Task<byte[]?> DoGetImageAsync(CancellationToken cancellationToken)
    {
        byte[]? image = null;
        var httpClient = Root.HttpClient;

        string imageCacheFilePath = GetImageCacheFilePath();
        try
        {
            if (File.Exists(imageCacheFilePath))
            {
                image = await File.ReadAllBytesAsync(imageCacheFilePath, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred during reading the image cache file: {FilePath}", imageCacheFilePath);
        }
        if (image != null)
        {
            return image;
        }

        var publishedFileDetails = await GetPublishedFileDetailsAllowCacheAsync(cancellationToken).ConfigureAwait(false);
        if (publishedFileDetails != null)
        {
            string imageUrl = publishedFileDetails.PreviewUrl;
            try
            {
                image = await httpClient.GetByteArrayAsync(imageUrl, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred during downloading the image: {Url}", imageUrl);
            }
            if (image != null)
            {
                try
                {
                    await File.WriteAllBytesAsync(imageCacheFilePath, image).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception occurred during writing the image cache file: {FilePath}", imageCacheFilePath);
                }
            }
        }

        return image;
    }

    private string GetImageCacheFilePath()
    {
        return Path.Join(Root.CacheDirectoryPath, GetImageCacheFileName());
    }

    private string GetImageCacheFileName()
    {
        return $"workshop_image_{PublishedFileId.GetValueOrDefault(0)}";
    }

    protected override Task OnDestroyAsync()
    {
        var tasks = new List<Task>();

        _download?.Cancel();
        if (_downloadCheckTask != null)
        {
            tasks.Add(_downloadCheckTask);
        }

        if (_getPublishedFileDetailsTask != null)
        {
            tasks.Add(_getPublishedFileDetailsTask);
        }

        var baseTask = base.OnDestroyAsync();
        tasks.Add(baseTask);

        return TaskUtils.WhenAllIgnoreCanceled(tasks);
    }

    protected override void OnCreateSave(AddonNodeSave save)
    {
        base.OnCreateSave(save);
        var save1 = (WorkshopVpkAddonSave)save;
        save1.PublishedFileId = PublishedFileId;
        save1.AutoUpdateStrategy = AutoUpdateStrategy;
        save1.RequestAutoSetName = RequestAutoSetName;
        save1.RequestApplyTagsFromWorkshop = RequestApplyTagsFromWorkshop;
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
        RequestAutoSetName = save1.RequestAutoSetName;
        RequestApplyTagsFromWorkshop = save1.RequestApplyTagsFromWorkshop;
    }

    private Task<GetPublishedFileDetailsResult> DoGetPublishedFileDetailsAsync(CancellationToken cancellationToken)
    {
        if (!_publishedFileId.HasValue)
        {
            return Task.FromResult(new GetPublishedFileDetailsResult(null, GetPublishedFileDetailsResultStatus.Failed));
        }
        return PublishedFileUtils.GetPublishedFileDetailsAsync(_publishedFileId.Value, Root.HttpClient, cancellationToken);
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var name = e.PropertyName;
        if (name == nameof(Name))
        {
            RequestAutoSetName = false;
        }
        else if (name == nameof(FullFilePath))
        {
            NotifyChanged(nameof(FullVpkFilePath));
        }
    }
}
