using System;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;
using FireAxe.ViewModels;
using ReactiveUI;
using System.Reactive;
using FireAxe.Resources;
using System.Collections.Generic;
using ReactiveUI.Avalonia;
using System.Reactive.Disposables.Fluent;

namespace FireAxe.Views;

public partial class FileCleanerWindow : ReactiveWindow<FileCleanerViewModel>
{
    public FileCleanerWindow()
    {
        InitializeComponent();

        this.WhenActivated((CompositeDisposable disposables) =>
        {

        });

        this.RegisterViewModelConnection(ConnectViewModel);

        addSourceButton.Click += AddSourceButton_Click;
    }

    private void ConnectViewModel(FileCleanerViewModel viewModel, CompositeDisposable disposables)
    {
        viewModel.ShowExceptionInteraction.RegisterHandler(async context =>
        {
            await CommonMessageBoxes.ShowException(this, context.Input);
            context.SetOutput(Unit.Default);
        }).DisposeWith(disposables);

        viewModel.ShowItemsFoundInteraction.RegisterHandler(async context =>
        {
            int count = context.Input;
            await CommonMessageBoxes.ShowInfo(this, Texts.ItemsFoundWithCount.FormatNoThrow(count));
            context.SetOutput(Unit.Default);
        }).DisposeWith(disposables);

        viewModel.ConfirmDeleteAllInteraction.RegisterHandler(async context =>
        {
            bool confirm = await CommonMessageBoxes.Confirm(this, Texts.ConfirmDeleteAll);
            context.SetOutput(confirm);
        }).DisposeWith(disposables);
    }

    private async void AddSourceButton_Click(object? sender, RoutedEventArgs e)
    {
        var viewModel = ViewModel;
        if (viewModel is null)
        {
            return;
        }
        var addonRoot = viewModel.AddonRoot;

        var pickerWindow = new AddonNodePickerWindow
        {
            DataContext = new AddonNodePickerViewModel(addonRoot),
            Title = Texts.AddSearchSource
        };
        var result = await pickerWindow.ShowDialog<IReadOnlyList<AddonNode>>(this);
        if (result is null)
        {
            return;
        }
        if (!viewModel.IsValid)
        {
            return;
        }
        foreach (var addon in result)
        {
            viewModel.AddSourceAddon(addon.Id);
        }
    }
}