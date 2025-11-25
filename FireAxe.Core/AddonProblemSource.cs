using System;

namespace FireAxe;

public class AddonProblemSource : ProblemSource
{
    public AddonProblemSource(AddonNode addon)
        : base(problem => addon.AddProblem((AddonProblem)problem), problem => addon.RemoveProblem((AddonProblem)problem))
    {
        ArgumentNullException.ThrowIfNull(addon);
        Addon = addon;
    }

    public new AddonProblem? Problem => (AddonProblem?)base.Problem;

    public AddonNode Addon { get; }
}

public class AddonProblemSource<TAddon> : AddonProblemSource where TAddon : AddonNode
{
    public AddonProblemSource(TAddon addon) : base(addon)
    {

    }

    public new TAddon Addon => (TAddon)base.Addon; 
}