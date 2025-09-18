using SteamDatabase.ValvePak;
using System;
using System.Collections.Immutable;

namespace FireAxe;

public static class AddonConflictUtils
{
    private static readonly ImmutableHashSet<string> s_commonIgnoringVpkFiles = [
        "addoninfo.txt", "addonimage.jpg", 
        "sound/sound.cache", 
        "scripts/vscripts/mapspawn_addon.nut", "scripts/vscripts/response_testbed_addon.nut", "scripts/vscripts/scriptedmode_addon.nut", "scripts/vscripts/director_base_addon.nut"
    ];

    public static Task<AddonConflictResult> FindConflicts(IEnumerable<AddonNode> addons, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(addons);

        var vpkItems = new List<VpkItem>();
        foreach (var addon1 in addons)
        {
            foreach (var addon in addon1.GetSelfAndDescendantsByDfsPreorder())
            {
                if (addon is VpkAddon vpkAddon)
                {
                    if (vpkAddon.FullVpkFilePath is { } vpkPath)
                    {
                        vpkItems.Add(new VpkItem(vpkAddon, vpkPath));
                    }
                }
            }
        }

        return Task.Run(
            () =>
            {
                var vpkPriorityGroups = new Dictionary<int, (Dictionary<string, HashSet<VpkAddon>> FileToAddons, Dictionary<VpkAddon, HashSet<string>> AddonToFiles)>();

                foreach (var vpkItem in vpkItems)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using var pak = new Package();
                    pak.Read(vpkItem.VpkPath);
                    var addon = vpkItem.Addon;
                    var priority = addon.VpkPriority;
                    if (!vpkPriorityGroups.TryGetValue(priority, out var priorityGroup))
                    {
                        priorityGroup = (new(), new());
                        vpkPriorityGroups[priority] = priorityGroup;
                    }
                    var (fileToAddons, addonToFiles) = priorityGroup;
                    foreach (var pakEntries in pak.Entries.Values)
                    {
                        foreach (var pakEntry in pakEntries)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            string file = pakEntry.GetFullPath();
                            if (s_commonIgnoringVpkFiles.Contains(file) || vpkItem.IgnoredFiles.Contains(file))
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

                var resultVpkFileToAddons = new Dictionary<string, HashSet<VpkAddon>>();
                var resultVpkAddonToFiles = new Dictionary<VpkAddon, HashSet<string>>();

                foreach (var (fileToAddons, addonToFiles) in vpkPriorityGroups.Values)
                {
                    foreach (var (file, addons) in fileToAddons)
                    {
                        if (addons.Count <= 1)
                        {
                            continue;
                        }
                        if (!resultVpkFileToAddons.TryGetValue(file, out var resultAddons))
                        {
                            resultAddons = new();
                            resultVpkFileToAddons[file] = resultAddons;
                        }
                        resultAddons.UnionWith(addons);
                    }
                    foreach (var (addon, files) in addonToFiles)
                    {
                        if (!resultVpkAddonToFiles.TryGetValue(addon, out var resultFiles))
                        {
                            resultFiles = new();
                            resultVpkAddonToFiles[addon] = resultFiles;
                        }
                        resultFiles.UnionWith(files);
                    }
                }

                return new AddonConflictResult(
                    resultVpkFileToAddons.Select(kvp => (kvp.Key, (IEnumerable<VpkAddon>)kvp.Value.ToArray())).ToDictionary(),
                    resultVpkAddonToFiles.Select(kvp => (kvp.Key, (IEnumerable<string>)kvp.Value.ToArray())).ToDictionary()
                );
            },
            cancellationToken
        );
    }

    private class VpkItem
    {
        public readonly VpkAddon Addon;
        public readonly string VpkPath;
        public readonly IReadOnlySet<string> IgnoredFiles;

        public VpkItem(VpkAddon addon, string vpkPath)
        {
            Addon = addon;
            VpkPath = vpkPath;
            IgnoredFiles = addon.ConflictIgnoringFiles.ToImmutableHashSet();
        }
    }
}

public class AddonConflictResult
{
    internal AddonConflictResult(IReadOnlyDictionary<string, IEnumerable<VpkAddon>> vpkFileToAddons, IReadOnlyDictionary<VpkAddon, IEnumerable<string>> vpkAddonToFiles)
    {
        ConflictingVpkFileToAddons = vpkFileToAddons;
        ConflictingVpkAddonToFiles = vpkAddonToFiles;
        HasConflict = vpkFileToAddons.Count > 0 || vpkAddonToFiles.Count > 0;
    }

    public bool HasConflict { get; }

    public IEnumerable<VpkAddon> ConflictingVpkAddons => ConflictingVpkAddonToFiles.Keys;

    public IReadOnlyDictionary<string, IEnumerable<VpkAddon>> ConflictingVpkFileToAddons { get; }

    public IReadOnlyDictionary<VpkAddon, IEnumerable<string>> ConflictingVpkAddonToFiles { get; }

    public IEnumerable<VpkAddon> GetConflictingAddons(VpkAddon addon)
    {
        ArgumentNullException.ThrowIfNull(addon);

        IEnumerable<VpkAddon> GetRaw()
        {
            if (ConflictingVpkAddonToFiles.TryGetValue(addon, out var files))
            {
                foreach (var file in files)
                {
                    if (ConflictingVpkFileToAddons.TryGetValue(file, out var addons))
                    {
                        foreach (var addon2 in addons)
                        {
                            if (addon2 != addon && addon2.VpkPriority == addon.VpkPriority)
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
}