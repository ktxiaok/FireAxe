using Avalonia;
using Avalonia.Controls;
using DynamicData;
using DynamicData.Alias;
using DynamicData.Binding;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace FireAxe.ViewModels
{
    public class AddonNodeContainerViewModel : ViewModelBase, IActivatableViewModel
    {
        private ReadOnlyObservableCollection<AddonNode>? _nodes = null;
        private IDisposable? _nodesSubscription = null;
        private ReadOnlyObservableCollection<AddonNodeListItemViewModel>? _nodeViewModels = null;

        private AddonNodeListItemViewKind _listItemViewKind = AddonNodeListItemViewKind.MediumTile;

        private readonly ObservableAsPropertyHelper<bool> _isGridView;
        private readonly ObservableAsPropertyHelper<bool> _isTileView;
        private readonly ObservableAsPropertyHelper<double> _tileViewSize;

        public AddonNodeContainerViewModel()
        {
            _isGridView = this.WhenAnyValue(x => x.ListItemViewKind)
                .Select((kind) => kind == AddonNodeListItemViewKind.Grid)
                .ToProperty(this, nameof(IsGridView));
            _isTileView = this.WhenAnyValue(x => x.ListItemViewKind)
                .Select((kind) => kind.IsTile())
                .ToProperty(this, nameof(IsTileView));
            _tileViewSize = this.WhenAnyValue(x => x.ListItemViewKind)
                .Select((kind) => (kind switch
                {
                    AddonNodeListItemViewKind.MediumTile => (double?)Application.Current!.FindResource("size_addon_tile"),
                    AddonNodeListItemViewKind.LargeTile => (double?)Application.Current!.FindResource("size_addon_tile_large"),
                    AddonNodeListItemViewKind.SmallTile => (double?)Application.Current!.FindResource("size_addon_tile_small"),
                    _ => null
                }).GetValueOrDefault(200))
                .ToProperty(this, nameof(TileViewSize));

            this.WhenActivated((CompositeDisposable disposables) =>
            {
                this.WhenAnyValue(x => x.Nodes)
                .Subscribe(nodes =>
                {
                    TryDisposeNodesSubscription();
                    if (nodes != null)
                    {
                        _nodesSubscription = nodes.ToObservableChangeSet()
                        .Select(node => new AddonNodeListItemViewModel(node, this))
                        .Bind(out var nodeViewModels)
                        .Subscribe();
                        NodeViewModels = nodeViewModels;
                    }
                })
                .DisposeWith(disposables);

                Disposable.Create(() =>
                {
                    TryDisposeNodesSubscription();
                })
                .DisposeWith(disposables);
            });
        }

        public ViewModelActivator Activator { get; } = new();

        public ReadOnlyObservableCollection<AddonNode>? Nodes
        {
            get => _nodes;
            set => this.RaiseAndSetIfChanged(ref _nodes, value);
        }

        public ReadOnlyObservableCollection<AddonNodeListItemViewModel>? NodeViewModels
        {
            get => _nodeViewModels;
            private set => this.RaiseAndSetIfChanged(ref _nodeViewModels, value);
        }

        public AddonNodeListItemViewKind ListItemViewKind
        {
            get => _listItemViewKind;
            set => this.RaiseAndSetIfChanged(ref _listItemViewKind, value);
        }

        public bool IsGridView => _isGridView.Value;

        public bool IsTileView => _isTileView.Value;

        public double TileViewSize => _tileViewSize.Value;
        
        private void TryDisposeNodesSubscription()
        {
            if (_nodesSubscription != null)
            {
                _nodesSubscription.Dispose();
                _nodesSubscription = null;
                _nodeViewModels = null;
            }
        }
    }
}
