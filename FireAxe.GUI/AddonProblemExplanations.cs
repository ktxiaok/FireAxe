using System;
using FireAxe.Resources;

namespace FireAxe;

internal static class AddonProblemExplanations
{
    public static void Register(ObjectExplanationManager manager)
    {
        manager.Register<AddonFileNotExistProblem>((problem, arg) => Texts.AddonFileNotExistProblemExplain.FormatNoThrow(problem.FilePath));
        manager.Register<AddonChildrenProblem>((problem, arg) => Texts.AddonChildProblemExplain);
        manager.Register<InvalidPublishedFileIdProblem>((problem, arg) => Texts.InvalidPublishedFileIdMessage);
        manager.Register<WorkshopVpkNotLoadProblem>((problem, arg) => Texts.WorkshopVpkFileNotLoadProblemExplain);
        manager.Register<AddonGroup.EnableStrategyProblem>((problem, arg) => Texts.AddonGroupEnableStrategyProblemExplain);
        manager.Register<VpkAddonConflictProblem>((problem, arg) => Texts.VpkAddonConflictProblemExplain);
        manager.Register<AddonDependenciesProblem>((problem, arg) => Texts.AddonDependenciesProblemExplain);
        manager.Register<AddonCircularRefProblem>((problem, arg) => Texts.AddonCircularRefProblemExplain);
        manager.Register<AddonDownloadFailedProblem>((problem, arg) => Texts.AddonDownloadFailedProblemExplain.FormatNoThrow(problem.Exception?.GetType().Name ?? "null", problem.FilePath, problem.Url));
    }
}
