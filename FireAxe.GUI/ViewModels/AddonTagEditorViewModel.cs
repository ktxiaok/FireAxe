using ReactiveUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;

namespace FireAxe.ViewModels;

public class AddonTagEditorViewModel : ViewModelBase, IActivatableViewModel, IValidity
{
    private readonly AddonNode _addon;

    private bool _isValid = true;

    private readonly ObservableCollection<string> _selectedTags = new();
    private bool _hasSelection = false;

    public AddonTagEditorViewModel(AddonNode addon)
    {
        ArgumentNullException.ThrowIfNull(addon);
        _addon = addon;

        ((INotifyCollectionChanged)_selectedTags).CollectionChanged += (sender, e) =>
        {
            HasSelection = _selectedTags.Count > 0;
        };

        AddCommand = ReactiveCommand.CreateFromTask(async () =>
        {
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
                if (!_addon.AddTag(input))
                {
                    await ShowTagExistInteraction.Handle(Unit.Default);
                    continue;
                }
                break;
            }
        });
        DeleteCommand = ReactiveCommand.Create(() =>
        {
            string[] tags = [.. SelectedTags];
            foreach (var tag in tags)
            {
                _addon.RemoveTag(tag);
            }
        }, this.WhenAnyValue(x => x.HasSelection));
        MoveUpCommand = ReactiveCommand.Create(() =>
        {
            string[] tags = [.. GetSelectedTagsInOriginalOrder()];
            foreach (var tag in tags)
            {
                int idx = _addon.Tags.IndexOf(tag);
                if (idx == -1)
                {
                    continue;
                }
                if (idx == 0)
                {
                    continue;
                }
                _addon.MoveTag(idx, idx - 1);
            }

            _selectedTags.Clear();
            foreach (var tag in tags)
            {
                _selectedTags.Add(tag);
            }
        }, this.WhenAnyValue(x => x.HasSelection));
        MoveDownCommand = ReactiveCommand.Create(() =>
        {
            string[] tags = [.. GetSelectedTagsInOriginalOrder()];
            Array.Reverse(tags);
            foreach (var tag in tags)
            {
                int idx = _addon.Tags.IndexOf(tag);
                if (idx == -1)
                {
                    continue;
                }
                if (idx == _addon.Tags.Count - 1)
                {
                    continue;
                }
                _addon.MoveTag(idx, idx + 1);
            }

            _selectedTags.Clear();
            foreach (var tag in tags)
            {
                _selectedTags.Add(tag);
            }

        }, this.WhenAnyValue(x => x.HasSelection));

        this.WhenActivated((CompositeDisposable disposables) =>
        {
            var addon = Addon;

            addon.RegisterInvalidHandler(() => IsValid = false)
                .DisposeWith(disposables);
            if (!IsValid)
            {
                return;
            }
        });
    }

    public ViewModelActivator Activator { get; } = new();

    public AddonNode Addon => _addon;

    public bool IsValid
    {
        get => _isValid;
        private set => this.RaiseAndSetIfChanged(ref _isValid, value);
    }

    public ObservableCollection<string> SelectedTags => _selectedTags;

    public bool HasSelection
    {
        get => _hasSelection;
        private set => this.RaiseAndSetIfChanged(ref _hasSelection, value);
    }

    public ReactiveCommand<Unit, Unit> AddCommand { get; }

    public ReactiveCommand<Unit, Unit> DeleteCommand { get; }

    public ReactiveCommand<Unit, Unit> MoveUpCommand { get; }

    public ReactiveCommand<Unit, Unit> MoveDownCommand { get; }

    public Interaction<string, string?> AddTagInteraction { get; } = new();

    public Interaction<Unit, Unit> ShowInputCannotBeEmptyInteraction { get; } = new();

    public Interaction<Unit, Unit> ShowTagExistInteraction { get; } = new();

    public IEnumerable<string> GetSelectedTagsInOriginalOrder()
    {
        HashSet<string> selectedTags = [.. SelectedTags];
        foreach (var tag in _addon.Tags)
        {
            if (selectedTags.Contains(tag))
            {
                yield return tag;
            }
        }
    }
}
