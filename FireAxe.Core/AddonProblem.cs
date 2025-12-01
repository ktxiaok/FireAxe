using System;

namespace FireAxe;

public abstract class AddonProblem : Problem
{
    public AddonProblem(AddonNode addon)
    {
        ArgumentNullException.ThrowIfNull(addon);
        Addon = addon;
    }

    public AddonNode Addon { get; }
}