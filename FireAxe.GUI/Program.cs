using Avalonia;
using ReactiveUI.Avalonia;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace FireAxe;

internal sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args)
    {
        _ = LanguageManager.Instance;
        SetupLogger();
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        Log.Information("FireAxe Start (Version: {Version})", AppGlobal.VersionString);
        try
        {
            RegisterObjectExplanations();

            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Unhandled Exception");
            CrashReporter.Run(ex);
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
            AppMutex.Release();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();

    private static void SetupLogger()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("Logs/Log.txt", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
            .CreateLogger();
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unobserved task exception occurred");
    }

    private static void RegisterObjectExplanations()
    {
        var defaultManager = ObjectExplanationManager.Default;
        ExceptionExplanations.Register(defaultManager);
        AddonProblemExplanations.Register(defaultManager);
    }
}
