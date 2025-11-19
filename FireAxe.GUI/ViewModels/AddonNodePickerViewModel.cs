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
    private bool _isValid = true;

    public AddonNodePickerViewModel(AddonRoot addonRoot)
    {
        ArgumentNullException.ThrowIfNull(addonRoot);

        AddonRoot = addonRoot;

        ExplorerViewModel = new(addonRoot)
        {
            IsAddonNodeViewEnabled = false
        };

        ConfirmCommand = ReactiveCommand.Create(() => CloseRequested?.Invoke(), ExplorerViewModel.WhenAnyValue(x => x.HasSelection));

        this.WhenActivated((CompositeDisposable disposables) =>
        {
            var addonRoot = AddonRoot;

            addonRoot.RegisterInvalidHandler(() => IsValid = false)
                .DisposeWith(disposables);
        });
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