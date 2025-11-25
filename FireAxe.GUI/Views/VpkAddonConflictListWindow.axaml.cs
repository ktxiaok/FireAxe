using System;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FireAxe.ViewModels;
using ReactiveUI;
using System.Reactive;
using ReactiveUI.Avalonia;
using System.Reactive.Disposables.Fluent;

namespace FireAxe.Views;

public partial class VpkAddonConflictListWindow : ReactiveWindow<VpkAddonConflictListViewModel>
{
    public VpkAddonConflictListWindow()
    {
        InitializeComponent();

        Closed += VpkAddonConflictListWindow_Closed;

        this.WhenActivated((CompositeDisposable disposables) =>
        {

        });

        this.RegisterViewModelConnection((viewModel, disposables) =>
        {
            viewModel.RegisterInvalidHandler(Close)
                .DisposeWith(disposables);
            if (!viewModel.IsValid)
            {
                return;
            }

            viewModel.ShowExceptionInteraction.RegisterHandler(async context =>
            {
                await CommonMessageBoxes.ShowException(this, context.Input);
                context.SetOutput(Unit.Default);
            }).DisposeWith(disposables);
        });
    }

    private void VpkAddonConflictListWindow_Closed(object? sender, EventArgs e)
    {
        if (DataContext is VpkAddonConflictListViewModel viewModel)
        {
            DataContext = null;
            viewModel.Dispose();
        }
    }
}