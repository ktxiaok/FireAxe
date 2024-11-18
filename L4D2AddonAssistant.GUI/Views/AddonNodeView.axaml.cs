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
                    sectionViewPanel.Children.Clear();
                    if (viewModel != null)
                    {
                        if (viewModel is AddonGroupViewModel addonGroupViewModel)
                        {
                            sectionViewPanel.Children.Add(new AddonGroupSectionView()
                            {
                                ViewModel = addonGroupViewModel
                            });
                        }
                        else if (viewModel is VpkAddonViewModel vpkAddonViewModel)
                        {
                            sectionViewPanel.Children.Add(new VpkAddonSectionView()
                            {
                                ViewModel = vpkAddonViewModel
                            });
                            
                            if (viewModel is WorkshopVpkAddonViewModel workshopVpkAddonViewModel)
                            {
                                sectionViewPanel.Children.Add(new WorkshopVpkAddonSectionView()
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
    }
}
