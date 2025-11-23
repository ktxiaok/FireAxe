using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Aggregation;
using DynamicData.Binding;
using ReactiveUI;
using Serilog;

namespace FireAxe.ViewModels;

public class FileCleanerViewModel : ViewModelBase, IActivatableViewModel, IValidity
{
    public class StagedItem : ReactiveObject
    {
        private readonly FileCleanerViewModel _host;

        private bool _isDeleting = false;
        private Exception? _deletionException = null;

        internal StagedItem(FileCleanerViewModel host, string path, string? relativePath = null)
        {
            _host = host;
            Path = path;
            relativePath ??= FileSystemUtils.NormalizePath(System.IO.Path.GetRelativePath(_host.AddonRoot.DirectoryPath, path));
            RelativePath = relativePath;

            try
            {
                if (File.Exists(path))
                {
                    FileSize = new FileInfo(path).Length;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred during getting the size of the file: {FilePath}", path);
            }

            DeleteCommand = ReactiveCommand.Create(Delete, this.WhenAnyValue(x => x.IsDeleting).Select(isDeleting => !isDeleting));
            RetainCommand = ReactiveCommand.Create(Remove);
            ShowInFileExplorerCommand = ReactiveCommand.Create(() => Utils.ShowInFileExplorer(Path));
        }

        public string Path { get; }

        public string RelativePath { get; }

        public long? FileSize { get; } = null;

        public bool IsDeleting
        {
            get => _isDeleting;
            private set => this.RaiseAndSetIfChanged(ref _isDeleting, value);
        }

        public Exception? DeletionException
        {
            get => _deletionException;
            private set => this.RaiseAndSetIfChanged(ref _deletionException, value);
        }

        public ReactiveCommand<Unit, Unit> DeleteCommand { get; }

        public ReactiveCommand<Unit, Unit> RetainCommand { get; }

        public ReactiveCommand<Unit, Unit> ShowInFileExplorerCommand { get; }

        public async void Delete()
        {
            if (IsDeleting)
            {
                return;
            }

            DeletionException = null;
            IsDeleting = true;

            var path = Path;
            bool success = false;
            try
            {
                await Task.Run(() =>
                {
                    if (FileSystemUtils.Exists(path))
                    {
                        FileSystemUtils.MoveToRecycleBin(path);
                    }
                });
                success = true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred during deleting the file: {Path}", path);
                DeletionException = ex;
            }

            IsDeleting = false;

            if (success)
            {
                Remove();
            }
        }

        public void Remove()
        {
            _host._stagedItems.Remove(this);
        }
    }

    public class SourceAddonViewModel : AddonNodeSimpleViewModel
    {
        private readonly FileCleanerViewModel _host;

        internal SourceAddonViewModel(FileCleanerViewModel host, Guid addonId) : base(host.AddonRoot, addonId)
        {
            _host = host;
        }

        public void Remove()
        {
            _host.RemoveSourceAddon(AddonId);
        }
    }

    private bool _isValid = true;

    internal readonly ObservableCollection<StagedItem> _stagedItems = new();
    private readonly ReadOnlyObservableCollection<StagedItem> _stagedItemsReadOnly;

    private readonly ObservableCollection<Guid> _sourceAddonIds = new();
    private readonly ReadOnlyObservableCollection<Guid> _sourceAddonIdsReadOnly;
    private readonly ReadOnlyObservableCollection<SourceAddonViewModel> _sourceAddonViewModels;

    private bool _isGlobalSearch = true;

    private readonly ObservableAsPropertyHelper<long> _totalFileSize;

    public FileCleanerViewModel(AddonRoot addonRoot)
    {
        ArgumentNullException.ThrowIfNull(addonRoot);

        AddonRoot = addonRoot;

        _stagedItemsReadOnly = new(_stagedItems);
        _sourceAddonIdsReadOnly = new(_sourceAddonIds);

        _sourceAddonIds.ToObservableChangeSet()
            .Transform(id => new SourceAddonViewModel(this, id))
            .Bind(out _sourceAddonViewModels)
            .Subscribe();

        _totalFileSize = _stagedItems.ToObservableChangeSet()
            .Sum(item => item.FileSize ?? 0)
            .ToProperty(this, nameof(TotalFileSize));

        var stagedItemsNotEmpty = _stagedItems.WhenAnyValue(x => x.Count).Select(count => count > 0);
        DeleteAllCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            bool confirm = await ConfirmDeleteAllInteraction.Handle(Unit.Default);
            if (!confirm)
            {
                return;
            }

            StagedItem[] items = [.. _stagedItems];
            foreach (var item in items)
            {
                item.Delete();
            }
        }, stagedItemsNotEmpty);
        RetainAllCommand = ReactiveCommand.Create(ClearStagedItems, stagedItemsNotEmpty);

