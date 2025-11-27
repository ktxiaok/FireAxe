using Serilog;
using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace FireAxe;

public class AddonNode : ObservableObject, IHierarchyNode<AddonNode>, IValidity
{
    public const string NullName = "__null__";

    private static readonly string[] s_linkedImageFileSupportedExtensions = [".jpg", ".jpeg", ".png"];

    private bool _isValid = true;

    private CancellationTokenSource _destructionCancellationTokenSource = new();

    private bool _isEnabled = false;
    private bool _allowEnabledInHierarchy = true;
    private bool _isBusySettingEnabled = false;

    private int _blockMove = 0;

    private Guid _id = Guid.Empty;

    private string _name = NullName;

    private AddonGroup? _group = null;

    private AddonRoot? _root = null;

    private int _priority = 0; 

    private readonly ObservableCollection<string> _tags = new();
    private readonly ReadOnlyObservableCollection<string> _tagsReadOnly;
    private readonly HashSet<string> _tagSet = new(2);

    private DateTime _creationTime = DateTime.Now;

    private string? _customImagePath = null;

    private int _lastProblemCountGiveToRoot = 0;
    private readonly ObservableCollection<AddonProblem> _problems = new();
    private readonly ReadOnlyObservableCollection<AddonProblem> _problemsReadOnly;
    private bool _isBusyChecking = false;

    private readonly ObservableCollection<Guid> _dependentAddonIds = new();
    private readonly ReadOnlyObservableCollection<Guid> _dependentAddonIdsReadOnly;
    private readonly HashSet<Guid> _dependentAddonIdSet = new();

    private readonly AddonProblemSource _fileNotExistProblemSource;
    private readonly AddonProblemSource _dependenciesProblemSource;

    private readonly WeakReference<byte[]?> _imageCache = new(null);
    private Task<byte[]?>? _getImageTask = null;

    private long? _fileSize = null;

    protected AddonNode()
    {
        _tagsReadOnly = new(_tags);
        _problemsReadOnly = new(_problems);
        _dependentAddonIdsReadOnly = new(_dependentAddonIds);

        _fileNotExistProblemSource = new(this);
        _dependenciesProblemSource = new(this);

        PropertyChanged += OnPropertyChanged;

        ((INotifyCollectionChanged)_tags).CollectionChanged += OnTagCollectionChanged;
        ((INotifyCollectionChanged)_problems).CollectionChanged += OnProblemCollectionChanged;
        ((INotifyCollectionChanged)_dependentAddonIds).CollectionChanged += OnDependentAddonIdsCollectionChanged;
    }

    public virtual Type SaveType => typeof(AddonNodeSave);

    public bool IsValid => _isValid;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            this.ThrowIfInvalid();

            if (value == _isEnabled)
            {
                return;
            }

            if (_isBusySettingEnabled)
            {
                return;
            }
            _isBusySettingEnabled = true;
            try
            {
                _isEnabled = value;
                NotifyChanged();
                UpdateEnabledInHierarchy();
                if (Group is { } group)
                {
                    group.NotifyChildEnableOrDisable(this);
                }
                //AutoCheck();
                Root.RequestSave = true;
            }
            finally
            {
                _isBusySettingEnabled = false;
            }
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

            this.ThrowIfInvalid();

            if (value == _id)
            {
                return;
            }

