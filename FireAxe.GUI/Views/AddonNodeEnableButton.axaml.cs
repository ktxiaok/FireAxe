using Avalonia;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.Media;
using FireAxe.ViewModels;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using ReactiveUI.Avalonia;
using System.Reactive.Disposables.Fluent;

namespace FireAxe.Views;

public partial class AddonNodeEnableButton : ReactiveUserControl<AddonNodeSimpleViewModel>
{
    public AddonNodeEnableButton()
    {
        InitializeComponent();

        DoubleTapped += AddonNodeEnableButton_DoubleTapped;

        var app = Application.Current!;
        var iconEnabled = (Geometry?)app.FindResource("icon_enabled");
        var iconEnabledSuppressed = (Geometry?)app.FindResource("icon_enabled_suppressed");
        var iconDisabled = (Geometry?)app.FindResource("icon_disabled");
        this.WhenActivated((CompositeDisposable disposables) =>
        {
            this.WhenAnyValue(x => x.ViewModel!.EnableState)
                .Subscribe(enableState =>
                {
                    Geometry? iconData;
                    Color color;
                    if (enableState == AddonNodeEnableState.Enabled)
                    {
                        iconData = iconEnabled;
                        color = Colors.Green;
                    }
                    else if (enableState == AddonNodeEnableState.EnabledSuppressed)
                    {
                        iconData = iconEnabledSuppressed;
                        color = Colors.Orange;
                    }
                    else
                    {
                        iconData = iconDisabled;
                        color = Colors.Red;
                    }
                    if (iconData != null)
                    {
                        icon.Data = iconData;
                    }
                    icon.Foreground = new SolidColorBrush(color);
                })
                .DisposeWith(disposables);
        });
    }

    private void AddonNodeEnableButton_DoubleTapped(object? sender, TappedEventArgs e)
    {
        e.Handled = true;
    }
}
