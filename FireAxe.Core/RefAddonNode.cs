using Serilog;
using System;

namespace FireAxe;

public class RefAddonNode : AddonNode
{
    private Guid _sourceAddonId = Guid.Empty;

    private bool _isTagsSyncEnabled = true;
    private bool _isDependenciesSyncEnabled = true;

    protected RefAddonNode()
    {
        
    }

    public override Type SaveType => typeof(RefAddonNodeSave);

    public Guid SourceAddonId
    {
        get => _sourceAddonId;
        set
        {
            this.ThrowIfInvalid();

            if (value == _sourceAddonId)
            {
                return;
            }
            if (value == Id)
            {
                return;
            }

            _sourceAddonId = value;
            NotifyChanged();

            AutoCheck();

            Root.RequestSave = true;
        }
    }

    public AddonNode? ActualSourceAddon 
    {
        get
        {
            this.ThrowIfInvalid();

            return GetActualSourceAddon();
        }
    }

    public bool IsTagsSyncEnabled
    {
        get => _isTagsSyncEnabled;
        set
        {
            this.ThrowIfInvalid();

            if (NotifyAndSetIfChanged(ref _isTagsSyncEnabled, value))
            {
                Root.RequestSave = true;
            }
        }
    }

    public bool IsDependenciesSyncEnabled
    {
        get => _isDependenciesSyncEnabled;
        set
        {
            this.ThrowIfInvalid();

            if (NotifyAndSetIfChanged(ref _isDependenciesSyncEnabled, value))
            {
                Root.RequestSave = true;
            }
        }
    }

    public override string? ActualImageFilePath
    {
        get
        {
            this.ThrowIfInvalid();

            if (base.ActualImageFilePath is { } result)
            {
                return result;
            }
            if (ActualSourceAddon is { } source)
            {
                return source.ActualImageFilePath;
            }
            return null;
        }
    }

    public static IReadOnlyList<AddonNode> CreateBasedOn(IEnumerable<AddonNode> sources, AddonGroup? parentGroup)
    {
        AddonRoot? root = null;
        if (parentGroup is not null)
        {
            parentGroup.ThrowIfInvalid();
            root = parentGroup.Root;
        }
        ArgumentNullException.ThrowIfNull(sources);
        var sourceArray = sources.ToArray();
        foreach (var source in sourceArray)
        {
            source.ThrowIfInvalid();
            if (root is null)
            {
                root = source.Root;
            }
            else if (source.Root != root)
            {
                throw new InvalidOperationException("different AddonRoot");
            }
        }
        if (root is null)
        {
            return [];
        }

        var targets = new List<AddonNode>(sourceArray.Length);
        var sourceToTarget = new Dictionary<AddonNode, AddonNode>();
        var dependenciesSettingTodos = new List<(AddonNode Addon, IEnumerable<AddonNode> Dependencies)>();
        foreach (var source in sourceArray.SelectMany(addon => addon.GetSelfAndDescendantsByDfsPreorder()))
        {
            var sourceParentGroup = source.Group;
            AddonGroup? targetParentGroup;
            if (sourceParentGroup is not null && sourceToTarget.TryGetValue(sourceParentGroup, out var sourceParentGroupMapping))
            {
                targetParentGroup = (AddonGroup)sourceParentGroupMapping;
            }
            else
            {
                targetParentGroup = parentGroup;
            }

            AddonNode target;
            if (source is AddonGroup)
            {
                target = Create<AddonGroup>(root, targetParentGroup);
            }
            else
            {
                var refAddon = Create<RefAddonNode>(root, targetParentGroup);
                refAddon.SourceAddonId = source.Id;
                target = refAddon;
            }

            try
            {
                target.Name = target.Parent.GetUniqueChildName(source.Name);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred during setting the name of the target addon whose source is {SourcePath}.", source.NodePath);
            }

            if (source.DependentAddonIds.Count > 0)
            {
                var dependencies = new List<AddonNode>(source.DependentAddonIds.Count);
                foreach (var dependency in source.DependentAddons)
                {
                    dependencies.Add(dependency);
                }
                dependenciesSettingTodos.Add((target, dependencies));
            }

            sourceToTarget[source] = target;

            if (sourceArray.Contains(source))
            {
                targets.Add(target);
            }
        }

        foreach (var dependenciesSettingTodo in dependenciesSettingTodos)
        {
            var addon = dependenciesSettingTodo.Addon;
            if (addon is RefAddonNode refAddon)
            {
                refAddon.IsDependenciesSyncEnabled = false;
            }
            foreach (var dependency in dependenciesSettingTodo.Dependencies)
            {
                if (sourceToTarget.TryGetValue(dependency, out var dependencyMapping))
                {
                    addon.AddDependentAddon(dependencyMapping.Id);
                }
                else
                {
                    addon.AddDependentAddon(dependency.Id);
                }
            }
        }

        return targets;
    }

