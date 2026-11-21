using Serilog;
using System.Globalization;

namespace FireAxe;

public class WorkshopCollectionSubscription : AddonGroup
{
    private ulong? _collectionId;
    private readonly Dictionary<ulong, Guid> _managedItems = new();
    private DateTimeOffset? _lastSynchronizationAttempt;
    private CancellationTokenSource? _synchronizationCts;
    private (ulong Id, bool IncludeLinkedCollections) _activeSource;

    protected WorkshopCollectionSubscription() { }

    public override Type SaveType => typeof(WorkshopCollectionSubscriptionSave);

    public ulong? CollectionId
    {
        get => _collectionId;
        set
        {
            this.ThrowIfInvalid();
            if (value == _collectionId) return;
            if (value is null or 0) throw new ArgumentOutOfRangeException(nameof(value));
            _collectionId = value;
            NotifyChanged();
            Root.RequestSave = true;
            OnSourceChanged();
        }
    }

    public bool IncludeLinkedCollections
    {
        get;
        set
        {
            this.ThrowIfInvalid();
            if (NotifyAndSetIfChanged(ref field, value))
            {
                Root.RequestSave = true;
                OnSourceChanged();
            }
        }
    } = true;

    public bool? IsAutoUpdate
    {
        get;
        set
        {
            this.ThrowIfInvalid();
            if (NotifyAndSetIfChanged(ref field, value)) Root.RequestSave = true;
        }
    }

    public bool? AllowDownloads
    {
        get;
        set
        {
            this.ThrowIfInvalid();
            if (NotifyAndSetIfChanged(ref field, value)) Root.RequestSave = true;
        }
    }

    public bool? AllowDeletions
    {
        get;
        set
        {
            this.ThrowIfInvalid();
            if (NotifyAndSetIfChanged(ref field, value)) Root.RequestSave = true;
        }
    }

    public bool? EnableNewAddons
    {
        get;
        set
        {
            this.ThrowIfInvalid();
            if (NotifyAndSetIfChanged(ref field, value)) Root.RequestSave = true;
        }
    }

    public bool IsAutoUpdateActually => IsAutoUpdate ?? Root.IsAutoUpdateWorkshopCollection;

    public bool AllowDownloadsActually => AllowDownloads ?? Root.AllowWorkshopCollectionDownloads;

    public bool AllowDeletionsActually => AllowDeletions ?? Root.AllowWorkshopCollectionDeletions;

    public bool EnableNewAddonsActually => EnableNewAddons ?? Root.EnableNewWorkshopCollectionAddons;

    public DateTimeOffset? LastSynchronizedAt { get; private set => NotifyAndSetIfChanged(ref field, value); }

    public int LastItemCount { get; private set => NotifyAndSetIfChanged(ref field, value); }

    public Task? SynchronizationTask { get; private set => NotifyAndSetIfChanged(ref field, value); }

    public string? ErrorMessage { get; private set => NotifyAndSetIfChanged(ref field, value); }

    public bool IsAutomaticSynchronizationDue => IsAutoUpdateActually &&
        (!_lastSynchronizationAttempt.HasValue || DateTimeOffset.UtcNow - _lastSynchronizationAttempt.Value >=
            TimeSpan.FromMinutes(Root.WorkshopCollectionUpdateIntervalMinutes));

    // Manual synchronization bypasses scheduling, but always respects download/deletion permissions.
    public Task SynchronizeAsync(bool forceUpdate = false)
    {
        this.ThrowIfInvalid();
        if (SynchronizationTask is { IsCompleted: false } current)
        {
            if (_activeSource == (CollectionId, IncludeLinkedCollections) && _synchronizationCts is { IsCancellationRequested: false })
                return current;
            return SynchronizeAfterCurrentAsync(current, forceUpdate);
        }
        if (CollectionId is not { } id || !Root.IsDirectoryPathSet || (!forceUpdate && !IsAutoUpdateActually))
            return Task.CompletedTask;

        _lastSynchronizationAttempt = DateTimeOffset.UtcNow;
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(DestructionCancellationToken);
        _synchronizationCts = cancellation;
        _activeSource = (id, IncludeLinkedCollections);
        var task = RunSynchronizationAsync(id, IncludeLinkedCollections, cancellation.Token);
        SynchronizationTask = task;
        _ = task.ContinueWith(_ =>
        {
            if (IsValid && SynchronizationTask == task) SynchronizationTask = null;
            if (_synchronizationCts == cancellation) _synchronizationCts = null;
            cancellation.Dispose();
        }, Root.TaskScheduler);
        return task;
    }

