using System;

namespace FireAxe;

public class AddonNodeJumpMessage
{
    public AddonNodeJumpMessage(AddonNode target)
    {
        ArgumentNullException.ThrowIfNull(target);

        Target = target;
    }
    
    public AddonNode Target { get; }
}
