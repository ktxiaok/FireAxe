using Avalonia.Controls;
using FireAxe.ViewModels;
using FireAxe.Views;
using System;
using System.Net.Http;

namespace FireAxe;

public class AppWindowManager : IAppWindowManager
{
    private readonly AppSettings _settings;
    private readonly DownloadItemListViewModel _downloadItemListViewModel;
    private readonly HttpClient _httpClient;

    private MainWindow? _mainWindow = null;
    private MainWindowViewModel? _mainWindowViewModel = null;
    private WindowReference<AppSettingsWindow>? _settingsWindowRef = null;
    private WindowReference<DownloadItemListWindow>? _downloadItemListWindowRef = null;
    private WindowReference<AboutWindow>? _aboutWindowRef = null;
    private WindowReference<AddonTagManagerWindow>? _tagManagerWindowRef = null;
    private WindowReference<AddonProblemListWindow>? _problemListWindowRef = null;
    private WindowReference<VpkAddonConflictListWindow>? _vpkConflictListWindowRef = null;

    public AppWindowManager(AppSettings settings, DownloadItemListViewModel downloadItemListViewModel, HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(downloadItemListViewModel);
        ArgumentNullException.ThrowIfNull(httpClient);
        _settings = settings;
        _downloadItemListViewModel = downloadItemListViewModel;
        _httpClient = httpClient;
    }

    public MainWindow? MainWindow => _mainWindow;

    public MainWindowViewModel? MainWindowViewModel => _mainWindowViewModel;

    public MainWindow CreateMainWindow(MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        if (_mainWindow is not null)
        {
            throw new InvalidOperationException("The main window already exists.");
        }

        _mainWindowViewModel = viewModel;
        _mainWindow = new MainWindow()
        {
            DataContext = viewModel
        };
        return _mainWindow;
    }

    public void OpenSettingsWindow()
    {
        var mainWindowViewModel = MainWindowViewModel;
        if (mainWindowViewModel is null)
        {
            return;
        }
        OpenWindow(ref _settingsWindowRef, () => new AppSettingsWindow
        {
            DataContext = new AppSettingsViewModel(_settings, mainWindowViewModel)
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

    public void OpenProblemListWindow()
    {
        var addonRoot = MainWindowViewModel?.AddonRoot;
        if (addonRoot is null)
        {
            return;
        }
        OpenWindow(ref _problemListWindowRef, () => new AddonProblemListWindow
        {
            DataContext = new AddonProblemListViewModel(addonRoot)
        });
    }

    public void OpenTagManagerWindow()
    {
        var mainWindowViewModel = MainWindowViewModel;
        if (mainWindowViewModel is null)
        {
            return;
        }
        OpenWindow(ref _tagManagerWindowRef, () => new AddonTagManagerWindow()
        {
            DataContext = new AddonTagManagerViewModel(mainWindowViewModel)
        });
    }

    public void OpenVpkConflictListWindow()
    {
        var addonRoot = MainWindowViewModel?.AddonRoot;
        if (addonRoot is null)
        {
            return;
        }
        OpenWindow(ref _vpkConflictListWindowRef, () => new VpkAddonConflictListWindow
        {
            DataContext = new VpkAddonConflictListViewModel(addonRoot)
        });
    }

    public void OpenWorkshopVpkFinderWindow()
    {
        var mainWindowViewModel = MainWindowViewModel;
        if (mainWindowViewModel is null)
        {
            return;
        }
        var window = new WorkshopVpkFinderWindow
        {
            DataContext = new WorkshopVpkFinderViewModel(mainWindowViewModel, _httpClient)
        };
        window.Show();
    }

    public void OpenFileCleanerWindow()
    {
        var addonRoot = MainWindowViewModel?.AddonRoot;
        if (addonRoot is null)
        {
            return;
        }
        var window = new FileCleanerWindow
        {
            DataContext = new FileCleanerViewModel(addonRoot)
        };
        window.Show();
    }

    public void OpenAddonNameAutoSetterWindow()
    {
        var addonRoot = MainWindowViewModel?.AddonRoot;
        if (addonRoot is null)
        {
            return;
        }
        var window = new AddonNameAutoSetterWindow
        {
            DataContext = new AddonNameAutoSetterViewModel(addonRoot)
        };
        window.Show();
    }

    public void OpenFileNameFixerWindow()
    {
        var addonRoot = MainWindowViewModel?.AddonRoot;
        if (addonRoot is null)
        {
            return;
        }
        var window = new FileNameFixerWindow
        {
            DataContext = new FileNameFixerViewModel(addonRoot)
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
