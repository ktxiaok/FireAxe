using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FireAxe.ViewModels;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace FireAxe.Views;

public partial class AddonNodePickerWindow : ReactiveWindow<AddonNodePickerViewModel>
{
    public AddonNodePickerWindow()
    {
        InitializeComponent();

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

            void OnCloseRequested() => Close(viewModel.GetSelectedAddons());
            viewModel.CloseRequested += OnCloseRequested;
            Disposable.Create(() => viewModel.CloseRequested -= OnCloseRequested).DisposeWith(disposables);

            explorerView.IsSingleSelectionEnabled = !viewModel.AllowMultiple;
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        if (ViewModel is { } viewModel)
        {
            ViewModel = null;
            viewModel.Dispose();
        }
    }
}