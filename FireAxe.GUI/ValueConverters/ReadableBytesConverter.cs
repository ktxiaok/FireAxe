using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace FireAxe.ValueConverters;

public class ReadableBytesConverter : IValueConverter
{
    private int _digits = 1;

    public int Digits
    {
        get => _digits;
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            _digits = value;
        }
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var convertible = value as IConvertible;
        if (convertible is null)
        {
            return null;
        }

        var bytes = convertible.ToDouble(null);
        Utils.GetReadableBytes(bytes, out var num, out var unit);
        return num.ToString($"F{Digits}") + unit;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return BindingOperations.DoNothing;
    }
}