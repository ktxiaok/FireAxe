using System;
using System.Collections.ObjectModel;

namespace L4D2AddonAssistant
{
    public interface IAddonNodeContainer : IHierarchyNode<AddonNode>
    {
        IEnumerable<AddonNode> IHierarchyNode<AddonNode>.Children => Nodes;

        bool IHierarchyNode<AddonNode>.IsNonterminal => true;

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
