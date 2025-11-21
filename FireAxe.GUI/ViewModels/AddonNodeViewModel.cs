using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;

namespace FireAxe.ViewModels;

public class AddonNodeViewModel : AddonNodeSimpleViewModel
{
    private readonly ObservableAsPropertyHelper<AddonNodeDependenciesViewModel?> _dependenciesViewModel;

    public AddonNodeViewModel(AddonNode addon) : base(addon)
    {
        _dependenciesViewModel = this.WhenAnyValue(x => x.Addon)
            .Select(addon => addon is null ? null : new AddonNodeDependenciesViewModel(addon))
            .ToProperty(this, nameof(DependenciesViewModel), deferSubscription: true);
    }

    public string? Priority
    {
        get => Addon?.Priority.ToString();
        set
        {
            if (!int.TryParse(value, out int priority))
            {
                throw new ArgumentException($"{nameof(Priority)} must be a integer.");
            }

            var addon = Addon;
            if (addon == null)
            {
                return;
            }

            addon.Priority = priority;
        }
    }

    public AddonNodeDependenciesViewModel? DependenciesViewModel => _dependenciesViewModel.Value;

    public static AddonNodeViewModel Create(AddonNode addonNode)
    {
        ArgumentNullException.ThrowIfNull(addonNode);

        if (addonNode is AddonGroup group)
        {
            return new AddonGroupViewModel(group);
        }
        else if (addonNode is RefAddonNode refAddon)
        {
            return new RefAddonNodeViewModel(refAddon);
        }
        else if (addonNode is LocalVpkAddon localVpkAddon)
        {
            return new LocalVpkAddonViewModel(localVpkAddon);
        }
        else if (addonNode is WorkshopVpkAddon workshopVpkAddon)
        {
            return new WorkshopVpkAddonViewModel(workshopVpkAddon);
        }
        else
        {
            return new AddonNodeViewModel(addonNode);
        }
    }

    protected override void OnNewAddon(AddonNode addon, CompositeDisposable disposables)
    {
        base.OnNewAddon(addon, disposables);

        addon.WhenAnyValue(x => x.Priority)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(Priority)))
            .DisposeWith(disposables);
    }

    protected override void OnNullAddon()
    {
        base.OnNullAddon();

        this.RaisePropertyChanged(nameof(Priority));
    }
}
