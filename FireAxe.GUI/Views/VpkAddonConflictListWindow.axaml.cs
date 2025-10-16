using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using FireAxe.ViewModels;
using ReactiveUI;
using System.Reactive;

namespace FireAxe.Views;

public partial class VpkAddonConflictListWindow : ReactiveWindow<VpkAddonConflictListViewModel>
{
    public VpkAddonConflictListWindow()
    {
        this.WhenActivated((CompositeDisposable disposables) =>
        {

        });

        this.RegisterViewModelConnection((viewModel, disposables) =>
        {
            viewModel.RegisterInvalidHandler(() =>
            {
                Close();
            }).DisposeWith(disposables);

            viewModel.ShowExceptionInteraction.RegisterHandler(async context =>
            {
                await CommonMessageBoxes.ShowException(this, context.Input);
                context.SetOutput(Unit.Default);
            }).DisposeWith(disposables);
        });

        InitializeComponent();
    }
}