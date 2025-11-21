using System;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;
using FireAxe.ViewModels;
using ReactiveUI;
using ReactiveUI.Avalonia;
using FireAxe.Resources;
using System.Collections.Generic;

namespace FireAxe.Views;

public partial class RefAddonNodeSectionView : ReactiveUserControl<RefAddonNodeViewModel>
{
    public RefAddonNodeSectionView()
    {
        InitializeComponent();

        setSourceButton.Click += SetSourceButton_Click;

        this.WhenActivated((CompositeDisposable disposables) =>
        {

        });
    }

    private async void SetSourceButton_Click(object? sender, RoutedEventArgs e)
    {
        var viewModel = ViewModel;
        if (viewModel is null)
        {
            return;
        }
        var addon = viewModel.Addon;
        if (addon is null)
        {
            return;
        }

        var pickerWindow = new AddonNodePickerWindow
        {
            DataContext = new AddonNodePickerViewModel(addon.Root)
            {
                AllowMultiple = false
            },
            Title = Texts.SetSource
        };
        var result = await pickerWindow.ShowDialog<IReadOnlyList<AddonNode>>(this.GetRootWindow());
        if (result is null)
        {
            return;
        }
        viewModel = ViewModel;
        if (viewModel is null)
        {
            return;
        }
        addon = viewModel.Addon;
        if (addon is null)
        {
            return;
        }
        if (result is [var source])
        {
            addon.SourceAddonId = source.Id;
        }
    }
}