using Newtonsoft.Json;
using System;

namespace FireAxe
{
    public class AddonNodeSave
    {
        [JsonIgnore]
        public virtual Type TargetType => typeof(AddonNode);

        public Guid? Id { get; set; } = null; 

        public bool IsEnabled { get; set; } = false;

        public string Name { get; set; } = "";

        public DateTime CreationTime { get; set; }

        public string[] Tags { get; set; } = [];

        public string? CustomImagePath { get; set; } = null;
    }
}
