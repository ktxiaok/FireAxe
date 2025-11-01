using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.ReactiveUI;
using FireAxe.Resources;
using FireAxe.ViewModels;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;

namespace FireAxe.Views;

public partial class AddonNodeExplorerView : ReactiveUserControl<AddonNodeExplorerViewModel>
{
    public static readonly StyledProperty<bool> IsAddonNodeViewEnabledProperty =
        AvaloniaProperty.Register<AddonNodeExplorerView, bool>(nameof(IsAddonNodeViewEnabled), defaultValue: true);

    public AddonNodeExplorerView()
    {
        InitializeComponent();

        this.WhenActivated((CompositeDisposable disposables) =>
        {
            var topLevel = TopLevel.GetTopLevel(this)!;
            topLevel.KeyDown += HandleKeyDown;

            Disposable.Create(() =>
            {
                topLevel.KeyDown -= HandleKeyDown;
            }).DisposeWith(disposables);
        });

        this.RegisterViewModelConnection(ConnectViewModel);

        DoubleTapped += AddonNodeExplorerView_DoubleTapped;
        PointerReleased += AddonNodeExplorerView_PointerReleased;

        searchOptionsButton.Click += (sender, e) =>
        {
            searchOptionsControl.IsVisible = !searchOptionsControl.IsVisible;
        };
    }

    public bool IsAddonNodeViewEnabled
    {
        get => GetValue(IsAddonNodeViewEnabledProperty);
        set => SetValue(IsAddonNodeViewEnabledProperty, value);
    }

    private void ConnectViewModel(AddonNodeExplorerViewModel viewModel, CompositeDisposable disposables)
    {
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
    }

    private void AddonNodeExplorerView_DoubleTapped(object? sender, TappedEventArgs e)
    {
        var viewModel = ViewModel;
        if (viewModel != null)
        {
            if (e.Source is Control sourceControl)
            {
                if (sourceControl.DataContext is AddonNodeListItemViewModel listItemViewModel)
                {
                    if (listItemViewModel.Addon is AddonGroup addonGroup)
                    {
                        viewModel.GotoGroup(addonGroup);
                        e.Handled = true;
                    }
                }
            }
        }
    }

    private void AddonNodeExplorerView_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.XButton1)
        {
            var viewModel = ViewModel;
            if (viewModel != null)
            {
                viewModel.GotoParent();
            }
        }
    }

    private async void HandleKeyDown(object? sender, KeyEventArgs e)
    {
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
        }
    }
}
