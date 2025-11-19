using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Security.Cryptography;
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

namespace FireAxe.ViewModels;

public class AddonNodeExplorerViewModel : ViewModelBase, IActivatableViewModel
{
    private readonly static TimeSpan SearchThrottleTime = TimeSpan.FromSeconds(0.5);

    private readonly AddonRoot _addonRoot;

    private ReadOnlyObservableCollection<AddonNode>? _nodes = null;
    private ReadOnlyObservableCollection<AddonNodeListItemViewModel> _nodeViewModels = ReadOnlyObservableCollection<AddonNodeListItemViewModel>.Empty;
    private IDisposable? _nodesSubscription = null;

    private IEnumerable<string>? _existingTags = null;

    private string _searchText = "";
    private readonly ObservableAsPropertyHelper<bool> _isSearchTextClearable;
    private CancellationTokenSource? _searchCts = null;
    private object? _currentSearchId = null;
    private bool _isSearching = false;

    private bool _searchIgnoreCase = true;
    private bool _isSearchFlatten = false;
    private bool _isSearchRegex = false;
    private bool _isFilterEnabled = false;
    private AddonTagFilterMode _tagFilterMode = AddonTagFilterMode.Or;
    private IEnumerable<string> _selectedTags = [];
    private ISet<string>? _filterTags = null;
    private AddonNodeSearchOptions _searchOptions = new();

    private AddonNodeSortMethod _sortMethod = AddonNodeSortMethod.None;
    private bool _isAscendingOrder = true;

    private AddonGroup? _currentGroup = null;

    private IEnumerable<AddonNode> _movingNodes = [];
    private readonly ObservableAsPropertyHelper<string?> _movingNodeNames;

    private readonly ObservableCollection<AddonNodeListItemViewModel> _selection = new();
    private readonly ObservableAsPropertyHelper<bool> _hasSelection;
    private readonly ObservableAsPropertyHelper<bool> _isSingleSelection;
    private readonly ObservableAsPropertyHelper<bool> _isMultipleSelection;
    private readonly ObservableAsPropertyHelper<string> _selectionNames;

    private readonly ObservableAsPropertyHelper<AddonNodeViewModel?> _activeAddonNodeViewModel;
    private bool _isAddonNodeViewEnabled = true;

    private AddonNodeListItemViewKind _listItemViewKind = AddonNodeListItemViewKind.MediumTile;

    private readonly ObservableAsPropertyHelper<bool> _isGridView;
    private readonly ObservableAsPropertyHelper<bool> _isTileView;
    private readonly ObservableAsPropertyHelper<double> _tileViewSize;

    private readonly ObservableAsPropertyHelper<IEnumerable<AddonNodeNavBarItemViewModel>> _navBarItemViewModels;

    private readonly Subject<AddonNode> _nodeMovedSubject = new();

