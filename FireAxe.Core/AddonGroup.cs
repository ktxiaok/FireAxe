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
    public class EnableStrategyProblem : AddonProblem
    {
        public EnableStrategyProblem(AddonProblemSource<AddonGroup> problemSource) : base(problemSource)
        {

        }

        public new AddonGroup Addon => (AddonGroup)base.Addon;

        protected override bool OnAutomaticallyFix()
        {
            var group = Addon;
            switch (group.EnableStrategy)
            {
                case AddonGroupEnableStrategy.Single:
                case AddonGroupEnableStrategy.SingleRandom:
                    {
                        foreach (var child in group.Children)
                        {
                            child.IsEnabled = false;
                        }
                        break;
                    }
                case AddonGroupEnableStrategy.All:
                    {
                        bool enabled = group.IsEnabled;
                        foreach (var child in group.Children)
                        {
                            child.IsEnabled = enabled;
                        }
                        break;
                    }
            }
            return !HasProblem(group);
        }

        public static EnableStrategyProblem? TryCreate(AddonProblemSource<AddonGroup> problemSource)
        {
            ArgumentNullException.ThrowIfNull(problemSource);

            if (HasProblem(problemSource.Addon))
            {
                return new EnableStrategyProblem(problemSource);
            }

            return null;
        }

        public static bool HasProblem(AddonGroup group)
        {
            ArgumentNullException.ThrowIfNull(group);

            switch (group.EnableStrategy)
            {
                case AddonGroupEnableStrategy.Single:
                case AddonGroupEnableStrategy.SingleRandom:
                    {
                        int enabledCount = 0;
                        foreach (var child in group.Children)
                        {
                            if (child.IsEnabled)
                            {
                                enabledCount++;
                            }
                            if (enabledCount > 1)
                            {
                                return true;
                            }
                        }
                        break;
                    }
                case AddonGroupEnableStrategy.All:
                    {
                        int enabledCount = 0;
                        foreach (var child in group.Children)
                        {
                            if (child.IsEnabled)
                            {
                                ++enabledCount;
                            }
                        }
                        if (group.IsEnabled)
                        {
                            ++enabledCount;
                        }
                        if (enabledCount != 0 && enabledCount != group.Children.Count + 1)
                        {
                            return true;
                        }
                        break;
                    }
            }
            return false;
        }
    }

    private readonly AddonNodeContainerService _containerService;

    private AddonGroupEnableStrategy _enableStrategy = AddonGroupEnableStrategy.None;

    private bool _isBusyHandlingChildEnableOrDisable = false;

    private readonly AddonProblemSource<AddonGroup> _childrenProblemSource;
    private readonly AddonProblemSource<AddonGroup> _enableStrategyProblemSource;

    protected AddonGroup()
    {
        _containerService = new(this);

        _childrenProblemSource = new(this);
        _enableStrategyProblemSource = new(this);

        ((INotifyCollectionChanged)Children).CollectionChanged += OnCollectionChanged;
        PropertyChanged += OnPropertyChanged;
        DescendantNodeMoved += OnDescendantNodeMoved;
    }

    public override Type SaveType => typeof(AddonGroupSave);

    public override bool RequireFile => true;

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

    public string GetUniqueNodeName(string name)
    {
        this.ThrowIfInvalid();

        return _containerService.GetUniqueName(name);
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

        _childrenProblemSource.Clear();
        foreach (var child in Children)
        {
            if (child.Problems.Count > 0)
            {
                new AddonChildrenProblem(_childrenProblemSource).Submit();
                break;
            }
        }
    }

    public void CheckEnableStrategy()
    {
        this.ThrowIfInvalid();

        _enableStrategyProblemSource.Clear();
        EnableStrategyProblem.TryCreate(_enableStrategyProblemSource)?.Submit();
    }

    protected override void OnPostCheck()
    {
        base.OnPostCheck();

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

    void IAddonNodeContainerInternal.ThrowIfNodeNameInvalid(string name, AddonNode node)
    {
        _containerService.ThrowIfNameInvalid(name, node);
    }

    void IAddonNodeContainerInternal.ChangeNameUnchecked(string? oldName, string newName, AddonNode node)
    {
        _containerService.ChangeNameUnchecked(oldName, newName, node);
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
