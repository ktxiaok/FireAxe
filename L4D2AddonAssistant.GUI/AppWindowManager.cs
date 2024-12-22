using L4D2AddonAssistant.ViewModels;
using L4D2AddonAssistant.Views;
using System;
using System.Net.Http;

namespace L4D2AddonAssistant
{
    public class AppWindowManager : IAppWindowManager
    {
        private AppSettingsViewModel _settingsViewModel;
        private DownloadItemListViewModel _downloadItemListViewModel;
        private HttpClient _httpClient;

        private MainWindow? _mainWindow = null;
        private WindowReference<AppSettingsWindow>? _settingsWindow = null;
        private WindowReference<DownloadItemListWindow>? _downloadItemListWindow = null;
        private WindowReference<AboutWindow>? _aboutWindow = null;
        private WindowReference<FlatVpkAddonListWindow>? _flatVpkAddonListWindow = null;
        private WindowReference<AddonTagManagerWindow>? _tagManagerWindow = null;

        public AppWindowManager(AppSettingsViewModel settingsViewModel, DownloadItemListViewModel downloadItemListViewModel, HttpClient httpClient)
        {
            ArgumentNullException.ThrowIfNull(settingsViewModel);
            ArgumentNullException.ThrowIfNull(downloadItemListViewModel);
            ArgumentNullException.ThrowIfNull(httpClient);
            _settingsViewModel = settingsViewModel;
            _downloadItemListViewModel = downloadItemListViewModel;
            _httpClient = httpClient;
        }

        public MainWindow? MainWindow => _mainWindow;

        public MainWindow CreateMainWindow(MainWindowViewModel viewModel)
        {
            _mainWindow = new MainWindow()
            {
                DataContext = viewModel
            };
            return _mainWindow;
        }

        public void OpenSettingsWindow()
        {
            if (_settingsWindow == null || _settingsWindow.Get() == null)
            {
                _settingsWindow = new(new AppSettingsWindow
                {
                    DataContext = _settingsViewModel
                });
            }
            var window = _settingsWindow.Get()!;
            window.Show();
            window.Activate();
        }

        public void OpenDownloadListWindow()
        {
            if (_downloadItemListWindow == null || _downloadItemListWindow.Get() == null)
            {
                _downloadItemListWindow = new(new DownloadItemListWindow()
                {
                    DataContext = _downloadItemListViewModel
                });
            }
            var window = _downloadItemListWindow.Get()!;
            window.Show();
            window.Activate();
        }

        public void OpenAboutWindow()
        {
            if (_aboutWindow == null || _aboutWindow.Get() == null)
            {
                _aboutWindow = new(new AboutWindow());
            }
            var window = _aboutWindow.Get()!;
            window.Show();
            window.Activate();
        }

        public void OpenNewWorkshopCollectionWindow(AddonRoot addonRoot, AddonGroup? addonGroup)
        {
            var window = new NewWorkshopCollectionWindow()
            {
                DataContext = new NewWorkshopCollectionViewModel(addonRoot, addonGroup, _httpClient)
            };
            window.Show();
        }

        public void OpenFlatVpkAddonListWindow(MainWindowViewModel mainWindowViewModel)
        {
            if (_flatVpkAddonListWindow == null || _flatVpkAddonListWindow.Get() == null)
            {
                _flatVpkAddonListWindow = new(new FlatVpkAddonListWindow()
                {
                    DataContext = new FlatVpkAddonListViewModel(mainWindowViewModel)
                });
            }
            var window = _flatVpkAddonListWindow.Get()!;
            window.Show();
            window.Activate();
        }

        public void OpenTagManagerWindow(MainWindowViewModel mainWindowViewModel)
        {
            if (_tagManagerWindow == null || _tagManagerWindow.Get() == null)
            {
                _tagManagerWindow = new(new AddonTagManagerWindow()
                {
                    DataContext = new AddonTagManagerViewModel(mainWindowViewModel)
                });
            }
            var window = _tagManagerWindow.Get()!;
            window.Show();
            window.Activate();
        }
    }
}
