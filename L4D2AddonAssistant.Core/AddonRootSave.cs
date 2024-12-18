using System;

namespace L4D2AddonAssistant
{
    public class AddonRootSave
    {
        public AddonNodeSave[] Nodes { get; set; } = Array.Empty<AddonNodeSave>();

        public string[] CustomTags { get; set; } = [];
    }
}