    private void OnSourceChanged()
    {
        _synchronizationCts?.Cancel();
        _lastSynchronizationAttempt = null;
        LastSynchronizedAt = null;
        LastItemCount = 0;
        ErrorMessage = null;
        InvalidateProblem<WorkshopCollectionSyncProblem>();
        // Editing the source is an explicit synchronization request, like editing a Workshop mod ID.
        if (IsAutoCheckEnabled && CollectionId.HasValue) _ = SynchronizeAsync(forceUpdate: true);
    }

    private async Task SynchronizeAfterCurrentAsync(Task current, bool forceUpdate)
    {
        var creator = this.GetValidTaskCreator();
        await current.ConfigureAwait(false);
        Task next = Task.CompletedTask;
        await creator.StartNew(self => next = self.SynchronizeAsync(forceUpdate)).ConfigureAwait(false);
        await next.ConfigureAwait(false);
    }

    private async Task RunSynchronizationAsync(ulong id, bool includeLinkedCollections, CancellationToken cancellationToken)
    {
        var taskCreator = this.GetValidTaskCreator();
        // Publish SynchronizationTask before synchronous HTTP handlers or notifications can reenter.
        await Task.Yield();
        var checks = new List<Task>();
        var removals = new List<Task>();
        IDisposable? blockMove = null;
        try
        {
            var idsTask = WorkshopCollectionUtils.GetWorkshopCollectionContentAsync(id, includeLinkedCollections, Root.HttpClient, cancellationToken);
            var detailsTask = PublishedFileUtils.GetPublishedFileDetailsAsync(id, Root.HttpClient, cancellationToken);
            await Task.WhenAll(idsTask, detailsTask).ConfigureAwait(false);
            if (idsTask.Result is not { } ids || !detailsTask.Result.IsSucceeded)
                throw new InvalidDataException("The Workshop collection is unavailable or its response is incomplete.");
            cancellationToken.ThrowIfCancellationRequested();

            if (await taskCreator.StartNew(self =>
            {
                // Changing this option while fetching invalidates the snapshot.
                cancellationToken.ThrowIfCancellationRequested();
                if (self.CollectionId != id || self.IncludeLinkedCollections != includeLinkedCollections) return;
                using var blockAutoCheck = Root.BlockAutoCheck();
                Directory.CreateDirectory(self.FullFilePath);
                var remoteIds = ids.ToHashSet();
                if (self.AllowDeletionsActually)
                {
                    foreach (var member in self._managedItems.ToArray())
                    {
                        if (remoteIds.Contains(member.Key)) continue;
                        if (self.TryGetManagedChild(member.Key, member.Value) is { } addon)
                            removals.Add(addon.DestroyWithFileAsync());
                        self._managedItems.Remove(member.Key);
                        Root.RequestSave = true;
                    }
                }

                var strategy = self.EnableStrategy;
                bool enabled = self.IsEnabled;
                self.EnableStrategy = AddonGroupEnableStrategy.None;
                try
                {
                    foreach (var itemId in ids)
                    {
                        // Remember manually deleted/moved items until they leave the remote collection.
                        if (!self._managedItems.ContainsKey(itemId) && self.AllowDownloadsActually &&
                            !self.Children.OfType<WorkshopVpkAddon>().Any(child => child.PublishedFileId == itemId))
                        {
                            var addon = Create<WorkshopVpkAddon>(Root, self);
                            self._managedItems.Add(itemId, addon.Id);
                            addon.Name = self.GetUniqueChildName(itemId.ToString(CultureInfo.InvariantCulture));
                            addon.RequestAutoSetName = true;
                            addon.IsEnabled = strategy switch
                            {
                                AddonGroupEnableStrategy.All => enabled,
                                AddonGroupEnableStrategy.Single or AddonGroupEnableStrategy.SingleRandom =>
                                    self.EnableNewAddonsActually && !self.Children.Any(child => child != addon && child.IsEnabled),
                                _ => self.EnableNewAddonsActually
                            };
                            addon.PublishedFileId = itemId;
                            Root.RequestSave = true;
                        }
                    }
                }
                finally { self.EnableStrategy = strategy; }
                self.LastItemCount = ids.Length;
                blockMove = self.BlockMove();
            }).ConfigureAwait(false)) return;

            await Task.WhenAll(removals).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await taskCreator.StartNew(self =>
            {
                blockMove?.Dispose();
                blockMove = null;
                cancellationToken.ThrowIfCancellationRequested();
                if (self.CollectionId != id || self.IncludeLinkedCollections != includeLinkedCollections) return;
                // Reuse normal Workshop download/update policies and progress notifications.
                foreach (var itemId in ids)
                {
                    if (self._managedItems.TryGetValue(itemId, out var addonId) && self.TryGetManagedChild(itemId, addonId) is { } addon)
                    {
                        checks.Add(addon.CheckDownloadAsync(refreshDetails: true));
                        addon.Check();
                        if (addon.CheckTask is { } check) checks.Add(check);
                    }
                }
                self.LastSynchronizedAt = DateTimeOffset.UtcNow;
                self.ErrorMessage = null;
                self.InvalidateProblem<WorkshopCollectionSyncProblem>();
                Root.RequestSave = true;
                self.CheckChildrenProblems();
                self.CheckEnableStrategy();
            }).ConfigureAwait(false);
            await Task.WhenAll(checks).WaitAsync(cancellationToken).ConfigureAwait(false);
            await taskCreator.StartNew(self => self.CheckChildrenProblems()).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to synchronize Workshop collection {CollectionId}", id);
            await taskCreator.StartNew(self =>
            {
                self.ErrorMessage = ex.Message;
                self.SetProblem(new WorkshopCollectionSyncProblem(self, ex));
            }).ConfigureAwait(false);
        }
        finally
        {
            if (blockMove is not null)
                await taskCreator.StartNew(_ => blockMove.Dispose()).ConfigureAwait(false);
        }
    }

