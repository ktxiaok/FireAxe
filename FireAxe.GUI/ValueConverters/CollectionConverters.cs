using System;
using Avalonia.Data.Converters;

namespace FireAxe.ValueConverters;

public static class CollectionConverters
{
    public static IValueConverter IsEmpty { get; } = new FuncValueConverter<int, bool>(count => count == 0);

    public static IValueConverter IsNotEmpty { get; } = new FuncValueConverter<int, bool>(count => count > 0);
}

