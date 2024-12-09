using ReactiveUI;
using Serilog;
using System;
using System.IO;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace L4D2AddonAssistant.ViewModels
{
    public class MainWindowViewModel : ViewModelBase, IActivatableViewModel, ISaveable, IDisposable
    {
        private bool _disposed = false;

        private AppSettings _settings;
        private IAppWindowManager _windowManager;
        private IDownloadService _downloadService;
        private HttpClient _httpClient;
        private DownloadItemListViewModel _downloadItemListViewModel;

        private AddonRoot? _addonRoot = null;
        private IObservable<bool> _addonRootNotNull;

        private AddonNodeExplorerViewModel? _addonNodeExplorerViewModel = null;

        private readonly ObservableAsPropertyHelper<string> _titleExtraInfo;

        private bool _isCheckingUpdate = false;
        private object? _checkUpdateActivity = null;
        private CancellationTokenSource? _checkUpdateCts = null;

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

            _addonRootNotNull = this.WhenAnyValue(x => x.AddonRoot).Select(root => root != null);

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
                OpenDirectory(path);               
            });
            ImportCommand = ReactiveCommand.CreateFromTask(Import, _addonRootNotNull);
            OpenSettingsWindowCommand = ReactiveCommand.Create(() => _windowManager.OpenSettingsWindow());
            OpenDownloadListWindowCommand = ReactiveCommand.Create(() => _windowManager.OpenDownloadListWindow());
            PushCommand = ReactiveCommand.CreateFromTask(Push, _addonRootNotNull);
            CheckCommand = ReactiveCommand.Create(Check, _addonRootNotNull);
            ClearCachesCommand = ReactiveCommand.Create(ClearCaches, _addonRootNotNull);
            RandomlySelectCommand = ReactiveCommand.Create(RandomlySelect, _addonRootNotNull);
            OpenAboutWindowCommand = ReactiveCommand.Create(() => _windowManager.OpenAboutWindow());
            CheckUpdateCommand = ReactiveCommand.Create(() => CheckUpdate(false));

            _settings.WhenAnyValue(x => x.GamePath).Subscribe((gamePath) =>
            {
                if (_addonRoot != null)
                {
                    _addonRoot.GamePath = gamePath;
                }
            });
            _settings.WhenAnyValue(x => x.IsAutoUpdateWorkshopItem).Subscribe((isAutoUpdateWorkshopItem) =>
            {
                if (_addonRoot != null)
                {
                    _addonRoot.IsAutoUpdateWorkshopItem = isAutoUpdateWorkshopItem;
                }
            });

            bool initExecuted = false;
            this.WhenActivated((CompositeDisposable disposables) =>
            {
                if (!initExecuted)
                {
                    initExecuted = true;

                    CheckUpdate(true);

                    // Try to open the LastOpenDirectory.
                    var lastOpenDir = _settings.LastOpenDirectory;
                    if (lastOpenDir != null)
                    {
                        _settings.LastOpenDirectory = null;
                        _settings.Save();
                        if (Directory.Exists(lastOpenDir))
                        {
                            OpenDirectory(lastOpenDir);
                        }
                    }
                }
            });
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
                // TODO
                _addonRoot?.DisposeAsync();
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
                    _addonRoot.CheckAll();
                    AddonNodeExplorerViewModel = new(_addonRoot);
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

        public ReactiveCommand<Unit, Unit> OpenDirectoryCommand { get; }

        public ReactiveCommand<Unit, Unit> ImportCommand { get; }

        public ReactiveCommand<Unit, Unit> OpenSettingsWindowCommand { get; }

        public ReactiveCommand<Unit, Unit> OpenDownloadListWindowCommand { get; }

        public ReactiveCommand<Unit, Unit> PushCommand { get; } 

        public ReactiveCommand<Unit, Unit> CheckCommand { get; }

        public ReactiveCommand<Unit, Unit> ClearCachesCommand { get; } 

        public ReactiveCommand<Unit, Unit> RandomlySelectCommand { get; }

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

        public void OpenDirectory(string dirPath)
        {
            ArgumentNullException.ThrowIfNull(dirPath);

            if (GamePathUtils.IsAddonsPath(dirPath))
            {
                return;
            }

            var addonRoot = new AddonRoot();
            addonRoot.DirectoryPath = dirPath;
            AddonRoot = addonRoot;

            _settings.LastOpenDirectory = dirPath;
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
            if (_addonRoot != null)
            {
                _addonRoot.CheckAll();
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
        }

        public void Check()
        {
            if (_addonRoot != null)
            {
                foreach (var addonNode in _addonRoot.GetAllNodes())
                {
                    addonNode.Check();
                }
            }
        }

        public void ClearCaches()
        {
            if (_addonRoot != null)
            {
                foreach (var addonNode in _addonRoot.GetAllNodes())
                {
                    addonNode.ClearCaches();
                }
            }
        }

        public void RandomlySelect()
        {
            if (_addonRoot != null)
            {
                foreach (var addonNode in _addonRoot.GetAllNodes())
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

        public void DummyCrash()
        {
            throw new Exception("dummy crash");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _addonRoot?.DisposeAsync();
            }
        }

        private void OnAddonRootNewDownloadItem(IDownloadItem downloadItem)
        {
            _downloadItemListViewModel.AddDownloadItem(downloadItem);
        }
    }
}
