using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using FireAxe.ViewModels;
using ReactiveUI;
using System;
using System.Reactive.Disposables;

namespace FireAxe.Views;

public partial class AppSettingsWindow : ReactiveWindow<AppSettingsViewModel> 
{
    public AppSettingsWindow()
    {
        this.WhenActivated((CompositeDisposable disposables) =>
        {
            
        });

        this.RegisterViewModelConnection(ConnectViewModel);

        InitializeComponent();
    }

    private void ConnectViewModel(AppSettingsViewModel viewModel, CompositeDisposable disposables)
    {
        viewModel.ChooseDirectoryInteraction.RegisterHandler(async (context) =>
        {
            context.SetOutput(await CommonMessageBoxes.ChooseDirectory(this));
        }).DisposeWith(disposables);
    }
}