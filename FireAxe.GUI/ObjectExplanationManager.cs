using System;
using System.Collections.Concurrent;

namespace FireAxe;

public class ObjectExplanationManager
{
    private readonly ConcurrentDictionary<Type, Func<object, object?, string?>> _dict = new();

    public static ObjectExplanationManager Default { get; } = new();

    public void Register(Type type, Func<object, object?, string?> func)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(func);

        _dict[type] = func;
    }

    public void Register<T>(Func<T, object?, string?> func)
    {
        ArgumentNullException.ThrowIfNull(func);

        Register(typeof(T), (obj, arg) => func((T)obj, arg));
    }

    public string Get(object? obj, object? arg = null)
    {
        if (obj is null)
        {
            return "null";
        }

        Type? currentType = obj.GetType();
        while (true)
        {
            if (_dict.TryGetValue(currentType, out var func))
            {
                if (func(obj, arg) is { } result)
                {
                    return result;
                }
            }
            currentType = currentType.BaseType;
            if (currentType is null)
            {
                return obj.ToString() ?? "";
            }
        }
    }
}
