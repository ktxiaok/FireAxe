using Serilog;
using SteamDatabase.ValvePak;
using System;
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace FireAxe;

public interface IVpkAddonConflictIgnoringFileSet
{
    bool ShouldIgnore(string file);
}

public class VpkAddonConflictIgnoringFileSet : IVpkAddonConflictIgnoringFileSet
{
    private readonly IReadOnlySet<string>[] _sets;
    private readonly Func<IReadOnlySet<string>?>[] _setProviders;

    public VpkAddonConflictIgnoringFileSet(IEnumerable<IReadOnlySet<string>> sets, IEnumerable<Func<IReadOnlySet<string>?>>? setProviders = null)
    {
        ArgumentNullException.ThrowIfNull(sets);
        setProviders ??= [];

        _sets = [.. sets];
        _setProviders = [.. setProviders];
    }

    public static VpkAddonConflictIgnoringFileSet Empty { get; } = new([]);
    
    public bool ShouldIgnore(string file)
    {
        ArgumentNullException.ThrowIfNull(file);

        foreach (var set in _sets)
        {
            if (set.Contains(file))
            {
                return true;
            }
        }
        foreach (var setProvider in _setProviders)
        {
            if (setProvider()?.Contains(file) ?? false)
            {
                return true;
            }
        }
        return false;
    }
}

public class VpkAddonConflictCheckSettings
{
    public IVpkAddonConflictIgnoringFileSet IgnoringFileSet { get; init; } = VpkAddonConflictIgnoringFileSet.Empty;

    public static VpkAddonConflictCheckSettings Default { get; } = new();
}

public static class AddonConflictUtils
{
    private static readonly FrozenSet<string> s_commonIgnoringVpkFiles = FrozenSet.ToFrozenSet([
        "addoninfo.txt", "addoninfo.jpg", "addonimage.jpg", "addonimage.vtf", 
        "sound/sound.cache", 
        "scripts/vscripts/mapspawn_addon.nut", "scripts/vscripts/response_testbed_addon.nut", "scripts/vscripts/scriptedmode_addon.nut", "scripts/vscripts/director_base_addon.nut"
    ]);

    public static Task<VpkAddonConflictResult> CheckVpkConflictsAsync(IEnumerable<AddonNode> addons, VpkAddonConflictCheckSettings? settings = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(addons);
        settings ??= VpkAddonConflictCheckSettings.Default;

        var vpkItems = new List<VpkItem>();
        foreach (var addon in addons.SelectMany(addon => addon.GetAllNodesEnabledInHierarchy()))
        {
            AddonNode? actualAddon;
            if (addon is RefAddonNode refAddon)
            {
                actualAddon = refAddon.ActualSourceAddon;
            }
            else
            {
                actualAddon = addon;
            }
            if (actualAddon is VpkAddon actualVpkAddon)
            {
                if (actualVpkAddon.VpkFilePath is { } vpkPath)
                {
                    bool vpkItemPresent = false;
                    if (actualAddon != addon)
                    {
                        foreach (var vpkItem in vpkItems)
                        {
                            if (vpkItem.Addon == actualVpkAddon)
                            {
                                vpkItemPresent = true;
                                vpkItem.Priority = Math.Max(vpkItem.Priority, addon.PriorityInHierarchy);
                                break;
                            }
                        }
                    }

                    if (!vpkItemPresent)
                    {
                        vpkItems.Add(new VpkItem(addon, actualVpkAddon, vpkPath));
                    }
                }
            }
        }

        return Task.Run(
            () =>
            {
                var priorityGroups = new Dictionary<int, (Dictionary<string, HashSet<VpkAddon>> FileToAddons, Dictionary<VpkAddon, HashSet<string>> AddonToFiles)>();

                foreach (var vpkItem in vpkItems)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var vpkPath = vpkItem.VpkPath;
                    if (!File.Exists(vpkPath))
                    {
                        continue;
                    }

                    using var pak = new Package();
                    try
                    {
                        pak.Read(vpkPath);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Exception occurred during reading the VPK file: {VpkPath}", vpkPath);
                    }
                    if (pak.Entries is null)
                    {
                        continue;
                    }

                    var addon = vpkItem.Addon;
                    var priority = vpkItem.Priority;
                    if (!priorityGroups.TryGetValue(priority, out var priorityGroup))
                    {
                        priorityGroup = (new(), new());
                        priorityGroups[priority] = priorityGroup;
                    }
                    var (fileToAddons, addonToFiles) = priorityGroup;

                    foreach (var pakEntries in pak.Entries.Values)
                    {
                        foreach (var pakEntry in pakEntries)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            string file = pakEntry.GetFullPath();
                            if (s_commonIgnoringVpkFiles.Contains(file) || vpkItem.IgnoredFiles.Contains(file) || settings.IgnoringFileSet.ShouldIgnore(file))
                            {
                                continue;
                            }
                            if (!fileToAddons.TryGetValue(file, out var addons))
                            {
                                addons = new();
                                fileToAddons[file] = addons;
                            }
                            addons.Add(addon);
                            if (addons.Count > 1)
                            {
                                foreach (var addon2 in addons)
                                {
                                    if (!addonToFiles.TryGetValue(addon2, out var files))
                                    {
                                        files = new();
                                        addonToFiles[addon2] = files;
                                    }
                                    files.Add(file);
                                }
                            }
                        }
                    }
                }

                var resultFileToAddons = new Dictionary<string, HashSet<VpkAddon>>();
                var resultAddonToFiles = new Dictionary<VpkAddon, HashSet<string>>();

                foreach (var (fileToAddons, addonToFiles) in priorityGroups.Values)
                {
                    foreach (var (file, addons) in fileToAddons)
                    {
                        if (addons.Count <= 1)
                        {
                            continue;
                        }
                        if (!resultFileToAddons.TryGetValue(file, out var resultAddons))
                        {
                            resultAddons = new();
                            resultFileToAddons[file] = resultAddons;
                        }
                        resultAddons.UnionWith(addons);
                    }
                    foreach (var (addon, files) in addonToFiles)
                    {
                        if (!resultAddonToFiles.TryGetValue(addon, out var resultFiles))
                        {
                            resultFiles = new();
                            resultAddonToFiles[addon] = resultFiles;
                        }
                        resultFiles.UnionWith(files);
                    }
                }

                return new VpkAddonConflictResult(
                    resultFileToAddons.Select(kvp => (kvp.Key, (IEnumerable<VpkAddon>)kvp.Value.ToArray())).ToDictionary(),
                    resultAddonToFiles.Select(kvp => (kvp.Key, (IEnumerable<string>)kvp.Value.ToArray())).ToDictionary()
                );
            },
            cancellationToken
        );
    }

    private class VpkItem
    {
        public readonly VpkAddon Addon;
        public readonly string VpkPath;
        public int Priority;
        public readonly IReadOnlySet<string> IgnoredFiles;

        public VpkItem(AddonNode addon, VpkAddon actualAddon, string vpkPath)
        {
            Addon = actualAddon;
            VpkPath = vpkPath;
            Priority = addon.PriorityInHierarchy;
            IgnoredFiles = actualAddon.ConflictIgnoringFiles.ToImmutableHashSet();
        }
    }
}

