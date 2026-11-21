using System.Net;
using Newtonsoft.Json;
using Xunit;

namespace FireAxe.Tests;

public sealed class WorkshopCollectionTests : IDisposable
{
    private readonly string _directory = Path.Join(Path.GetTempPath(), $"fireaxe-collection-tests-{Guid.NewGuid():N}");
    private readonly WorkshopHandler _handler = new();
    private readonly MemoryDownloads _downloads = new();
    private readonly Settings _settings = new();
    private readonly HttpClient _client;
    private readonly TaskScheduler _scheduler = new ConcurrentExclusiveSchedulerPair().ExclusiveScheduler;

    public WorkshopCollectionTests()
    {
        Directory.CreateDirectory(_directory);
        _client = new HttpClient(_handler);
        _handler.Collections[100] = [(1, 0), (2, 0)];
    }

    [Fact]
    public async Task DownloadsNewMembersAndRemovesOnlyOwnedChildrenWhilePreservingCustomizations()
    {
        await using var root = CreateRoot();
        using var block = root.BlockAutoCheck();
        var subscription = CreateSubscription(root);
        int notifications = 0;
        root.NewDownloadItem += _ => notifications++;
        await subscription.SynchronizeAsync();
        Assert.Null(subscription.ErrorMessage);
        Assert.Equal(2, subscription.Children.Count);
        Assert.Equal(2, _downloads.Count);
        Assert.Equal(2, notifications);
        var first = Find(subscription, 1);
        var removed = Find(subscription, 2);
        string removedPath = removed.FullFilePath;
        Assert.True(File.Exists(first.VpkFilePath));
        Assert.True(Directory.Exists(removedPath));
        first.Name = "My mod";
        first.Priority = 37;
        first.IsEnabled = false;
        first.AddTag("custom");
        var manual = AddonNode.Create<LocalVpkAddon>(root, subscription);
        manual.Name = "manual";
        File.WriteAllBytes(manual.FullFilePath, MemoryDownloads.Vpk);
        var gameMaps = AddonNode.Create<GameMapsAddon>(root, subscription);
        gameMaps.Name = "GameMaps";
        _handler.Collections[100] = [(1, 0), (3, 0)];
        await subscription.SynchronizeAsync();
        Assert.Null(subscription.ErrorMessage);
        Assert.False(removed.IsValid);
        Assert.False(Directory.Exists(removedPath));
        Assert.True(manual.IsValid);
        Assert.True(gameMaps.IsValid);
        Assert.Equal(first.Id, Find(subscription, 1).Id);
        Assert.Equal("My mod", first.Name);
        Assert.Equal(37, first.Priority);
        Assert.False(first.IsEnabled);
        Assert.Contains("custom", first.Tags);
        Assert.True(File.Exists(Find(subscription, 3).VpkFilePath));
        Assert.Equal(3, _downloads.Count);
    }

    [Fact]
    public async Task PermissionsRemainEffectiveDuringManualSynchronizationAndCanBeEnabledLater()
    {
        await using var root = CreateRoot();
        using var block = root.BlockAutoCheck();
        var subscription = CreateSubscription(root);
        await subscription.SynchronizeAsync();
        var retained = Find(subscription, 2);
        subscription.IsAutoUpdate = false;
        subscription.AllowDownloads = false;
        subscription.AllowDeletions = false;
        _handler.Collections[100] = [(1, 0), (3, 0)];
        int requests = _handler.CollectionRequests;
        await subscription.SynchronizeAsync();
        Assert.Equal(requests, _handler.CollectionRequests);
        await subscription.SynchronizeAsync(forceUpdate: true);
        Assert.True(retained.IsValid);
        Assert.DoesNotContain(subscription.Children.OfType<WorkshopVpkAddon>(), addon => addon.PublishedFileId == 3);
        subscription.AllowDownloads = true;
        subscription.AllowDeletions = true;
        await subscription.SynchronizeAsync(forceUpdate: true);
        Assert.False(retained.IsValid);
        Assert.True(File.Exists(Find(subscription, 3).VpkFilePath));
    }