        var hasSource = this.WhenAnyValue(x => x.IsGlobalSearch)
            .CombineLatest(_sourceAddonIds.WhenAnyValue(x => x.Count))
            .Select(((bool IsGlobalSearch, int Count) args) => args.IsGlobalSearch || args.Count > 0);

        ReactiveCommand<Unit, Unit> CreateFindingCommand(Func<int> find, string findingFuncName)
        {
            return ReactiveCommand.CreateFromTask(async () =>
            {
                int? count = null;
                try
                {
                    count = find();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Exception occurred during the {nameof(FileCleanerViewModel)} finding function: {{FuncName}}", findingFuncName);
                    await ShowExceptionInteraction.Handle(ex);
                }
                if (count.HasValue)
                {
                    await ShowItemsFoundInteraction.Handle(count.Value);
                }
            }, hasSource);
        }

        FindAllCleanableCommand = CreateFindingCommand(FindAllCleanable, nameof(FindAllCleanable));
        FindRedundantVpkFilesCommand = CreateFindingCommand(FindRedundantVpkFiles, nameof(FindRedundantVpkFiles));
        FindDownloadTempFilesCommand = CreateFindingCommand(FindDownloadTempFiles, nameof(FindDownloadTempFiles));
        FindEmptyDirectoriesCommand = CreateFindingCommand(FindEmptyDirectories, nameof(FindEmptyDirectories));

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

    public ReadOnlyObservableCollection<StagedItem> StagedItems => _stagedItemsReadOnly;

    public ReadOnlyObservableCollection<Guid> SourceAddonIds => _sourceAddonIdsReadOnly;

    public ReadOnlyObservableCollection<SourceAddonViewModel> SourceAddonViewModels => _sourceAddonViewModels;

    public IEnumerable<AddonNode> ActualSourceAddons
    {
        get
        {
            var root = AddonRoot;

            if (IsGlobalSearch)
            {
                foreach (var addon in root.Nodes)
                {
                    yield return addon;
                }
                yield break;
            }

            foreach (var id in _sourceAddonIds)
            {
                if (root.TryGetNodeById(id, out var addon))
                {
                    yield return addon;
                }
            }
        }
    }

    public bool IsGlobalSearch
    {
        get => _isGlobalSearch;
        set => this.RaiseAndSetIfChanged(ref _isGlobalSearch, value);
    }

    public long TotalFileSize => _totalFileSize.Value;

    public ReactiveCommand<Unit, Unit> FindAllCleanableCommand { get; }

    public ReactiveCommand<Unit, Unit> FindRedundantVpkFilesCommand { get; }

    public ReactiveCommand<Unit, Unit> FindDownloadTempFilesCommand { get; }

    public ReactiveCommand<Unit, Unit> FindEmptyDirectoriesCommand { get; }

    public ReactiveCommand<Unit, Unit> DeleteAllCommand { get; }

    public ReactiveCommand<Unit, Unit> RetainAllCommand { get; }

    public Interaction<Exception, Unit> ShowExceptionInteraction { get; } = new();

    public Interaction<int, Unit> ShowItemsFoundInteraction { get; } = new();

    public Interaction<Unit, bool> ConfirmDeleteAllInteraction { get; } = new();

    public int FindAllCleanable()
    {
        int count = 0;
        count += FindRedundantVpkFiles();
        count += FindDownloadTempFiles();
        count += FindEmptyDirectories();
        return count;
    }

    public int FindRedundantVpkFiles()
    {
        int count = 0;
        foreach (var addon in ActualSourceAddons.SelectMany(addon => addon.GetSelfAndDescendants()))
        {
            if (addon is WorkshopVpkAddon workshopVpkAddon)
            {
                var report = workshopVpkAddon.RequestDeleteRedundantVpkFiles();
                foreach (var file in report.Files)
                {
                    if (TryAddStagedItem(file))
                    {
                        count++;
                    }
                }
            }
        }
        return count;
    }

