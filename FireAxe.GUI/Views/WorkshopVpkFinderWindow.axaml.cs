using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FireAxe.Resources;
using FireAxe.ViewModels;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace FireAxe.Views;

public partial class WorkshopVpkFinderWindow : ReactiveWindow<WorkshopVpkFinderViewModel>
{
    public WorkshopVpkFinderWindow()
    {
        InitializeComponent();

        Closed += OnClosed;

        this.WhenActivated((CompositeDisposable disposables) =>
        {

        });

        this.RegisterViewModelConnection((viewModel, disposables) =>
        {
            var onRequestClose = () => Close();
            viewModel.CloseRequested += onRequestClose;
            Disposable.Create(() => viewModel.CloseRequested -= onRequestClose).DisposeWith(disposables);

            viewModel.ChooseDirectoryInteraction.RegisterHandler(async context =>
            {
                string? startDir = context.Input;
                var dir = await CommonMessageBoxes.ChooseDirectory(this, new ChooseDirectoryOptions
                {
                    Title = Texts.FindFromDirectory,
                    StartDirectoryPath = startDir
                });
                context.SetOutput(dir);
            }).DisposeWith(disposables);
        });
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        var viewModel = ViewModel;
        ViewModel = null;
        viewModel?.Dispose();
    }
}