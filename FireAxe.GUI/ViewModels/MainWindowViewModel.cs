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

namespace FireAxe.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase, IActivatableViewModel, ISaveable, IDisposable
{
    private static TimeSpan CheckClipboardInterval = TimeSpan.FromSeconds(0.5);
    private static TimeSpan AutoRedownloadInterval = TimeSpan.FromSeconds(15);

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
            var path = await ChooseDirectoryInteraction.Handle(Unit.Default);
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

        OpenSettingsWindowCommand = ReactiveCommand.Create(() => _windowManager.OpenSettingsWindow());
        OpenDownloadListWindowCommand = ReactiveCommand.Create(() => _windowManager.OpenDownloadListWindow());
        OpenTagManagerWindowCommand = ReactiveCommand.Create(() => _windowManager.OpenTagManagerWindow(this));
        OpenVpkConflictListWindowCommand = ReactiveCommand.Create(() =>
        {
            if (_addonRoot is null)
            {
                return;
            }
            _windowManager.OpenVpkConflictListWindow(_addonRoot);
        }, _addonRootNotNullObservable);

        PushCommand = ReactiveCommand.CreateFromTask(Push, _addonRootNotNullObservable);
        CheckCommand = ReactiveCommand.Create(Check, _addonRootNotNullObservable);
        ClearCachesCommand = ReactiveCommand.Create(ClearCaches, _addonRootNotNullObservable);
        RandomlySelectCommand = ReactiveCommand.Create(RandomlySelect, _addonRootNotNullObservable);
        DeleteRedundantVpkFilesCommand = ReactiveCommand.CreateFromTask(() => DeleteRedundantVpkFiles(), _addonRootNotNullObservable);
        DeleteRedundantVpkFilesForSelectedItemsCommand = ReactiveCommand.CreateFromTask(() => DeleteRedundantVpkFiles(true), this.WhenAnyValue(x => x.HasSelection));

        OpenAboutWindowCommand = ReactiveCommand.Create(() => _windowManager.OpenAboutWindow());
        CheckUpdateCommand = ReactiveCommand.Create(() => CheckUpdate(false));

        _settings.WhenAnyValue(x => x.GamePath)
            .Subscribe(gamePath =>
            {
                if (_addonRoot != null)
                {
                    _addonRoot.GamePath = gamePath;
                }
            })
            .DisposeWith(_disposables);
        _settings.WhenAnyValue(x => x.IsAutoUpdateWorkshopItem)
            .Subscribe(isAutoUpdateWorkshopItem =>
            {
                if (_addonRoot != null)
                {
                    _addonRoot.IsAutoUpdateWorkshopItem = isAutoUpdateWorkshopItem;
                }
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
                _addonRoot.TaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
                _addonRoot.DownloadService = _downloadService;
                _addonRoot.HttpClient = _httpClient;
                _addonRoot.GamePath = _settings.GamePath;
                _addonRoot.IsAutoUpdateWorkshopItem = _settings.IsAutoUpdateWorkshopItem;
                _addonRoot.LoadFile();
                _addonRoot.Check();
                AddonNodeExplorerViewModel = new(_addonRoot, _windowManager);
            }
            this.RaisePropertyChanged();
        }
    }

    public AddonNodeExplorerViewModel? AddonNodeExplorerViewModel
    {
        get => _addonNodeExplorerViewModel;
        private set => this.RaiseAndSetIfChanged(ref _addonNodeExplorerViewModel, value);
    }

    public string TitleExtraInfo => _titleExtraInfo.Value;

    public bool HasSelection => _hasSelection.Value;

    public ReactiveCommand<Unit, Unit> OpenDirectoryCommand { get; }

    public ReactiveCommand<Unit, Unit> CloseDirectoryCommand { get; }

    public ReactiveCommand<Unit, Unit> ImportCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenSettingsWindowCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenDownloadListWindowCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenTagManagerWindowCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenVpkConflictListWindowCommand { get; }

    public ReactiveCommand<Unit, Unit> PushCommand { get; } 

    public ReactiveCommand<Unit, Unit> CheckCommand { get; }

    public ReactiveCommand<Unit, Unit> ClearCachesCommand { get; } 

    public ReactiveCommand<Unit, Unit> RandomlySelectCommand { get; }

    public ReactiveCommand<Unit, Unit> DeleteRedundantVpkFilesCommand { get; }

    public ReactiveCommand<Unit, Unit> DeleteRedundantVpkFilesForSelectedItemsCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenAboutWindowCommand { get; }

    public ReactiveCommand<Unit, Unit> CheckUpdateCommand { get; }

    public Interaction<Unit, string?> ChooseDirectoryInteraction { get; } = new();

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

    public Interaction<Exception, Unit> ShowExceptionInteraction { get; } = new();

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
        
        _addonRoot.Check();
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

        _addonRoot.Check();
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

    public void RandomlySelect()
    {
        if (_addonRoot != null)
        {
            foreach (var addonNode in _addonRoot.GetDescendants())
            {
                if (addonNode is AddonGroup addonGroup)
                {
                    addonGroup.EnableOneChildRandomlyIfSingleRandom();
                }
            }
        }
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
                    if (WorkshopVpkAddon.TryParsePublishedFileIdLink(clipboardText, out var id))
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
                return await clipboard.GetTextAsync();
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
}
