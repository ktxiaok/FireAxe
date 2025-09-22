using System;

namespace FireAxe.ViewModels
{
    public class AddonNodeViewModel : AddonNodeSimpleViewModel
    {
        public AddonNodeViewModel(AddonNode addon) : base(addon)
        {

        }

        public static AddonNodeViewModel Create(AddonNode addonNode)
        {
            ArgumentNullException.ThrowIfNull(addonNode);

            if (addonNode is AddonGroup group)
            {
                return new AddonGroupViewModel(group);
            }
            else if (addonNode is LocalVpkAddon localVpkAddon)
            {
                return new LocalVpkAddonViewModel(localVpkAddon);
            }
            else if (addonNode is WorkshopVpkAddon workshopVpkAddon)
            {
                return new WorkshopVpkAddonViewModel(workshopVpkAddon);
            }
            else
            {
                return new AddonNodeViewModel(addonNode);
            }
        }
    }
}
