using Serilog;
using SteamDatabase.ValvePak;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace FireAxe;

public abstract class VpkAddon : AddonNode
{
    private readonly ObservableCollection<string> _conflictIgnoringFiles = new();
    private readonly ReadOnlyObservableCollection<string> _conflictIgnoringFilesReadOnly;
    private readonly HashSet<string> _conflictIgnoringFileSet = new HashSet<string>();

    internal readonly ObservableCollection<(string File, IEnumerable<Guid> AddonIds)> _conflictingFilesWithAddonIds = new();
    private readonly ReadOnlyObservableCollection<(string File, IEnumerable<Guid> AddonIds)> _conflictingFilesWithAddonIdsReadOnly;
    internal readonly ObservableCollection<(Guid AddonId, IEnumerable<string> Files)> _conflictingAddonIdsWithFiles = new();
    private readonly ReadOnlyObservableCollection<(Guid AddonId, IEnumerable<string> Files)> _conflictingAddonIdsWithFilesReadOnly;

    private WeakReference<VpkAddonInfo?> _addonInfo = new(null);

    internal readonly AddonProblemSource<VpkAddon> _vpkAddonConflictProblemSource;

    protected VpkAddon()
    {
        _conflictIgnoringFilesReadOnly = new(_conflictIgnoringFiles);
        _conflictingFilesWithAddonIdsReadOnly = new(_conflictingFilesWithAddonIds);
        _conflictingAddonIdsWithFilesReadOnly = new(_conflictingAddonIdsWithFiles);

        _vpkAddonConflictProblemSource = new(this);
    }

    public ReadOnlyObservableCollection<string> ConflictIgnoringFiles => _conflictIgnoringFilesReadOnly;

    public ReadOnlyObservableCollection<(string File, IEnumerable<Guid> AddonIds)> ConflictingFilesWithAddonIds => _conflictingFilesWithAddonIdsReadOnly;

    public ReadOnlyObservableCollection<(Guid AddonId, IEnumerable<string> Files)> ConflictingAddonIdsWithFiles => _conflictingAddonIdsWithFilesReadOnly;

    public abstract string? FullVpkFilePath
    {
        get;
    }

    public override Type SaveType => typeof(VpkAddonSave);

    public override bool RequireFile => true;

    protected override long? GetFileSize()
    {
        var path = FullVpkFilePath;
        if (path == null)
        {
            return null;
        }

        try
        {
            if (File.Exists(path))
            {
                return new FileInfo(path).Length;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred during VpkAddon.GetFileSize.");
        }

        return null;
    }

    protected override Task<byte[]?> DoGetImageAsync(CancellationToken cancellationToken)
    {
        string? vpkPath = FullVpkFilePath;
        if (vpkPath == null)
        {
            return Task.FromResult<byte[]?>(null);
        }
        return Task.Run(() =>
        {
            if (TryCreatePackage(vpkPath, out var pak))
            {
                using (pak)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return VpkUtils.GetAddonImage(pak);
                }
            }
            return null;
        }, cancellationToken);
    }

    public VpkAddonInfo? RetrieveInfo()
    {
        this.ThrowIfInvalid();

        if (!_addonInfo.TryGetTarget(out var addonInfo))
        {
            if (TryCreatePackage(FullVpkFilePath, out var pak))
            {
                using (pak)
                {
                    addonInfo = VpkUtils.GetAddonInfo(pak);
                    _addonInfo.SetTarget(addonInfo);
                }
            }
        }
        return addonInfo;
    }

    protected override void OnClearCaches()
    {
        base.OnClearCaches();

        _addonInfo.SetTarget(null);
    }

    public bool AddConflictIgnoringFile(string file)
    {
        ArgumentNullException.ThrowIfNull(file);

        this.ThrowIfInvalid();

        if (!_conflictIgnoringFileSet.Add(file))
        {
            return false;
        }
        _conflictIgnoringFiles.Add(file);

        for (int i = 0, len = _conflictingFilesWithAddonIds.Count; i < len; i++)
        {
            if (_conflictingFilesWithAddonIds[i].File == file)
            {
                _conflictingFilesWithAddonIds.RemoveAt(i);
                break;
            }
        }

        Root.RequestSave = true;

        return true;
    }

    public bool RemoveConflictIgnoringFile(string file)
    {
        ArgumentNullException.ThrowIfNull(file);

        this.ThrowIfInvalid();

        if (!_conflictIgnoringFileSet.Remove(file))
        {
            return false;
        }
        _conflictIgnoringFiles.Remove(file);

        Root.RequestSave = true;

        return true;
    }

    public void ClearConflictIgnoringFiles()
    {
        this.ThrowIfInvalid();

        _conflictIgnoringFileSet.Clear();
        _conflictIgnoringFiles.Clear();
        Root.RequestSave = true;
    }

    public bool ContainsConflictIgnoringFile(string file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return _conflictIgnoringFileSet.Contains(file);
    }

    protected override void OnCreateSave(AddonNodeSave save)
    {
        base.OnCreateSave(save);
        var save1 = (VpkAddonSave)save;
        save1.ConflictIgnoringFiles = [.. ConflictIgnoringFiles];
    }

    protected override void OnLoadSave(AddonNodeSave save)
    {
        base.OnLoadSave(save);
        var save1 = (VpkAddonSave)save;
        ClearConflictIgnoringFiles();
        foreach (var file in save1.ConflictIgnoringFiles)
        {
            AddConflictIgnoringFile(file);
        }
    }

    private static bool TryCreatePackage(string? path, [NotNullWhen(true)] out Package? pak)
    {
        pak = null;
        if (path == null)
        {
            return false;
        }
        try
        {
            pak = new Package();
            pak.Read(path);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Exception in VpkAddon.TryCreatePackage.");
            if (pak != null)
            {
                pak.Dispose();
                pak = null;
            }
            return false;
        }
    }
}
