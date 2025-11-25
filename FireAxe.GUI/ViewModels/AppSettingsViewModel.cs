using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using FireAxe.Resources;
using System.IO;
using Serilog;

namespace FireAxe.ViewModels;

public class AppSettingsViewModel : ViewModelBase, IActivatableViewModel
{
    private readonly AppSettings _settings;
    private readonly MainWindowViewModel _mainWindowViewModel;

    private readonly IEnumerable<string?> _languageItemsSource;

    private bool _addonRootNotNull = false;

    public AppSettingsViewModel(AppSettings settings, MainWindowViewModel mainWindowViewModel)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(mainWindowViewModel);
        _settings = settings;
        _mainWindowViewModel = mainWindowViewModel;

        _languageItemsSource = [null, .. LanguageManager.SupportedLanguages];

        this.WhenActivated((CompositeDisposable disposables) =>
        {
            _mainWindowViewModel.WhenAnyValue(x => x.AddonRoot)
                .Subscribe(addonRoot => AddonRootNotNull = addonRoot is not null)
                .DisposeWith(disposables);
            Disposable.Create(() => AddonRootNotNull = false)
                .DisposeWith(disposables);

            _settings.WhenAnyValue(x => x.MaxRetainedBackupFileCount)
                .Subscribe(_ => this.RaisePropertyChanged(nameof(MaxRetainedBackupFileCount)));
            _settings.WhenAnyValue(x => x.FileBackupIntervalMinutes)
                .Subscribe(_ => this.RaisePropertyChanged(nameof(FileBackupIntervalMinutes)));
        });

        ShowSettingsFileCommand = ReactiveCommand.Create(() => Utils.ShowInFileExplorer(_settings.SettingsFilePath));
        SelectGamePathCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var path = await ChooseGamePathDirectoryInteraction.Handle(Unit.Default);
            if (path != null)
            {
                _settings.GamePath = path;
            }
        });
        FindGamePathCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var gamePath = GamePathUtils.TryFind();
            if (gamePath is null)
            {
                await ReportGamePathNotFoundInteraction.Handle(Unit.Default);
                return;
            }
            bool confirm = await ConfirmFoundGamePathInteraction.Handle(gamePath);
            if (!confirm)
            {
                return;
            }
            Settings.GamePath = gamePath;
        });
        OpenCustomVpkAddonConflictIgnoringFilesDirectory = ReactiveCommand.Create(() =>
        {
            var dirPath = _settings.CustomVpkAddonConflictIgnoringFilesDirectoryPath;
            try
            {
                Directory.CreateDirectory(dirPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred during creating the directory: {Path}", dirPath);
                return;
            }
            try
            {
                var readmeFilePath = Path.Join(dirPath, "readme.txt");
                if (!FileSystemUtils.Exists(readmeFilePath))
                {
                    using var sourceStream = File.OpenRead(Path.Join(AppGlobal.ExportedAssetsDirectoryName, "CustomVpkAddonConflictIgnoringFiles_ReadMe.asset"));
                    using var targetStream = File.Create(readmeFilePath);
                    sourceStream.CopyTo(targetStream);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred during creating CustomVpkAddonConflictIgnoringFiles readme file.");
            }
            Utils.ShowInFileExplorer(dirPath, openDir: true);
        });
        OpenBackupDirectoryCommand = ReactiveCommand.Create(() =>
        {
            if (_mainWindowViewModel.AddonRoot?.BackupDirectoryPath is { } path)
            {
                Utils.ShowInFileExplorer(path, openDir: true);
            }
        }, this.WhenAnyValue(x => x.AddonRootNotNull));
    }

    public ViewModelActivator Activator { get; } = new();

    public AppSettings Settings => _settings;

    public IEnumerable<string?> LanguageItemsSource => _languageItemsSource;

    public string MaxRetainedBackupFileCount
    {
        get => _settings.MaxRetainedBackupFileCount.ToString();
        set
        {
            if (!int.TryParse(value, out int count))
            {
                throw new ArgumentException(Texts.ValueMustBeInteger);
            }

            _settings.MaxRetainedBackupFileCount = count;
        }
    }

    public string FileBackupIntervalMinutes
    {
        get => _settings.FileBackupIntervalMinutes.ToString();
        set
        {
            if (!int.TryParse(value, out int minutes))
            {
                throw new ArgumentException(Texts.ValueMustBeInteger);
            }

            _settings.FileBackupIntervalMinutes = minutes;
        }
    }

    public ReactiveCommand<Unit, Unit> ShowSettingsFileCommand { get; }

    public ReactiveCommand<Unit, Unit> SelectGamePathCommand { get; }

    public ReactiveCommand<Unit, Unit> FindGamePathCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenCustomVpkAddonConflictIgnoringFilesDirectory { get; }

    public ReactiveCommand<Unit, Unit> OpenBackupDirectoryCommand { get; }

    public Interaction<Unit, string?> ChooseGamePathDirectoryInteraction { get; } = new();

    public Interaction<string, bool> ConfirmFoundGamePathInteraction { get; } = new();

    public Interaction<Unit, Unit> ReportGamePathNotFoundInteraction { get; } = new();

    private bool AddonRootNotNull
    {
        get => _addonRootNotNull;
        set => this.RaiseAndSetIfChanged(ref _addonRootNotNull, value);
    }
}
