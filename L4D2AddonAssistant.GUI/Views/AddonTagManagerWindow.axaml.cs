using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using L4D2AddonAssistant.ViewModels;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using L4D2AddonAssistant.Resources;
using System.Reactive;

namespace L4D2AddonAssistant.Views;

public partial class AddonTagManagerWindow : ReactiveWindow<AddonTagManagerViewModel>
{
    public AddonTagManagerWindow()
    {
        this.WhenAnyValue(x => x.ViewModel)
            .WhereNotNull()
            .Subscribe(viewModel => ConnectViewModel(viewModel));

        this.WhenActivated((CompositeDisposable disposables) =>
        {

        });

        InitializeComponent();
    }

    private void ConnectViewModel(AddonTagManagerViewModel viewModel)
    {
        viewModel.AddTagInteraction.RegisterHandler(async (context) =>
        {
            context.SetOutput(await CommonMessageBoxes.Input(this, Texts.InputTagNameMessage, Texts.NewTag, context.Input));
        });
        viewModel.RenameTagInteraction.RegisterHandler(async (context) =>
        {
            context.SetOutput(await CommonMessageBoxes.Input(this, Texts.InputTagNameMessage, Texts.RenameTag, context.Input));
        });
        viewModel.ShowInputCannotBeEmptyInteraction.RegisterHandler(async (context) =>
        {
            await CommonMessageBoxes.ShowInfo(this, Texts.InputCannotBeEmptyMessage, Texts.Error);
            context.SetOutput(Unit.Default);
        });
        viewModel.ShowTagExistInteraction.RegisterHandler(async (context) =>
        {
            await CommonMessageBoxes.ShowInfo(this, Texts.TagNameExistMessage, Texts.Error);
            context.SetOutput(Unit.Default);
        });
        viewModel.ConfirmDeleteTagCompletelyInteraction.RegisterHandler(async (context) =>
        {
            context.SetOutput(await CommonMessageBoxes.Confirm(this, Texts.ConfirmDeleteTagCompletely, Texts.ConfirmDelete));
        });
    }
}