using System;

namespace FireAxe;

public interface IAddonRootParentSettings
{
    bool IsAutoUpdateWorkshopItem { get; }

    bool IsAutoUpdateGameMapsItem { get; }

    VpkAddonConflictCheckSettings VpkAddonConflictCheckSettings { get; }
}
