using Newtonsoft.Json;
using System;

namespace FireAxe;

public class AddonNodeSave
{
    [JsonIgnore]
    public virtual Type TargetType => typeof(AddonNode);

    public Guid Id { get; set; } = Guid.Empty; 

    public bool IsEnabled { get; set; } = false;

    public string Name { get; set; } = "";

    public int Priority { get; set; } = 0; 

    public DateTime CreationTime { get; set; }

    public string[] Tags { get; set; } = [];

    public Guid[] DependentAddonIds { get; set; } = [];

    public string? CustomImagePath { get; set; } = null;
}
