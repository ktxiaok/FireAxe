using Newtonsoft.Json;
using System;

namespace L4D2AddonAssistant
{
    public class AddonNodeSave
    {
        [JsonIgnore]
        public virtual Type TargetType => typeof(AddonNode);

        public bool IsEnabled { get; set; } = false;

        public string Name { get; set; } = "";
    }
}
