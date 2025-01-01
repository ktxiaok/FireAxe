using Newtonsoft.Json;
using System;

namespace FireAxe
{
    public class PublishedFileDetails
    {
        [JsonProperty("file_url")]
        public required string FileUrl { get; init; }

        [JsonProperty("preview_url")]
        public required string PreviewUrl { get; init; }

        [JsonProperty("title")]
        public required string Title { get; init; }

        [JsonProperty("description")]
        public required string Description { get; init; }

        [JsonProperty("time_created")]
        public required ulong TimeCreated { get; init; }

        [JsonProperty("time_updated")]
        public required ulong TimeUpdated { get; init; }

        [JsonProperty("subscriptions")]
        public required uint Subscriptions { get; init; }

        [JsonProperty("favorited")]
        public required uint Favorited { get; init; }

        [JsonProperty("lifetime_subscriptions")]
        public required uint LifetimeSubscriptions { get; init; }

        [JsonProperty("lifetime_favorited")]
        public required uint LifetimeFavorited { get; init; }

        [JsonProperty("views")]
        public required uint Views { get; init; }
    }
}
