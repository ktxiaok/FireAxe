using L4D2AddonAssistant.Resources;
using ReactiveUI;
using System;
using System.Reactive.Disposables;

namespace L4D2AddonAssistant.ViewModels
{
    public class WorkshopVpkAddonViewModel : VpkAddonViewModel
    {
        private DownloadItemViewModel? _downloadItemViewModel = null;

        public WorkshopVpkAddonViewModel(WorkshopVpkAddon addon) : base(addon)
        {
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
            get => AddonNode.PublishedFileId?.ToString() ?? Texts.Null;
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

        public DownloadItemViewModel? DownloadItemViewModel
        {
            get => _downloadItemViewModel;
            private set => this.RaiseAndSetIfChanged(ref _downloadItemViewModel, value);
        }
    }
}
