using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using FireAxe.ViewModels;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive.Disposables;

namespace FireAxe.Views;

public partial class FlatVpkAddonListWindow : ReactiveWindow<FlatVpkAddonListViewModel>
{
    public FlatVpkAddonListWindow()
    {
        this.WhenAnyValue(x => x.ViewModel)
            .WhereNotNull()
            .Subscribe(viewModel =>
            {
                viewModel.Select += ViewModel_Select;
            });

        this.WhenActivated((CompositeDisposable disposables) =>
        {

        });

        InitializeComponent();
    }

    private void ViewModel_Select(IEnumerable<VpkAddon> addons)
    {
        var listBox = FindListBox();
        if (listBox == null)
        {
            return;
        }

        SelectionModelHelper.Select(listBox.Selection, addons, obj =>
        {
            if (obj is FlatVpkAddonViewModel viewModel)
            {
                return viewModel.AddonNode;
            }
            return null;
        });
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
}