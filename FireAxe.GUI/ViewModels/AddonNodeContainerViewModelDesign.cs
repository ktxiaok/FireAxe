using DynamicData;
using DynamicData.Alias;
using DynamicData.Binding;
using System;
using System.Collections.ObjectModel;

namespace FireAxe.ViewModels;

public class AddonNodeContainerViewModelDesign : AddonNodeContainerViewModel
{
    private AddonRoot _root;

    public AddonNodeContainerViewModelDesign()
    {
        _root = DesignHelper.CreateEmptyAddonRoot();
        DesignHelper.AddTestAddonNodes(_root);
        Nodes = _root.Nodes;
    }
}
