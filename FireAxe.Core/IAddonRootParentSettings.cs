using System;

namespace FireAxe;

public interface IAddonRootParentSettings
{
    bool IsAutoUpdateWorkshopItem { get; }

    VpkAddonConflictCheckSettings VpkAddonConflictCheckSettings { get; }
}