using System;

namespace L4D2AddonAssistant.ViewModels
{
    public class AddonNodeViewModel : AddonNodeSimpleViewModel
    {
        public AddonNodeViewModel(AddonNode addonNode) : base(addonNode)
        {

        }

        public static AddonNodeViewModel Create(AddonNode addonNode)
        {
            if (addonNode is AddonGroup group)
            {
                return new AddonGroupViewModel(group);
            }
            else if (addonNode is LocalVpkAddon localVpkAddon)
            {
                return new LocalVpkAddonViewModel(localVpkAddon);
            }
            else
            {
                return new AddonNodeViewModel(addonNode);
            }
        }
    }
}
