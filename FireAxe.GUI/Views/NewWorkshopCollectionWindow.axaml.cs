using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FireAxe.ViewModels;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using FireAxe.Resources;
using System.Reactive;
using ReactiveUI.Avalonia;
using System.Reactive.Disposables.Fluent;

namespace FireAxe.Views;

public partial class NewWorkshopCollectionWindow : ReactiveWindow<NewWorkshopCollectionViewModel>
{
    public NewWorkshopCollectionWindow()
    {
        InitializeComponent();

        Closed += NewWorkshopCollectionWindow_Closed;

        this.WhenActivated((CompositeDisposable disposables) =>
        {

        });

        this.RegisterViewModelConnection((viewModel, disposables) =>
        {
            viewModel.RegisterInvalidHandler(Close)
                .DisposeWith(disposables);
            if (!viewModel.IsValid)
            {
                return;
            }

            viewModel.CloseRequested += Close;
            Disposable.Create(() => viewModel.CloseRequested -= Close).DisposeWith(disposables);

            viewModel.ShowInvalidCollectionIdInteraction.RegisterHandler(async (context) =>
            {
                await CommonMessageBoxes.ShowInfo(this, Texts.InvalidCollectionIdMessage, Texts.Error);
                context.SetOutput(Unit.Default);
            }).DisposeWith(disposables);
            viewModel.ShowCreateFailedInteraction.RegisterHandler(async (context) =>
            {
                await CommonMessageBoxes.ShowInfo(this, Texts.CreateCollectionFailedMessage, Texts.Error);
                context.SetOutput(Unit.Default);
            }).DisposeWith(disposables);
        });
    }

    private void NewWorkshopCollectionWindow_Closed(object? sender, EventArgs e)
    {
        if (ViewModel is { } viewModel)
        {
            ViewModel = null;
            viewModel.Dispose();
        }
    }
}