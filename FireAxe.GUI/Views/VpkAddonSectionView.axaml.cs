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
    private CompositeDisposable? _viewModelConnection = null;

    public VpkAddonSectionView()
    {
        this.WhenActivated((CompositeDisposable disposables) =>
        {
            this.WhenAnyValue(x => x.ViewModel)
                .Subscribe(viewModel => ConnectViewModel(viewModel))
                .DisposeWith(disposables);

            Disposable.Create(() =>
            {
                DisconnectViewModel();
            }).DisposeWith(disposables);
        });
        InitializeComponent();
    }

    private void ConnectViewModel(VpkAddonViewModel? viewModel)
    {
        DisconnectViewModel();
        if (viewModel is null)
        {
            return;
        }

        var disposables = new CompositeDisposable();
        _viewModelConnection = disposables;

        viewModel.ConfirmIgnoreAllConflictingFilesInteraction.RegisterHandler(async context =>
        {
            context.SetOutput(await CommonMessageBoxes.Confirm(Utils.GetRootWindow(this), Texts.ConfirmIgnoreAllConflictingFiles, Texts.Warning));
        }).DisposeWith(disposables);
        viewModel.ConfirmRemoveAllConflictIgnoringFilesInteraction.RegisterHandler(async context =>
        {
            context.SetOutput(await CommonMessageBoxes.Confirm(Utils.GetRootWindow(this), Texts.ConfirmRemoveAllConflictIgnoringFiles, Texts.Warning));
        }).DisposeWith(disposables);
    }

    private void DisconnectViewModel()
    {
        Utils.DisposeAndSetNull(ref _viewModelConnection);
    }
}