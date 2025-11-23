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

public sealed class VpkAddonConflictListViewModel : ViewModelBase, IActivatableViewModel, IValidity, IDisposable
{
    private bool _disposed = false;
    private bool _valid = true;
    private bool _active = false;

    private ObservableValidRefCollection<VpkAddonConflictingDetailsViewModel> _vpkConflictingDetailsViewModels = new();
    private ReadOnlyObservableCollection<VpkAddonConflictingDetailsViewModel> _vpkConflictingDetailsViewModelsReadOnly;

    private IEnumerable<ConflictingVpkFileWithAddonsViewModel> _conflictingFileWithAddonsViewModels = [];

    private readonly CancellationTokenSource _cts = new();

    public VpkAddonConflictListViewModel(AddonRoot addonRoot)
    {
        ArgumentNullException.ThrowIfNull(addonRoot);
        AddonRoot = addonRoot;

        _vpkConflictingDetailsViewModelsReadOnly = _vpkConflictingDetailsViewModels.AsReadOnlyObservableCollection();

        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);

        this.WhenActivated((CompositeDisposable disposables) =>
        {
            addonRoot.RegisterInvalidHandler(() => IsValid = false)
                .DisposeWith(disposables);
            if (!IsValid)
            {
                return;
            }

            _active = true;

            Disposable.Create(() =>
            {
                _active = false;
            }).DisposeWith(disposables);

            RefreshCommand.Execute().Subscribe();
        });
    }

    public ViewModelActivator Activator { get; } = new();

    public bool IsValid
    {
        get => _valid;
        private set => this.RaiseAndSetIfChanged(ref _valid, value);
    }

    public AddonRoot AddonRoot { get; }

    public ReadOnlyObservableCollection<VpkAddonConflictingDetailsViewModel> VpkConflictingDetailsViewModels => _vpkConflictingDetailsViewModelsReadOnly;

    public IEnumerable<ConflictingVpkFileWithAddonsViewModel> ConflictingFileWithAddonsViewModels
    {
        get => _conflictingFileWithAddonsViewModels;
        private set => this.RaiseAndSetIfChanged(ref _conflictingFileWithAddonsViewModels, value);
    }

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    public Interaction<Exception, Unit> ShowExceptionInteraction { get; } = new();

    public void Dispose()
    {
        if (!_disposed)
        {
            IsValid = false;

            _vpkConflictingDetailsViewModels.Dispose();

            _cts.Cancel();
            _cts.Dispose();

            _disposed = true;
        }
    }

    private async Task RefreshAsync()
    {
        this.ThrowIfInvalid();

        _vpkConflictingDetailsViewModels.Clear();
        ConflictingFileWithAddonsViewModels = [];

        var cancellationToken = _cts.Token;
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
            if (_active)
            {
                await ShowExceptionInteraction.Handle(ex);
            }
            return;
        }

        if (!IsValid)
        {
            return;
        }

        _vpkConflictingDetailsViewModels.Reset(
            conflictResult.ConflictingAddons.Select(addon => new VpkAddonConflictingDetailsViewModel(addon)));
        ConflictingFileWithAddonsViewModels = conflictResult.ConflictingFiles
            .Select(file => new ConflictingVpkFileWithAddonsViewModel(file, addonRoot, conflictResult.GetConflictingAddons(file).Select(addon => addon.Id)))
            .ToArray();
    }
}