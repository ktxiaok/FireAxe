﻿using Serilog;
using System;

namespace FireAxe
{
    public static class GamePathUtils
    {
        public static bool CheckValidity(string gamePath)
        {
            ArgumentNullException.ThrowIfNull(gamePath);

            if (!FileUtils.IsValidPath(gamePath) || !Path.IsPathRooted(gamePath))
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
                Log.Warning(ex, "Exception in GamePathUtils.CheckValidity.");
            }

            return false;
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
}
