using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.ComponentModel;

namespace FireAxe
{
    public class AddonGroup : AddonNode, IAddonNodeContainer, IAddonNodeContainerInternal
    {
        public class EnableStrategyProblem : AddonProblem
        {
            public EnableStrategyProblem(AddonGroup source) : base(source)
            {
            }

            public new AddonGroup Source => (AddonGroup)base.Source;

            protected override bool OnAutoSolve()
            {
                var group = Source;
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

            // returns true when there's a problem
            private static bool Check(AddonGroup group)
            {
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

        protected AddonGroup()
        {
            _containerService = new(this);

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

        public event Action<AddonNode>? DescendantNodeMoved = null;

        public string GetUniqueNodeName(string name)
        {
            return _containerService.GetUniqueName(name);
        }

        public void EnableOneChildRandomlyIfSingleRandom()
        {
            if (_enableStrategy != AddonGroupEnableStrategy.SingleRandom)
            {
                return;
            }

            var children = Children;
            int count = children.Count;
            if (count == 0)
            {
                return;
            }

            children[Random.Shared.Next(count)].IsEnabled = true;
        }

        protected override void OnPostCheck()
        {
            base.OnPostCheck();

            foreach (var child in Children)
            {
                if (child.Problems.Count > 0)
                {
                    AddProblem(new AddonChildProblem(this));
                    break;
                }
            }

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

        internal void AddChild(AddonNode child)
        {
            _containerService.AddUnchecked(child);
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
            if (IsAutoCheck)
            {
                Check();
            }
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
