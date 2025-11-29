using ReactiveUI;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;

namespace FireAxe.ViewModels;

public class FileNameFixerViewModel : ViewModelBase, IActivatableViewModel, IValidity
{
    public class Item : ViewModelBase
    {
        private readonly FileNameFixerViewModel _host;

        internal Item(FileNameFixerViewModel host, string filePath, string newFilePath)
        {
            _host = host;
            FilePath = filePath;
            NewFilePath = newFilePath;

            this.WhenAnyValue(x => x.NewFilePath)
                .Subscribe(_ => this.RaisePropertyChanged(nameof(RelativeNewFilePath)));
            this.WhenAnyValue(x => x.IsFixed)
                .Subscribe(isFixed =>
                {
                    if (isFixed)
                    {
                        _host.NotifyItemFixed(this);
                    }
                });
        }

        public string FilePath { get; }

        public string NewFilePath { get; private set => this.RaiseAndSetIfChanged(ref field, value); } = "";

        public string RelativeFilePath => Path.GetRelativePath(_host.AddonRoot.DirectoryPath, FilePath);

        public string RelativeNewFilePath => Path.GetRelativePath(_host.AddonRoot.DirectoryPath, NewFilePath);

        public bool IsFixed { get; private set => this.RaiseAndSetIfChanged(ref field, value); } = false;

        public Exception? FixException { get; private set => this.RaiseAndSetIfChanged(ref field, value); } = null;

        public void Fix()
        {
            if (IsFixed)
            {
                return;
            }

            FixException = null;
            try
            {
                NewFilePath = FileSystemUtils.GetUniquePath(NewFilePath);
                FileSystemUtils.Move(FilePath, NewFilePath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred during fixing the file name: {FilePath}", FilePath);
                FixException = ex;
                return;
            }

            IsFixed = true;
        }
    }

    private readonly Item[] _items;

    public FileNameFixerViewModel(AddonRoot addonRoot)
    {
        ArgumentNullException.ThrowIfNull(addonRoot);

        AddonRoot = addonRoot;
        _items = [.. FindItems()];

        UpdateIsAllFixed();

        FixAllCommand = ReactiveCommand.Create(() =>
        {
            foreach (var item in _items)
            {
                item.Fix();
            }
        }, this.WhenAnyValue(x => x.IsAllFixed).Select(allFixed => !allFixed));

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

    public AddonRoot AddonRoot { get; }

    public bool IsValid { get; private set => this.RaiseAndSetIfChanged(ref field, value); } = true;

    public IReadOnlyList<Item> Items => _items;

    public bool IsAllFixed { get; private set => this.RaiseAndSetIfChanged(ref field, value); } = true;

    public ReactiveCommand<Unit, Unit> FixAllCommand { get; }

    private IEnumerable<Item> FindItems()
    {
        foreach (var path in AddonRoot.EnumerateUserFileSystemEntries())
        {
            var nameNoExt = Path.GetFileNameWithoutExtension(path);
            var newNameNoExt = AddonNode.SanitizeName(nameNoExt);
            if (newNameNoExt != nameNoExt)
            {
                var dirPath = Path.GetDirectoryName(path) ?? throw new IOException("Failed to get the directory path.");
                var ext = Path.GetExtension(path);
                var newPath = Path.Join(dirPath, newNameNoExt + ext);
                yield return new Item(this, path, newPath);
            }
        }
    }

    private void NotifyItemFixed(Item item)
    {
        UpdateIsAllFixed();
    }

    private void UpdateIsAllFixed()
    {
        bool allFixed = true;
        foreach (var item in _items)
        {
            if (!item.IsFixed)
            {
                allFixed = false;
                break;
            }
        }
        IsAllFixed = allFixed;
    }
}