    public AddonNodeExplorerViewModel(AddonRoot addonRoot)
    {
        ArgumentNullException.ThrowIfNull(addonRoot);

        _addonRoot = addonRoot;

        _isSearchTextClearable = this.WhenAnyValue(x => x.SearchText)
            .Select(searchText => searchText.Length > 0)
            .ToProperty(this, nameof(IsSearchTextClearable));

        _movingNodeNames = this.WhenAnyValue(x => x.MovingNodes)
            .Select(movingNodes =>
            {
                if (movingNodes.Any())
                {
                    return string.Join(", ", movingNodes.Select(node => node.Name));
                }
                return null;
            })
            .ToProperty(this, nameof(MovingNodeNames));

        _hasSelection = this.WhenAnyValue(x => x.Selection.Count)
            .Select(count => count > 0)
            .ToProperty(this, nameof(HasSelection));
        _isSingleSelection = this.WhenAnyValue(x => x.Selection.Count)
            .Select(count => count == 1)
            .ToProperty(this, nameof(IsSingleSelection));
        _isMultipleSelection = this.WhenAnyValue(x => x.Selection.Count)
            .Select(count => count > 1)
            .ToProperty(this, nameof(IsMultipleSelection));
        _selectionNames = this.WhenAnyValue(x => x.SelectedNodes)
            .Select(nodes =>
            {
                return string.Join(", ", nodes.Select(node => node.Name));
            })
            .ToProperty(this, nameof(SelectionNames));

        _activeAddonNodeViewModel = Selection.ObserveCollectionChanges()
            .CombineLatest(this.WhenAnyValue(x => x.IsAddonNodeViewEnabled))
            .Select(_ =>
            {
                if (!IsAddonNodeViewEnabled)
                {
                    return null;
                }

                var selection = Selection;
                if (selection.Count == 1 && selection[0].Addon is { } addon)
                {
                    return AddonNodeViewModel.Create(addon);
                }

                return null;
            })
            .ToProperty(this, nameof(ActiveAddonNodeViewModel), deferSubscription: true);

        _isGridView = this.WhenAnyValue(x => x.ListItemViewKind)
            .Select((kind) => kind == AddonNodeListItemViewKind.Grid)
            .ToProperty(this, nameof(IsGridView));
        _isTileView = this.WhenAnyValue(x => x.ListItemViewKind)
            .Select((kind) => kind.IsTile())
            .ToProperty(this, nameof(IsTileView));
        _tileViewSize = this.WhenAnyValue(x => x.ListItemViewKind)
            .Select((kind) => (kind switch
            {
                AddonNodeListItemViewKind.MediumTile => (double?)Application.Current!.FindResource("size_addon_tile"),
                AddonNodeListItemViewKind.LargeTile => (double?)Application.Current!.FindResource("size_addon_tile_large"),
                AddonNodeListItemViewKind.SmallTile => (double?)Application.Current!.FindResource("size_addon_tile_small"),
                _ => null
            }).GetValueOrDefault(200))
            .ToProperty(this, nameof(TileViewSize));

        _navBarItemViewModels = this.WhenAnyValue(x => x.CurrentGroup)
            .Select((currentGroup) =>
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

        GotoParentCommand = ReactiveCommand.Create(GotoParent,
            this.WhenAnyValue(x => x.CurrentGroup)
            .Select(group => group != null));
        GotoRootCommand = ReactiveCommand.Create(() => GotoGroup(null));

        EnableCommand = ReactiveCommand.Create(() => SetSelectionEnabled(true), this.WhenAnyValue(x => x.HasSelection));
        DisableCommand = ReactiveCommand.Create(() => SetSelectionEnabled(false), this.WhenAnyValue(x => x.HasSelection));
        EnableRecursivelyCommand = ReactiveCommand.Create(() => SetSelectionEnabledRecursively(true), this.WhenAnyValue(x => x.HasSelection));
        DisableRecursivelyCommand = ReactiveCommand.Create(() => SetSelectionEnabledRecursively(false), this.WhenAnyValue(x => x.HasSelection));

        MoveCommand = ReactiveCommand.Create(Move,
            this.WhenAnyValue(x => x.MovingNodes, x => x.Selection.Count)
            .Select(_ => !MovingNodes.Any() && Selection.Count > 0));
        MoveHereCommand = ReactiveCommand.CreateFromTask(MoveHere,
            this.WhenAnyValue(x => x.MovingNodes)
            .Select(movingNodes => movingNodes.Any()));
        CancelMoveCommand = ReactiveCommand.Create(CancelMove,
            this.WhenAnyValue(x => x.MovingNodes)
            .Select(movingNodes => movingNodes.Any()));

        NewGroupCommand = ReactiveCommand.Create(() => { NewGroup(); });
        NewWorkshopAddonCommand = ReactiveCommand.Create(() => { NewWorkshopAddon(); });
        NewWorkshopCollectionCommand = ReactiveCommand.CreateFromTask(async () => await ShowNewWorkshopCollectionWindowInteraction.Handle((_addonRoot, CurrentGroup)));

        DeleteCommand = ReactiveCommand.CreateFromTask<bool>(Delete, this.WhenAnyValue(x => x.HasSelection));

        SetAutoUpdateStrategyToDefaultRecursivelyCommand = ReactiveCommand.Create(() => SetAutoUpdateStrategyRecursively(AutoUpdateStrategy.Default));
        SetAutoUpdateStrategyToEnabledRecursivelyCommand = ReactiveCommand.Create(() => SetAutoUpdateStrategyRecursively(AutoUpdateStrategy.Enabled));
        SetAutoUpdateStrategyToDisabledRecursivelyCommand = ReactiveCommand.Create(() => SetAutoUpdateStrategyRecursively(AutoUpdateStrategy.Disabled));

        Selection.ObserveCollectionChanges()
            .Subscribe(_ => this.RaisePropertyChanged(nameof(SelectedNodes)));

        UpdateFilterTags();
        this.WhenAnyValue(x => x.SelectedTags, x => x.IsFilterEnabled)
            .Skip(1)
            .Throttle(SearchThrottleTime, RxApp.MainThreadScheduler)
            .Subscribe(_ => UpdateFilterTags());
        UpdateSearchOptions();
        this.WhenAnyValue(x => x.SearchIgnoreCase,
            x => x.IsSearchFlatten,
            x => x.IsSearchRegex, 
            x => x.TagFilterMode, 
            x => x.FilterTags)
            .Skip(1)
            .Throttle(SearchThrottleTime, RxApp.MainThreadScheduler)
            .Subscribe(_ => UpdateSearchOptions());

        this.WhenAnyValue(x => x.SearchText)
            .Skip(1)
            .Throttle(SearchThrottleTime, RxApp.MainThreadScheduler)
            .Subscribe(_ => RefreshNodes());
        this.WhenAnyValue(x => x.CurrentGroup, x => x.SortMethod, x => x.IsAscendingOrder, x => x.SearchOptions)
            .Skip(1)
            .Subscribe(_ => RefreshNodes());

        this.WhenActivated((CompositeDisposable disposables) =>
        {
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
                .DisposeWith(disposables);

            _nodeMovedSubject.Subscribe(_ => RefreshNodes())
                .DisposeWith(disposables);

            UpdateExistingTags();
            AddonRoot.CustomTags.ObserveCollectionChanges()
                .Subscribe(_ => UpdateExistingTags())
                .DisposeWith(disposables);

            var nodeMovedListener = (AddonNode node) =>
            {
                _nodeMovedSubject.OnNext(node);
            };
            AddonRoot.DescendantNodeMoved += nodeMovedListener;

            Disposable.Create(() =>
            {
                DisposeNodesSubscription();

                AddonRoot.DescendantNodeMoved -= nodeMovedListener;
            }).DisposeWith(disposables);

            Refresh();
        });
    }

    public AddonRoot AddonRoot => _addonRoot;

    public AddonGroup? CurrentGroup
    {
        get => _currentGroup;
        private set => this.RaiseAndSetIfChanged(ref _currentGroup, value);
    }

    public ReadOnlyObservableCollection<AddonNode>? Nodes
    {
        get => _nodes;
        private set => this.RaiseAndSetIfChanged(ref _nodes, value);
    }

    public ReadOnlyObservableCollection<AddonNodeListItemViewModel> NodeViewModels
    {
        get => _nodeViewModels;
        private set => this.RaiseAndSetIfChanged(ref _nodeViewModels, value);
    }

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

    public IEnumerable<AddonNode> MovingNodes
    {
        get => _movingNodes;
        private set => this.RaiseAndSetIfChanged(ref _movingNodes, value);
    }

    public string? MovingNodeNames => _movingNodeNames.Value;

    public ObservableCollection<AddonNodeListItemViewModel> Selection => _selection;

    public IEnumerable<AddonNode> SelectedNodes
    {
        get
        {
            var selection = Selection;
            foreach (var viewModel in selection)
            {
                if (viewModel.Addon is { } addon)
                {
                    yield return addon;
                }
            }
        }
    }

    public bool HasSelection => _hasSelection.Value;

    public bool IsSingleSelection => _isSingleSelection.Value;

    public bool IsMultipleSelection => _isMultipleSelection.Value;

    public string SelectionNames => _selectionNames.Value;

    public AddonNodeViewModel? ActiveAddonNodeViewModel => _activeAddonNodeViewModel.Value;

    public bool IsAddonNodeViewEnabled
    {
        get => _isAddonNodeViewEnabled;
        set => this.RaiseAndSetIfChanged(ref _isAddonNodeViewEnabled, value);
    }

    public AddonNodeListItemViewKind ListItemViewKind
    {
        get => _listItemViewKind;
        set => this.RaiseAndSetIfChanged(ref _listItemViewKind, value);
    }

    public bool IsGridView => _isGridView.Value;

    public bool IsTileView => _isTileView.Value;

    public double TileViewSize => _tileViewSize.Value;

    public IEnumerable<AddonNodeNavBarItemViewModel> NavBarItemViewModels => _navBarItemViewModels.Value;


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

    public ReactiveCommand<Unit, Unit> NewWorkshopAddonCommand { get; }

    public ReactiveCommand<Unit, Unit> NewWorkshopCollectionCommand { get; }

    public ReactiveCommand<bool, Unit> DeleteCommand { get; }

    public ReactiveCommand<Unit, Unit> SetAutoUpdateStrategyToDefaultRecursivelyCommand { get; }

    public ReactiveCommand<Unit, Unit> SetAutoUpdateStrategyToEnabledRecursivelyCommand { get; }

    public ReactiveCommand<Unit, Unit> SetAutoUpdateStrategyToDisabledRecursivelyCommand { get; }


    public Interaction<bool, bool> ConfirmDeleteInteraction { get; } = new();

    public Interaction<Exception, Unit> ReportExceptionInteraction { get; } = new();

    public Interaction<string, ErrorOperationReply> ReportInvalidMoveInteraction { get; } = new();

    public Interaction<string, ErrorOperationReply> ReportNameExistsForMoveInteraction { get; } = new();

    public Interaction<(string, Exception), ErrorOperationReply> ReportExceptionForMoveInteraction { get; } = new();

    public Interaction<IEnumerable<(Task, string)>, Unit> ShowDeletionProgressInteraction { get; } = new();

    public Interaction<(AddonRoot, AddonGroup?), Unit> ShowNewWorkshopCollectionWindowInteraction { get; } = new();


    public ViewModelActivator Activator { get; } = new();

    public bool SelectNode(AddonNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        Selection.Clear();
        foreach (var nodeViewModel in NodeViewModels)
        {
            if (nodeViewModel.Addon == node)
            {
                Selection.Add(nodeViewModel);
                return true;
            }
        }
        return false;
    }

    public int SelectNodes(IEnumerable<AddonNode>? nodes)
    {
        Selection.Clear();
        if (nodes is null)
        {
            return 0;
        }
        var nodeSet = new HashSet<AddonNode>(nodes);
        if (nodeSet.Count == 0)
        {
            return 0;
        }
        int count = 0;
        foreach (var nodeViewModel in NodeViewModels)
        {
            if (nodeViewModel.Addon is { } node)
            {
                if (nodeSet.Contains(node))
                {
                    Selection.Add(nodeViewModel);
                    count++;
                }
            }
        }
        return count;
    }

    public AddonGroup NewGroup()
    {
        var group = AddonNode.Create<AddonGroup>(AddonRoot, CurrentGroup);
        group.Name = group.Parent.GetUniqueNodeName(Texts.UnnamedGroup);
        Directory.CreateDirectory(group.FullFilePath);
        SelectNode(group);
        return group;
    }

    public WorkshopVpkAddon NewWorkshopAddon()
    {
        var addon = AddonNode.Create<WorkshopVpkAddon>(AddonRoot, CurrentGroup);
        addon.Name = addon.Parent.GetUniqueNodeName(Texts.UnnamedWorkshopAddon);
        addon.RequestAutoSetName = true;
        SelectNode(addon);
        return addon;
    }

    public async Task Delete(bool retainFile)
    {
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
            var nodeName = node.NodePath;
            if (retainFile)
            {
                operations[i] = (node.DestroyAsync(), nodeName);
            }
            else
            {
                operations[i] = (node.DestroyWithFileAsync(), nodeName);
            }
        }
        await ShowDeletionProgressInteraction.Handle(operations);
    }

