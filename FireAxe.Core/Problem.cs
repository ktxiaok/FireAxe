using System;

namespace FireAxe;

public abstract class Problem
{
    private readonly ProblemSource _problemSource;

    private bool _isValid = true;
    private bool _isSubmitted = false;

    public Problem(ProblemSource problemSource)
    {
        ArgumentNullException.ThrowIfNull(problemSource);

        _problemSource = problemSource;
    }

    public bool IsValid => _isValid;

    public bool IsSubmitted => _isSubmitted;

    public virtual bool CanAutomaticallyFix => false;

    public virtual bool ShouldFixAutomatically => false;

    public void Submit()
    {
        if (_isSubmitted)
        {
            return;
        }

        _isSubmitted = true;
        _problemSource._problem?.Remove();
        if (ShouldFixAutomatically)
        {
            if (TryAutomaticallyFix())
            {
                return;
            }
        }
        _problemSource._problem = this;
        _problemSource._problemRegister(this);
    }

    public bool TryAutomaticallyFix()
    {
        if (!_isValid)
        {
            return false;
        }

        if (OnAutomaticallyFix())
        {
            if (_problemSource._problem == this)
            {
                Remove();
            }
            return true;
        }
        else
        {
            return false;
        }
    }

    protected virtual bool OnAutomaticallyFix()
    {
        return false;
    }

    internal void Remove()
    {
        _problemSource._problem = null;
        _problemSource._problemUnregister(this);
        _isValid = false;
    }
}
