using System;
using System.Diagnostics.CodeAnalysis;

namespace FireAxe;

public class VpkAddonSave : AddonNodeSave 
{
    public override Type TargetType => typeof(VpkAddon);

    // reserved for backward compatibility
    public int VpkPriority
    {
        set => Priority = value;
    }

    [AllowNull]
    public string[] ConflictIgnoringFiles { get; set => field = value.EliminateNull(); } = [];
}
