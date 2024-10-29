using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using L4D2AddonAssistant.ViewModels;
using ReactiveUI;
using System.Reactive.Disposables;

namespace L4D2AddonAssistant.Views;

public partial class AddonGroupSectionView : ReactiveUserControl<AddonGroupViewModel>
{
    public AddonGroupSectionView()
    {
        this.WhenActivated((CompositeDisposable disposables) =>
        {

        });
        InitializeComponent();
    }
}