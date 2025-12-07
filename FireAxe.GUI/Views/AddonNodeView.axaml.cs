using Avalonia.Controls;
using Avalonia.Interactivity;
using FireAxe.ViewModels;
using FireAxe.Resources;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using ReactiveUI.Avalonia;
using System.Reactive.Disposables.Fluent;
using System.Collections.Generic;

namespace FireAxe.Views;

public partial class AddonNodeView : ReactiveUserControl<AddonNodeViewModel>
{
    public AddonNodeView()
    {
        InitializeComponent();

        this.WhenActivated((CompositeDisposable disposables) =>
        {
            this.WhenAnyValue(x => x.ViewModel)
                .Subscribe(viewModel =>
                {
                    ClearSectionViews();
                    if (viewModel != null)
                    {
                        if (viewModel is AddonGroupViewModel addonGroupViewModel)
                        {
                            AddSectionView(new AddonGroupSectionView
                            {
                                ViewModel = addonGroupViewModel
                            });
                        }
                        else if (viewModel is RefAddonNodeViewModel refAddonViewModel)
                        {
                            AddSectionView(new RefAddonNodeSectionView
                            {
                                ViewModel = refAddonViewModel
                            });
                        }
                        else if (viewModel is VpkAddonViewModel vpkAddonViewModel)
                        {
                            AddSectionView(new VpkAddonSectionView
                            {
                                ViewModel = vpkAddonViewModel
                            });

                            if (viewModel is WorkshopVpkAddonViewModel workshopVpkAddonViewModel)
                            {
                                AddSectionView(new WorkshopVpkAddonSectionView
                                {
                                    ViewModel = workshopVpkAddonViewModel
                                });
                            }
                        }
                    }
                })
                .DisposeWith(disposables);
        });

        editTagButton.Click += EditTagButton_Click;
        customizeImageButton.Click += CustomizeImageButton_Click;
    }

    public EditableTextBlock NameEditingControl => nameEditingControl;

    private void EditTagButton_Click(object? sender, RoutedEventArgs e)
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

        var tagEditorWindow = new AddonTagEditorWindow
        {
            DataContext = new AddonTagEditorViewModel(addon)
        };
        tagEditorWindow.ShowDialog(this.GetRootWindow());
    }

    private void CustomizeImageButton_Click(object? sender, RoutedEventArgs e)
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

        var customizeImageWindow = new AddonNodeCustomizeImageWindow()
        {
            DataContext = new AddonNodeCustomizeImageViewModel(addon)
        };
        customizeImageWindow.ShowDialog(this.GetRootWindow());
    }

    private void ClearSectionViews()
    {
        sectionViewContainerControl.Children.Clear();
    }

    private void AddSectionView(Control control)
    {
        sectionViewContainerControl.Children.Add(new AddonNodeSectionViewDecorator(control));
    }
}
