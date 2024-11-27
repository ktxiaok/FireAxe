using Avalonia.Data;
using Avalonia.Data.Converters;
using L4D2AddonAssistant.Resources;
using System;
using System.Globalization;
using System.Linq;
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

            var targetNames = Enum.GetNames(type).Where((name) => Enum.Parse(type, name).Equals(value));
            string className = type.Name;
            string? resultText = null;
            foreach (string name in targetNames)
            {
                string key = $"Enum_{className}_{name}";
                resultText = Texts.ResourceManager.GetString(key, Texts.Culture);
                if (resultText != null)
                {
                    break;
                }
            }
            resultText ??= $"[missing text: Enum_{className}_{string.Join('|', targetNames)}]";

            return resultText;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return BindingOperations.DoNothing;
        }
    }
}
