using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using FireAxe.ViewModels;
using ReactiveUI;

namespace FireAxe.Views;

public partial class VpkAddonConflictingDetailsView : ReactiveUserControl<VpkAddonViewModel>
{
    public VpkAddonConflictingDetailsView()
    {
        this.WhenActivated((CompositeDisposable disposables) =>
        {

        });
        InitializeComponent();
    }
}