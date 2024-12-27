using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using L4D2AddonAssistant.Resources;
using System.Threading.Tasks;
using System.Reactive.Disposables;
using DynamicData.Binding;
using DynamicData;
using System.Threading;
using Avalonia.Threading;
using System.Collections.Specialized;

namespace L4D2AddonAssistant.ViewModels
{
    public class AddonNodeExplorerViewModel : ViewModelBase, IActivatableViewModel
    {
        private readonly static TimeSpan SearchThrottleTime = TimeSpan.FromSeconds(0.5);

        private readonly AddonRoot _addonRoot;
        private readonly IAppWindowManager _windowManager;

        private AddonNodeContainerViewModel _containerViewModel;

        private ReadOnlyObservableCollection<AddonNode>? _nodes = null;
        private IDisposable? _nodesSubscription = null;

        private IEnumerable<string>? _existingTags = null;

        private string _searchText = "";
        private readonly ObservableAsPropertyHelper<bool> _isSearchTextClearable;
        private ReadOnlyObservableCollection<AddonNode>? _searchResultNodes = null;
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

        private AddonNodeSortMethod _sortMethod = AddonNodeSortMethod.Default;
        private bool _isAscendingOrder = true;
        private readonly IObservable<IComparer<AddonNode>> _observableComparer;

        private AddonGroup? _currentGroup = null;

        private IEnumerable<AddonNode>? _movingNodes = null;
        private readonly ObservableAsPropertyHelper<string?> _movingNodeNames;

        private IReadOnlyList<AddonNodeListItemViewModel>? _selection = null;
        private readonly ObservableAsPropertyHelper<int> _selectionCount;
        private readonly ObservableAsPropertyHelper<bool> _isSingleSelection;
        private readonly ObservableAsPropertyHelper<bool> _isMultipleSelection;
        private readonly ObservableAsPropertyHelper<AddonNodeViewModel?> _singleSelection;
        private readonly ObservableAsPropertyHelper<string?> _selectionNames;

        private readonly ObservableAsPropertyHelper<IEnumerable<AddonNodeNavBarItemViewModel>> _navBarItemViewModels;

