using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;

namespace L4D2AddonAssistant
{
    public class AddonGroup : AddonNode, IAddonNodeContainer, IAddonNodeContainerInternal
    {
        public class EnableStrategyProblem : AddonProblem
        {
            public EnableStrategyProblem(AddonGroup source) : base(source)
            {
            }

            public new AddonGroup Source => (AddonGroup)base.Source;

            public override bool TrySolve()
            {
                var group = Source;
                switch (group.EnableStrategy)
                {
                    case AddonGroupEnableStrategy.Single:
                    case AddonGroupEnableStrategy.SingleRandom:
                        {
                            group.IsEnabled = false;
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
                return !Check(group);
            }

            public static bool TryCreate(AddonGroup group, [NotNullWhen(true)] out EnableStrategyProblem? problem)
            {
                problem = null;
                if (Check(group))
                {
                    problem = new(group);
                }
                return problem != null;
            }

            private static bool Check(AddonGroup group)
            {
                switch (group.EnableStrategy)
                {
                    case AddonGroupEnableStrategy.Single:
                    case AddonGroupEnableStrategy.SingleRandom:
                        {
                            bool enabled = group.IsEnabled;
                            int enabledCount = 0;
                            foreach (var child in group.Children)
                            {
                                if (child.IsEnabled)
                                {
                                    ++enabledCount;
                                }
                                if (enabled)
                                {
                                    if (enabledCount > 1)
                                    {
                                        return true;
                                    }
                                }
                                else
                                {
                                    if (enabledCount > 0)
                                    {
                                        return true;
                                    }
                                }
                            }
                            if (enabled)
                            {
                                if (enabledCount != 1)
                                {
                                    return true;
                                }
                            }
                            else
                            {
                                if (enabledCount != 0)
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

        private AddonNodeContainerService _containerService = null!;

        private AddonGroupEnableStrategy _enableStrategy = AddonGroupEnableStrategy.None;

        private bool _isBusyHandlingChildEnableOrDisable = false;

        public AddonGroup(AddonRoot root, AddonGroup? group = null) : base(root, group)
        {
            ((INotifyCollectionChanged)Children).CollectionChanged += OnCollectionChanged;
        }

        public override Type SaveType => typeof(AddonGroupSave);

        public override bool RequireFile => true;

        public AddonGroupEnableStrategy EnableStrategy
        {
            get => _enableStrategy;
            set
            {
                if (value == _enableStrategy)
                {
                    return;
                }
                _enableStrategy = value;
                NotifyChanged();
                AutoCheck();
                Root.RequestSave = true;
            }
        }

        internal override ReadOnlyObservableCollection<AddonNode> Children_Internal => _containerService.Nodes;

        internal override bool HasChildren_Internal => true;

        ReadOnlyObservableCollection<AddonNode> IAddonNodeContainer.Nodes => Children;

        IAddonNodeContainer? IAddonNodeContainer.Parent => Group;

        public string GetUniqueNodeName(string name)
        {
            return _containerService.GetUniqueName(name);
        }

        protected override void OnCheck()
        {
            base.OnCheck();
            if (EnableStrategyProblem.TryCreate(this, out var problem))
            {
                AddProblem(problem);
            }
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
                            IsEnabled = true;
                            foreach (var child1 in Children)
                            {
                                if (child1 == child)
                                {
                                    continue;
                                }
                                child1.IsEnabled = false;
                            }
                        }
                        else
                        {
                            IsEnabled = false;
                            foreach (var child1 in Children)
                            {
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
        }

        protected override void OnCreateSave(AddonNodeSave save)
        {
            base.OnCreateSave(save);
            var save1 = (AddonGroupSave)save;
            save1.EnableStrategy = EnableStrategy;
        }

        protected override void OnLoadSave(AddonNodeSave save)
        {
            base.OnLoadSave(save);
            var save1 = (AddonGroupSave)save;
            EnableStrategy = save1.EnableStrategy;
        }

        internal override void OnInitSelf()
        {
            base.OnInitSelf();
            _containerService = new();
        }

        internal void AddChild(AddonNode child)
        {
            _containerService.AddUncheckName(child);
            NotifyChildEnableOrDisable(child);
            AutoCheck();
        }

        internal void RemoveChild(AddonNode child)
        {
            _containerService.Remove(child);
            AutoCheck();
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

        void IAddonNodeContainerInternal.ThrowIfNodeNameInvalid(string name)
        {
            _containerService.ThrowIfNameInvalid(name);
        }

        void IAddonNodeContainerInternal.ChangeNameUnchecked(string? oldName, string newName, AddonNode node)
        {
            _containerService.ChangeNameUnchecked(oldName, newName, node);
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            Root.RequestSave = true;
        }
    }

    public enum AddonGroupEnableStrategy
    {
        None = 0,
        Single,
        SingleRandom,
        All
    }
}
