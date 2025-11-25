using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;

namespace FireAxe.ViewModels;

public sealed class AddonNameAutoSetterViewModel : ViewModelBase, IActivatableViewModel, IValidity, IDisposable
{
    public class TargetAddonViewModel : AddonNodeSimpleViewModel
    {
        private readonly AddonNameAutoSetterViewModel _host;

        public TargetAddonViewModel(AddonNameAutoSetterViewModel host, Guid addonId) : base(host.AddonRoot, addonId)
        {
            _host = host;
        }

        public void Remove()
        {
            _host.RemoveTargetAddon(AddonId);
        }
    }

    private bool _isValid = true;

    private bool _disposed = false;

    private readonly ObservableCollection<AddonNameAutoSetJobViewModel> _jobViewModels = new();
    private readonly ReadOnlyObservableCollection<AddonNameAutoSetJobViewModel> _jobViewModelsReadOnly;

    private readonly ObservableCollection<Guid> _targetAddonIds = new();
    private readonly ReadOnlyObservableCollection<Guid> _targetAddonIdsReadOnly;
    private readonly ReadOnlyObservableCollection<TargetAddonViewModel> _targetAddonViewModels;

    private bool _forceUseWorkshopTitle = false;

    public AddonNameAutoSetterViewModel(AddonRoot addonRoot)
    {
        ArgumentNullException.ThrowIfNull(addonRoot);

        AddonRoot = addonRoot;

        _jobViewModelsReadOnly = new(_jobViewModels);
        _targetAddonIdsReadOnly = new(_targetAddonIds);

        _targetAddonIds.ToObservableChangeSet()
            .Transform(id => new TargetAddonViewModel(this, id))
            .Bind(out _targetAddonViewModels)
            .Subscribe();

        var targetAddonsNotEmpty = _targetAddonIds.WhenAnyValue(x => x.Count).Select(count => count > 0);
        StartCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            if (!Start())
            {
                await ShowNoItemsToStartInteraction.Handle(Unit.Default);
            }
        }, targetAddonsNotEmpty);

        var jobsNotEmpty = _jobViewModels.WhenAnyValue(x => x.Count).Select(count => count > 0);
        ApplyAllCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            int errorCount = 0;
            foreach (var jobViewModel in _jobViewModels)
            {
                if (jobViewModel.IsApplied)
                {
                    continue;
                }
                if (!jobViewModel.Apply())
                {
                    errorCount++;
                }
            }
            if (errorCount > 0)
            {
                await ShowMultipleFailedApplyInteraction.Handle(errorCount);
            }
        }, jobsNotEmpty);
        RetryAllCommand = ReactiveCommand.Create(() =>
        {
            foreach (var jobViewModel in _jobViewModels)
            {
                jobViewModel.Retry();
            }
        }, jobsNotEmpty);
        RemoveAllCommand = ReactiveCommand.Create(() =>
        {
            foreach (var jobViewModel in _jobViewModels)
            {
                jobViewModel.Dispose();
            }
            _jobViewModels.Clear();
        }, jobsNotEmpty);

        this.WhenActivated((CompositeDisposable disposables) =>
        {
            var addonRoot = AddonRoot;

            addonRoot.RegisterInvalidHandler(() => IsValid = false)
                .DisposeWith(disposables);
            if (!IsValid)
            {
                return;
            }
        });
    }

    public ViewModelActivator Activator { get; } = new();

    public bool IsValid
    {
        get => _isValid;
        private set => this.RaiseAndSetIfChanged(ref _isValid, value);
    }

    public AddonRoot AddonRoot { get; }

    public ReadOnlyObservableCollection<AddonNameAutoSetJobViewModel> JobViewModels => _jobViewModelsReadOnly;

    public ReadOnlyObservableCollection<Guid> TargetAddonIds => _targetAddonIdsReadOnly;

    public IEnumerable<AddonNode> TargetAddons
    {
        get
        {
            var addonRoot = AddonRoot;
            foreach (var id in _targetAddonIds)
            {
                if (addonRoot.TryGetNodeById(id, out var addon))
                {
                    yield return addon;
                }
            }
        }
    }

    public ReadOnlyObservableCollection<TargetAddonViewModel> TargetAddonViewModels => _targetAddonViewModels;

    public bool ForceUseWorkshopTitle
    {
        get => _forceUseWorkshopTitle;
        set => this.RaiseAndSetIfChanged(ref _forceUseWorkshopTitle, value);
    }

    public ReactiveCommand<Unit, Unit> StartCommand { get; }

    public ReactiveCommand<Unit, Unit> ApplyAllCommand { get; }

    public ReactiveCommand<Unit, Unit> RetryAllCommand { get; }

    public ReactiveCommand<Unit, Unit> RemoveAllCommand { get; }

    public Interaction<Unit, Unit> ShowNoItemsToStartInteraction { get; } = new();

    public Interaction<int, Unit> ShowMultipleFailedApplyInteraction { get; } = new();

    public bool Start()
    {
        this.ThrowIfInvalid();

        bool hasItemsToStart = false;
        foreach (var addon in TargetAddons.SelectMany(addon => addon.GetSelfAndDescendants()))
        {
            if (TryAddJob(addon))
            {
                hasItemsToStart = true;
            }
        }
        return hasItemsToStart;
    }

    public bool AddTargetAddon(Guid id)
    {
        this.ThrowIfInvalid();

        if (_targetAddonIds.Contains(id))
        {
            return false;
        }
        _targetAddonIds.Add(id);
        return true;
    }

    public bool RemoveTargetAddon(Guid id)
    {
        this.ThrowIfInvalid();

        return _targetAddonIds.Remove(id);
    }

    public void RemoveJobViewModel(AddonNameAutoSetJobViewModel jobViewModel)
    {
        _jobViewModels.Remove(jobViewModel);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            IsValid = false;
            foreach (var jobViewModel in _jobViewModels)
            {
                jobViewModel.Dispose();
            }

            _disposed = true;
        }
    }

    private bool TryAddJob(AddonNode addon)
    {
        if (!addon.CanGetSuggestedName)
        {
            return false;
        }
        foreach (var jobViewModel in _jobViewModels)
        {
            if (jobViewModel.Addon == addon)
            {
                return false;
            }
        }

        object? settingArg = null;
        if (ForceUseWorkshopTitle && addon is WorkshopVpkAddon)
        {
            settingArg = WorkshopVpkAddon.ForceUseWorkshopTitle;
        }
        _jobViewModels.Add(new AddonNameAutoSetJobViewModel(addon, settingArg));
        return true;
    }
}