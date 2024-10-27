using Avalonia.Data;
using Avalonia.Data.Converters;
using L4D2AddonAssistant.Resources;
using System;
using System.Globalization;
using System.Reflection;

namespace L4D2AddonAssistant.ValueConverters
{
    public class EnumDescriptionConverter : IValueConverter
    {
        private static EnumDescriptionConverter s_instance = new();

        public static EnumDescriptionConverter Instance => s_instance;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return null;
            }
            var type = value.GetType();
            if (!type.IsEnum)
            {
                return null;
            }
            var name = Enum.GetName(type, value);
            if (name == null)
            {
                return null;
            }
            var className = type.Name;
            var key = "Enum_" + className + "_" + name;
            var propertyInfo = typeof(Texts).GetProperty(key, BindingFlags.Static | BindingFlags.Public);
            if (propertyInfo == null)
            {
                return $"[property not found: {key}]";
            }
            return propertyInfo.GetValue(null);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return BindingOperations.DoNothing;
        }
    }
}
