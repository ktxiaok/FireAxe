using System;

namespace FireAxe.ViewModels
{
    public class AddonNodeListItemViewModel : AddonNodeSimpleViewModel
    {
        private readonly AddonNodeContainerViewModel? _containerViewModel;

        public AddonNodeListItemViewModel(AddonNode addonNode, AddonNodeContainerViewModel? containerViewModel) : base(addonNode)
        {
            _containerViewModel = containerViewModel;
        }

        public AddonNodeContainerViewModel? ContainerViewModel => _containerViewModel;
    }
}
