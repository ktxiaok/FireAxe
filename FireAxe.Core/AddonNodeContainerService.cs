using System;
using System.Collections.ObjectModel;
using Serilog;

namespace FireAxe;

internal class AddonNodeContainerService
{
    private readonly IAddonNodeContainer _container;

    private readonly ObservableCollection<AddonNode> _nodes;
    private readonly ReadOnlyObservableCollection<AddonNode> _nodesReadOnly;

    private readonly Dictionary<string, AddonNode> _nameToNode = new(StringComparer.InvariantCultureIgnoreCase);

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

        ChangeChildNameUnchecked(null, node.Name, node);
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
            _nameToNode.Remove(name);
        }
        _nodes.Remove(node);
        foreach (var containerOrAncestor in _container.GetSelfAndAncestors())
        {
            ((IAddonNodeContainerInternal)containerOrAncestor).NotifyDescendantNodeMoved(node);
        }
    }

    public string GetUniqueChildName(string name, bool ignoreFileSystem)
    {
        ArgumentNullException.ThrowIfNull(name);
        name = AddonNode.SanitizeName(name);

        var fileSystemEntries = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

        if (!ignoreFileSystem)
        {
            var fileSystemPath = _container.FileSystemPath;
            if (fileSystemPath is not null)
            {
                try
                {
                    foreach (var path in Directory.EnumerateFileSystemEntries(fileSystemPath))
                    {
                        fileSystemEntries.Add(Path.GetFileName(path));
                        fileSystemEntries.Add(Path.GetFileNameWithoutExtension(path));
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception occurred during enumerating file system entires of the directory: {Path}", fileSystemPath);
                }
            }
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

    public AddonNode? TryGetByName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (_nameToNode.TryGetValue(name, out var node))
        {
            return node;
        }
        return null;
    }

    public AddonNode? TryGetByPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var names = path.Split('/');
        int i = 0;
        IAddonNodeContainer parent = _container;
        while (true)
        {
            var node = parent.TryGetNodeByName(names[i]);
            if (node is null)
            {
                return null;
            }
            if (i == names.Length - 1)
            {
                return node;
            }
            var container = node as IAddonNodeContainer;
            if (container is null)
            {
                return null;
            }
            parent = container;
            i++;
        }
    }

    public void ThrowIfChildNewNameDisallowed(string name, AddonNode child)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(child);

        if (_nameToNode.TryGetValue(name, out var existingNode) && existingNode != child)
        {
            throw new AddonNameExistsException(name);
        }
    }

    public bool NameExists(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return _nameToNode.ContainsKey(name);
    }

    public void ChangeChildNameUnchecked(string? oldName, string newName, AddonNode child)
    {
        ArgumentNullException.ThrowIfNull(newName);
        ArgumentNullException.ThrowIfNull(child);

        if (oldName != null && oldName != AddonNode.NullName)
        {
            _nameToNode.Remove(oldName);
        }
        if (newName != AddonNode.NullName)
        {
            _nameToNode[newName] = child;
        }
    }
}
