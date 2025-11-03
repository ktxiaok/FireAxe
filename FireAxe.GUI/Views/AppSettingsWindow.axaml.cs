using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using FireAxe.ViewModels;
using FireAxe.Resources;
using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Disposables;

namespace FireAxe.Views;

public partial class AppSettingsWindow : ReactiveWindow<AppSettingsViewModel> 
{
    public AppSettingsWindow()
    {
        InitializeComponent();

        this.WhenActivated((CompositeDisposable disposables) =>
        {
            
        });

        this.RegisterViewModelConnection(ConnectViewModel);
    }

    private void ConnectViewModel(AppSettingsViewModel viewModel, CompositeDisposable disposables)
    {
        viewModel.ChooseGamePathDirectoryInteraction.RegisterHandler(async context =>
        {
            context.SetOutput(await CommonMessageBoxes.ChooseDirectory(this, new ChooseDirectoryOptions { Title = Texts.SelectGamePath }));
        }).DisposeWith(disposables);
        viewModel.ConfirmFoundGamePathInteraction.RegisterHandler(async context =>
        {
            bool confirm = await CommonMessageBoxes.Confirm(this, Texts.ConfirmFoundGamePathMessage.FormatNoThrow(context.Input));
            context.SetOutput(confirm);
        }).DisposeWith(disposables);
        viewModel.ReportGamePathNotFoundInteraction.RegisterHandler(async context =>
        {
            await CommonMessageBoxes.ShowInfo(this, Texts.GamePathNotFoundMessage);
            context.SetOutput(Unit.Default);
        }).DisposeWith(disposables);
    }
}