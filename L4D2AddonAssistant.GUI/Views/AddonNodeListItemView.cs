using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.ReactiveUI;
using L4D2AddonAssistant.ViewModels;
using ReactiveUI;
using System;
using System.Reactive.Disposables;

namespace L4D2AddonAssistant.Views
{
    public class AddonNodeListItemView : ReactiveUserControl<AddonNodeSimpleViewModel>
    {
        public AddonNodeListItemView()
        {
            DoubleTapped += AddonNodeListItemView_DoubleTapped;
            this.WhenActivated((CompositeDisposable disposables) =>
            {

            });
        }

        private void AddonNodeListItemView_DoubleTapped(object? sender, TappedEventArgs e)
        {
            e.Source = this;
        }
    }
}
