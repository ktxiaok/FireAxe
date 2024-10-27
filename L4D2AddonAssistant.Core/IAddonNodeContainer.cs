using System;
using System.Collections.ObjectModel;

namespace L4D2AddonAssistant
{
    public interface IAddonNodeContainer
    {
        ReadOnlyObservableCollection<AddonNode> Nodes { get; }

        IAddonNodeContainer? Parent { get; }

        AddonRoot Root { get; }

        string GetUniqueNodeName(string name);
    }

    internal interface IAddonNodeContainerInternal
    {
        void ThrowIfNodeNameInvalid(string name);

        void ChangeNameUnchecked(string? oldName, string newName, AddonNode node);
    }
}