    [Fact]
    public async Task InheritsLiveGlobalSettingsAndUsesSeparateWorkshopItemUpdatePolicy()
    {
        await using var root = CreateRoot();
        using var block = root.BlockAutoCheck();
        var subscription = CreateSubscription(root);
        _settings.AllowWorkshopCollectionDownloads = false;
        await subscription.SynchronizeAsync();
        Assert.Empty(subscription.Children);
        _settings.AllowWorkshopCollectionDownloads = true;
        _settings.EnableNewWorkshopCollectionAddons = false;
        await subscription.SynchronizeAsync();
        Assert.All(subscription.Children, addon => Assert.False(addon.IsEnabled));
        _handler.ModRevision = 2;
        _settings.IsAutoUpdateWorkshopItem = false;
        await subscription.SynchronizeAsync();
        Assert.Equal(2, _downloads.Count);
        _settings.IsAutoUpdateWorkshopItem = true;
        await subscription.SynchronizeAsync();
        Assert.Equal(4, _downloads.Count);
        _settings.IsAutoUpdateWorkshopCollection = false;
        Assert.False(subscription.IsAutomaticSynchronizationDue);
        subscription.IsAutoUpdate = true;
        Assert.True(subscription.IsAutoUpdateActually);
    }

    [Fact]
    public async Task ResolvesNestedCollectionsWithoutCyclesOrDuplicateDownloads()
    {
        _handler.Collections[100] = [(1, 0), (200, 2), (2, 0)];
        _handler.Collections[200] = [(1, 0), (100, 2), (3, 0), (300, 2)];
        _handler.Collections[300] = [(3, 0), (4, 0)];
        await using var root = CreateRoot();
        using var block = root.BlockAutoCheck();
        var subscription = CreateSubscription(root);
        await subscription.SynchronizeAsync();
        Assert.Null(subscription.ErrorMessage);
        Assert.Equal(4, subscription.Children.Count);
        Assert.Equal(4, _downloads.Count);
        subscription.IncludeLinkedCollections = false;
        await subscription.SynchronizeAsync();
        Assert.Equal(new ulong?[] { 1, 2 }, subscription.Children.OfType<WorkshopVpkAddon>().Select(addon => addon.PublishedFileId));
    }

