using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace FireAxe.Tests;

public sealed class GameMapsTests : IDisposable
{
    private readonly string _directory = Path.Join(Path.GetTempPath(), $"fireaxe-tests-{Guid.NewGuid():N}");
    // An empty version 1 VPK with a terminating directory tree byte.
    private static readonly byte[] Vpk = [0x34, 0x12, 0xaa, 0x55, 1, 0, 0, 0, 1, 0, 0, 0, 0];

    public GameMapsTests() => Directory.CreateDirectory(_directory);

    [Theory]
    [InlineData("24878", true)]
    [InlineData(" https://www.gamemaps.com/details/24878 ", true)]
    [InlineData("https://gamemaps.com/details/24878/?foo=1#download", true)]
    [InlineData("http://www.gamemaps.com/details/24878", true)]
    [InlineData("https://www.gamemaps.com.evil.test/details/24878", false)]
    [InlineData("https://evil.test/?next=https://www.gamemaps.com/details/24878", false)]
    [InlineData("https://www.gamemaps.com/details/24878/extra", false)]
    [InlineData("https://user@www.gamemaps.com/details/24878", false)]
    [InlineData("https://www.gamemaps.com:444/details/24878", false)]
    [InlineData("0", false)]
    [InlineData("-24878", false)]
    [InlineData("18446744073709551616", false)]
    public void ParsesOnlyDetailsUrlsOrPositiveIds(string value, bool valid)
    {
        Assert.Equal(valid, GameMapsClient.TryParseDetailsId(value, out var id));
        if (valid) Assert.Equal(24878ul, id);
    }

    [Fact]
    public async Task ResolvesFreshSignedUrlWithFormFieldsAndReferer()
    {
        var handler = new GameMapsHandler(CreateZip(("addon.vpk", Vpk)));
        using var client = new GameMapsClient(new HttpClient(handler));
        var first = await client.GetDownloadInfoAsync(24878);
        var second = await client.GetDownloadInfoAsync(24878);
        Assert.Equal("Test & Campaign", first.Title);
        Assert.NotEqual(first.DownloadUrl, second.DownloadUrl);
        Assert.Equal(first.Revision, second.Revision);
        Assert.Equal(0, handler.Downloads);
        Assert.Equal(2, handler.Posts);
    }

    [Fact]
    public async Task ReportsCloudflareAndRejectsHtmlDownloadResponses()
    {
        using var blocked = new GameMapsClient(new HttpClient(new DelegateHandler(_ => new(HttpStatusCode.Forbidden))));
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => blocked.GetDownloadInfoAsync(24878));
        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
        Assert.Contains("Cloudflare", error.Message);

