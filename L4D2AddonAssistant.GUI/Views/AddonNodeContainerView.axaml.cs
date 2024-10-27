using Avalonia.ReactiveUI;
using L4D2AddonAssistant.ViewModels;
using ReactiveUI;
using System;
using System.Reactive.Disposables;

namespace L4D2AddonAssistant.Views;

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