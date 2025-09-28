using Avalonia.Media.Imaging;
using ReactiveUI;
using System;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;

namespace FireAxe.ViewModels;

public abstract class VpkAddonViewModel : AddonNodeViewModel
{
    private VpkAddonInfo? _info = null;

    public VpkAddonViewModel(VpkAddon addon) : base(addon)
    {
        
    }

    public new VpkAddon? Addon => (VpkAddon?)base.Addon;

    public override Type AddonType => typeof(VpkAddon);

    public VpkAddonInfo? Info
    {
        get => _info;
        private set => this.RaiseAndSetIfChanged(ref _info, value);
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
