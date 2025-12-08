using System;

namespace FireAxe;

public class InvalidVpkFileProblem(VpkAddon addon) : AddonProblem(addon)
{
    public new VpkAddon Addon => (VpkAddon)base.Addon;
}
