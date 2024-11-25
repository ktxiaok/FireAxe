using Avalonia.Controls;
using Avalonia.ReactiveUI;
using L4D2AddonAssistant.ViewModels;
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
