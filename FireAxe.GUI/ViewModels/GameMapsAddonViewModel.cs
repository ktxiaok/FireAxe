using FireAxe.Resources;
using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;

namespace FireAxe.ViewModels;

public class GameMapsAddonViewModel : AddonGroupViewModel
{
    public GameMapsAddonViewModel(GameMapsAddon addon) : base(addon)
    {
        DownloadCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            if (Addon is { } current) await current.CheckDownloadAsync();
        });
        UpdateCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            if (Addon is { } current) await current.CheckDownloadAsync(forceUpdate: true);
        });
    }

    public new GameMapsAddon? Addon => (GameMapsAddon?)base.Addon;

    public override Type AddonType => typeof(GameMapsAddon);

    public string ItemId
    {
        get => Addon?.DetailsId?.ToString() ?? "";
        set
        {
            if (!GameMapsClient.TryParseDetailsId(value, out var id))
                throw new ArgumentException(Texts.InvalidGameMapsId);
            if (Addon is { } addon) addon.DetailsId = id;
        }
    }

    public DownloadItemViewModel? DownloadItemViewModel { get; private set => this.RaiseAndSetIfChanged(ref field, value); }

    public ReactiveCommand<Unit, Unit> DownloadCommand { get; }

    public ReactiveCommand<Unit, Unit> UpdateCommand { get; }

    public void OpenGameMapsPage()
    {
        if (Addon?.DetailsId is { } id) Utils.OpenWebsite(GameMapsClient.GetDetailsUrl(id));
    }

    protected override void OnNewAddon(AddonNode addon, CompositeDisposable disposables)
    {
        base.OnNewAddon(addon, disposables);
        var gameMapsAddon = (GameMapsAddon)addon;
        gameMapsAddon.WhenAnyValue(x => x.DetailsId)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(ItemId)))
            .DisposeWith(disposables);
        gameMapsAddon.WhenAnyValue(x => x.DownloadItem)
            .Subscribe(item => DownloadItemViewModel = item is null ? null : new(item))
            .DisposeWith(disposables);
    }

    protected override void OnNullAddon()
    {
        base.OnNullAddon();
        DownloadItemViewModel = null;
        this.RaisePropertyChanged(nameof(ItemId));
    }
}
