using System;

namespace FireAxe;

public class ProblemSource
{
    internal readonly Action<Problem> _problemRegister;
    internal readonly Action<Problem> _problemUnregister;

    internal Problem? _problem = null;

    public ProblemSource(Action<Problem> problemRegister, Action<Problem> problemUnregister)
    {
        ArgumentNullException.ThrowIfNull(problemRegister);
        ArgumentNullException.ThrowIfNull(problemUnregister);

        _problemRegister = problemRegister;
        _problemUnregister = problemUnregister;
    }

    public Problem? Problem => _problem;

    public void Clear()
    {
        _problem?.Remove();
    }
}