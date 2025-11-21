using System;

namespace FireAxe;

public class RefAddonNodeSave : AddonNodeSave
{
    public override Type TargetType => typeof(RefAddonNode);

    public Guid SourceAddonId { get; set; } = Guid.Empty;

    public bool IsTagsSyncEnabled { get; set; } = true;

    public bool IsDependenciesSyncEnabled { get; set; } = true;
}