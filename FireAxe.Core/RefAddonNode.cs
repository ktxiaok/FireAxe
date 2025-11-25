using System;

namespace FireAxe;

public class RefAddonNode : AddonNode
{
    private Guid _sourceAddonId = Guid.Empty;

    private bool _isTagsSyncEnabled = true;
    private bool _isDependenciesSyncEnabled = true;

    private readonly AddonProblemSource<RefAddonNode> _circularRefProblemSource;

    protected RefAddonNode()
    {
        _circularRefProblemSource = new(this);
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

            _circularRefProblemSource.Clear();

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
                        for (int i = accessedList.IndexOf(refAddon), len = accessedList.Count; i < len; i++)
                        {
                            new AddonCircularRefProblem(accessedList[i]._circularRefProblemSource).Submit();
                        }
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

    public void CheckCircularRef()
    {
        _ = ActualSourceAddon;
    }

    public void CheckTagsSync()
    {
        this.ThrowIfInvalid();

        if (IsTagsSyncEnabled)
        {
            if (ActualSourceAddon is { } source)
            {
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
                foreach (var id in source.DependentAddonIds)
                {
                    AddDependentAddon(id);
                }
            }
        }
    }

    protected override void OnCheck()
    {
        base.OnCheck();

        CheckCircularRef();
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
}