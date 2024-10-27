using System;

namespace L4D2AddonAssistant.Views
{
    public enum AddonNodeSimpleViewKind : byte
    {
        MediumTile,
        SmallTile,
        LargeTile,
        TileBegin = MediumTile,
        TileEnd = LargeTile,
        List
    }

    public static class AddonNodeSimpleViewKindExtensions
    {
        public static bool IsTile(this AddonNodeSimpleViewKind kind)
        {
            return kind >= AddonNodeSimpleViewKind.TileBegin && kind <= AddonNodeSimpleViewKind.TileEnd;
        }
    }
}
