using System;
using System.Collections.Generic;

namespace FireAxe.ViewModels;

public class ConflictingVpkAddonWithFilesViewModel : ViewModelBase
{
    public ConflictingVpkAddonWithFilesViewModel(AddonRoot addonRoot, Guid addonId, IEnumerable<string> files)
    {
        ArgumentNullException.ThrowIfNull(addonRoot);
        ArgumentNullException.ThrowIfNull(files);

        AddonViewModel = new AddonNodeSimpleViewModel(addonRoot, addonId);
        Files = files;
    }

    public AddonNodeSimpleViewModel AddonViewModel { get; }

    public IEnumerable<string> Files { get; }
}