        public AddonNodeExplorerViewModel(AddonRoot addonRoot, IAppWindowManager windowManager)
        {
            ArgumentNullException.ThrowIfNull(addonRoot);
            ArgumentNullException.ThrowIfNull(windowManager);

            _addonRoot = addonRoot;
            _windowManager = windowManager;
            _containerViewModel = new();
            Activator = new();

            _observableComparer = this.WhenAnyValue(x => x.SortMethod, x => x.IsAscendingOrder)
                .Select(((AddonNodeSortMethod SortMethod, bool IsAscendingOrder) args) => new AddonNodeComparer(args.SortMethod, args.IsAscendingOrder));

            _isSearchTextClearable = this.WhenAnyValue(x => x.SearchText)
                .Select(searchText => searchText.Length > 0)
                .ToProperty(this, nameof(IsSearchTextClearable));

            _movingNodeNames = this.WhenAnyValue(x => x.MovingNodes)
                .Select(movingNodes =>
                {
                    if (movingNodes == null)
                    {
                        return null;
                    }
                    return string.Join(", ", movingNodes.Select(node => node.Name));
                })
                .ToProperty(this, nameof(MovingNodeNames));

            _selectionCount = this.WhenAnyValue(x => x.Selection)
                .Select(selection => selection?.Count ?? 0)
                .ToProperty(this, nameof(SelectionCount));
            _isSingleSelection = this.WhenAnyValue(x => x.SelectionCount)
                .Select(count => count == 1)
                .ToProperty(this, nameof(IsSingleSelection));
            _isMultipleSelection = this.WhenAnyValue(x => x.SelectionCount)
                .Select(count => count > 1)
                .ToProperty(this, nameof(IsMultipleSelection));
            _singleSelection = this.WhenAnyValue(x => x.Selection)
                .Select(selection =>
                {
                    if (selection != null && selection.Count == 1)
                    {
                        return AddonNodeViewModel.Create(selection[0].AddonNode);
                    }
                    else
                    {
                        return null;
                    }
                })
                .ToProperty(this, nameof(SingleSelection));
            _selectionNames = this.WhenAnyValue(x => x.Selection)
                .Select(selection =>
                {
                    if (selection == null)
                    {
                        return null;
                    }
                    return string.Join(", ", selection.Select(viewModel => viewModel.AddonNode.Name));
                })
                .ToProperty(this, nameof(SelectionNames));

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

            var hasSelection = this.WhenAnyValue(x => x.SelectionCount).Select((count) => count > 0);

            GotoParentCommand = ReactiveCommand.Create(GotoParent,
                this.WhenAnyValue(x => x.CurrentGroup)
                .Select(group => group != null));
            GotoRootCommand = ReactiveCommand.Create(() => GotoGroup(null));

            EnableCommand = ReactiveCommand.Create(() => SetSelectionEnabled(true), hasSelection);
            DisableCommand = ReactiveCommand.Create(() => SetSelectionEnabled(false), hasSelection);
            EnableRecursivelyCommand = ReactiveCommand.Create(() => SetSelectionEnabledRecursively(true), hasSelection);
            DisableRecursivelyCommand = ReactiveCommand.Create(() => SetSelectionEnabledRecursively(false), hasSelection);

            MoveCommand = ReactiveCommand.Create(Move,
                this.WhenAnyValue(x => x.MovingNodes, x => x.SelectionCount)
                .Select(((IEnumerable<AddonNode>? MovingNodes, int Count) args) => args.MovingNodes == null && args.Count > 0));
            MoveHereCommand = ReactiveCommand.CreateFromTask(MoveHere,
                this.WhenAnyValue(x => x.MovingNodes)
                .Select((movingNodes) => movingNodes != null));
            CancelMoveCommand = ReactiveCommand.Create(CancelMove,
                this.WhenAnyValue(x => x.MovingNodes)
                .Select((movingNodes) => movingNodes != null));

            NewGroupCommand = ReactiveCommand.Create(() => { NewGroup(); });
            NewWorkshopAddonCommand = ReactiveCommand.Create(() => { NewWorkshopAddon(); });
            NewWorkshopCollectionCommand = ReactiveCommand.Create(() => _windowManager.OpenNewWorkshopCollectionWindow(_addonRoot, CurrentGroup));

            DeleteCommand = ReactiveCommand.CreateFromTask<bool>(Delete, hasSelection);

            // Ensure the validity of the current group.
            this.WhenAnyValue(x => x.CurrentGroup!.IsValid)
                .Subscribe((isValid) =>
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
                            if (current.Root != _addonRoot)
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
                });

            this.WhenAnyValue(x => x.Nodes)
                .Subscribe((nodes) => _containerViewModel.Nodes = nodes);

            this.WhenAnyValue(x => x.SelectedTags, x => x.IsFilterEnabled)
                .Throttle(TimeSpan.FromSeconds(0.1))
                .Subscribe(_ =>
                {
                    if (IsFilterEnabled)
                    {
                        FilterTags = new HashSet<string>(SelectedTags);
                    }
                    else
                    {
                        FilterTags = null;
                    }
                });
            this.WhenAnyValue(x => x.SearchIgnoreCase,
                x => x.IsSearchFlatten,
                x => x.IsSearchRegex, 
                x => x.TagFilterMode, 
                x => x.FilterTags)
                .Throttle(SearchThrottleTime)
                .Subscribe(_ => UpdateSearchOptions());
            this.WhenAnyValue(x => x.SearchText)
                .Throttle(SearchThrottleTime)
                .Subscribe(_ => RefreshSearch());
            this.WhenAnyValue(x => x.CurrentGroup, x => x.SearchOptions)
                .Subscribe(_ => RefreshSearch());

