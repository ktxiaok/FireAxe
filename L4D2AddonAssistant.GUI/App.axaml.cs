using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using L4D2AddonAssistant.ViewModels;
using L4D2AddonAssistant.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Net.Http;

namespace L4D2AddonAssistant
{
    public partial class App : Application
    {
        public const string DocumentDirectoryName = "L4D2AddonAssistant";

        private string _documentDirectoryPath;

        public event Action? ShutdownRequested = null;

        public App()
        {
            _documentDirectoryPath = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), DocumentDirectoryName);
            Directory.CreateDirectory(_documentDirectoryPath);

            Services = new ServiceCollection()
                .AddSingleton(this)
                .AddSingleton<MainWindowViewModel>()
                .AddSingleton<MainWindow>(services => new MainWindow() { DataContext = services.GetRequiredService<MainWindowViewModel>()})
                .AddSingleton<AppSettings>()
                .AddSingleton<AppSettingsViewModel>()
                .AddSingleton<SaveManager>()
                .AddSingleton<IAppWindowManager, AppWindowManager>()
                .AddSingleton<IDownloadService, DownloadService>()
                .AddSingleton<HttpClient>()
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
                Services.GetRequiredService<AppSettings>();

                desktop.ShutdownRequested += (sender, args) =>
                {
                    ShutdownRequested?.Invoke();
                };

                var mainWindow = Services.GetRequiredService<MainWindow>();

                desktop.MainWindow = mainWindow;

                var saveManager = Services.GetRequiredService<SaveManager>();
                saveManager.Register(Services.GetRequiredService<MainWindowViewModel>());
                saveManager.Register(Services.GetRequiredService<AppSettings>());
                saveManager.Run();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}