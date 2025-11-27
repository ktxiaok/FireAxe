using System;
using System.Diagnostics.CodeAnalysis;

namespace FireAxe;

public class AddonGroupSave : AddonNodeSave
{
    public override Type TargetType => typeof(AddonGroup);

    [AllowNull]
    public AddonNodeSave[] Children { get; set => field = value.EliminateNull(); } = [];

    public AddonGroupEnableStrategy EnableStrategy { get; set; } = AddonGroupEnableStrategy.None;
}
