using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using FireAxe.Resources;
using FireAxe.ViewModels;
using ReactiveUI;
using ReactiveUI.Avalonia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;

namespace FireAxe.Views;

public partial class AddonNodeExplorerView : ReactiveUserControl<AddonNodeExplorerViewModel>
{
    public static readonly StyledProperty<bool> IsSingleSelectionEnabledProperty =
        AvaloniaProperty.Register<AddonNodeExplorerView, bool>(nameof(IsSingleSelectionEnabled), defaultValue: false);

    public static readonly DirectProperty<AddonNodeExplorerView, SelectionMode> ExpectedSelectionModeProperty =
        AvaloniaProperty.RegisterDirect<AddonNodeExplorerView, SelectionMode>(nameof(ExpectedSelectionMode), t => t.ExpectedSelectionMode);

    private const int AddonNodeViewColumnIndex = 2;

    private SelectionMode _expectedSelectionMode = SelectionMode.Multiple;

    static AddonNodeExplorerView()
    {
        DoubleTappedEvent.AddClassHandler<AddonNodeExplorerView>((x, e) => x.AddonNodeExplorerView_DoubleTapped(e));
    }

    public AddonNodeExplorerView()
    {
        InitializeComponent();

        Focusable = true;

        this.WhenAnyValue(x => x.IsSingleSelectionEnabled)
            .Select(singleSelection => singleSelection ? SelectionMode.Single : SelectionMode.Multiple)
            .Subscribe(selectionMode => ExpectedSelectionMode = selectionMode);

        this.WhenActivated((CompositeDisposable disposables) =>
        {
            
        });

        this.RegisterViewModelConnection(ConnectViewModel); 

        searchOptionsButton.Click += (sender, e) =>
        {
            searchOptionsControl.IsVisible = !searchOptionsControl.IsVisible;
        };
    }

    public bool IsSingleSelectionEnabled
    {
        get => GetValue(IsSingleSelectionEnabledProperty);
        set => SetValue(IsSingleSelectionEnabledProperty, value);
    }

    public SelectionMode ExpectedSelectionMode
    {
        get => _expectedSelectionMode;
        private set => SetAndRaise(ExpectedSelectionModeProperty, ref _expectedSelectionMode, value);
    }

