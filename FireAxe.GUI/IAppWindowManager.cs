using FireAxe.ViewModels;
using FireAxe.Views;
using System;

namespace FireAxe;

public interface IAppWindowManager
{
    MainWindow? MainWindow { get; }

    MainWindow CreateMainWindow(MainWindowViewModel viewModel);

    void OpenSettingsWindow();

    void OpenDownloadListWindow();

    void OpenAboutWindow();

    void OpenNewWorkshopCollectionWindow(AddonRoot addonRoot, AddonGroup? addonGroup);

    void OpenTagManagerWindow(MainWindowViewModel mainWindowViewModel);

    void OpenVpkConflictListWindow(AddonRoot addonRoot);
}
