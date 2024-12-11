using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace L4D2AddonAssistant
{
    public static class AppGlobal
    {
        public const string GitHubApiUrl = "https://api.github.com/repos/ktxiaok/L4D2AddonAssistant";

        public const string GithubReleasesUrl = "https://github.com/ktxiaok/L4D2AddonAssistant/releases";

        public const string GithubRepoLink = "https://github.com/ktxiaok/L4D2AddonAssistant";

        public const string License = "Apache-2.0 license";

        public static Version Version => typeof(AppGlobal).Assembly.GetName().Version!;

        public static string VersionString => Version.ToString(3);

        public static async Task<string?> GetLatestVersionAsync(HttpClient httpClient, CancellationToken cancellationToken)
        {
            var url = GitHubApiUrl + "/releases/latest";
            try
            {
                var response = await httpClient.GetAsync(url, cancellationToken);
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
}
