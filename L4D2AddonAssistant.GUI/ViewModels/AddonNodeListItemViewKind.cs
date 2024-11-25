using System;

namespace L4D2AddonAssistant.ViewModels
{
    public enum AddonNodeListItemViewKind : byte
    {
        List,
        MediumTile,
        SmallTile,
        LargeTile,
        TileBegin = MediumTile,
        TileEnd = LargeTile,
    }

    public static class AddonNodeListItemViewKindExtensions
    {
        public static bool IsTile(this AddonNodeListItemViewKind kind)
        {
            return kind >= AddonNodeListItemViewKind.TileBegin && kind <= AddonNodeListItemViewKind.TileEnd;
        }
    }
}
