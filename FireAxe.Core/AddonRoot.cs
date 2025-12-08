using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.IO.Enumeration;
using System.Text.RegularExpressions;
using ValveKeyValue;

namespace FireAxe;

public sealed class AddonRoot : ObservableObject, IAsyncDisposable, IAddonNodeContainer, IAddonNodeContainerInternal, ISaveable, IValidity
{
    public const string SaveFileName = ".addonroot";
    public const string VersionFileName = ".addonrootversion";
    public const string AddonRootDirectoryName = ".addonrootdir";
    public const string CacheDirectoryName = "caches";
    public const string BackupDirectoryName = "backups";

    private static readonly JsonSerializerSettings s_jsonSettings = new()
    {
        Formatting = Formatting.Indented,
        TypeNameHandling = TypeNameHandling.Auto,
        Converters =
        {
            new StringEnumConverter()
        }
    };

    private static readonly Regex s_pushedLinkVpkFileNameNoExtRegex = new(@"^fireaxe_link_[0-9A-Fa-f]+$");

    private static readonly Regex s_backupFileNameRegex = new(@"^backup_(\d+)-(\d+)-(\d+)_(\d+)-(\d+)\.addonroot$");

    private bool _disposed = false;

    private DirectoryInfo? _directory = null;

    private string _gamePath = "";

    private readonly ObservableCollection<string> _customTags = new();
    private readonly ReadOnlyObservableCollection<string> _customTagsReadOnly;
    private readonly HashSet<string> _customTagSet = new();

    private readonly Dictionary<Guid, AddonNode> _idToNode = new();

    private int _problemCount = 0;

    private Task? _checkTask = null; 
    private CancellationTokenSource? _checkVpkConflictsCts = null;
    private Task<VpkAddonConflictResult>? _checkVpkConflictsTask = null;

    private TaskScheduler? _taskScheduler = null;

    private IDownloadService? _downloadService = null;

    private HttpClient? _httpClient = null;

    private readonly AddonNodeContainerService _containerService;

    private int _blockAutoCheck = 0;
    
    public AddonRoot()
    {
        _containerService = new(this);
        _customTagsReadOnly = new(_customTags);

        ((INotifyCollectionChanged)Nodes).CollectionChanged += OnCollectionChanged;
        ((INotifyCollectionChanged)_customTags).CollectionChanged += OnCustomTagsCollectionChanged;
    }

    public event Action<IDownloadItem>? NewDownloadItem = null;

    public bool IsValid => !_disposed;