    public void Move()
    {
        MovingNodes = SelectedNodes.ToArray();
    }

    public async Task MoveHere()
    {
        var movingNodes = MovingNodes;
        bool skipAll = false;
        var targetGroup = CurrentGroup;
        foreach (var node in movingNodes)
        {
            if (!node.IsValid)
            {
                continue;
            }

            ErrorOperationReply? reply = null;
            if (!node.CanMoveTo(targetGroup))
            {
                if (!skipAll)
                {
                    reply = await ReportInvalidMoveInteraction.Handle(node.NodePath);
                }
            }
            else
            {
                try
                {
                    node.MoveTo(targetGroup);
                }
                catch (AddonNameExistsException)
                {
                    if (!skipAll)
                    {
                        reply = await ReportNameExistsForMoveInteraction.Handle(node.NodePath);
                    }
                }
                catch (Exception ex)
                {
                    if (!skipAll)
                    {
                        reply = await ReportExceptionForMoveInteraction.Handle((node.NodePath, ex));
                    }
                }
            }

            if (reply != null)
            {
                bool abort = false;
                switch (reply)
                {
                    case ErrorOperationReply.Abort:
                        abort = true;
                        break;
                    case ErrorOperationReply.Skip:
                        break;
                    case ErrorOperationReply.SkipAll:
                        skipAll = true;
                        break;
                    default:
                        throw new Exception("invalid enum value");
                }
                if (abort)
                {
                    break;
                }
            }
        }
        MovingNodes = [];
    }

