using Avalonia.ReactiveUI;
using FireAxe.ViewModels;
using ReactiveUI;
using System;
using System.Reactive.Disposables;

namespace FireAxe.Views;

public partial class AddonNodeContainerView : ReactiveUserControl<AddonNodeContainerViewModel>
{
    public AddonNodeContainerView()
    {
        this.WhenActivated((CompositeDisposable disposables) =>
        {

        });
        InitializeComponent();
    }
}