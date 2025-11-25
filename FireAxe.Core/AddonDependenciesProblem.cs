using System;

namespace FireAxe;

public class AddonDependenciesProblem : AddonProblem
{
    public AddonDependenciesProblem(AddonProblemSource problemSource) : base(problemSource)
    {

    }

    public override bool CanAutomaticallyFix => true;

    public override bool ShouldFixAutomatically => true;

    protected override bool OnAutomaticallyFix()
    {
        var addon = Addon;

        if (!addon.IsEnabledInHierarchy)
        {
            return true;
        }
        addon.EnableAllDependencies();
        return addon.IsDependenciesAllEnabled;
    }
}