using Serilog;
using System;

namespace L4D2AddonAssistant
{
    public static class GamePathUtils
    {
        public static bool CheckValidity(string gamePath)
        {
            ArgumentNullException.ThrowIfNull(gamePath);

            string appidPath = Path.Join(gamePath, "steam_appid.txt");
            try
            {
                if (File.Exists(appidPath))
                {
                    return File.ReadAllText(appidPath).Trim() == "550";
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
    }
}
