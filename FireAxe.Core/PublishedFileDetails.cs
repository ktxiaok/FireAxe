using Newtonsoft.Json;
using System;
using System.Diagnostics.CodeAnalysis;

namespace FireAxe;

public class PublishedFileDetails
{
    public class TagObject
    {
        [JsonProperty("tag")]
        [AllowNull]
        public string Tag { get; init => field = value ?? ""; } = "";
    }

    [JsonProperty("file_url")]
    [AllowNull]
    public string FileUrl { get; init => field = value ?? ""; } = "";

    [JsonProperty("preview_url")]
    [AllowNull]
    public string PreviewUrl { get; init => field = value ?? ""; } = "";

    [JsonProperty("title")]
    [AllowNull]
    public string Title { get; init => field = value ?? ""; } = "";

    [JsonProperty("description")]
    [AllowNull]
    public string Description { get; init => field = value ?? ""; } = "";

    [JsonProperty("time_created")]
    public ulong TimeCreated { get; init; } = 0;

    [JsonProperty("time_updated")]
    public ulong TimeUpdated { get; init; } = 0;

    [JsonProperty("subscriptions")]
    public uint Subscriptions { get; init; } = 0;

    [JsonProperty("favorited")]
    public uint Favorited { get; init; } = 0;

    [JsonProperty("lifetime_subscriptions")]
    public uint LifetimeSubscriptions { get; init; } = 0;

    [JsonProperty("lifetime_favorited")]
    public uint LifetimeFavorited { get; init; } = 0;

    [JsonProperty("views")]
    public uint Views { get; init; } = 0;

    [JsonProperty("tags")]
    public IReadOnlyList<TagObject?>? Tags { get; init; } = null;
}
