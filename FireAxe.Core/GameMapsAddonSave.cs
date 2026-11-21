namespace FireAxe;

public class GameMapsAddonSave : AddonGroupSave
{
    public override Type TargetType => typeof(GameMapsAddon);

    public ulong? DetailsId { get; set; }

    public bool RequestAutoSetName { get; set; } = true;

    public bool? IsAutoUpdate { get; set; }

    public string? InstalledRevision { get; set; }

    public GameMapsVpkFile[] InstalledFiles { get; set; } = [];
}

public sealed record GameMapsVpkFile(string FileName, Guid AddonId);