            this.WhenActivated((CompositeDisposable disposables) =>
            {
                this.WhenAnyValue(x => x.CurrentGroup, x => x.SearchResultNodes, x => x.SortMethod)
                .Subscribe((args) =>
                {
                    var currentGroup = args.Item1;
                    var searchResultNodes = args.Item2;
                    
                    var rawNodes = searchResultNodes ?? (currentGroup?.Children ?? _addonRoot.Nodes);
                    DisposeNodesSubscription();
                    _nodesSubscription = rawNodes.ToObservableChangeSet()
                    .Sort(_observableComparer)
                    .Bind(out ReadOnlyObservableCollection<AddonNode> nodes)
                    .Subscribe();
                    Nodes = nodes;
                })
                .DisposeWith(disposables);

                NotifyCollectionChangedEventHandler onCustomTagsChanged = (sender, e) =>
                {
                    UpdateExistingTags();
                };
                ((INotifyCollectionChanged)_addonRoot.CustomTags).CollectionChanged += onCustomTagsChanged;
                UpdateExistingTags();

                Disposable.Create(() =>
                {
                    DisposeNodesSubscription();

                    ((INotifyCollectionChanged)_addonRoot.CustomTags).CollectionChanged -= onCustomTagsChanged;
                })
                .DisposeWith(disposables);
            });
        }

        public event Action<IEnumerable<AddonNode>>? SetSelection = null;

        public AddonRoot Root => _addonRoot;

        public AddonNodeContainerViewModel ContainerViewModel => _containerViewModel;

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

        public ReadOnlyObservableCollection<AddonNode>? SearchResultNodes
        {
            get => _searchResultNodes;
            private set => this.RaiseAndSetIfChanged(ref _searchResultNodes, value);
        }

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

        public IEnumerable<AddonNode>? MovingNodes
        {
            get => _movingNodes;
            private set => this.RaiseAndSetIfChanged(ref _movingNodes, value);
        }

        public string? MovingNodeNames => _movingNodeNames.Value;

        public IReadOnlyList<AddonNodeListItemViewModel>? Selection
        {
            get => _selection;
            set
            {
                _selection = value;
                this.RaisePropertyChanged();
            }
        }

        public IEnumerable<AddonNode> SelectedNodes
        {
            get
            {
                var selection = Selection;
                if (selection == null)
                {
                    return [];
                }
                return selection.Select((viewModel) => viewModel.AddonNode);
            }
        }

        public int SelectionCount => _selectionCount.Value;

        public bool IsSingleSelection => _isSingleSelection.Value;

        public bool IsMultipleSelection => _isMultipleSelection.Value;

        public AddonNodeViewModel? SingleSelection => _singleSelection.Value;

        public string? SelectionNames => _selectionNames.Value;

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


        public Interaction<bool, bool> ConfirmDeleteInteraction { get; } = new();

        public Interaction<Exception, Unit> ReportExceptionInteraction { get; } = new();

        public Interaction<string, ErrorOperationReply> ReportInvalidMoveInteraction { get; } = new();

        public Interaction<string, ErrorOperationReply> ReportNameExistsForMoveInteraction { get; } = new();

        public Interaction<(string, Exception), ErrorOperationReply> ReportExceptionForMoveInteraction { get; } = new();


        public ViewModelActivator Activator { get; }

        public AddonGroup NewGroup()
        {
            var group = new AddonGroup(_addonRoot, CurrentGroup);
            group.Name = group.Parent.GetUniqueNodeName(Texts.UnnamedGroup);
            Directory.CreateDirectory(group.FullFilePath);
            SetSelection?.Invoke([group]);
            return group;
        }

        public WorkshopVpkAddon NewWorkshopAddon()
        {
            var addon = new WorkshopVpkAddon(_addonRoot, CurrentGroup);
            addon.Name = addon.Parent.GetUniqueNodeName(Texts.UnnamedWorkshopAddon);
            addon.RequestAutoSetName = true;
            SetSelection?.Invoke([addon]);
            return addon;
        }

