using HtmlAgilityPack;
using CurlImpersonate.Http;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace FireAxe;

public sealed class GameMapsClient : IDisposable
{
    private readonly HttpClient _httpClient;

    public GameMapsClient(IWebProxy? proxy = null)
        : this(new HttpClient(new CurlHandler(new CurlHandlerOptions
        {
            Proxy = proxy,
            HeaderPolicy = BrowserHeaderPolicy.AllowUserOverride,
            FollowRedirects = false,
            StreamResponseBodies = true,
            MaxResponseBodyBytes = null
        })))
    {
    }

    public GameMapsClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public static bool TryParseDetailsId(string? value, out ulong id)
    {
        id = 0;
        value = value?.Trim();
        if (ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0)
        {
            return true;
        }
        id = 0;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || (uri.Host != "www.gamemaps.com" && uri.Host != "gamemaps.com")
            || !uri.IsDefaultPort || uri.UserInfo.Length != 0)
        {
            return false;
        }
        var match = Regex.Match(uri.AbsolutePath, @"^/details/([0-9]+)/?$", RegexOptions.CultureInvariant);
        return match.Success && ulong.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0;
    }

    public static string GetDetailsUrl(ulong id) => $"https://www.gamemaps.com/details/{id.ToString(CultureInfo.InvariantCulture)}";

    public async Task<GameMapsDownloadInfo> GetDownloadInfoAsync(ulong id, CancellationToken cancellationToken = default)
    {
        var details = await GetDetailsAsync(id, cancellationToken).ConfigureAwait(false);
        return await GetDownloadInfoAsync(details, cancellationToken).ConfigureAwait(false);
    }

    public async Task<GameMapsItemDetails> GetDetailsAsync(ulong id, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(id);
        var pageUri = new Uri(GetDetailsUrl(id));
        using var pageRequest = new HttpRequestMessage(HttpMethod.Get, pageUri);
        using var pageResponse = await _httpClient.SendAsync(pageRequest, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(pageResponse);
        var html = await pageResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var document = new HtmlDocument();
        document.LoadHtml(html);
        var form = document.DocumentNode.SelectNodes("//form[@action]")?.FirstOrDefault(node =>
            Uri.TryCreate(pageUri, HtmlEntity.DeEntitize(node.GetAttributeValue("action", "")), out var action)
            && action.GetLeftPart(UriPartial.Path) == "https://www.gamemaps.com/downloads/download");
        if (form is null)
        {
            throw new InvalidDataException("GameMaps did not provide a download form. The item may be unavailable or require signing in.");
        }

        var fields = new Dictionary<string, string>();
        foreach (var input in form.SelectNodes(".//input[@name]") ?? Enumerable.Empty<HtmlNode>())
        {
            fields[HtmlEntity.DeEntitize(input.GetAttributeValue("name", ""))] = HtmlEntity.DeEntitize(input.GetAttributeValue("value", ""));
        }
        fields["ids[]"] = id.ToString(CultureInfo.InvariantCulture);
        fields["noqueue"] = "true";
        fields["direct"] = "true";
        var title = HtmlEntity.DeEntitize(document.DocumentNode.SelectSingleNode("//h1")?.InnerText ?? $"GameMaps {id}").Trim();
        var fileDetails = document.DocumentNode.SelectSingleNode("//section[contains(concat(' ', normalize-space(@class), ' '), ' details ')]");
        // Only fingerprint the file details, never expiring download signatures, reviews or view counts.
        string? revision = null;
        if (fileDetails is not null)
        {
            string updated = fileDetails.SelectSingleNode(".//time[@datetime]")?.GetAttributeValue("datetime", "") ?? "";
            string fileName = fileDetails.SelectSingleNode(".//p[@title]")?.GetAttributeValue("title", "") ?? "";
            string text = Regex.Replace(HtmlEntity.DeEntitize(fileDetails.InnerText), @"\s+", " ").Trim();
            revision = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{updated}|{fileName}|{text}")));
        }
        return new(id, title, revision) { DownloadFields = fields };
    }

    public async Task<GameMapsDownloadInfo> GetDownloadInfoAsync(GameMapsItemDetails details, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(details);
        var pageUri = new Uri(GetDetailsUrl(details.DetailsId));
        using var downloadRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(pageUri, "/downloads/download"));
        downloadRequest.Headers.Referrer = pageUri;
        downloadRequest.Headers.Add("Origin", pageUri.GetLeftPart(UriPartial.Authority));
        downloadRequest.Content = new FormUrlEncodedContent(details.DownloadFields);
        // Read the redirect itself: following it here would start downloading the archive twice.
        using var response = await _httpClient.SendAsync(downloadRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Found && response.StatusCode != HttpStatusCode.SeeOther)
        {
            EnsureSuccess(response);
            throw new InvalidDataException("GameMaps did not provide a download redirect.");
        }
        if (response.Headers.Location is not { } location
            || !Uri.TryCreate(pageUri, location, out var downloadUri)
            || downloadUri.Scheme != Uri.UriSchemeHttps || downloadUri.Host != pageUri.Host
            || !downloadUri.AbsolutePath.StartsWith("/download/", StringComparison.Ordinal))
        {
            throw new InvalidDataException("GameMaps returned a web page instead of a mod download. Open the source page to check its availability.");
        }
        return new(details.Title, downloadUri.AbsoluteUri, details.Revision);
    }

    public IDownloadItem Download(string url, string filePath, ulong detailsId)
        => new HttpDownloadItem(_httpClient, url, filePath, GetDetailsUrl(detailsId));

    public void Dispose() => _httpClient.Dispose();

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new HttpRequestException("GameMaps denied the request (HTTP 403 / Cloudflare). Try the configured proxy or open the source page to download and import the VPK manually.", null, response.StatusCode);
        }
        response.EnsureSuccessStatusCode();
    }
}

public sealed record GameMapsItemDetails(ulong DetailsId, string Title, string? Revision)
{
    internal IReadOnlyDictionary<string, string> DownloadFields { get; init; } = new Dictionary<string, string>();
}

public sealed record GameMapsDownloadInfo(string Title, string DownloadUrl, string? Revision = null);
