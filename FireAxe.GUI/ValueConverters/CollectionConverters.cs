using System;
using System.Collections;
using Avalonia.Data.Converters;

namespace FireAxe.ValueConverters;

public static class CollectionConverters
{
    public static IValueConverter IsEmpty { get; } = new FuncValueConverter<int, bool>(count => count == 0);

    public static IValueConverter IsNotEmpty { get; } = new FuncValueConverter<int, bool>(count => count > 0);

    public static IValueConverter IsEnumerableEmpty { get; } = new FuncValueConverter<IEnumerable?, bool>(IsEnumerableEmptyFunc);

    public static IValueConverter IsEnumerableNotEmpty { get; } = new FuncValueConverter<IEnumerable?, bool>(enumerable => !IsEnumerableEmptyFunc(enumerable));

    private static bool IsEnumerableEmptyFunc(IEnumerable? enumerable)
    {
        if (enumerable is null)
        {
            return true;
        }
        foreach (var _ in enumerable)
        {
            return false;
        }
        return true;
    }
}

