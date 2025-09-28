using Avalonia.Controls;
using Avalonia.ReactiveUI;
using FireAxe.ViewModels;
using FireAxe.Resources;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using System.Reactive;
using MsBox.Avalonia;
using MsBox.Avalonia.Models;

namespace FireAxe.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    private CompositeDisposable? _viewModelConnection = null;

    private WindowReference<CheckingUpdateWindow>? _checkingUpdateWindow = null;

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

            ViewModel?.InitIfNot();
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

        var showCheckingUpdateWindow = () =>
        {
            if (_checkingUpdateWindow == null || _checkingUpdateWindow.Get() == null)
            {
                var window = new CheckingUpdateWindow();
                window.Closed += (sender, args) =>
                {
                    ViewModel?.CancelCheckUpdate();
                };
                _checkingUpdateWindow = new(window);
            }
            var checkingUpdateWindow = _checkingUpdateWindow.Get()!;
            checkingUpdateWindow.Show();
            checkingUpdateWindow.Activate();
        };
        var closeCheckingUpdateWindow = () =>
        {
            _checkingUpdateWindow?.Get()?.Close();
        };
        viewModel.ShowCheckingUpdateWindow += showCheckingUpdateWindow;
        viewModel.CloseCheckingUpdateWindow += closeCheckingUpdateWindow;
        Disposable.Create(() =>
        {
            viewModel.ShowCheckingUpdateWindow -= showCheckingUpdateWindow;
            viewModel.CloseCheckingUpdateWindow -= closeCheckingUpdateWindow;
        })
        .DisposeWith(disposables);

        viewModel.ShowCheckUpdateFailedInteraction.RegisterHandler(async (context) =>
        {
            await CommonMessageBoxes.ShowInfo(this, Texts.FailedToGetLatestVersion, Texts.Error);
            context.SetOutput(Unit.Default);
        })
        .DisposeWith(disposables);
        viewModel.ShowUpdateRequestInteraction.RegisterHandler(async (context) =>
        {
            var version = context.Input;
            var reply = UpdateRequestReply.None;
            var textIgnore = Texts.Ignore;
            var textGoToDownload = Texts.GoToDownload;
            var result = await MessageBoxManager.GetMessageBoxCustom(new MsBox.Avalonia.Dto.MessageBoxCustomParams()
            {
                ButtonDefinitions =
                [
                    new ButtonDefinition{ Name = textGoToDownload },
                    new ButtonDefinition{ Name = textIgnore }
                ],
                ContentTitle = Texts.Update,
                ContentMessage = string.Format(Texts.NewVersionAvailable, version),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }).ShowWindowDialogAsync(this);
            if (result == textGoToDownload)
            {
                reply = UpdateRequestReply.GoToDownload;
            }
            else if (result == textIgnore)
            {
                reply = UpdateRequestReply.Ignore;
            }
            context.SetOutput(reply);
        })
        .DisposeWith(disposables);
        viewModel.ShowCurrentVersionLatestInteraction.RegisterHandler(async (context) =>
        {
            await CommonMessageBoxes.ShowInfo(this, Texts.CurrentVersionLatestMessage, Texts.Update);
            context.SetOutput(Unit.Default);
        })
        .DisposeWith(disposables);

        viewModel.ShowDontOpenGameAddonsDirectoryInteraction.RegisterHandler(async (context) =>
        {
            await CommonMessageBoxes.ShowInfo(this, Texts.DontOpenGameAddonsDirectory, Texts.Error);
            context.SetOutput(Unit.Default);
        })
        .DisposeWith(disposables);

        viewModel.ShowAutoDetectWorkshopItemLinkDialogInteraction.RegisterHandler(async (context) =>
        {
            var link = context.Input;
            bool confirm = await CommonMessageBoxes.Confirm(this, string.Format(Texts.AutoDetectWorkshopItemLinkDialogMessage, link), "");
            context.SetOutput(confirm);
        })
        .DisposeWith(disposables);

        viewModel.ConfirmOpenHigherVersionFileInteraction.RegisterHandler(async (context) =>
        {
            var path = context.Input;
            bool confirm = await CommonMessageBoxes.Confirm(this, $"{Texts.ConfirmOpenHigherVersionFileMessage}\n({path})", Texts.Warning);
            context.SetOutput(confirm);
        })
        .DisposeWith(disposables);
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