using ReactiveUI;
using System;
using System.Reactive.Disposables;

namespace FireAxe.ViewModels
{
    public class FlatVpkAddonViewModel : AddonNodeSimpleViewModel
    {
        private FlatVpkAddonListViewModel _listViewModel;

        public FlatVpkAddonViewModel(VpkAddon addon, FlatVpkAddonListViewModel listViewModel) : base(addon)
        {
            ArgumentNullException.ThrowIfNull(listViewModel);
            _listViewModel = listViewModel;

            this.WhenActivated((CompositeDisposable disposables) =>
            {
                bool first = true;
                addon.WhenAnyValue(x => x.VpkPriority)
                .Subscribe(_ => 
                { 
                    this.RaisePropertyChanged(nameof(Priority));
                    if (!first) 
                    { 
                        listViewModel.RequestSoftRefresh(); 
                    }
                    first = false;
                })
                .DisposeWith(disposables);
            });
        }

        public new VpkAddon AddonNode => (VpkAddon)base.AddonNode;

        public string Priority
        {
            get => AddonNode.VpkPriority.ToString();
            set
            {
                if (int.TryParse(value, out int priority))
                {
                    AddonNode.VpkPriority = priority;
                }
            }
        }

        public void TurnUpPriority()
        {
            AddonNode.VpkPriority++;
        }

        public void TurnDownPriority()
        {
            AddonNode.VpkPriority--;
        }
    }
}
