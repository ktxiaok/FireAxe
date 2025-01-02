using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using FireAxe.ViewModels;
using ReactiveUI;
using System.Reactive.Disposables;

namespace FireAxe.Views;

public partial class VpkAddonSectionView : ReactiveUserControl<VpkAddonViewModel>
{
    public VpkAddonSectionView()
    {
        this.WhenActivated((CompositeDisposable disposables) =>
        {

        });
        InitializeComponent();
    }
}