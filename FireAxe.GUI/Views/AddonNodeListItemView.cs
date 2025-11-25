using Avalonia.Controls;
using Avalonia.Input;
using FireAxe.ViewModels;
using ReactiveUI;
using ReactiveUI.Avalonia;
using System;
using System.Reactive.Disposables;

namespace FireAxe.Views;

public class AddonNodeListItemView : ReactiveUserControl<AddonNodeListItemViewModel>
{
    public AddonNodeListItemView()
    {
        this.WhenActivated((CompositeDisposable disposables) =>
        {

        });
    }
}
