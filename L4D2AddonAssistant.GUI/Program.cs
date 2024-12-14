using Avalonia;
using Avalonia.ReactiveUI;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace L4D2AddonAssistant
{
    internal sealed class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            SetupLogger();
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            Log.Information("L4D2AddonAssistant Start (Version: {Version})", AppGlobal.VersionString);
            try
            {
                RegisterObjectExplanations();

                BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Unhandled Exception");
                try
                {
                    OpenCrashReporter(ex);
                }
                catch (Exception ex2)
                {
                    Log.Error(ex2, "Exception occurred during Program.OpenCrashReporter.");
                }
                throw;
            }
            finally
            {
                Log.CloseAndFlush();
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
                .WriteTo.File("Logs/Log.txt", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 31)
                .CreateLogger();
        }

        private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Log.Error(e.Exception, "Unobserved task exception occurred");
        }

        private static void OpenCrashReporter(Exception ex)
        {
            string pipeName = Guid.NewGuid().ToString();
            using var crashReporterProcess = new Process();
            crashReporterProcess.StartInfo.FileName = "L4D2AddonAssistant.CrashReporter.exe";
            crashReporterProcess.StartInfo.UseShellExecute = true;
            crashReporterProcess.StartInfo.Arguments = pipeName;
            crashReporterProcess.Start();

            try
            {
                using (var pipeServer = new NamedPipeServerStream(pipeName))
                {
                    pipeServer.WaitForConnection();
                    
                    string message = "L4D2AddonAssistant crashed due to an unhandled exception. The following is the details of the exception.\n" + ex.ToString();
                    using (var writer = new BinaryWriter(pipeServer))
                    {
                        writer.Write(message);
                    }
                }
            }
            catch (IOException ioEx)
            {
                Log.Warning(ioEx, "IOException occurred during Program.OpenCrashReporter.");
            }

            crashReporterProcess.WaitForExit();
        }

        private static void RegisterObjectExplanations()
        {
            var defaultManager = ObjectExplanationManager.Default;
            ExceptionExplanations.Register(defaultManager);
            AddonProblemExplanations.Register(defaultManager);
        }
    }
}