    public TaskScheduler TaskScheduler
    {
        get
        {
            return _taskScheduler ?? throw new InvalidOperationException($"{nameof(TaskScheduler)} is not set.");
        }
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            this.ThrowIfInvalid();
            if (_taskScheduler is not null)
            {
                throw new InvalidOperationException($"{nameof(TaskScheduler)} is already set.");
            }

            _taskScheduler = value;
        }
    }

    public IDownloadService DownloadService
    {
        get
        {
            return _downloadService ?? throw new InvalidOperationException($"{nameof(DownloadService)} is not set.");
        }
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            this.ThrowIfInvalid();

            _downloadService = value;
        }
    }

    public HttpClient HttpClient
    {
        get
        {
            return _httpClient ?? throw new InvalidOperationException($"{nameof(HttpClient)} is not set.");
        }
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            this.ThrowIfInvalid();

            _httpClient = value;
        }
    }

    public bool RequestSave { get; set; } = true;

    public ReadOnlyObservableCollection<AddonNode> Nodes => _containerService.Nodes;

    public int ProblemCount
    {
        get => _problemCount;
        internal set => NotifyAndSetIfChanged(ref _problemCount, value);
    }

    public Task? CheckTask
    {
        get => _checkTask;
        private set => NotifyAndSetIfChanged(ref _checkTask, value);
    }

    public Task<VpkAddonConflictResult>? CheckVpkAddonConflictsTask
    {
        get => _checkVpkConflictsTask;
        private set => NotifyAndSetIfChanged(ref _checkVpkConflictsTask, value);
    }

    public string DirectoryPath
    {
        get => _directory?.FullName ?? throw new InvalidOperationException($"{nameof(DirectoryPath)} is not set.");
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            this.ThrowIfInvalid();
            if (_directory is not null)
            {
                throw new InvalidOperationException($"{nameof(DirectoryPath)} is already set.");
            }

            _directory = new(value);
            _directory.Create();
            RequestSave = true;

            Directory.CreateDirectory(CacheDirectoryPath);
        }
    }

    public bool IsDirectoryPathSet => _directory is not null;

    string? IAddonNodeContainer.FileSystemPath => IsDirectoryPathSet ? DirectoryPath : null;

    public string CacheDirectoryPath => Path.Join(DirectoryPath, AddonRootDirectoryName, CacheDirectoryName);

    public string BackupDirectoryPath => Path.Join(DirectoryPath, AddonRootDirectoryName, BackupDirectoryName);

    public string GamePath
    {
        get => _gamePath;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            _gamePath = value;
        }
    }

    public ReadOnlyObservableCollection<string> CustomTags => _customTagsReadOnly;

    public bool IsAutoCheckEnabled => _blockAutoCheck == 0;

    public IAddonRootParentSettings? ParentSettings { get; set; } = null;

    public bool IsAutoUpdateWorkshopItem => ParentSettings?.IsAutoUpdateWorkshopItem ?? false;

    public VpkAddonConflictCheckSettings VpkAddonConflictCheckSettings => ParentSettings?.VpkAddonConflictCheckSettings ?? VpkAddonConflictCheckSettings.Default;

    IAddonNodeContainer? IAddonNodeContainer.Parent => null;

    AddonRoot IAddonNodeContainer.Root => this;

    internal bool IsUnstable { get; set; } = false;

    public event Action<AddonNode>? DescendantNodeMoved = null;

    public event Action<AddonNode>? DescendantNodeCreated = null;

    public event Action<AddonNode>? DescendantNodeDestructionStarted = null;

    public event Action<AddonNode>? DescendantNodeDestroyed = null;

    public event Action<AddonNode>? NewNodeIdRegistered = null;

    public event Action<AddonProblem>? ProblemProduced = null;

    public event Action? Pushed = null;

    public bool TryGetNodeById(Guid id, [NotNullWhen(true)] out AddonNode? node)
    {
        this.ThrowIfInvalid();

        return _idToNode.TryGetValue(id, out node);
    }

    public bool ContainsNodeId(Guid id)
    {
        this.ThrowIfInvalid();

        return _idToNode.ContainsKey(id);
    }

    public AddonNode? TryGetNodeByName(string name)
    {
        this.ThrowIfInvalid();

        return _containerService.TryGetByName(name);
    }

    public AddonNode? TryGetNodeByPath(string path)
    {
        this.ThrowIfInvalid();

        return _containerService.TryGetByPath(path);
    }

    public string ConvertFilePathToNodePath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        this.ThrowIfInvalid();

        path = Path.ChangeExtension(path, null);
        path = Path.GetRelativePath(DirectoryPath, path);
        path = FileSystemUtils.NormalizePath(path);
        return path;
    }

    public bool AddCustomTag(string? tag)
    {
        this.ThrowIfInvalid();

        if (string.IsNullOrEmpty(tag))
        {
            return false;
        }

        if (AddonTags.BuiltInTags.Contains(tag))
        {
            return false;
        }

        if (!_customTagSet.Add(tag))
        {
            return false;
        }
        _customTags.Add(tag);
        return true;
    }

    public bool RemoveCustomTag(string? tag)
    {
        this.ThrowIfInvalid();

        if (string.IsNullOrEmpty(tag))
        {
            return false;
        }

        bool result = _customTagSet.Remove(tag);
        if (result)
        {
            _customTags.Remove(tag);
        }
        return result;
    }

    public bool RemoveCustomTagCompletely(string tag)
    {
        ArgumentNullException.ThrowIfNull(tag);

        this.ThrowIfInvalid();

        if (RemoveCustomTag(tag))
        {
            foreach (var node in this.GetDescendants())
            {
                node.RemoveTag(tag);
            }
            return true;
        }
        else
        {
            return false;
        }
    }

    public void MoveCustomTag(int oldIndex, int newIndex)
    {
        this.ThrowIfInvalid();

        _customTags.Move(oldIndex, newIndex);
    }

    public void RefreshCustomTags()
    {
        this.ThrowIfInvalid();

        foreach (var node in this.GetDescendants())
        {
            foreach (var tag in node.Tags)
            {
                AddCustomTag(tag);
            }
        }
    }

    public void RenameCustomTag(string oldTag, string newTag)
    {
        ArgumentNullException.ThrowIfNull(oldTag);
        ArgumentNullException.ThrowIfNull(newTag);
        if (newTag.Length == 0)
        {
            throw new ArgumentException("empty tag string");
        }

        this.ThrowIfInvalid();

        if (oldTag == newTag)
        {
            return;
        }

        int idx = _customTags.Count;
        if (_customTagSet.Remove(oldTag))
        {
            idx = _customTags.IndexOf(oldTag);
            _customTags.RemoveAt(idx);
        }
        if (_customTagSet.Add(newTag))
        {
            _customTags.Insert(idx, newTag);
        }

        foreach (var node in this.GetDescendants())
        {
            node.RenameTag(oldTag, newTag);
        }
    }

    public string GetUniqueChildName(string name, bool ignoreFileSystem = false)
    {
        this.ThrowIfInvalid();

        return _containerService.GetUniqueChildName(name, ignoreFileSystem);
    }

    public void Check()
    {
        this.ThrowIfInvalid();

        var tasks = new List<Task>();

        this.CheckDescendants();
        foreach (var node in this.GetDescendants())
        {
            if (node.CheckTask is { } task)
            {
                tasks.Add(task);
            }
        }

        tasks.Add(CheckVpkAddonConflictsAsync());

        var checkTask = Task.WhenAll(tasks);
        CheckTask = checkTask;
        checkTask.ContinueWith(_ =>
        {
            if (CheckTask == checkTask)
            {
                CheckTask = null;
            }

            if (checkTask.Exception is { } ex)
            {
                Log.Error(ex, "Exception occurred during checking the AddonRoot.");
            }
        }, TaskScheduler);
    }

    public Task<VpkAddonConflictResult> CheckVpkAddonConflictsAsync()
    {
        this.ThrowIfInvalid();

        if (CheckVpkAddonConflictsTask is { } task)
        {
            return task;
        }

        _checkVpkConflictsCts ??= new();
        var cancellationToken = _checkVpkConflictsCts.Token;

        var rawTask = AddonConflictUtils.CheckVpkConflictsAsync(Nodes, VpkAddonConflictCheckSettings, cancellationToken);

        foreach (var addon in this.GetDescendants())
        {
            if (addon is VpkAddon vpkAddon)
            {
                vpkAddon.InvalidateProblem<VpkAddonConflictProblem>();
                vpkAddon._conflictingAddonIdsWithFiles.Clear();
                vpkAddon._conflictingFilesWithAddonIds.Clear();
            }
        }

        var addonTaskCreator = this.GetValidTaskCreator();

        var checkTask = Task.Run(async () =>
        {
            var conflictResult = await rawTask.ConfigureAwait(false);
            await addonTaskCreator.StartNew(self =>
            {
                if (conflictResult.HasConflict)
                {
                    foreach (var vpkAddon in conflictResult.ConflictingAddons)
                    {
                        vpkAddon.SetProblem(new VpkAddonConflictProblem(vpkAddon));

                        vpkAddon._conflictingFilesWithAddonIds.Clear();
                        foreach (var (file, addons) in conflictResult.GetConflictingFilesWithAddons(vpkAddon))
                        {
                            vpkAddon._conflictingFilesWithAddonIds.Add((file, addons.Select(addon => addon.Id).ToArray()));
                        }

                        vpkAddon._conflictingAddonIdsWithFiles.Clear();
                        foreach (var (addon, files) in conflictResult.GetConflictingAddonsWithFiles(vpkAddon))
                        {
                            vpkAddon._conflictingAddonIdsWithFiles.Add((addon.Id, files));
                        }
                    }
                }
            }).ConfigureAwait(false);
            return conflictResult;
        }, cancellationToken);
        CheckVpkAddonConflictsTask = checkTask;
        checkTask.ContinueWith(_ =>
        {
            if (CheckVpkAddonConflictsTask == checkTask)
            {
                CheckVpkAddonConflictsTask = null;
            }

            if (checkTask.Exception is { } ex)
            {
                Log.Error(ex, "Exception occurred during checking VPK conflicts.");
            }
        }, TaskScheduler);
        return checkTask;
    }

    public void CancelCheckVpkConflicts()
    {
        CheckVpkAddonConflictsTask = null;
        if (_checkVpkConflictsCts is not null)
        {
            _checkVpkConflictsCts.Cancel();
            _checkVpkConflictsCts.Dispose();
            _checkVpkConflictsCts = null;
        }
    }

    private abstract class ImportItem
    {
        protected ImportItem(string filePath, AddonGroup? group)
        {
            FilePath = filePath;
            Group = group;
        }

        public string FilePath { get; }

        public AddonGroup? Group { get; }

        public abstract AddonNode Create(AddonRoot addonRoot, out string? newFilePath);
    }

    private abstract class ImportItem<T> : ImportItem where T : AddonNode
    {
        public ImportItem(string filePath, AddonGroup? group) : base(filePath, group)
        {

        }

        public sealed override AddonNode Create(AddonRoot addonRoot, out string? newFilePath)
        {
            newFilePath = null;

            var addon = AddonNode.Create<T>(addonRoot, Group);

            var name = Path.GetFileNameWithoutExtension(FilePath);
            name = addon.Parent.GetUniqueChildName(name, true);
            addon.Name = name;

            OnCreate(addon);

            var currentFilePath = addon.FullFilePath;
            if (!FileSystemUtils.IsSamePath(FilePath, currentFilePath))
            {
                FileSystemUtils.Move(FilePath, currentFilePath);
                newFilePath = currentFilePath;
            }

            return addon;
        }

        protected abstract void OnCreate(T addon);
    }

    private class LocalVpkImportItem : ImportItem<LocalVpkAddon>
    {
        public LocalVpkImportItem(string filePath, AddonGroup? group) : base(filePath, group)
        {

        }

        protected override void OnCreate(LocalVpkAddon addon)
        {
            
        }
    }

    private class WorkshopVpkImportItem : ImportItem<WorkshopVpkAddon>
    {
        public WorkshopVpkImportItem(string filePath, AddonGroup? group, ulong publishedFileId) : base(filePath, group)
        {
            PublishedFileId = publishedFileId;
        }

        public ulong PublishedFileId { get; }

        protected override void OnCreate(WorkshopVpkAddon addon)
        {
            addon.PublishedFileId = PublishedFileId;
        }
    }

    public abstract class ImportResultItem
    {
        internal ImportResultItem(AddonRoot addonRoot, string filePath, string? newFilePath)
        {
            var addonRootDirPath = addonRoot.DirectoryPath;
            RelativeFilePath = Path.GetRelativePath(addonRootDirPath, filePath);
            if (newFilePath is not null)
            {
                NewRelativeFilePath = Path.GetRelativePath(addonRootDirPath, newFilePath);
            }
        }

        public abstract bool Success { get; }

        public string RelativeFilePath { get; }

        public string? NewRelativeFilePath { get; } = null;

        public abstract AddonNode Addon { get; }

        public abstract Exception Exception { get; }
    }

    private class SuccessfulImportResultItem : ImportResultItem
    {
        internal SuccessfulImportResultItem(AddonRoot addonRoot, string filePath, string? newFilePath, AddonNode addon) : base(addonRoot, filePath, newFilePath)
        {
            Addon = addon;
        }

        public override bool Success => true;

        public override AddonNode Addon { get; }

        public override Exception Exception => throw new InvalidOperationException($"{nameof(Exception)} is not set.");
    }

    private class FailedImportResultItem : ImportResultItem
    {
        internal FailedImportResultItem(AddonRoot addonRoot, string filePath, string? newFilePath, Exception exception) : base(addonRoot, filePath, newFilePath)
        {
            Exception = exception;
        }

        public override bool Success => false;

        public override AddonNode Addon => throw new InvalidOperationException($"{nameof(Addon)} is not set.");

        public override Exception Exception { get; }
    }

    public class ImportResult
    {
        internal ImportResult(IReadOnlyList<ImportResultItem> items)
        {
            Items = items;
        }

        public IReadOnlyList<ImportResultItem> Items { get; }

        public bool HasFailure
        {
            get
            {
                foreach (var item in Items)
                {
                    if (!item.Success)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public int SuccessCount
        {
            get
            {
                int count = 0;
                foreach (var item in Items)
                {
                    if (item.Success)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        public int FailureCount
        {
            get
            {
                int count = 0;
                foreach (var item in Items)
                {
                    if (!item.Success)
                    {
                        count++;
                    }
                }
                return count;
            }
        }
    }

    public ImportResult Import(AddonGroup? group = null)
    {
        this.ThrowIfInvalid();

        if (group is not null)
        {
            group.ThrowIfInvalid();
            if (group.Root != this)
            {
                throw new InvalidOperationException($"different {nameof(AddonRoot)}");
            }
        }

        var fileFinder = group == null ? new AddonNodeFileFinder(this) : new AddonNodeFileFinder(group);
        var importItems = new List<ImportItem>();
        bool skipDir = false;
        while (fileFinder.MoveNext(skipDir))
        {
            skipDir = false;
            if (fileFinder.CurrentNodeExists)
            {
                continue;
            }

            var filePath = fileFinder.CurrentFilePath;
            var fileExtension = Path.GetExtension(filePath);
            if (fileFinder.IsCurrentDirectory)
            {
                if (fileExtension == ".workshop")
                {
                    string metaInfoFilePath = Path.Join(filePath, WorkshopVpkAddon.MetaInfoFileName);
                    WorkshopVpkMetaInfo? metaInfo = null;
                    try
                    {
                        if (File.Exists(metaInfoFilePath))
                        {
                            metaInfo = JsonConvert.DeserializeObject<WorkshopVpkMetaInfo>(File.ReadAllText(metaInfoFilePath), WorkshopVpkAddon.s_metaInfoJsonSettings);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Exception occurred during reading workshop meta info file: {FilePath}", metaInfoFilePath);
                    }
                    if (metaInfo != null)
                    {
                        skipDir = true;
                        importItems.Add(new WorkshopVpkImportItem(filePath, fileFinder.GetOrCreateCurrentGroup(), metaInfo.PublishedFileId));
                    }
                }
            }
            else
            {
                if (fileExtension == ".vpk")
                {
                    importItems.Add(new LocalVpkImportItem(filePath, fileFinder.GetOrCreateCurrentGroup()));
                }
            }
        }

        var resultItems = new ImportResultItem[importItems.Count];
        for (int i = 0, len = importItems.Count; i < len; i++)
        {
            var importItem = importItems[i];
            string? newFilePath = null;
            AddonNode addon;
            try
            {
                addon = importItem.Create(this, out newFilePath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to import the addon file: {FilePath}", importItem.FilePath);
                resultItems[i] = new FailedImportResultItem(this, importItem.FilePath, newFilePath, ex);
                continue;
            }
            resultItems[i] = new SuccessfulImportResultItem(this, importItem.FilePath, newFilePath, addon);
        }

        return new ImportResult(resultItems);
    }

    public void Push()
    {
        this.ThrowIfInvalid();

        var gamePath = EnsureValidGamePath();
        string addonsPath = GamePathUtils.GetAddonsPath(gamePath);

        // Delete old link files.
        var remainingFilePaths = new List<string>();
        {
            var filePathsToDelete = new List<string>();
            foreach (string filePath in Directory.EnumerateFiles(addonsPath))
            {
                if ((File.GetAttributes(filePath) & FileAttributes.ReparsePoint) == 0)
                {
                    continue;
                }

                if (filePath.EndsWith(".vpk"))
                {
                    string fileNameNoExt = Path.GetFileNameWithoutExtension(filePath);

                    if (TryParseLinkVpkFileNameNoExt(fileNameNoExt))
                    {
                        filePathsToDelete.Add(filePath);
                    }
                    else if (TryParseLegacyLocalVpkFileNameNoExt(fileNameNoExt, out _))
                    {
                        filePathsToDelete.Add(filePath);
                    }
                    else if (TryParseLegacyWorkshopVpkFileNameNoExt(fileNameNoExt, out _))
                    {
                        filePathsToDelete.Add(filePath);
                    }
                }
            }
            foreach (string filePath in filePathsToDelete)
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception ex)
                {
                    Log.Information(ex, "Exception occurred during deleting file {FilePath}.", filePath);
                }
                if (File.Exists(filePath))
                {
                    remainingFilePaths.Add(filePath);
                }
            }
        }

        // Read addonlist.txt.
        string addonListPath = GamePathUtils.GetAddonListPath(gamePath);
        KVObject? addonList = null;
        var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
        if (File.Exists(addonListPath))
        {
            try
            {
                using (var stream = File.OpenRead(addonListPath))
                {
                    addonList = kv.Deserialize(stream);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Exception occurred during deserializing addonlist.txt.");
            }
        }

        var addonEntries = new Dictionary<string, (string IsEnabled, int Priority)>();           
        // Load the content of the addonlist and ignore old entries in addonlist.
        if (addonList != null)
        {
            foreach (var obj in addonList)
            {
                string name = obj.Name;
                if (name.EndsWith(".vpk"))
                {
                    string nameNoExt = name.Substring(0, name.Length - ".vpk".Length);

                    if (TryParseLinkVpkFileNameNoExt(nameNoExt))
                    {
                        continue;
                    }
                    if (TryParseLegacyLocalVpkFileNameNoExt(nameNoExt, out _))
                    {
                        continue;
                    }
                    if (TryParseLegacyWorkshopVpkFileNameNoExt(nameNoExt, out _))
                    {
                        continue;
                    }
                }
                addonEntries[name] = ((string)obj.Value, 0);
            }
        }

        // Add remaining files to entries.
        foreach (string filePath in remainingFilePaths)
        {
            string fileName = Path.GetFileName(filePath);
            addonEntries[fileName] = ("0", 0);
        }

        // Add enabled addons to entries.
        int nextLinkVpkFileId = 1;
        foreach (var addon in Nodes.SelectMany(addon => addon.GetAllNodesEnabledInHierarchy()))
        {
            var actualAddon = addon;

            if (addon is RefAddonNode refAddon)
            {
                var sourceAddon = refAddon.ActualSourceAddon;
                if (sourceAddon is null)
                {
                    continue;
                }
                actualAddon = sourceAddon;
            }

            if (actualAddon is VpkAddon vpkAddon)
            {
                string? vpkPath = vpkAddon.FullVpkFilePath;

                if (vpkPath == null || !File.Exists(vpkPath))
                {
                    continue;
                }

                var linkFilePath = BuildLinkVpkFilePath(addonsPath, ref nextLinkVpkFileId);
                File.CreateSymbolicLink(linkFilePath, vpkPath);
                var linkFileName = Path.GetFileName(linkFilePath);
                addonEntries[linkFileName] = ("1", addon.PriorityInHierarchy);
            }
        }

        // Finally update addonlist.txt.
        addonList = new KVObject("AddonList", 
            addonEntries.Select(pair => new 
            {
                Key = pair.Key, IsEnabled = pair.Value.IsEnabled, Priority = pair.Value.Priority
            })
            .OrderByDescending(obj => obj.Priority)
            .Select(obj => new KVObject(obj.Key, obj.IsEnabled)));
        using (var stream = File.Create(addonListPath))
        {
            kv.Serialize(stream, addonList);
        }

        Pushed?.Invoke();

        // File name format-related functions
        string BuildLinkVpkFilePath(string addonsPath, ref int nextId)
        {
            while (true)
            {
                var path = Path.Join(addonsPath, $"fireaxe_link_{nextId:x}.vpk");
                checked { nextId++; }
                if (!FileSystemUtils.Exists(path))
                {
                    return path;
                }
            }
        }

        bool TryParseLinkVpkFileNameNoExt(string nameNoExt)
        {
            return s_pushedLinkVpkFileNameNoExtRegex.IsMatch(nameNoExt);
        }

        // Legacy Formats
        bool TryParseLegacyLocalVpkFileNameNoExt(string nameNoExt, out Guid guid)
        {
            const int GuidLength = 32;
            const string Prefix = "local_";

            guid = default;
            if (nameNoExt.Length == (Prefix.Length + GuidLength) && nameNoExt.StartsWith(Prefix))
            {
                var guidStr = nameNoExt.AsSpan().Slice(Prefix.Length);
                if (Guid.TryParse(guidStr, out guid))
                {
                    return true;
                }
            }
            return false;
        }

        bool TryParseLegacyWorkshopVpkFileNameNoExt(string nameNoExt, out ulong publishedFileId)
        {
            const string Prefix = "workshop_";

            publishedFileId = 0;
            if (nameNoExt.Length > Prefix.Length && nameNoExt.StartsWith(Prefix))
            {
                var idStr = nameNoExt.AsSpan().Slice(Prefix.Length);
                if (ulong.TryParse(idStr, out publishedFileId))
                {
                    return true;
                }
            }
            return false;
        }

        //string BuildLocalVpkFileName(Guid guid)
        //{
        //    return $"local_{guid:N}.vpk";
        //}

        //string BuildWorkshopVpkFileName(ulong publishedFileId)
        //{
        //    return $"workshop_{publishedFileId}.vpk";
        //}
    }

    public bool BackUpIfNeed(int maxRetainedFileCount, int backupIntervalMinutes, DateTime? overrideCurrentDateTime = null)
    {
        if (maxRetainedFileCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRetainedFileCount));
        }
        if (backupIntervalMinutes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(backupIntervalMinutes));
        }

        var dir = BackupDirectoryPath;
        Directory.CreateDirectory(dir);

        DateTime currentDateTime = overrideCurrentDateTime ?? DateTime.Now;

        // Find existing backup files.
        var existingItems = new List<(string Path, DateTime DateTime)>();
        foreach (var path in Directory.EnumerateFiles(dir))
        {
            var fileName = Path.GetFileName(path);
            if (TryParseBackupFileName(fileName, out var dateTime))
            {
                existingItems.Add((path, dateTime));
            }
        }

        // Sort existing items by date time, check the backup interval, and compare self to the latest backup file.
        string? content = null;
        if (existingItems.Count > 0)
        {
            existingItems.Sort((a, b) => a.DateTime.Ticks.CompareTo(b.DateTime.Ticks));

            // Ignore future items.
            while (existingItems.Count > 0 && existingItems[existingItems.Count - 1].DateTime > currentDateTime)
            {
                existingItems.RemoveAt(existingItems.Count - 1);
            }

            if (existingItems.Count > 0)
            {
                // Check the backup interval.
                var latestDateTime = existingItems[existingItems.Count - 1].DateTime;
                if (currentDateTime - latestDateTime < TimeSpan.FromMinutes(backupIntervalMinutes))
                {
                    return false;
                }

                // The backup file won't be created if it's the same as the latest one.
                content = Serialize(CreateSave());
                var latestFilePath = existingItems[existingItems.Count - 1].Path;
                var latestContent = File.ReadAllText(latestFilePath);
                if (content == latestContent)
                {
                    return false;
                }
            }
        }

        // Delete overflowed items.
        int overflowedCount = existingItems.Count + 1 - maxRetainedFileCount;
        if (overflowedCount > 0)
        {
            for (int i = 0; i < overflowedCount; i++)
            {
                var path = existingItems[i].Path;
                if (File.Exists(path))
                {
                    FileSystemUtils.MoveToRecycleBin(path);
                }
            }
        }

        // Finally create the backup.
        content ??= Serialize(CreateSave());
        var backupPath = Path.Join(dir, BuildBackupFileName(currentDateTime));
        File.WriteAllText(backupPath, content);
        return true;
    }

    public static string BuildBackupFileName(DateTime dateTime)
    {
        return $"backup_{dateTime.Year}-{dateTime.Month:D2}-{dateTime.Day:D2}_{dateTime.Hour:D2}-{dateTime.Minute:D2}.addonroot";
    }

    public static bool TryParseBackupFileName(string? input, out DateTime dateTime)
    {
        dateTime = default;
        if (input is null)
        {
            return false;
        }
        var match = s_backupFileNameRegex.Match(input);
        if (match.Success)
        {
            var groups = match.Groups;
            if (int.TryParse(groups[1].ValueSpan, out int year)
                && int.TryParse(groups[2].ValueSpan, out int month)
                && int.TryParse(groups[3].ValueSpan, out int day)
                && int.TryParse(groups[4].ValueSpan, out int hour)
                && int.TryParse(groups[5].ValueSpan, out int minute))
            {
                try
                {
                    dateTime = new DateTime(year, month, day, hour, minute, 0);
                }
                catch (ArgumentOutOfRangeException)
                {
                    return false;
                }
                return true;
            }
        }
        return false;
    }

    public IEnumerable<string> EnumerateUserFileSystemEntries()
    {
        if (!IsDirectoryPathSet)
        {
            yield break;
        }
        var dirPath = DirectoryPath;

        foreach (var path in Directory.EnumerateFileSystemEntries(dirPath))
        {
            var name = Path.GetFileName(path);
            if (name != SaveFileName && name != VersionFileName && name != AddonRootDirectoryName)
            {
                yield return path;
            }
        }

        bool ShouldEnterDirectory(string path)
            => TryGetNodeByPath(ConvertFilePathToNodePath(path)) is null or AddonGroup;

        var dirQueue = new Queue<string>();
        foreach (var topDirPath in Directory.EnumerateDirectories(dirPath))
        {
            var topDirName = Path.GetFileName(topDirPath);
            if (topDirName != AddonRootDirectoryName && ShouldEnterDirectory(topDirPath))
            {
                dirQueue.Enqueue(topDirPath);
            }
        }

        while (dirQueue.Count > 0)
        {
            var subDirPath = dirQueue.Dequeue();

            foreach (var subDirPath2 in Directory.EnumerateDirectories(subDirPath))
            {
                yield return subDirPath2;
                if (ShouldEnterDirectory(subDirPath2))
                {
                    dirQueue.Enqueue(subDirPath2);
                }
            }

            foreach (var filePath in Directory.EnumerateFiles(subDirPath))
            {
                yield return filePath;
            }
        }
    }

    public void LoadFile()
    {
        this.ThrowIfInvalid();

        var save = LoadFileFromDirectory(DirectoryPath);
        if (save == null)
        {
            return;
        }
        LoadSave(save);
    }

    public void Save()
    {
        this.ThrowIfInvalid();

        SaveFileToDirectory(DirectoryPath, CreateSave());
    }

    public static void SaveFileToDirectory(string dirPath, AddonRootSave save)
    {
        ArgumentNullException.ThrowIfNull(dirPath);
        ArgumentNullException.ThrowIfNull(save);

        string path = Path.Join(dirPath, SaveFileName);
        string content = Serialize(save); 
        File.WriteAllText(path, content);

        string versionPath = Path.Join(dirPath, VersionFileName);
        string version = typeof(AddonRoot).Assembly.GetName().Version!.ToString(3);
        File.WriteAllText(versionPath, version);
    }

    public static AddonRootSave? LoadFileFromDirectory(string dirPath)
    {
        ArgumentNullException.ThrowIfNull(dirPath);

        string path = Path.Join(dirPath, SaveFileName);
        if (!File.Exists(path))
        {
            return null;
        }

        string content = File.ReadAllText(path);
        return Deserialize(content);
    }

    public static string Serialize(AddonRootSave save)
    {
        ArgumentNullException.ThrowIfNull(save);

        return JsonConvert.SerializeObject(save, s_jsonSettings);
    }

    public static AddonRootSave Deserialize(string str)
    {
        ArgumentNullException.ThrowIfNull(str);

        JObject jobj;
        try
        {
            jobj = JObject.Parse(str);
        }
        catch (Exception ex)
        {
            throw new AddonRootDeserializationException("Exception occurred during parsing the JSON text.", ex);
        }

        // handle old version content
        foreach (var jtoken in jobj.Descendants())
        {
            if (jtoken is JProperty jproperty)
            {
                if (jproperty.Name == "$type" && jproperty.Value is JValue jvalue && jvalue.Value is string typeValue)
                {
                    jvalue.Value = typeValue.Replace("L4D2AddonAssistant", "FireAxe");
                }
            }
        }

        AddonRootSave? save;
        try
        {
            save = jobj.ToObject<AddonRootSave>(JsonSerializer.Create(s_jsonSettings));
        }
        catch (Exception ex)
        {
            throw new AddonRootDeserializationException($"Exception occurred during converting {nameof(JObject)} to {nameof(AddonRootSave)}.", ex);
        }
        if (save is null)
        {
            throw new AddonRootDeserializationException($"The result {nameof(AddonRootSave)} is null.");
        }
        return save;
    }

    public AddonRootSave CreateSave()
    {
        this.ThrowIfInvalid();

        var save = new AddonRootSave();
        save.Nodes = Nodes.Select(node => node.CreateSave()).ToArray();
        save.CustomTags = [.. CustomTags];
        return save;
    }

    public void LoadSave(AddonRootSave save)
    {
        ArgumentNullException.ThrowIfNull(save);

        this.ThrowIfInvalid();

        foreach (var nodeSave in save.Nodes)
        {
            AddonNode.LoadSave(nodeSave, this);
        }
        foreach (var tag in save.CustomTags)
        {
            AddCustomTag(tag);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            BlockAutoCheck();

            CancelCheckVpkConflicts();

            var tasks = new List<Task>();

            if (CheckTask is { } checkTask)
            {
                tasks.Add(checkTask);
            }

            foreach (var node in Nodes)
            {
                tasks.Add(node.DestroyAsync());
            }

            _disposed = true;
            NotifyChanged(nameof(IsValid));

            await TaskUtils.WhenAllIgnoreCanceled(tasks).ConfigureAwait(false);
        }
    }

    public IDisposable BlockAutoCheck()
    {
        this.ThrowIfInvalid();

        _blockAutoCheck++;
        bool disposed = false;
        return DisposableUtils.Create(() =>
        {
            if (!disposed)
            {
                disposed = true;
                _blockAutoCheck--;
            }
        });
    }

    public void NotifyDownloadItem(IDownloadItem downloadItem)
    {
        ArgumentNullException.ThrowIfNull(downloadItem);

        this.ThrowIfInvalid();

        NewDownloadItem?.Invoke(downloadItem);
    }

    internal void RunUnstable(Action action)
    {
        ThrowIfUnstable();
        IsUnstable = true;
        try
        {
            action();
        }
        finally
        {
            IsUnstable = false;
        }
    }

    internal async Task RunUnstableAsync(Func<Task> action)
    {
        ThrowIfUnstable();
        IsUnstable = true;
        try
        {
            await action();
        }
        finally
        {
            IsUnstable = false;
        }
    }

    internal void ThrowIfUnstable()
    {
        if (IsUnstable)
        {
            throw new InvalidOperationException($"The {nameof(AddonRoot)} instance is unstable now.");
        }
    }
    
    internal void AddNode(AddonNode node)
    {
        _containerService.AddUnchecked(node);
    }

    internal void RemoveNode(AddonNode node)
    {
        _containerService.Remove(node);
    }

    void IAddonNodeContainerInternal.ThrowIfChildNewNameDisallowed(string name, AddonNode child)
    {
        _containerService.ThrowIfChildNewNameDisallowed(name, child);
    }

    void IAddonNodeContainerInternal.ChangeChildNameUnchecked(string? oldName, string newName, AddonNode child)
    {
        _containerService.ChangeChildNameUnchecked(oldName, newName, child);
    }

    void IAddonNodeContainerInternal.NotifyDescendantNodeMoved(AddonNode node)
    {
        DescendantNodeMoved?.Invoke(node);
    }

    internal void NotifyDescendantNodeCreated(AddonNode node)
    {
        DescendantNodeCreated?.Invoke(node);
    }

    internal void NotifyDescendantNodeDestructionStarted(AddonNode node)
    {
        DescendantNodeDestructionStarted?.Invoke(node);
    }

    internal void NotifyDescendantNodeDestroyed(AddonNode node)
    {
        DescendantNodeDestroyed?.Invoke(node);
    }

    internal void RegisterNodeId(Guid newId, Guid oldId, AddonNode node)
    {
        if (newId != Guid.Empty)
        {
            if (_idToNode.ContainsKey(newId))
            {
                throw new AddonNodeIdExistsException(newId, node);
            }
            _idToNode[newId] = node;
        }
        if (oldId != Guid.Empty)
        {
            _idToNode.Remove(oldId);
        }
    }

    internal void UnregisterNodeId(Guid id)
    {
        if (id == Guid.Empty)
        {
            return;
        }

        _idToNode.Remove(id);
    }

    internal void NotifyNewNodeIdRegistered(AddonNode node)
    {
        NewNodeIdRegistered?.Invoke(node);
    }

    internal void NotifyProblemProduced(AddonProblem problem)
    {
        ProblemProduced?.Invoke(problem);
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RequestSave = true;
    }

    private void OnCustomTagsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RequestSave = true;
    }

    private string EnsureValidGamePath()
    {
        if (!GamePathUtils.CheckValidity(_gamePath))
        {
            throw new InvalidGamePathException();
        }
        return _gamePath;
    }
}
