using Avalonia.Controls;
using FireAxe.ViewModels;
using FireAxe.Views;
using System;
using System.Net.Http;

namespace FireAxe;

public class AppWindowManager : IAppWindowManager
{
    private readonly AppSettingsViewModel _settingsViewModel;
    private readonly DownloadItemListViewModel _downloadItemListViewModel;
    private readonly HttpClient _httpClient;

    private MainWindow? _mainWindow = null;
    private WindowReference<AppSettingsWindow>? _settingsWindowRef = null;
    private WindowReference<DownloadItemListWindow>? _downloadItemListWindowRef = null;
    private WindowReference<AboutWindow>? _aboutWindowRef = null;
    private WindowReference<AddonTagManagerWindow>? _tagManagerWindowRef = null;
    private WindowReference<VpkAddonConflictListWindow>? _vpkConflictListWindowRef = null;

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
        OpenWindow(ref _settingsWindowRef, () => new AppSettingsWindow
        {
            DataContext = _settingsViewModel
        });
    }

    public void OpenDownloadListWindow()
    {
        OpenWindow(ref _downloadItemListWindowRef, () => new DownloadItemListWindow()
        {
            DataContext = _downloadItemListViewModel
        });
    }

    public void OpenAboutWindow()
    {
        OpenWindow(ref _aboutWindowRef, () => new AboutWindow());
    }

    public void OpenTagManagerWindow(MainWindowViewModel mainWindowViewModel)
    {
        OpenWindow(ref _tagManagerWindowRef, () => new AddonTagManagerWindow()
        {
            DataContext = new AddonTagManagerViewModel(mainWindowViewModel)
        });
    }

    public void OpenVpkConflictListWindow(AddonRoot addonRoot)
    {
        ArgumentNullException.ThrowIfNull(addonRoot);

        OpenWindow(ref _vpkConflictListWindowRef, () => new VpkAddonConflictListWindow
        {
            DataContext = new VpkAddonConflictListViewModel(addonRoot)
        });
    }

    public void OpenWorkshopVpkFinderWindow(MainWindowViewModel mainWindowViewModel)
    {
        ArgumentNullException.ThrowIfNull(mainWindowViewModel);

        var window = new WorkshopVpkFinderWindow
        {
            DataContext = new WorkshopVpkFinderViewModel(mainWindowViewModel, _httpClient)
        };
        window.Show();
    }

    public void OpenFileCleanerWindow(AddonRoot addonRoot)
    {
        ArgumentNullException.ThrowIfNull(addonRoot);

        var window = new FileCleanerWindow
        {
            DataContext = new FileCleanerViewModel(addonRoot)
        };
        window.Show();
    }

    private static void OpenWindow<T>(ref WindowReference<T>? windowRef, Func<T> windowFactory) where T : Window
    {
        if (windowRef is null || windowRef.Get() is null)
        {
            windowRef = new(windowFactory());
        }
        var window = windowRef.Get()!;
        window.Show();
        window.Activate();
    }
}
