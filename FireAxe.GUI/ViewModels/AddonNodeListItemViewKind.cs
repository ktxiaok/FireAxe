using System;

namespace FireAxe.ViewModels
{
    public enum AddonNodeListItemViewKind : byte
    {
        Grid,
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
