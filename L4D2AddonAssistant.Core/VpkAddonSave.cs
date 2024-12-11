using System;

namespace L4D2AddonAssistant
{
    public class VpkAddonSave : AddonNodeSave 
    {
        public override Type TargetType => typeof(VpkAddon);

        public int VpkPriority { get; set; } = 0;
    }
}
