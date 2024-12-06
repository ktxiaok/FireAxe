using Avalonia.Controls;
using Avalonia.ReactiveUI;
using L4D2AddonAssistant.ViewModels;
using L4D2AddonAssistant.Resources;
using ReactiveUI;
using System;
using System.Reactive.Disposables;

namespace L4D2AddonAssistant.Views
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

        private void ClearSectionViews()
        {
            sectionViewContainerControl.Children.Clear();
        }

        private void AddSectionView(Control control)
        {
            sectionViewContainerControl.Children.Add(new AddonNodeSectionViewDecorator(control));
        }
    }
}
