using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Text;

namespace L4D2AddonAssistant
{
    public static class WorkshopCollectionUtils
    {
        public static async Task<ulong[]?> GetWorkshopCollectionContentAsync(ulong collectionId, bool includeLinkedCollections, HttpClient httpClient, CancellationToken cancellationToken)
        {
            if (!includeLinkedCollections)
            {
                var results = await GetRaw(collectionId).ConfigureAwait(false);
                if (results == null)
                {
                    return null;
                }
                return results.Select(obj => obj.Id).ToArray();
            }

            var resultIds = new List<ulong>();
            var queue = new Queue<(ulong Id, bool IsCollection)>();
            queue.Enqueue((collectionId, true));
            while (queue.Count > 0)
            {
                var next = queue.Dequeue();
                if (next.IsCollection)
                {
                    var results = await GetRaw(next.Id).ConfigureAwait(false);
                    if (results == null)
                    {
                        return null;
                    }
                    foreach (var item in results)
                    {
                        queue.Enqueue(item);
                    }
                }
                else
                {
                    resultIds.Add(next.Id);
                }
            }

            return resultIds.ToArray();

            async Task<List<(ulong Id, bool IsCollection)>?> GetRaw(ulong collectionId)
            {
                try
                {
                    var postContent = new StringContent($"collectioncount=1&publishedfileids[0]={collectionId}", Encoding.UTF8, "application/x-www-form-urlencoded");
                    var response = await httpClient.PostAsync("https://api.steampowered.com/ISteamRemoteStorage/GetCollectionDetails/v1/", postContent, cancellationToken).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    var responseJson = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    var jobj = JObject.Parse(responseJson);
                    var responseToken = jobj["response"]!;
                    if ((int)responseToken["result"]! == 1)
                    {
                        var collectiondetailsToken = responseToken["collectiondetails"]![0]!;
                        if ((int)collectiondetailsToken["result"]! == 1)
                        {
                            var childrenToken = collectiondetailsToken["children"]!;
                            var results = new List<(ulong Id, bool IsCollection)>();
                            foreach (var childToken in childrenToken)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                int fileType = (int)childToken["filetype"]!;
                                if (fileType != 0 && fileType != 2)
                                {
                                    continue;
                                }
                                bool isCollection = fileType == 2;
                                if (isCollection && !includeLinkedCollections)
                                {
                                    continue;
                                }
                                ulong id = (ulong)childToken["publishedfileid"]!;
                                results.Add((id, isCollection));
                            }
                            
                            return results;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Exception occurred during WorkshopCollectionUtils.GetWorkshopCollectionContentAsync");
                }

                return null;
            }
        }
    }
}