    public void CancelMove()
    {
        MovingNodes = [];
    }

    public void SetSelectionEnabled(bool enabled)
    {
        var selectedNodes = SelectedNodes.ToArray();
        foreach (var node in selectedNodes)
        {
            node.IsEnabled = enabled;
        }
    }

    public void SetSelectionEnabledRecursively(bool enabled)
    {
        var selectedNodes = SelectedNodes.ToArray();
        foreach (var node in selectedNodes)
        {
            foreach (var node1 in node.GetSelfAndDescendantsByDfsPreorder())
            {
                node1.IsEnabled = enabled;
            }
        }
    }

    public void GotoGroup(AddonGroup? group)
    {
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
        if (_currentGroup == null)
        {
            return;
        }
        GotoGroup(_currentGroup.Group);
    }

    public void GotoNode(AddonNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!node.IsValid || node.Root != AddonRoot)
        {
            return;
        }

        CurrentGroup = node.Group;
        SelectNode(node);
    }

    public void ToggleOrderDirection()
    {
        IsAscendingOrder = !IsAscendingOrder;
    }

    public void ClearSearchText()
    {
        SearchText = "";
    }

    public void Refresh()
    {
        RefreshNodes();
    }

    public void SetAutoUpdateStrategyRecursively(AutoUpdateStrategy strategy)
    {
        var selectedNodes = SelectedNodes.ToArray();
        foreach (var addon in selectedNodes.SelectMany(addon => addon.GetSelfAndDescendants()))
        {
            if (addon is WorkshopVpkAddon workshopVpkAddon)
            {
                workshopVpkAddon.AutoUpdateStrategy = strategy;
            }
        }
    }

    private void RefreshNodes()
    {
        var selectedNodes = SelectedNodes.ToArray();

        DisposeNodesSubscription();
        var disposables = new CompositeDisposable();
        _nodesSubscription = disposables;

        var rawNodes = Search(CurrentGroup?.Children ?? AddonRoot.Nodes);
        var observableChangeSet = rawNodes.ToObservableChangeSet();
        if (SortMethod is not AddonNodeSortMethod.None)
        {
            observableChangeSet = observableChangeSet.Sort(new AddonNodeComparer(SortMethod, IsAscendingOrder));
        }
        observableChangeSet.Bind(out ReadOnlyObservableCollection<AddonNode> nodes)
            .Subscribe()
            .DisposeWith(disposables);
        Nodes = nodes;
        nodes.ToObservableChangeSet()
            .Select(node => new AddonNodeListItemViewModel(node))
            .Bind(out var nodeViewModels)
            .Subscribe()
            .DisposeWith(disposables);
        NodeViewModels = nodeViewModels;

        SelectNodes(selectedNodes);
    }

    private void DisposeNodesSubscription()
    {
        NodeViewModels = ReadOnlyObservableCollection<AddonNodeListItemViewModel>.Empty;
        Nodes = null;
        if (_nodesSubscription is not null)
        {
            _nodesSubscription.Dispose();
            _nodesSubscription = null;
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
}
