using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FireAxe.ViewModels;
using ReactiveUI;
using ReactiveUI.Avalonia;
using System.Reactive.Disposables;

namespace FireAxe.Views;

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