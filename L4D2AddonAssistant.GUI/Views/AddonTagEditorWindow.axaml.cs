using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using L4D2AddonAssistant.ViewModels;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using System.Reactive;
using L4D2AddonAssistant.Resources;

namespace L4D2AddonAssistant.Views;

public partial class AddonTagEditorWindow : ReactiveWindow<AddonTagEditorViewModel>
{
    public AddonTagEditorWindow()
    {
        this.WhenAnyValue(x => x.ViewModel)
            .WhereNotNull()
            .Subscribe(viewModel => ConnectViewModel(viewModel));

        this.WhenActivated((CompositeDisposable disposables) =>
        {

        });

        InitializeComponent();
    }

    private void ConnectViewModel(AddonTagEditorViewModel viewModel)
    {
        viewModel.AddTagInteraction.RegisterHandler(async (context) =>
        {
            var addTagWindow = new AddAddonTagWindow()
            {
                DataContext = new AddAddonTagViewModel(viewModel.AddonNode.Root)
            };
            addTagWindow.Input = context.Input;
            var result = await addTagWindow.ShowDialog<string>(this);
            context.SetOutput(result);
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
    }
}