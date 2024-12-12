using L4D2AddonAssistant.ViewModels;
using L4D2AddonAssistant.Views;
using System;

namespace L4D2AddonAssistant
{
    public interface IAppWindowManager
    {
        MainWindow? MainWindow { get; }

        MainWindow CreateMainWindow(MainWindowViewModel viewModel);

        void OpenSettingsWindow();

        void OpenDownloadListWindow();

        void OpenAboutWindow();

        void OpenNewWorkshopCollectionWindow(AddonRoot addonRoot, AddonGroup? addonGroup);
    }
}
