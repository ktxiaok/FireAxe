using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using ValveKeyValue;

namespace FireAxe;

public sealed class AddonRoot : ObservableObject, IAsyncDisposable, IAddonNodeContainer, IAddonNodeContainerInternal, ISaveable, IValidity
{
    public const string SaveFileName = ".addonroot";
    public const string VersionFileName = ".addonrootversion";

    private static readonly JsonSerializerSettings s_jsonSettings = new()
    {
        Formatting = Formatting.Indented,
        TypeNameHandling = TypeNameHandling.Auto,
        Converters =
        {
            new StringEnumConverter()
        }
    };

    private bool _disposed = false;

    private DirectoryInfo? _directory = null;

    private string _gamePath = "";

    private readonly ObservableCollection<string> _customTags = new();
    private readonly ReadOnlyObservableCollection<string> _customTagsReadOnly;
    private readonly HashSet<string> _customTagSet = new();

    private readonly Dictionary<Guid, AddonNode> _idToNode = new();

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

    public string CacheDirectoryPath => Path.Join(DirectoryPath, ".addonrootdir", "caches");

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

    public bool IsAutoUpdateWorkshopItem { get; set; } = true;

    public bool IsCheckingVpkConflicts => CheckVpkConflictsTask is not null;

    private Task<VpkAddonConflictResult>? CheckVpkConflictsTask
    {
        get => _checkVpkConflictsTask;
        set
        {
            _checkVpkConflictsTask = value;
            NotifyChanged(nameof(IsCheckingVpkConflicts));
        }
    }

    IAddonNodeContainer? IAddonNodeContainer.Parent => null;

    AddonRoot IAddonNodeContainer.Root => this;

    internal bool IsUnstable { get; set; } = false;

    public event Action<AddonNode>? DescendantNodeMoved = null;

    public event Action<AddonNode>? DescendantNodeCreated = null;

    public event Action<AddonNode>? DescendantNodeDestructionStarted = null;

    public event Action<AddonNode>? DescendantNodeDestroyed = null;

    public event Action<AddonNode>? NewNodeIdRegistered = null;

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

