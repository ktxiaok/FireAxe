using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using DynamicData;
using DynamicData.Alias;
using DynamicData.Binding;
using FireAxe.Resources;
using ReactiveUI;
using Serilog;

namespace FireAxe.ViewModels;

public class AddonNodeExplorerViewModel : ViewModelBase, IActivatableViewModel, IValidity, IDisposable
{
    public class MovingNodeViewModel : AddonNodeSimpleViewModel
    {
        private readonly AddonNodeExplorerViewModel _explorerViewModel;

        internal MovingNodeViewModel(AddonNodeExplorerViewModel explorerViewModel, AddonNode addon) : base(addon)
        {
            _explorerViewModel = explorerViewModel;

            MoveHereCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                try
                {
                    MoveHere();
                }
                catch (Exception ex)
                {
                    var nodePath = Addon?.NodePath ?? "null";
                    Log.Error(ex, "Exception occurred during moving the AddonNode: {NodePath}", nodePath);
                    await _explorerViewModel.ShowMoveExceptionInteraction.Handle((ex, nodePath));
                }
            });
        }

        public ReactiveCommand<Unit, Unit> MoveHereCommand { get; }

        public void Remove()
        {
            if (Addon is { } addon)
            {
                _explorerViewModel._movingNodes.Remove(addon);
            }
        }

        public void MoveHere()
        {
            if (Addon is { } addon)
            {
                addon.MoveTo(_explorerViewModel.CurrentGroup);
            }
            Remove();
        }
    }

    private readonly static TimeSpan SearchThrottleTime = TimeSpan.FromSeconds(0.5);

    private bool _disposed = false;
    private readonly CompositeDisposable _disposables = new();

    private readonly AddonRoot _addonRoot;

    private ReadOnlyObservableCollection<AddonNode> _currentNodes = ReadOnlyObservableCollection<AddonNode>.Empty;
    private IDisposable? _currentNodesSubscription = null;
    private bool _isRefreshCurrentNodesDeferredRequested = false;

    private AddonGroup? _currentGroup = null;

    private readonly ObservableAsPropertyHelper<IEnumerable<AddonNodeNavBarItemViewModel>> _navBarItemViewModels;

    private readonly ObservableValidRefCollection<AddonNode> _selectedNodes = new();
    private readonly ObservableAsPropertyHelper<AddonNode?> _selectedNode;
    private readonly ObservableAsPropertyHelper<bool> _hasSelection;
    private readonly ObservableAsPropertyHelper<bool> _isSingleSelection;
    private readonly ObservableAsPropertyHelper<bool> _isMultipleSelection;
    private readonly ObservableAsPropertyHelper<string?> _selectionNames;

    private readonly ObservableValidRefCollection<AddonNode> _movingNodes = new();
    private readonly ReadOnlyObservableCollection<MovingNodeViewModel> _movingNodeViewModels;

    private AddonNodeSortMethod _sortMethod = AddonNodeSortMethod.None;
    private bool _isAscendingOrder = true;

    private AddonNodeListItemViewKind _listItemViewKind = AddonNodeListItemViewKind.MediumTile;
    private readonly ObservableAsPropertyHelper<bool> _isGridView;
    private readonly ObservableAsPropertyHelper<bool> _isTileView;
    private readonly ObservableAsPropertyHelper<double> _tileViewSize;

    private string _searchText = "";
    private readonly ObservableAsPropertyHelper<bool> _isSearchTextClearable;
    private CancellationTokenSource? _searchCts = null;
    private object? _currentSearchId = null;
    private bool _isSearching = false;

    private bool _searchIgnoreCase = true;
    private bool _isSearchFlatten = false;
    private bool _isSearchRegex = false;
    private bool _isFilterEnabled = false;
    private IEnumerable<string>? _existingTags = null;
    private AddonTagFilterMode _tagFilterMode = AddonTagFilterMode.Or;
    private IEnumerable<string> _selectedTags = [];
    private ISet<string>? _filterTags = null;
    private AddonNodeSearchOptions _searchOptions = new();

    public AddonNodeExplorerViewModel(AddonRoot addonRoot)
    {
        ArgumentNullException.ThrowIfNull(addonRoot);

        _addonRoot = addonRoot;

        _navBarItemViewModels = this.WhenAnyValue(x => x.CurrentGroup)
            .Select(currentGroup =>
            {
                var items = new List<AddonNodeNavBarItemViewModel>();
                AddonGroup? current = currentGroup;
                while (current != null)
                {
                    items.Add(new AddonNodeNavBarItemViewModel(this, current));
                    current = current.Group;
                }
                return items.ToArray().Reverse();
            })
            .ToProperty(this, nameof(NavBarItemViewModels));

        _selectedNodes.DisposeWith(_disposables);
        _selectedNode = _selectedNodes.ObserveCollectionChanges()
            .Select(_ =>
            {
                if (_selectedNodes is [AddonNode selectedNode])
                {
                    return selectedNode;
                }
                return null;
            })
            .ToProperty(this, nameof(SelectedNode));
        _hasSelection = this.WhenAnyValue(x => x.SelectedNodes.Count)
            .Select(count => count > 0)
            .ToProperty(this, nameof(HasSelection));
        _isSingleSelection = this.WhenAnyValue(x => x.SelectedNodes.Count)
            .Select(count => count == 1)
            .ToProperty(this, nameof(IsSingleSelection));
        _isMultipleSelection = this.WhenAnyValue(x => x.SelectedNodes.Count)
            .Select(count => count > 1)
            .ToProperty(this, nameof(IsMultipleSelection));
        _selectionNames = _selectedNodes.ObserveCollectionChanges()
            .Select(_ =>
            {
                if (_selectedNodes.Count == 0)
                {
                    return null;
                }
                return string.Join(", ", _selectedNodes.Select(node => node.Name));
            })
            .ToProperty(this, nameof(SelectionNames));

        _movingNodes.DisposeWith(_disposables);
        _movingNodes.ToObservableChangeSet<ObservableValidRefCollection<AddonNode>, AddonNode>()
            .Transform(addon => new MovingNodeViewModel(this, addon))
            .Bind(out _movingNodeViewModels)
            .Subscribe();

        _isGridView = this.WhenAnyValue(x => x.ListItemViewKind)
            .Select(kind => kind == AddonNodeListItemViewKind.Grid)
            .ToProperty(this, nameof(IsGridView));
        _isTileView = this.WhenAnyValue(x => x.ListItemViewKind)
            .Select(kind => kind.IsTile())
            .ToProperty(this, nameof(IsTileView));
        _tileViewSize = this.WhenAnyValue(x => x.ListItemViewKind)
            .Select(kind => (kind switch
            {
                AddonNodeListItemViewKind.MediumTile => (double?)Application.Current!.FindResource("size_addon_tile"),
                AddonNodeListItemViewKind.LargeTile => (double?)Application.Current!.FindResource("size_addon_tile_large"),
                AddonNodeListItemViewKind.SmallTile => (double?)Application.Current!.FindResource("size_addon_tile_small"),
                _ => null
            }).GetValueOrDefault(200))
            .ToProperty(this, nameof(TileViewSize));

        _isSearchTextClearable = this.WhenAnyValue(x => x.SearchText)
            .Select(searchText => searchText.Length > 0)
            .ToProperty(this, nameof(IsSearchTextClearable));

        GotoParentCommand = ReactiveCommand.Create(GotoParent, this.WhenAnyValue(x => x.CurrentGroup).Select(group => group != null));
        GotoRootCommand = ReactiveCommand.Create(() => GotoGroup(null));

        EnableCommand = ReactiveCommand.Create(() => SetSelectionEnabled(true), this.WhenAnyValue(x => x.HasSelection));
        DisableCommand = ReactiveCommand.Create(() => SetSelectionEnabled(false), this.WhenAnyValue(x => x.HasSelection));
        EnableRecursivelyCommand = ReactiveCommand.Create(() => SetSelectionEnabledRecursively(true), this.WhenAnyValue(x => x.HasSelection));
        DisableRecursivelyCommand = ReactiveCommand.Create(() => SetSelectionEnabledRecursively(false), this.WhenAnyValue(x => x.HasSelection));

        MoveCommand = ReactiveCommand.Create(() =>
        {
            this.ThrowIfInvalid();

            var selectedNodes = SelectedNodes.ToArray();
            foreach (var node in selectedNodes)
            {
                AddMovingNode(node);
            }
        }, this.WhenAnyValue(x => x.HasSelection));
        var movingNodesNotEmptyObservable = this.WhenAnyValue(x => x.MovingNodeViewModels.Count).Select(count => count > 0);
        MoveHereCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            this.ThrowIfInvalid();

            var movingNodeViewModels = MovingNodeViewModels.ToArray();
            bool skipAll = false;
            foreach (var movingNodeViewModel in movingNodeViewModels)
            {
                try
                {
                    movingNodeViewModel.MoveHere();
                }
                catch (Exception ex)
                {
                    var nodePath = movingNodeViewModel.Addon?.NodePath ?? "null";
                    Log.Error(ex, "Exception occurred during moving the AddonNode: {NodePath}", nodePath);
                    if (movingNodeViewModels.Length == 1)
                    {
                        await ShowMoveExceptionInteraction.Handle((ex, nodePath));
                    }
                    else if (!skipAll)
                    {
                        var reply = await ShowBatchMoveExceptionInteraction.Handle((ex, nodePath));
                        if (reply == OperationInterruptReply.Abort)
                        {
                            break;
                        }
                        else if (reply == OperationInterruptReply.SkipAll)
                        {
                            skipAll = true;
                        }
                    }
                }
            }
        }, movingNodesNotEmptyObservable);
        CancelMoveCommand = ReactiveCommand.Create(ClearMovingNodes, movingNodesNotEmptyObservable);

        NewGroupCommand = ReactiveCommand.Create(() => { NewGroup(); });
        NewRefAddonCommand = ReactiveCommand.Create(() => { NewRefAddon(); });
        NewWorkshopAddonCommand = ReactiveCommand.Create(() => { NewWorkshopAddon(); });
        NewWorkshopCollectionCommand = ReactiveCommand.CreateFromTask(async () => 
        {
            this.ThrowIfInvalid();

            await ShowNewWorkshopCollectionWindowInteraction.Handle((_addonRoot, CurrentGroup));
        });

        CreateRefAddonsBasedOnSelectedCommand = ReactiveCommand.Create(() => { CreateRefAddonsBasedOnSelected(); }, this.WhenAnyValue(x => x.HasSelection));

        DeleteCommand = ReactiveCommand.CreateFromTask<bool>(DeleteSelectionAsync, this.WhenAnyValue(x => x.HasSelection));

        var hasSelectedWorkshopVpkAddon = _selectedNodes.ObserveCollectionChanges()
            .Select(_ => SelectedNodes.SelectMany(addon => addon.GetSelfAndDescendants()).OfType<WorkshopVpkAddon>().Any());
        var hasSelectedUpdateableAddon = hasSelectedWorkshopVpkAddon;
        SetAutoUpdateStrategyToDefaultRecursivelyCommand = ReactiveCommand.Create(() => SetSelectionAutoUpdateStrategyRecursively(null), hasSelectedUpdateableAddon);
        SetAutoUpdateStrategyToEnabledRecursivelyCommand = ReactiveCommand.Create(() => SetSelectionAutoUpdateStrategyRecursively(true), hasSelectedUpdateableAddon);
        SetAutoUpdateStrategyToDisabledRecursivelyCommand = ReactiveCommand.Create(() => SetSelectionAutoUpdateStrategyRecursively(false), hasSelectedUpdateableAddon);
        OpenWorkshopPageCommand = ReactiveCommand.Create(OpenSelectionWorkshopPage, hasSelectedWorkshopVpkAddon);

        // Ensure the validity of the current group.
        this.WhenAnyValue(x => x.CurrentGroup!.IsValid)
            .Subscribe(isValid =>
            {
                if (!isValid)
                {
                    AddonGroup? current = _currentGroup;
                    while (true)
                    {
                        if (current == null)
                        {
                            GotoGroup(null);
                            break;
                        }

                        if (current.IsValid)
                        {
                            GotoGroup(current);
                            break;
                        }

                        current = current.Group;
                    }
                }
            })
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.CurrentGroup)
            .Subscribe(_ => SelectedNodes.Clear());

        UpdateFilterTags();
        this.WhenAnyValue(x => x.SelectedTags, x => x.IsFilterEnabled)
            .Skip(1)
            .Throttle(SearchThrottleTime, RxApp.MainThreadScheduler)
            .Subscribe(_ => 
            {
                if (IsValid)
                {
                    UpdateFilterTags();
                }
            });
        UpdateSearchOptions();
        this.WhenAnyValue(x => x.SearchIgnoreCase,
            x => x.IsSearchFlatten,
            x => x.IsSearchRegex, 
            x => x.TagFilterMode, 
            x => x.FilterTags)
            .Skip(1)
            .Throttle(SearchThrottleTime, RxApp.MainThreadScheduler)
            .Subscribe(_ => 
            {
                if (IsValid)
                {
                    UpdateSearchOptions();
                }
            });

        this.WhenAnyValue(x => x.SearchText)
            .Skip(1)
            .Throttle(SearchThrottleTime, RxApp.MainThreadScheduler)
            .Subscribe(_ => 
            {
                if (IsValid)
                {
                    RefreshCurrentNodes();
                }
            });
        this.WhenAnyValue(x => x.CurrentGroup, x => x.SortMethod, x => x.IsAscendingOrder, x => x.SearchOptions)
            .Skip(1)
            .Subscribe(_ => RefreshCurrentNodes());

        this.WhenActivated((CompositeDisposable disposables) =>
        {
            AddonRoot.RegisterInvalidHandler(() => IsValid = false)
                .DisposeWith(disposables);
            if (!IsValid)
            {
                return;
            }

            void OnNodeMoved(AddonNode node)
            {
                RefreshCurrentNodesDeferred();
            }
            AddonRoot.DescendantNodeMoved += OnNodeMoved;
            Disposable.Create(() => AddonRoot.DescendantNodeMoved -= OnNodeMoved)
                .DisposeWith(disposables);

            void OnNewNodeIdRegistered(AddonNode node)
            {
                RefreshCurrentNodesDeferred();
            }
            AddonRoot.NewNodeIdRegistered += OnNewNodeIdRegistered;
            Disposable.Create(() => AddonRoot.NewNodeIdRegistered -= OnNewNodeIdRegistered)
                .DisposeWith(disposables);

            UpdateExistingTags();
            AddonRoot.CustomTags.ObserveCollectionChanges()
                .Subscribe(_ => UpdateExistingTags())
                .DisposeWith(disposables);

            Disposable.Create(() =>
            {
                DisposeCurrentNodesSubscription();
            }).DisposeWith(disposables);

            Refresh();
        });
    }

    ~AddonNodeExplorerViewModel()
    {
        Dispose(false);
    }

    public bool IsValid { get; protected set => this.RaiseAndSetIfChanged(ref field, value); } = true;

    public AddonRoot AddonRoot => _addonRoot;

    public AddonGroup? CurrentGroup
    {
        get => _currentGroup;
        private set => this.RaiseAndSetIfChanged(ref _currentGroup, value);
    }

    public ReadOnlyObservableCollection<AddonNode> CurrentNodes
    {
        get => _currentNodes;
        private set => this.RaiseAndSetIfChanged(ref _currentNodes, value);
    }

    public AddonNodeSortMethod SortMethod
    {
        get => _sortMethod;
        set => this.RaiseAndSetIfChanged(ref _sortMethod, value);
    }

    public bool IsAscendingOrder
    {
        get => _isAscendingOrder;
        set => this.RaiseAndSetIfChanged(ref _isAscendingOrder, value);
    }

    public ReadOnlyObservableCollection<MovingNodeViewModel> MovingNodeViewModels => _movingNodeViewModels;

    public ObservableValidRefCollection<AddonNode> SelectedNodes => _selectedNodes;

    public AddonNode? SelectedNode => _selectedNode.Value;

    public bool HasSelection => _hasSelection.Value;

    public bool IsSingleSelection => _isSingleSelection.Value;

    public bool IsMultipleSelection => _isMultipleSelection.Value;

    public string? SelectionNames => _selectionNames.Value;

    public AddonNodeListItemViewKind ListItemViewKind
    {
        get => _listItemViewKind;
        set => this.RaiseAndSetIfChanged(ref _listItemViewKind, value);
    }

    public bool IsGridView => _isGridView.Value;

    public bool IsTileView => _isTileView.Value;

    public double TileViewSize => _tileViewSize.Value;

    public IEnumerable<AddonNodeNavBarItemViewModel> NavBarItemViewModels => _navBarItemViewModels.Value;

    public IEnumerable<string>? ExistingTags
    {
        get => _existingTags;
        private set => this.RaiseAndSetIfChanged(ref _existingTags, value);
    }

    public string SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

    public bool IsSearchTextClearable => _isSearchTextClearable.Value;

    public bool IsSearching
    {
        get => _isSearching;
        private set => this.RaiseAndSetIfChanged(ref _isSearching, value);
    }

    public bool SearchIgnoreCase
    {
        get => _searchIgnoreCase;
        set => this.RaiseAndSetIfChanged(ref _searchIgnoreCase, value);
    }

    public bool IsSearchFlatten
    {
        get => _isSearchFlatten;
        set => this.RaiseAndSetIfChanged(ref _isSearchFlatten, value);
    }

    public bool IsSearchRegex
    {
        get => _isSearchRegex;
        set => this.RaiseAndSetIfChanged(ref _isSearchRegex, value);
    }

    public bool IsFilterEnabled
    {
        get => _isFilterEnabled;
        set => this.RaiseAndSetIfChanged(ref _isFilterEnabled, value);
    }

    public AddonTagFilterMode TagFilterMode
    {
        get => _tagFilterMode;
        set => this.RaiseAndSetIfChanged(ref _tagFilterMode, value);
    }

    public IEnumerable<string> SelectedTags
    {
        get => _selectedTags;
        set
        {
            _selectedTags = value;
            this.RaisePropertyChanged();
        }
    }

    public ISet<string>? FilterTags
    {
        get => _filterTags;
        private set
        {
            _filterTags = value;
            this.RaisePropertyChanged();
        }
    }

    public AddonNodeSearchOptions SearchOptions
    {
        get => _searchOptions;
        private set => this.RaiseAndSetIfChanged(ref _searchOptions, value);
    }


    public ReactiveCommand<Unit, Unit> GotoParentCommand { get; }

    public ReactiveCommand<Unit, Unit> GotoRootCommand { get; }

    public ReactiveCommand<Unit, Unit> EnableCommand { get; }

    public ReactiveCommand<Unit, Unit> DisableCommand { get; }

    public ReactiveCommand<Unit, Unit> EnableRecursivelyCommand { get; }

    public ReactiveCommand<Unit, Unit> DisableRecursivelyCommand { get; }

    public ReactiveCommand<Unit, Unit> MoveCommand { get; }

    public ReactiveCommand<Unit, Unit> MoveHereCommand { get; }

    public ReactiveCommand<Unit, Unit> CancelMoveCommand { get; }

    public ReactiveCommand<Unit, Unit> NewGroupCommand { get; }

    public ReactiveCommand<Unit, Unit> NewRefAddonCommand { get; }

    public ReactiveCommand<Unit, Unit> NewWorkshopAddonCommand { get; }

    public ReactiveCommand<Unit, Unit> NewWorkshopCollectionCommand { get; }

    public ReactiveCommand<Unit, Unit> CreateRefAddonsBasedOnSelectedCommand { get; }

    public ReactiveCommand<bool, Unit> DeleteCommand { get; }

    public ReactiveCommand<Unit, Unit> SetAutoUpdateStrategyToDefaultRecursivelyCommand { get; }

    public ReactiveCommand<Unit, Unit> SetAutoUpdateStrategyToEnabledRecursivelyCommand { get; }

    public ReactiveCommand<Unit, Unit> SetAutoUpdateStrategyToDisabledRecursivelyCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenWorkshopPageCommand { get; }


    public Interaction<bool, bool> ConfirmDeleteInteraction { get; } = new();

    public Interaction<IEnumerable<(Task, string)>, Unit> ShowDeletionProgressInteraction { get; } = new();

    public Interaction<(Exception, string), Unit> ShowMoveExceptionInteraction { get; } = new();

    public Interaction<(Exception, string), OperationInterruptReply> ShowBatchMoveExceptionInteraction { get; } = new();

    public Interaction<(AddonRoot, AddonGroup?), Unit> ShowNewWorkshopCollectionWindowInteraction { get; } = new();


    public ViewModelActivator Activator { get; } = new();

    public void SelectNode(AddonNode node)
    {
        this.ThrowIfInvalid();
        ArgumentNullException.ThrowIfNull(node);

        _selectedNodes.Clear();
        if (node.Root == AddonRoot)
        {
            _selectedNodes.Add(node);
        }
    }

    public void SelectNodes(IEnumerable<AddonNode> nodes)
    {
        this.ThrowIfInvalid();
        ArgumentNullException.ThrowIfNull(nodes);

        _selectedNodes.Reset(nodes.Where(node => node.Root == AddonRoot));
    }

    public void GotoGroup(AddonGroup? group)
    {
        this.ThrowIfInvalid();
        if (group is not null)
        {
            if (!group.IsValid || group.Root != AddonRoot)
            {
                return;
            }
        }

        CurrentGroup = group;
    }

    public void GotoParent()
    {
        this.ThrowIfInvalid();

        if (CurrentGroup is { } currentGroup)
        {
            GotoGroup(currentGroup.Group);
        }
    }

    public void GotoNode(AddonNode node)
    {
        this.ThrowIfInvalid();
        ArgumentNullException.ThrowIfNull(node);
        if (!node.IsValid || node.Root != AddonRoot)
        {
            return;
        }

        CurrentGroup = node.Group;
        SelectNode(node);
    }

    public void AddMovingNode(AddonNode node)
    {
        this.ThrowIfInvalid();
        ArgumentNullException.ThrowIfNull(node);
        if (!node.IsValid || node.Root != AddonRoot)
        {
            return;
        }

        if (_movingNodes.Contains(node))
        {
            return;
        }

        _movingNodes.Add(node);
    }

    public void ClearMovingNodes()
    {
        this.ThrowIfInvalid();

        _movingNodes.Clear();
    }

    public void Refresh()
    {
        this.ThrowIfInvalid();

        RefreshCurrentNodes();
    }

    public AddonGroup NewGroup()
    {
        this.ThrowIfInvalid();

        var group = AddonNode.Create<AddonGroup>(AddonRoot, CurrentGroup);
        group.Name = group.Parent.GetUniqueChildName(Texts.UnnamedGroup);
        Directory.CreateDirectory(group.FullFilePath);
        SelectNode(group);
        return group;
    }

    public RefAddonNode NewRefAddon()
    {
        this.ThrowIfInvalid();

        var addon = AddonNode.Create<RefAddonNode>(AddonRoot, CurrentGroup);
        addon.Name = addon.Parent.GetUniqueChildName(Texts.UnnamedReferenceAddon);
        SelectNode(addon);
        return addon;
    }

    public WorkshopVpkAddon NewWorkshopAddon()
    {
        this.ThrowIfInvalid();

        var addon = AddonNode.Create<WorkshopVpkAddon>(AddonRoot, CurrentGroup);
        addon.Name = addon.Parent.GetUniqueChildName(Texts.UnnamedWorkshopAddon);
        addon.RequestAutoSetName = true;
        SelectNode(addon);
        return addon;
    }

    public IReadOnlyList<AddonNode> CreateRefAddonsBasedOnSelected()
    {
        this.ThrowIfInvalid();

        var selected = SelectedNodes.ToArray();
        using var blockAutoCheck = AddonRoot.BlockAutoCheck();
        var addons = RefAddonNode.CreateBasedOn(selected, CurrentGroup);
        foreach (var addon in addons)
        {
            addon.CheckSelfAndDescendants();
        }
        SelectNodes(addons);
        return addons;
    }

    public void ToggleOrderDirection()
    {
        this.ThrowIfInvalid();

        IsAscendingOrder = !IsAscendingOrder;
    }

    public void ClearSearchText()
    {
        this.ThrowIfInvalid();

        SearchText = "";
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                IsValid = false;
                _disposables.Dispose();
            }

            _disposed = true;
        }
    }

    private void RefreshCurrentNodes()
    {
        this.ThrowIfInvalid();

        var selectedNodes = SelectedNodes.ToArray();

        DisposeCurrentNodesSubscription();
        var disposables = new CompositeDisposable();
        _currentNodesSubscription = disposables;

        var rawNodes = Search(CurrentGroup?.Children ?? AddonRoot.Nodes);
        var nodesObservableChangeSet = rawNodes.ToObservableChangeSet();
        var sortMethod = SortMethod;
        if (sortMethod is not AddonNodeSortMethod.None)
        {
            nodesObservableChangeSet = nodesObservableChangeSet.Sort(new AddonNodeComparer(sortMethod, IsAscendingOrder));
        }
        nodesObservableChangeSet.Bind(out ReadOnlyObservableCollection<AddonNode> nodes)
            .Subscribe()
            .DisposeWith(disposables);
        CurrentNodes = nodes;

        SelectNodes(selectedNodes);
    }

    private async void RefreshCurrentNodesDeferred()
    {
        this.ThrowIfInvalid();

        if (_isRefreshCurrentNodesDeferredRequested)
        {
            return;
        }

        _isRefreshCurrentNodesDeferredRequested = true;
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (IsValid)
                {
                    RefreshCurrentNodes();
                }
            }, DispatcherPriority.Default);
        }
        finally
        {
            _isRefreshCurrentNodesDeferredRequested = false;
        }
    }

    private void DisposeCurrentNodesSubscription()
    {
        CurrentNodes = ReadOnlyObservableCollection<AddonNode>.Empty;
        if (_currentNodesSubscription is not null)
        {
            _currentNodesSubscription.Dispose();
            _currentNodesSubscription = null;
        }
    }

    private ReadOnlyObservableCollection<AddonNode> Search(ReadOnlyObservableCollection<AddonNode> nodes)
    {
        CancelSearch();

        if (SearchText.Length == 0 && !IsFilterEnabled && !SearchOptions.IsFlatten)
        {
            return nodes;
        }

        _searchCts = new();
        var cancellationToken = _searchCts.Token;
        var searchId = new object();
        _currentSearchId = searchId;
        IsSearching = true;

        var resultNodes = new ObservableCollection<AddonNode>();

        async void DoSearch()
        {
            Action<AddonNode> consumer = node => Dispatcher.UIThread.Invoke(() => resultNodes.Add(node), DispatcherPriority.Background);
            try
            {
                await AddonNodeSearchUtils.SearchAsync(nodes, SearchText, SearchOptions, consumer, cancellationToken);
            }
            catch (OperationCanceledException) 
            {
                return;
            }

            if (_currentSearchId == searchId)
            {
                _currentSearchId = null;
                IsSearching = false;
            }
        }

        DoSearch();

        return new ReadOnlyObservableCollection<AddonNode>(resultNodes);
    }

    private void CancelSearch()
    {
        if (_searchCts is not null)
        {
            _searchCts.Cancel();
            _searchCts.Dispose();
            _searchCts = null;
        }
        _currentSearchId = null;
        IsSearching = false;
    }

    private void UpdateSearchOptions()
    {
        SearchOptions = new AddonNodeSearchOptions()
        {
            IgnoreCase = SearchIgnoreCase,
            IsFlatten = IsSearchFlatten,
            IsRegex = IsSearchRegex,
            Tags = FilterTags,
            TagFilterMode = TagFilterMode
        };
    }

    private void UpdateExistingTags()
    {
        string[] tags = [.. _addonRoot.CustomTags, .. AddonTags.BuiltInTags];
        ExistingTags = tags;
    }

    private void UpdateFilterTags()
    {
        if (IsFilterEnabled)
        {
            FilterTags = new HashSet<string>(SelectedTags);
        }
        else
        {
            FilterTags = null;
        }
    }

    private async Task DeleteSelectionAsync(bool retainFile)
    {
        this.ThrowIfInvalid();

        bool confirm = await ConfirmDeleteInteraction.Handle(retainFile);
        if (!confirm)
        {
            return;
        }
        var selectedNodes = SelectedNodes.ToArray();
        int count = selectedNodes.Length;
        if (count == 0)
        {
            return;
        }
        var operations = new (Task, string)[count];
        for (int i = 0; i < count; i++)
        {
            var node = selectedNodes[i];
            var nodePath = node.NodePath;
            if (retainFile)
            {
                operations[i] = (node.DestroyAsync(), nodePath);
            }
            else
            {
                operations[i] = (node.DestroyWithFileAsync(), nodePath);
            }
        }
        await ShowDeletionProgressInteraction.Handle(operations);
    }

    private void SetSelectionEnabled(bool enabled)
    {
        this.ThrowIfInvalid();

        var selectedNodes = SelectedNodes.ToArray();
        foreach (var node in selectedNodes)
        {
            node.IsEnabled = enabled;
        }
    }

    private void SetSelectionEnabledRecursively(bool enabled)
    {
        this.ThrowIfInvalid();

        var selectedNodes = SelectedNodes.ToArray();
        foreach (var node in selectedNodes)
        {
            foreach (var node1 in node.GetSelfAndDescendantsByDfsPreorder())
            {
                node1.IsEnabled = enabled;
            }
        }
    }

    private void SetSelectionAutoUpdateStrategyRecursively(bool? strategy)
    {
        this.ThrowIfInvalid();

        var selectedNodes = SelectedNodes.ToArray();
        foreach (var addon in selectedNodes.SelectMany(addon => addon.GetSelfAndDescendants()))
        {
            if (addon is WorkshopVpkAddon workshopVpkAddon)
            {
                workshopVpkAddon.IsAutoUpdate = strategy;
            }
        }
    }

    private void OpenSelectionWorkshopPage()
    {
        this.ThrowIfInvalid();

        foreach (var addon in SelectedNodes.SelectMany(addon => addon.GetSelfAndDescendants()).OfType<WorkshopVpkAddon>())
        {
            if (addon.PublishedFileId is { } id)
            {
                Utils.OpenWorkshopPage(id);
            }
        }
    }
}
