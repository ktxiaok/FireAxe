using Avalonia.Controls;
using Avalonia.ReactiveUI;
using L4D2AddonAssistant.ViewModels;
using L4D2AddonAssistant.Resources;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using System.Reactive;

namespace L4D2AddonAssistant.Views
{
    public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
    {
        private CompositeDisposable? _viewModelConnection = null;

        public MainWindow()
        {
            InitializeComponent();

            this.WhenActivated((CompositeDisposable disposables) =>
            {
                this.WhenAnyValue(x => x.ViewModel)
                .Subscribe((viewModel) =>
                {
                    ConnectViewModel(viewModel);
                })
                .DisposeWith(disposables);

                Disposable.Create(() =>
                {
                    DisconnectViewModel();
                })
                .DisposeWith(disposables);
            });
        }

        private void ConnectViewModel(MainWindowViewModel? viewModel)
        {
            DisconnectViewModel();
            if (viewModel == null)
            {
                return;
            }
            var disposables = new CompositeDisposable();
            _viewModelConnection = disposables;

            viewModel.ChooseDirectoryInteraction.RegisterHandler(async (context) =>
            {
                context.SetOutput(await CommonMessageBoxes.ChooseDirectory(this));
            }).DisposeWith(disposables);

            viewModel.ShowImportSuccessInteraction.RegisterHandler(async (context) =>
            {
                await CommonMessageBoxes.ShowInfo(this, Texts.ImportSuccessMessage, Texts.Success);
                context.SetOutput(Unit.Default);
            }).DisposeWith(disposables);
            viewModel.ShowImportErrorInteraction.RegisterHandler(async (context) =>
            {
                await CommonMessageBoxes.ShowException(this, context.Input, Texts.ImportErrorMessage);
                context.SetOutput(Unit.Default);
            }).DisposeWith(disposables);

            viewModel.ShowPushSuccessInteraction.RegisterHandler(async (context) =>
            {
                await CommonMessageBoxes.ShowInfo(this, Texts.PushSuccessMessage, Texts.Success);
                context.SetOutput(Unit.Default);
            }).DisposeWith(disposables);
            viewModel.ShowPushErrorInteraction.RegisterHandler(async (context) =>
            {
                await CommonMessageBoxes.ShowException(this, context.Input, Texts.PushErrorMessage);
                context.SetOutput(Unit.Default);
            }).DisposeWith(disposables);
            viewModel.ShowInvalidGamePathInteraction.RegisterHandler(async (context) =>
            {
                await CommonMessageBoxes.ShowInfo(this, string.Format(Texts.InvalidGamePathMessage, context.Input), Texts.Error);
                context.SetOutput(Unit.Default);
            }).DisposeWith(disposables);
        }

        private void DisconnectViewModel()
        {
            if (_viewModelConnection != null)
            {
                _viewModelConnection.Dispose();
                _viewModelConnection = null;
            }
        }
    }
}