    public bool AddCustomTag(string tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        if (tag.Length == 0)
        {
            throw new ArgumentException("empty tag string");
        }

        this.ThrowIfInvalid();

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

    public bool RemoveCustomTag(string tag)
    {
        ArgumentNullException.ThrowIfNull(tag);

        this.ThrowIfInvalid();

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

    public string GetUniqueNodeName(string name)
    {
        this.ThrowIfInvalid();

        return _containerService.GetUniqueName(name);
    }

    public void Check()
    {
        this.ThrowIfInvalid();

        this.CheckDescendants();
        CheckVpkConflictsAsync();
    }

    public Task<VpkAddonConflictResult> CheckVpkConflictsAsync()
    {
        this.ThrowIfInvalid();

        if (CheckVpkConflictsTask is { } task)
        {
            return task;
        }

        _checkVpkConflictsCts ??= new();
        var cancellationToken = _checkVpkConflictsCts.Token;

        var rawTask = AddonConflictUtils.FindVpkConflicts(Nodes, cancellationToken);

        foreach (var addon in this.GetDescendants())
        {
            if (addon is VpkAddon vpkAddon)
            {
                vpkAddon._vpkAddonConflictProblemSource.Clear();
                vpkAddon._conflictingAddonIdsWithFiles.Clear();
                vpkAddon._conflictingFilesWithAddonIds.Clear();
            }
        }

        var addonRootTaskScheduler = TaskScheduler;

        CheckVpkConflictsTask = Task.Run(async () =>
        {
            var conflictResult = await rawTask.ConfigureAwait(false);
            var endingTask = new Task(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsValid)
                {
                    return;
                }

                if (conflictResult.HasConflict)
                {
                    foreach (var vpkAddon in conflictResult.ConflictingAddons)
                    {
                        new VpkAddonConflictProblem(vpkAddon._vpkAddonConflictProblemSource).Submit();

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
            }, cancellationToken);
            endingTask.Start(addonRootTaskScheduler);
            await endingTask.ConfigureAwait(false);
            return conflictResult;
        }, cancellationToken);
        CheckVpkConflictsTask.ContinueWith(task =>
        {
            if (CheckVpkConflictsTask == task)
            {
                CheckVpkConflictsTask = null;
            }
        }, addonRootTaskScheduler);
        return CheckVpkConflictsTask;
    }

    public void CancelCheckVpkConflicts()
    {
        CheckVpkConflictsTask = null;
        if (_checkVpkConflictsCts is not null)
        {
            _checkVpkConflictsCts.Cancel();
            _checkVpkConflictsCts.Dispose();
            _checkVpkConflictsCts = null;
        }
    }

    private abstract class ImportItem
    {
        public abstract void Create(AddonRoot root);
    }

    private class LocalVpkImportItem : ImportItem
    {
        public string Name;
        public AddonGroup? Group;

        public LocalVpkImportItem(string name, AddonGroup? group)
        {
            Name = name;
            Group = group;
        }

        public override void Create(AddonRoot root)
        {
            var addon = AddonNode.Create<LocalVpkAddon>(root, Group);
            addon.Name = Name;
            addon.Check();
        }
    }

    private class WorkshopVpkImportItem : ImportItem
    {
        public string Name;
        public AddonGroup? Group;
        public ulong PublishedFileId;

        public WorkshopVpkImportItem(string name, AddonGroup? group, ulong publishedFileId)
        {
            Name = name;
            Group = group;
            PublishedFileId = publishedFileId;
        }

        public override void Create(AddonRoot root)
        {
            var addon = AddonNode.Create<WorkshopVpkAddon>(root, Group);
            addon.Name = Name;
            addon.PublishedFileId = PublishedFileId;
        }
    }

    public void Import(AddonGroup? group = null)
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
        var imports = new List<ImportItem>();
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
                        imports.Add(new WorkshopVpkImportItem(Path.GetFileNameWithoutExtension(filePath), fileFinder.GetOrCreateCurrentGroup(), metaInfo.PublishedFileId));
                    }
                }
            }
            else
            {
                if (fileExtension == ".vpk")
                {
                    imports.Add(new LocalVpkImportItem(Path.GetFileNameWithoutExtension(filePath), fileFinder.GetOrCreateCurrentGroup()));
                }
            }
        }
        foreach (var import in imports)
        {
            import.Create(this);
        }
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

                    if (TryParseLinkVpkFileNameNoExt(fileNameNoExt, out _))
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

                    if (TryParseLinkVpkFileNameNoExt(nameNoExt, out _))
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

                string linkFileName = BuildLinkVpkFileName(addon.Id);
                string linkFilePath = Path.Join(addonsPath, linkFileName);
                if (File.Exists(linkFilePath))
                {
                    continue;
                }
                File.CreateSymbolicLink(linkFilePath, vpkPath);
                addonEntries[linkFileName] = ("1", addon.PriorityInHierarchy);
            }
        }

        // Update addonlist.txt.
        if (!File.Exists(addonListPath))
        {
            File.Create(addonListPath);
        }
        addonList = new KVObject("AddonList", 
            addonEntries.Select(pair => new 
            {
                Key = pair.Key, IsEnabled = pair.Value.IsEnabled, Priority = pair.Value.Priority
            })
            .OrderByDescending(obj => obj.Priority)
            .Select(obj => new KVObject(obj.Key, obj.IsEnabled)));
        using (var stream = File.Open(addonListPath, FileMode.Truncate))
        {
            kv.Serialize(stream, addonList);
        }

        const string LinkVpkFilePrefix = "fireaxe_link_";

        string BuildLinkVpkFileName(Guid guid) => $"{LinkVpkFilePrefix}{guid:N}.vpk";

        bool TryParseLinkVpkFileNameNoExt(string nameNoExt, out Guid guid)
        {
            const int GuidLength = 32;

            guid = default;
            if (nameNoExt.Length == (LinkVpkFilePrefix.Length + GuidLength) && nameNoExt.StartsWith(LinkVpkFilePrefix))
            {
                var guidStr = nameNoExt.AsSpan().Slice(LinkVpkFilePrefix.Length);
                if (Guid.TryParse(guidStr, out guid))
                {
                    return true;
                }
            }
            return false;
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
            throw new AddonRootSerializationException("Exception occurred during parsing the JSON text.", ex);
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
            throw new AddonRootSerializationException($"Exception occurred during converting {nameof(JObject)} to {nameof(AddonRootSave)}.", ex);
        }
        if (save is null)
        {
            throw new AddonRootSerializationException($"The result {nameof(AddonRootSave)} is null.");
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
            if (string.IsNullOrEmpty(tag))
            {
                continue;
            }
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
            foreach (var node in Nodes)
            {
                tasks.Add(node.DestroyAsync());
            }

            _disposed = true;
            NotifyChanged(nameof(IsValid));

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }

    public bool ShouldAutoUpdateWorkshopItem(AutoUpdateStrategy strategy)
    {
        if (strategy == AutoUpdateStrategy.Default)
        {
            return IsAutoUpdateWorkshopItem;
        }
        else
        {
            return strategy == AutoUpdateStrategy.Enabled;
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

    void IAddonNodeContainerInternal.ThrowIfNodeNameInvalid(string name, AddonNode node)
    {
        _containerService.ThrowIfNameInvalid(name, node);
    }

    void IAddonNodeContainerInternal.ChangeNameUnchecked(string? oldName, string newName, AddonNode node)
    {
        _containerService.ChangeNameUnchecked(oldName, newName, node);
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
