using System;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DynamicData;
using DynamicData.Binding;
using FireAxe.ViewModels;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace FireAxe.Views;

public partial class OperationsProgressView : ReactiveUserControl<OperationsProgressViewModel>
{
    public static readonly StyledProperty<OperationsProgressViewMessageTemplate> MessageTemplateProperty =
        AvaloniaProperty.Register<OperationsProgressView, OperationsProgressViewMessageTemplate>(nameof(MessageTemplate), defaultValue: OperationsProgressViewMessageTemplate.Default);

    public static readonly DirectProperty<OperationsProgressView, string?> MessageProperty =
        AvaloniaProperty.RegisterDirect<OperationsProgressView, string?>(nameof(Message), t => t.Message, (t, v) => t.Message = v);

    public static readonly DirectProperty<OperationsProgressView, ReadOnlyObservableCollection<string>> DetailedMessagesProperty =
        AvaloniaProperty.RegisterDirect<OperationsProgressView, ReadOnlyObservableCollection<string>>(
            nameof(DetailedMessages), t => t.DetailedMessages, (t, v) => t.DetailedMessages = v, unsetValue: ReadOnlyObservableCollection<string>.Empty);

    private string? _message = null;

    private ReadOnlyObservableCollection<string> _detailedMessages = ReadOnlyObservableCollection<string>.Empty;

    public OperationsProgressView()
    {
        InitializeComponent();

        this.WhenActivated((CompositeDisposable disposables) =>
        {

        });

        this.RegisterViewModelConnection(ConnectViewModel);
    }

    public OperationsProgressViewMessageTemplate MessageTemplate
    {
        get => GetValue(MessageTemplateProperty);
        set => SetValue(MessageTemplateProperty, value);
    }

    public string? Message
    {
        get => _message;
        private set => SetAndRaise(MessageProperty, ref _message, value);
    }

    public ReadOnlyObservableCollection<string> DetailedMessages
    {
        get => _detailedMessages;
        private set => SetAndRaise(DetailedMessagesProperty, ref _detailedMessages, value);
    }

    private void ConnectViewModel(OperationsProgressViewModel viewModel, CompositeDisposable disposables)
    {
        viewModel.WhenAnyValue(x => x.CompletedOperationCount, x => x.TotalOperationCount, x => x.FailedOperationCount, x => x.IsDone)
            .CombineLatest(this.WhenAnyValue(x => x.MessageTemplate))
            .Select(_ =>
            {
                if (viewModel.IsDone)
                {
                    if (viewModel.HasFailedOperation)
                    {
                        return MessageTemplate.DoneWithFailure.FormatNoThrow(viewModel.SuccessfulOperationCount, viewModel.FailedOperationCount);
                    }
                    else
                    {
                        return MessageTemplate.Done.FormatNoThrow(viewModel.TotalOperationCount);
                    }
                }
                else
                {
                    if (viewModel.HasFailedOperation)
                    {
                        return MessageTemplate.ProgressWithFailure.FormatNoThrow(viewModel.CompletedOperationCount, viewModel.TotalOperationCount, viewModel.FailedOperationCount);
                    }
                    else
                    {
                        return MessageTemplate.Progress.FormatNoThrow(viewModel.CompletedOperationCount, viewModel.TotalOperationCount);
                    }
                }
            })
            .BindTo(this, x => x.Message)
            .DisposeWith(disposables);

        viewModel.CompletedOperations.ToObservableChangeSet()
            .Transform(op =>
            {
                if (op.Failure is { } failure)
                {
                    return MessageTemplate.OperationFailed.FormatNoThrow(op.Operation, failure);
                }
                else
                {
                    return MessageTemplate.OperationSucceeded.FormatNoThrow(op.Operation);
                }
            })
            .Bind(out ReadOnlyObservableCollection<string> detailedMessages)
            .Subscribe()
            .DisposeWith(disposables);
        DetailedMessages = detailedMessages;
        Disposable.Create(() => DetailedMessages = ReadOnlyObservableCollection<string>.Empty).DisposeWith(disposables);
    }
}