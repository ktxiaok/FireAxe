using System;

namespace FireAxe;

public class VpkAddonSave : AddonNodeSave 
{
    public override Type TargetType => typeof(VpkAddon);

    // reserved for backward compatibility
    public int VpkPriority
    {
        set => Priority = value;
    }

    public string[] ConflictIgnoringFiles { get; set; } = [];
}
