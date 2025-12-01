using System;
using System.Collections.Generic;

namespace FireAxe;

public class AddonGroupEnableStrategyProblem : AddonProblem
{
    public AddonGroupEnableStrategyProblem(AddonGroup group) : base(group)
    {

    }

    public new AddonGroup Addon => (AddonGroup)base.Addon;

    public static AddonGroupEnableStrategyProblem? TryCreate(AddonGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);

        if (HasProblem(group))
        {
            return new AddonGroupEnableStrategyProblem(group);
        }
        return null;
    }

    public static bool HasProblem(AddonGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);

        switch (group.EnableStrategy)
        {
            case AddonGroupEnableStrategy.Single:
            case AddonGroupEnableStrategy.SingleRandom:
                {
                    int enabledCount = 0;
                    foreach (var child in group.Children)
                    {
                        if (child.IsEnabled)
                        {
                            enabledCount++;
                        }
                        if (enabledCount > 1)
                        {
                            return true;
                        }
                    }
                    break;
                }
            case AddonGroupEnableStrategy.All:
                {
                    int enabledCount = 0;
                    foreach (var child in group.Children)
                    {
                        if (child.IsEnabled)
                        {
                            ++enabledCount;
                        }
                    }
                    if (group.IsEnabled)
                    {
                        ++enabledCount;
                    }
                    if (enabledCount != 0 && enabledCount != group.Children.Count + 1)
                    {
                        return true;
                    }
                    break;
                }
        }
        return false;
    }

    protected override bool OnAutomaticallyFix()
    {
        var group = Addon;
        switch (group.EnableStrategy)
        {
            case AddonGroupEnableStrategy.Single:
            case AddonGroupEnableStrategy.SingleRandom:
                {
                    foreach (var child in group.Children)
                    {
                        child.IsEnabled = false;
                    }
                    break;
                }
            case AddonGroupEnableStrategy.All:
                {
                    bool enabled = group.IsEnabled;
                    foreach (var child in group.Children)
                    {
                        child.IsEnabled = enabled;
                    }
                    break;
                }
        }
        return !HasProblem(group);
    }
}
