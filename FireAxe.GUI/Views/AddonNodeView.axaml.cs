using Avalonia.Controls;
using Avalonia.ReactiveUI;
using Avalonia.Interactivity;
using FireAxe.ViewModels;
using FireAxe.Resources;
using ReactiveUI;
using System;
using System.Reactive.Disposables;

namespace FireAxe.Views;

public partial class AddonNodeView : ReactiveUserControl<AddonNodeViewModel>
{
    public AddonNodeView()
    {
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
                            AddSectionView(new AddonGroupSectionView()
                            {
                                ViewModel = addonGroupViewModel
                            });
                        }
                        else if (viewModel is VpkAddonViewModel vpkAddonViewModel)
                        {
                            AddSectionView(new VpkAddonSectionView()
                            {
                                ViewModel = vpkAddonViewModel
                            });
                        
                            if (viewModel is WorkshopVpkAddonViewModel workshopVpkAddonViewModel)
                            {
                                AddSectionView(new WorkshopVpkAddonSectionView()
                                {
                                    ViewModel = workshopVpkAddonViewModel
                                });
                            }
                        }
                    }
                })
                .DisposeWith(disposables);
        });

        InitializeComponent();

        autoSetNameButton.Click += AutoSetNameButton_Click;
        editTagButton.Click += EditTagButton_Click;
        customizeImageButton.Click += CustomizeImageButton_Click;
    }

    private void AutoSetNameButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext != null)
        {
            if (DataContext is WorkshopVpkAddonViewModel workshopVpkAddonViewModel)
            {
                if (workshopVpkAddonViewModel.PublishedFileDetails is var details && details != null)
                {
                    if (TrySetName(details.Title))
                    {
                        return;
                    }
                }
            }

            if (DataContext is VpkAddonViewModel vpkAddonViewModel)
            {
                if (vpkAddonViewModel.Info is { } info && info.Title is { } title)
                {
                    if (TrySetName(title))
                    {
                        return;
                    }
                }
            }
        }

        TrySetName(Texts.NoAvailableName);

        bool TrySetName(string name)
        {
            name = name.Trim();
            if (name.Length == 0)
            {
                return false;
            }

            nameControl.IsEditing = true;
            var textBox = nameControl.TextBox;
            if (textBox != null)
            {
                textBox.Text = name;
            }
            return true;
        }
    }

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
