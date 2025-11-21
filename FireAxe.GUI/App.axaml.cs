using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FireAxe.ViewModels;
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

        Services = new ServiceCollection()
            .AddSingleton(this)
            .AddSingleton<MainWindowViewModel>()
            .AddSingleton<AppSettings>()
            .AddSingleton<AppSettingsViewModel>()
            .AddSingleton<DownloadItemListViewModel>()
            .AddSingleton<SaveManager>()
            .AddSingleton<IAppWindowManager, AppWindowManager>()
            .AddSingleton<IDownloadService, DownloadService>(
                services => new DownloadService(services.GetRequiredService<AppSettings>().GetDownloadServiceSettings()))
            .AddSingleton<HttpClient>(services =>
            {
                var appSettings = services.GetRequiredService<AppSettings>();
                var socketsHttpHandler = new SocketsHttpHandler();
                if (appSettings.WebProxy is { } webProxy)
                {
                    socketsHttpHandler.UseProxy = true;
                    socketsHttpHandler.Proxy = webProxy;
                }
                var httpClient = new HttpClient(socketsHttpHandler);
                httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(".NET", null));
                return httpClient;
            })
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