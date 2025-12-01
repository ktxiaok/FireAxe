using System;

namespace FireAxe;

public class AddonCircularRefProblem : AddonProblem
{
    public AddonCircularRefProblem(RefAddonNode addon) : base(addon)
    {

    }

    public new RefAddonNode Addon => (RefAddonNode)base.Addon;
}