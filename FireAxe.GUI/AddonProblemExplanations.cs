using System;
using FireAxe.Resources;

namespace FireAxe;

internal static class AddonProblemExplanations
{
    public static void Register(ObjectExplanationManager manager)
    {
        manager.Register<AddonFileMissingProblem>((problem, arg) =>
        {
            switch (problem.FileTypeMismatch)
            {
                case AddonFileTypeMismatch.ShouldBeDirectory:
                    return Texts.AddonFileNotExistProblemExplain_ShouldBeDirectory.FormatNoThrow(problem.FilePath);
                case AddonFileTypeMismatch.ShouldBeFile:
                    return Texts.AddonFileNotExistProblemExplain_ShouldBeFile.FormatNoThrow(problem.FilePath);
            }
            return Texts.AddonFileNotExistProblemExplain.FormatNoThrow(problem.FilePath);
        });
        manager.Register<AddonChildrenProblem>((problem, arg) => Texts.AddonChildProblemExplain);
        manager.Register<InvalidPublishedFileIdProblem>((problem, arg) => Texts.InvalidPublishedFileIdProblemExplain.FormatNoThrow(problem.PublishedFileId));
        manager.Register<WorkshopVpkNotLoadedProblem>((problem, arg) => Texts.WorkshopVpkFileNotLoadProblemExplain);
        manager.Register<AddonGroupEnableStrategyProblem>((problem, arg) => Texts.AddonGroupEnableStrategyProblemExplain);
        manager.Register<VpkAddonConflictProblem>((problem, arg) => Texts.VpkAddonConflictProblemExplain);
        manager.Register<AddonDependencyProblem>((problem, arg) => Texts.AddonDependenciesProblemExplain);
        manager.Register<AddonCircularRefProblem>((problem, arg) => Texts.AddonCircularRefProblemExplain);
        manager.Register<AddonDownloadFailedProblem>((problem, arg) => Texts.AddonDownloadFailedProblemExplain.FormatNoThrow(problem.Exception?.GetType().Name ?? "null", problem.FilePath, problem.Url));
        manager.Register<AddonInvalidRefSourceProblem>((problem, arg) => Texts.AddonInvalidRefSourceProblemExplain);
    }
}
