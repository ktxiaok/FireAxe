using System;
using Avalonia.Data.Converters;
using FireAxe.Resources;

namespace FireAxe.ValueConverters;

public static class StringConverters
{
    public static IValueConverter ShowEmpty { get; } = new FuncValueConverter<string?, string>(value =>
    {
        if (string.IsNullOrEmpty(value))
        {
            return Texts.Empty;
        }
        return value;
    });
}