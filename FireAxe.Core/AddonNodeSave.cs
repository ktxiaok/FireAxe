using Newtonsoft.Json;
using System;
using System.Diagnostics.CodeAnalysis;

namespace FireAxe;

public class AddonNodeSave
{
    [JsonIgnore]
    public virtual Type TargetType => typeof(AddonNode);

    public Guid Id { get; set; } = Guid.Empty; 

    public bool IsEnabled { get; set; } = false;

    [AllowNull]
    public string Name { get; set => field = value ?? ""; } = "";

    public int Priority { get; set; } = 0; 

    public DateTime CreationTime { get; set; }

    [AllowNull]
    public string[] Tags { get; set => field = value.EliminateNull(); } = [];

    [AllowNull]
    public Guid[] DependentAddonIds { get; set => field = value ?? []; } = [];

    public string? CustomImagePath { get; set; } = null;
}
