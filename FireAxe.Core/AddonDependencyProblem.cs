using System;

namespace FireAxe;

public class AddonDependencyProblem : AddonProblem
{
    public AddonDependencyProblem(AddonNode addon) : base(addon)
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