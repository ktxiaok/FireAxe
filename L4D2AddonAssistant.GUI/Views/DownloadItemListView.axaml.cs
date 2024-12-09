using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using L4D2AddonAssistant.ViewModels;
using ReactiveUI;
using System.Linq;
using System.Reactive.Disposables;

namespace L4D2AddonAssistant.Views;

public partial class DownloadItemListView : ReactiveUserControl<DownloadItemListViewModel>
{
    public DownloadItemListView()
    {
        AddHandler(ListBox.SelectionChangedEvent, DownloadItemListView_SelectionChanged);

        this.WhenActivated((CompositeDisposable disposables) =>
        {
            Disposable.Create(() =>
            {
                FindListBox()?.Selection?.Clear();
            })
            .DisposeWith(disposables);
        });

        InitializeComponent();
    }

    private ListBox? FindListBox()
    {
        foreach (var obj in this.GetLogicalDescendants())
        {
            if (obj is ListBox listBox)
            {
                return listBox;
            }
        }

        return null;
    }

    private void DownloadItemListView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var viewModel = ViewModel;
        if (viewModel != null)
        {
            if (e.Source is ListBox listBox)
            {
                viewModel.Selection = listBox.Selection.SelectedItems.Select(obj => obj as DownloadItemViewModel)
                    .Where(obj => obj != null)
                    .ToArray()!;
            }
        }
    }
}