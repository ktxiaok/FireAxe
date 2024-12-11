using Avalonia.Media.Imaging;
using ReactiveUI;
using System;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace L4D2AddonAssistant.ViewModels
{
    public abstract class VpkAddonViewModel : AddonNodeViewModel
    {
        private VpkAddonInfo? _info = null;

        public VpkAddonViewModel(VpkAddon addon) : base(addon)
        {
            this.WhenActivated((CompositeDisposable disposables) =>
            {
                addon.WhenAnyValue(x => x.VpkPriority)
                .Subscribe(priority => this.RaisePropertyChanged(nameof(VpkPriority)))
                .DisposeWith(disposables);
            });
        }

        public new VpkAddon AddonNode => (VpkAddon)base.AddonNode;

        public string VpkPriority
        {
            get => AddonNode.VpkPriority.ToString();
            set
            {
                if (int.TryParse(value, out int priority))
                {
                    AddonNode.VpkPriority = priority;
                }
                else
                {
                    throw new ArgumentException();
                }
            }
        }

        public VpkAddonInfo? Info
        {
            get => _info;
            private set => this.RaiseAndSetIfChanged(ref _info, value);
        }

        protected override void OnRefresh()
        {
            base.OnRefresh();

            var addon = AddonNode;
            Info = addon.RetrieveInfo();
        }

        protected override void OnClearCaches()
        {
            base.OnClearCaches();

            Info = null;
        }
    }
}
