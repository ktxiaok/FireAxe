using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace FireAxe.ViewModels;

public class AddonTagManagerViewModel : ViewModelBase, IActivatableViewModel
{
    private readonly MainWindowViewModel _mainWindowViewModel;

    private bool _addonRootPresent = false;

    private readonly ObservableCollection<string> _selectedTags = new();
    private string? _selectedTag = null;
    private bool _hasSelection = false;
    private bool _hasSingleSelection = false;

    public AddonTagManagerViewModel(MainWindowViewModel mainWindowViewModel)
    {
        ArgumentNullException.ThrowIfNull(mainWindowViewModel);
        _mainWindowViewModel = mainWindowViewModel;

        ((INotifyCollectionChanged)_selectedTags).CollectionChanged += (sender, e) =>
        {
            if (_selectedTags.Count == 1)
            {
                SelectedTag = _selectedTags[0];
                HasSingleSelection = true;
            }
            else
            {
                SelectedTag = null;
                HasSingleSelection = false;
            }

            HasSelection = _selectedTags.Count > 0;
        };

        RefreshCommand = ReactiveCommand.Create(Refresh, this.WhenAnyValue(x => x.IsAddonRootPresent));
        AddCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var addonRoot = _mainWindowViewModel.AddonRoot;
            if (addonRoot == null)
            {
                return;
            }

            string? input = "";
            while (true)
            {
                input = await AddTagInteraction.Handle(input);
                if (input == null)
                {
                    return;
                }
                if (input.Length == 0)
                {
                    await ShowInputCannotBeEmptyInteraction.Handle(Unit.Default);
                    continue;
                }
                if (!addonRoot.IsValid)
                {
                    return;
                }
                if (!addonRoot.AddCustomTag(input))
                {
                    await ShowTagExistInteraction.Handle(Unit.Default);
                    continue;
                }
                break;
            }
        }, this.WhenAnyValue(x => x.IsAddonRootPresent));
        RenameCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var addonRoot = _mainWindowViewModel.AddonRoot;
            if (addonRoot == null)
            {
                return;
            }
            var oldTag = SelectedTag;
            if (oldTag == null)
            {
                return;
            }

            string? input = oldTag;
            while (true)
            {
                input = await RenameTagInteraction.Handle(input);
                if (input == null)
                {
                    return;
                }
                if (input.Length == 0)
                {
                    await ShowInputCannotBeEmptyInteraction.Handle(Unit.Default);
                    continue;
                }
                if (input == oldTag)
                {
                    return;
                }
                if (!addonRoot.IsValid)
                {
                    return;
                }
                addonRoot.RenameCustomTag(oldTag, input);
                break;
            }
        }, this.WhenAnyValue(x => x.HasSingleSelection));
        DeleteCommand = ReactiveCommand.Create(() =>
        {
            var addonRoot = _mainWindowViewModel.AddonRoot;
            if (addonRoot == null)
            {
                return;
            }

            string[] tags = [.. SelectedTags];
            foreach (var tag in tags)
            {
                addonRoot.RemoveCustomTag(tag);
            }
        }, this.WhenAnyValue(x => x.HasSelection));
        DeleteCompletelyCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            bool confirm = await ConfirmDeleteTagCompletelyInteraction.Handle(Unit.Default);
            if (!confirm)
            {
                return;
            }

            var addonRoot = _mainWindowViewModel.AddonRoot;
            if (addonRoot == null)
            {
                return;
            }

            string[] tags = [.. SelectedTags];
            foreach (var tag in tags)
            {
                addonRoot.RemoveCustomTagCompletely(tag);
            }
        }, this.WhenAnyValue(x => x.HasSelection));
        MoveUpCommand = ReactiveCommand.Create(() =>
        {
            var addonRoot = _mainWindowViewModel.AddonRoot;
            if (addonRoot == null)
            {
                return;
            }

            string[] tags = [.. GetSelectedTagsInOriginalOrder()];
            foreach (var tag in tags)
            {
                int idx = addonRoot.CustomTags.IndexOf(tag);
                if (idx == -1)
                {
                    continue;
                }
                if (idx == 0)
                {
                    continue;
                }
                addonRoot.MoveCustomTag(idx, idx - 1);
            }

            _selectedTags.Clear();
            foreach (var tag in tags)
            {
                _selectedTags.Add(tag);
            }
        }, this.WhenAnyValue(x => x.HasSelection));
        MoveDownCommand = ReactiveCommand.Create(() =>
        {
            var addonRoot = _mainWindowViewModel.AddonRoot;
            if (addonRoot == null)
            {
                return;
            }

            string[] tags = [.. GetSelectedTagsInOriginalOrder()];
            Array.Reverse(tags);
            foreach (var tag in tags)
            {
                int idx = addonRoot.CustomTags.IndexOf(tag);
                if (idx == -1)
                {
                    continue;
                }
                if (idx == addonRoot.CustomTags.Count - 1)
                {
                    continue;
                }
                addonRoot.MoveCustomTag(idx, idx + 1);
            }

            _selectedTags.Clear();
            foreach (var tag in tags)
            {
                _selectedTags.Add(tag);
            }
        }, this.WhenAnyValue(x => x.HasSelection));
        this.WhenActivated((CompositeDisposable disposables) =>
        {
            _mainWindowViewModel.WhenAnyValue(x => x.AddonRoot)
            .Subscribe(addonRoot =>
            {
                IsAddonRootPresent = addonRoot != null;
                Refresh();
            })
            .DisposeWith(disposables);

            Disposable.Create(() =>
            {
                IsAddonRootPresent = false;
            })
            .DisposeWith(disposables);
        });
    }

    public ViewModelActivator Activator { get; } = new();

    public MainWindowViewModel MainWindowViewModel => _mainWindowViewModel;

    public ObservableCollection<string> SelectedTags => _selectedTags;

    public string? SelectedTag
    {
        get => _selectedTag;
        private set => this.RaiseAndSetIfChanged(ref _selectedTag, value);
    }

    public bool HasSelection
    {
        get => _hasSelection;
        private set => this.RaiseAndSetIfChanged(ref _hasSelection, value);
    }

    public bool HasSingleSelection
    {
        get => _hasSingleSelection;
        private set => this.RaiseAndSetIfChanged(ref _hasSingleSelection, value);
    }

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    public ReactiveCommand<Unit, Unit> AddCommand { get; }

    public ReactiveCommand<Unit, Unit> RenameCommand { get; }

    public ReactiveCommand<Unit, Unit> DeleteCommand { get; }

    public ReactiveCommand<Unit, Unit> DeleteCompletelyCommand { get; }

    public ReactiveCommand<Unit, Unit> MoveUpCommand { get; }

    public ReactiveCommand<Unit, Unit> MoveDownCommand { get; }

    public Interaction<string, string?> AddTagInteraction { get; } = new();

    public Interaction<string, string?> RenameTagInteraction { get; } = new();

    public Interaction<Unit, Unit> ShowInputCannotBeEmptyInteraction { get; } = new();

    public Interaction<Unit, Unit> ShowTagExistInteraction { get; } = new();

    public Interaction<Unit, bool> ConfirmDeleteTagCompletelyInteraction { get; } = new();

    private bool IsAddonRootPresent
    {
        get => _addonRootPresent;
        set => this.RaiseAndSetIfChanged(ref _addonRootPresent, value);
    }

    public void Refresh()
    {
        var addonRoot = _mainWindowViewModel.AddonRoot;
        if (addonRoot == null)
        {
            return;
        }

        addonRoot.RefreshCustomTags();
    }

    public IEnumerable<string> GetSelectedTagsInOriginalOrder()
    {
        var addonRoot = _mainWindowViewModel.AddonRoot;
        if (addonRoot == null)
        {
            yield break;
        }

        HashSet<string> selectedTagSet = [.. SelectedTags];
        foreach (var tag in addonRoot.CustomTags)
        {
            if (selectedTagSet.Contains(tag))
            {
                yield return tag;
            }
        }
    }
}
