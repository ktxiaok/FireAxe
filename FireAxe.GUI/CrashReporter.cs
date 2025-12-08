using System;
using System.Diagnostics;
using System.IO;
using FireAxe.Resources;
using Serilog;

namespace FireAxe;

public static class CrashReporter
{
    private const string Title = "====FireAxe Crash Reporter====";

    public static void Run(Exception unhandledException)
    {
        ArgumentNullException.ThrowIfNull(unhandledException);

        string message = $"{Texts.CrashReporterMessageHeader}\n{unhandledException}";

        if (OperatingSystem.IsWindows())
        {
            string? messagePath = null;
            string? batPath = null;
            
            try
            {
                messagePath = Path.GetTempFileName();
                batPath = Path.GetTempFileName() + ".bat";
                File.WriteAllText(messagePath, message);
                string bat = $"""
                    @echo off
                    chcp 65001
                    @echo off
                    echo.
                    echo {Title}
                    echo.
                    type "{messagePath}"
                    echo.
                    pause
                    """;
                File.WriteAllText(batPath, bat);
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = batPath,
                    UseShellExecute = false,
                }) ?? throw new Exception("Failed to create a bat process.");
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                LogException(ex);
            }

            try
            {
                if (messagePath is not null && File.Exists(messagePath))
                {
                    File.Delete(messagePath);
                }
                if (batPath is not null && File.Exists(batPath))
                {
                    File.Delete(batPath);
                }
            }
            catch (Exception ex)
            {
                LogException(ex);
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            string? messagePath = null;
            string? shPath = null;

            try
            {
                messagePath = Path.GetTempFileName();
                shPath = Path.GetTempFileName();
                File.WriteAllText(messagePath, message);
                string sh = $"""
                    set +x
                    echo "{Title}"
                    echo ""
                    cat "{messagePath}"
                    echo ""
                    read -n 1 -s -p "Press any key to continue..."
                    """;
                File.WriteAllText(shPath, sh);
                var shFileMode = File.GetUnixFileMode(shPath);
                shFileMode |= (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
                File.SetUnixFileMode(shPath, shFileMode);
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"\"{shPath}\"",
                    UseShellExecute = true,
                }) ?? throw new Exception("Failed to start a bash process");
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                LogException(ex);
            }

            try
            {
                if (messagePath is not null && File.Exists(messagePath))
                {
                    File.Delete(messagePath);
                }
                if (shPath is not null && File.Exists(shPath))
                {
                    File.Delete(shPath);
                }
            }
            catch (Exception ex)
            {
                LogException(ex);
            }
        }

        void LogException(Exception ex) => Log.Error(ex, "Exception occurred during CrashReporter.Run.");
    }
}