using Serilog;

namespace FireAxe;

public class GameMapsAddon : AddonGroup
{
    private ulong? _detailsId;
    private GameMapsVpkFile[] _installedFiles = [];
    private bool? _isAutoUpdate;

    protected GameMapsAddon() { }

    public override Type SaveType => typeof(GameMapsAddonSave);

    public ulong? DetailsId
    {
        get => _detailsId;
        set
        {
            this.ThrowIfInvalid();
            if (value == _detailsId) return;
            if (value is null or 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (DownloadCheckTask is { IsCompleted: false } || _installedFiles.Length > 0)
                throw new InvalidOperationException("Create another GameMaps addon to use a different source.");
            _detailsId = value;
            NotifyChanged();
            Root.RequestSave = true;
            AutoCheck();
        }
    }

    public bool RequestAutoSetName { get; set; } = true;

    public bool? IsAutoUpdate
    {
        get => _isAutoUpdate;
        set
        {
            this.ThrowIfInvalid();
            if (NotifyAndSetIfChanged(ref _isAutoUpdate, value)) Root.RequestSave = true;
        }
    }

    public bool IsAutoUpdateActually => IsAutoUpdate ?? Root.IsAutoUpdateGameMapsItem;

    public string? InstalledRevision { get; private set; }

    public bool NeedsDownload => DetailsId.HasValue && (_installedFiles.Length == 0 || _installedFiles.Any(file =>
        Root.TryGetNodeById(file.AddonId, out var node) && node is LocalVpkAddon vpk && !File.Exists(vpk.VpkFilePath)));

    public Task? DownloadCheckTask { get; private set => NotifyAndSetIfChanged(ref field, value); }

    public IDownloadItem? DownloadItem { get; private set => NotifyAndSetIfChanged(ref field, value); }

    public string? ErrorMessage { get; private set => NotifyAndSetIfChanged(ref field, value); }

    public Task CheckDownloadAsync(bool forceUpdate = false)
    {
        this.ThrowIfInvalid();
        if (DownloadCheckTask is { IsCompleted: false }) return DownloadCheckTask;
        if (_detailsId is not { } id || !Root.IsDirectoryPathSet) return Task.CompletedTask;
        if (!NeedsDownload && !IsAutoUpdateActually && !forceUpdate)
        {
            InvalidateProblem<AddonDownloadFailedProblem>();
            ErrorMessage = null;
            return Task.CompletedTask;
        }
        var task = RunDownloadAsync(id, forceUpdate);
        DownloadCheckTask = task;
        task.ContinueWith(_ =>
        {
            if (IsValid && DownloadCheckTask == task) DownloadCheckTask = null;
        }, Root.TaskScheduler);
        return task;
    }

    protected override void OnCheck(Action<Task> taskSubmitter)
    {
        base.OnCheck(taskSubmitter);
        taskSubmitter(CheckDownloadAsync());
    }

    protected override void OnCreate()
    {
        base.OnCreate();
        EnableStrategy = AddonGroupEnableStrategy.All;
    }

    private async Task RunDownloadAsync(ulong id, bool forceUpdate)
    {
        var taskCreator = this.GetValidTaskCreator();
        var root = Root;
        var cancellationToken = DestructionCancellationToken;
        var blockMove = BlockMove();
        string downloadPath = Path.Join(root.CacheDirectoryPath, $"gamemaps-{Id:N}.download");
        string stagingPath = Path.Join(root.CacheDirectoryPath, $"gamemaps-{Id:N}-{Guid.NewGuid():N}");
        string url = GameMapsClient.GetDetailsUrl(id);
        bool installed = false;
        try
        {
            ErrorMessage = null;
            var details = await root.GameMapsClient.GetDetailsAsync(id, cancellationToken).ConfigureAwait(false);
            bool shouldDownload = false;
            bool replaceExisting = false;
            if (await taskCreator.StartNew(self =>
            {
                bool revisionChanged = details.Revision is not null && details.Revision != self.InstalledRevision;
                shouldDownload = self.NeedsDownload || forceUpdate || (self.IsAutoUpdateActually && revisionChanged);
                replaceExisting = forceUpdate || revisionChanged;
                if (!shouldDownload)
                {
                    self.InvalidateProblem<AddonDownloadFailedProblem>();
                    return;
                }
                if (self.RequestAutoSetName)
                {
                    blockMove.Dispose();
                    try
                    {
                        self.Name = self.Parent.GetUniqueChildName(SanitizeName(details.Title, out _));
                        self.RequestAutoSetName = false;
                    }
                    finally { blockMove = self.BlockMove(); }
                }
                Directory.CreateDirectory(self.FullFilePath);
            }).ConfigureAwait(false)) return;
            if (!shouldDownload) return;

            var info = await root.GameMapsClient.GetDownloadInfoAsync(details, cancellationToken).ConfigureAwait(false);
            url = info.DownloadUrl;

            using (var download = root.GameMapsClient.Download(url, downloadPath, id))
            {
                using var cancellationRegistration = cancellationToken.Register(download.Cancel);
                if (await taskCreator.StartNew(self =>
                {
                    self.DownloadItem = download;
                    root.NotifyDownloadItem(download);
                }).ConfigureAwait(false)) return;
                await download.WaitAsync().ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (download.Status == DownloadStatus.Cancelled) return;
                if (download.Status != DownloadStatus.Succeeded) throw download.Exception;
            }

            var paths = await Task.Run(() => VpkArchiveExtractor.ExtractAsync(downloadPath, stagingPath, cancellationToken), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var pendingChecks = new List<Task>();
            await taskCreator.StartNew(self =>
            {
                foreach (var file in self._installedFiles)
                {
                    if (root.TryGetNodeById(file.AddonId, out var node) && node is LocalVpkAddon { CheckTask: { } check })
                        pendingChecks.Add(check);
                }
                if (root.CheckVpkAddonConflictsTask is { } conflicts) pendingChecks.Add(conflicts);
            }).ConfigureAwait(false);
            await Task.WhenAll(pendingChecks).WaitAsync(cancellationToken).ConfigureAwait(false);
            var checks = new List<Task>();
            if (await taskCreator.StartNew(self =>
            {
                checks.AddRange(self.InstallVpks(paths, replaceExisting, Path.Join(stagingPath, "previous")));
                installed = true;
                self.InstalledRevision = info.Revision;
                self.InvalidateProblem<AddonDownloadFailedProblem>();
                root.RequestSave = true;
            }).ConfigureAwait(false)) return;
            await Task.WhenAll(checks).ConfigureAwait(false);
            File.Delete(downloadPath);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to download or extract GameMaps addon {DetailsId}", id);
            await taskCreator.StartNew(self =>
            {
                self.ErrorMessage = ex.Message;
                self.SetProblem(new AddonDownloadFailedProblem(self) { Url = url, FilePath = downloadPath, Exception = ex });
            }).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                string backupPath = Path.Join(stagingPath, "previous");
                bool recoveryFilesRemain = !installed && Directory.Exists(backupPath) && Directory.EnumerateFiles(backupPath).Any();
                if (recoveryFilesRemain) Log.Error("Preserving GameMaps recovery files at {Path}", backupPath);
                else if (Directory.Exists(stagingPath)) Directory.Delete(stagingPath, true);
            }
            catch (Exception ex) { Log.Warning(ex, "Failed to clean GameMaps staging directory {Path}", stagingPath); }
            await taskCreator.StartNew(self =>
            {
                self.DownloadItem = null;
                blockMove.Dispose();
            }).ConfigureAwait(false);
        }
    }

    private IReadOnlyList<Task> InstallVpks(IReadOnlyList<string> paths, bool replaceExisting, string backupDirectory)
    {
        using var blockAutoCheck = Root.BlockAutoCheck();
        var remaining = new List<GameMapsVpkFile>(_installedFiles);
        var installed = new List<GameMapsVpkFile>();
        var created = new List<LocalVpkAddon>();
        var changes = new List<(LocalVpkAddon Addon, string? Source, string Target)>();
        var backups = new List<(string Original, string Backup)>();
        var movedPaths = new List<string>();
        bool enabled = IsEnabled;
        var strategy = EnableStrategy;
        // Adding a disabled child to an All group would otherwise disable its existing children.
        EnableStrategy = AddonGroupEnableStrategy.None;
        try
        {
            foreach (string path in paths)
            {
                string fileName = Path.GetFileName(path);
                var previous = remaining.FirstOrDefault(file => file.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
                if (previous is null && paths.Count == 1 && _installedFiles.Length == 1)
                    previous = remaining.FirstOrDefault();
                LocalVpkAddon addon;
                if (previous is not null)
                {
                    remaining.Remove(previous);
                    installed.Add(new(fileName, previous.AddonId));
                    // A removed child is an intentional user choice; do not recreate it on update.
                    if (!Root.TryGetNodeById(previous.AddonId, out var node) || node is not LocalVpkAddon existing) continue;
                    addon = existing;
                    if (!replaceExisting && File.Exists(addon.FullFilePath)) continue;
                }
                else
                {
                    if (_installedFiles.Length > 0 && !replaceExisting) continue;
                    string name = GetUniqueChildName(SanitizeName(Path.GetFileNameWithoutExtension(fileName), out _));
                    addon = Create<LocalVpkAddon>(Root, this);
                    created.Add(addon);
                    addon.Name = name;
                    addon.IsEnabled = enabled;
                    installed.Add(new(fileName, addon.Id));
                }
                changes.Add((addon, path, addon.FullFilePath));
            }
            foreach (var previous in remaining)
            {
                if (!replaceExisting)
                {
                    if (Root.TryGetNodeById(previous.AddonId, out var node) && node is LocalVpkAddon vpk && !File.Exists(vpk.FullFilePath))
                        throw new InvalidDataException($"The download no longer contains the required VPK '{previous.FileName}'.");
                    installed.Add(previous);
                }
                else if (Root.TryGetNodeById(previous.AddonId, out var node) && node is LocalVpkAddon vpk)
                    changes.Add((vpk, null, vpk.FullFilePath));
            }

            Directory.CreateDirectory(backupDirectory);
            foreach (var change in changes)
            {
                if (File.Exists(change.Target))
                {
                    string backup = Path.Join(backupDirectory, $"{backups.Count}.vpk");
                    File.Move(change.Target, backup);
                    backups.Add((change.Target, backup));
                }
                if (change.Source is not null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(change.Target)!);
                    File.Move(change.Source, change.Target);
                    movedPaths.Add(change.Target);
                }
            }
        }
        catch (Exception installError)
        {
            var errors = new List<Exception>();
            foreach (string path in movedPaths)
            {
                try { File.Delete(path); }
                catch (Exception ex) { errors.Add(ex); }
            }
            foreach (var (original, backup) in backups)
            {
                try { File.Move(backup, original); }
                catch (Exception ex) { errors.Add(ex); }
            }
            foreach (var addon in created) _ = addon.DestroyAsync();
            if (errors.Count > 0)
                throw new AggregateException($"The GameMaps update could not be fully restored. Recovery files: {backupDirectory}", new[] { installError }.Concat(errors));
            throw;
        }
        finally { EnableStrategy = strategy; }

        _installedFiles = installed.ToArray();
        var checks = new List<Task>();
        foreach (var change in changes)
        {
            if (change.Source is null) checks.Add(change.Addon.DestroyAsync());
            else
            {
                change.Addon.ClearCaches();
                change.Addon.Check();
                if (change.Addon.CheckTask is { } check) checks.Add(check);
            }
        }
        return checks;
    }

    protected override async Task OnDestroyAsync()
    {
        var task = DownloadCheckTask;
        await base.OnDestroyAsync();
        if (task is not null) await task.ConfigureAwait(false);
    }

    protected override void OnCreateSave(AddonNodeSave save)
    {
        base.OnCreateSave(save);
        var gameMapsSave = (GameMapsAddonSave)save;
        gameMapsSave.DetailsId = DetailsId;
        gameMapsSave.RequestAutoSetName = RequestAutoSetName;
        gameMapsSave.InstalledFiles = _installedFiles;
        gameMapsSave.IsAutoUpdate = IsAutoUpdate;
        gameMapsSave.InstalledRevision = InstalledRevision;
    }

    protected override void OnLoadSave(AddonNodeSave save)
    {
        base.OnLoadSave(save);
        var gameMapsSave = (GameMapsAddonSave)save;
        _detailsId = gameMapsSave.DetailsId;
        RequestAutoSetName = gameMapsSave.RequestAutoSetName;
        _installedFiles = gameMapsSave.InstalledFiles ?? [];
        _isAutoUpdate = gameMapsSave.IsAutoUpdate;
        InstalledRevision = gameMapsSave.InstalledRevision;
    }
}
