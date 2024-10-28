using ReactiveUI;
using System;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;

namespace L4D2AddonAssistant.ViewModels
{
    public class MainWindowViewModel : ViewModelBase, ISaveable
    {
        private AppSettings _settings;
        private CommonInteractions _commonInteractions;

        private AddonRoot? _addonRoot = null;
        private IObservable<bool> _addonRootNotNull;

        private AddonNodeExplorerViewModel? _addonNodeExplorerViewModel = null;

        public MainWindowViewModel(AppSettings settings, CommonInteractions commonInteractions)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(commonInteractions);
            _settings = settings;
            _commonInteractions = commonInteractions;

            _addonRootNotNull = this.WhenAnyValue(x => x.AddonRoot).Select(root => root != null);

            OpenDirectoryCommand = ReactiveCommand.CreateFromObservable(() =>
            {
                return _commonInteractions.ChooseDirectory.Handle(Unit.Default).Select((path) =>
                {
                    if (path != null)
                    {
                        OpenDirectory(path);
                    }
                    return Unit.Default;
                });
            });
            ImportCommand = ReactiveCommand.Create(Import, _addonRootNotNull);

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
        }

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
            set
            {
                _addonRoot = value;
                if (_addonRoot == null)
                {
                    AddonNodeExplorerViewModel = null;
                }
                else
                {
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

        public ReactiveCommand<Unit, Unit> OpenDirectoryCommand { get; }

        public ReactiveCommand<Unit, Unit> ImportCommand { get; }

        public void OpenDirectory(string dirPath)
        {
            ArgumentNullException.ThrowIfNull(dirPath);

            var addonRoot = new AddonRoot();
            addonRoot.DirectoryPath = dirPath;
            addonRoot.LoadFile();
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

        public void Import()
        {
            if (_addonRoot != null)
            {
                _addonRoot.Import();
            }
        }
    }
}
