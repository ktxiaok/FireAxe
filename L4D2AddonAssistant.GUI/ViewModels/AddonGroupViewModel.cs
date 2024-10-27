using System;

namespace L4D2AddonAssistant.ViewModels
{
    public class AddonGroupViewModel : AddonNodeViewModel
    {
        public AddonGroupViewModel(AddonGroup group) : base(group)
        {

        }

        public new AddonGroup AddonNode => (AddonGroup)base.AddonNode;
    }
}
