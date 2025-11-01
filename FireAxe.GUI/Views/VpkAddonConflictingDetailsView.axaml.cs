using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using FireAxe.ViewModels;
using ReactiveUI;
using FireAxe.Resources;

namespace FireAxe.Views;

public partial class VpkAddonConflictingDetailsView : ReactiveUserControl<VpkAddonConflictingDetailsViewModel>
{
    public VpkAddonConflictingDetailsView()
    {
        InitializeComponent();

        this.WhenActivated((CompositeDisposable disposables) =>
        {

        });

        this.RegisterViewModelConnection(ConnectViewModel);
    }

    private void ConnectViewModel(VpkAddonConflictingDetailsViewModel viewModel, CompositeDisposable disposables)
    {
        viewModel.ConfirmIgnoreAllConflictingFilesInteraction.RegisterHandler(async context =>
        {
            context.SetOutput(await CommonMessageBoxes.Confirm(this.GetRootWindow(), Texts.ConfirmIgnoreAllConflictingFiles, Texts.Warning));
        }).DisposeWith(disposables);
        viewModel.ConfirmRemoveAllConflictIgnoringFilesInteraction.RegisterHandler(async context =>
        {
            context.SetOutput(await CommonMessageBoxes.Confirm(this.GetRootWindow(), Texts.ConfirmRemoveAllConflictIgnoringFiles, Texts.Warning));
        }).DisposeWith(disposables);
    }
}