using System;

namespace FireAxe;

public class InvalidPublishedFileIdProblem : AddonProblem
{
    public InvalidPublishedFileIdProblem(WorkshopVpkAddon addon, ulong publishedFileId) : base(addon)
    {
        PublishedFileId = publishedFileId;
    }

    public new WorkshopVpkAddon Addon => (WorkshopVpkAddon)base.Addon;

    public ulong PublishedFileId { get; }
}
