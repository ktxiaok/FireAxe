using ReactiveUI;
using System;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using FireAxe.Resources;
using System.Reactive.Disposables.Fluent;

namespace FireAxe.ViewModels;

public class NewWorkshopCollectionViewModel : ViewModelBase, IActivatableViewModel, IValidity
{
    private readonly AddonRoot _addonRoot;
    private readonly ValidRef<AddonGroup>? _addonGroupRef = null;

    private string _collectionId = "";
    private bool _includeLinkedCollections = true;

    private CancellationTokenSource? _createCts = null;

    private bool _created = false;

    private bool _active = false;

    private bool _isValid = true;

    public NewWorkshopCollectionViewModel(AddonRoot addonRoot, AddonGroup? addonGroup)
    {
        ArgumentNullException.ThrowIfNull(addonRoot);
        _addonRoot = addonRoot;
        if (addonGroup is not null)
        {
            _addonGroupRef = new(addonGroup);
        }

        CreateCommand = ReactiveCommand.CreateFromTask(Create);

        this.WhenActivated((CompositeDisposable disposables) =>
        {
            _active = true;

            _addonRoot.RegisterInvalidHandler(() => IsValid = false)
                .DisposeWith(disposables);

            Disposable.Create(() =>
            {
                _active = false;

                CancelCreate();
            })
            .DisposeWith(disposables);
        });
    }

    public event Action? CloseRequested = null;

    public ViewModelActivator Activator { get; } = new();

    public bool IsValid
    {
        get => _isValid;
        private set => this.RaiseAndSetIfChanged(ref _isValid, value);
    }

    public string CollectionId
    {
        get => _collectionId;
        set => this.RaiseAndSetIfChanged(ref _collectionId, value);
    }

    public bool IncludeLinkedCollections
    {
        get => _includeLinkedCollections;
        set => this.RaiseAndSetIfChanged(ref _includeLinkedCollections, value);
    }

    public ReactiveCommand<Unit, Unit> CreateCommand { get; }

    public Interaction<Unit, Unit> ShowInvalidCollectionIdInteraction { get; } = new();

    public Interaction<Unit, Unit> ShowCreateFailedInteraction { get; } = new();

    public void CancelCreate()
    {
        if (_createCts is not null)
        {
            _createCts.Cancel();
            _createCts.Dispose();
            _createCts = null;
        }
    }

    private async Task Create()
    {
        if (_created)
        {
            return;
        }

        ulong collectionId;
        if (!PublishedFileUtils.TryParsePublishedFileId(CollectionId, out collectionId))
        {
            await ShowInvalidCollectionIdInteraction.Handle(Unit.Default);
            return;
        }

        _createCts ??= new();

        ulong[]? itemIds = null;
        PublishedFileDetails? collectionDetails = null;
        var httpClient = _addonRoot.HttpClient;
        try
        {
            var itemIdsTask = WorkshopCollectionUtils.GetWorkshopCollectionContentAsync(collectionId, _includeLinkedCollections, httpClient, _createCts.Token);
            var collectionDetailsTask = PublishedFileUtils.GetPublishedFileDetailsAsync(collectionId, httpClient, _createCts.Token);
            itemIds = await itemIdsTask;
            var getCollectionDetailsResult = await collectionDetailsTask;
            if (getCollectionDetailsResult.IsSucceeded)
            {
                collectionDetails = getCollectionDetailsResult.Content;
            }
        }
        catch (OperationCanceledException) { }

        if (itemIds == null || collectionDetails == null)
        {
            if (_active)
            {
                await ShowCreateFailedInteraction.Handle(Unit.Default);
            }
            return;
        }

        CloseRequested?.Invoke();

        if (!_addonRoot.IsValid)
        {
            return;
        }

        var collectionGroup = AddonNode.Create<AddonGroup>(_addonRoot, _addonGroupRef?.TryGet());
        var collectionName = collectionGroup.Parent.GetUniqueNodeName(FileSystemUtils.SanitizeFileName(collectionDetails.Title));
        collectionGroup.Name = collectionName;
        foreach (var itemId in itemIds)
        {
            var addon = AddonNode.Create<WorkshopVpkAddon>(_addonRoot, collectionGroup);
            addon.Name = addon.Parent.GetUniqueNodeName(Texts.UnnamedWorkshopAddon);
            addon.RequestAutoSetName = true;
            addon.PublishedFileId = itemId;
        }

        _created = true;
    }
}
