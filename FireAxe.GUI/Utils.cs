using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using ReactiveUI;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;

namespace FireAxe;

internal static class Utils
{
    private static readonly string[] s_byteUnits = ["B", "KiB", "MiB", "GiB", "TiB"];

    private static readonly object s_openDirectoryCustomProcessLock = new();
    private static readonly object s_showFileCustomProcessLock = new();

    public static string? OpenDirectoryCustomProcessFileName
    {
        get
        {
            lock (s_openDirectoryCustomProcessLock)
            {
                return field;
            }
        }
        set
        {
            lock (s_openDirectoryCustomProcessLock)
            {
                field = value;
            }
        }
    } = null;

    public static string? OpenDirectoryCustomProcessArguments
    {
        get
        {
            lock (s_openDirectoryCustomProcessLock)
            {
                return field;
            }
        }
        set
        {
            lock (s_openDirectoryCustomProcessLock)
            {
                field = value;
            }
        }
    } = null;

    public static string? ShowFileCustomProcessFileName
    {
        get
        {
            lock (s_showFileCustomProcessLock)
            {
                return field;
            }
        }
        set
        {
            lock (s_showFileCustomProcessLock)
            {
                field = value;
            }
        }
    } = null;

    public static string? ShowFileCustomProcessArguments
    {
        get
        {
            lock (s_showFileCustomProcessLock)
            {
                return field;
            }
        }
        set
        {
            lock (s_showFileCustomProcessLock)
            {
                field = value;
            }
        }
    }

    public static void GetReadableBytes(double bytes, out double value, out string unit)
    {
        value = bytes;
        int i = 0;
        while (value >= 1000 && i < s_byteUnits.Length)
        {
            value /= 1024;
            i++;
        }
        unit = s_byteUnits[i];
    }

    public static string GetReadableBytes(double bytes)
    {
        GetReadableBytes(bytes, out double value, out string unit);
        return value.ToString("F1") + unit;
    }

    public static void OpenWebsite(string url)
    {
        ArgumentNullException.ThrowIfNull(url);

        try
        {
            using var process = Process.Start(new ProcessStartInfo(url)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred during Utils.OpenWebsite");
        }
    }

    public static void OpenSteamWorkshopPage(ulong publishedFileId)
    {
        OpenWebsite($"https://steamcommunity.com/sharedfiles/filedetails/?id={publishedFileId}");
    }

    public static void ShowInFileExplorer(string path, bool openDir = false)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (Path.DirectorySeparatorChar != '/')
        {
            path = path.Replace('/', Path.DirectorySeparatorChar);
        }
        try
        {
            if (openDir && Directory.Exists(path)
                && OpenDirectoryCustomProcessFileName is var openDirCustomProcessFileName && !string.IsNullOrEmpty(openDirCustomProcessFileName)
                && OpenDirectoryCustomProcessArguments is var openDirCustomProcessArgs && !string.IsNullOrEmpty(openDirCustomProcessArgs))
            {
                using var process = Process.Start(openDirCustomProcessFileName, string.Format(openDirCustomProcessArgs, path));
            }
            else if (ShowFileCustomProcessFileName is var showFileCustomProcessFileName && !string.IsNullOrEmpty(showFileCustomProcessFileName)
                && ShowFileCustomProcessArguments is var showFileCustomProcessArgs && !string.IsNullOrEmpty(showFileCustomProcessArgs))
            {
                using var process = Process.Start(showFileCustomProcessFileName, string.Format(showFileCustomProcessArgs, path));
            }
            else if (OperatingSystem.IsWindows())
            {
                string processArgs;
                if (openDir && Directory.Exists(path))
                {
                    processArgs = $"\"{path}\"";
                }
                else
                {
                    processArgs = $"/select, \"{path}\"";
                }
                using var process = Process.Start("explorer.exe", processArgs);
            }
            else if (OperatingSystem.IsLinux())
            {
                string processArgs;
                if (openDir && Directory.Exists(path))
                {
                    processArgs = $"\"{path}\"";
                }
                else
                {
                    processArgs = $"--select \"{path}\"";
                }
                using var process = Process.Start("nautilus", processArgs);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Exception occurred during {nameof(Utils)}.{nameof(ShowInFileExplorer)}.");
        }
    }

    public static void DisposeAndSetNull<T>(ref T? disposable) where T : class, IDisposable
    {
        if (disposable != null)
        {
            disposable.Dispose();
            disposable = null;
        }
    }

    public static Window GetRootWindow(this Visual visual)
    {
        ArgumentNullException.ThrowIfNull(visual);

        return (visual.GetVisualRoot() as Window) ?? throw new InvalidOperationException($"Failed to get the root window of {visual.GetType().FullName}");
    }

    public static void RegisterViewModelConnection<TViewModel>(this IViewFor<TViewModel> view, Action<TViewModel, CompositeDisposable> connect) where TViewModel : class
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(connect);

        view.WhenActivated((CompositeDisposable disposables) =>
        {
            CompositeDisposable? connectionDisposable = null;

            void Disconnect() => Utils.DisposeAndSetNull(ref connectionDisposable);

            view.WhenAnyValue(x => x.ViewModel)
                .Subscribe(viewModel =>
                {
                    Disconnect();
                    if (viewModel is null)
                    {
                        return;
                    }
                    connectionDisposable = new();
                    connect(viewModel, connectionDisposable);
                })
                .DisposeWith(disposables);

            Disposable.Create(Disconnect).DisposeWith(disposables);
        });
    }

    public static string FormatNoThrow(this string? str, params object?[] args)
    {
        if (str is null)
        {
            return "";
        }
        
        try
        {
            return string.Format(str, args);
        }
        catch (FormatException ex)
        {
            Log.Error(ex, "FormatException ocurred during Utils.FormatNoThrow.");
        }

        return str;
    }
}
