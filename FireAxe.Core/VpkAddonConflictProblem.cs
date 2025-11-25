using System;

namespace FireAxe;

public class VpkAddonConflictProblem(AddonProblemSource<VpkAddon> problemSource) : AddonProblem(problemSource)
{
    public new VpkAddon Addon => (VpkAddon)base.Addon;
}