using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using ValveKeyValue;

namespace L4D2AddonAssistant
{
    public class AddonRoot : IAsyncDisposable, IAddonNodeContainer, IAddonNodeContainerInternal, ISaveable
    {
        public const string SaveFileName = ".addonroot";

        private static JsonSerializerSettings s_jsonSettings = new()
        {
            Formatting = Formatting.Indented,
            TypeNameHandling = TypeNameHandling.Auto,
            Converters =
            {
                new StringEnumConverter()
            }
        };

        private bool _disposed = false;

        private TaskScheduler? _taskScheduler = null;

        private IDownloadService? _downloadService = null;

        private HttpClient? _httpClient = null;

        private AddonNodeContainerService _containerService = new();

        private DirectoryInfo? _directoryPath = null;

        private string _gamePath = "";

        private int _blockAutoCheck = 0;
        
        public AddonRoot()
        {
            ((INotifyCollectionChanged)Nodes).CollectionChanged += OnCollectionChanged;
        }

        [AllowNull]
        public TaskScheduler TaskScheduler
        {
            get
            {
                if (_taskScheduler == null)
                {
                    throw new InvalidOperationException("TaskScheduler not set");
                }
                return _taskScheduler;
            }
            set
            {
                _taskScheduler = value;
            }
        }

        [AllowNull]
        public IDownloadService DownloadService
        {
            get
            {
                if (_downloadService == null)
                {
                    throw new InvalidOperationException("DownloadService not set");
                }
                return _downloadService;
            }
            set
            {
                _downloadService = value;
            }
        }

        [AllowNull]
        public HttpClient HttpClient
        {
            get
            {
                if (_httpClient == null)
                {
                    throw new InvalidOperationException("HttpClient not set");
                }
                return _httpClient;
            }
            set
            {
                _httpClient = value;
            }
        }

        public bool RequestSave { get; set; } = true;

        public ReadOnlyObservableCollection<AddonNode> Nodes => _containerService.Nodes;

        public string DirectoryPath
        {
            get => _directoryPath?.FullName ?? "";
            set
            {
                _directoryPath = new(value);
                _directoryPath.Create();
                RequestSave = true;
            }
        }

        public string GamePath
        {
            get => _gamePath;
            set
            {
                ArgumentNullException.ThrowIfNull(value);

                _gamePath = value;
            }
        }

        public bool IsAutoCheck => _blockAutoCheck == 0;

        public bool IsAutoUpdateWorkshopItem { get; set; } = true;

        IAddonNodeContainer? IAddonNodeContainer.Parent => null;

        AddonRoot IAddonNodeContainer.Root => this;

        public string GetUniqueNodeName(string name)
        {
            return _containerService.GetUniqueName(name);
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
                new LocalVpkAddon(root, Group) { Name = Name};
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
                new WorkshopVpkAddon(root, Group)
                {
                    Name = Name,
                    PublishedFileId = PublishedFileId
                };
            }
        }

