using SharpCompress.Archives;
using System.Buffers.Binary;

namespace FireAxe;

public static class VpkArchiveExtractor
{
    public static async Task<IReadOnlyList<string>> ExtractAsync(string sourcePath, string destinationDirectory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(destinationDirectory);
        var result = new List<string>();
        await using var source = File.OpenRead(sourcePath);
        if (IsVpk(source))
        {
            string path = Path.Join(destinationDirectory, "addon.vpk");
            await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
            await source.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            result.Add(path);
            return result;
        }

        using var archive = ArchiveFactory.OpenArchive(source);
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.IsDirectory || entry.Key is not { } key || !key.EndsWith(".vpk", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            // Import only VPKs. Flatten paths so archive entries cannot escape the staging directory.
            string name = FileSystemUtils.SanitizeFileName(Path.GetFileName(key.Replace('\\', '/')));
            string path = Path.Join(destinationDirectory, FileSystemUtils.GetUniqueFileName(name, destinationDirectory));
            using (var input = entry.OpenEntryStream())
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            {
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }
            using var extracted = File.OpenRead(path);
            if (!IsVpk(extracted))
            {
                throw new InvalidDataException($"The archive entry '{key}' is not a VPK file.");
            }
            result.Add(path);
        }
        if (result.Count == 0)
        {
            throw new InvalidDataException("The GameMaps download contains no VPK files.");
        }
        return result;
    }

    private static bool IsVpk(Stream stream)
    {
        Span<byte> signature = stackalloc byte[4];
        int count = stream.Read(signature);
        stream.Position = 0;
        return count == 4 && BinaryPrimitives.ReadUInt32LittleEndian(signature) == 0x55aa1234;
    }
}
