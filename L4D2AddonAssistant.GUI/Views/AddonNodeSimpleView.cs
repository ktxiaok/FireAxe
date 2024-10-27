using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.ReactiveUI;
using L4D2AddonAssistant.ViewModels;
using ReactiveUI;
using System;
using System.Reactive.Disposables;

namespace L4D2AddonAssistant.Views
{
    public class AddonNodeSimpleView : ReactiveUserControl<AddonNodeSimpleViewModel>
    {
        public AddonNodeSimpleView()
        {
            DoubleTapped += AddonNodeSimpleView_DoubleTapped;
            this.WhenActivated((CompositeDisposable disposables) =>
            {

            });
        }

        private void AddonNodeSimpleView_DoubleTapped(object? sender, TappedEventArgs e)
        {
            e.Source = this;
        }
    }
}
