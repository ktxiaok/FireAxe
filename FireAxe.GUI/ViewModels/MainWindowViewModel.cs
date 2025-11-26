using Avalonia.Threading;
using ReactiveUI;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using FireAxe.Resources;
using System.Reactive.Disposables.Fluent;
using Avalonia.Input.Platform;

namespace FireAxe.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase, IActivatableViewModel, ISaveable, IDisposable
{
    private class AddonRootParentSettings : IAddonRootParentSettings
    {
        private readonly MainWindowViewModel _mainWindowViewModel;

        internal AddonRootParentSettings(MainWindowViewModel mainWindowViewModel)
        {
            _mainWindowViewModel = mainWindowViewModel;
        }

        public bool IsAutoUpdateWorkshopItem => _mainWindowViewModel._settings.IsAutoUpdateWorkshopItem;

        public VpkAddonConflictCheckSettings VpkAddonConflictCheckSettings => _mainWindowViewModel._vpkAddonConflictCheckSettings;
    }

    private static TimeSpan CheckClipboardInterval = TimeSpan.FromSeconds(0.5);
    private static TimeSpan AutoRedownloadInterval = TimeSpan.FromSeconds(5);
    private static TimeSpan BackupInterval = TimeSpan.FromMinutes(1);

    private bool _disposed = false;

    private CompositeDisposable _disposables = new();
    private CompositeDisposable? _addonNodeExplorerViewModelDisposables = null;

    private bool _inited = false;

    private readonly AppSettings _settings;
    private readonly IAppWindowManager _windowManager;
    private readonly IDownloadService _downloadService;
    private readonly HttpClient _httpClient;
    private readonly DownloadItemListViewModel _downloadItemListViewModel;

    private AddonRoot? _addonRoot = null;
    private readonly IObservable<bool> _addonRootNotNullObservable;

    private bool _isOpeningDirectory = false;
    private readonly ObservableAsPropertyHelper<bool> _isEmptyDirectory; 

    private readonly AddonRootParentSettings _addonRootParentSettings;
    private VpkAddonConflictCheckSettings _vpkAddonConflictCheckSettings = VpkAddonConflictCheckSettings.Default;

    private DateTime? _lastPushDateTime = null;

    private AddonNodeExplorerViewModel? _addonNodeExplorerViewModel = null;

    private readonly ObservableAsPropertyHelper<string> _titleExtraInfo;

    private readonly ObservableAsPropertyHelper<bool> _hasSelection;

    private bool _isCheckingUpdate = false;
    private object? _checkUpdateActivity = null;
    private CancellationTokenSource? _checkUpdateCts = null;

    private IDisposable _checkClipboardTimer;
    private bool _isCheckingClipboard = false;
    private string? _lastClipboardText = null;

    private IDisposable _autoRedownloadTimer;

    private IDisposable _backupTimer;

    public MainWindowViewModel(AppSettings settings, IAppWindowManager windowManager, IDownloadService downloadService, HttpClient httpClient, DownloadItemListViewModel downloadItemListViewModel)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(windowManager);
        ArgumentNullException.ThrowIfNull(downloadService);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(downloadItemListViewModel);
        _settings = settings;
        _windowManager = windowManager;
        _downloadService = downloadService;
        _httpClient = httpClient;
        _downloadItemListViewModel = downloadItemListViewModel;

        _addonRootNotNullObservable = this.WhenAnyValue(x => x.AddonRoot).Select(root => root != null);

        _isEmptyDirectory = this.WhenAnyValue(x => x.AddonRoot, x => x.IsOpeningDirectory, (addonRoot, isOpeningDirectory) => addonRoot is null && !isOpeningDirectory)
            .ToProperty(this, nameof(IsEmptyDirectory));

        _addonRootParentSettings = new(this);

        _titleExtraInfo = this.WhenAnyValue(x => x.AddonRoot)
            .Select((addonRoot) =>
            {
                if (addonRoot == null)
                {
                    return "";
                }
                return " - " + addonRoot.DirectoryPath;
            })
            .ToProperty(this, nameof(TitleExtraInfo));

