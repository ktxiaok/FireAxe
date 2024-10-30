using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace L4D2AddonAssistant.ViewModels
{
    public class AppSettingsViewModel : ViewModelBase, IActivatableViewModel
    {
        private AppSettings _settings;

        private CommonInteractions _commonInteractions;

        private IEnumerable<string?> _languageItemsSource;

        public AppSettingsViewModel(AppSettings settings, CommonInteractions commonInteractions)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(commonInteractions);
            _settings = settings;
            _commonInteractions = commonInteractions;

            _languageItemsSource = [null, .. LanguageManager.SupportedLanguages];

            this.WhenActivated((CompositeDisposable disposables) =>
            {
                
            });

            SelectGamePathCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                var path = await _commonInteractions.ChooseDirectory.Handle(Unit.Default);
                if (path != null)
                {
                    _settings.GamePath = path;
                }
            });
        }

        public ViewModelActivator Activator { get; } = new();

        public AppSettings Settings => _settings;

        public IEnumerable<string?> LanguageItemsSource => _languageItemsSource;

        public ReactiveCommand<Unit, Unit> SelectGamePathCommand { get; }
    }
}
