using System;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;

namespace FireAxe.ViewModels;

public class AddonNodeDependenciesViewModel : ViewModelBase, IActivatableViewModel, IValidity
{
    public class DependencyViewModel : AddonNodeSimpleViewModel
    {
        private readonly AddonNodeDependenciesViewModel _host;

        internal DependencyViewModel(AddonNodeDependenciesViewModel host, Guid dependencyId) : base(host.Addon.Root, dependencyId)
        {
            _host = host;
        }

        public void Remove()
        {
            _host.Addon.RemoveDependentAddon(AddonId);
        }
    }

    private bool _isValid = true;

    private ReadOnlyObservableCollection<DependencyViewModel> _dependencyViewModels = ReadOnlyObservableCollection<DependencyViewModel>.Empty;

    public AddonNodeDependenciesViewModel(AddonNode addon)
    {
        ArgumentNullException.ThrowIfNull(addon);

        Addon = addon;

        this.WhenActivated((CompositeDisposable disposables) =>
        {
            var addon = Addon;

            addon.RegisterInvalidHandler(() => IsValid = false)
                .DisposeWith(disposables);

            addon.DependentAddonIds.ToObservableChangeSet()
                .Transform(id => new DependencyViewModel(this, id))
                .Bind(out ReadOnlyObservableCollection<DependencyViewModel> dependencyViewModels)
                .Subscribe()
                .DisposeWith(disposables);
            DependencyViewModels = dependencyViewModels;
            Disposable.Create(() => DependencyViewModels = ReadOnlyObservableCollection<DependencyViewModel>.Empty)
                .DisposeWith(disposables);
        });
    }

    public ViewModelActivator Activator { get; } = new();

    public bool IsValid
    {
        get => _isValid;
        private set => this.RaiseAndSetIfChanged(ref _isValid, value);
    }

    public AddonNode Addon { get; }

    public ReadOnlyObservableCollection<DependencyViewModel> DependencyViewModels
    {
        get => _dependencyViewModels;
        private set => this.RaiseAndSetIfChanged(ref _dependencyViewModels, value);
    }
}