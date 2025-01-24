using Avalonia.Controls;
using Avalonia.ReactiveUI;
using FireAxe.ViewModels;
using FireAxe.Resources;
using ReactiveUI;
using System;
using System.Reactive.Disposables;

namespace FireAxe.Views
{
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

        private void AutoSetNameButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
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
                    if (vpkAddonViewModel.Info is var info && info != null)
                    {
                        if (TrySetName(info.Title))
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

        private void EditTagButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var window = FindWindow();
            if (window == null)
            {
                return;
            }
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            var tagEditorWindow = new AddonTagEditorWindow()
            {
                DataContext = new AddonTagEditorViewModel(viewModel.AddonNode)
            };
            tagEditorWindow.ShowDialog(window);
        }

        private void CustomizeImageButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }
            var window = FindWindow();
            if (window == null)
            {
                return;
            }

            var customizeImageWindow = new AddonNodeCustomizeImageWindow()
            {
                DataContext = new AddonNodeCustomizeImageViewModel(viewModel.AddonNode)
            };
            customizeImageWindow.ShowDialog(window);
        }

        private void ClearSectionViews()
        {
            sectionViewContainerControl.Children.Clear();
        }

        private void AddSectionView(Control control)
        {
            sectionViewContainerControl.Children.Add(new AddonNodeSectionViewDecorator(control));
        }

        private Window? FindWindow()
        {
            return VisualRoot as Window;
        }
    }
}
