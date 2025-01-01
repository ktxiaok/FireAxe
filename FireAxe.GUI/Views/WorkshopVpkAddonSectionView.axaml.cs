using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using FireAxe.ViewModels;
using ReactiveUI;
using System.Reactive.Disposables;

namespace FireAxe.Views;

public partial class WorkshopVpkAddonSectionView : ReactiveUserControl<WorkshopVpkAddonViewModel>
{
    public WorkshopVpkAddonSectionView()
    {
        InitializeComponent();

        this.WhenActivated((CompositeDisposable disposables) =>
        {

        });
    }
}