using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;

namespace FireAxe.ViewModels;

public class OperationsProgressViewModel : ViewModelBase, IActivatableViewModel, IOperationsProgressObserver
{
    public class CompletedOperationInfo
    {
        internal CompletedOperationInfo(string operation, string? failure = null)
        {
            Operation = operation;
            Failure = failure;
        }

        public string Operation { get; }

        public string? Failure { get; }
    }

    private int _totalOperationCount = 0;
    private bool _isCancelable = false;
    private int _completedOperationCount = 0;
    private int _successfulOperationCount = 0;
    private int _failedOperationCount = 0;

    private readonly ObservableCollection<CompletedOperationInfo> _completedOperations = new();
    private readonly ReadOnlyObservableCollection<CompletedOperationInfo> _completedOperationsReadOnly;

    private readonly ObservableAsPropertyHelper<bool> _isDone;

    private bool _hasFailedOperation = false;

    public OperationsProgressViewModel()
    {
        _completedOperationsReadOnly = new(_completedOperations);

        _isDone = this.WhenAnyValue(x => x.CompletedOperationCount, x => x.TotalOperationCount)
            .Select(_ => TotalOperationCount > 0 && CompletedOperationCount == TotalOperationCount)
            .ToProperty(this, nameof(IsDone));

        CloseCommand = ReactiveCommand.Create(() => CloseRequested?.Invoke(), this.WhenAnyValue(x => x.IsDone));
        
        this.WhenActivated((CompositeDisposable disposables) =>
        {

        });
    }

    public ViewModelActivator Activator { get; } = new();

    public int TotalOperationCount
    {
        get => _totalOperationCount;
        private set => this.RaiseAndSetIfChanged(ref _totalOperationCount, value);
    }

    public bool IsCancelable
    {
        get => _isCancelable;
        private set => this.RaiseAndSetIfChanged(ref _isCancelable, value);
    }

    public int CompletedOperationCount
    {
        get => _completedOperationCount;
        private set => this.RaiseAndSetIfChanged(ref _completedOperationCount, value);
    }

    public int SuccessfulOperationCount
    {
        get => _successfulOperationCount;
        private set => this.RaiseAndSetIfChanged(ref _successfulOperationCount, value);
    }

    public int FailedOperationCount
    {
        get => _failedOperationCount;
        private set => this.RaiseAndSetIfChanged(ref _failedOperationCount, value);
    }

    public ReadOnlyObservableCollection<CompletedOperationInfo> CompletedOperations => _completedOperationsReadOnly;

    public bool IsDone => _isDone.Value;

    public bool HasFailedOperation
    {
        get => _hasFailedOperation;
        private set => this.RaiseAndSetIfChanged(ref _hasFailedOperation, value);
    }

    public ReactiveCommand<Unit, Unit> CloseCommand { get; }

    public event Action<string>? OperationSucceeded = null;

    public event Action<string, string>? OperationFailed = null;

    public event Action? CloseRequested = null;

    public void InitOperations(int totalOperationCount, bool isCancelable)
    {
        if (totalOperationCount <= 0)
        {
            throw new ArgumentException($"{nameof(totalOperationCount)} should be bigger than 0.");
        }
        if (TotalOperationCount != 0)
        {
            throw new InvalidOperationException("Operations had been initialized.");
        }

        TotalOperationCount = totalOperationCount;
        IsCancelable = isCancelable;
    }

    public void NotifyOperationSucceeded(string operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ThrowIfOperationsNotInited();
        ThrowIfDone();

        SuccessfulOperationCount++;
        CompletedOperationCount++;

        _completedOperations.Add(new CompletedOperationInfo(operation));

        OperationSucceeded?.Invoke(operation);
    }

    public void NotifyOperationFailed(string operation, string failure)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(failure);
        ThrowIfOperationsNotInited();
        ThrowIfDone();

        FailedOperationCount++;
        HasFailedOperation = true;
        CompletedOperationCount++;

        _completedOperations.Add(new CompletedOperationInfo(operation, failure));

        OperationFailed?.Invoke(operation, failure);
    }

    private void ThrowIfOperationsNotInited()
    {
        if (TotalOperationCount == 0)
        {
            throw new InvalidOperationException("Operations are not initialized.");
        }
    }

    private void ThrowIfDone()
    {
        if (IsDone)
        {
            throw new InvalidOperationException("Operations are done.");
        }
    }
}