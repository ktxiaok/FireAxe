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