using L4D2AddonAssistant.ViewModels;
using L4D2AddonAssistant.Views;
using System;

namespace L4D2AddonAssistant
{
    public class AppWindowManager : IAppWindowManager
    {
        private AppSettingsViewModel _settingsViewModel;
        private DownloadItemListViewModel _downloadItemListViewModel;

        private WindowReference<AppSettingsWindow>? _settingsWindow = null;
        private WindowReference<DownloadItemListWindow>? _downloadItemListWindow = null;
        private WindowReference<AboutWindow>? _aboutWindow = null;

        public AppWindowManager(AppSettingsViewModel settingsViewModel, DownloadItemListViewModel downloadItemListViewModel)
        {
            ArgumentNullException.ThrowIfNull(settingsViewModel);
            ArgumentNullException.ThrowIfNull(downloadItemListViewModel);
            _settingsViewModel = settingsViewModel;
            _downloadItemListViewModel = downloadItemListViewModel;
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
    }
}
