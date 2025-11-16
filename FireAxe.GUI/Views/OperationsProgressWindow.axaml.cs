using System;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using FireAxe.ViewModels;
using ReactiveUI;

namespace FireAxe.Views;

public partial class OperationsProgressWindow : ReactiveWindow<OperationsProgressViewModel>
{
    public OperationsProgressWindow()
    {
        InitializeComponent();

        Closing += OperationsProgressWindow_Closing;

        this.WhenActivated((CompositeDisposable disposables) =>
        {

        });

        this.RegisterViewModelConnection((viewModel, disposables) =>
        {
            viewModel.CloseRequested += Close;
            Disposable.Create(() => viewModel.CloseRequested -= Close).DisposeWith(disposables);
        });
    }

    public OperationsProgressView OperationsProgressView => operationsProgressView;

    private void OperationsProgressWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        var viewModel = ViewModel;
        if (viewModel is null)
        {
            return;
        }

        if (e.CloseReason is WindowCloseReason.WindowClosing or WindowCloseReason.OwnerWindowClosing)
        {
            if (!viewModel.IsDone)
            {
                e.Cancel = true;
            }
        }
    }
}