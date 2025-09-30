using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using FireAxe.Resources;
using FireAxe.ViewModels;
using ReactiveUI;
using System;
using System.Reactive.Disposables;

namespace FireAxe.Views;

public partial class VpkAddonSectionView : ReactiveUserControl<VpkAddonViewModel>
{
    public VpkAddonSectionView()
    {
        this.WhenActivated((CompositeDisposable disposables) =>
        {
            
        });

        this.RegisterViewModelConnection(ConnectViewModel);

        InitializeComponent();
    }

    private void ConnectViewModel(VpkAddonViewModel viewModel, CompositeDisposable disposables)
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