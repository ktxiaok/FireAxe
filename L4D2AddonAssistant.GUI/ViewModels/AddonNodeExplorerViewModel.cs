using Avalonia;
using HarfBuzzSharp;
using Microsoft.Extensions.DependencyInjection;
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
using System.Runtime.InteropServices;

namespace L4D2AddonAssistant.ViewModels
{
    public class AddonNodeExplorerViewModel : ViewModelBase, IActivatableViewModel
    {
        private readonly AddonRoot _root;

        private AddonNodeContainerViewModel _containerViewModel;

        private bool _hasOverridingNodes = false;
        private AddonGroup? _currentGroup = null;

        private IEnumerable<AddonNode>? _movingNodes = null;

        private IReadOnlyList<AddonNodeSimpleViewModel>? _selection = null;
        private readonly ObservableAsPropertyHelper<int> _selectionCount;
        private readonly ObservableAsPropertyHelper<bool> _isSingleSelection;
        private readonly ObservableAsPropertyHelper<bool> _isMultipleSelection;
        private readonly ObservableAsPropertyHelper<AddonNodeViewModel?> _singleSelection;
        private readonly ObservableAsPropertyHelper<string?> _selectionNames;

        private readonly ObservableAsPropertyHelper<IEnumerable<AddonNodeNavBarItemViewModel>> _navBarItemViewModels;

        public AddonNodeExplorerViewModel(AddonRoot root)
        {
            ArgumentNullException.ThrowIfNull(root);

            _root = root;
            _containerViewModel = new();
            Activator = new();

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

            NewGroupCommand = ReactiveCommand.Create(NewGroup);

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
                            if (current.Root != _root)
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

            ResetNodes();
        }

        public event Action<IEnumerable<AddonNode>>? SetSelection = null;

        public AddonRoot Root => _root;

        public AddonNodeContainerViewModel ContainerViewModel => _containerViewModel;

        public AddonGroup? CurrentGroup
        {
            get => _currentGroup;
            private set => this.RaiseAndSetIfChanged(ref _currentGroup, value);
        }

        public IEnumerable<AddonNode>? MovingNodes
        {
            get => _movingNodes;
            private set => this.RaiseAndSetIfChanged(ref _movingNodes, value);
        }

        public IReadOnlyList<AddonNodeSimpleViewModel>? Selection
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

        public ReactiveCommand<bool, Unit> DeleteCommand { get; }


        public Interaction<bool, bool> ConfirmDeleteInteraction { get; } = new();

        public Interaction<Exception, Unit> ReportExceptionInteraction { get; } = new();

        public Interaction<string, ErrorOperationReply> ReportInvalidMoveInteraction { get; } = new();

        public Interaction<string, ErrorOperationReply> ReportNameExistsForMoveInteraction { get; } = new();

        public Interaction<(string, Exception), ErrorOperationReply> ReportExceptionForMoveInteraction { get; } = new();


        public ViewModelActivator Activator { get; }

        public void NewGroup()
        {
            var group = new AddonGroup(_root, CurrentGroup);
            group.Name = group.Parent.GetUniqueNodeName(Texts.UnnamedGroup);
            Directory.CreateDirectory(group.FullFilePath);
            SetSelection?.Invoke([group]);
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
                if (!retainFile)
                {
                    try
                    {
                        node.DeleteFile();
                    }
                    catch (Exception ex)
                    {
                        await ReportExceptionInteraction.Handle(ex);
                    }
                }
                node.Destroy();
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
                        reply = await ReportNameExistsForMoveInteraction.Handle(node.FullName);
                    }
                    catch (Exception ex)
                    {
                        reply = await ReportExceptionForMoveInteraction.Handle((node.FullName, ex));
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
                if (!group.IsValid || group.Root != _root)
                {
                    return;
                }
            }
            CurrentGroup = group;
            if (!_hasOverridingNodes)
            {
                ResetNodes();
            }
        }

        public void GotoParent()
        {
            if (_currentGroup == null)
            {
                return;
            }
            GotoGroup(_currentGroup.Group);
        }

        public void ResetNodes()
        {
            _containerViewModel.Nodes = _currentGroup?.Children ?? _root.Nodes;
            _hasOverridingNodes = false;
        }

        public void SetOverridingNodes(ReadOnlyObservableCollection<AddonNode> nodes)
        {
            _containerViewModel.Nodes = nodes;
            _hasOverridingNodes = true;
        }
    }
}
