using System;

namespace FireAxe;

public class AddonInvalidRefSourceProblem(RefAddonNode addon) : AddonProblem(addon)
{
    public new RefAddonNode Addon => (RefAddonNode)base.Addon;
}
