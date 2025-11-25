using System;

namespace FireAxe.ViewModels;

public class AddonGroupViewModel : AddonNodeViewModel
{
    public AddonGroupViewModel(AddonGroup group) : base(group)
    {

    }

    public new AddonGroup? Addon => (AddonGroup?)base.Addon;

    public override Type AddonType => typeof(AddonGroup);
}
