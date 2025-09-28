using System;

namespace FireAxe.ViewModels;

public class AddonNodeListItemViewModel : AddonNodeSimpleViewModel
{
    private readonly AddonNodeContainerViewModel? _containerViewModel;

    public AddonNodeListItemViewModel(AddonNode addon, AddonNodeContainerViewModel? containerViewModel) : base(addon)
    {
        _containerViewModel = containerViewModel;
    }

    public AddonNodeContainerViewModel? ContainerViewModel => _containerViewModel;
}
