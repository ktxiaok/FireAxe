using System;

namespace L4D2AddonAssistant
{
    internal class AddonNodeFileFinder
    {
        private class StackFrame
        {
            public StackFrame? Up = null;
            public StackFrame? Down = null;
            public AddonGroup? Group = null;
            public string? DirectoryName;
            public IEnumerator<string> FileEnumerator;
            public Dictionary<string, AddonNode>? FileNameToNode = null;

            public StackFrame(string? directoryName, IEnumerator<string> fileEnumerator)
            {
                DirectoryName = directoryName;
                FileEnumerator = fileEnumerator;
            }
        }

        private StackFrame _stackTop;
        private AddonRoot _addonRoot;
        private bool _isDirectory = false;

        public AddonNodeFileFinder(AddonRoot addonRoot)
        {
            _addonRoot = addonRoot;
            _stackTop = new StackFrame(null, Directory.EnumerateFileSystemEntries(_addonRoot.DirectoryPath).GetEnumerator())
            {
                FileNameToNode = CreateFileNameToNodeDict(_addonRoot)
            };
        }

        public AddonNodeFileFinder(AddonGroup addonGroup)
        {
            _addonRoot = addonGroup.Root;
            _stackTop = new StackFrame(addonGroup.Name, Directory.EnumerateFileSystemEntries(addonGroup.FullFilePath).GetEnumerator())
            {
                Group = addonGroup,
                FileNameToNode = CreateFileNameToNodeDict(addonGroup)
            };
        }

        public bool IsCurrentDirectory => _isDirectory;

        public bool CurrentNodeExists
        {
            get
            {
                CheckFinished();
                if (_stackTop.FileNameToNode != null)
                {
                    string fileName = Path.GetFileName(CurrentFilePath);
                    return _stackTop.FileNameToNode.ContainsKey(fileName);
                }
                return false;
            }
        }

        public string CurrentFilePath
        {
            get
            {
                CheckFinished();
                return _stackTop.FileEnumerator.Current;
            }
        }

        public AddonGroup? CurrentGroup
        {
            get
            {
                CheckFinished();
                return _stackTop.Group;
            }
        }

        public bool MoveNext(bool skipDirectory = false)
        {
            CheckFinished();
            if (_isDirectory && !skipDirectory)
            {
                string dirPath = _stackTop.FileEnumerator.Current;
                string dirName = Path.GetFileName(dirPath);
                AddonGroup? group = null;
                bool nodeExists = false;
                if (_stackTop.FileNameToNode != null)
                {
                    if (_stackTop.FileNameToNode.TryGetValue(dirName, out var node))
                    {
                        nodeExists = true;
                        group = node as AddonGroup;
                    }
                }
                if (!nodeExists || (nodeExists && group != null))
                {
                    var frame = new StackFrame(dirName, Directory.EnumerateFileSystemEntries(dirPath).GetEnumerator());
                    if (group != null)
                    {
                        frame.Group = group;
                        frame.FileNameToNode = CreateFileNameToNodeDict(group);
                    }
                    Push(frame);
                }
            }
            _isDirectory = false;
            while (true)
            {
                bool hasNext = _stackTop.FileEnumerator.MoveNext();
                if (hasNext)
                {
                    _isDirectory = Directory.Exists(_stackTop.FileEnumerator.Current);
                    return true;
                }
                else
                {
                    Pop(out bool empty);
                    if (empty)
                    {
                        return false;
                    }
                }
            }
        }

        public AddonGroup? GetOrCreateCurrentGroup()
        {
            if (_stackTop.Down == null)
            {
                return CurrentGroup;
            }
            var group = CurrentGroup;
            if (group != null)
            {
                return group;
            }

            StackFrame current = _stackTop;
            while (true)
            {
                if (current.Down == null || current.Group != null)
                {
                    break;
                }
                current = current.Down;
            }
            while (true)
            {
                if (current.Up == null)
                {
                    break;
                }
                current.Up.Group = new AddonGroup(_addonRoot, current.Group) { Name = current.Up.DirectoryName!};
                current = current.Up;
            }
            
            return current.Group;
        }

        private void Push(StackFrame frame)
        {
            frame.Down = _stackTop;
            _stackTop.Up = frame;
            _stackTop = frame;
        }

        private void Pop(out bool empty)
        {
            _stackTop = _stackTop.Down!;
            if (_stackTop != null)
            {
                _stackTop.Up = null;
            }
            empty = _stackTop == null;
        }

        private void CheckFinished()
        {
            if (_stackTop == null)
            {
                throw new InvalidOperationException("The AddonNodeFileFinder is finished.");
            }
        }
        
        private static Dictionary<string, AddonNode> CreateFileNameToNodeDict(IAddonNodeContainer container)
        {
            Dictionary<string, AddonNode> dict = new();
            foreach (var node in container.Nodes)
            {
                dict[node.FileName] = node;
            }
            return dict;
        }
    }
}
