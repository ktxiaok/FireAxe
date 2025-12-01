using System;

namespace FireAxe;

public abstract class Problem : ObservableObject, IValidity
{
    public bool IsValid { get; private set => NotifyAndSetIfChanged(ref field, value); } = true;

    public virtual bool CanAutomaticallyFix => false;

    public virtual bool ShouldFixAutomatically => false;

    public bool TryAutomaticallyFix()
    {
        if (!IsValid)
        {
            return false;
        }
        if (OnAutomaticallyFix())
        {
            IsValid = false;
            return true;
        }
        return false;
    }

    public void Invalidate()
    {
        IsValid = false;
    }

    protected virtual bool OnAutomaticallyFix()
    {
        return false;
    }
}
