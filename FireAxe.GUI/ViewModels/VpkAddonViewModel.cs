using Avalonia.Media.Imaging;
using ReactiveUI;
using System;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;

namespace FireAxe.ViewModels;

public abstract class VpkAddonViewModel : AddonNodeViewModel
{
    private VpkAddonInfo? _info = null;

    public VpkAddonViewModel(VpkAddon addon) : base(addon)
    {
        IgnoreAllConflictingFilesCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            bool confirm = await ConfirmIgnoreAllConflictingFilesInteraction.Handle(Unit.Default);
            if (!confirm)
            {
                return;
            }

            IgnoreAllConflictingFiles();
        });
        RemoveAllConflictIgnoringFilesCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            bool confirm = await ConfirmRemoveAllConflictIgnoringFilesInteraction.Handle(Unit.Default);
            if (!confirm)
            {
                return;
            }

            var addon = Addon;
            if (addon is null)
            {
                return;
            }

            addon.ClearConflictIgnoringFiles();
        });
    }

    public new VpkAddon? Addon => (VpkAddon?)base.Addon;

    public override Type AddonType => typeof(VpkAddon);

    public VpkAddonInfo? Info
    {
        get => _info;
        private set => this.RaiseAndSetIfChanged(ref _info, value);
    }

    public ReactiveCommand<Unit, Unit> IgnoreAllConflictingFilesCommand { get; }

    public ReactiveCommand<Unit, Unit> RemoveAllConflictIgnoringFilesCommand { get; }

    public Interaction<Unit, bool> ConfirmIgnoreAllConflictingFilesInteraction { get; } = new();

    public Interaction<Unit, bool> ConfirmRemoveAllConflictIgnoringFilesInteraction { get; } = new();

    public void IgnoreConflictingFile(string? file)
    {
        if (file is null)
        {
            return;
        }

        var addon = Addon;
        if (addon is null)
        {
            return;
        }

        addon.AddConflictIgnoringFile(file);
    }

    public void RemoveConflictIgnoringFile(string? file)
    {
        if (file is null)
        {
            return;
        }

        var addon = Addon;
        if (addon is null)
        {
            return;
        }

        addon.RemoveConflictIgnoringFile(file);
    }

    public void IgnoreAllConflictingFiles()
    {
        var addon = Addon;
        if (addon is null)
        {
            return;
        }

        string[] files = [.. addon.ConflictingFiles];
        foreach (var file in files)
        {
            addon.AddConflictIgnoringFile(file);
        }
    }

    protected override void OnRefresh(CancellationToken cancellationToken)
    {
        base.OnRefresh(cancellationToken);

        var addon = Addon;

        Info = addon?.RetrieveInfo();
    }

    protected override void OnClearCaches()
    {
        base.OnClearCaches();

        Info = null;
    }
}
