using Avalonia.Threading;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace FireAxe.ViewModels
{
    public class DownloadItemListViewModel : ViewModelBase, IActivatableViewModel, IDisposable
    {
        private static TimeSpan CleanInterval = TimeSpan.FromSeconds(0.5);

        private bool _disposed = false;

        private readonly ObservableCollection<IDownloadItem> _downloadItems = new();
        private readonly ReadOnlyObservableCollection<DownloadItemViewModel> _downloadItemViewModels;

        private IReadOnlyList<DownloadItemViewModel>? _selection = null;
        private readonly ObservableAsPropertyHelper<bool> _hasSelection;

        private IDisposable _cleanTimer;

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

            _cleanTimer = DispatcherTimer.Run(() =>
            {
                Clean();
                return true;
            }, CleanInterval);

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

        public void Add(IDownloadItem downloadItem)
        {
            ArgumentNullException.ThrowIfNull(downloadItem);

            _downloadItems.Add(downloadItem);
        }

        public void Remove(IDownloadItem downloadItem)
        {
            ArgumentNullException.ThrowIfNull(downloadItem);

            _downloadItems.Remove(downloadItem);
        }

        public void Clean()
        {
            int i = 0;
            while (i < _downloadItems.Count)
            {
                var downloadItem = _downloadItems[i];
                if (downloadItem.Status.IsCompleted())
                {
                    _downloadItems.RemoveAt(i);
                }
                else
                {
                    i++;
                }
            }
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

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            _cleanTimer.Dispose();
        }
    }
}
