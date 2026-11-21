using System.Diagnostics.CodeAnalysis;

namespace FireAxe;

public class WorkshopCollectionSubscriptionSave : AddonGroupSave
{
    public override Type TargetType => typeof(WorkshopCollectionSubscription);

    public ulong? CollectionId { get; set; }

    public bool IncludeLinkedCollections { get; set; } = true;

    public bool? IsAutoUpdate { get; set; }

    public bool? AllowDownloads { get; set; }

    public bool? AllowDeletions { get; set; }

    public bool? EnableNewAddons { get; set; }

    public DateTimeOffset? LastSynchronizedAt { get; set; }

    public int LastItemCount { get; set; }

    [AllowNull]
    public WorkshopCollectionMember[] ManagedItems { get; set => field = value.EliminateNull(); } = [];
}

public sealed record WorkshopCollectionMember(ulong PublishedFileId, Guid AddonId);
