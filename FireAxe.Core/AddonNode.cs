using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace FireAxe
{
    public class AddonNode : ObservableObject, IHierarchyNode<AddonNode>
    {
        public const string NullName = "__null__";

        private bool _isValid = true;

        private CancellationTokenSource _destructionCancellationTokenSource = new();

        private bool _isEnabled = false;

        private bool _allowEnabledInHierarchy = true;

        private int _blockMove = 0;

        private Guid _id = Guid.Empty;

        private string _name = NullName;

        private AddonGroup? _group = null;

        private readonly AddonRoot _root;

        private readonly ObservableCollection<string> _tags = new();
        private readonly ReadOnlyObservableCollection<string> _tagsReadOnly;
        private readonly HashSet<string> _tagSet = new(2);

        private DateTime _creationTime = DateTime.Now;

        private string? _customImagePath = null;

        private readonly ObservableCollection<AddonProblem> _problems = new();
        private readonly ReadOnlyObservableCollection<AddonProblem> _problemsReadOnly;
        private bool _isBusyChecking = false;

        private readonly WeakReference<byte[]?> _imageCache = new(null);
        private Task<byte[]?>? _getImageTask = null;

        private long? _fileSize = null;

        public AddonNode(AddonRoot root, AddonGroup? group = null)
        {
            OnInitSelf();

            ArgumentNullException.ThrowIfNull(root);
            if (group != null && group.Root != root)
            {
                ThrowDifferentRootException();
            }

            _root = root;
            _tagsReadOnly = new(_tags);
            _problemsReadOnly = new(_problems);

            ((INotifyCollectionChanged)_tags).CollectionChanged += OnTagCollectionChanged;

            SetNewId();

            if (group == null)
            {
                UpdateEnabledInHierarchy();
                root.AddNode(this);
            }
            else
            {
                Group = group;
                UpdateEnabledInHierarchy();
                group.AddChild(this);
            }
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

        public Guid Id
        {
            get => _id;
            set
            {
                if (value == Guid.Empty)
                {
                    throw new ArgumentException("Cannot set Id to Guid.Empty");
                }
                if (value == _id)
                {
                    return;
                }

                Root.RegisterNodeId(value, _id, this);
                _id = value;
                NotifyChanged();
                Root.RequestSave = true;
            }
        }

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

        IHierarchyNode<AddonNode>? IHierarchyNode<AddonNode>.Parent => Parent;

        public AddonRoot Root => _root;

        public virtual bool RequireFile => false;

        public bool MayHaveFile => RequireFile && Name != NullName;

        public bool IsAutoCheck => Root.IsAutoCheck;

        bool IHierarchyNode<AddonNode>.IsNonterminal => HasChildren;

        public ReadOnlyObservableCollection<AddonNode> Children => Children_Internal;

        IEnumerable<AddonNode> IHierarchyNode<AddonNode>.Children => Children;

        public bool HasChildren => HasChildren_Internal;

        public ReadOnlyObservableCollection<string> Tags => _tagsReadOnly;

        public IEnumerable<string> TagsInHierarchy
        {
            get
            {
                return GetRawTags().Distinct();

                IEnumerable<string> GetRawTags()
                {
                    AddonNode? current = this;
                    while (current != null)
                    {
                        foreach (var tag in current.Tags)
                        {
                            yield return tag;
                        }

                        current = current.Group;
                    }
                }
            }
        }

        public ReadOnlyObservableCollection<AddonProblem> Problems => _problemsReadOnly;

        public byte[]? ImageCache
        {
            get
            {
                if (_imageCache.TryGetTarget(out var target))
                {
                    return target;
                }
                return null;
            }
            private set
            {
                _imageCache.SetTarget(value);
                NotifyChanged();
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
                if (value == NullName)
                {
                    throw new ArgumentException($"Couldn't set the name to \"{NullName}\" because it's a reserved name.");
                }

                ThrowIfMoveDenied();

                var parentInternal = (IAddonNodeContainerInternal)Parent;
                parentInternal.ThrowIfNodeNameInvalid(value);

                // Try to move the file.
                if (MayHaveFile)
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

        public string FullName 
        {
            get 
            {
                return BuildFilePath(Group, Name); 
            }
        }

        public string FileName 
        {
            get 
            {
                return Name + FileExtension; 
            } 
        }

        public string FilePath
        {
            get 
            {
                return BuildFilePath(Group, FileName); 
            }
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
            set
            {
                if (NotifyAndSetIfChanged(ref _creationTime, value))
                {
                    Root.RequestSave = true;
                }
            }
        }

        public string? CustomImagePath
        {
            get => _customImagePath;
            set
            {
                if (value != null && !FileUtils.IsValidPath(value))
                {
                    throw new ArgumentException($"invalid path: {value}");
                }
                if (NotifyAndSetIfChanged(ref _customImagePath, value))
                {
                    Root.RequestSave = true;
                }
            }
        }

        public string? CustomImageFullPath
        {
            get
            {
                var path = CustomImagePath;
                if (path == null)
                {
                    return null;
                }
                try
                {
                    return GetFullFilePath(path);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception occurred during getting full path of custom image: {CustomImagePath}", path);
                }
                return null;
            }
        }

        internal CancellationToken DestructionCancellationToken => _destructionCancellationTokenSource.Token;

        internal virtual ReadOnlyObservableCollection<AddonNode> Children_Internal => throw new NotSupportedException();

        internal virtual bool HasChildren_Internal => false;

        public void SetNewId()
        {
            while (true)
            {
                try
                {
                    Id = Guid.NewGuid();
                    return;
                }
                catch (AddonNodeIdExistsException) { }
            }
        }

        public bool AddTag(string tag)
        {
            ArgumentNullException.ThrowIfNull(tag);
            if (tag.Length == 0)
            {
                throw new ArgumentException("empty tag string");
            }

            if (!_tagSet.Add(tag))
            {
                return false;
            }
            _tags.Add(tag);
            return true;
        }

        public bool RemoveTag(string tag)
        {
            ArgumentNullException.ThrowIfNull(tag);

            bool result = _tagSet.Remove(tag);
            if (result)
            {
                _tags.Remove(tag);
            }
            return result;
        }

        public void RenameTag(string oldTag, string newTag)
        {
            ArgumentNullException.ThrowIfNull(oldTag);
            ArgumentNullException.ThrowIfNull(newTag);
            if (newTag.Length == 0)
            {
                throw new ArgumentException("empty tag string");
            }
            if (oldTag == newTag)
            {
                return;
            }

            if (!_tagSet.Remove(oldTag))
            {
                return;
            }
            int idx = _tags.IndexOf(oldTag);
            _tags.RemoveAt(idx);
            if (_tagSet.Add(newTag))
            {
                _tags.Insert(idx, newTag);
            }
        }

        public void MoveTag(int oldIndex, int newIndex)
        {
            _tags.Move(oldIndex, newIndex);
        }

        public bool ContainsTag(string tag)
        {
            ArgumentNullException.ThrowIfNull(tag);

            return _tagSet.Contains(tag);
        }

        public Task<byte[]?> GetImageAsync(CancellationToken cancellationToken = default)
        {
            var task = _getImageTask;
            if (task == null)
            {
                var rootTaskScheduler = Root.TaskScheduler;
                var destructionCancellationToken = DestructionCancellationToken;
                var rawGetImageTask = DoGetImageAsync(destructionCancellationToken);
                async Task<byte[]?> RunGetImageTask()
                {
                    var image = await rawGetImageTask.ConfigureAwait(false);
                    var endingTask = new Task(() => ImageCache = image);
                    endingTask.Start(rootTaskScheduler);
                    await endingTask.ConfigureAwait(false);
                    return image;
                }
                task = RunGetImageTask();
                _getImageTask = task;
                _getImageTask.ContinueWith(_ => _getImageTask = null, rootTaskScheduler);
            }
            return task.WaitAsync(cancellationToken);
        }

        public Task<byte[]?> GetImageAllowCacheAsync(CancellationToken cancellationToken = default)
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

        public void MoveTo(AddonGroup? targetGroup)
        {
            // Check the argument.
            if (targetGroup != null && Root != targetGroup.Root)
            {
                ThrowDifferentRootException();
            }
            if (!CanMoveTo(targetGroup))
            {
                ThrowMoveGroupToItselfException();
            }
            if (targetGroup == Group)
            {
                return;
            }

            ThrowIfMoveDenied();

            var containerInternal = targetGroup == null ? (IAddonNodeContainerInternal)Root : (IAddonNodeContainerInternal)targetGroup;
            containerInternal.ThrowIfNodeNameInvalid(Name);

            // Try to move the file.
            if (MayHaveFile)
            {
                string fileName = FileName;
                string sourcePath = BuildFilePath(Group, fileName);
                string fullSourcePath = GetFullFilePath(sourcePath);
                if (FileUtils.Exists(fullSourcePath))
                {
                    string targetPath = BuildFilePath(targetGroup, fileName);
                    string fullTargetPath = GetFullFilePath(targetPath);
                    FileUtils.Move(fullSourcePath, fullTargetPath);
                }
            }

            if (targetGroup == null)
            {
                Group!.RemoveChild(this);
                Group = null;
                UpdateEnabledInHierarchy();
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
                Group = targetGroup;
                UpdateEnabledInHierarchy();
                targetGroup.AddChild(this);
            }
        }

        public Task DestroyAsync()
        {
            if (!IsValid)
            {
                return Task.CompletedTask;
            }
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

        public async Task DestroyWithFileAsync()
        {
            if (!IsValid)
            {
                return;
            }

            ClearCacheFiles();

            string? pathToDelete = null;
            if (MayHaveFile)
            {
                pathToDelete = FullFilePath;
            }

            await DestroyAsync().ConfigureAwait(false);
            if (pathToDelete != null)
            {
                try
                {
                    FileUtils.MoveToRecycleBin(pathToDelete);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception occurred during moving file to recycle bin: {FilePath}", pathToDelete);
                }
            }
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
            ClearCacheFiles();
            ImageCache = null;
        }

        public virtual void ClearCacheFiles()
        {

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

            Root.UnregisterNodeId(_id);

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
            save.Id = Id;
            save.IsEnabled = IsEnabled;
            save.Name = Name;
            save.CreationTime = CreationTime;
            save.Tags = [.. Tags];
            save.CustomImagePath = CustomImagePath;
        }

        protected virtual void OnLoadSave(AddonNodeSave save)
        {
            if (save.Id != Guid.Empty)
            {
                Id = save.Id;
            }
            IsEnabled = save.IsEnabled;
            Name = save.Name;
            if (save.CreationTime != default)
            {
                CreationTime = save.CreationTime;
            }
            foreach (var tag in save.Tags)
            {
                if (string.IsNullOrEmpty(tag))
                {
                    continue;
                }
                AddTag(tag);
            }
            CustomImagePath = save.CustomImagePath;
        }

        protected virtual void OnAncestorsChanged()
        {
            NotifyChanged(nameof(TagsInHierarchy));
        }

        internal void NotifyAncestorsChanged()
        {
            OnAncestorsChanged();
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

        private void OnTagCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            foreach (var node in this.GetSelfAndDescendantsByDfsPreorder())
            {
                node.NotifyChanged(nameof(TagsInHierarchy));
            }

            Root.RequestSave = true;
        }

        private void UpdateEnabledInHierarchy()
        {
            var dfs = this.GetSelfAndDescendantsEnumeratorByDfsPreorder();
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
