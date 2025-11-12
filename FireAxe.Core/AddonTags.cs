using System;
using System.Collections.Frozen;

namespace FireAxe;

public static class AddonTags
{
    #region Built-in Tags
    private static readonly FrozenSet<string> s_builtInTags = FrozenSet.ToFrozenSet([
        "Survivors",
        "Bill",
        "Francis",
        "Louis",
        "Zoey",
        "Coach",
        "Ellis",
        "Nick",
        "Rochelle",

        "Common Infected",
        "Special Infected",
        "Boomer",
        "Charger",
        "Hunter",
        "Jockey",
        "Smoker",
        "Spitter",
        "Tank",
        "Witch",

        "Campaigns",
        "Weapons",
        "Items",
        "Sounds",
        "Scripts",
        "UI",
        "Miscellaneous",
        "Models",
        "Textures",

        "Single Player",
        "Co-op",
        "Versus",
        "Scavenge",
        "Survival",
        "Realism",
        "Realism Versus",
        "Mutations",

        "Grenade Launcher",
        "M60",
        "Melee",
        "Pistol",
        "Rifle",
        "Shotgun",
        "SMG",
        "Sniper",
        "Throwable",

        "Adrenaline",
        "Defibrillator",
        "Medkit",
        "Pills",
        "Other"
    ]);
    #endregion

    public static IReadOnlySet<string> BuiltInTags => s_builtInTags;
}
