using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FireAxe.ViewModels;
using FireAxe.Views;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace FireAxe;

public partial class App : Application
{
    public const string DocumentDirectoryName = "FireAxe";

    private IServiceProvider? _services = null;

    private string _documentDirectoryPath;

    public event Action? ShutdownRequested = null;

    public App()
    {
        _documentDirectoryPath = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), DocumentDirectoryName);
        Directory.CreateDirectory(_documentDirectoryPath);
    }

    //public static new App Current => (App?)Application.Current ?? throw new InvalidOperationException("The App.Current is null.");

    public IServiceProvider Services => _services ?? throw new InvalidOperationException($"{nameof(Services)} is not set.");

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

            if (AppMutex.TryEnter())
            {
                var services = new ServiceCollection()
                    .AddSingleton(this)
                    .AddSingleton<MainWindowViewModel>()
                    .AddSingleton<AppSettings>()
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

                _services = services;

                desktop.ShutdownRequested += (sender, args) =>
                {
                    ShutdownRequested?.Invoke();

                    args.Cancel = true;

                    var windows = desktop.Windows.ToArray();
                    foreach (var window in windows)
                    {
                        window.Close();
                    }

                    services.DisposeAsync()
                        .AsTask()
                        .ContinueWith(task =>
                        {
                            if (task.Exception is { } ex)
                            {
                                Log.Error(ex, "Exception occurred during disposing services.");
                            }
                            else
                            {
                                Log.Information("All services disposed successfully.");
                            }

                            desktop.Shutdown();
                        }, TaskScheduler.FromCurrentSynchronizationContext());

                    new Thread(() =>
                    {
                        try
                        {
                            Thread.Sleep(TimeSpan.FromSeconds(15));
                            Log.Warning("Application Shutdown Timeout: The application will be forcibly terminated.");
                            Program.OnExit();
                        }
                        finally
                        {
                            Environment.Exit(-1);
                        }
                    }){ IsBackground = true }.Start();
                };

                services.GetRequiredService<AppSettings>();

                var mainWindowViewModel = services.GetRequiredService<MainWindowViewModel>();
                var mainWindow = services.GetRequiredService<IAppWindowManager>().CreateMainWindow(mainWindowViewModel);
                desktop.MainWindow = mainWindow;
                mainWindow.Show();

                var saveManager = services.GetRequiredService<SaveManager>();
                saveManager.Register(services.GetRequiredService<MainWindowViewModel>());
                saveManager.Register(services.GetRequiredService<AppSettings>());
                saveManager.Run();
            }
            else
            {
                var messageWindow = new AppAlreadyRunningMessageWindow();
                desktop.MainWindow = messageWindow;
                messageWindow.Show();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}