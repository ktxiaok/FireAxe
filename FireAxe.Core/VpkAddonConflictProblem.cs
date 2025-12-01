using System;

namespace FireAxe;

public class VpkAddonConflictProblem(VpkAddon addon) : AddonProblem(addon)
{
    public new VpkAddon Addon => (VpkAddon)base.Addon;
}