        public void Import(AddonGroup? group = null)
        {
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

                        if (TryParseLocalVpkFileNameNoExt(fileNameNoExt, out _))
                        {
                            filePathsToDelete.Add(filePath);
                        }

                        if (TryParseWorkshopVpkFileNameNoExt(fileNameNoExt, out _))
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

            var addonEntries = new Dictionary<string, string>();           
            // Remove old entries in addonlist.
            if (addonList != null)
            {
                foreach (var obj in addonList)
                {
                    string name = obj.Name;
                    if (name.EndsWith(".vpk"))
                    {
                        string nameNoExt = name.Substring(0, name.Length - 4);

                        if (TryParseLocalVpkFileNameNoExt(nameNoExt, out _))
                        {
                            continue;
                        }
                        if (TryParseWorkshopVpkFileNameNoExt(nameNoExt, out _))
                        {
                            continue;
                        }
                    }
                    addonEntries[name] = (string)obj.Value;
                }
            }

            // Add remaining files to entries.
            foreach (string filePath in remainingFilePaths)
            {
                string fileName = Path.GetFileName(filePath);
                addonEntries[fileName] = "0";
            }

            // Add enabled addons to entries.
            foreach (var addon in this.GetAllNodes())
            {
                if (!addon.IsEnabledInHierarchy)
                {
                    continue;
                }
                
                if (addon is VpkAddon vpkAddon)
                {
                    string? vpkPath = vpkAddon.FullVpkFilePath;
                    string? linkFileName = null;

                    if (vpkPath == null || !File.Exists(vpkPath))
                    {
                        continue;
                    }

                    if (vpkAddon is LocalVpkAddon localVpkAddon)
                    {
                        localVpkAddon.ValidateVpkGuid();
                        linkFileName = BuildLocalVpkFileName(localVpkAddon.VpkGuid);
                    }
                    else if (vpkAddon is WorkshopVpkAddon workshopVpkAddon)
                    {
                        if (workshopVpkAddon.PublishedFileId.HasValue)
                        {
                            linkFileName = BuildWorkshopVpkFileName(workshopVpkAddon.PublishedFileId.Value);
                        }
                    }

                    if (linkFileName == null)
                    {
                        continue;
                    }

                    File.CreateSymbolicLink(Path.Join(addonsPath, linkFileName), vpkPath);
                    addonEntries[linkFileName] = "1";
                }
            }

            // Update addonlist.txt.
            if (!File.Exists(addonListPath))
            {
                File.Create(addonListPath);
            }
            addonList = new KVObject("AddonList", addonEntries.Select(pair => new KVObject(pair.Key, pair.Value)));
            using (var stream = File.Open(addonListPath, FileMode.Truncate))
            {
                kv.Serialize(stream, addonList);
            }

            bool TryParseLocalVpkFileNameNoExt(string nameNoExt, out Guid guid)
            {
                const int PrefixLength = 6;
                const int GuidLength = 32;

                guid = Guid.Empty;
                if (nameNoExt.Length == (PrefixLength + GuidLength) && nameNoExt.StartsWith("local_"))
                {
                    string guidStr = nameNoExt.Substring(PrefixLength);
                    if (Guid.TryParse(guidStr, out guid))
                    {
                        return true;
                    }
                }
                return false;
            }

            bool TryParseWorkshopVpkFileNameNoExt(string nameNoExt, out ulong publishedFileId)
            {
                const int PrefixLength = 9;

                publishedFileId = 0;
                if (nameNoExt.Length > PrefixLength && nameNoExt.StartsWith("workshop_"))
                {
                    string idStr = nameNoExt.Substring(PrefixLength);
                    if (ulong.TryParse(idStr, out publishedFileId))
                    {
                        return true;
                    }
                }
                return false;
            }

            string BuildLocalVpkFileName(Guid guid)
            {
                return $"local_{guid:N}.vpk";
            }

            string BuildWorkshopVpkFileName(ulong publishedFileId)
            {
                return $"workshop_{publishedFileId}.vpk";
            }
        }

        public void LoadFile()
        {
            var save = LoadFile(DirectoryPath);
            if (save == null)
            {
                return;
            }
            LoadSave(save);
        }

        public void Save()
        {
            SaveFile(DirectoryPath, CreateSave());
        }

        public static void SaveFile(string dirPath, AddonRootSave save)
        {
            ArgumentNullException.ThrowIfNull(dirPath);
            ArgumentNullException.ThrowIfNull(save);

            string path = Path.Join(dirPath, SaveFileName);
            string json = JsonConvert.SerializeObject(save, s_jsonSettings);
            File.WriteAllText(path, json);
        }

        public static AddonRootSave? LoadFile(string dirPath)
        {
            ArgumentNullException.ThrowIfNull(dirPath);

            string path = Path.Join(dirPath, SaveFileName);
            if (!File.Exists(path))
            {
                return null;
            }
            string json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<AddonRootSave>(json, s_jsonSettings) ?? throw new JsonSerializationException();
        }

        public AddonRootSave CreateSave()
        {
            var save = new AddonRootSave();
            save.Nodes = Nodes.Select(node => node.CreateSave()).ToArray();
            return save;
        }

        public void LoadSave(AddonRootSave save)
        {
            ArgumentNullException.ThrowIfNull(save);

            foreach (var nodeSave in save.Nodes)
            {
                AddonNode.LoadSave(nodeSave, this);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
                var tasks = new List<Task>();
                foreach (var node in Nodes)
                {
                    tasks.Add(node.DestroyAsync(false));
                }
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
        }

        public bool ShouldUpdateWorkshopItem(AutoUpdateStrategy strategy)
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
        
        internal void AddNode(AddonNode node)
        {
            _containerService.AddUncheckName(node);
        }

        internal void RemoveNode(AddonNode node)
        {
            _containerService.Remove(node);
        }

        void IAddonNodeContainerInternal.ThrowIfNodeNameInvalid(string name)
        {
            _containerService.ThrowIfNameInvalid(name);
        }

        void IAddonNodeContainerInternal.ChangeNameUnchecked(string? oldName, string newName, AddonNode node)
        {
            _containerService.ChangeNameUnchecked(oldName, newName, node);
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
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
}
