using FireAxe.Resources;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;

namespace FireAxe.ViewModels
{
    public class WorkshopVpkAddonViewModel : VpkAddonViewModel
    {
        private DownloadItemViewModel? _downloadItemViewModel = null;

        private PublishedFileDetails? _publishedFileDetails = null;

        private readonly ObservableAsPropertyHelper<string> _displayItemId;

        private ObservableAsPropertyHelper<bool>? _isFileDownloadCompleted = null;

        private CancellationTokenSource? _cts = null;

        public WorkshopVpkAddonViewModel(WorkshopVpkAddon addon) : base(addon)
        {
            _displayItemId = this.WhenAnyValue(x => x.ItemId)
                .Select(itemId =>
                {
                    if (itemId.Length == 0)
                    {
                        return Texts.Null;
                    }
                    return itemId;
                })
                .ToProperty(this, nameof(DisplayItemId));

            this.WhenActivated((CompositeDisposable disposables) =>
            {
                addon.WhenAnyValue(x => x.PublishedFileId)
                .Subscribe((id) => this.RaisePropertyChanged(nameof(ItemId)))
                .DisposeWith(disposables);

                addon.WhenAnyValue(x => x.DownloadItem)
                .Subscribe((downloadItem) =>
                {
                    if (downloadItem == null)
                    {
                        DownloadItemViewModel = null;
                    }
                    else
                    {
                        DownloadItemViewModel = new(downloadItem);
                    }
                })
                .DisposeWith(disposables);

                _isFileDownloadCompleted = addon.WhenAnyValue(x => x.FullVpkFilePath)
                .Select(path => path != null)
                .ToProperty(this, nameof(IsFileDownloadCompleted));

                Disposable.Create(() =>
                {
                    CancelTasks();

                    _isFileDownloadCompleted.Dispose();
                    _isFileDownloadCompleted = null;
                }).DisposeWith(disposables);
            });
        }

        public new WorkshopVpkAddon AddonNode => (WorkshopVpkAddon)((AddonNodeViewModel)this).AddonNode;

        public string ItemId
        {
            get => AddonNode.PublishedFileId?.ToString() ?? "";
            set
            {
                if (WorkshopVpkAddon.TryParsePublishedFileId(value, out var id))
                {
                    AddonNode.PublishedFileId = id;
                    Refresh();
                }
                else
                {
                    throw new ArgumentException();
                }
            }
        }

        public string DisplayItemId => _displayItemId.Value;

        public bool IsFileDownloadCompleted => _isFileDownloadCompleted?.Value ?? false;

        public DownloadItemViewModel? DownloadItemViewModel
        {
            get => _downloadItemViewModel;
            private set => this.RaiseAndSetIfChanged(ref _downloadItemViewModel, value);
        }

        public PublishedFileDetails? PublishedFileDetails
        {
            get => _publishedFileDetails;
            private set => this.RaiseAndSetIfChanged(ref _publishedFileDetails, value);
        }

        public void OpenWorkshopPage()
        {
            Utils.OpenWebsite($"https://steamcommunity.com/sharedfiles/filedetails/?id={AddonNode.PublishedFileId}");
        }

        protected override async void OnRefresh()
        {
            base.OnRefresh();

            var addon = AddonNode;
            PublishedFileDetails? publishedFileDetails = null;
            CancelTasks();
            _cts = new();

            try
            {
                publishedFileDetails = await addon.GetPublishedFileDetailsAllowCacheAsync(_cts.Token);
            }
            catch (OperationCanceledException) { }
            
            if (publishedFileDetails != null)
            {
                PublishedFileDetails = publishedFileDetails;
            }
        }

        protected override void OnClearCaches()
        {
            base.OnClearCaches();

            PublishedFileDetails = null;
        }

        private void CancelTasks()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
        }
    }
}
