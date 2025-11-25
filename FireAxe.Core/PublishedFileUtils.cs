using System;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace FireAxe;

public static class PublishedFileUtils
{
    private static readonly Regex s_publishedFileIdLinkRegex = new(@"steamcommunity\.com/(?:sharedfiles|workshop)/filedetails/\?.*id=(\d+)");

    public static async Task<GetPublishedFileDetailsResult> GetPublishedFileDetailsAsync(ulong publishedFileId, HttpClient httpClient, CancellationToken cancellationToken)
    {
        PublishedFileDetails? content = null;
        GetPublishedFileDetailsResultStatus status = GetPublishedFileDetailsResultStatus.Failed;

        try
        {
            using var postContent = new StringContent($"itemcount=1&publishedfileids[0]={publishedFileId}", Encoding.UTF8, "application/x-www-form-urlencoded");
            using var response = await httpClient.PostAsync("https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/", postContent, cancellationToken).ConfigureAwait(false);
            var responseContentStr = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var json = JObject.Parse(responseContentStr);
            if (json.TryGetValue("response", out var responseToken) && responseToken is JObject responseObj)
            {
                if (responseObj.TryGetValue("publishedfiledetails", out var detailsToken) && detailsToken is JArray detailsArray)
                {
                    if (detailsArray.Count == 1)
                    {
                        var elementToken = detailsArray[0];
                        if (elementToken is JObject element)
                        {
                            if (element.TryGetValue("result", out var resultTypeToken) && resultTypeToken.Type == JTokenType.Integer)
                            {
                                int resultType = (int)resultTypeToken;
                                if (resultType == 1)
                                {
                                    if (element.TryGetValue("consumer_app_id", out var consumerAppIdToken) && consumerAppIdToken.Type == JTokenType.Integer && (int)consumerAppIdToken == 550)
                                    {
                                        content = element.ToObject<PublishedFileDetails>();
                                    }
                                    else
                                    {
                                        status = GetPublishedFileDetailsResultStatus.InvalidPublishedFileId;
                                    }

                                }
                                else if (resultType == 9)
                                {
                                    status = GetPublishedFileDetailsResultStatus.InvalidPublishedFileId;
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, $"Exception occurred during {nameof(PublishedFileUtils)}.{nameof(GetPublishedFileDetailsAsync)}.");
        }

        if (content != null)
        {
            status = GetPublishedFileDetailsResultStatus.Succeeded;
        }
        return new GetPublishedFileDetailsResult(content, status);
    }

    public static bool TryParsePublishedFileId(string input, out ulong id)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (ulong.TryParse(input, out id))
        {
            return true;
        }

        if (TryParsePublishedFileIdLink(input, out id))
        {
            return true;
        }

        return false;
    }

    public static bool TryParsePublishedFileIdLink(string input, out ulong id)
    {
        ArgumentNullException.ThrowIfNull(input);

        id = 0;

        var match = s_publishedFileIdLinkRegex.Match(input);
        if (match.Success)
        {
            if (ulong.TryParse(match.Groups[1].ValueSpan, out id))
            {
                return true;
            }
        }

        return false;
    }
}

public class GetPublishedFileDetailsResult
{
    private PublishedFileDetails? _content;
    private GetPublishedFileDetailsResultStatus _status;

    internal GetPublishedFileDetailsResult(PublishedFileDetails? content, GetPublishedFileDetailsResultStatus status)
    {
        if (status == GetPublishedFileDetailsResultStatus.Succeeded && content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        _content = content;
        _status = status;
    }

    public PublishedFileDetails Content
    {
        get
        {
            if (!IsSucceeded)
            {
                throw new InvalidOperationException("The status isn't succeeded.");
            }
            return _content!;
        }
    }

    public GetPublishedFileDetailsResultStatus Status => _status;

    public bool IsSucceeded => _status == GetPublishedFileDetailsResultStatus.Succeeded;
}

public enum GetPublishedFileDetailsResultStatus
{
    Succeeded,
    Failed,
    InvalidPublishedFileId,
}
