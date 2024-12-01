using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.ReactiveUI;
using L4D2AddonAssistant.ViewModels;
using ReactiveUI;
using System;
using System.Reactive.Disposables;

namespace L4D2AddonAssistant.Views
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
