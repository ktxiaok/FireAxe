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
    public class AddonRoot : IDisposable, IAddonNodeContainer, IAddonNodeContainerInternal, ISaveable
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

        private List<VpkAddon> _vpkAddons = new();
        
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

        public bool IsAutoCheck { get; set; } = true;

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

        public void Import(AddonGroup? group = null)
        {
            var fileFinder = group == null ? new AddonNodeFileFinder(this) : new AddonNodeFileFinder(group);
            var imports = new List<ImportItem>();
            while (fileFinder.MoveNext())
            {
                if (fileFinder.CurrentNodeExists)
                {
                    continue;
                }

                var filePath = fileFinder.CurrentFilePath;
                var fileExtension = Path.GetExtension(filePath);
                if (fileExtension == ".vpk")
                {
                    imports.Add(new LocalVpkImportItem(Path.GetFileNameWithoutExtension(filePath), fileFinder.GetOrCreateCurrentGroup()));
                }
            }
            foreach (var import in imports)
            {
                import.Create(this);
            }
        }

        private class LocalVpkFile
        {
            public string FileName;
            public Guid Guid;

            public LocalVpkFile(string fileName, Guid guid)
            {
                FileName = fileName;
                Guid = guid;
            }
        }

        public void Push()
        {
            var gamePath = EnsureValidGamePath();

            string addonsPath = GamePathUtils.GetAddonsPath(gamePath);

            // Delete old vpk files.
            var remainingLocalVpkFiles = new List<LocalVpkFile>();
            {
                var filesToDelete = new List<(string FilePath, object File)>();
                foreach (string filePath in Directory.EnumerateFiles(addonsPath))
                {
                    if (filePath.EndsWith(".vpk"))
                    {
                        string fileNameNoExt = Path.GetFileNameWithoutExtension(filePath);
                        if (TryParseLocalVpkFileName(fileNameNoExt, out Guid guid))
                        {
                            filesToDelete.Add((filePath, new LocalVpkFile(fileNameNoExt + ".vpk", guid)));
                        }
                    }
                }
                foreach ((string filePath, object file) in filesToDelete)
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
                        if (file is LocalVpkFile localVpkFile)
                        {
                            remainingLocalVpkFiles.Add(localVpkFile);
                        }
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
                        if (TryParseLocalVpkFileName(nameNoExt, out _))
                        {
                            continue;
                        }
                    }
                    addonEntries[name] = (string)obj.Value;
                }
            }

            // Add remaining files to entries.
            foreach (var file in remainingLocalVpkFiles)
            {
                addonEntries[file.FileName] = "0";
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
                    if (vpkPath == null)
                    {
                        continue;
                    }
                    if (vpkAddon is LocalVpkAddon localVpkAddon)
                    {
                        localVpkAddon.ValidateVpkGuid();
                        linkFileName = BuildLocalVpkFileName(localVpkAddon.VpkGuid);
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

            bool TryParseLocalVpkFileName(string nameNoExt, out Guid guid)
            {
                guid = Guid.Empty;
                if (nameNoExt.Length == (6 + 32) && nameNoExt.StartsWith("local_"))
                {
                    string guidStr = nameNoExt.Substring(6);
                    if (Guid.TryParse(guidStr, out guid))
                    {
                        return true;
                    }
                }
                return false;
            }

            string BuildLocalVpkFileName(Guid guid)
            {
                return "local_" + guid.ToString("N") + ".vpk";
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

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var node in Nodes)
                {
                    node.Destroy();
                }
                _disposed = true;
            }
        }
        
        internal void AddNode(AddonNode node)
        {
            _containerService.AddUncheckName(node);
        }

        internal void RemoveNode(AddonNode node)
        {
            _containerService.Remove(node);
        }

        internal void RegisterVpkAddon(VpkAddon addon)
        {
            _vpkAddons.Add(addon);
        }

        internal void UnregisterVpkAddon(VpkAddon addon)
        {
            _vpkAddons.Remove(addon);
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
