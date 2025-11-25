using System;
using Avalonia.Data.Converters;

namespace FireAxe.ValueConverters;

public static class DateTimeConverters
{
    public static IValueConverter HourMinuteSecond { get; } = new FuncValueConverter<DateTime?, string?>(dateTime =>
    {
        return dateTime is null ? null : $"{dateTime.Value.Hour:D2}:{dateTime.Value.Minute:D2}:{dateTime.Value.Second:D2}";
    });
}