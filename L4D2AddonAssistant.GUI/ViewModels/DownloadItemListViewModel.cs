using Avalonia.Threading;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace L4D2AddonAssistant.ViewModels
{
    public class DownloadItemListViewModel : ViewModelBase, IActivatableViewModel
    {
        private readonly ObservableCollection<IDownloadItem> _downloadItems = new();
        private readonly ReadOnlyObservableCollection<DownloadItemViewModel> _downloadItemViewModels;

        private IReadOnlyList<DownloadItemViewModel>? _selection = null;

        private readonly ObservableAsPropertyHelper<bool> _hasSelection;

        public DownloadItemListViewModel()
        {
            _downloadItems.ToObservableChangeSet()
                .Transform(downloadItem => new DownloadItemViewModel(downloadItem))
                .Bind(out _downloadItemViewModels)
                .Subscribe();

            _hasSelection = this.WhenAnyValue(x => x.Selection)
                .Select(selection =>
                {
                    if (selection == null)
                    {
                        return false;
                    }
                    return selection.Count > 0;
                })
                .ToProperty(this, nameof(HasSelection));
                

            this.WhenActivated((CompositeDisposable disposables) =>
            {
                Disposable.Create(() =>
                {
                    Selection = null;
                })
                .DisposeWith(disposables);
            });
        }

        public ViewModelActivator Activator { get; } = new();

        public ReadOnlyObservableCollection<DownloadItemViewModel> DownloadItemViewModels => _downloadItemViewModels;

        public IReadOnlyList<DownloadItemViewModel>? Selection
        {
            get => _selection;
            set
            {
                _selection = value;
                this.RaisePropertyChanged();
            }
        }

        public IEnumerable<IDownloadItem> SelectedDownloadItems
        {
            get
            {
                var selection = Selection;
                if (selection == null)
                {
                    yield break;
                }
                foreach (var item in selection)
                {
                    yield return item.DownloadItem;
                }
            }
        }

        public bool HasSelection => _hasSelection.Value;

        public void AddDownloadItem(IDownloadItem downloadItem)
        {
            ArgumentNullException.ThrowIfNull(downloadItem);

            _downloadItems.Add(downloadItem);
            Task.Run(() =>
            {
                downloadItem.Wait();
                Dispatcher.UIThread.Post(() => RemoveDownloadItem(downloadItem));
            });
        }

        public void RemoveDownloadItem(IDownloadItem downloadItem)
        {
            ArgumentNullException.ThrowIfNull(downloadItem);

            _downloadItems.Remove(downloadItem);
        }

        public void Pause()
        {
            foreach (var download in SelectedDownloadItems)
            {
                download.Pause();
            }
        }

        public void Resume()
        {
            foreach (var download in SelectedDownloadItems)
            {
                download.Resume();
            }
        }

        public void Cancel()
        {
            foreach (var download in SelectedDownloadItems)
            {
                download.Cancel();
            }
        }
    }
}
