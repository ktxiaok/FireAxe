using System;
using System.Reactive.Disposables;
using ReactiveUI;

namespace FireAxe.ViewModels;

public class AddonNodeViewModel : AddonNodeSimpleViewModel
{
    public AddonNodeViewModel(AddonNode addon) : base(addon)
    {

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

    public static AddonNodeViewModel Create(AddonNode addonNode)
    {
        ArgumentNullException.ThrowIfNull(addonNode);

        if (addonNode is AddonGroup group)
        {
            return new AddonGroupViewModel(group);
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
