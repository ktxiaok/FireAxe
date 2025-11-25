using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using FireAxe.Resources;

namespace FireAxe;

public static class AddonProblemTypeExplanations
{
    private static readonly FrozenDictionary<Type, string> s_dict = FrozenDictionary.ToFrozenDictionary<Type, string>([
        new KeyValuePair<Type, string>(typeof(AddonFileNotExistProblem), Texts.AddonFileNotExist),
        new KeyValuePair<Type, string>(typeof(InvalidPublishedFileIdProblem), Texts.InvalidPublishedFileIdMessage),
        new KeyValuePair<Type, string>(typeof(WorkshopVpkNotLoadProblem), Texts.WorkshopVpkFileNotLoadProblemExplain),
        new KeyValuePair<Type, string>(typeof(AddonGroup.EnableStrategyProblem), Texts.AddonGroupEnableStrategyProblemExplain),
        new KeyValuePair<Type, string>(typeof(VpkAddonConflictProblem), Texts.VpkAddonConflictProblemExplain),
        new KeyValuePair<Type, string>(typeof(AddonDependenciesProblem), Texts.AddonDependenciesProblemExplain),
        new KeyValuePair<Type, string>(typeof(AddonCircularRefProblem), Texts.AddonCircularRefProblemExplain),
        new KeyValuePair<Type, string>(typeof(AddonDownloadFailedProblem), Texts.DownloadFailed),
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