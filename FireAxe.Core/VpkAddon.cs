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

    private readonly WeakReference<VpkAddonInfo?> _addonInfo = new(null);

    protected VpkAddon()
    {
        _conflictIgnoringFilesReadOnly = new(_conflictIgnoringFiles);
        _conflictingFilesWithAddonIdsReadOnly = new(_conflictingFilesWithAddonIds);
        _conflictingAddonIdsWithFilesReadOnly = new(_conflictingAddonIdsWithFiles);
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

    public override bool CanGetSuggestedName => true;

    public Task? CheckVpkValidityTask { get; private set => NotifyAndSetIfChanged(ref field, value); } = null;

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

    public bool AddConflictIgnoringFile(string? file)
    {
        this.ThrowIfInvalid();

        if (string.IsNullOrEmpty(file))
        {
            return false;
        }

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

    public override Task<string?> TryGetSuggestedNameAsync(object? arg = null, CancellationToken cancellationToken = default)
    {
        this.ThrowIfInvalid();

        if (RetrieveInfo()?.Title is { } title)
        {
            title = SanitizeName(title, out bool empty);
            if (!empty)
            {
                return Task.FromResult<string?>(title);
            }
        }

        return Task.FromResult<string?>(null);
    }

    public Task CheckVpkValidity()
    {
        this.ThrowIfInvalid();

        if (CheckVpkValidityTask is { IsCompleted: false })
        {
            return CheckVpkValidityTask;
        }

        Task GetTask()
        {
            var vpkPath = FullVpkFilePath;
            if (vpkPath is null)
            {
                InvalidateProblem<InvalidVpkFileProblem>();
                return Task.CompletedTask;
            }
            var addonTaskCreator = this.GetValidTaskCreator();
            return Task.Run(async () =>
            {
                InvalidVpkFileProblem? problem = null;
                bool exist = false;
                try
                {
                    exist = File.Exists(vpkPath);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception occurred during checking the existence of the file: {FilePath}", vpkPath);
                }
                if (exist)
                {
                    try
                    {
                        using var pak = new Package();
                        pak.Read(vpkPath);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Exception occurred during reading the VPK file: {VpkPath}", vpkPath);
                        problem = new(this);
                    }
                }
                await addonTaskCreator.StartNew(self => self.SetProblem(problem)).ConfigureAwait(false);
            });
        }

        var checkTask = GetTask();
        CheckVpkValidityTask = checkTask;
        checkTask.ContinueWith(_ =>
        {
            if (CheckVpkValidityTask == checkTask)
            {
                CheckVpkValidityTask = null;
            }
        }, Root.TaskScheduler);

        return checkTask;
    }

    protected override void OnPostCheck(Action<Task> taskSubmitter)
    {
        base.OnPostCheck(taskSubmitter);

        taskSubmitter(CheckVpkValidity());
    }

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

    protected override void OnClearCaches()
    {
        base.OnClearCaches();

        _addonInfo.SetTarget(null);
    }

    protected override void OnCreateSave(AddonNodeSave save0)
    {
        base.OnCreateSave(save0);

        var save = (VpkAddonSave)save0;
        save.ConflictIgnoringFiles = [.. ConflictIgnoringFiles];
    }

    protected override void OnLoadSave(AddonNodeSave save0)
    {
        base.OnLoadSave(save0);

        var save = (VpkAddonSave)save0;
        ClearConflictIgnoringFiles();
        foreach (var file in save.ConflictIgnoringFiles)
        {
            AddConflictIgnoringFile(file);
        }
    }

    protected override Task OnDestroyAsync()
    {
        var tasks = new List<Task>();

        if (CheckVpkValidityTask is { } task)
        {
            tasks.Add(task);
        }

        var baseTask = base.OnDestroyAsync();
        tasks.Add(baseTask);

        return TaskUtils.WhenAllIgnoreCanceled(tasks);
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
