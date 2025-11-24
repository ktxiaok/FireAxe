using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace FireAxe;

public static class AppGlobal
{
    public const string GitHubApiUrl = "https://api.github.com/repos/ktxiaok/FireAxe";

    public const string GithubReleasesUrl = "https://github.com/ktxiaok/FireAxe/releases";

    public const string GithubRepoLink = "https://github.com/ktxiaok/FireAxe";

    public const string License = "Apache-2.0 license";

    public static Version Version => typeof(AppGlobal).Assembly.GetName().Version!;

    public static string VersionString => Version.ToString(3);

    public const string ExportedAssetsDirectoryName = "ExportedAssets";

    public static async Task<string?> GetLatestVersionAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        var url = GitHubApiUrl + "/releases/latest";
        try
        {
            using var response = await httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var jobj = JObject.Parse(responseJson);
            if (jobj.TryGetValue("tag_name", out var tagNameToken))
            {
                return (string?)tagNameToken;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred during AppGlobal.GetLatestVersionAsync");
        }
        return null;
    }
}
