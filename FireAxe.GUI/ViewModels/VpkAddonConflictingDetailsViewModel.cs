using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;

namespace FireAxe.ViewModels;

public class VpkAddonConflictingDetailsViewModel : ViewModelBase, IActivatableViewModel
{
    private ReadOnlyObservableCollection<ConflictingVpkAddonWithFilesViewModel> _conflictingAddonWithFilesViewModels = ReadOnlyObservableCollection<ConflictingVpkAddonWithFilesViewModel>.Empty;
    private ReadOnlyObservableCollection<ConflictingVpkFileWithAddonsViewModel> _conflictingFileWithAddonsViewModels = ReadOnlyObservableCollection<ConflictingVpkFileWithAddonsViewModel>.Empty;

    public VpkAddonConflictingDetailsViewModel(VpkAddon vpkAddon)
    {
        ArgumentNullException.ThrowIfNull(vpkAddon);
        Addon = vpkAddon;

        this.WhenActivated((CompositeDisposable disposables) =>
        {
            var addon = Addon;
            var addonRoot = addon.Root;

            addon.ConflictingAddonIdsWithFiles
                .ToObservableChangeSet()
                .Transform(item => new ConflictingVpkAddonWithFilesViewModel(addonRoot, item.AddonId, item.Files))
                .Bind(out ReadOnlyObservableCollection<ConflictingVpkAddonWithFilesViewModel> conflictingAddonWithFilesViewModels)
                .Subscribe()
                .DisposeWith(disposables);
            ConflictingAddonWithFilesViewModels = conflictingAddonWithFilesViewModels;

            addon.ConflictingFilesWithAddonIds
                .ToObservableChangeSet()
                .Transform(item => new ConflictingVpkFileWithAddonsViewModel(item.File, addonRoot, item.AddonIds))
                .Bind(out ReadOnlyObservableCollection<ConflictingVpkFileWithAddonsViewModel> conflictingFileWithAddonsViewModels)
                .Subscribe()
                .DisposeWith(disposables);
            ConflictingFileWithAddonsViewModels = conflictingFileWithAddonsViewModels;

            Disposable.Create(() =>
            {
                ConflictingAddonWithFilesViewModels = ReadOnlyObservableCollection<ConflictingVpkAddonWithFilesViewModel>.Empty;
                ConflictingFileWithAddonsViewModels = ReadOnlyObservableCollection<ConflictingVpkFileWithAddonsViewModel>.Empty;
            }).DisposeWith(disposables);
        });

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

    public ViewModelActivator Activator { get; } = new();

    public VpkAddon Addon { get; }

    public ReadOnlyObservableCollection<ConflictingVpkAddonWithFilesViewModel> ConflictingAddonWithFilesViewModels
    {
        get => _conflictingAddonWithFilesViewModels;
        private set => this.RaiseAndSetIfChanged(ref _conflictingAddonWithFilesViewModels, value); 
    }

    public ReadOnlyObservableCollection<ConflictingVpkFileWithAddonsViewModel> ConflictingFileWithAddonsViewModels
    {
        get => _conflictingFileWithAddonsViewModels;
        private set => this.RaiseAndSetIfChanged(ref _conflictingFileWithAddonsViewModels, value);
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

        string[] files = [.. addon.ConflictingFilesWithAddonIds.Select(item => item.File)];
        foreach (var file in files)
        {
            addon.AddConflictIgnoringFile(file);
        }
    }
}

public class ConflictingVpkAddonWithFilesViewModel : ViewModelBase
{
    public ConflictingVpkAddonWithFilesViewModel(AddonRoot addonRoot, Guid addonId, IEnumerable<string> files)
    {
        ArgumentNullException.ThrowIfNull(addonRoot);
        ArgumentNullException.ThrowIfNull(files);

        AddonViewModel = new AddonNodeSimpleViewModel(addonRoot, addonId);
        Files = files;
    }

    public AddonNodeSimpleViewModel AddonViewModel { get; }

    public IEnumerable<string> Files { get; }
}

public class ConflictingVpkFileWithAddonsViewModel : ViewModelBase
{
    public ConflictingVpkFileWithAddonsViewModel(string file, AddonRoot addonRoot, IEnumerable<Guid> addonIds)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(addonRoot);
        ArgumentNullException.ThrowIfNull(addonIds);

        File = file;
        AddonViewModels = addonIds.Select(id => new AddonNodeSimpleViewModel(addonRoot, id)).ToArray();
    }

    public string File { get; }

    public IEnumerable<AddonNodeSimpleViewModel> AddonViewModels { get; }
}