using System;
using System.Collections.ObjectModel;

namespace FireAxe
{
    public interface IAddonNodeContainer : IHierarchyNode<AddonNode>
    {
        IEnumerable<AddonNode> IHierarchyNode<AddonNode>.Children => Nodes;

        bool IHierarchyNode<AddonNode>.IsNonterminal => true;

        IHierarchyNode<AddonNode>? IHierarchyNode<AddonNode>.Parent => Parent;

        ReadOnlyObservableCollection<AddonNode> Nodes { get; }

        new IAddonNodeContainer? Parent { get; }

        AddonRoot Root { get; }

        event Action<AddonNode>? DescendantNodeMoved;

        string GetUniqueNodeName(string name);
    }

    internal interface IAddonNodeContainerInternal
    {
        void ThrowIfNodeNameInvalid(string name);

        void ChangeNameUnchecked(string? oldName, string newName, AddonNode node);

        void NotifyDescendantNodeMoved(AddonNode node);
    }
}
