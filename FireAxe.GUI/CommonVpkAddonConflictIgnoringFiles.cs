using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using Serilog;

namespace FireAxe;

public static class CommonVpkAddonConflictIgnoringFiles
{
    public const string DirectoryName = "CommonVpkAddonConflictIgnoringFiles";

    private static readonly Lazy<FrozenSet<string>> s_hl2 = new(() => LoadFile("HalfLife2.asset"));

    public static IReadOnlySet<string> HalfLife2 => s_hl2.Value;

    private static FrozenSet<string> LoadFile(string fileName)
    {
        var filePath = Path.Join(AppGlobal.ExportedAssetsDirectoryName, DirectoryName, fileName);

        IEnumerable<string> GetContent()
        {
            using var stream = File.OpenText(filePath);
            string? line;
            while ((line = stream.ReadLine()) is not null)
            {
                yield return line;
            }
        }

        return FrozenSet.ToFrozenSet(GetContent());
    }
}