    public void CheckRef()
    {
        this.ThrowIfInvalid();

        InvalidateProblem<AddonCircularRefProblem>();
        AddonInvalidRefSourceProblem? invalidRefSourceProblem = null;
        if (GetActualSourceAddon(out var circularRefChain) is null)
        {
            invalidRefSourceProblem = new(this);
        }
        SetProblem(invalidRefSourceProblem);
        foreach (var refAddon in circularRefChain)
        {
            refAddon.SetProblem(new AddonCircularRefProblem(refAddon));
        }
    }

    public void CheckTagsSync()
    {
        this.ThrowIfInvalid();

        if (IsTagsSyncEnabled)
        {
            if (ActualSourceAddon is { } source)
            {
                ClearTags();
                foreach (var tag in source.Tags)
                {
                    AddTag(tag);
                }
            }
        }
    }

    public void CheckDependenciesSync()
    {
        this.ThrowIfInvalid();

        if (IsDependenciesSyncEnabled)
        {
            if (ActualSourceAddon is { } source)
            {
                ClearDependentAddons();
                foreach (var id in source.DependentAddonIds)
                {
                    AddDependentAddon(id);
                }
            }
        }
    }

    protected override void OnCheck(Action<Task> taskSubmitter)
    {
        base.OnCheck(taskSubmitter);

        CheckRef();
        CheckTagsSync();
        CheckDependenciesSync();
    }

    protected override Task<byte[]?> DoGetImageAsync(CancellationToken cancellationToken)
    {
        if (ActualSourceAddon is { } source)
        {
            return source.GetImageAsync(cancellationToken);
        }
        return Task.FromResult<byte[]?>(null);
    }

    protected override void OnCreateSave(AddonNodeSave save0)
    {
        base.OnCreateSave(save0);

        var save = (RefAddonNodeSave)save0;
        save.SourceAddonId = SourceAddonId;
        save.IsTagsSyncEnabled = IsTagsSyncEnabled;
        save.IsDependenciesSyncEnabled = IsDependenciesSyncEnabled;
    }

    protected override void OnLoadSave(AddonNodeSave save0)
    {
        base.OnLoadSave(save0);

        var save = (RefAddonNodeSave)save0;
        SourceAddonId = save.SourceAddonId;
        IsTagsSyncEnabled = save.IsTagsSyncEnabled;
        IsDependenciesSyncEnabled = save.IsDependenciesSyncEnabled;
    }

    private AddonNode? GetActualSourceAddon(out IEnumerable<RefAddonNode> circularRefChain)
    {
        circularRefChain = [];
        var root = Root;
        if (root.TryGetNodeById(SourceAddonId, out var addon))
        {
            var refAddon = addon as RefAddonNode;
            if (refAddon is null)
            {
                return addon;
            }

            var accessed = new HashSet<RefAddonNode>();
            var accessedList = new List<RefAddonNode>();
            while (true)
            {
                if (!accessed.Add(refAddon))
                {
                    int chainStart = accessedList.IndexOf(refAddon);
                    circularRefChain = accessedList[chainStart..];
                    return null;
                }
                accessedList.Add(refAddon);
                if (!root.TryGetNodeById(refAddon.SourceAddonId, out var nextAddon))
                {
                    return null;
                }
                if (nextAddon is RefAddonNode nextRefAddon)
                {
                    refAddon = nextRefAddon;
                    continue;
                }
                return nextAddon;
            }
        }
        return null;
    }

    private AddonNode? GetActualSourceAddon() => GetActualSourceAddon(out _);
}