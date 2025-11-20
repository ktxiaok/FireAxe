using System;

namespace FireAxe.ViewModels;

internal class AddonNodeViewModelDesign : AddonNodeViewModel
{
    public AddonNodeViewModelDesign() : base(DesignHelper.CreateTestAddonNode())
    {
        var addon = Addon!;
        var addonRoot = addon.Root;
        
        // dependencies
        for (int i = 0; i < 3; i++)
        {
            var dependency = AddonNode.Create<AddonNode>(addonRoot);
            dependency.Name = $"test_{DesignHelper.GenerateRandomString(5, 15)}";
            addon.AddDependentAddon(dependency.Id);
        }
    }
}
