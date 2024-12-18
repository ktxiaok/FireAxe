using System;

namespace L4D2AddonAssistant
{
    public static class AddonTags
    {
        #region Built-in Tags
        private static readonly HashSet<string> s_builtInTags =
            [
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
            ];
        #endregion

        public static ISet<string> BuiltInTags => s_builtInTags;
    }
}
