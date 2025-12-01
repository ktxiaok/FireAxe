using FireAxe.ViewModels;
using FireAxe.Views;
using System;

namespace FireAxe;

public interface IAppWindowManager
{
    MainWindow? MainWindow { get; }

    MainWindowViewModel? MainWindowViewModel { get; }

    MainWindow CreateMainWindow(MainWindowViewModel viewModel);

    void OpenSettingsWindow();

    void OpenDownloadListWindow();

    void OpenAboutWindow();

    void OpenProblemListWindow();

    void OpenTagManagerWindow();

    void OpenVpkConflictListWindow();

    void OpenWorkshopVpkFinderWindow();

    void OpenFileCleanerWindow();

    void OpenAddonNameAutoSetterWindow();

    void OpenFileNameFixerWindow();
}
