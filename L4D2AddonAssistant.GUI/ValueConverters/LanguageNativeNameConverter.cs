using Avalonia.Data;
using Avalonia.Data.Converters;
using L4D2AddonAssistant.Resources;
using Serilog;
using System;
using System.Globalization;

namespace L4D2AddonAssistant.ValueConverters
{
    public class LanguageNativeNameConverter : IValueConverter
    {
        private static LanguageNativeNameConverter s_instance = new();

        public static LanguageNativeNameConverter Instance => s_instance;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return Texts.Default;
            }
            var strValue = value as string;
            if (strValue == null)
            {
                return value;
            }
            try
            {
                var languageCulture = new CultureInfo(strValue);
                return languageCulture.NativeName;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred during LanguageNativeNameConverter.Convert.");
            }
            return value;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return BindingOperations.DoNothing;
        }
    }
}
