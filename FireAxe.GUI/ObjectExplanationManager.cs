using System;
using System.Collections.Generic;

namespace FireAxe
{
    public class ObjectExplanationManager
    {
        private static ObjectExplanationManager s_default = new();

        private Dictionary<Type, Func<object, object?, string>> _dict = new();

        public static ObjectExplanationManager Default => s_default;

        public void Register(Type type, Func<object, object?, string> func)
        {
            ArgumentNullException.ThrowIfNull(type);
            ArgumentNullException.ThrowIfNull(func);

            _dict[type] = func;
        }

        public void Register<T>(Func<T, object?, string> func)
        {
            ArgumentNullException.ThrowIfNull(func);

            Register(typeof(T), (obj, arg) => func((T)obj, arg));
        }

        public string? TryGet(object obj, object? arg = null)
        {
            ArgumentNullException.ThrowIfNull(obj);

            Type? currentType = obj.GetType();
            while (true)
            {
                if (_dict.TryGetValue(currentType, out var func))
                {
                    return func(obj, arg);
                }
                currentType = currentType.BaseType;
                if (currentType == null)
                {
                    return null;
                }
            }
        }
    }
}
