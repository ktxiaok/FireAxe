using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace FireAxe.ViewModels;

public class AppSettingsViewModel : ViewModelBase, IActivatableViewModel
{
    private AppSettings _settings;

    private IEnumerable<string?> _languageItemsSource;

    public AppSettingsViewModel(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;

        _languageItemsSource = [null, .. LanguageManager.SupportedLanguages];

        this.WhenActivated((CompositeDisposable disposables) =>
        {
            
        });

        SelectGamePathCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var path = await ChooseDirectoryInteraction.Handle(Unit.Default);
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
    }

    public ViewModelActivator Activator { get; } = new();

    public AppSettings Settings => _settings;

    public IEnumerable<string?> LanguageItemsSource => _languageItemsSource;

    public ReactiveCommand<Unit, Unit> SelectGamePathCommand { get; }

    public ReactiveCommand<Unit, Unit> FindGamePathCommand { get; }

    public Interaction<Unit, string?> ChooseDirectoryInteraction { get; } = new();

    public Interaction<string, bool> ConfirmFoundGamePathInteraction { get; } = new();

    public Interaction<Unit, Unit> ReportGamePathNotFoundInteraction { get; } = new();
}