        using var client = new GameMapsClient(new HttpClient(new DelegateHandler(_ => Html("<h1>Access denied</h1>"))));
        using var download = client.Download("https://www.gamemaps.com/download/current", Path.Join(_directory, "html.archive"), 24878);
        await download.WaitAsync();
        Assert.Equal(DownloadStatus.Failed, download.Status);
        Assert.False(File.Exists(download.FilePath));
    }

    [Fact]
    public async Task ExtractsOnlyVpksAndFlattensPathsWithoutOverwritingDuplicateNames()
    {
        string source = Path.Join(_directory, "source.archive");
        await File.WriteAllBytesAsync(source, CreateZip(
            ("folder/part.vpk", Vpk), ("../../part.vpk", Vpk),
            ("windows\\UPPER.VPK", Vpk), ("run.exe", [1, 2, 3]), ("readme.txt", [4])));
        string destination = Path.Join(_directory, "output");
        var paths = await VpkArchiveExtractor.ExtractAsync(source, destination);
        Assert.Equal(3, paths.Count);
        Assert.Equal(3, paths.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach (string path in paths)
        {
            Assert.Equal(destination, Path.GetDirectoryName(path));
            Assert.Equal(Vpk, await File.ReadAllBytesAsync(path));
        }
        Assert.False(File.Exists(Path.Join(_directory, "part.vpk")));
        Assert.Equal(3, Directory.GetFiles(destination).Length);
    }

    [Fact]
    public async Task AcceptsDirectVpkAndRejectsArchivesWithoutValidVpks()
    {
        string source = Path.Join(_directory, "source");
        await File.WriteAllBytesAsync(source, Vpk);
        Assert.Single(await VpkArchiveExtractor.ExtractAsync(source, Path.Join(_directory, "raw")));
        await File.WriteAllBytesAsync(source, CreateZip(("readme.txt", [1])));
        await Assert.ThrowsAsync<InvalidDataException>(() => VpkArchiveExtractor.ExtractAsync(source, Path.Join(_directory, "empty")));
        await File.WriteAllBytesAsync(source, CreateZip(("bad.vpk", [1, 2, 3, 4])));
        await Assert.ThrowsAsync<InvalidDataException>(() => VpkArchiveExtractor.ExtractAsync(source, Path.Join(_directory, "invalid")));
    }

    [Fact]
    public async Task ImportsMultipleVpksIntoNamedGroupAndRestoresWithoutRedownloading()
    {
        var handler = new GameMapsHandler(CreateZip(("maps/one.vpk", Vpk), ("two.vpk", Vpk), ("readme.txt", [1])));
        using var client = new GameMapsClient(new HttpClient(handler));
        await using var root = CreateRoot(client, "addons");
        GameMapsAddon addon;
        using (root.BlockAutoCheck())
        {
            addon = AddonNode.Create<GameMapsAddon>(root);
            addon.Name = "New GameMaps addon";
            addon.DetailsId = 24878;
        }
        int notifications = 0;
        root.NewDownloadItem += _ => notifications++;
        await addon.CheckDownloadAsync();
        Assert.Null(addon.ErrorMessage);
        Assert.Equal("Test & Campaign", addon.Name);
        Assert.Equal(2, addon.Children.Count);
        Assert.All(addon.Children, node => Assert.True(File.Exists(Assert.IsType<LocalVpkAddon>(node).VpkFilePath)));
        addon.IsEnabled = true;
        Assert.All(addon.Children, node => Assert.True(node.IsEnabledInHierarchy));
        Assert.Equal(1, notifications);
        await addon.CheckDownloadAsync();
        Assert.Equal(1, handler.Downloads);

        string json = AddonRoot.Serialize(root.CreateSave());
        var save = AddonRoot.Deserialize(json);
        var savedAddon = Assert.IsType<GameMapsAddonSave>(Assert.Single(save.Nodes));
        Assert.Equal(24878ul, savedAddon.DetailsId);
        Assert.Equal(2, savedAddon.InstalledFiles.Length);
        await using var restored = new AddonRoot { DirectoryPath = root.DirectoryPath, TaskScheduler = TaskScheduler.Default, GameMapsClient = client };
        restored.LoadSave(save);
        var restoredAddon = Assert.IsType<GameMapsAddon>(Assert.Single(restored.Nodes));
        Assert.Equal(addon.Name, restoredAddon.Name);
        Assert.Equal(2, restoredAddon.Children.Count);
        await restoredAddon.CheckDownloadAsync();
        Assert.Equal(1, handler.Downloads);
    }

    [Fact]
    public async Task RepairsMissingFileWithoutDuplicatingOrUndoingRenames()
    {
        var handler = new GameMapsHandler(CreateZip(("one.vpk", Vpk), ("two.vpk", Vpk)));
        using var client = new GameMapsClient(new HttpClient(handler));
        await using var root = CreateRoot(client, "repair");
        using var block = root.BlockAutoCheck();
        var addon = AddonNode.Create<GameMapsAddon>(root);
        addon.Name = "GameMaps";
        addon.DetailsId = 24878;
        await addon.CheckDownloadAsync();
        var child = Assert.IsType<LocalVpkAddon>(addon.Children[0]);
        child.Name = "renamed";
        File.Delete(child.FullFilePath);
        await addon.CheckDownloadAsync();
        Assert.Null(addon.ErrorMessage);
        Assert.True(File.Exists(child.FullFilePath));
        Assert.Equal("renamed", child.Name);
        Assert.Equal(2, addon.Children.Count);
        Assert.Equal(2, handler.Downloads);
    }

    [Fact]
    public async Task FailedExtractionDoesNotImportPartialResultsAndCanBeRetried()
    {
        var handler = new GameMapsHandler(CreateZip(("good.vpk", Vpk), ("bad.vpk", [1, 2, 3])));
        using var client = new GameMapsClient(new HttpClient(handler));
        await using var root = CreateRoot(client, "retry");
        using var block = root.BlockAutoCheck();
        var addon = AddonNode.Create<GameMapsAddon>(root);
        addon.Name = "GameMaps";
        addon.DetailsId = 24878;
        await addon.CheckDownloadAsync();
        Assert.NotNull(addon.ErrorMessage);
        Assert.Empty(addon.Children);
        Assert.Contains(addon.Problems, problem => problem is AddonDownloadFailedProblem);
        handler.Payload = CreateZip(("good.vpk", Vpk));
        await addon.CheckDownloadAsync();
        Assert.Null(addon.ErrorMessage);
        Assert.Single(addon.Children);
        Assert.DoesNotContain(addon.Problems, problem => problem is AddonDownloadFailedProblem);
    }

    [Fact]
    public async Task RevisionIgnoresReviewsButDetectsFileUpdateTimeAndVersion()
    {
        var handler = new GameMapsHandler(CreateZip(("one.vpk", Vpk)));
        using var client = new GameMapsClient(new HttpClient(handler));
        var first = await client.GetDetailsAsync(24878);
        handler.PageNoise = "100 reviews and 12345 views";
        var same = await client.GetDetailsAsync(24878);
        Assert.Equal(first.Revision, same.Revision);
        handler.UpdatedAt = "2026-11-21T01:02:03";
        var newUpload = await client.GetDetailsAsync(24878);
        Assert.NotEqual(first.Revision, newUpload.Revision);
        handler.Version = "2.0";
        Assert.NotEqual(newUpload.Revision, (await client.GetDetailsAsync(24878)).Revision);
        Assert.Equal(0, handler.Posts);
    }

    [Theory]
    [InlineData(true, null, true)]
    [InlineData(false, null, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    public async Task PerAddonPolicyOverridesGameMapsGlobalSetting(bool global, bool? local, bool expected)
    {
        using var client = new GameMapsClient(new HttpClient(new GameMapsHandler(CreateZip(("one.vpk", Vpk)))));
        await using var root = CreateRoot(client, "policy");
        root.ParentSettings = new ParentSettings { IsAutoUpdateGameMapsItem = global };
        using var block = root.BlockAutoCheck();
        var addon = CreateAddon(root);
        addon.IsAutoUpdate = local;
        Assert.Equal(expected, addon.IsAutoUpdateActually);
        Assert.Equal(local, Assert.IsType<GameMapsAddonSave>(addon.CreateSave()).IsAutoUpdate);
    }

    [Fact]
    public async Task ChecksUpdateExistingVpksAndKeepGroupIdsNamesAndTags()
    {
        var handler = new GameMapsHandler(CreateZip(("one.vpk", Vpk), ("two.vpk", Vpk)));
        using var client = new GameMapsClient(new HttpClient(handler));
        await using var root = CreateRoot(client, "update");
        root.ParentSettings = new ParentSettings { IsAutoUpdateGameMapsItem = true };
        using var block = root.BlockAutoCheck();
        var addon = CreateAddon(root);
        await addon.CheckDownloadAsync();
        addon.Name = "My Campaign";
        addon.IsEnabled = true;
        var child = addon.Children[0];
        child.Name = "My VPK";
        child.AddTag("custom-tag");
        Guid id = child.Id;
        string? revision = addon.InstalledRevision;
        byte[] updatedVpk = [.. Vpk, 42];
        handler.Payload = CreateZip(("one.vpk", updatedVpk), ("two.vpk", updatedVpk));
        handler.Version = "2.0";
        await addon.CheckDownloadAsync();
        Assert.Null(addon.ErrorMessage);
        Assert.NotEqual(revision, addon.InstalledRevision);
        Assert.Equal("My Campaign", addon.Name);
        Assert.Equal("My VPK", child.Name);
        Assert.Equal(id, child.Id);
        Assert.Contains("custom-tag", child.Tags);
        Assert.All(addon.Children, node => Assert.True(node.IsEnabledInHierarchy));
        Assert.All(addon.Children, node => Assert.Equal(updatedVpk, File.ReadAllBytes(node.FullFilePath)));
        await addon.CheckDownloadAsync();
        Assert.Equal(2, handler.Downloads);
        Assert.Equal(2, handler.Posts);
        Assert.Equal(3, handler.PageRequests);
    }

    [Fact]
    public async Task DisabledAutoUpdateKeepsOldFilesButManualUpdateStillWorksAndPersistsPolicy()
    {
        var handler = new GameMapsHandler(CreateZip(("one.vpk", Vpk)));
        using var client = new GameMapsClient(new HttpClient(handler));
        await using var root = CreateRoot(client, "manual");
        root.ParentSettings = new ParentSettings { IsAutoUpdateGameMapsItem = true };
        using var block = root.BlockAutoCheck();
        var addon = CreateAddon(root);
        addon.IsAutoUpdate = false;
        await addon.CheckDownloadAsync();
        string? oldRevision = addon.InstalledRevision;
        byte[] updatedVpk = [.. Vpk, 7];
        handler.Version = "2.0";
        handler.Payload = CreateZip(("one.vpk", updatedVpk));
        await addon.CheckDownloadAsync();
        Assert.Equal(1, handler.Downloads);
        Assert.Equal(oldRevision, addon.InstalledRevision);
        await addon.CheckDownloadAsync(forceUpdate: true);
        Assert.Equal(2, handler.Downloads);
        Assert.Equal(updatedVpk, await File.ReadAllBytesAsync(Assert.Single(addon.Children).FullFilePath));
        var save = AddonRoot.Deserialize(AddonRoot.Serialize(root.CreateSave()));
        await using var restored = new AddonRoot { DirectoryPath = root.DirectoryPath, TaskScheduler = TaskScheduler.Default, GameMapsClient = client, ParentSettings = root.ParentSettings };
        using (restored.BlockAutoCheck()) restored.LoadSave(save);
        var restoredAddon = Assert.IsType<GameMapsAddon>(Assert.Single(restored.Nodes));
        Assert.False(restoredAddon.IsAutoUpdateActually);
        Assert.Equal(addon.InstalledRevision, restoredAddon.InstalledRevision);
        await restoredAddon.CheckDownloadAsync();
        Assert.Equal(2, handler.Downloads);
    }

    [Fact]
    public async Task UpdateAddsAndRemovesPartsWithoutTouchingUnmanagedVpks()
    {
        var handler = new GameMapsHandler(CreateZip(("keep.vpk", Vpk), ("old.vpk", Vpk)));
        using var client = new GameMapsClient(new HttpClient(handler));
        await using var root = CreateRoot(client, "parts");
        using var block = root.BlockAutoCheck();
        var addon = CreateAddon(root);
        addon.IsAutoUpdate = true;
        await addon.CheckDownloadAsync();
        var obsolete = addon.Children.Single(node => node.Name == "old");
        string oldPath = obsolete.FullFilePath;
        var custom = AddonNode.Create<LocalVpkAddon>(root, addon);
        custom.Name = "custom";
        await File.WriteAllBytesAsync(custom.FullFilePath, Vpk);
        handler.Version = "2.0";
        handler.Payload = CreateZip(("keep.vpk", Vpk), ("new.vpk", Vpk));
        await addon.CheckDownloadAsync();
        Assert.Null(addon.ErrorMessage);
        Assert.False(obsolete.IsValid);
        Assert.False(File.Exists(oldPath));
        Assert.Equal(new[] { "custom", "keep", "new" }, addon.Children.Select(node => node.Name).Order().ToArray());
        Assert.True(File.Exists(custom.FullFilePath));
    }

    [Fact]
    public async Task SingleVpkFilenameChangePreservesTheExistingNode()
    {
        var handler = new GameMapsHandler(CreateZip(("campaign-v1.vpk", Vpk)));
        using var client = new GameMapsClient(new HttpClient(handler));
        await using var root = CreateRoot(client, "filename");
        using var block = root.BlockAutoCheck();
        var addon = CreateAddon(root);
        addon.IsAutoUpdate = true;
        await addon.CheckDownloadAsync();
        var child = Assert.Single(addon.Children);
        handler.Version = "2.0";
        handler.Payload = CreateZip(("campaign-v2.vpk", [.. Vpk, 22]));
        await addon.CheckDownloadAsync();
        Assert.Same(child, Assert.Single(addon.Children));
        Assert.Equal("campaign-v2.vpk", Assert.Single(Assert.IsType<GameMapsAddonSave>(addon.CreateSave()).InstalledFiles).FileName);
    }

    [Fact]
    public async Task FailedReplacementRollsBackFilesAndVersionThenAllowsRetry()
    {
        var handler = new GameMapsHandler(CreateZip(("one.vpk", Vpk), ("two.vpk", Vpk)));
        using var client = new GameMapsClient(new HttpClient(handler));
        await using var root = CreateRoot(client, "rollback");
        using var block = root.BlockAutoCheck();
        var addon = CreateAddon(root);
        addon.IsAutoUpdate = true;
        await addon.CheckDownloadAsync();
        string? revision = addon.InstalledRevision;
        var first = addon.Children.Single(node => node.Name == "one");
        var second = addon.Children.Single(node => node.Name == "two");
        File.Delete(second.FullFilePath);
        Directory.CreateDirectory(second.FullFilePath);
        byte[] updatedVpk = [.. Vpk, 99];
        handler.Version = "2.0";
        handler.Payload = CreateZip(("one.vpk", updatedVpk), ("two.vpk", updatedVpk));
        await addon.CheckDownloadAsync();
        Assert.NotNull(addon.ErrorMessage);
        Assert.Equal(revision, addon.InstalledRevision);
        Assert.Equal(Vpk, await File.ReadAllBytesAsync(first.FullFilePath));
        Assert.Equal(2, addon.Children.Count);
        Directory.Delete(second.FullFilePath);
        await addon.CheckDownloadAsync();
        Assert.Null(addon.ErrorMessage);
        Assert.Equal(updatedVpk, await File.ReadAllBytesAsync(first.FullFilePath));
        Assert.Equal(updatedVpk, await File.ReadAllBytesAsync(second.FullFilePath));
        Assert.NotEqual(revision, addon.InstalledRevision);
    }

    private static GameMapsAddon CreateAddon(AddonRoot root)
    {
        using var block = root.BlockAutoCheck();
        var addon = AddonNode.Create<GameMapsAddon>(root);
        addon.Name = "GameMaps";
        addon.DetailsId = 24878;
        return addon;
    }

    private sealed class ParentSettings : IAddonRootParentSettings
    {
        public bool IsAutoUpdateWorkshopItem => true;
        public bool IsAutoUpdateGameMapsItem { get; set; }
        public VpkAddonConflictCheckSettings VpkAddonConflictCheckSettings => VpkAddonConflictCheckSettings.Default;
    }

    private AddonRoot CreateRoot(GameMapsClient client, string name) => new()
    {
        DirectoryPath = Path.Join(_directory, name),
        TaskScheduler = TaskScheduler.Default,
        GameMapsClient = client
    };

    private static byte[] CreateZip(params (string Name, byte[] Bytes)[] entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
            foreach (var (name, bytes) in entries)
            {
                using var entry = archive.CreateEntry(name).Open();
                entry.Write(bytes);
            }
        return stream.ToArray();
    }

    private static HttpResponseMessage Html(string html) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(html, System.Text.Encoding.UTF8, "text/html")
    };

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            => Task.FromResult(send(request));
    }

    private sealed class GameMapsHandler(byte[] payload) : HttpMessageHandler
    {
        public byte[] Payload = payload;
        public string Version = "1.0";
        public string UpdatedAt = "2026-11-02T21:20:00";
        public string PageNoise = "1 review";
        public int PageRequests;
        public int Posts;
        public int Downloads;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            switch (request.RequestUri!.AbsolutePath)
            {
                case "/details/24878":
                    PageRequests++;
                    return Html($"""
                        <h1>Test &amp; Campaign</h1>
                        <form action='/downloads/download' method='post'>
                          <input name='ids[]' value='24878'><input value='true' name='noqueue'>
                          <input value='true' name='direct'><input name='csrf' value='token&amp;value'>
                        </form>
                        <section class='details'>
                          <h2>File Details</h2>
                          <div class='info-card'><div><p title='campaign.zip'>campaign.zip</p>
                            <p><time datetime='{UpdatedAt}'>November 2, 2026</time> (37.2 MB)</p></div></div>
                          <div class='info-card interactive changelog'><div><p>View Changelog</p><p>Version {Version} Final</p></div></div>
                        </section>
                        <section class='reviews'>{PageNoise}</section>
                        """);
                case "/downloads/download":
                    Assert.Equal(HttpMethod.Post, request.Method);
                    Assert.Equal("https://www.gamemaps.com/details/24878", request.Headers.Referrer!.AbsoluteUri);
                    string body = await request.Content!.ReadAsStringAsync(token);
                    Assert.Contains("ids%5B%5D=24878", body);
                    Assert.Contains("direct=true", body);
                    Assert.Contains("noqueue=true", body);
                    Assert.Contains("csrf=token%26value", body);
                    var response = new HttpResponseMessage(HttpStatusCode.Found);
                    response.Headers.Location = new Uri($"/download/current?s=signature-{++Posts}", UriKind.Relative);
                    return response;
                case "/download/current":
                    Downloads++;
                    var download = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Payload) };
                    download.Content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
                    return download;
                default:
                    throw new InvalidOperationException(request.RequestUri.AbsoluteUri);
            }
        }
    }

    public void Dispose() => Directory.Delete(_directory, true);
}
