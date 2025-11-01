using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using FireAxe.ViewModels;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using System.Reactive;
using FireAxe.Resources;

namespace FireAxe.Views;

public partial class AddonTagEditorWindow : ReactiveWindow<AddonTagEditorViewModel>
{
    public AddonTagEditorWindow()
    {
        InitializeComponent();

        this.WhenActivated((CompositeDisposable disposables) =>
        {

        });

        this.RegisterViewModelConnection(ConnectViewModel);
    }

    private void ConnectViewModel(AddonTagEditorViewModel viewModel, CompositeDisposable disposables)
    {
        viewModel.AddTagInteraction.RegisterHandler(async (context) =>
        {
            var addTagWindow = new AddAddonTagWindow()
            {
                DataContext = new AddAddonTagViewModel(viewModel.Addon.Root)
            };
            addTagWindow.Input = context.Input;
            var result = await addTagWindow.ShowDialog<string>(this);
            context.SetOutput(result);
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
    }
}