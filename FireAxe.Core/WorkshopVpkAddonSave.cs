using System;

namespace FireAxe;

public class WorkshopVpkAddonSave : VpkAddonSave
{
    public override Type TargetType => typeof(WorkshopVpkAddon);

    public ulong? PublishedFileId { get; set; }

    public bool? IsAutoUpdate { get; set; } = null;

    // for backward compatibility
    public string? AutoUpdateStrategy
    {
        set
        {
            if (value == "Enabled")
            {
                IsAutoUpdate = true;
            }
            else if (value == "Disabled")
            {
                IsAutoUpdate = false;
            }
            else
            {
                IsAutoUpdate = null;
            }
        }
    }

    public bool RequestAutoSetName { get; set; } = false;

    public bool RequestApplyTagsFromWorkshop { get; set; } = true;
}
