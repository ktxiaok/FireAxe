using Serilog;
using SteamDatabase.ValvePak;
using System;
using ValveKeyValue;

namespace FireAxe;

internal static class VpkUtils
{
    public static VpkAddonInfo? GetAddonInfo(Package pak)
    {
        VpkAddonInfo? addonInfo = null;

        try
        {
            var addonInfoEntry = pak.FindEntry("addoninfo.txt");
            if (addonInfoEntry != null)
            {
                pak.ReadEntry(addonInfoEntry, out byte[] data);
                var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
                var stream = new MemoryStream(data);
                var document = kv.Deserialize(stream);

                var version = document.TryGetChildValueIgnoreCase("addonversion");
                var title = document.TryGetChildValueIgnoreCase("addontitle");
                var author = document.TryGetChildValueIgnoreCase("addonauthor");
                var desc = document.TryGetChildValueIgnoreCase("addondescription");

                string? versionStr = version?.ToString();
                string? titleStr = title?.ToString();
                string? authorStr = author?.ToString();
                string? descStr = desc?.ToString();

                addonInfo = new VpkAddonInfo
                {
                    Version = versionStr,
                    Title = titleStr,
                    Author = authorStr,
                    Description = descStr
                };
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Exception occurred during VpkUtils.GetAddonInfo.");
        }

        return addonInfo;
    }

    public static byte[]? GetAddonImage(Package pak)
    {
        byte[]? image = null;
        try
        {
            var imageEntry = pak.FindEntry("addonimage.jpg");
            if (imageEntry != null)
            {
                pak.ReadEntry(imageEntry, out image);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Exception in VpkUtils.GetAddonImage.");
        }
        return image;
    }
}