        _hasSelection =
            this.WhenAnyValue(x => x.AddonNodeExplorerViewModel, x => x.AddonNodeExplorerViewModel!.HasSelection, (viewModel, _) => viewModel?.HasSelection ?? false)
            .ToProperty(this, nameof(HasSelection));

        OpenDirectoryCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var path = await ChooseOpenDirectoryInteraction.Handle(Unit.Default);
            if (path == null)
            {
                return;
            }
            if (GamePathUtils.IsAddonsPath(path))
            {
                await ShowDontOpenGameAddonsDirectoryInteraction.Handle(Unit.Default);
                return;
            }
            await OpenDirectory(path);
        });
        CloseDirectoryCommand = ReactiveCommand.Create(CloseDirectory, _addonRootNotNullObservable);
        ImportCommand = ReactiveCommand.CreateFromTask(Import, _addonRootNotNullObservable);
        ImportAddonRootFileCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var filePath = await ChooseAddonRootFileToImportInteraction.Handle(Unit.Default);
            if (filePath is null)
            {
                return;
            }

            var explorerViewModel = AddonNodeExplorerViewModel;
            if (explorerViewModel is null)
            {
                return;
            }
            var addonRoot = explorerViewModel.AddonRoot;
            using var blockAutoCheck = addonRoot.BlockAutoCheck();

            IEnumerable<AddonNodeSave> nodeSaves;
            try
            {
                nodeSaves = AddonRoot.Deserialize(File.ReadAllText(filePath)).Nodes;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception ocurred during importing .addonroot file: {FilePath}", filePath);
                await ShowExceptionInteraction.Handle(ex);
                return;
            }

            var importedGroup = AddonNode.Create<AddonGroup>(addonRoot, explorerViewModel.CurrentGroup);
            importedGroup.Name = importedGroup.Parent.GetUniqueChildName(Texts.ImportedGroup);
            explorerViewModel.SelectNode(importedGroup);
            foreach (var nodeSave in nodeSaves)
            {
                AddonNode.LoadSave(nodeSave, addonRoot, importedGroup);
            }
            _ = addonRoot.CheckAsync();
        }, _addonRootNotNullObservable);

        OpenSettingsWindowCommand = ReactiveCommand.Create(() => _windowManager.OpenSettingsWindow());
        OpenProblemListWindowCommand = ReactiveCommand.Create(() => _windowManager.OpenProblemListWindow(), _addonRootNotNullObservable);
        OpenDownloadListWindowCommand = ReactiveCommand.Create(() => _windowManager.OpenDownloadListWindow());
        OpenTagManagerWindowCommand = ReactiveCommand.Create(() => _windowManager.OpenTagManagerWindow(), _addonRootNotNullObservable);
        OpenVpkConflictListWindowCommand = ReactiveCommand.Create(() => _windowManager.OpenVpkConflictListWindow(), _addonRootNotNullObservable);
        OpenWorkshopVpkFinderWindowCommand = ReactiveCommand.Create(() => _windowManager.OpenWorkshopVpkFinderWindow());
        OpenFileCleanerWindowCommand = ReactiveCommand.Create(() => _windowManager.OpenFileCleanerWindow(), _addonRootNotNullObservable);
        OpenAddonNameAutoSetterWindowCommand = ReactiveCommand.Create(() => _windowManager.OpenAddonNameAutoSetterWindow(), _addonRootNotNullObservable);

        PushCommand = ReactiveCommand.CreateFromTask(Push, _addonRootNotNullObservable);
        CheckCommand = ReactiveCommand.Create(Check, _addonRootNotNullObservable);
        ClearCachesCommand = ReactiveCommand.Create(ClearCaches, _addonRootNotNullObservable);
        RandomlySelectCommand = 
            ReactiveCommand.CreateFromTask(async () => await ShowItemsRandomSelectedInteraction.Handle(RandomlySelect()), _addonRootNotNullObservable);
        RandomlySelectForSelectedItemsCommand = 
            ReactiveCommand.CreateFromTask(async () => await ShowItemsRandomSelectedInteraction.Handle(RandomlySelect(true)), this.WhenAnyValue(x => x.HasSelection));
        DeleteRedundantVpkFilesCommand = ReactiveCommand.CreateFromTask(() => DeleteRedundantVpkFiles(), _addonRootNotNullObservable);
        DeleteRedundantVpkFilesForSelectedItemsCommand = ReactiveCommand.CreateFromTask(() => DeleteRedundantVpkFiles(true), this.WhenAnyValue(x => x.HasSelection));
        ExportSelectedItemsAsAddonRootFile = ReactiveCommand.CreateFromTask(async () =>
        {
            var filePath = await SaveAddonRootFileInteraction.Handle(Unit.Default);
            if (filePath is null)
            {
                return;
            }

            try
            {
                var explorerViewModel = AddonNodeExplorerViewModel;
                if (explorerViewModel is null)
                {
                    return;
                }

                var save = new AddonRootSave
                {
                    Nodes = [.. explorerViewModel.SelectedNodes.Select(node => node.CreateSave())]
                };
                await Task.Run(() => File.WriteAllText(filePath, AddonRoot.Serialize(save)));

                await ShowSaveAddonRootFileSuccessInteraction.Handle(filePath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred during exporting .addonroot file: {FilePath}", filePath);
                await ShowExceptionInteraction.Handle(ex);
                return;
            }
        }, this.WhenAnyValue(x => x.HasSelection));

        OpenAboutWindowCommand = ReactiveCommand.Create(() => _windowManager.OpenAboutWindow());
        CheckUpdateCommand = ReactiveCommand.Create(() => CheckUpdate(false));

        _settings.WhenAnyValue(x => x.GamePath)
            .CombineLatest(this.WhenAnyValue(x => x.AddonRoot))
            .Subscribe(args =>
            {
                var (gamePath, addonRoot) = args;
                if (addonRoot is not null)
                {
                    addonRoot.GamePath = gamePath;
                }
            })
            .DisposeWith(_disposables);
        _settings.WhenAnyValue(x => x.IgnoreHalfLife2FilesForVpkAddonConflicts)
            .Subscribe(_ =>
            {
                bool ignoreHl2 = _settings.IgnoreHalfLife2FilesForVpkAddonConflicts;
                _vpkAddonConflictCheckSettings = new()
                {
                    IgnoringFileSet = new VpkAddonConflictIgnoringFileSet([], [
                        () => _settings.WaitCustomVpkAddonConflictIgnoringFiles(),
                        () => ignoreHl2 ? CommonVpkAddonConflictIgnoringFiles.HalfLife2 : null
                    ])
                };
            })
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.AddonNodeExplorerViewModel)
            .Subscribe(explorerViewModel =>
            {
                if (_addonNodeExplorerViewModelDisposables != null)
                {
                    _addonNodeExplorerViewModelDisposables.Dispose();
                    _addonNodeExplorerViewModelDisposables = null;
                }
                if (explorerViewModel == null)
                {
                    return;
                }
                _addonNodeExplorerViewModelDisposables = new();
                var disposables = _addonNodeExplorerViewModelDisposables;
                explorerViewModel.SortMethod = _settings.AddonNodeSortMethod;
                explorerViewModel.IsAscendingOrder = _settings.IsAddonNodeAscendingOrder;
                explorerViewModel.ListItemViewKind = _settings.AddonNodeListItemViewKind;
                explorerViewModel.WhenAnyValue(x => x.SortMethod)
                    .BindTo(_settings, x => x.AddonNodeSortMethod)
                    .DisposeWith(disposables);
                explorerViewModel.WhenAnyValue(x => x.IsAscendingOrder)
                    .BindTo(_settings, x => x.IsAddonNodeAscendingOrder)
                    .DisposeWith(disposables);
                explorerViewModel.WhenAnyValue(x => x.ListItemViewKind)
                    .BindTo(_settings, x => x.AddonNodeListItemViewKind)
                    .DisposeWith(disposables);
                _settings.WhenAnyValue(x => x.AddonNodeSortMethod)
                    .BindTo(explorerViewModel, x => x.SortMethod)
                    .DisposeWith(disposables);
                _settings.WhenAnyValue(x => x.IsAddonNodeAscendingOrder)
                    .BindTo(explorerViewModel, x => x.IsAscendingOrder)
                    .DisposeWith(disposables);
                _settings.WhenAnyValue(x => x.AddonNodeListItemViewKind)
                    .BindTo(explorerViewModel, x => x.ListItemViewKind)
                    .DisposeWith(disposables);
            });

        this.WhenAnyValue(x => x.AddonRoot)
            .Subscribe(_ => LastPushDateTime = null);

        this.WhenActivated((CompositeDisposable disposables) =>
        {
            
        });

        _checkClipboardTimer = DispatcherTimer.Run(() =>
        {
            CheckClipboard();
            return true;
        }, CheckClipboardInterval);

        _autoRedownloadTimer = DispatcherTimer.Run(() =>
        {
            AutoRedownload();
            return true;
        }, AutoRedownloadInterval);

        _backupTimer = DispatcherTimer.Run(() =>
        {
            BackUpIfNeed();
            return true;
        }, BackupInterval);
        this.WhenAnyValue(x => x.AddonRoot)
            .Subscribe(_ => BackUpIfNeed());

        MessageBus.Current.Listen<AddonNodeJumpMessage>()
            .Subscribe(msg =>
            {
                var explorerViewModel = AddonNodeExplorerViewModel;
                if (explorerViewModel is null)
                {
                    return;
                }

                explorerViewModel.GotoNode(msg.Target);
                _windowManager.MainWindow?.Activate();
            })
            .DisposeWith(_disposables);
    }

    public event Action? ShowCheckingUpdateWindow = null;

    public event Action? CloseCheckingUpdateWindow = null;

    public ViewModelActivator Activator { get; } = new();

    public bool RequestSave
    {
        get
        {
            if (_addonRoot == null)
            {
                return false;
            }
            return _addonRoot.RequestSave;
        }
        set
        {
            if (_addonRoot != null)
            {
                _addonRoot.RequestSave = value;
            }
        }
    }

    public AddonRoot? AddonRoot
    {
        get => _addonRoot;
        private set
        {
            if (value == _addonRoot)
            {
                return;
            }

            if (_addonRoot != null)
            {
                _addonRoot.Save();

                _addonRoot.NewDownloadItem -= OnAddonRootNewDownloadItem;
                _addonRoot.Pushed -= OnAddonRootPushed;
                _addonRoot.DisposeAsync().AsTask(); // TODO
                _addonRoot = null;
            }

            _addonRoot = value;

            if (_addonRoot == null)
            {
                AddonNodeExplorerViewModel = null;
            }
            else
            {
                using var blockAutoCheck = _addonRoot.BlockAutoCheck();

                _addonRoot.NewDownloadItem += OnAddonRootNewDownloadItem;
                _addonRoot.Pushed += OnAddonRootPushed;
                _addonRoot.ParentSettings = _addonRootParentSettings;
                _addonRoot.TaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
                _addonRoot.DownloadService = _downloadService;
                _addonRoot.HttpClient = _httpClient;
                
                _addonRoot.LoadFile();

                _addonRoot.CheckAsync();

                AddonNodeExplorerViewModel = new(_addonRoot);
            }

            this.RaisePropertyChanged();
        }
    }

    public bool IsOpeningDirectory
    {
        get => _isOpeningDirectory;
        private set => this.RaiseAndSetIfChanged(ref _isOpeningDirectory, value);
    }

    public bool IsEmptyDirectory => _isEmptyDirectory.Value;

    public AddonNodeExplorerViewModel? AddonNodeExplorerViewModel
    {
        get => _addonNodeExplorerViewModel;
        private set => this.RaiseAndSetIfChanged(ref _addonNodeExplorerViewModel, value);
    }

    public string TitleExtraInfo => _titleExtraInfo.Value;

    public bool HasSelection => _hasSelection.Value;

    public DateTime? LastPushDateTime
    {
        get => _lastPushDateTime;
        private set => this.RaiseAndSetIfChanged(ref _lastPushDateTime, value);
    }

    public ReactiveCommand<Unit, Unit> OpenDirectoryCommand { get; }

    public ReactiveCommand<Unit, Unit> CloseDirectoryCommand { get; }

    public ReactiveCommand<Unit, Unit> ImportCommand { get; }

    public ReactiveCommand<Unit, Unit> ImportAddonRootFileCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenSettingsWindowCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenDownloadListWindowCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenProblemListWindowCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenTagManagerWindowCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenVpkConflictListWindowCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenWorkshopVpkFinderWindowCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenFileCleanerWindowCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenAddonNameAutoSetterWindowCommand { get; }

    public ReactiveCommand<Unit, Unit> PushCommand { get; } 

    public ReactiveCommand<Unit, Unit> CheckCommand { get; }

    public ReactiveCommand<Unit, Unit> ClearCachesCommand { get; } 

    public ReactiveCommand<Unit, Unit> RandomlySelectCommand { get; }

    public ReactiveCommand<Unit, Unit> RandomlySelectForSelectedItemsCommand { get; }

    public ReactiveCommand<Unit, Unit> DeleteRedundantVpkFilesCommand { get; }

    public ReactiveCommand<Unit, Unit> DeleteRedundantVpkFilesForSelectedItemsCommand { get; }

    public ReactiveCommand<Unit, Unit> ExportSelectedItemsAsAddonRootFile { get; }

    public ReactiveCommand<Unit, Unit> OpenAboutWindowCommand { get; }

    public ReactiveCommand<Unit, Unit> CheckUpdateCommand { get; }

    public Interaction<Exception, Unit> ShowExceptionInteraction { get; } = new();

    public Interaction<Unit, string?> ChooseOpenDirectoryInteraction { get; } = new();

    public Interaction<Unit, string?> ChooseAddonRootFileToImportInteraction { get; } = new();

    public Interaction<Unit, string?> SaveAddonRootFileInteraction { get; } = new();

    public Interaction<Unit, Unit> ShowImportSuccessInteraction { get; } = new();

    public Interaction<Exception, Unit> ShowImportErrorInteraction { get; } = new();

    public Interaction<Unit, Unit> ShowPushSuccessInteraction { get; } = new();

    public Interaction<Exception, Unit> ShowPushErrorInteraction { get; } = new();

    public Interaction<string, Unit> ShowInvalidGamePathInteraction { get; } = new();

    public Interaction<Unit, Unit> ShowCheckUpdateFailedInteraction { get; } = new();

    public Interaction<string, UpdateRequestReply> ShowUpdateRequestInteraction { get; } = new();

    public Interaction<Unit, Unit> ShowCurrentVersionLatestInteraction { get; } = new();

    public Interaction<Unit, Unit> ShowDontOpenGameAddonsDirectoryInteraction { get; } = new();

    public Interaction<string, bool> ShowAutoDetectWorkshopItemLinkDialogInteraction { get; } = new();

    public Interaction<string, bool> ConfirmOpenHigherVersionFileInteraction { get; } = new();

    public Interaction<WorkshopVpkAddon.DeleteRedundantVpkFilesReport, bool> ConfirmDeleteRedundantVpkFilesInteraction { get; } = new();

    public Interaction<WorkshopVpkAddon.DeleteRedundantVpkFilesReport, Unit> ShowDeleteRedundantVpkFilesSuccessInteraction { get; } = new();

    public Interaction<string, Unit> ShowSaveAddonRootFileSuccessInteraction { get; } = new();

    public Interaction<int, Unit> ShowItemsRandomSelectedInteraction { get; } = new();

    public async void InitIfNot()
    {
        if (_inited)
        {
            return;
        }
        _inited = true;

        CheckUpdate(true);

        // Try to open the LastOpenDirectory.
        var lastOpenDir = _settings.LastOpenDirectory;
        if (lastOpenDir != null)
        {
            _settings.LastOpenDirectory = null;
            _settings.Save();
            if (Directory.Exists(lastOpenDir))
            {
                await OpenDirectory(lastOpenDir);
            }
        }
    }

    public async Task OpenDirectory(string dirPath)
    {
        ArgumentNullException.ThrowIfNull(dirPath);

        if (IsOpeningDirectory)
        {
            return;
        }

        IsOpeningDirectory = true;
        try
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (GamePathUtils.IsAddonsPath(dirPath))
                {
                    return;
                }

                string versionFilePath = Path.Join(dirPath, AddonRoot.VersionFileName);
                if (File.Exists(versionFilePath))
                {
                    if (Version.TryParse(File.ReadAllText(versionFilePath), out var version))
                    {
                        if (version > AppGlobal.Version)
                        {
                            if (!(await ConfirmOpenHigherVersionFileInteraction.Handle($"{dirPath} v{version.ToString(3)}")))
                            {
                                return;
                            }
                        }
                    }
                }

                var addonRoot = new AddonRoot();
                addonRoot.DirectoryPath = dirPath;
                AddonRoot = addonRoot;

                _settings.LastOpenDirectory = dirPath;
            }, DispatcherPriority.Default);
        }
        finally
        {
            IsOpeningDirectory = false;
        }
    }

    public void CloseDirectory()
    {
        AddonRoot = null;

        _settings.LastOpenDirectory = null;
    }

    public void Save()
    {
        if (_addonRoot != null)
        {
            _addonRoot.Save();
        }
    }

    public async Task Import()
    {
        if (_addonRoot != null)
        {
            try
            {
                _addonRoot.Import();
                await ShowImportSuccessInteraction.Handle(Unit.Default);
            }
            catch (Exception ex)
            {
                await ShowImportErrorInteraction.Handle(ex);
            }
        }
    }

    public async Task Push()
    {
        if (_addonRoot == null)
        {
            return;
        }
        
        _ = _addonRoot.CheckAsync();
        try
        {
            _addonRoot.Push();
        }
        catch (InvalidGamePathException)
        {
            await ShowInvalidGamePathInteraction.Handle(_addonRoot.GamePath);
            return;
        }
        catch (Exception ex)
        {
            await ShowPushErrorInteraction.Handle(ex);
            return;
        }
        await ShowPushSuccessInteraction.Handle(Unit.Default);
    }

    public void Check()
    {
        if (_addonRoot == null)
        {
            return;
        }

        _addonRoot.CheckAsync();
    }

    public void ClearCaches()
    {
        if (_addonRoot != null)
        {
            foreach (var addonNode in _addonRoot.GetDescendants())
            {
                addonNode.ClearCaches();
            }
        }
    }

    public int RandomlySelect(bool forSelectedItems = false)
    {
        IEnumerable<AddonGroup>? targets = forSelectedItems ?
            AddonNodeExplorerViewModel?.SelectedNodes.SelectMany(node => node.GetSelfAndDescendants()).OfType<AddonGroup>() :
            AddonRoot?.GetDescendants().OfType<AddonGroup>();
        if (targets is null)
        {
            return 0;
        }
        int count = 0;
        foreach (var group in targets)
        {
            if (group.EnableOneChildRandomlyIfSingleRandom())
            {
                count++;
            }
        }
        return count;
    }

    public async void CheckUpdate(bool silenced)
    {
        if (!silenced)
        {
            ShowCheckingUpdateWindow?.Invoke();
        }
        if (_isCheckingUpdate)
        {
            return;
        }

        _isCheckingUpdate = true;
        var activity = new object();
        _checkUpdateActivity = activity;
        _checkUpdateCts = new();
        string? latestVersion = null;
        try
        {
            latestVersion = await AppGlobal.GetLatestVersionAsync(_httpClient, _checkUpdateCts.Token);
        }
        catch (OperationCanceledException) { }
        if (activity == _checkUpdateActivity)
        {
            _isCheckingUpdate = false;
            _checkUpdateActivity = null;
            _checkUpdateCts.Dispose();
            _checkUpdateCts = null;
        }

        CloseCheckingUpdateWindow?.Invoke();
        if (silenced)
        {
            if (latestVersion == null)
            {
                return;
            }
            if (latestVersion == _settings.SuppressedUpdateRequestVersion)
            {
                return;
            }
            if (latestVersion == AppGlobal.VersionString)
            {
                return;
            }
        }

        if (latestVersion == null)
        {
            await ShowCheckUpdateFailedInteraction.Handle(Unit.Default);
        }
        else if (latestVersion == AppGlobal.VersionString)
        {
            await ShowCurrentVersionLatestInteraction.Handle(Unit.Default);
        }
        else
        {
            var reply = await ShowUpdateRequestInteraction.Handle(latestVersion);
            if (reply == UpdateRequestReply.GoToDownload)
            {
                Utils.OpenWebsite(AppGlobal.GithubReleasesUrl);
            }
            else if (reply == UpdateRequestReply.Ignore)
            {
                _settings.SuppressedUpdateRequestVersion = latestVersion;
            }
        }
    }

    public void CancelCheckUpdate()
    {
        if (!_isCheckingUpdate)
        {
            return;
        }

        _isCheckingUpdate = false;
        _checkUpdateActivity = null;
        _checkUpdateCts!.Cancel();
        _checkUpdateCts!.Dispose();
        _checkUpdateCts = null;
    }

    public async void CheckClipboard()
    {
        if (_isCheckingClipboard)
        {
            return;
        }
        _isCheckingClipboard = true;

        async Task Check()
        {
            string? clipboardText = await GetClipboardText();
            if (_disposed)
            {
                return;
            }
            if (clipboardText == _lastClipboardText)
            {
                return;
            }
            _lastClipboardText = clipboardText;

            var explorerViewModel = AddonNodeExplorerViewModel;

            if (explorerViewModel != null && _settings.IsAutoDetectWorkshopItemLinkInClipboard)
            {
                if (clipboardText != null)
                {
                    if (PublishedFileUtils.TryParsePublishedFileIdLink(clipboardText, out var id))
                    {
                        bool confirm = await ShowAutoDetectWorkshopItemLinkDialogInteraction.Handle(clipboardText);
                        if (confirm)
                        {
                            var addon = explorerViewModel.NewWorkshopAddon();
                            addon.PublishedFileId = id;
                        }
                    }
                }
            }
        }
        async Task<string?> GetClipboardText()
        {
            var clipboard = _windowManager.MainWindow?.Clipboard;
            if (clipboard == null)
            {
                return null;
            }
            try
            {
                return await clipboard.TryGetTextAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred during opening clipboard");
            }
            return null;
        }
        await Check();

        _isCheckingClipboard = false;
    }

    public void AutoRedownload()
    {
        if (_addonRoot == null)
        {
            return;
        }
        if (!_settings.IsAutoRedownload)
        {
            return;
        }

        foreach (var addonNode in _addonRoot.GetDescendants())
        {
            if (addonNode is WorkshopVpkAddon workshopVpkAddon)
            {
                if (workshopVpkAddon.FullVpkFilePath == null)
                {
                    workshopVpkAddon.Check();
                }
            }
        }
    }

    public void BackUpIfNeed()
    {
        if (!_settings.IsFileBackupEnabled)
        {
            return;
        }
        AddonRoot?.BackUpIfNeed(_settings.MaxRetainedBackupFileCount, _settings.FileBackupIntervalMinutes);
    }

    public void DummyCrash()
    {
        throw new Exception("dummy crash");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        _disposables.Dispose();
        _addonNodeExplorerViewModelDisposables?.Dispose();
        AddonRoot = null;
        _checkClipboardTimer.Dispose();
        _autoRedownloadTimer.Dispose();
        _backupTimer.Dispose();
    }

    private async Task DeleteRedundantVpkFiles(bool selectedItems = false)
    {
        var addonRoot = AddonRoot;
        if (addonRoot is null)
        {
            return;
        }

        IEnumerable<AddonNode>? targetAddons = selectedItems ?
            AddonNodeExplorerViewModel?.SelectedNodes?.SelectMany(addon => addon.GetSelfAndDescendants()) :
            addonRoot.GetDescendants();
        if (targetAddons is null)
        {
            return;
        }

        try
        {
            var report = WorkshopVpkAddon.DeleteRedundantVpkFilesReport.Combine(
                targetAddons.OfType<WorkshopVpkAddon>().Select(addon => addon.RequestDeleteRedundantVpkFiles()));
            bool confirm = await ConfirmDeleteRedundantVpkFilesInteraction.Handle(report);
            if (!confirm)
            {
                return;
            }
            report.Execute();
            await ShowDeleteRedundantVpkFilesSuccessInteraction.Handle(report);
        }
        catch (Exception ex)
        {
            await ShowExceptionInteraction.Handle(ex);
        }
    }

    private void OnAddonRootNewDownloadItem(IDownloadItem downloadItem)
    {
        _downloadItemListViewModel.Add(downloadItem);
    }

    private void OnAddonRootPushed()
    {
        LastPushDateTime = DateTime.Now;
    }
}
