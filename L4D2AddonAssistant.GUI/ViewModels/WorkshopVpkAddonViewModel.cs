using L4D2AddonAssistant.Resources;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace L4D2AddonAssistant.ViewModels
{
    public class WorkshopVpkAddonViewModel : VpkAddonViewModel
    {
        private DownloadItemViewModel? _downloadItemViewModel = null;

        private readonly ObservableAsPropertyHelper<string> _displayItemId;

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
    }
}
