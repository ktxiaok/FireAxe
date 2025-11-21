using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;

namespace FireAxe.ViewModels;

public class RefAddonNodeViewModel : AddonNodeViewModel
{
    private AddonNodeSimpleViewModel? _sourceViewModel = null;

    public RefAddonNodeViewModel(RefAddonNode addon) : base(addon)
    {

    }

    public override Type AddonType => typeof(RefAddonNode);

    public new RefAddonNode? Addon => (RefAddonNode?)base.Addon;

    public AddonNodeSimpleViewModel? SourceViewModel
    {
        get => _sourceViewModel;
        private set => this.RaiseAndSetIfChanged(ref _sourceViewModel, value);
    }

    protected override void OnNewAddon(AddonNode addon0, CompositeDisposable disposables)
    {
        base.OnNewAddon(addon0, disposables);

        var addon = (RefAddonNode)addon0;
        addon.WhenAnyValue(x => x.SourceAddonId)
            .Select(sourceId => sourceId == Guid.Empty ? null : new AddonNodeSimpleViewModel(addon.Root, sourceId))
            .Subscribe(sourceViewModel => SourceViewModel = sourceViewModel)
            .DisposeWith(disposables);
    }

    protected override void OnNullAddon()
    {
        base.OnNullAddon();

        SourceViewModel = null;
    }
}