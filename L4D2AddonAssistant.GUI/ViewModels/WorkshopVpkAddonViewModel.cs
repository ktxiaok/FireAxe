using L4D2AddonAssistant.Resources;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;

namespace L4D2AddonAssistant.ViewModels
{
    public class WorkshopVpkAddonViewModel : VpkAddonViewModel
    {
        private DownloadItemViewModel? _downloadItemViewModel = null;

        private PublishedFileDetails? _publishedFileDetails = null;

        private readonly ObservableAsPropertyHelper<string> _displayItemId;

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

                Disposable.Create(() =>
                {
                    CancelTasks();
                }).DisposeWith(disposables);
            });
        }

        public new WorkshopVpkAddon AddonNode => (WorkshopVpkAddon)((AddonNodeViewModel)this).AddonNode;

        public string ItemId
        {
            get => AddonNode.PublishedFileId?.ToString() ?? "";
            set
            {
                if (ulong.TryParse(value, out ulong id))
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
