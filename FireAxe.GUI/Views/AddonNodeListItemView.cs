using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.ReactiveUI;
using FireAxe.ViewModels;
using ReactiveUI;
using System;
using System.Reactive.Disposables;

namespace FireAxe.Views
{
    public class AddonNodeListItemView : ReactiveUserControl<AddonNodeListItemViewModel>
    {
        public AddonNodeListItemView()
        {
            this.WhenActivated((CompositeDisposable disposables) =>
            {

            });
        }
    }
}
