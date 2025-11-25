using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using ReactiveUI;

namespace FireAxe.ViewModels;

public class AddonNodePickerViewModel : ViewModelBase, IActivatableViewModel, IValidity
{
    private static readonly ValidRef<AddonGroup> s_lastAccessedGroupRef = new();

    private bool _isValid = true;

    public AddonNodePickerViewModel(AddonRoot addonRoot)
    {
        ArgumentNullException.ThrowIfNull(addonRoot);

        AddonRoot = addonRoot;

        ExplorerViewModel = new(addonRoot)
        {
            IsAddonNodeViewEnabled = false
        };

        ExplorerViewModel.GotoGroup(LastAccessedGroup);
        ExplorerViewModel.WhenAnyValue(x => x.CurrentGroup)
            .Subscribe(currentGroup => LastAccessedGroup = currentGroup);

        ConfirmCommand = ReactiveCommand.Create(() => CloseRequested?.Invoke(), ExplorerViewModel.WhenAnyValue(x => x.HasSelection));

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

    public static AddonGroup? LastAccessedGroup
    {
        get => s_lastAccessedGroupRef.TryGet();
        private set => s_lastAccessedGroupRef.Set(value);
    }

    public ViewModelActivator Activator { get; } = new();

    public bool IsValid
    {
        get => _isValid;
        private set => this.RaiseAndSetIfChanged(ref _isValid, value);
    }

    public AddonRoot AddonRoot { get; }

    public AddonNodeExplorerViewModel ExplorerViewModel { get; }

    public bool AllowMultiple { get; init; } = true;

    public ReactiveCommand<Unit, Unit> ConfirmCommand { get; }

    public event Action? CloseRequested = null;

    public IReadOnlyList<AddonNode> GetSelectedAddons()
    {
        return ExplorerViewModel.SelectedNodes.ToArray();
    }
}