using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;

namespace FireAxe.ViewModels;

public class AddonNodeCustomizeImageViewModel : ViewModelBase, IActivatableViewModel
{
    private readonly AddonNode _addon;

    public AddonNodeCustomizeImageViewModel(AddonNode addon)
    {
        ArgumentNullException.ThrowIfNull(addon);
        _addon = addon;

        SelectCustomImagePathCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var path = await SelectCustomImagePathInteraction.Handle(Unit.Default);
            if (path == null)
            {
                return;
            }
            CustomImagePath = path;
        });

        this.WhenActivated((CompositeDisposable disposables) =>
        {
            _addon.WhenAnyValue(x => x.CustomImagePath)
                .Subscribe(_ => this.RaisePropertyChanged(nameof(CustomImagePath)))
                .DisposeWith(disposables);
        });
    }

    public ViewModelActivator Activator { get; } = new();

    public AddonNode Addon => _addon;

    public string CustomImagePath
    {
        get => _addon.CustomImagePath ?? "";
        set
        {
            _addon.CustomImagePath = value.Length == 0 ? null : value;
        }
    }

    public ReactiveCommand<Unit, Unit> SelectCustomImagePathCommand { get; }

    public Interaction<Unit, string?> SelectCustomImagePathInteraction { get; } = new();
}
