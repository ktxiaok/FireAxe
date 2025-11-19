using FireAxe.Resources;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading;

namespace FireAxe.ViewModels;

public class WorkshopVpkAddonViewModel : VpkAddonViewModel
{
    private DownloadItemViewModel? _downloadItemViewModel = null;

    private PublishedFileDetails? _publishedFileDetails = null;

    private readonly ObservableAsPropertyHelper<string> _displayItemId;

    private readonly ObservableAsPropertyHelper<string> _workshopTagsString;

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

        _workshopTagsString = this.WhenAnyValue(x => x.PublishedFileDetails)
            .Select(details =>
            {
                if (details == null)
                {
                    return "";
                }
                var tags = details.Tags;
                if (tags == null)
                {
                    return "";
                }
                return string.Join(", ", tags.Select(tagObj => tagObj.Tag));
            })
            .ToProperty(this, nameof(WorkshopTagsString));

        ApplyTagsFromWorkshopCommand = ReactiveCommand.Create(() =>
        {
            var addon = Addon;
            if (addon == null)
            {
                return;
            }

            addon.RequestApplyTagsFromWorkshop = true;
            addon.Check();
        });
        DeleteRedundantVpkFilesCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var addon = Addon;
            if (addon is null)
            {
                return;
            }

            try
            {
                var report = addon.RequestDeleteRedundantVpkFiles();
                bool confirm = await ConfirmDeleteRedundantVpkFilesInteraction.Handle(report);
                if (report.IsEmpty || !confirm)
                {
                    return;
                }
                report.Execute();
                await ShowDeleteRedundantVpkFilesSuccessInteraction.Handle(report);
            }
            catch (Exception ex)
            {
                await ShowExceptionInteraction.Handle(ex);
            }
        });
    }

    public new WorkshopVpkAddon? Addon => (WorkshopVpkAddon?)((AddonNodeViewModel)this).Addon;

    public override Type AddonType => typeof(WorkshopVpkAddon);

    public string ItemId
    {
        get => Addon?.PublishedFileId?.ToString() ?? "";
        set
        {
            if (!PublishedFileUtils.TryParsePublishedFileId(value, out var id))
            {
                throw new ArgumentException("Invalid published file id."); 
            }

            var addon = Addon;
            if (addon == null)
            {
                return;
            }

            addon.PublishedFileId = id;
        }
    }

    public string DisplayItemId => _displayItemId.Value;

    public string WorkshopTagsString => _workshopTagsString.Value;

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

    public ReactiveCommand<Unit, Unit> ApplyTagsFromWorkshopCommand { get; }

    public ReactiveCommand<Unit, Unit> DeleteRedundantVpkFilesCommand { get; }

    public Interaction<WorkshopVpkAddon.DeleteRedundantVpkFilesReport, bool> ConfirmDeleteRedundantVpkFilesInteraction { get; } = new();

    public Interaction<WorkshopVpkAddon.DeleteRedundantVpkFilesReport, Unit> ShowDeleteRedundantVpkFilesSuccessInteraction { get; } = new();

    public Interaction<Exception, Unit> ShowExceptionInteraction { get; } = new();

    public void OpenWorkshopPage()
    {
        var addon = Addon;
        if (addon == null)
        {
            return;
        }

        Utils.OpenWebsite($"https://steamcommunity.com/sharedfiles/filedetails/?id={addon.PublishedFileId}");
    }

    protected override void OnNewAddon(AddonNode addon0, CompositeDisposable disposables)
    {
        base.OnNewAddon(addon0, disposables);

        var addon = (WorkshopVpkAddon)addon0;

        addon.WhenAnyValue(x => x.PublishedFileId)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(ItemId)))
            .DisposeWith(disposables);

        addon.WhenAnyValue(x => x.DownloadItem)
            .Subscribe(downloadItem =>
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
            
        }).DisposeWith(disposables);
    }

    protected override void OnNullAddon()
    {
        base.OnNullAddon();

        this.RaisePropertyChanged(nameof(ItemId));
        DownloadItemViewModel = null;
    }

    protected override void OnRefresh(CancellationToken cancellationToken)
    {
        base.OnRefresh(cancellationToken);

        var addon = Addon;
        var refreshId = CurrentRefreshId;

        async void RefreshPublishedFileDetails()
        {
            PublishedFileDetails = null;

            if (addon == null)
            {
                return;
            }

            try
            {
                var result = await addon.GetPublishedFileDetailsAllowCacheAsync(cancellationToken);
                if (refreshId != CurrentRefreshId)
                {
                    return;
                }
                PublishedFileDetails = result;
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        RefreshPublishedFileDetails();
    }

    protected override void OnClearCaches()
    {
        base.OnClearCaches();

        PublishedFileDetails = null;
    }
}
