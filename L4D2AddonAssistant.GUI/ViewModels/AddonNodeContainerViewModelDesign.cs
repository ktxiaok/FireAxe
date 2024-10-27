using DynamicData;
using DynamicData.Alias;
using DynamicData.Binding;
using System;
using System.Collections.ObjectModel;

namespace L4D2AddonAssistant.ViewModels
{
    public class AddonNodeContainerViewModelDesign : AddonNodeContainerViewModel
    {
        private AddonRoot _root;

        public AddonNodeContainerViewModelDesign()
        {
            _root = new();
            DesignHelper.AddTestAddonNodes(_root);
            Nodes = _root.Nodes;
        }
    }
}
