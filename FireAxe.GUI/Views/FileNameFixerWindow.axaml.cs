using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FireAxe.ViewModels;
using ReactiveUI;
using ReactiveUI.Avalonia;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;

namespace FireAxe.Views;

public partial class FileNameFixerWindow : ReactiveWindow<FileNameFixerViewModel>
{
    public FileNameFixerWindow()
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
        });
    }
}