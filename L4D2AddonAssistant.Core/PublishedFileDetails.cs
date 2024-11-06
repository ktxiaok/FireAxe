using Newtonsoft.Json;
using System;

namespace L4D2AddonAssistant
{
    public class PublishedFileDetails
    {
        [JsonProperty("file_url")]
        public string FileUrl { get; set; } = "";

        [JsonProperty("preview_url")]
        public string PreviewUrl { get; set; } = "";

        [JsonProperty("title")]
        public string Title { get; set; } = "";

        [JsonProperty("description")]
        public string Description { get; set; } = "";

        [JsonProperty("time_created")]
        public ulong TimeCreated { get; set; } = 0;

        [JsonProperty("time_updated")]
        public ulong TimeUpdated { get; set; } = 0;

        [JsonProperty("subscriptions")]
        public uint Subscriptions { get; set; } = 0;

        [JsonProperty("favorited")]
        public uint Favorited { get; set; } = 0;

        [JsonProperty("lifetime_subscriptions")]
        public uint LifetimeSubscriptions { get; set; } = 0;

        [JsonProperty("lifetime_favorited")]
        public uint LifetimeFavorited { get; set; } = 0;

        [JsonProperty("views")]
        public uint Views { get; set; } = 0;
    }
}
