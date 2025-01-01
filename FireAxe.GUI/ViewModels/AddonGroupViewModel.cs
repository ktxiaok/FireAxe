using System;

namespace FireAxe.ViewModels
{
    public class AddonGroupViewModel : AddonNodeViewModel
    {
        public AddonGroupViewModel(AddonGroup group) : base(group)
        {

        }

        public new AddonGroup AddonNode => (AddonGroup)base.AddonNode;
    }
}
