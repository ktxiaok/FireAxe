using System;

namespace FireAxe;

public class ValidTaskCreator<T> where T : class, IValidity
{
    private readonly ValidRef<T> _target;

    public ValidTaskCreator(T target, TaskFactory taskFactory)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(taskFactory);

        _target = new(target);
        TaskFactory = taskFactory;
    }

    public T? Target => _target.TryGet();

    public TaskFactory TaskFactory { get; }

    public Task<bool> StartNew(Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        return TaskFactory.StartNew(() =>
        {
            var target = Target;
            if (target is null)
            {
                return true;
            }
            action(target);
            return false;
        });
    }
}