    public int FindDownloadTempFiles()
    {
        int count = 0;
        foreach (var addon in ActualSourceAddons.SelectMany(addon => addon.GetSelfAndDescendants()))
        {
            if (addon is WorkshopVpkAddon)
            {
                var dir = addon.FullFilePath;
                if (Directory.Exists(dir))
                {
                    foreach (var file in EnumerateDownloadTempFilesInDirectory(dir))
                    {
                        if (TryAddStagedItem(file))
                        {
                            count++;
                        }
                    }
                }
            }
        }
        return count;
    }

    public int FindEmptyDirectories()
    {
        int count = 0;
        var addonRoot = AddonRoot;

        bool DirectoryMatchExistingAddonNode(string dir)
            => addonRoot.TryGetNodeByPath(addonRoot.ConvertFilePathToNodePath(dir)) is not null;

        if (IsGlobalSearch)
        {
            string rootDir = addonRoot.DirectoryPath;
            foreach (string dir in Directory.EnumerateDirectories(rootDir))
            {
                string dirName = Path.GetFileName(dir);
                if (dirName.StartsWith('.'))
                {
                    continue;
                }

                if (FileSystemUtils.IsDirectoryEmpty(dir))
                {
                    if (!DirectoryMatchExistingAddonNode(dir))
                    {
                        if (TryAddStagedItem(dir))
                        {
                            count++;
                        }
                    }
                    continue;
                }

                foreach (string subDir in FileSystemUtils.FindEmptyDirectories(dir))
                {
                    if (DirectoryMatchExistingAddonNode(subDir))
                    {
                        continue;
                    }
                    if (TryAddStagedItem(subDir))
                    {
                        count++;
                    }
                }
            }
        }
        else
        {
            foreach (var addon in ActualSourceAddons)
            {
                string dir = addon.FullFilePath;
                if (Directory.Exists(dir))
                {
                    foreach (string subDir in FileSystemUtils.FindEmptyDirectories(dir))
                    {
                        if (DirectoryMatchExistingAddonNode(subDir))
                        {
                            continue;
                        }
                        if (TryAddStagedItem(subDir))
                        {
                            count++;
                        }
                    }
                }
            }
        }

        return count;
    }

    private IEnumerable<string> EnumerateDownloadTempFilesInDirectory(string dir)
    {
        foreach (string file in Directory.EnumerateFiles(dir))
        {
            if (file.EndsWith(IDownloadService.DownloadingFileExtension) || file.EndsWith(IDownloadService.DownloadInfoFileExtension))
            {
                yield return file;
            }
        }
    }

    public void ClearStagedItems() => _stagedItems.Clear();

    public bool AddSourceAddon(Guid id)
    {
        if (_sourceAddonIds.Contains(id))
        {
            return false;
        }
        _sourceAddonIds.Add(id);
        return true;
    }

    public bool RemoveSourceAddon(Guid id)
    {
        return _sourceAddonIds.Remove(id);
    }

    private bool TryAddStagedItem(string path)
    {
        path = FileSystemUtils.NormalizePath(path);
        if (ContainsStagedItem(path))
        {
            return false;
        }
        _stagedItems.Add(new StagedItem(this, path));
        return true;
    }

    private bool ContainsStagedItem(string path)
    {
        foreach (var item in _stagedItems)
        {
            if (item.Path == path)
            {
                return true;
            }
        }
        return false;
    }
}

internal class FileCleanerViewModelDesign : FileCleanerViewModel
{
    public FileCleanerViewModelDesign() : base(DesignHelper.CreateEmptyAddonRoot())
    {
        var addonRoot = AddonRoot;

        for (int i = 0; i < 10; i++)
        {
            var addonNode = AddonNode.Create<AddonNode>(addonRoot);
            addonNode.Name = addonNode.Parent.GetUniqueNodeName($"test_{DesignHelper.GenerateRandomString(1, 15)}");
            AddSourceAddon(addonNode.Id);
        }

        for (int i = 0; i < 20; i++)
        {
            _stagedItems.Add(new StagedItem(this, "", DesignHelper.GenerateRandomString(5, 120)));
        }
    }
}