    private void ConnectViewModel(AddonNodeExplorerViewModel viewModel, CompositeDisposable disposables)
    {
        viewModel.WhenAnyValue(x => x.CurrentGroup)
            .Subscribe(_ => Focus())
            .DisposeWith(disposables);

        viewModel.WhenAnyValue(x => x.IsAddonNodeViewEnabled)
            .Subscribe(isAddonNodeViewEnabled =>
            {
                if (isAddonNodeViewEnabled)
                {
                    rootGrid.ColumnDefinitions[AddonNodeViewColumnIndex].MaxWidth = double.PositiveInfinity;
                }
                else
                {
                    rootGrid.ColumnDefinitions[AddonNodeViewColumnIndex].MaxWidth = 0;
                }
            })
            .DisposeWith(disposables);

        viewModel.WhenAnyValue(x => x.TileViewSize)
            .Subscribe(size =>
            {
                Resources["AddonTileSize"] = size;
            })
            .DisposeWith(disposables);

        viewModel.ReportExceptionInteraction.RegisterHandler(async (context) =>
        {
            await CommonMessageBoxes.ShowException(this.GetRootWindow(), context.Input);
            context.SetOutput(Unit.Default);
        }).DisposeWith(disposables);

        viewModel.ConfirmDeleteInteraction.RegisterHandler(async (context) =>
        {
            bool retainFile = context.Input;
            string message = Texts.ConfirmDeleteMessage;
            if (retainFile)
            {
                message += '\n' + Texts.RetainFileMessage;
            }
            bool result = await CommonMessageBoxes.Confirm(this.GetRootWindow(), message, Texts.ConfirmDelete);
            context.SetOutput(result);
        }).DisposeWith(disposables);

        viewModel.ReportInvalidMoveInteraction.RegisterHandler(async (context) =>
        {
            string message = string.Format(Texts.CantMoveItemWithName, context.Input) + '\n' + Texts.InvalidMoveMessage;
            var reply = await CommonMessageBoxes.GetErrorOperationReply(this.GetRootWindow(), message);
            context.SetOutput(reply);
        }).DisposeWith(disposables);

        viewModel.ReportNameExistsForMoveInteraction.RegisterHandler(async (context) =>
        {
            string message = string.Format(Texts.CantMoveItemWithName, context.Input) + '\n' + Texts.ItemNameExists;
            var reply = await CommonMessageBoxes.GetErrorOperationReply(this.GetRootWindow(), message);
            context.SetOutput(reply);
        }).DisposeWith(disposables);

        viewModel.ReportExceptionForMoveInteraction.RegisterHandler(async (context) =>
        {
            var input = context.Input;
            string name = input.Item1;
            Exception ex = input.Item2;
            string exceptionMessage = ObjectExplanationManager.Default.TryGet(ex) ?? ex.ToString();
            string message = string.Format(Texts.CantMoveItemWithName, name) + '\n' + exceptionMessage;
            var reply = await CommonMessageBoxes.GetErrorOperationReply(this.GetRootWindow(), message);
            context.SetOutput(reply);
        }).DisposeWith(disposables);

        viewModel.ShowDeletionProgressInteraction.RegisterHandler(context =>
        {
            var operations = context.Input;
            var progressViewModel = new OperationsProgressViewModel();

            var window = new OperationsProgressWindow
            {
                DataContext = progressViewModel,
                Title = Texts.DeletingAddons,
                OperationsProgressView =
                {
                    MessageTemplate = OperationsProgressViewMessageTemplates.Deletion
                }
            };
            window.Show();
            window.Activate();

            new TaskOperationsProgressNotifier(operations, false, progressViewModel);

            context.SetOutput(Unit.Default);
        }).DisposeWith(disposables);

        viewModel.ShowNewWorkshopCollectionWindowInteraction.RegisterHandler(context =>
        {
            var (addonRoot, addonGroup) = context.Input;
            var window = new NewWorkshopCollectionWindow
            {
                DataContext = new NewWorkshopCollectionViewModel(addonRoot, addonGroup)
            };
            window.Show();
            window.Activate();

            context.SetOutput(Unit.Default);
        }).DisposeWith(disposables);
    }
    
    private void AddonNodeExplorerView_DoubleTapped(TappedEventArgs e)
    {
        var viewModel = ViewModel;
        if (viewModel != null)
        {
            if (e.Source is Control sourceControl)
            {
                if (sourceControl.DataContext is AddonNodeListItemViewModel listItemViewModel)
                {
                    var addon = listItemViewModel.Addon;
                    if (addon is AddonGroup addonGroup)
                    {
                        viewModel.GotoGroup(addonGroup);
                        e.Handled = true;
                    }
                    else if (addon is RefAddonNode refAddon && refAddon.ActualSourceAddon is AddonGroup sourceAddonGroup)
                    {
                        viewModel.GotoGroup(sourceAddonGroup);
                        e.Handled = true;
                    }
                }
            }
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (e.InitialPressMouseButton == MouseButton.XButton1)
        {
            var viewModel = ViewModel;
            if (viewModel != null)
            {
                viewModel.GotoParent();
            }
        }
    }

    protected override async void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        var viewModel = ViewModel;
        if (viewModel == null)
        {
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (e.Key == Key.X)
            {
                e.Handled = true;
                viewModel.Move();
            }
            else if (e.Key == Key.V)
            {
                e.Handled = true;
                await viewModel.MoveHere();
            }
            else if (e.Key == Key.F)
            {
                e.Handled = true;
                searchTextBox.Focus();
            }
        }
        else
        {
            if (e.Key == Key.Delete)
            {
                e.Handled = true;
                await viewModel.Delete(false);
            }
            else if (e.Key == Key.F2)
            {
                if (viewModel.IsAddonNodeViewEnabled && viewModel.IsSingleSelection)
                {
                    addonNodeView.NameEditingControl.StartEditing();
                }
            }
        }
    }
}
