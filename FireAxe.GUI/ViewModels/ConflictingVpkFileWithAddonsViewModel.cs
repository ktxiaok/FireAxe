using System;
using System.Collections.Generic;
using System.Linq;

namespace FireAxe.ViewModels;

public class ConflictingVpkFileWithAddonsViewModel : ViewModelBase
{
    public ConflictingVpkFileWithAddonsViewModel(string file, AddonRoot addonRoot, IEnumerable<Guid> addonIds)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(addonRoot);
        ArgumentNullException.ThrowIfNull(addonIds);

        File = file;
        AddonViewModels = addonIds.Select(id => new AddonNodeSimpleViewModel(addonRoot, id)).ToArray();
    }

    public string File { get; }

    public IEnumerable<AddonNodeSimpleViewModel> AddonViewModels { get; }
}