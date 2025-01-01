using System;

namespace FireAxe
{
    public class AddonGroupSave : AddonNodeSave
    {
        public override Type TargetType => typeof(AddonGroup);

        public AddonNodeSave[] Children { get; set; } = [];

        public AddonGroupEnableStrategy EnableStrategy { get; set; } = AddonGroupEnableStrategy.None;
    }
}