        public async Task Delete(bool retainFile)
        {
            bool confirm = await ConfirmDeleteInteraction.Handle(retainFile);
            if (!confirm)
            {
                return;
            }
            foreach (var node in SelectedNodes)
            {
                if (!node.IsValid)
                {
                    continue;
                }
                // TODO
                if (retainFile)
                {
                    node.DestroyAsync();
                }
                else
                {
                    node.DestroyWithFileAsync();
                }
            }
        }

        public void Move()
        {
            if (_movingNodes != null || SelectionCount == 0)
            {
                return;
            }
            MovingNodes = SelectedNodes.ToArray();
        }

        public async Task MoveHere()
        {
            if (_movingNodes == null)
            {
                return;
            }
            bool skipAll = false;
            var targetGroup = CurrentGroup;
            foreach (var node in _movingNodes)
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
                        reply = await ReportInvalidMoveInteraction.Handle(node.FullName);
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
                            reply = await ReportNameExistsForMoveInteraction.Handle(node.FullName);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (!skipAll)
                        {
                            reply = await ReportExceptionForMoveInteraction.Handle((node.FullName, ex));
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
            MovingNodes = null;
        }

        public void CancelMove()
        {
            MovingNodes = null;
        }

        public void SetSelectionEnabled(bool enabled)
        {
            foreach (var node in SelectedNodes)
            {
                node.IsEnabled = enabled;
            }
        }

        public void SetSelectionEnabledRecursively(bool enabled)
        {
            foreach (var node in SelectedNodes)
            {
                foreach (var node1 in node.GetSelfAndDescendantsByDfsPreorder())
                {
                    node1.IsEnabled = enabled;
                }
            }
        }

        public void GotoGroup(AddonGroup? group)
        {
            if (group != null)
            {
                if (!group.IsValid || group.Root != _addonRoot)
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
            if (ContainerViewModel.NodeViewModels != null)
            {
                foreach (var viewModel in ContainerViewModel.NodeViewModels)
                {
                    viewModel.Refresh();
                }
            }
            SingleSelection?.Refresh();
        }

        private void DisposeNodesSubscription()
        {
            if (_nodesSubscription != null)
            {
                _nodesSubscription.Dispose();
                _nodesSubscription = null;
                _nodes = null;
            }
        }

        private void RefreshSearch()
        {
            CancelSearch();
            if (SearchText.Length == 0 && !IsFilterEnabled)
            {
                SearchResultNodes = null;
            }
            else
            {
                StartSearch();
            }
        }

        private async void StartSearch()
        {
            _searchCts = new();
            var searchId = new object();
            _currentSearchId = searchId;
            IsSearching = true;
            IEnumerable<AddonNode> addonNodes = CurrentGroup?.Children ?? Root.Nodes;
            var resultNodes = new ObservableCollection<AddonNode>();
            SearchResultNodes = new ReadOnlyObservableCollection<AddonNode>(resultNodes);
            Action<AddonNode> consumer = (addonNode) => Dispatcher.UIThread.Post(() => resultNodes.Add(addonNode));
            try
            {
                await AddonNodeSearchUtils.SearchAsync(addonNodes, SearchText, SearchOptions, consumer, _searchCts.Token);
            }
            catch (OperationCanceledException) { }
            if (_currentSearchId == searchId)
            {
                if (_searchCts != null)
                {
                    _searchCts.Dispose();
                    _searchCts = null;
                }
                _currentSearchId = null;
                IsSearching = false;
            }
        }

        private void CancelSearch()
        {
            if (_searchCts != null)
            {
                _searchCts.Cancel();
                _searchCts.Dispose();
                _searchCts = null;
                _currentSearchId = null;
                IsSearching = false;
            }
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
    }
}
