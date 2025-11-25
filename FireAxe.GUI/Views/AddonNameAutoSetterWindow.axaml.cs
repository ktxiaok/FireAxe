using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FireAxe.ViewModels;
using ReactiveUI;
using ReactiveUI.Avalonia;
using FireAxe.Resources;
using System.Reactive;
using Avalonia.Interactivity;
using System.Linq;
using System.Collections.Generic;

namespace FireAxe.Views;

public partial class AddonNameAutoSetterWindow : ReactiveWindow<AddonNameAutoSetterViewModel>
{
    public AddonNameAutoSetterWindow()
    {
        InitializeComponent();

        Closed += AddonNameAutoSetterWindow_Closed;

        addTargetButton.Click += AddTargetButton_Click;

        this.WhenActivated((CompositeDisposable disposables) =>
        {

        });

        this.RegisterViewModelConnection(ConnectViewModel);
    }

    private void ConnectViewModel(AddonNameAutoSetterViewModel viewModel, CompositeDisposable disposables)
    {
        viewModel.RegisterInvalidHandler(Close)
            .DisposeWith(disposables);
        if (!viewModel.IsValid)
        {
            return;
        }

        viewModel.ShowNoItemsToStartInteraction.RegisterHandler(async context =>
        {
            await CommonMessageBoxes.ShowInfo(this, Texts.NoItemsToStart);
            context.SetOutput(Unit.Default);
        }).DisposeWith(disposables);
        viewModel.ShowMultipleFailedApplyInteraction.RegisterHandler(async context =>
        {
            int count = context.Input;
            await CommonMessageBoxes.ShowError(this, Texts.FailedToApplyNameWithCount.FormatNoThrow(count));
            context.SetOutput(Unit.Default);
        }).DisposeWith(disposables);
    }

    private void AddonNameAutoSetterWindow_Closed(object? sender, EventArgs e)
    {
        var viewModel = ViewModel;
        ViewModel = null;
        viewModel?.Dispose();
    }

    private async void AddTargetButton_Click(object? sender, RoutedEventArgs e)
    {
        var viewModel = ViewModel;
        if (viewModel is null)
        {
            return;
        }

        var pickerWindow = new AddonNodePickerWindow
        {
            DataContext = new AddonNodePickerViewModel(viewModel.AddonRoot),
            Title = Texts.AddTargets
        };
        var result = await pickerWindow.ShowDialog<IReadOnlyList<AddonNode>>(this);
        if (result is null)
        {
            return;
        }
        foreach (var addon in result)
        {
            viewModel.AddTargetAddon(addon.Id);
        }
    }
}