using System;

namespace FireAxe;

public class AddonRootSave
{
    public AddonNodeSave[] Nodes { get; set; } = Array.Empty<AddonNodeSave>();

    public string[] CustomTags { get; set; } = [];
}
