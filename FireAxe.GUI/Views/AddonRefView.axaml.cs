using System;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using FireAxe.ViewModels;
using ReactiveUI;

namespace FireAxe.Views;

public partial class AddonRefView : ReactiveUserControl<AddonNodeSimpleViewModel>
{
    public AddonRefView()
    {
        InitializeComponent();

        this.WhenActivated((CompositeDisposable disposables) =>
        {

        });

        jumpButton.Click += JumpButton_Click;
    }

    private void JumpButton_Click(object? sender, RoutedEventArgs e)
    {
        
    }
}