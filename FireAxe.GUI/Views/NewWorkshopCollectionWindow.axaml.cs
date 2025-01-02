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
        InitializeComponent();

        this.WhenAnyValue(x => x.ViewModel)
            .WhereNotNull()
            .Subscribe(viewModel =>
            {
                viewModel.Close += Close;

                viewModel.ShowInvalidCollectionIdInteraction.RegisterHandler(async (context) =>
                {
                    await CommonMessageBoxes.ShowInfo(this, Texts.InvalidCollectionIdMessage, Texts.Error);
                    context.SetOutput(Unit.Default);
                });
                viewModel.ShowCreateFailedInteraction.RegisterHandler(async (context) =>
                {
                    await CommonMessageBoxes.ShowInfo(this, Texts.CreateCollectionFailedMessage, Texts.Error);
                    context.SetOutput(Unit.Default);
                });
            });

        this.WhenActivated((CompositeDisposable disposables) =>
        {
            
        });
    }
}