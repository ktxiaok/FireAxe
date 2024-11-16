using System;

namespace L4D2AddonAssistant
{
    public class AddonNodeMoveDeniedException : Exception
    {
        public AddonNode AddonNode { get; }

        public AddonNodeMoveDeniedException(AddonNode addonNode)
        {
            ArgumentNullException.ThrowIfNull(addonNode);
            AddonNode = addonNode;
        }
    }
}
