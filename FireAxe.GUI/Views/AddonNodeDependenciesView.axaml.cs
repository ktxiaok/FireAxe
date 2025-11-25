using System.Collections.Generic;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FireAxe.Resources;
using FireAxe.ViewModels;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace FireAxe.Views;

public partial class AddonNodeDependenciesView : ReactiveUserControl<AddonNodeDependenciesViewModel>
{
    public AddonNodeDependenciesView()
    {
        InitializeComponent();

        addDependencyButton.Click += AddDependencyButton_Click;
        enableAllDependenciesButton.Click += EnableAllDependenciesButton_Click;

        this.WhenActivated((CompositeDisposable disposables) =>
        {

        });
    }

    private async void AddDependencyButton_Click(object? sender, RoutedEventArgs e)
    {
        var viewModel = ViewModel;
        if (viewModel is null)
        {
            return;
        }
        var addon = viewModel.Addon;

        var pickerWindow = new AddonNodePickerWindow
        {
            DataContext = new AddonNodePickerViewModel(addon.Root),
            Title = Texts.AddDependencies
        };
        var result = await pickerWindow.ShowDialog<IReadOnlyList<AddonNode>>(this.GetRootWindow());
        if (result is null)
        {
            return;
        }
        if (!addon.IsValid)
        {
            return;
        }
        foreach (var dependency in result)
        {
            addon.AddDependentAddon(dependency.Id);
        }
    }

    private void EnableAllDependenciesButton_Click(object? sender, RoutedEventArgs e)
    {
        ViewModel?.Addon.EnableAllDependencies();
    }
}