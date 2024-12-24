using System;
using System.Collections.Generic;

namespace L4D2AddonAssistant.ViewModels
{
    public class AddAddonTagViewModel : ViewModelBase
    {
        private string[] _existingTags;

        public AddAddonTagViewModel(AddonRoot addonRoot)
        {
            ArgumentNullException.ThrowIfNull(addonRoot);

            _existingTags = [.. AddonTags.BuiltInTags, .. addonRoot.CustomTags];
        }

        public IEnumerable<string> ExistingTags => _existingTags;
    }
}
