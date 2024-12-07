using System;
using L4D2AddonAssistant.Resources;

namespace L4D2AddonAssistant
{
    internal static class AddonProblemExplanations
    {
        public static void Register(ObjectExplanationManager manager)
        {
            manager.Register<AddonFileNotExistProblem>((problem, arg) => string.Format(Texts.AddonFileNotExistProblemExplain, problem.FilePath));
            manager.Register<AddonChildProblem>((problem, arg) => Texts.AddonChildProblemExplain);
            manager.Register<InvalidPublishedFileIdProblem>((problem, arg) => Texts.InvalidPublishedFileIdMessage);
            manager.Register<WorkshopVpkFileNotLoadProblem>((problem, arg) => Texts.WorkshopVpkFileNotLoadProblemExplain);
        }
    }
}