public class VpkAddonConflictResult
{
    private readonly IReadOnlyDictionary<string, IEnumerable<VpkAddon>> _fileToAddons;
    private readonly IReadOnlyDictionary<VpkAddon, IEnumerable<string>> _addonToFiles;

    internal VpkAddonConflictResult(IReadOnlyDictionary<string, IEnumerable<VpkAddon>> fileToAddons, IReadOnlyDictionary<VpkAddon, IEnumerable<string>> addonToFiles)
    {
        _fileToAddons = fileToAddons;
        _addonToFiles = addonToFiles;
    }

    public bool HasConflict => _addonToFiles.Count > 0;

    public IEnumerable<VpkAddon> ConflictingAddons => _addonToFiles.Keys;

    public IEnumerable<string> ConflictingFiles => _fileToAddons.Keys;

    public IEnumerable<VpkAddon> GetConflictingAddons(string file)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (_fileToAddons.TryGetValue(file, out var addons))
        {
            return addons;
        }

        return [];
    }

    public IEnumerable<string> GetConflictingFiles(VpkAddon addon)
    {
        ArgumentNullException.ThrowIfNull(addon);

        if (_addonToFiles.TryGetValue(addon, out var files))
        {
            return files;
        }

        return [];
    }

    public IEnumerable<VpkAddon> GetConflictingAddons(VpkAddon addon)
    {
        ArgumentNullException.ThrowIfNull(addon);

        IEnumerable<VpkAddon> GetRaw()
        {
            if (_addonToFiles.TryGetValue(addon, out var files))
            {
                foreach (var file in files)
                {
                    if (_fileToAddons.TryGetValue(file, out var addons))
                    {
                        foreach (var addon2 in addons)
                        {
                            if (addon2 != addon && addon2.PriorityInHierarchy == addon.PriorityInHierarchy)
                            {
                                yield return addon2;
                            }
                        }
                    }
                }
            }
        }

        return GetRaw().Distinct();
    }

    public IEnumerable<string> GetConflictingFiles(VpkAddon addon1, VpkAddon addon2)
    {
        ArgumentNullException.ThrowIfNull(addon1);
        ArgumentNullException.ThrowIfNull(addon2);

        if (addon1.PriorityInHierarchy != addon2.PriorityInHierarchy)
        {
            return [];
        }

        return GetConflictingFiles(addon1).Intersect(GetConflictingFiles(addon2));
    }

    public IEnumerable<VpkAddon> GetConflictingAddons(VpkAddon addon, string file)
    {
        ArgumentNullException.ThrowIfNull(addon);
        ArgumentNullException.ThrowIfNull(file);

        var priority = addon.PriorityInHierarchy;
        return GetConflictingAddons(file).Where(addon0 => addon0 != addon && addon0.PriorityInHierarchy == priority);
    }

    public IEnumerable<(VpkAddon Addon, IEnumerable<string> Files)> GetConflictingAddonsWithFiles(VpkAddon addon)
    {
        ArgumentNullException.ThrowIfNull(addon);

        return GetConflictingAddons(addon).Select(addon0 => (addon0, (IEnumerable<string>)(GetConflictingFiles(addon0, addon).ToArray())));
    }

    public IEnumerable<(string File, IEnumerable<VpkAddon> Addons)> GetConflictingFilesWithAddons(VpkAddon addon)
    {
        ArgumentNullException.ThrowIfNull(addon);

        return GetConflictingFiles(addon).Select(file => (file, (IEnumerable<VpkAddon>)(GetConflictingAddons(addon, file).ToArray())));
    }
}