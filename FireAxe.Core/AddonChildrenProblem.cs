using System;

namespace FireAxe;

public class AddonChildrenProblem : AddonProblem
{
    public AddonChildrenProblem(AddonGroup group) : base(group)
    {

    }

    public new AddonGroup Addon => (AddonGroup)base.Addon;
}
