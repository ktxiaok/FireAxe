using System;
namespace FireAxe.ViewModels;

public class LocalVpkAddonViewModel : VpkAddonViewModel
{
    public LocalVpkAddonViewModel(LocalVpkAddon addon) : base(addon)
    {

    }

    public override Type AddonType => typeof(LocalVpkAddon);
}
