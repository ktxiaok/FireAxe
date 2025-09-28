using System;

namespace FireAxe;

public class VpkAddonSave : AddonNodeSave 
{
    public override Type TargetType => typeof(VpkAddon);

    public int VpkPriority { get; set; } = 0;

    public string[] ConflictIgnoringFiles { get; set; } = [];
}
