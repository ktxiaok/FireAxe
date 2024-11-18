using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using L4D2AddonAssistant.ViewModels;
using ReactiveUI;
using System.Reactive.Disposables;

namespace L4D2AddonAssistant.Views;

public partial class DownloadItemView : ReactiveUserControl<DownloadItemViewModel>
{
    public DownloadItemView()
    {
        InitializeComponent();

        this.WhenActivated((CompositeDisposable disposables) =>
        {

        });
    }
}