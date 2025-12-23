using Avalonia.Data;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace FireAxe.ValueConverters;

public class ObjectExplanationConverter : IValueConverter
{
    public static ObjectExplanationConverter Default { get; } = new();

    public ObjectExplanationManager ExplanationManager { get; init; } = ObjectExplanationManager.Default;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return ExplanationManager.Get(value);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return BindingOperations.DoNothing;
    }
}
