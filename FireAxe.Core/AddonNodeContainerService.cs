using System;
using System.Collections.ObjectModel;
using Serilog;

namespace FireAxe;

internal class AddonNodeContainerService
{
    private readonly IAddonNodeContainer _container;

    private ObservableCollection<AddonNode> _nodes;
    private ReadOnlyObservableCollection<AddonNode> _nodesReadOnly;

    private Dictionary<string, AddonNode> _nodeNames = new();

    public AddonNodeContainerService(IAddonNodeContainer container)
    {
        _container = container;
        _nodes = new();
        _nodesReadOnly = new(_nodes);
    }

    public IAddonNodeContainer Container => _container;

    public ReadOnlyObservableCollection<AddonNode> Nodes => _nodesReadOnly;

    public void AddUnchecked(AddonNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        ChangeNameUnchecked(null, node.Name, node);
        foreach (var nodeOrDescendant in node.GetSelfAndDescendantsByDfsPreorder())
        {
            nodeOrDescendant.NotifyAncestorsChanged();
        }
        _nodes.Add(node);
        foreach (var containerOrAncestor in _container.GetSelfAndAncestors())
        {
            ((IAddonNodeContainerInternal)containerOrAncestor).NotifyDescendantNodeMoved(node);
        }
    }

    public void Remove(AddonNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var name = node.Name;
        if (name != AddonNode.NullName)
        {
            _nodeNames.Remove(name);
        }
        _nodes.Remove(node);
        foreach (var containerOrAncestor in _container.GetSelfAndAncestors())
        {
            ((IAddonNodeContainerInternal)containerOrAncestor).NotifyDescendantNodeMoved(node);
        }
    }

    public string GetUniqueName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var fileSystemEntries = new HashSet<string>();

        try
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(_container.FileSystemPath))
            {
                fileSystemEntries.Add(Path.GetFileNameWithoutExtension(path));
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred during enumerating file system entires of the directory: {Path}", _container.FileSystemPath);
        }

        bool Exists(string name) => NameExists(name) || fileSystemEntries.Contains(name);

        if (!Exists(name))
        {
            return name;
        }
        int i = 1;
        while (true)
        {
            string nameTry = name + $"({i})";
            if (!Exists(nameTry))
            {
                return nameTry;
            }
            checked
            {
                i++;
            }
        }
    }

    public void ThrowIfNameInvalid(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (NameExists(name))
        {
            throw new AddonNameExistsException(name);
        }
    }

    public bool NameExists(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return _nodeNames.ContainsKey(name);
    }

    public void ChangeNameUnchecked(string? oldName, string newName, AddonNode node)
    {
        ArgumentNullException.ThrowIfNull(newName);
        ArgumentNullException.ThrowIfNull(node);

        if (oldName != null && oldName != AddonNode.NullName)
        {
            _nodeNames.Remove(oldName);
        }
        if (newName != AddonNode.NullName)
        {
            _nodeNames[newName] = node;
        }
    }
}
