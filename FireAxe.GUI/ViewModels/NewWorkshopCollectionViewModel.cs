using ReactiveUI;
using System;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using FireAxe.Resources;

namespace FireAxe.ViewModels
{
    public class NewWorkshopCollectionViewModel : ViewModelBase, IActivatableViewModel
    {
        private AddonRoot _addonRoot;
        private AddonGroup? _addonGroup;
        private HttpClient _httpClient;

        private string _collectionId = "";
        private bool _includeLinkedCollections = true;

        private CancellationTokenSource? _createCts = null;

        private bool _created = false;

        private bool _active = false;

        public NewWorkshopCollectionViewModel(AddonRoot addonRoot, AddonGroup? addonGroup, HttpClient httpClient)
        {
            ArgumentNullException.ThrowIfNull(addonRoot);
            ArgumentNullException.ThrowIfNull(httpClient);
            _addonRoot = addonRoot;
            _addonGroup = addonGroup;
            _httpClient = httpClient;

            CreateCommand = ReactiveCommand.CreateFromTask(Create);

            this.WhenActivated((CompositeDisposable disposables) =>
            {
                _active = true;

                Disposable.Create(() =>
                {
                    _active = false;

                    _createCts?.Cancel();
                })
                .DisposeWith(disposables);
            });
        }

        public event Action? Close = null;

        public ViewModelActivator Activator { get; } = new();

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

        public async Task Create()
        {
            if (_created)
            {
                return;
            }
            ulong collectionId;
            if (!WorkshopVpkAddon.TryParsePublishedFileId(CollectionId, out collectionId))
            {
                await ShowInvalidCollectionIdInteraction.Handle(Unit.Default);
                return;
            }

            _createCts = new();
            ulong[]? itemIds = null;
            PublishedFileDetails? collectionDetails = null;
            try
            {
                var itemIdsTask = WorkshopCollectionUtils.GetWorkshopCollectionContentAsync(collectionId, _includeLinkedCollections, _httpClient, _createCts.Token);
                var collectionDetailsTask = PublishedFileDetailsUtils.GetPublishedFileDetailsAsync(collectionId, _httpClient, _createCts.Token);
                itemIds = await itemIdsTask;
                var getCollectionDetailsResult = await collectionDetailsTask;
                if (getCollectionDetailsResult.IsSucceeded)
                {
                    collectionDetails = getCollectionDetailsResult.Content;
                }
            }
            catch (OperationCanceledException) { }

            _createCts.Dispose();
            _createCts = null;

            if (itemIds == null || collectionDetails == null)
            {
                if (_active)
                {
                    await ShowCreateFailedInteraction.Handle(Unit.Default);
                }
                return;
            }

            Close?.Invoke();
            if (!_addonRoot.IsValid)
            {
                return;
            }
            if (_addonGroup != null && !_addonGroup.IsValid)
            {
                _addonGroup = null;
            }

            var collectionGroup = new AddonGroup(_addonRoot, _addonGroup);
            var collectionName = collectionGroup.Parent.GetUniqueNodeName(FileUtils.SanitizeFileName(collectionDetails.Title));
            collectionGroup.Name = collectionName;
            foreach (var itemId in itemIds)
            {
                var addon = new WorkshopVpkAddon(_addonRoot, collectionGroup);
                addon.Name = addon.Parent.GetUniqueNodeName(Texts.UnnamedWorkshopAddon);
                addon.RequestAutoSetName = true;
                addon.PublishedFileId = itemId;
            }
            _created = true;
        }
    }
}
