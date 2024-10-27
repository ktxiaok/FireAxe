using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using L4D2AddonAssistant.ViewModels;
using ReactiveUI;
using System.Reactive.Disposables;

namespace L4D2AddonAssistant;

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