            Root.RegisterNodeId(value, _id, this);
            _id = value;
            NotifyChanged();
            Root.NotifyNewNodeIdRegistered(this);
            Root.RequestSave = true;
        }
    }

    public int Priority
    {
        get => _priority;
        set
        {
            this.ThrowIfInvalid();

            if (NotifyAndSetIfChanged(ref _priority, value))
            {
                NotifyChanged(nameof(PriorityInHierarchy));
                Root.RequestSave = true;
            }
        }
    }

    public int PriorityInHierarchy
    {
        get
        {
            this.ThrowIfInvalid();

            int result = 0;
            foreach (var selfOrAncestor in this.GetSelfAndAncestors())
            {
                if (selfOrAncestor is AddonNode node)
                {
                    result += node.Priority;
                }
            }
            return result;
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

    public AddonRoot Root => _root ?? throw new InvalidOperationException($"{nameof(Root)} is uninitialized");

    public virtual bool RequireFile => false;

    public virtual bool IsDirectory => false;

    public bool IsAutoCheckEnabled => Root.IsAutoCheckEnabled;

    bool IHierarchyNode<AddonNode>.IsNonterminal => HasChildren;

    public ReadOnlyObservableCollection<AddonNode> Children => Children_Internal;

    IEnumerable<AddonNode> IHierarchyNode<AddonNode>.Children => Children;

    public bool HasChildren => HasChildren_Internal;

    public ReadOnlyObservableCollection<string> Tags => _tagsReadOnly;

    public IEnumerable<string> TagsInHierarchy
    {
        get
        {
            this.ThrowIfInvalid();

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

    public ReadOnlyObservableCollection<Guid> DependentAddonIds => _dependentAddonIdsReadOnly;

    public IEnumerable<AddonNode> DependentAddons
    {
        get
        {
            var root = Root;
            foreach (var id in _dependentAddonIds)
            {
                if (root.TryGetNodeById(id, out var addon))
                {
                    yield return addon;
                }
            }
        }
    }

    public bool IsDependenciesAllEnabled
    {
        get
        {
            foreach (var addon in DependentAddons)
            {
                if (!addon.IsEnabledInHierarchy)
                {
                    return false;
                }
            }
            return true;
        }
    }

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
            value = SanitizeName(value);

            this.ThrowIfInvalid();

            if (value == _name)
            {
                return;
            }

            Root.RunUnstable(() =>
            {
                ThrowIfMoveDenied();

                var parent = Parent;
                var parentInternal = (IAddonNodeContainerInternal)parent;

                parentInternal.ThrowIfChildNewNameDisallowed(value, this);

                // Move the linked files.
                if (Root.IsDirectoryPathSet && Name != NullName && parent.FileSystemPath is { } parentFilePath)
                {
                    // Move the content file first.
                    if (RequireFile)
                    {
                        var contentFilePath = FullFilePath;
                        if (FileSystemUtils.Exists(contentFilePath))
                        {
                            var newContentFilePath = Path.Join(parentFilePath, value + FileExtension);
                            FileSystemUtils.Move(contentFilePath, newContentFilePath);
                        }
                    }

                    // Move other linked files.
                    foreach (var linkedFilePath in EnumerateLinkedFiles())
                    {
                        var extension = Path.GetExtension(linkedFilePath);
                        var newLinkedFilePath = Path.Join(parentFilePath, value + extension);
                        FileSystemUtils.Move(linkedFilePath, newLinkedFilePath);
                    }
                }

                parentInternal.ChangeChildNameUnchecked(_name, value, this);
            });

            _name = value;
            NotifyChanged();

            Root.RequestSave = true;
        }
    }

    public string NodePath 
    {
        get 
        {
            this.ThrowIfInvalid();

            return FileSystemUtils.NormalizePath(BuildFilePath(Group, Name)); 
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
            this.ThrowIfInvalid();

            return BuildFilePath(Group, FileName); 
        }
    }

    public string FullFilePath 
    {
        get
        {
            this.ThrowIfInvalid();

            return GetFullFilePath(FilePath);
        }
    }

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
            this.ThrowIfInvalid();

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
            if (value is not null)
            {
                FileSystemUtils.ThrowIfPathInvalid(value);
                if (value.StartsWith("..") || Path.IsPathRooted(value))
                {
                    throw new FileOutOfAddonRootException(value);
                }
                value = FileSystemUtils.NormalizePath(value);
            }

            this.ThrowIfInvalid();

            if (value == _customImagePath)
            {
                return;
            }

            _customImagePath = value;
            NotifyChanged();

            Root.RequestSave = true;
        }
    }

    public string? CustomImageFullPath
    {
        get
        {
            this.ThrowIfInvalid();

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

    public string? ActualImageFilePath
    {
        get
        {
            this.ThrowIfInvalid();

            var path = TryGetLinkedImageFilePath();
            if (path is not null)
            {
                return path;
            }

            path = CustomImageFullPath;
            return path;
        }
    }

    public virtual bool CanGetSuggestedName => false;

    internal CancellationToken DestructionCancellationToken => _destructionCancellationTokenSource.Token;

    internal virtual ReadOnlyObservableCollection<AddonNode> Children_Internal => throw new NotSupportedException();

    internal virtual bool HasChildren_Internal => false;

    public static AddonNode Create(Type addonType, AddonRoot root, AddonGroup? group = null)
    {
        ArgumentNullException.ThrowIfNull(addonType);
        if (!addonType.IsAssignableTo(typeof(AddonNode)))
        {
            throw new ArgumentException($"{nameof(addonType)} must be a subtype of {nameof(AddonNode)}");
        }
        ArgumentNullException.ThrowIfNull(root);
        root.ThrowIfInvalid();
        if (group is not null)
        {
            group.ThrowIfInvalid();
            if (group.Root != root)
            {
                ThrowDifferentRootException();
            }
        }

        var node = (AddonNode)Activator.CreateInstance(addonType, true)!;

        root.RunUnstable(() =>
        {
            node._root = root;

            node.SetNewId();

            if (group == null)
            {
                node.UpdateEnabledInHierarchy();
                node.Create();
                root.AddNode(node);
            }
            else
            {
                node.Group = group;
                node.UpdateEnabledInHierarchy();
                node.Create();
                group.AddChild(node);
            }
        });

        return node;
    }

    public static T Create<T>(AddonRoot root, AddonGroup? group = null) where T : AddonNode
    {
        return (T)Create(typeof(T), root, group);
    }

    public static string SanitizeName(string name, out bool empty)
    {
        const string Unnamed = "UNNAMED";

        ArgumentNullException.ThrowIfNull(name);

        empty = false;

        if (name.Length == 0 || name == NullName)
        {
            empty = true;
            return Unnamed;
        }

        name = name.Trim(' ', '.');
        if (name.Length == 0)
        {
            empty = true;
            return Unnamed;
        }

        name = FileSystemUtils.SanitizeFileName(name);
        if (name.Length == 0)
        {
            empty = true;
            return Unnamed;
        }

        return name;
    }

    public static string SanitizeName(string name) => SanitizeName(name, out _);

    public virtual Task<string?> TryGetSuggestedNameAsync(object? arg = null, CancellationToken cancellationToken = default)
    {
        this.ThrowIfInvalid();

        return Task.FromResult<string?>(null);
    }

    public void SetNewId()
    {
        this.ThrowIfInvalid();

        while (true)
        {
            try
            {
                Id = Guid.NewGuid();
                break;
            }
            catch (AddonNodeIdExistsException) { }
        }
    }

    public bool AddTag(string? tag)
    {
        this.ThrowIfInvalid();

        if (string.IsNullOrEmpty(tag))
        {
            return false;
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

        this.ThrowIfInvalid();

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

        this.ThrowIfInvalid();

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
        this.ThrowIfInvalid();

        _tags.Move(oldIndex, newIndex);
    }

    public bool ContainsTag(string tag)
    {
        ArgumentNullException.ThrowIfNull(tag);

        return _tagSet.Contains(tag);
    }

    public bool AddDependentAddon(Guid id)
    {
        this.ThrowIfInvalid();

        if (id == Id)
        {
            return false;
        }

        if (!_dependentAddonIdSet.Add(id))
        {
            return false;
        }
        _dependentAddonIds.Add(id);
        return true;
    }

    public bool RemoveDependentAddon(Guid id)
    {
        this.ThrowIfInvalid();

        if (!_dependentAddonIdSet.Remove(id))
        {
            return false;
        }
        _dependentAddonIds.Remove(id);
        return true;
    }

    public bool ContainsDependentAddon(Guid id)
    {
        return _dependentAddonIdSet.Contains(id);
    }

    public void EnableAllDependencies()
    {
        foreach (var addon in DependentAddons)
        {
            addon.IsEnabled = true;
        }
    }

    public void CheckDependencies()
    {
        _dependenciesProblemSource.Clear();
        if (!IsEnabledInHierarchy)
        {
            return;
        }
        if (!IsDependenciesAllEnabled)
        {
            new AddonDependenciesProblem(_dependenciesProblemSource).Submit();
        }
    }

    public string? TryGetLinkedImageFilePath()
    {
        this.ThrowIfInvalid();

        if (!Root.IsDirectoryPathSet)
        {
            return null;
        }

        var path = FullFilePath;
        try
        {
            foreach (var extension in s_linkedImageFileSupportedExtensions)
            {
                var imagePath = path + extension;
                if (File.Exists(imagePath))
                {
                    return imagePath;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Exception occurred during {nameof(AddonNode)}.{nameof(TryGetLinkedImageFilePath)}.");
        }
        return null;
    }

    public Task<byte[]?> GetImageAsync(CancellationToken cancellationToken = default)
    {
        this.ThrowIfInvalid();

        var task = _getImageTask;
        if (task == null)
        {
            var addonTaskCreator = this.GetValidTaskCreator();
            var destructionCancellationToken = DestructionCancellationToken;
            var rawGetImageTask = DoGetImageAsync(destructionCancellationToken);
            async Task<byte[]?> RunGetImageTask()
            {
                var image = await rawGetImageTask.ConfigureAwait(false);
                await addonTaskCreator.StartNew(self => self.ImageCache = image);
                return image;
            }
            task = RunGetImageTask();
            _getImageTask = task;
            _getImageTask.ContinueWith(_ => addonTaskCreator.StartNew(self =>
            {
                if (self._getImageTask == task)
                {
                    self._getImageTask = null;
                }
            }));
        }
        return task.WaitAsync(cancellationToken);
    }

    public Task<byte[]?> GetImageAllowCacheAsync(CancellationToken cancellationToken = default)
    {
        this.ThrowIfInvalid();

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

    public IEnumerable<string> EnumerateLinkedFiles()
    {
        this.ThrowIfInvalid();

        if (!Root.IsDirectoryPathSet || Name == NullName)
        {
            yield break;
        }
        var parentPath = Parent.FileSystemPath;
        if (parentPath is null)
        {
            yield break;
        }

        var corePathNormalized = FileSystemUtils.NormalizePath(Path.Join(parentPath, Name));
        foreach (var path in Directory.EnumerateFileSystemEntries(parentPath))
        {
            if (FileSystemUtils.NormalizePath(Path.ChangeExtension(path, null)) == corePathNormalized)
            {
                yield return path;
            }
        }
    }

    public bool CanMoveTo(AddonGroup? group)
    {
        this.ThrowIfInvalid();

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

        this.ThrowIfInvalid();

        if (targetGroup == Group)
        {
            return;
        }

        Root.RunUnstable(() =>
        {
            ThrowIfMoveDenied();

            var targetContainer = targetGroup == null ? (IAddonNodeContainer)Root : (IAddonNodeContainer)targetGroup;
            var targetContainerInternal = (IAddonNodeContainerInternal)targetContainer;

            targetContainerInternal.ThrowIfChildNewNameDisallowed(Name, this);

            // Move the linked files.
            if (Root.IsDirectoryPathSet && Name != NullName && targetContainer.FileSystemPath is { } targetContainerFilePath)
            {
                // Move the content file first.
                var contentFilePath = FullFilePath;
                if (FileSystemUtils.Exists(contentFilePath))
                {
                    var newContentFilePath = Path.Join(targetContainerFilePath, FileName);
                    FileSystemUtils.Move(contentFilePath, newContentFilePath);
                }

                // Move other linked files.
                foreach (var linkedFilePath in EnumerateLinkedFiles())
                {
                    var linkedFileName = Path.GetFileName(linkedFilePath);
                    var newLinkedFilePath = Path.Join(targetContainerFilePath, linkedFileName);
                    FileSystemUtils.Move(linkedFilePath, newLinkedFilePath);
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
        });
    }

    public Task DestroyAsync()
    {
        Task resultTask = null!;
        Root.RunUnstable(() =>
        {
            if (!IsValid)
            {
                resultTask = Task.CompletedTask;
                return;
            }

            var tasks = new List<Task>();
            foreach (var node in this.GetSelfAndDescendantsByDfsPostorder())
            {
                tasks.Add(node.OnDestroyAsync());
            }

            var group = Group;
            var root = Root;

            if (group == null)
            {
                root.RemoveNode(this);
            }
            else
            {
                group.RemoveChild(this);
            }

            var rootTaskScheduler = root.TaskScheduler;

            root.NotifyDescendantNodeDestructionStarted(this);

            resultTask = Task.WhenAll(tasks);
            resultTask.ContinueWith(_ =>
            {
                if (!root.IsValid)
                {
                    return;
                }
                root.NotifyDescendantNodeDestroyed(this);
            }, rootTaskScheduler);
        });
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
        if (RequireFile && Name != NullName)
        {
            pathToDelete = FullFilePath;
        }

        await DestroyAsync().ConfigureAwait(false);
        if (pathToDelete != null)
        {
            try
            {
                if (FileSystemUtils.Exists(pathToDelete))
                {
                    FileSystemUtils.MoveToRecycleBin(pathToDelete);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred during moving file to recycle bin: {FilePath}", pathToDelete);
            }
        }
    }

    public void ClearCaches()
    {
        this.ThrowIfInvalid();

        OnClearCaches();
    }

    protected virtual void OnClearCaches()
    {
        ClearCacheFiles();
        ImageCache = null;
    }

    public void ClearCacheFiles()
    {
        this.ThrowIfInvalid();

        OnClearCacheFiles();
    }

    protected virtual void OnClearCacheFiles()
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
        this.ThrowIfInvalid();

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

    public static AddonNode LoadSave(AddonNodeSave save, AddonRoot root, AddonGroup? parentGroup = null)
    {
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(root);
        root.ThrowIfInvalid();
        if (parentGroup is not null)
        {
            parentGroup.ThrowIfInvalid();
            if (parentGroup.Root != root)
            {
                ThrowDifferentRootException();
            }
        }

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
            group ??= parentGroup;
            return AddonNode.Create(save.TargetType, root, group);
        }
    }

    public IDisposable BlockMove()
    {
        this.ThrowIfInvalid();

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

    protected virtual void OnCreate()
    {

    }

    private void Create()
    {
        OnCreate();
        Root.NotifyDescendantNodeCreated(this);
    }

    protected virtual Task OnDestroyAsync()
    {
        _isValid = false;
        NotifyChanged(nameof(IsValid));

        var root = Root;

        root.ProblemCount -= _lastProblemCountGiveToRoot;
        root.UnregisterNodeId(_id);

        var tasks = new List<Task>();
        _destructionCancellationTokenSource.Cancel();
        _destructionCancellationTokenSource.Dispose();

        if (_getImageTask != null)
        {
            tasks.Add(_getImageTask);
        }

        return TaskUtils.WhenAllIgnoreCanceled(tasks);
    }

    public void Check()
    {
        this.ThrowIfInvalid();

        if (_isBusyChecking)
        {
            return;
        }

        _isBusyChecking = true;
        try
        {
            OnCheck();
            OnPostCheck();
        }
        finally
        {
            _isBusyChecking = false;
        }
    }

    protected void AutoCheck()
    {
        if (IsAutoCheckEnabled)
        {
            Check();
        }
    }

    protected virtual void OnCheck()
    {
        CheckDependencies();
    }

    protected virtual void OnPostCheck()
    {
        CheckFiles();
    }

    public void CheckFiles()
    {
        this.ThrowIfInvalid();

        FileSize = GetFileSize();

        _fileNotExistProblemSource.Clear();
        if (RequireFile)
        {
            var fullFilePath = FullFilePath;

            try
            {
                if (!FileSystemUtils.Exists(fullFilePath))
                {
                    new AddonFileNotExistProblem(_fileNotExistProblemSource).Submit();
                }
                else if (IsDirectory && !Directory.Exists(fullFilePath))
                {
                    new AddonFileNotExistProblem(_fileNotExistProblemSource)
                    {
                        FormatProblemType = AddonFileFormatProblemType.ShouldBeDirectory
                    }.Submit();
                }
                else if (!IsDirectory && !File.Exists(fullFilePath))
                {
                    new AddonFileNotExistProblem(_fileNotExistProblemSource)
                    {
                        FormatProblemType = AddonFileFormatProblemType.ShouldBeFile
                    }.Submit();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred during AddonNode.CheckFiles.");
            }
        }
    }

    internal void AddProblem(AddonProblem problem)
    {
        this.ThrowIfInvalid();

        _problems.Add(problem);
    }

    internal void RemoveProblem(AddonProblem problem)
    {
        this.ThrowIfInvalid();

        _problems.Remove(problem);
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
        save.Priority = Priority;
        save.CreationTime = CreationTime;
        save.Tags = [.. Tags];
        save.DependentAddonIds = [.. DependentAddonIds];
        save.CustomImagePath = CustomImagePath;
    }

    protected virtual void OnLoadSave(AddonNodeSave save)
    {
        if (save.Id != Guid.Empty)
        {
            if (Root.ContainsNodeId(save.Id))
            {
                Log.Warning("Existing ID({Id}) found and ignored during loading AddonNodeSave.", save.Id);
            }
            else
            {
                Id = save.Id;
            } 
        }

        IsEnabled = save.IsEnabled;

        var name = Parent.GetUniqueChildName(save.Name, true);
        if (name != save.Name)
        {
            Log.Warning($"Addon name corrected at {nameof(AddonNode)}.{nameof(OnLoadSave)}: from \"{{OldName}}\" to \"{{NewName}}\"", save.Name, name);
        }
        try
        {
            Name = name;
        }
        catch (Exception ex)
        {
            LogValueException(ex, nameof(Name), name);
        }

        Priority = save.Priority;

        if (save.CreationTime != default)
        {
            CreationTime = save.CreationTime;
        }

        foreach (var tag in save.Tags)
        {
            AddTag(tag);
        }

        foreach (var id in save.DependentAddonIds)
        {
            AddDependentAddon(id);
        }

        try
        {
            CustomImagePath = save.CustomImagePath;
        }
        catch (Exception ex)
        {
            LogValueException(ex, nameof(CustomImagePath), save.CustomImagePath);
        }

        void LogValueException(Exception ex, string propertyName, object? value)
        {
            Log.Error(ex, $"Exception occurred during setting {propertyName} at {nameof(AddonNode)}.{nameof(OnLoadSave)}. Invalid value: {{InvalidValue}}", value);
        }
    }

    protected virtual void OnAncestorsChanged()
    {
        NotifyChanged(nameof(NodePath));
        NotifyChanged(nameof(FilePath));
        NotifyChanged(nameof(PriorityInHierarchy));
        NotifyChanged(nameof(TagsInHierarchy));
    }

    internal void NotifyAncestorsChanged()
    {
        OnAncestorsChanged();
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

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var name = e.PropertyName;
        if (name == nameof(Name))
        {
            NotifyChanged(nameof(FileName));
            foreach (var node in this.GetSelfAndDescendantsByDfsPreorder())
            {
                node.NotifyChanged(nameof(NodePath));
                node.NotifyChanged(nameof(FilePath));
            }
        }
        else if (name == nameof(FilePath))
        {
            NotifyChanged(nameof(FullFilePath));
        }
        else if (name == nameof(IsEnabled))
        {
            if (IsAutoCheckEnabled)
            {
                CheckDependencies();
            }
        }
    }

    private void OnTagCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var node in this.GetSelfAndDescendantsByDfsPreorder())
        {
            node.NotifyChanged(nameof(TagsInHierarchy));
        }

        Root.RequestSave = true;
    }

    private void OnProblemCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        int problemCount;
        if (this is AddonGroup)
        {
            problemCount = 0;
            foreach (var problem in _problems)
            {
                if (problem is AddonChildrenProblem)
                {
                    continue;
                }
                problemCount++;
            }
        }
        else
        {
            problemCount = _problems.Count;
        }
        var root = Root;
        root.ProblemCount = root.ProblemCount - _lastProblemCountGiveToRoot + problemCount;
        _lastProblemCountGiveToRoot = problemCount;

        if (IsAutoCheckEnabled)
        {
            Group?.Check();
        }
    }

    private void OnDependentAddonIdsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
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
        throw new InvalidOperationException("Different AddonRoot instance");
    }

    private static void ThrowMoveGroupToItselfException()
    {
        throw new InvalidOperationException("Cannot move a AddonGroup instance to itself!");
    }
}
