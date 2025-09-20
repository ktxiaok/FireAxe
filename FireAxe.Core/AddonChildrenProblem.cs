using System;

namespace FireAxe
{
    public class AddonChildrenProblem : AddonProblem
    {
        public AddonChildrenProblem(AddonProblemSource<AddonGroup> problemSource) : base(problemSource)
        {

        }

        public new AddonGroup Addon => (AddonGroup)base.Addon;
    }
}
