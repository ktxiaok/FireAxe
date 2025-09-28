using System;

namespace FireAxe;

public class WorkshopVpkAddonSave : VpkAddonSave
{
    public override Type TargetType => typeof(WorkshopVpkAddon);

    public ulong? PublishedFileId { get; set; }

    public AutoUpdateStrategy AutoUpdateStrategy { get; set; }

    public bool RequestAutoSetName { get; set; } = false;

    public bool RequestApplyTagsFromWorkshop { get; set; } = true;
}
