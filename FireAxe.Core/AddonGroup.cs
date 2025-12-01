using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.ComponentModel;
using Serilog;

namespace FireAxe;

public class AddonGroup : AddonNode, IAddonNodeContainer, IAddonNodeContainerInternal
{
    private readonly AddonNodeContainerService _containerService;

    private AddonGroupEnableStrategy _enableStrategy = AddonGroupEnableStrategy.None;

    private bool _isBusyHandlingChildEnableOrDisable = false;

    protected AddonGroup()
    {
        _containerService = new(this);

        ((INotifyCollectionChanged)Children).CollectionChanged += OnCollectionChanged;
        PropertyChanged += OnPropertyChanged;
        DescendantNodeMoved += OnDescendantNodeMoved;
    }

    public override Type SaveType => typeof(AddonGroupSave);

    public override bool RequireFile => true;

    public override bool IsDirectory => true;

    public AddonGroupEnableStrategy EnableStrategy
    {
        get => _enableStrategy;
        set
        {
            this.ThrowIfInvalid();

            if (!Enum.IsDefined(value))
            {
                throw new InvalidEnumArgumentException(nameof(EnableStrategy), (int)value, typeof(AddonGroupEnableStrategy));
            }

            if (value == _enableStrategy)
            {
                return;
            }

            _enableStrategy = value;
            NotifyChanged();
            Root.RequestSave = true;
            if (IsAutoCheckEnabled)
            {
                CheckEnableStrategy();
            }
        }
    }

    internal override ReadOnlyObservableCollection<AddonNode> Children_Internal => _containerService.Nodes;

    internal override bool HasChildren_Internal => true;

    ReadOnlyObservableCollection<AddonNode> IAddonNodeContainer.Nodes => Children;

    IAddonNodeContainer? IAddonNodeContainer.Parent => Group;

    string? IAddonNodeContainer.FileSystemPath => Root.IsDirectoryPathSet ? FullFilePath : null;

    public event Action<AddonNode>? DescendantNodeMoved = null;

    public string GetUniqueChildName(string name, bool ignoreFileSystem = false)
    {
        this.ThrowIfInvalid();

        return _containerService.GetUniqueChildName(name, ignoreFileSystem);
    }

    public AddonNode? TryGetNodeByName(string name)
    {
        this.ThrowIfInvalid();

        return _containerService.TryGetByName(name);
    }

    public AddonNode? TryGetNodeByPath(string path)
    {
        this.ThrowIfInvalid();

        return _containerService.TryGetByPath(path);
    }

    public bool EnableOneChildRandomlyIfSingleRandom()
    {
        this.ThrowIfInvalid();

        if (_enableStrategy != AddonGroupEnableStrategy.SingleRandom)
        {
            return false;
        }

        var children = Children;
        int count = children.Count;
        if (count == 0)
        {
            return false;
        }

        children[Random.Shared.Next(count)].IsEnabled = true;

        return true;
    }

    public void CheckChildrenProblems()
    {
        this.ThrowIfInvalid();

        InvalidateProblem<AddonChildrenProblem>();
        foreach (var child in Children)
        {
            if (child.Problems.Count > 0)
            {
                SetProblem(new AddonChildrenProblem(this));
                break;
            }
        }
    }

    public void CheckEnableStrategy()
    {
        this.ThrowIfInvalid();

        SetProblem(AddonGroupEnableStrategyProblem.TryCreate(this));
    }

    protected override void OnCheck(Action<Task> taskSubmitter)
    {
        base.OnCheck(taskSubmitter);

        if (Root.IsDirectoryPathSet)
        {
            var dirPath = FullFilePath;
            try
            {
                Directory.CreateDirectory(dirPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred during trying to create the directory: {Path}", dirPath);
            }
        }
    }

    protected override void OnPostCheck(Action<Task> taskSubmitter)
    {
        base.OnPostCheck(taskSubmitter);

        CheckChildrenProblems();

        CheckEnableStrategy();
    }

    protected virtual void OnChildEnableOrDisable(AddonNode child)
    {
        switch (_enableStrategy)
        {
            case AddonGroupEnableStrategy.Single:
            case AddonGroupEnableStrategy.SingleRandom:
                {
                    if (child.IsEnabled)
                    {
                        foreach (var child1 in Children)
                        {
                            if (child1 == child)
                            {
                                continue;
                            }
                            child1.IsEnabled = false;
                        }
                    }
                    break;
                }
            case AddonGroupEnableStrategy.All:
                {
                    bool enabled = child.IsEnabled;
                    IsEnabled = enabled;
                    foreach (var child1 in Children)
                    {
                        child1.IsEnabled = enabled;
                    }
                    break;
                }
        }

        if (IsAutoCheckEnabled)
        {
            CheckEnableStrategy();
        }
    }

    protected override void OnCreateSave(AddonNodeSave save)
    {
        base.OnCreateSave(save);
        var save1 = (AddonGroupSave)save;
        save1.EnableStrategy = EnableStrategy;
    }

    protected override void OnLoadSave(AddonNodeSave save0)
    {
        base.OnLoadSave(save0);

        var save = (AddonGroupSave)save0;
        try
        {
            EnableStrategy = save.EnableStrategy;
        }
        catch (Exception ex)
        {
            LogValueException(ex, nameof(EnableStrategy), save.EnableStrategy);
        }

        void LogValueException(Exception ex, string name, object? value)
        {
            Log.Error(ex, $"Exception occurred during setting {name} at {nameof(AddonGroup)}.{nameof(OnLoadSave)}. Invalid value: {{InvalidValue}}", value);
        }
    }

    internal void AddChild(AddonNode child)
    {
        _containerService.AddUnchecked(child);
        NotifyChildEnableOrDisable(child);
    }

    internal void RemoveChild(AddonNode child)
    {
        _containerService.Remove(child);
    }

    internal void NotifyChildEnableOrDisable(AddonNode child)
    {
        if (_isBusyHandlingChildEnableOrDisable)
        {
            return;
        }
        _isBusyHandlingChildEnableOrDisable = true;
        OnChildEnableOrDisable(child);
        _isBusyHandlingChildEnableOrDisable = false;
    }

    void IAddonNodeContainerInternal.ThrowIfChildNewNameDisallowed(string name, AddonNode child)
    {
        _containerService.ThrowIfChildNewNameDisallowed(name, child);
    }

    void IAddonNodeContainerInternal.ChangeChildNameUnchecked(string? oldName, string newName, AddonNode child)
    {
        _containerService.ChangeChildNameUnchecked(oldName, newName, child);
    }

    void IAddonNodeContainerInternal.NotifyDescendantNodeMoved(AddonNode node)
    {
        DescendantNodeMoved?.Invoke(node);
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IsEnabled))
        {
            OnIsEnabledChanged();
        }
    }

    private void OnIsEnabledChanged()
    {
        if (_enableStrategy == AddonGroupEnableStrategy.All)
        {
            bool oldValueOfIsBusyHandlingChildEnableOrDisable = _isBusyHandlingChildEnableOrDisable;
            _isBusyHandlingChildEnableOrDisable = true;
            bool enabled = IsEnabled;
            foreach (var child in Children)
            {
                child.IsEnabled = enabled;
            }
            _isBusyHandlingChildEnableOrDisable = oldValueOfIsBusyHandlingChildEnableOrDisable;
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Root.RequestSave = true;
    }

    private void OnDescendantNodeMoved(AddonNode obj)
    {
        AutoCheck();
    }
}

public enum AddonGroupEnableStrategy
{
    None = 0,
    Single,
    SingleRandom,
    All
}
