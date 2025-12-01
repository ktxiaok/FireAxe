using System;

namespace FireAxe;

public class WorkshopVpkNotLoadedProblem(WorkshopVpkAddon addon) : AddonProblem(addon)
{
    public new WorkshopVpkAddon Addon => (WorkshopVpkAddon)base.Addon;
}
