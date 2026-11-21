using FireAxe.Resources;
using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;

namespace FireAxe.ViewModels;

public class WorkshopCollectionSubscriptionViewModel : AddonGroupViewModel
{
    public WorkshopCollectionSubscriptionViewModel(WorkshopCollectionSubscription addon) : base(addon)
    {
        SynchronizeCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            if (Addon is { } current) await current.SynchronizeAsync(forceUpdate: true);
        }, this.WhenAnyValue(x => x.Addon!.SynchronizationTask).Select(task => task is null || task.IsCompleted));
    }

    public new WorkshopCollectionSubscription? Addon => (WorkshopCollectionSubscription?)base.Addon;

    public override Type AddonType => typeof(WorkshopCollectionSubscription);

    public string CollectionId
    {
        get => Addon?.CollectionId?.ToString() ?? "";
        set
        {
            if (!PublishedFileUtils.TryParsePublishedFileId(value.Trim(), out var id) || id == 0)
                throw new ArgumentException(Texts.InvalidCollectionIdMessage);
            if (Addon is { } addon) addon.CollectionId = id;
        }
    }

    public string LastSynchronization => Addon?.LastSynchronizedAt is { } time
        ? time.LocalDateTime.ToString("g") : Texts.NeverSynchronized;

    public ReactiveCommand<Unit, Unit> SynchronizeCommand { get; }

    public void OpenWorkshopPage()
    {
        if (Addon?.CollectionId is { } id) Utils.OpenWorkshopPage(id);
    }

    protected override void OnNewAddon(AddonNode addon, CompositeDisposable disposables)
    {
        base.OnNewAddon(addon, disposables);
        ((WorkshopCollectionSubscription)addon).WhenAnyValue(x => x.LastSynchronizedAt)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(LastSynchronization)))
            .DisposeWith(disposables);
        ((WorkshopCollectionSubscription)addon).WhenAnyValue(x => x.CollectionId)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(CollectionId)))
            .DisposeWith(disposables);
    }

    protected override void OnNullAddon()
    {
        base.OnNullAddon();
        this.RaisePropertyChanged(nameof(CollectionId));
        this.RaisePropertyChanged(nameof(LastSynchronization));
    }
}
