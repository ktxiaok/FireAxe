using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using ReactiveUI;
using Serilog;
using FireAxe.Resources;

namespace FireAxe.ViewModels;

public class WorkshopVpkFinderViewModel : ViewModelBase, IActivatableViewModel, IDisposable
{
    public class StagedItem : ReactiveObject
    {
        private readonly WorkshopVpkFinderViewModel _finder;

        private string? _title = null;
        private bool _isGetTitleFailed = false;

        private CancellationTokenSource _cancellationTokenSource = new();

        internal StagedItem(ulong publishedFileId, WorkshopVpkFinderViewModel finder)
        {
            PublishedFileId = publishedFileId;
            _finder = finder;

            GetTitle();
        }

        public ulong PublishedFileId { get; }

        public string? Title
        {
            get => _title;
            private set => this.RaiseAndSetIfChanged(ref _title, value);
        }

        public bool IsGetTitleFailed
        {
            get => _isGetTitleFailed;
            private set => this.RaiseAndSetIfChanged(ref _isGetTitleFailed, value);
        }

        public void Remove()
        {
            CancelTasks();
            _finder._stagedItems.Remove(this);
        }

        public void OpenWebsite()
        {
            Utils.OpenWebsite($"https://steamcommunity.com/sharedfiles/filedetails/?id={PublishedFileId}");
        }

        private async void GetTitle()
        {
            try
            {
                var result = await PublishedFileDetailsUtils.GetPublishedFileDetailsAsync(PublishedFileId, _finder._httpClient, _cancellationTokenSource.Token);
                if (result.IsSucceeded)
                {
                    Title = result.Content.Title;
                }
                else
                {
                    IsGetTitleFailed = true;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        internal void CancelTasks()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
        }
    }

    private bool _disposed = false;

    private readonly MainWindowViewModel _mainWindowViewModel;
    private readonly HttpClient _httpClient;

    private readonly ObservableCollection<StagedItem> _stagedItems = new();
    private readonly ReadOnlyObservableCollection<StagedItem> _stagedItemsReadOnly;

    private bool _isAddonRootPresent = false;

    public WorkshopVpkFinderViewModel(MainWindowViewModel mainWindowViewModel, HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(mainWindowViewModel);
        ArgumentNullException.ThrowIfNull(httpClient);

        _mainWindowViewModel = mainWindowViewModel;
        _httpClient = httpClient;

        _stagedItemsReadOnly = new(_stagedItems);

        FindFromDirectoryCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            ThrowIfDisposed();

            string? workshopPath = null;
            if (_mainWindowViewModel.AddonRoot?.GamePath is { } gamePath && gamePath.Length > 0)
            {
                workshopPath = Path.Join(GamePathUtils.GetAddonsPath(gamePath), "workshop");
            }
            var dirPath = await ChooseDirectoryInteraction.Handle(workshopPath);
            if (dirPath is null)
            {
                return;
            }
            FindFromDirectory(dirPath);
        });

        CreateCommand = ReactiveCommand.Create(() =>
        {
            Create();
            CloseRequested?.Invoke();
        }, this.WhenAnyValue(x => x.IsAddonRootPresent, x => x.StagedItems.Count).Select(_ => IsAddonRootPresent && StagedItems.Count > 0));

        this.WhenActivated((CompositeDisposable disposables) =>
        {
            ThrowIfDisposed();

            _mainWindowViewModel.WhenAnyValue(x => x.AddonRoot)
                .Subscribe(addonRoot => IsAddonRootPresent = addonRoot is not null)
                .DisposeWith(disposables);

            Disposable.Create(() =>
            {
                IsAddonRootPresent = false;
            }).DisposeWith(disposables);
        });
    }

    ~WorkshopVpkFinderViewModel()
    {
        Dispose(false);
    }

    public ReadOnlyObservableCollection<StagedItem> StagedItems => _stagedItemsReadOnly;

    public ViewModelActivator Activator { get; } = new();

    public ReactiveCommand<Unit, Unit> FindFromDirectoryCommand { get; }

    public ReactiveCommand<Unit, Unit> CreateCommand { get; }

    public Interaction<string?, string?> ChooseDirectoryInteraction { get; } = new();

    private bool IsAddonRootPresent
    {
        get => _isAddonRootPresent;
        set => this.RaiseAndSetIfChanged(ref _isAddonRootPresent, value);
    }

    public event Action? CloseRequested = null;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            foreach (var stagedItem in _stagedItems)
            {
                stagedItem.CancelTasks();
            }
            
            _disposed = true;
        }
    }

    public void Create()
    {
        ThrowIfDisposed();

        var addonRoot = _mainWindowViewModel.AddonRoot;
        if (addonRoot is null)
        {
            return;
        }
        var addonGroup = _mainWindowViewModel.AddonNodeExplorerViewModel?.CurrentGroup;

        foreach (var stagedItem in _stagedItems)
        {
            var addon = AddonNode.Create<WorkshopVpkAddon>(addonRoot, addonGroup);
            if (stagedItem.Title is { } title)
            {
                var name = FileUtils.SanitizeFileName(title);
                if (name.Length == 0)
                {
                    name = "UNNAMED";
                }
                name = addon.Parent.GetUniqueNodeName(name);
                addon.Name = name;
            }
            else
            {
                addon.Name = addon.Parent.GetUniqueNodeName(Texts.UnnamedWorkshopAddon);
                addon.RequestAutoSetName = true;
            }
            addon.PublishedFileId = stagedItem.PublishedFileId;
        }
    }

    public int FindFromDirectory(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        ThrowIfDisposed();

        int count = 0;
        try
        {
            if (Directory.Exists(path))
            {
                foreach (string file in Directory.EnumerateFiles(path, "*.vpk", SearchOption.AllDirectories))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (WorkshopVpkAddon.TryParsePublishedFileId(name, out var id) && !ContainsPublishedFileId(id))
                    {
                        _stagedItems.Add(new StagedItem(id, this));
                        count++;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Exception occurred during {nameof(WorkshopVpkFinderViewModel)}.{nameof(FindFromDirectory)}.");
        }
        return count;
    }

    private bool ContainsPublishedFileId(ulong id)
    {
        foreach (var stagedItem in _stagedItems)
        {
            if (stagedItem.PublishedFileId == id)
            {
                return true;
            }
        }
        return false;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }
}