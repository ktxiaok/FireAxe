using ReactiveUI;
using System;
using System.IO;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
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

        private AddonRoot? _addonRoot = null;
        private IObservable<bool> _addonRootNotNull;

        private AddonNodeExplorerViewModel? _addonNodeExplorerViewModel = null;

        private readonly ObservableAsPropertyHelper<string> _titleExtraInfo;

        public MainWindowViewModel(AppSettings settings, IAppWindowManager windowManager, IDownloadService downloadService, HttpClient httpClient)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(windowManager);
            ArgumentNullException.ThrowIfNull(downloadService);
            ArgumentNullException.ThrowIfNull(httpClient);
            _settings = settings;
            _windowManager = windowManager;
            _downloadService = downloadService;
            _httpClient = httpClient;

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
                if (path != null)
                {
                    OpenDirectory(path);
                }
            });
            ImportCommand = ReactiveCommand.CreateFromTask(Import, _addonRootNotNull);
            OpenSettingsWindowCommand = ReactiveCommand.Create(() => _windowManager.OpenSettingsWindow());
            PushCommand = ReactiveCommand.CreateFromTask(Push, _addonRootNotNull);
            CheckCommand = ReactiveCommand.Create(Check, _addonRootNotNull);
            ClearCachesCommand = ReactiveCommand.Create(ClearCaches, _addonRootNotNull);
            OpenAboutWindowCommand = ReactiveCommand.Create(() => _windowManager.OpenAboutWindow());

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

            // Try to open the LastOpenDirectory.
            var lastOpenDir = _settings.LastOpenDirectory;
            if (lastOpenDir != null)
            {
                if (Directory.Exists(lastOpenDir))
                {
                    OpenDirectory(lastOpenDir);
                }
                else
                {
                    _settings.LastOpenDirectory = null;
                    _settings.Save();
                }
            }

            this.WhenActivated((CompositeDisposable disposables) =>
            {

            });
        }

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
                    _addonRoot.TaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
                    _addonRoot.DownloadService = _downloadService;
                    _addonRoot.HttpClient = _httpClient;
                    _addonRoot.GamePath = _settings.GamePath;
                    _addonRoot.IsAutoUpdateWorkshopItem = _settings.IsAutoUpdateWorkshopItem;
                    _addonRoot.LoadFile();
                    foreach (var addonNode in _addonRoot.GetAllNodes())
                    {
                        addonNode.Check();
                    }
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

        public ReactiveCommand<Unit, Unit> PushCommand { get; } 

        public ReactiveCommand<Unit, Unit> CheckCommand { get; }

        public ReactiveCommand<Unit, Unit> ClearCachesCommand { get; } 

        public ReactiveCommand<Unit, Unit> OpenAboutWindowCommand { get; }

        public Interaction<Unit, string?> ChooseDirectoryInteraction { get; } = new();

        public Interaction<Unit, Unit> ShowImportSuccessInteraction { get; } = new();

        public Interaction<Exception, Unit> ShowImportErrorInteraction { get; } = new();

        public Interaction<Unit, Unit> ShowPushSuccessInteraction { get; } = new();

        public Interaction<Exception, Unit> ShowPushErrorInteraction { get; } = new();

        public Interaction<string, Unit> ShowInvalidGamePathInteraction { get; } = new();

        public void OpenDirectory(string dirPath)
        {
            ArgumentNullException.ThrowIfNull(dirPath);

            var addonRoot = new AddonRoot();
            addonRoot.DirectoryPath = dirPath;
            // TEST
            //DesignHelper.AddTestAddonNodes(addonRoot);
            AddonRoot = addonRoot;

            _settings.LastOpenDirectory = dirPath;
            _settings.Save();
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
    }
}
