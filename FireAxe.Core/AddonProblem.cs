using System;

namespace FireAxe;

public abstract class AddonProblem : Problem
{
    private readonly AddonProblemSource _problemSource;

    public AddonProblem(AddonProblemSource problemSource) : base(problemSource)
    {
        _problemSource = problemSource;
    }

    public AddonNode Addon => _problemSource.Addon;
}