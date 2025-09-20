using System;

namespace FireAxe
{
    public class InvalidPublishedFileIdProblem : AddonProblem
    {
        public InvalidPublishedFileIdProblem(AddonProblemSource<WorkshopVpkAddon> problemSource) : base(problemSource)
        {

        }

        public new WorkshopVpkAddon Addon => (WorkshopVpkAddon)base.Addon;
    }
}