    private WorkshopVpkAddon? TryGetManagedChild(ulong publishedFileId, Guid addonId) =>
        Root.TryGetNodeById(addonId, out var node) && node is WorkshopVpkAddon addon &&
        addon.Group == this && addon.PublishedFileId == publishedFileId ? addon : null;

    protected override void OnCheck(Action<Task> taskSubmitter)
    {
        base.OnCheck(taskSubmitter);
        taskSubmitter(SynchronizeAsync());
    }

    protected override async Task OnDestroyAsync()
    {
        var synchronizationTask = SynchronizationTask;
        await base.OnDestroyAsync();
        if (synchronizationTask is not null) await synchronizationTask.ConfigureAwait(false);
    }

    protected override void OnCreateSave(AddonNodeSave save)
    {
        base.OnCreateSave(save);
        var subscription = (WorkshopCollectionSubscriptionSave)save;
        subscription.CollectionId = CollectionId;
        subscription.IncludeLinkedCollections = IncludeLinkedCollections;
        subscription.IsAutoUpdate = IsAutoUpdate;
        subscription.AllowDownloads = AllowDownloads;
        subscription.AllowDeletions = AllowDeletions;
        subscription.EnableNewAddons = EnableNewAddons;
        subscription.LastSynchronizedAt = LastSynchronizedAt;
        subscription.LastItemCount = LastItemCount;
        subscription.ManagedItems = _managedItems.Select(member => new WorkshopCollectionMember(member.Key, member.Value)).ToArray();
    }

    protected override void OnLoadSave(AddonNodeSave save)
    {
        base.OnLoadSave(save);
        var subscription = (WorkshopCollectionSubscriptionSave)save;
        _collectionId = subscription.CollectionId is > 0 ? subscription.CollectionId : null;
        IncludeLinkedCollections = subscription.IncludeLinkedCollections;
        IsAutoUpdate = subscription.IsAutoUpdate;
        AllowDownloads = subscription.AllowDownloads;
        AllowDeletions = subscription.AllowDeletions;
        EnableNewAddons = subscription.EnableNewAddons;
        LastSynchronizedAt = subscription.LastSynchronizedAt;
        LastItemCount = subscription.LastItemCount;
        foreach (var member in subscription.ManagedItems)
        {
            if (member.PublishedFileId > 0 && member.AddonId != Guid.Empty)
                _managedItems.TryAdd(member.PublishedFileId, member.AddonId);
        }
    }

    protected override void OnPostLoadSave(AddonNodeSave save)
    {
        base.OnPostLoadSave(save);
        // Importing an exported subscription can assign new IDs when the originals already exist.
        // Rebind only children explicitly owned in that save; never adopt manually added mods.
        var children = ((WorkshopCollectionSubscriptionSave)save).Children;
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] is WorkshopVpkAddonSave { PublishedFileId: { } publishedId } child &&
                _managedItems.TryGetValue(publishedId, out var savedId) && savedId == child.Id)
                _managedItems[publishedId] = Children[i].Id;
        }
    }
}
