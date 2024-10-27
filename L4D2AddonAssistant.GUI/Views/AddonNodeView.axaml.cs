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
                            var sectionView = new AddonGroupSectionView()
                            {
                                ViewModel = addonGroupViewModel
                            };
                            sectionViewPanel.Children.Add(sectionView);
                        }
                        if (viewModel is VpkAddonViewModel vpkAddonViewModel)
                        {
                            var sectionView = new VpkAddonSectionView()
                            {
                                ViewModel = vpkAddonViewModel
                            };
                            sectionViewPanel.Children.Add(sectionView);
                        }
                    }
                })
                .DisposeWith(disposables);
            });
            InitializeComponent();
        }
    }
}
