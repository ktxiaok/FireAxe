using Avalonia.Media.Imaging;
using ReactiveUI;
using System;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;

namespace FireAxe.ViewModels
{
    public abstract class VpkAddonViewModel : AddonNodeViewModel
    {
        private VpkAddonInfo? _info = null;

        public VpkAddonViewModel(VpkAddon addon) : base(addon)
        {
            
        }

        public new VpkAddon? Addon => (VpkAddon?)base.Addon;

        public override Type AddonType => typeof(VpkAddon);

        public string? VpkPriority
        {
            get => Addon?.VpkPriority.ToString();
            set
            {
                if (!int.TryParse(value, out int priority))
                {
                    throw new ArgumentException($"{nameof(VpkPriority)} must be a integer.");
                }

                var addon = Addon;
                if (addon == null)
                {
                    return;
                }

                addon.VpkPriority = priority;
            }
        }

        public VpkAddonInfo? Info
        {
            get => _info;
            private set => this.RaiseAndSetIfChanged(ref _info, value);
        }

        protected override void OnNewAddon(AddonNode addon, CompositeDisposable disposables)
        {
            base.OnNewAddon(addon, disposables);

            var vpkAddon = (VpkAddon)addon;
            vpkAddon.WhenAnyValue(x => x.VpkPriority)
                .Subscribe(_ => this.RaisePropertyChanged(nameof(VpkPriority)))
                .DisposeWith(disposables);
        }

        protected override void OnNullAddon()
        {
            base.OnNullAddonNode();

            this.RaisePropertyChanged(nameof(VpkPriority));
        }

        protected override void OnRefresh(CancellationToken cancellationToken)
        {
            base.OnRefresh(cancellationToken);

            var addon = Addon;

            Info = addon?.RetrieveInfo();
        }

        protected override void OnClearCaches()
        {
            base.OnClearCaches();

            Info = null;
        }
    }
}
