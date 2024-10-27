using System;

namespace L4D2AddonAssistant
{
    public class LocalVpkAddonSave : VpkAddonSave
    {
        public override Type TargetType => typeof(LocalVpkAddon);

        public Guid VpkGuid { get; set; } = Guid.Empty;
    }
}
