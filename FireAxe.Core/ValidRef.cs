using System;

namespace FireAxe;

public class ValidRef<T> where T : class, IValidity
{
    private readonly WeakReference<T?> _weakRef = new(null);

    public ValidRef(T? target)
    {
        Set(target);
    }

    public void Set(T? target)
    {
        if (target is not null)
        {
            if (!target.IsValid)
            {
                target = null;
            }
        }
        _weakRef.SetTarget(target);
    }

    public T? TryGet()
    {
        if (_weakRef.TryGetTarget(out var target))
        {
            if (!target.IsValid)
            {
                _weakRef.SetTarget(null);
                return null;
            }
            return target;
        }
        return null;
    }
}