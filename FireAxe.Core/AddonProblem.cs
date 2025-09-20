using System;

namespace FireAxe;

public abstract class AddonProblem : Problem
{
    public AddonProblem(AddonProblemSource problemSource) : base(problemSource)
    {
        Addon = problemSource.Addon; 
    }

    public AddonNode Addon { get; }
}