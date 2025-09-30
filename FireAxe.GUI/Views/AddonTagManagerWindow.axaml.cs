using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using FireAxe.ViewModels;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using FireAxe.Resources;
using System.Reactive;

namespace FireAxe.Views;

public partial class AddonTagManagerWindow : ReactiveWindow<AddonTagManagerViewModel>
{
    public AddonTagManagerWindow()
    {
        this.WhenActivated((CompositeDisposable disposables) =>
        {

        });

        this.RegisterViewModelConnection(ConnectViewModel);

        InitializeComponent();
    }

    private void ConnectViewModel(AddonTagManagerViewModel viewModel, CompositeDisposable disposables)
    {
        viewModel.AddTagInteraction.RegisterHandler(async (context) =>
        {
            context.SetOutput(await CommonMessageBoxes.Input(this, Texts.InputTagNameMessage, Texts.NewTag, context.Input));
        }).DisposeWith(disposables);
        viewModel.RenameTagInteraction.RegisterHandler(async (context) =>
        {
            context.SetOutput(await CommonMessageBoxes.Input(this, Texts.InputTagNameMessage, Texts.RenameTag, context.Input));
        }).DisposeWith(disposables);
        viewModel.ShowInputCannotBeEmptyInteraction.RegisterHandler(async (context) =>
        {
            await CommonMessageBoxes.ShowInfo(this, Texts.InputCannotBeEmptyMessage, Texts.Error);
            context.SetOutput(Unit.Default);
        }).DisposeWith(disposables);
        viewModel.ShowTagExistInteraction.RegisterHandler(async (context) =>
        {
            await CommonMessageBoxes.ShowInfo(this, Texts.TagNameExistMessage, Texts.Error);
            context.SetOutput(Unit.Default);
        }).DisposeWith(disposables);
        viewModel.ConfirmDeleteTagCompletelyInteraction.RegisterHandler(async (context) =>
        {
            context.SetOutput(await CommonMessageBoxes.Confirm(this, Texts.ConfirmDeleteTagCompletely, Texts.ConfirmDelete));
        }).DisposeWith(disposables);
    }
}