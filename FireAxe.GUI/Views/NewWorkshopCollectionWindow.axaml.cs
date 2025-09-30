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

public partial class NewWorkshopCollectionWindow : ReactiveWindow<NewWorkshopCollectionViewModel>
{
    public NewWorkshopCollectionWindow()
    {
        this.WhenActivated((CompositeDisposable disposables) =>
        {

        });

        this.RegisterViewModelConnection((viewModel, disposables) =>
            {
                viewModel.Close += Close;
                Disposable.Create(() => viewModel.Close -= Close).DisposeWith(disposables);

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

        InitializeComponent();
    }
}