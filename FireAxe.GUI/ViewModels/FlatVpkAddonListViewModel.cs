using Avalonia.Threading;
using DynamicData;
using ReactiveUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;

namespace FireAxe.ViewModels
{
    public class FlatVpkAddonListViewModel : ViewModelBase, IActivatableViewModel
    {
        private static TimeSpan SoftRefreshInterval = TimeSpan.FromMilliseconds(100);

        private MainWindowViewModel _mainWindowViewModel;

        private readonly SourceList<VpkAddon> _addons = new();
        private readonly ReadOnlyObservableCollection<FlatVpkAddonViewModel> _addonViewModels;

        private bool _includeEnabledOnly = false;

        private bool _requestSoftRefresh = false;

        private ObservableCollection<object> _selectedItems = new();

        public FlatVpkAddonListViewModel(MainWindowViewModel mainWindowViewModel)
        {
            ArgumentNullException.ThrowIfNull(mainWindowViewModel);
            _mainWindowViewModel = mainWindowViewModel;

            _addons.Connect()
                .Sort(Comparer<VpkAddon>.Create((x, y) => -x.VpkPriority.CompareTo(y.VpkPriority)))
                .Transform(vpkAddon => new FlatVpkAddonViewModel(vpkAddon, this))
                .Bind(out _addonViewModels)
                .Subscribe();

            this.WhenActivated((CompositeDisposable disposables) =>
            {
                Refresh();

                DispatcherTimer.Run(() =>
                {
                    if (_requestSoftRefresh)
                    {
                        _requestSoftRefresh = false;
                        SoftRefresh();
                    }
                    return true;
                }, SoftRefreshInterval)
                .DisposeWith(disposables);

                Disposable.Create(() =>
                {
                    _addons.Clear();
                })
                .DisposeWith(disposables);
            });
        }

        public event Action<IEnumerable<VpkAddon>>? Select = null;

        public ViewModelActivator Activator { get; } = new();

        public ReadOnlyObservableCollection<FlatVpkAddonViewModel> AddonViewModels => _addonViewModels;

        public bool IncludeEnabledOnly
        {
            get => _includeEnabledOnly;
            set => this.RaiseAndSetIfChanged(ref _includeEnabledOnly, value);
        }

        public ObservableCollection<object> SelectedItems
        {
            get => _selectedItems;
            set => this.RaiseAndSetIfChanged(ref _selectedItems, value);
        }

        public IEnumerable<FlatVpkAddonViewModel> SelectedAddonViewModels
        {
            get
            {
                var items = SelectedItems;
                if (items == null)
                {
                    yield break;
                }
                foreach (var item in items)
                {
                    if (item is FlatVpkAddonViewModel viewModel)
                    {
                        yield return viewModel;
                    }
                }
            }
        }

        public IEnumerable<VpkAddon> SelectedAddons
        {
            get
            {
                foreach (var viewModel in SelectedAddonViewModels)
                {
                    yield return viewModel.AddonNode;
                }
            }
        }

        public void Refresh()
        {
            _addons.Edit(list =>
            {
                list.Clear();
                var addonRoot = _mainWindowViewModel.AddonRoot;
                if (addonRoot == null)
                {
                    return;
                }
                foreach (var addonNode in addonRoot.GetAllNodes())
                {
                    if (IncludeEnabledOnly && !addonNode.IsEnabledInHierarchy)
                    {
                        continue;
                    }
                    
                    if (addonNode is VpkAddon vpkAddon)
                    {
                        list.Add(vpkAddon);
                    }
                }
            });
        }

        public void SoftRefresh()
        {
            VpkAddon[] selectedAddons = [.. SelectedAddons];
            _addons.Edit(list => 
            {
                VpkAddon[] items = [.. list];
                list.Clear();
                foreach (var item in items)
                {
                    list.Add(item);
                }
            });
            Select?.Invoke(selectedAddons);
        }

        public void RequestSoftRefresh()
        {
            _requestSoftRefresh = true;
        }

        public void TurnUpPriority()
        {
            foreach (var addon in SelectedAddons)
            {
                addon.VpkPriority++;
            }
        }

        public void TurnDownPriority()
        {
            foreach (var addon in SelectedAddons)
            {
                addon.VpkPriority--;
            }
        }
    }
}
