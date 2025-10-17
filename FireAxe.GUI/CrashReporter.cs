using System;
using System.Diagnostics;
using System.IO;
using FireAxe.Resources;
using Serilog;

namespace FireAxe;

public static class CrashReporter
{
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
                    echo ====FireAxe Crash Reporter====
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

        void LogException(Exception ex) => Log.Error(ex, "Exception occurred during CrashReporter.Run.");
    }
}