    [Theory]
    [InlineData("private")]
    [InlineData("malformed")]
    [InlineData("missing-children")]
    [InlineData("wrong-count")]
    [InlineData("wrong-id")]
    [InlineData("nested-unavailable")]
    public async Task FailedOrIncompleteResponsesNeverRemoveExistingMembersAndCanBeRetried(string failure)
    {
        await using var root = CreateRoot();
        using var block = root.BlockAutoCheck();
        var subscription = CreateSubscription(root);
        await subscription.SynchronizeAsync();
        var ids = subscription.Children.Select(addon => addon.Id).ToArray();
        var lastSync = subscription.LastSynchronizedAt;
        _handler.Failure = failure;
        await subscription.SynchronizeAsync();
        Assert.NotNull(subscription.ErrorMessage);
        Assert.Contains(subscription.Problems, problem => problem is WorkshopCollectionSyncProblem);
        Assert.Equal(ids, subscription.Children.Select(addon => addon.Id));
        Assert.Equal(lastSync, subscription.LastSynchronizedAt);
        _handler.Failure = null;
        await subscription.SynchronizeAsync();
        Assert.Null(subscription.ErrorMessage);
        Assert.DoesNotContain(subscription.Problems, problem => problem is WorkshopCollectionSyncProblem);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuccessfulEmptyCollectionRemovesAllManagedMods(bool omitEmptyChildren)
    {
        await using var root = CreateRoot();
        using var block = root.BlockAutoCheck();
        var subscription = CreateSubscription(root);
        await subscription.SynchronizeAsync();
        _handler.Collections[100] = [];
        _handler.OmitEmptyChildren = omitEmptyChildren;
        await subscription.SynchronizeAsync();
        Assert.Null(subscription.ErrorMessage);
        Assert.Empty(subscription.Children);
        Assert.Equal(0, subscription.LastItemCount);
    }

    [Fact]
    public async Task RespectsMovedDeletedAndManuallyAddedWorkshopModsAcrossSaveReload()
    {
        await using var root = CreateRoot();
        using var block = root.BlockAutoCheck();
        var subscription = CreateSubscription(root);
        await subscription.SynchronizeAsync();
        var moved = Find(subscription, 1);
        moved.MoveTo(null);
        await Find(subscription, 2).DestroyWithFileAsync();
        var manual = AddonNode.Create<WorkshopVpkAddon>(root, subscription);
        manual.Name = "manual Workshop mod";
        manual.PublishedFileId = 3;
        subscription.IsAutoUpdate = false;
        subscription.AllowDownloads = true;
        subscription.EnableNewAddons = false;
        _handler.Collections[100] = [(1, 0), (2, 0), (3, 0), (4, 0)];
        string json = AddonRoot.Serialize(root.CreateSave());
        await using var restored = CreateRoot(root.DirectoryPath);
        using var blockRestored = restored.BlockAutoCheck();
        restored.LoadSave(AddonRoot.Deserialize(json));
        var restoredSubscription = Assert.IsType<WorkshopCollectionSubscription>(restored.Nodes[0]);
        Assert.False(restoredSubscription.IsAutoUpdate);
        Assert.True(restoredSubscription.AllowDownloads);
        Assert.False(restoredSubscription.EnableNewAddons);
        await restoredSubscription.SynchronizeAsync(forceUpdate: true);
        Assert.Equal(new ulong?[] { 3, 4 }, restoredSubscription.Children.OfType<WorkshopVpkAddon>().Select(addon => addon.PublishedFileId));
        Assert.Equal(manual.Id, Find(restoredSubscription, 3).Id);
        Assert.Equal(3, _downloads.Count);
        _handler.Collections[100] = [];
        await restoredSubscription.SynchronizeAsync(forceUpdate: true);
        Assert.Equal(manual.Id, Assert.Single(restoredSubscription.Children).Id);
        Assert.True(restored.TryGetNodeById(moved.Id, out var preserved));
        Assert.Null(preserved!.Group);
    }

    [Theory]
    [InlineData(AddonGroupEnableStrategy.None, true)]
    [InlineData(AddonGroupEnableStrategy.All, true)]
    [InlineData(AddonGroupEnableStrategy.All, false)]
    [InlineData(AddonGroupEnableStrategy.Single, true)]
    [InlineData(AddonGroupEnableStrategy.SingleRandom, true)]
    public async Task AddingMembersPreservesTheGroupsEnableStrategy(AddonGroupEnableStrategy strategy, bool enabled)
    {
        await using var root = CreateRoot();
        using var block = root.BlockAutoCheck();
        var subscription = CreateSubscription(root);
        subscription.EnableStrategy = strategy;
        subscription.IsEnabled = enabled;
        await subscription.SynchronizeAsync();
        Assert.Null(subscription.ErrorMessage);
        Assert.Equal(strategy, subscription.EnableStrategy);
        Assert.Equal(enabled, subscription.IsEnabled);
        if (strategy is AddonGroupEnableStrategy.Single or AddonGroupEnableStrategy.SingleRandom)
            Assert.Single(subscription.Children, addon => addon.IsEnabled);
        else
            Assert.All(subscription.Children, addon => Assert.Equal(strategy == AddonGroupEnableStrategy.None || enabled, addon.IsEnabled));
        var first = Find(subscription, 1);
        _handler.Collections[100] = [(1, 0), (2, 0), (3, 0)];
        await subscription.SynchronizeAsync();
        Assert.Equal(enabled, subscription.IsEnabled);
        Assert.Equal(first.IsEnabled, Find(subscription, 1).IsEnabled);
    }

    [Fact]
    public async Task CoalescesConcurrentChecksAndCancelsBeforeMutatingAClosedRoot()
    {
        await using var root = CreateRoot();
        using var block = root.BlockAutoCheck();
        var subscription = CreateSubscription(root);
        _handler.Gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = subscription.SynchronizeAsync();
        Assert.Same(first, subscription.SynchronizeAsync(forceUpdate: true));
        await _handler.RequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await root.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        await first.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(subscription.IsValid);
        Assert.Equal(0, _downloads.Count);
    }

    [Fact]
    public async Task UnchangedCollectionsDoNotRedownloadAndAutomaticChecksObserveTheInterval()
    {
        await using var root = CreateRoot();
        using var block = root.BlockAutoCheck();
        var subscription = CreateSubscription(root);
        Assert.True(subscription.IsAutomaticSynchronizationDue);
        await subscription.SynchronizeAsync();
        Assert.False(subscription.IsAutomaticSynchronizationDue);
        var ids = subscription.Children.Select(addon => addon.Id).ToArray();
        await subscription.SynchronizeAsync(forceUpdate: true);
        Assert.Equal(ids, subscription.Children.Select(addon => addon.Id));
        Assert.Equal(2, _downloads.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => subscription.CollectionId = 0);
    }

    [Fact]
    public async Task ImportingAnExistingSubscriptionRemapsOwnershipWithoutDeletingTheOriginals()
    {
        await using var root = CreateRoot();
        using var block = root.BlockAutoCheck();
        var original = CreateSubscription(root);
        await original.SynchronizeAsync();
        var originalMod = Find(original, 2);
        var imported = Assert.IsType<WorkshopCollectionSubscription>(AddonNode.LoadSave(original.CreateSave(), root));
        var importedMod = Find(imported, 2);
        Assert.NotEqual(originalMod.Id, importedMod.Id);
        _handler.Collections[100] = [(1, 0)];
        await imported.SynchronizeAsync();
        Assert.Null(imported.ErrorMessage);
        Assert.False(importedMod.IsValid);
        Assert.True(originalMod.IsValid);
        Assert.Equal(2, original.Children.Count);
        Assert.Single(imported.Children);
    }

    [Fact]
    public async Task DiscardsSnapshotWhenLinkedCollectionOptionChangesDuringTheRequest()
    {
        await using var root = CreateRoot();
        using var block = root.BlockAutoCheck();
        var subscription = CreateSubscription(root);
        _handler.Gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var synchronization = subscription.SynchronizeAsync();
        await _handler.RequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        subscription.IncludeLinkedCollections = false;
        _handler.Gate.SetResult();
        await synchronization;
        Assert.Empty(subscription.Children);
        Assert.Null(subscription.LastSynchronizedAt);
        await subscription.SynchronizeAsync();
        Assert.Equal(2, subscription.Children.Count);
    }

    [Fact]
    public async Task ChangingCollectionIdKeepsMatchingModsAndReconcilesOldAndNewMembers()
    {
        await using var root = CreateRoot();
        using var block = root.BlockAutoCheck();
        var subscription = CreateSubscription(root);
        await subscription.SynchronizeAsync();
        var obsolete = Find(subscription, 1);
        var shared = Find(subscription, 2);
        shared.Name = "My shared mod";
        shared.AddTag("favorite");
        _handler.Collections[200] = [(2, 0), (3, 0)];
        subscription.CollectionId = 200;
        Assert.Null(subscription.LastSynchronizedAt);
        Assert.True(obsolete.IsValid);
        await subscription.SynchronizeAsync(forceUpdate: true);
        Assert.Null(subscription.ErrorMessage);
        Assert.False(obsolete.IsValid);
        Assert.Equal(shared.Id, Find(subscription, 2).Id);
        Assert.Equal("My shared mod", shared.Name);
        Assert.Contains("favorite", shared.Tags);
        Assert.True(File.Exists(Find(subscription, 3).VpkFilePath));
        Assert.Equal(3, _downloads.Count);
        var restoredSave = AddonRoot.Deserialize(AddonRoot.Serialize(root.CreateSave()));
        Assert.Equal(200ul, Assert.IsType<WorkshopCollectionSubscriptionSave>(restoredSave.Nodes[0]).CollectionId);
    }

    [Fact]
    public async Task InvalidNewSourcePreservesOldMembersUntilAValidSnapshotIsAvailable()
    {
        await using var root = CreateRoot();
        using var block = root.BlockAutoCheck();
        var subscription = CreateSubscription(root);
        await subscription.SynchronizeAsync();
        var previous = subscription.Children.ToArray();
        subscription.CollectionId = 200;
        _handler.Failure = "private";
        await subscription.SynchronizeAsync(forceUpdate: true);
        Assert.NotNull(subscription.ErrorMessage);
        Assert.Equal(previous, subscription.Children);
        Assert.All(previous, addon => Assert.True(Directory.Exists(addon.FullFilePath)));
        _handler.Failure = null;
        _handler.Collections[200] = [(3, 0)];
        await subscription.SynchronizeAsync(forceUpdate: true);
        Assert.Null(subscription.ErrorMessage);
        Assert.Equal(3ul, Assert.IsType<WorkshopVpkAddon>(Assert.Single(subscription.Children)).PublishedFileId);
    }

    [Fact]
    public async Task SourceChangesRespectDownloadAndRemovalPermissions()
    {
        await using var root = CreateRoot();
        using var block = root.BlockAutoCheck();
        var subscription = CreateSubscription(root);
        await subscription.SynchronizeAsync();
        _handler.Collections[200] = [(2, 0), (3, 0)];
        subscription.AllowDownloads = false;
        subscription.AllowDeletions = false;
        subscription.CollectionId = 200;
        await subscription.SynchronizeAsync(forceUpdate: true);
        Assert.Equal(new ulong?[] { 1, 2 }, subscription.Children.OfType<WorkshopVpkAddon>().Select(addon => addon.PublishedFileId));
        subscription.AllowDownloads = true;
        subscription.AllowDeletions = true;
        await subscription.SynchronizeAsync(forceUpdate: true);
        Assert.Equal(new ulong?[] { 2, 3 }, subscription.Children.OfType<WorkshopVpkAddon>().Select(addon => addon.PublishedFileId));
    }

    [Fact]
    public async Task ChangingSourceCancelsOldRequestAndSynchronizesLatestIdWithoutApplyingOldResults()
    {
        await using var root = CreateRoot();
        using var block = root.BlockAutoCheck();
        var subscription = CreateSubscription(root);
        _handler.Gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldRequest = subscription.SynchronizeAsync();
        await _handler.RequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        _handler.Collections[200] = [(3, 0)];
        subscription.CollectionId = 200;
        var newRequest = subscription.SynchronizeAsync(forceUpdate: true);
        _handler.Gate.SetResult();
        await Task.WhenAll(oldRequest, newRequest).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(subscription.ErrorMessage);
        Assert.Equal(3ul, Assert.IsType<WorkshopVpkAddon>(Assert.Single(subscription.Children)).PublishedFileId);
        Assert.Equal(1, _downloads.Count);
    }

    [Fact]
    public async Task EditingSourceTriggersSynchronizationEvenWhenAutomaticUpdatesAreDisabled()
    {
        await using var root = CreateRoot();
        WorkshopCollectionSubscription subscription;
        using (root.BlockAutoCheck())
        {
            subscription = CreateSubscription(root);
            subscription.IsAutoUpdate = false;
        }
        _handler.Collections[200] = [(3, 0)];
        subscription.CollectionId = 200;
        await subscription.SynchronizationTask!.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(subscription.ErrorMessage);
        Assert.Equal(3ul, Assert.IsType<WorkshopVpkAddon>(Assert.Single(subscription.Children)).PublishedFileId);
    }

    [Fact]
    public async Task DownloadsMembersWithAutomaticChecksEnabledAsInTheApplication()
    {
        await using var root = CreateRoot();
        WorkshopCollectionSubscription subscription;
        using (root.BlockAutoCheck()) subscription = CreateSubscription(root);

        await subscription.SynchronizeAsync(forceUpdate: true).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Null(subscription.ErrorMessage);
        Assert.Equal(2, subscription.Children.Count);
        Assert.Equal(2, _downloads.Count);
        Assert.All(subscription.Children, child => Assert.True(File.Exists(Assert.IsType<WorkshopVpkAddon>(child).VpkFilePath)));
    }

    private AddonRoot CreateRoot(string? directory = null) => new()
    {
        DirectoryPath = directory ?? Path.Join(_directory, Guid.NewGuid().ToString("N")),
        TaskScheduler = _scheduler,
        HttpClient = _client,
        DownloadService = _downloads,
        ParentSettings = _settings
    };

    private static WorkshopCollectionSubscription CreateSubscription(AddonRoot root)
    {
        var subscription = AddonNode.Create<WorkshopCollectionSubscription>(root);
        subscription.Name = "Collection";
        subscription.CollectionId = 100;
        return subscription;
    }

    private static WorkshopVpkAddon Find(WorkshopCollectionSubscription subscription, ulong id) =>
        subscription.Children.OfType<WorkshopVpkAddon>().Single(addon => addon.PublishedFileId == id);

    public void Dispose()
    {
        _client.Dispose();
        Directory.Delete(_directory, true);
    }

    private sealed class Settings : IAddonRootParentSettings
    {
        public bool IsAutoUpdateWorkshopItem { get; set; } = true;
        public bool IsAutoUpdateGameMapsItem => true;
        public bool IsAutoUpdateWorkshopCollection { get; set; } = true;
        public bool AllowWorkshopCollectionDownloads { get; set; } = true;
        public bool AllowWorkshopCollectionDeletions { get; set; } = true;
        public bool EnableNewWorkshopCollectionAddons { get; set; } = true;
        public VpkAddonConflictCheckSettings VpkAddonConflictCheckSettings => VpkAddonConflictCheckSettings.Default;
    }

    private sealed class WorkshopHandler : HttpMessageHandler
    {
        public readonly Dictionary<ulong, (ulong Id, int FileType)[]> Collections = new();
        public string? Failure;
        public bool OmitEmptyChildren;
        public int CollectionRequests;
        public ulong ModRevision = 1;
        public TaskCompletionSource? Gate;
        public readonly TaskCompletionSource RequestStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
                return new(HttpStatusCode.OK) { Content = new ByteArrayContent([0]) };
            string body = await request.Content!.ReadAsStringAsync(cancellationToken);
            ulong id = ulong.Parse(body[(body.LastIndexOf('=') + 1)..]);
            if (request.RequestUri!.AbsolutePath.Contains("GetCollectionDetails"))
            {
                CollectionRequests++;
                RequestStarted.TrySetResult();
                if (Gate is not null) await Gate.Task.WaitAsync(cancellationToken);
                if (Failure == "malformed") return Json("<html>unavailable</html>");
                if (Failure == "nested-unavailable")
                    return id == 100 ? Collection(id, [(200, 2)]) : Json(new { response = new { result = 1, collectiondetails = new[] { new { result = 9 } } } });
                if (Failure == "private") return Json(new { response = new { result = 1, collectiondetails = new[] { new { result = 9 } } } });
                if (Failure == "missing-children") return Json(new { response = new { result = 1, collectiondetails = new[] { new { result = 1, publishedfileid = id.ToString() } } } });
                return Collection(Failure == "wrong-id" ? id + 1 : id, Collections.GetValueOrDefault(id, []));
            }
            return Json(new
            {
                response = new
                {
                    publishedfiledetails = new[] { new { result = 1, consumer_app_id = 550, title = $"Mod {id}",
                        time_updated = ModRevision, preview_url = $"https://example.test/image/{id}",
                        file_url = $"https://example.test/mod/{id}", tags = Array.Empty<object>() } }
                }
            });
        }

        private HttpResponseMessage Collection(ulong id, (ulong Id, int FileType)[] items)
        {
            var details = new Dictionary<string, object>
            {
                ["result"] = 1, ["publishedfileid"] = id.ToString(),
                ["childcount"] = Failure == "wrong-count" ? items.Length + 1 : items.Length
            };
            if (!(OmitEmptyChildren && items.Length == 0))
                details["children"] = items.Select(item => new { publishedfileid = item.Id.ToString(), filetype = item.FileType }).ToArray();
            return Json(new { response = new { result = 1, collectiondetails = new[] { details } } });
        }

        private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(value is string text ? text : JsonConvert.SerializeObject(value))
        };
    }

    private sealed class MemoryDownloads : IDownloadService
    {
        public static readonly byte[] Vpk = [0x34, 0x12, 0xaa, 0x55, 1, 0, 0, 0, 1, 0, 0, 0, 0];
        public int Count;
        public IDownloadItem Download(string url, string filePath)
        {
            Count++;
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllBytes(filePath, Vpk);
            return new CompletedDownload(url, filePath);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class CompletedDownload(string url, string path) : IDownloadItem
    {
        public string Url => url;
        public string FilePath => path;
        public long DownloadedBytes => MemoryDownloads.Vpk.Length;
        public long TotalBytes => DownloadedBytes;
        public double BytesPerSecondSpeed => 0;
        public DownloadStatus Status => DownloadStatus.Succeeded;
        public Exception Exception => throw new InvalidOperationException();
        public void Pause() { }
        public void Resume() { }
        public void Cancel() { }
        public void Wait() { }
        public Task WaitAsync() => Task.CompletedTask;
        public void Dispose() { }
    }
}
