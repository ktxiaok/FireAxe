using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FireAxe.ViewModels;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace FireAxe.Views;

public partial class AddonProblemListWindow : ReactiveWindow<AddonProblemListViewModel>
{
    public AddonProblemListWindow()
    {
        InitializeComponent();

        Closed += AddonProblemListWindow_Closed;

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

    private void AddonProblemListWindow_Closed(object? sender, EventArgs e)
    {
        if (ViewModel is { } viewModel)
        {
            ViewModel = null;
            viewModel.Dispose();
        }
    }
}