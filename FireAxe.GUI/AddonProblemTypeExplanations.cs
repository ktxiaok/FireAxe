using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using FireAxe.Resources;

namespace FireAxe;

public static class AddonProblemTypeExplanations
{
    private static readonly FrozenDictionary<Type, string> s_dict = FrozenDictionary.ToFrozenDictionary<Type, string>([
        new KeyValuePair<Type, string>(typeof(AddonFileMissingProblem), Texts.AddonFileNotExist),
        new KeyValuePair<Type, string>(typeof(InvalidPublishedFileIdProblem), Texts.InvalidPublishedFileIdMessage),
        new KeyValuePair<Type, string>(typeof(WorkshopVpkNotLoadedProblem), Texts.WorkshopVpkFileNotLoadProblemExplain),
        new KeyValuePair<Type, string>(typeof(AddonGroupEnableStrategyProblem), Texts.AddonGroupEnableStrategyProblemExplain),
        new KeyValuePair<Type, string>(typeof(VpkAddonConflictProblem), Texts.VpkAddonConflictProblemExplain),
        new KeyValuePair<Type, string>(typeof(AddonDependencyProblem), Texts.AddonDependenciesProblemExplain),
        new KeyValuePair<Type, string>(typeof(AddonInvalidRefSourceProblem), Texts.AddonInvalidRefSourceProblemExplain),
        new KeyValuePair<Type, string>(typeof(AddonCircularRefProblem), Texts.AddonCircularRefProblemExplain),
        new KeyValuePair<Type, string>(typeof(AddonDownloadFailedProblem), Texts.DownloadFailed),
        new KeyValuePair<Type, string>(typeof(InvalidVpkFileProblem), Texts.InvalidVpkFileProblemExplain),
    ]);

    public static string Get(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (s_dict.TryGetValue(type, out var result))
        {
            return result;
        }

        return type.Name;
    }
}