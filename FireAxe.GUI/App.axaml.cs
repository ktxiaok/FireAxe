using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using FireAxe.ViewModels;
using FireAxe.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

namespace FireAxe;

public partial class App : Application
{
    public const string DocumentDirectoryName = "FireAxe";

    private string _documentDirectoryPath;

    public event Action? ShutdownRequested = null;

    public App()
    {
        _documentDirectoryPath = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), DocumentDirectoryName);
        Directory.CreateDirectory(_documentDirectoryPath);

        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(".NET", "8.0"));

        Services = new ServiceCollection()
            .AddSingleton(this)
            .AddSingleton<MainWindowViewModel>()
            .AddSingleton<AppSettings>()
            .AddSingleton<AppSettingsViewModel>()
            .AddSingleton<DownloadItemListViewModel>()
            .AddSingleton<SaveManager>()
            .AddSingleton<IAppWindowManager, AppWindowManager>()
            .AddSingleton<IDownloadService, DownloadService>()
            .AddSingleton(httpClient)
            .BuildServiceProvider();
    }

    //public static new App Current => (App?)Application.Current ?? throw new InvalidOperationException("The App.Current is null.");

    public IServiceProvider Services { get; }

    public string DocumentDirectoryPath => _documentDirectoryPath;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose; 

            Services.GetRequiredService<AppSettings>();

            desktop.ShutdownRequested += (sender, args) =>
            {
                ShutdownRequested?.Invoke();
            };

            var mainWindowViewModel = Services.GetRequiredService<MainWindowViewModel>();
            var mainWindow = Services.GetRequiredService<IAppWindowManager>().CreateMainWindow(mainWindowViewModel);
            desktop.MainWindow = mainWindow;
            mainWindow.Show();

            var saveManager = Services.GetRequiredService<SaveManager>();
            saveManager.Register(Services.GetRequiredService<MainWindowViewModel>());
            saveManager.Register(Services.GetRequiredService<AppSettings>());
            saveManager.Run();
        }

        base.OnFrameworkInitializationCompleted();
    }
}