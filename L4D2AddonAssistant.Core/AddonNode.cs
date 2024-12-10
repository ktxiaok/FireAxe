using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace L4D2AddonAssistant
{
    public class AddonNode : ObservableObject, IHierarchyNode<AddonNode>
    {
        private bool _isValid = true;

        private CancellationTokenSource _destructionCancellationTokenSource = new();

        private bool _isEnabled = false;

        private bool _allowEnabledInHierarchy = true;

        private int _blockMove = 0;

        private string _name = "";

        private AddonGroup? _group = null;

        private AddonRoot _root;

        private readonly ObservableCollection<AddonProblem> _problems = new();
        private readonly ReadOnlyObservableCollection<AddonProblem> _problemsReadOnly;
        private bool _isBusyChecking = false;

        private WeakReference<byte[]?> _image = new(null);
        private Task<byte[]?>? _getImageTask = null;

        private long? _fileSize = null;

        private DateTime _creationTime = DateTime.Now;

        public AddonNode(AddonRoot root, AddonGroup? group = null)
        {
            OnInitSelf();

            ArgumentNullException.ThrowIfNull(root);
            if (group != null && group.Root != root)
            {
                ThrowDifferentRootException();
            }

            _root = root;
            _problemsReadOnly = new(_problems);

            if (group == null)
            {
                root.AddNode(this);
            }
            else
            {
                group.AddChild(this);
                Group = group;
            }
            UpdateEnabledInHierarchy();
        }

        public virtual Type SaveType => typeof(AddonNodeSave);

        public bool IsValid => _isValid;

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (value == _isEnabled)
                {
                    return;
                }
                _isEnabled = value;
                NotifyChanged();
                UpdateEnabledInHierarchy();
                if (Group != null)
                {
                    Group.NotifyChildEnableOrDisable(this);
                }
                //AutoCheck();
                Root.RequestSave = true;
            }
        }

        public bool IsEnabledInHierarchy => _allowEnabledInHierarchy && _isEnabled;

        public AddonGroup? Group
        {
            get => _group;
            private set
            {
                if (NotifyAndSetIfChanged(ref _group, value))
                {
                    Root.RequestSave = true;
                }
            }
        }

        public IAddonNodeContainer Parent => ((IAddonNodeContainer?)Group) ?? Root; 

        public AddonRoot Root => _root;

        public virtual bool RequireFile => false;

        public bool IsAutoCheck => Root.IsAutoCheck; 

        bool IHierarchyNode<AddonNode>.IsNonterminal => HasChildren;

        public ReadOnlyObservableCollection<AddonNode> Children => Children_Internal;

        IEnumerable<AddonNode> IHierarchyNode<AddonNode>.Children => Children;

        public bool HasChildren => HasChildren_Internal;

        public bool HasProblem
        {
            get
            {
                if (_problems == null)
                {
                    return false;
                }
                return _problems.Count > 0;
            }
        }

        public ReadOnlyObservableCollection<AddonProblem> Problems => _problemsReadOnly;

        public byte[]? ImageCache
        {
            get
            {
                if (_image.TryGetTarget(out var target))
                {
                    return target;
                }
                return null;
            }
        }

        public string Name
        {
            get => _name;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                if (value.Length == 0)
                {
                    throw new ArgumentException("The value is empty.");
                }
                if (value == _name)
                {
                    return;
                }

                ThrowIfMoveDenied();

                var parentInternal = (IAddonNodeContainerInternal)Parent;
                parentInternal.ThrowIfNodeNameInvalid(value);

                // Try to move the file.
                if (RequireFile && _name.Length > 0)
                {
                    string sourcePath = BuildFilePath(Group, FileName);
                    string fullSourcePath = GetFullFilePath(sourcePath);
                    if (FileUtils.Exists(fullSourcePath))
                    {
                        string targetPath = Path.Join(Path.GetDirectoryName(sourcePath) ?? "", value + FileExtension);
                        string fullTargetPath = GetFullFilePath(targetPath);
                        FileUtils.Move(fullSourcePath, fullTargetPath);
                    }
                }

                parentInternal.ChangeNameUnchecked(_name, value, this);
                _name = value;
                NotifyChanged();
                Root.RequestSave = true;
            }
        }

        public string FullName => BuildFilePath(Group, Name);

        public string FileName => Name + FileExtension;

        public string FilePath
        {
            get => BuildFilePath(Group, FileName);
        }

        public string FullFilePath => GetFullFilePath(FilePath);

        public virtual string FileExtension => "";

        public long? FileSize
        {
            get => _fileSize;
            private set => NotifyAndSetIfChanged(ref _fileSize, value);
        }

        public DateTime CreationTime
        {
            get => _creationTime;
            set => NotifyAndSetIfChanged(ref _creationTime, value);
        }

        internal CancellationToken DestructionCancellationToken => _destructionCancellationTokenSource.Token;

        internal virtual ReadOnlyObservableCollection<AddonNode> Children_Internal => throw new NotSupportedException();

        internal virtual bool HasChildren_Internal => false;

        public Task<byte[]?> GetImageAsync(CancellationToken cancellationToken)
        {
            var task = _getImageTask;
            if (task == null)
            {
                task = DoGetImageAsync(DestructionCancellationToken);
                _getImageTask = task;
                _getImageTask.ContinueWith((task) =>
                {
                    _getImageTask = null;
                    if (task.IsCompletedSuccessfully)
                    {
                        _image.SetTarget(task.Result);
                    }
                }, Root.TaskScheduler);
            }
            return task.WaitAsync(cancellationToken);
        }

        public Task<byte[]?> GetImageAllowCacheAsync(CancellationToken cancellationToken)
        {
            var cache = ImageCache;
            if (cache != null)
            {
                return Task.FromResult<byte[]?>(cache);
            }
            return GetImageAsync(cancellationToken);
        }

        protected virtual Task<byte[]?> DoGetImageAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<byte[]?>(null);
        }

        public bool CanMoveTo(AddonGroup? group)
        {
            if (group == null)
            {
                return true;
            }
            if (Root != group.Root)
            {
                throw new InvalidOperationException("Different root.");
            }
            if (!HasChildren)
            {
                return true;
            }
            foreach (var node in this.GetSelfAndDescendantsByDfsPreorder())
            {
                if (node == group)
                {
                    return false;
                }
            }
            return true;
        }

        public void MoveTo(AddonGroup? group)
        {
            // Check the argument.
            if (group != null && Root != group.Root)
            {
                ThrowDifferentRootException();
            }
            if (!CanMoveTo(group))
            {
                ThrowMoveGroupToItselfException();
            }
            if (group == Group)
            {
                return;
            }

            ThrowIfMoveDenied();

            var containerInternal = group == null ? (IAddonNodeContainerInternal)Root : (IAddonNodeContainerInternal)group;
            containerInternal.ThrowIfNodeNameInvalid(Name);

            // Try to move the file.
            if (RequireFile && Name.Length > 0)
            {
                string fileName = FileName;
                string sourcePath = BuildFilePath(Group, fileName);
                string fullSourcePath = GetFullFilePath(sourcePath);
                if (FileUtils.Exists(fullSourcePath))
                {
                    string targetPath = BuildFilePath(group, fileName);
                    string fullTargetPath = GetFullFilePath(targetPath);
                    FileUtils.Move(fullSourcePath, fullTargetPath);
                }
            }

            if (group == null)
            {
                if (Group == null)
                {
                    return;
                }
                Group.RemoveChild(this);
                Group = null;
                Root.AddNode(this);
            }
            else
            {
                if (Group == null)
                {
                    Root.RemoveNode(this);
                }
                else
                {
                    Group.RemoveChild(this);
                }
                group.AddChild(this);
                Group = group;
            }

            UpdateEnabledInHierarchy();
        }

        public Task DestroyAsync()
        {
            var tasks = new List<Task>();
            foreach (var node in this.GetSelfAndDescendantsByDfsPreorder())
            {
                tasks.Add(node.OnDestroyAsync());
            }

            if (Group == null)
            {
                Root.RemoveNode(this);
            }
            else
            {
                Group.RemoveChild(this);
            }

            var resultTask = Task.WhenAll(tasks);
            return resultTask;
        }

        public Task DestroyWithFileAsync()
        {
            string? pathToDelete = null;
            if (RequireFile)
            {
                pathToDelete = FullFilePath;
            }

            var resultTask = DestroyAsync();
            if (pathToDelete != null)
            {
                resultTask = resultTask.ContinueWith((task) =>
                {
                    try
                    {
                        FileUtils.Delete(pathToDelete);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Exception occurred during deleting file: {FilePath}", pathToDelete);
                    }
                });
            }

            return resultTask;
        }

        public void Check()
        {
            if (_isBusyChecking)
            {
                return;
            }

            _isBusyChecking = true;
            _problems.Clear();
            OnCheck();
            OnPostCheck();
            _isBusyChecking = false;
        }

        public virtual void ClearCaches()
        {
            _image.SetTarget(null);
        }

        private class CreateSaveStackFrame
        {
            public AddonGroupSave Save;
            public IEnumerator<AddonNode> Children;
            public List<AddonNodeSave> SaveChildren = new();
            
            public CreateSaveStackFrame(AddonGroupSave save, IEnumerator<AddonNode> children)
            {
                Save = save;
                Children = children;
            }
        }

        public AddonNodeSave CreateSave()
        {
            var save = NewSave(this);
            OnCreateSave(save);
            if (!HasChildren)
            {
                return save;
            }
            List<CreateSaveStackFrame> stack = new();
            stack.Add(new CreateSaveStackFrame((AddonGroupSave)save, Children.GetEnumerator()));
            while (stack.Count != 0)
            {
                var current = stack[stack.Count - 1];
                if (current.Children.MoveNext())
                {
                    var child = current.Children.Current;
                    var childSave = NewSave(child);
                    child.OnCreateSave(childSave);
                    current.SaveChildren.Add(childSave);
                    if (child.HasChildren)
                    {
                        stack.Add(new CreateSaveStackFrame((AddonGroupSave)childSave, child.Children.GetEnumerator()));
                    }
                }
                else
                {
                    current.Save.Children = current.SaveChildren.ToArray();
                    stack.RemoveAt(stack.Count - 1);
                }
            }
            return save;

            AddonNodeSave NewSave(AddonNode node)
            {
                return (AddonNodeSave)Activator.CreateInstance(node.SaveType)!;
            }
        }

        private class LoadSaveStackFrame
        {
            public AddonGroup Group;
            public AddonGroupSave Save;
            public int ChildIdx = 0;

            public LoadSaveStackFrame(AddonGroup group, AddonGroupSave save)
            {
                Group = group;
                Save = save;
            }
        }

        public static AddonNode LoadSave(AddonNodeSave save, AddonRoot root)
        {
            var node = NewNode(save, null);
            node.OnLoadSave(save);
            var groupSave = save as AddonGroupSave;
            if (groupSave == null)
            {
                return node;
            }

            List<LoadSaveStackFrame> stack = new();
            stack.Add(new LoadSaveStackFrame((AddonGroup)node, groupSave));
            while (stack.Count != 0)
            {
                var current = stack[stack.Count - 1];
                if (current.ChildIdx < current.Save.Children.Length)
                {
                    var childSave = current.Save.Children[current.ChildIdx];
                    ++current.ChildIdx;
                    var child = NewNode(childSave, current.Group);
                    child.OnLoadSave(childSave);
                    if (child is AddonGroup childGroup)
                    {
                        stack.Add(new LoadSaveStackFrame(childGroup, (AddonGroupSave)childSave));
                    }
                }
                else
                {
                    stack.RemoveAt(stack.Count - 1);
                }
            }

            return node;

            AddonNode NewNode(AddonNodeSave save, AddonGroup? group)
            {
                return (AddonNode)Activator.CreateInstance(save.TargetType, root, group)!;
            }
        }

        protected void AutoCheck()
        {
            if (IsAutoCheck)
            {
                Check();
            }
        }

        protected void AddProblem(AddonProblem problem)
        {
            ArgumentNullException.ThrowIfNull(problem);

            _problems.Add(problem);
        }

        internal void RemoveProblem(AddonProblem problem)
        {
            _problems.Remove(problem);
        }

        protected IDisposable BlockMove()
        {
            _blockMove++;
            bool disposed = false;
            return DisposableUtils.Create(() =>
            {
                if (!disposed)
                {
                    _blockMove--;
                    disposed = true;
                }
            });
        }

        protected virtual Task OnDestroyAsync()
        {
            _isValid = false;
            NotifyChanged(nameof(IsValid));

            var task = Task.CompletedTask;
            _destructionCancellationTokenSource.Cancel();
            _destructionCancellationTokenSource.Dispose();
            if (_getImageTask != null)
            {
                task = Task.WhenAll(task, _getImageTask);
            }

            return task;
        }

        protected virtual void OnCheck()
        {
            
        }

        protected virtual void OnPostCheck()
        {
            FileSize = GetFileSize();

            if (RequireFile)
            {
                var fullFilePath = FullFilePath;

                try
                {
                    if (!File.Exists(fullFilePath) && !Directory.Exists(fullFilePath))
                    {
                        AddProblem(new AddonFileNotExistProblem(this));
                    }
                }
                catch (Exception ex)
                {
                    LogException(ex);
                }
            }

            void LogException(Exception ex)
            {
                Log.Error(ex, "Exception occurred during AddonNode.OnPostCheck");
            }
        }

        protected virtual long? GetFileSize()
        {
            string path = FullFilePath;

            try
            {
                if (File.Exists(path))
                {
                    return new FileInfo(path).Length;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred during AddonNode.GetFileSize.");
            }

            return null;
        }

        protected virtual void OnCreateSave(AddonNodeSave save)
        {
            save.IsEnabled = IsEnabled;
            save.Name = Name;
            save.CreationTime = CreationTime;
        }

        protected virtual void OnLoadSave(AddonNodeSave save)
        {
            IsEnabled = save.IsEnabled;
            Name = save.Name;
            if (save.CreationTime != default)
            {
                CreationTime = save.CreationTime;
            }
        }

        internal virtual void OnInitSelf()
        {

        }

        internal static string BuildFilePath(AddonGroup? group, string name)
        {
            if (group == null)
            {
                return name;
            }
            var nameList = new List<string>();
            nameList.Add(name);
            AddonGroup? current = group;
            while (current != null)
            {
                nameList.Add(current.Name);
                current = current.Group;
            }
            nameList.Reverse();
            return Path.Join(nameList.ToArray());
        }

        internal string GetFullFilePath(string path)
        {
            return Path.Join(Root.DirectoryPath, path);
        }

        private void UpdateEnabledInHierarchy()
        {
            var dfs = this.GetSelfAndDescendantsEnumByDfsPreorder();
            while (dfs.MoveNext())
            {
                var current = dfs.Current;
                var parent = current.Group;
                bool oldCurrentEnabledInHierarchy = current.IsEnabledInHierarchy;
                current._allowEnabledInHierarchy = parent == null ? true : parent.IsEnabledInHierarchy;
                if (current != this && current.IsEnabledInHierarchy == oldCurrentEnabledInHierarchy)
                {
                    dfs.SkipDescendantsOfCurrent();
                }
                else
                {
                    current.NotifyChanged(nameof(IsEnabledInHierarchy));
                }
            }
        }

        private void ThrowIfMoveDenied()
        {
            foreach (var node in this.GetSelfAndDescendantsByDfsPreorder())
            {
                if (node._blockMove > 0)
                {
                    throw new AddonNodeMoveDeniedException(node);
                }
            }
        }

        private static void ThrowDifferentRootException()
        {
            throw new InvalidOperationException("Couldn't move to a group whose root is different!");
        }

        private static void ThrowMoveGroupToItselfException()
        {
            throw new InvalidOperationException("Couldn't move a group to itself!");
        }
    }
}
