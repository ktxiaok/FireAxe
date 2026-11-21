using System;

namespace FireAxe;

public interface IAddonRootParentSettings
{
    bool IsAutoUpdateWorkshopItem { get; }

    bool IsAutoUpdateGameMapsItem { get; }

    bool IsAutoUpdateWorkshopCollection => true;

    bool AllowWorkshopCollectionDownloads => true;

    bool AllowWorkshopCollectionDeletions => true;

    bool EnableNewWorkshopCollectionAddons => true;

    int WorkshopCollectionUpdateIntervalMinutes => 30;

    VpkAddonConflictCheckSettings VpkAddonConflictCheckSettings { get; }
}
