using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using L4D2AddonAssistant.ViewModels;
using ReactiveUI;
using System.Reactive.Disposables;

namespace L4D2AddonAssistant.Views;

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