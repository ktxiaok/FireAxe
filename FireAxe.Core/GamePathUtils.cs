using Microsoft.Win32;
using Serilog;
using System;

namespace FireAxe;

public static class GamePathUtils
{
    public static bool CheckValidity(string gamePath)
    {
        ArgumentNullException.ThrowIfNull(gamePath);

        if (!FileSystemUtils.IsValidPath(gamePath) || !Path.IsPathRooted(gamePath))
        {
            return false;
        }

        string appidPath = Path.Join(gamePath, "steam_appid.txt");
        try
        {
            if (File.Exists(appidPath))
            {
                return File.ReadAllText(appidPath).Trim(' ', '\n', '\0') == "550";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred during GamePathUtils.CheckValidity.");
        }

        return false;
    }

    public static string? TryFind()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var localMachineRegistry = Registry.LocalMachine;
                using var steamRegistry = localMachineRegistry.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam") ?? localMachineRegistry.OpenSubKey(@"SOFTWARE\Valve\Steam");
                if (steamRegistry is not null)
                {
                    if (steamRegistry.GetValue("InstallPath")?.ToString() is { } steamInstallPath && FileSystemUtils.IsValidPath(steamInstallPath))
                    {
                        string gamePath = Path.Join(steamInstallPath, "steamapps", "common", "Left 4 Dead 2");
                        if (CheckValidity(gamePath))
                        {
                            return gamePath;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred during reading Windows Registry at GamePathUtils.TryFind.");
            }

            string[]? driveNames = null;
            try
            {
                driveNames = DriveInfo.GetDrives().Select(drive => drive.Name).ToArray();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred during getting drive info at GamePathUtils.TryFind.");
            }
            if (driveNames is not null)
            {
                foreach (string driveName in driveNames)
                {
                    string gamePath = Path.Join(driveName, "SteamLibrary", "steamapps", "common", "Left 4 Dead 2");
                    if (CheckValidity(gamePath))
                    {
                        return gamePath;
                    }
                }
            }
        }

        return null;
    }

    public static string GetAddonsPath(string gamePath)
    {
        ArgumentNullException.ThrowIfNull(gamePath);

        return Path.Join(gamePath, "left4dead2", "addons");
    }

    public static string GetAddonListPath(string gamePath)
    {
        ArgumentNullException.ThrowIfNull(gamePath);

        return Path.Join(gamePath, "left4dead2", "addonlist.txt");
    }

    public static bool IsAddonsPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        try
        {
            var path2 = Path.GetDirectoryName(path);
            if (path2 == null)
            {
                return false;
            }
            path2 = Path.GetDirectoryName(path2);
            if (path2 == null)
            {
                return false;
            }
            return CheckValidity(path2);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Exception occurred during GamePathUtils.IsAddonsPath");
        }

        return false;
    }
}
