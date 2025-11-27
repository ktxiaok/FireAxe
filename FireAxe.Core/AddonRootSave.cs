using System;
using System.Diagnostics.CodeAnalysis;

namespace FireAxe;

public class AddonRootSave
{
    [AllowNull]
    public AddonNodeSave[] Nodes { get; set => field = value.EliminateNull(); } = [];

    [AllowNull]
    public string[] CustomTags { get; set => field = value.EliminateNull(); } = [];
}
