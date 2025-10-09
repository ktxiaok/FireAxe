using Avalonia.Media.Imaging;
using ReactiveUI;
using System;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;

namespace FireAxe.ViewModels;

public abstract class VpkAddonViewModel : AddonNodeViewModel
{
    private VpkAddonInfo? _info = null;

    private readonly ObservableAsPropertyHelper<VpkAddonConflictingDetailsViewModel?> _conflictingDetailsViewModel;

    public VpkAddonViewModel(VpkAddon addon) : base(addon)
    {
        _conflictingDetailsViewModel = this.WhenAnyValue(x => x.Addon)
            .Select(addon => addon is null ? null : new VpkAddonConflictingDetailsViewModel(addon))
            .ToProperty(this, nameof(ConflictingDetailsViewModel), deferSubscription: true);
    }

    public new VpkAddon? Addon => (VpkAddon?)base.Addon;

    public override Type AddonType => typeof(VpkAddon);

    public VpkAddonInfo? Info
    {
        get => _info;
        private set => this.RaiseAndSetIfChanged(ref _info, value);
    }

    public VpkAddonConflictingDetailsViewModel? ConflictingDetailsViewModel => _conflictingDetailsViewModel.Value;

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
