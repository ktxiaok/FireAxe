using System;
using ReactiveUI;

namespace FireAxe.ViewModels;

public class AddonNodeRefViewModel : ViewModelBase, IActivatableViewModel
{
    public AddonNodeRefViewModel(Guid addonId, AddonRoot addonRoot)
    {
        ArgumentNullException.ThrowIfNull(addonRoot);
        AddonId = addonId;
        AddonRoot = addonRoot;


    }

    public ViewModelActivator Activator { get; } = new();

    public Guid AddonId { get; }

    public AddonRoot AddonRoot { get; }
}
