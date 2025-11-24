using Avalonia.Styling;
using FireAxe.ViewModels;
using FireAxe.Resources;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ReactiveUI;
using Serilog;
using System;
using System.IO;
using System.Net;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Collections.Frozen;
using System.Reactive;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace FireAxe;

[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
public sealed class AppSettings : ObservableObject, ISaveable, IDisposable
{
    public const string SettingsFileName = "Settings.json";

    public const string CustomVpkAddonConflictIgnoringFilesDirectoryName = "CustomVpkAddonConflictIgnoringFiles";

    private static readonly JsonSerializerSettings s_jsonSettings = new()
    {
        Formatting = Formatting.Indented,
        Converters =
        {
            new StringEnumConverter()
        }
    };

    private bool _disposed = false;
    private CompositeDisposable _disposables = new();

    private readonly App _app;

    private readonly string _settingsFilePath;

    private AddonNodeSortMethod _addonNodeSortMethod = AddonNodeSortMethod.None;
    private bool _isAddonNodeAscendingOrder = true;
    private AddonNodeListItemViewKind _addonNodeListItemViewKind = AddonNodeListItemViewKind.MediumTile;
    private string? _lastOpenDirectory = null;
    private string? _language = null;
    private string _gamePath = "";
    private bool _ignoreHalfLife2FilesForVpkAddonConflicts = true;
    private bool _isAutoUpdateWorkshopItem = true;
    private string? _suppressedUpdateRequestVersion = null;
    private bool _isAutoDetectWorkshopItemLinkInClipboard = true;
    private bool _isAutoRedownload = false;
    private Uri? _webProxyUri = null;
    private IWebProxy? _webProxy = null;
    private bool _isFileBackupEnabled = true;
    private int _maxRetainedBackupFileCount = 25;
    private int _fileBackupIntervalMinutes = 30;

    private FrozenSet<string>? _customVpkAddonConflictIgnoringFiles = null;
    private readonly object _customVpkAddonConflictIgnoringFilesLock = new();
    private bool _isLoadingCustomVpkAddonConflictIgnoringFiles = false;

    public AppSettings(App app)
    {
        ArgumentNullException.ThrowIfNull(app);
        _app = app;

        _settingsFilePath = Path.Join(app.DocumentDirectoryPath, SettingsFileName);

        _app.WhenAnyValue(x => x.RequestedThemeVariant)
            .Subscribe(_ => NotifyChanged(nameof(ColorTheme)))
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.ColorTheme)
            .Subscribe(_ => RequestSave = true);

        LoadCustomVpkAddonConflictIgnoringFilesCommand = ReactiveCommand.CreateFromTask(LoadCustomVpkAddonConflictIgnoringFilesAsync);

        LoadFile();
        LoadCustomVpkAddonConflictIgnoringFilesCommand.Execute().Subscribe();
    }

    public bool RequestSave { get; set; } = true;

    public string SettingsFilePath => _settingsFilePath;

    public string CustomVpkAddonConflictIgnoringFilesDirectoryPath => Path.Join(_app.DocumentDirectoryPath, CustomVpkAddonConflictIgnoringFilesDirectoryName);

    public IReadOnlySet<string>? CustomVpkAddonConflictIgnoringFiles
    {
        get
        {
            IReadOnlySet<string>? result = null;
            if (Monitor.TryEnter(_customVpkAddonConflictIgnoringFilesLock))
            {
                result = _customVpkAddonConflictIgnoringFiles;
                Monitor.Exit(_customVpkAddonConflictIgnoringFilesLock);
            }
            return result;
        }
    }

    public bool IsLoadingCustomVpkAddonConflictIgnoringFiles
    {
        get => _isLoadingCustomVpkAddonConflictIgnoringFiles;
        private set => NotifyAndSetIfChanged(ref _isLoadingCustomVpkAddonConflictIgnoringFiles, value);
    }

    [JsonProperty]
    public AddonNodeSortMethod AddonNodeSortMethod
    {
        get => _addonNodeSortMethod;
        set
        {
            if (NotifyAndSetIfChanged(ref _addonNodeSortMethod, value))
            {
                RequestSave = true;
            }
        }
    }

    [JsonProperty]
    public bool IsAddonNodeAscendingOrder
    {
        get => _isAddonNodeAscendingOrder;
        set
        {
            if (NotifyAndSetIfChanged(ref _isAddonNodeAscendingOrder, value))
            {
                RequestSave = true;
            }
        }
    }

    [JsonProperty]
    public AddonNodeListItemViewKind AddonNodeListItemViewKind
    {
        get => _addonNodeListItemViewKind;
        set
        {
            if (NotifyAndSetIfChanged(ref _addonNodeListItemViewKind, value))
            {
                RequestSave = true;
            }
        }
    }

    [JsonProperty]
    public string? LastOpenDirectory
    {
        get => _lastOpenDirectory;
        set 
        {
            if (value == _lastOpenDirectory)
            {
                return;
            }
            if (value != null)
            {
                if (!FileSystemUtils.IsValidPath(value) || !Path.IsPathRooted(value))
                {
                    throw new ArgumentException("The path is invalid.");
                }
            }
            _lastOpenDirectory = value;
            NotifyChanged();
            RequestSave = true;
        }
    }

    [JsonProperty]
    public string? Language
    {
        get => _language;
        set
        {
            if (value is not null && !LanguageManager.SupportedLanguages.Contains(value))
            {
                value = null;
            }
            if (value == _language)
            {
                return;
            }
            
            LanguageManager.Instance.SetCurrentLanguage(value);
            _language = value;
            NotifyChanged();
            RequestSave = true;
        }
    }

    [JsonProperty]
    public AppColorTheme ColorTheme
    {
        get
        {
            var themeVariant = _app.RequestedThemeVariant;
            if (ThemeVariant.Light.Equals(themeVariant))
            {
                return AppColorTheme.Light;
            }
            else if (ThemeVariant.Dark.Equals(themeVariant))
            {
                return AppColorTheme.Dark;
            }
            else
            {
                return AppColorTheme.Default;
            }
        }
        set
        {
            _app.RequestedThemeVariant = value switch
            {
                AppColorTheme.Light => ThemeVariant.Light,
                AppColorTheme.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default
            };
        }
    }

    [JsonProperty]
    public string GamePath
    {
        get => _gamePath;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Length > 0)
            {
                if (!FileSystemUtils.IsValidPath(value) || !Path.IsPathRooted(value))
                {
                    throw new ArgumentException(Texts.InvalidFilePath);
                }
                value = FileSystemUtils.NormalizePath(value);
            }
            if (value == _gamePath)
            {
                return;
            }

            _gamePath = value;
            NotifyChanged();
            RequestSave = true;
        }
    }

    [JsonProperty]
    public bool IgnoreHalfLife2FilesForVpkAddonConflicts
    {
        get => _ignoreHalfLife2FilesForVpkAddonConflicts;
        set
        {
            if (NotifyAndSetIfChanged(ref _ignoreHalfLife2FilesForVpkAddonConflicts, value))
            {
                RequestSave = true;
            }
        }
    }

    [JsonProperty]
    public bool IsAutoUpdateWorkshopItem
    {
        get => _isAutoUpdateWorkshopItem;
        set
        {
            if (NotifyAndSetIfChanged(ref _isAutoUpdateWorkshopItem, value))
            {
                RequestSave = true;
            }
        }
    }

    [JsonProperty]
    public string? SuppressedUpdateRequestVersion
    {
        get => _suppressedUpdateRequestVersion;
        set
        {
            if (NotifyAndSetIfChanged(ref _suppressedUpdateRequestVersion, value))
            {
                RequestSave = true;
            }
        }
    }

    [JsonProperty]
    public bool IsAutoDetectWorkshopItemLinkInClipboard
    {
        get => _isAutoDetectWorkshopItemLinkInClipboard;
        set
        {
            if (NotifyAndSetIfChanged(ref _isAutoDetectWorkshopItemLinkInClipboard, value))
            {
                RequestSave = true;
            }
        }
    }

    [JsonProperty]
    public bool IsAutoRedownload
    {
        get => _isAutoRedownload;
        set
        {
            if (NotifyAndSetIfChanged(ref _isAutoRedownload, value))
            {
                RequestSave = true;
            }
        }
    }

    [JsonProperty]
    public string WebProxyAddress
    {
        get => _webProxyUri?.OriginalString ?? "";
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (value == WebProxyAddress)
            {
                return;
            }

            Uri? uri = null;
            IWebProxy? proxy = null;
            if (value.Length > 0)
            {
                try
                {
                    uri = new Uri(value);
                }
                catch (UriFormatException)
                {
                    throw new ArgumentException(Texts.InvalidUri);
                }
                proxy = new WebProxy(uri);
            }

            _webProxyUri = uri;
            _webProxy = proxy;

            NotifyChanged();
            NotifyChanged(nameof(WebProxy));
        }
    }

    public IWebProxy? WebProxy => _webProxy;

    [JsonProperty]
    public bool IsFileBackupEnabled
    {
        get => _isFileBackupEnabled;
        set
        {
            if (NotifyAndSetIfChanged(ref _isFileBackupEnabled, value))
            {
                RequestSave = true;
            }
        }
    }

    [JsonProperty]
    public int MaxRetainedBackupFileCount
    {
        get => _maxRetainedBackupFileCount;
        set
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (value == _maxRetainedBackupFileCount)
            {
                return;
            }

            _maxRetainedBackupFileCount = value;
            NotifyChanged();

            RequestSave = true;
        }
    }

    [JsonProperty]
    public int FileBackupIntervalMinutes
    {
        get => _fileBackupIntervalMinutes;
        set
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (value == _fileBackupIntervalMinutes)
            {
                return;
            }

            _fileBackupIntervalMinutes = value;
            NotifyChanged();

            RequestSave = true;
        }
    }

    public ReactiveCommand<Unit, IReadOnlySet<string>?> LoadCustomVpkAddonConflictIgnoringFilesCommand { get; }

    public IReadOnlySet<string>? WaitCustomVpkAddonConflictIgnoringFiles()
    {
        lock (_customVpkAddonConflictIgnoringFilesLock)
        {
            return _customVpkAddonConflictIgnoringFiles;
        }
    }

    private async Task<IReadOnlySet<string>?> LoadCustomVpkAddonConflictIgnoringFilesAsync()
    {
        if (IsLoadingCustomVpkAddonConflictIgnoringFiles)
        {
            return await Task.Run(WaitCustomVpkAddonConflictIgnoringFiles);
        }

        IsLoadingCustomVpkAddonConflictIgnoringFiles = true;
        try
        {
            var dirPath = CustomVpkAddonConflictIgnoringFilesDirectoryPath;

            var oldValue = CustomVpkAddonConflictIgnoringFiles;
            try
            {
                await Task.Run(() =>
                {
                    lock (_customVpkAddonConflictIgnoringFilesLock)
                    {
                        _customVpkAddonConflictIgnoringFiles = null;
                        _customVpkAddonConflictIgnoringFiles = FrozenSet.ToFrozenSet(LoadCustomVpkAddonConflictIgnoringFiles(dirPath));
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred during loading custom VPK addon conflict ignoring files.");
            }
            var newValue = CustomVpkAddonConflictIgnoringFiles;
            if (oldValue != newValue)
            {
                NotifyChanged(nameof(CustomVpkAddonConflictIgnoringFiles));
            }
            return newValue;
        }
        finally
        {
            IsLoadingCustomVpkAddonConflictIgnoringFiles = false;
        }
    }

    private static IEnumerable<string> LoadCustomVpkAddonConflictIgnoringFiles(string dirPath)
    {
        if (!Directory.Exists(dirPath))
        {
            yield break;
        }
        foreach (var file in Directory.EnumerateFiles(dirPath))
        {
            if (!file.EndsWith(".txt"))
            {
                continue;
            }
            var fileName = Path.GetFileName(file);
            if (fileName == "readme.txt")
            {
                continue;
            }

            using (var reader = File.OpenText(file))
            {
                string? line;
                while ((line = reader.ReadLine()) is not null)
                {
                    line = line.Trim().Replace('\\', '/');
                    yield return line;
                }
            }
        }
    }

    public void LoadFile()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                JsonConvert.PopulateObject(File.ReadAllText(_settingsFilePath), this, s_jsonSettings);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred during AppSettings.LoadFile.");
        }
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(_settingsFilePath, JsonConvert.SerializeObject(this, s_jsonSettings));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred during AppSettings.Save.");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        _disposables.Dispose();
    }
}
