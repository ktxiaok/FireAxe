using System;

namespace FireAxe
{
    public class LocalVpkAddonSave : VpkAddonSave
    {
        public override Type TargetType => typeof(LocalVpkAddon);
    }
}
