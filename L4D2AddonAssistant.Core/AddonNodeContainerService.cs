using System;
using System.Collections.ObjectModel;

namespace L4D2AddonAssistant
{
    internal class AddonNodeContainerService
    {
        private ObservableCollection<AddonNode> _nodes;
        private ReadOnlyObservableCollection<AddonNode> _nodesReadOnly;

        private Dictionary<string, AddonNode> _nodeNames = new();

        public AddonNodeContainerService()
        {
            _nodes = new();
            _nodesReadOnly = new(_nodes);
        }

        public ReadOnlyObservableCollection<AddonNode> Nodes => _nodesReadOnly;

        public void AddUncheckName(AddonNode node)
        {
            ArgumentNullException.ThrowIfNull(node);

            ChangeNameUnchecked(null, node.Name, node);
            _nodes.Add(node);
        }

        public void Remove(AddonNode node)
        {
            ArgumentNullException.ThrowIfNull(node);

            var name = node.Name;
            if (name.Length > 0)
            {
                _nodeNames.Remove(name);
            }
            _nodes.Remove(node);
        }

        public string GetUniqueName(string name)
        {
            ArgumentNullException.ThrowIfNull(name);

            if (!NameExists(name))
            {
                return name;
            }
            int i = 1;
            while (true)
            {
                string nameTry = name + $"({i})";
                if (!NameExists(nameTry))
                {
                    return nameTry;
                }
                i++;
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

            if (oldName != null && oldName.Length > 0)
            {
                _nodeNames.Remove(oldName);
            }
            _nodeNames[newName] = node;
        }
    }
}
