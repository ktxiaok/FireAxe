using Avalonia.Data;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace FireAxe.ValueConverters
{
    public class ObjectExplanationConverter : IValueConverter
    {
        private static ObjectExplanationConverter s_default = new();

        public static ObjectExplanationConverter Default => s_default;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return null;
            }
            var result = ObjectExplanationManager.Default.TryGet(value);
            if (result != null)
            {
                return result;
            }
            return value;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return BindingOperations.DoNothing;
        }
    }
}
