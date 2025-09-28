using Serilog;
using SteamDatabase.ValvePak;
using System;
using ValveKeyValue;

namespace FireAxe;

internal static class VpkUtils
{
    public static VpkAddonInfo GetAddonInfo(Package pak)
    {
        var addonInfo = new VpkAddonInfo();

        try
        {
            var addonInfoEntry = pak.FindEntry("addoninfo.txt");
            if (addonInfoEntry != null)
            {
                pak.ReadEntry(addonInfoEntry, out byte[] data);
                var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
                var stream = new MemoryStream(data);
                var document = kv.Deserialize(stream);

                var version = document["addonversion"];
                if (version != null)
                {
                    addonInfo.Version = ((string)version).Trim();
                }

                var title = document["addontitle"];
                if (title != null)
                {
                    addonInfo.Title = ((string)title).Trim();
                }

                var author = document["addonauthor"];
                if (author != null)
                {
                    addonInfo.Author = ((string)author).Trim();
                }

                var desc = document["addonDescription"];
                if (desc != null)
                {
                    addonInfo.Description = (string)desc;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Exception in VpkUtils.GetAddonInfo.");
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
