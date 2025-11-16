using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FireAxe.Resources;

namespace FireAxe;

public class TaskOperationsProgressNotifier
{
    private readonly (Task, string)[] _operations;

    public TaskOperationsProgressNotifier(IEnumerable<(Task, string)> operations, bool isCancelable, IOperationsProgressObserver observer)
        : this(operations, isCancelable, observer, TaskScheduler.FromCurrentSynchronizationContext())
    {

    }

    public TaskOperationsProgressNotifier(IEnumerable<(Task, string)> operations, bool isCancelable, IOperationsProgressObserver observer, TaskScheduler notificationScheduler)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(notificationScheduler);
        _operations = [.. operations];
        if (_operations.Length == 0)
        {
            throw new ArgumentException($"{nameof(operations)} cannot be empty.");
        }
        IsCancelable = isCancelable;
        Observer = observer;
        NotificationScheduler = notificationScheduler;

        Observe();
    }

    public IReadOnlyCollection<(Task, string)> Operations => _operations;

    public bool IsCancelable { get; }

    public IOperationsProgressObserver Observer { get; }

    public TaskScheduler NotificationScheduler { get; }

    private void Observe()
    {
        Observer.InitOperations(_operations.Length, IsCancelable);

        foreach ((Task task, string operation) in _operations)
        {
            task.ContinueWith(task =>
            {
                if (task.IsCanceled)
                {
                    Observer.NotifyOperationFailed(operation, Texts.OperationCanceled);
                }
                else if (task.Exception is { } ex)
                {
                    Observer.NotifyOperationFailed(operation, ObjectExplanationManager.Default.TryGet(ex) ?? ex.ToString());
                }
                else
                {
                    Observer.NotifyOperationSucceeded(operation);
                }
            }, NotificationScheduler);
        }
    }
}