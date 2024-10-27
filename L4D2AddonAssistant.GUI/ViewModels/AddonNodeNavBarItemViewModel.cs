using System;

namespace L4D2AddonAssistant.ViewModels
{
    public class AddonNodeNavBarItemViewModel : ViewModelBase
    {
        private AddonNodeExplorerViewModel _explorerViewModel;

        private AddonGroup _addonGroup;

        public AddonNodeNavBarItemViewModel(AddonNodeExplorerViewModel explorerViewModel, AddonGroup addonGroup)
        {
            ArgumentNullException.ThrowIfNull(explorerViewModel);
            ArgumentNullException.ThrowIfNull(addonGroup);

            _explorerViewModel = explorerViewModel;
            _addonGroup = addonGroup;
        }

        public AddonGroup AddonGroup => _addonGroup;

        public void Goto()
        {
            _explorerViewModel.GotoGroup(_addonGroup);
        }
    }
}
