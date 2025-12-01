using System;
using System.Collections.ObjectModel;

namespace FireAxe;

public interface IAddonNodeContainer : IHierarchyNode<AddonNode>
{
    IEnumerable<AddonNode> IHierarchyNode<AddonNode>.Children => Nodes;

    bool IHierarchyNode<AddonNode>.IsNonterminal => true;

    IHierarchyNode<AddonNode>? IHierarchyNode<AddonNode>.Parent => Parent;

    ReadOnlyObservableCollection<AddonNode> Nodes { get; }

    new IAddonNodeContainer? Parent { get; }

    AddonRoot Root { get; }

    string? FileSystemPath { get; }

    event Action<AddonNode>? DescendantNodeMoved;

    string GetUniqueChildName(string name, bool ignoreFileSystem = false);

    AddonNode? TryGetNodeByName(string name);

    AddonNode? TryGetNodeByPath(string path);
}

internal interface IAddonNodeContainerInternal
{
    void ThrowIfChildNewNameDisallowed(string name, AddonNode child);

    void ChangeChildNameUnchecked(string? oldName, string newName, AddonNode child);

    void NotifyDescendantNodeMoved(AddonNode node);
}
