﻿using L4D2AddonAssistant.ViewModels;
using L4D2AddonAssistant.Views;
using System;

namespace L4D2AddonAssistant
{
    public class AppWindowManager : IAppWindowManager
    {
        private AppSettingsViewModel _settingsViewModel;

        private WindowReference<AppSettingsWindow>? _settingsWindow = null;
        private WindowReference<AboutWindow>? _aboutWindow = null;

        public AppWindowManager(AppSettingsViewModel settingsViewModel)
        {
            ArgumentNullException.ThrowIfNull(settingsViewModel);
            _settingsViewModel = settingsViewModel;
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
