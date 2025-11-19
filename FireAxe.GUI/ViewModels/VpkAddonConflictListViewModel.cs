using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;

namespace FireAxe.ViewModels;

public class VpkAddonConflictListViewModel : ViewModelBase, IActivatableViewModel, IValidity
{
    private bool _isValid = true;

    private bool _isActive = false;

    private readonly AddonRoot _addonRoot;

    private ObservableValidRefCollection<VpkAddonConflictingDetailsViewModel>? _vpkConflictingDetailsViewModels = null;
    private ReadOnlyObservableCollection<VpkAddonConflictingDetailsViewModel> _vpkConflictingDetailsViewModelsReadOnly = ReadOnlyObservableCollection<VpkAddonConflictingDetailsViewModel>.Empty;

    private IEnumerable<ConflictingVpkFileWithAddonsViewModel> _conflictingFileWithAddonsViewModels = [];

    private bool _isRefreshing = false;
    private object? _refreshId = null;
    private CancellationTokenSource? _waitRefreshCts = null;

    public VpkAddonConflictListViewModel(AddonRoot addonRoot)
    {
        ArgumentNullException.ThrowIfNull(addonRoot);
        _addonRoot = addonRoot;

        this.WhenActivated((CompositeDisposable disposables) =>
        {
            _isActive = true;

            addonRoot.RegisterInvalidHandler(() => IsValid = false).DisposeWith(disposables);

            _vpkConflictingDetailsViewModels = new();
            VpkConflictingDetailsViewModels = _vpkConflictingDetailsViewModels.AsReadOnlyObservableCollection();

            _ = RefreshAsync();

            Disposable.Create(() =>
            {
                _isActive = false;

                CancelWaitRefresh();

                Utils.DisposeAndSetNull(ref _vpkConflictingDetailsViewModels);
                VpkConflictingDetailsViewModels = ReadOnlyObservableCollection<VpkAddonConflictingDetailsViewModel>.Empty;

                ConflictingFileWithAddonsViewModels = [];
            }).DisposeWith(disposables);
        });

        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);
        CancelRefreshCommand = ReactiveCommand.Create(CancelRefresh);
    }

    public ViewModelActivator Activator { get; } = new();

    public bool IsValid
    {
        get => _isValid;
        private set => this.RaiseAndSetIfChanged(ref _isValid, value);
    }

    public AddonRoot AddonRoot => _addonRoot;

    public ReadOnlyObservableCollection<VpkAddonConflictingDetailsViewModel> VpkConflictingDetailsViewModels
    {
        get => _vpkConflictingDetailsViewModelsReadOnly;
        private set => this.RaiseAndSetIfChanged(ref _vpkConflictingDetailsViewModelsReadOnly, value);
    }

    public IEnumerable<ConflictingVpkFileWithAddonsViewModel> ConflictingFileWithAddonsViewModels
    {
        get => _conflictingFileWithAddonsViewModels;
        private set => this.RaiseAndSetIfChanged(ref _conflictingFileWithAddonsViewModels, value);
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set => this.RaiseAndSetIfChanged(ref _isRefreshing, value);
    }

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    public ReactiveCommand<Unit, Unit> CancelRefreshCommand { get; }

    public Interaction<Exception, Unit> ShowExceptionInteraction { get; } = new();

    public async Task RefreshAsync()
    {
        if (!_isActive)
        {
            return;
        }
        if (IsRefreshing)
        {
            return;
        }

        _vpkConflictingDetailsViewModels!.Clear();
        ConflictingFileWithAddonsViewModels = [];

        IsRefreshing = true;
        _waitRefreshCts ??= new();
        var cancellationToken = _waitRefreshCts.Token;
        _refreshId = new();
        var refreshId = _refreshId;
        var addonRoot = AddonRoot;

        VpkAddonConflictResult conflictResult;
        try
        {
            conflictResult = await addonRoot.CheckVpkConflictsAsync().WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            if (_isActive)
            {
                await ShowExceptionInteraction.Handle(ex);
            }
            return;
        }

        if (!_isActive)
        {
            return;
        }
        if (refreshId != _refreshId)
        {
            return;
        }

        _vpkConflictingDetailsViewModels!.Reset(
            conflictResult.ConflictingAddons.Select(addon => new VpkAddonConflictingDetailsViewModel(addon)));
        ConflictingFileWithAddonsViewModels = conflictResult.ConflictingFiles
            .Select(file => new ConflictingVpkFileWithAddonsViewModel(file, addonRoot, conflictResult.GetConflictingAddons(file).Select(addon => addon.Id)))
            .ToArray();

        _refreshId = null;
        IsRefreshing = false;
    }

    public void CancelRefresh()
    {
        if (!IsValid)
        {
            return;
        }
        if (!IsRefreshing)
        {
            return;
        }

        CancelWaitRefresh();
        AddonRoot.CancelCheckVpkConflicts();
    }

    private void CancelWaitRefresh()
    {
        _refreshId = null;
        if (_waitRefreshCts is not null)
        {
            _waitRefreshCts.Cancel();
            _waitRefreshCts.Dispose();
            _waitRefreshCts = null;
        }
        IsRefreshing = false;
    }
}