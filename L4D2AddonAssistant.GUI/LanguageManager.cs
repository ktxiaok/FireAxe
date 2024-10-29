using L4D2AddonAssistant.Resources;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;

namespace L4D2AddonAssistant
{
    public static class LanguageManager
    {
        private static ImmutableHashSet<string> s_supportedLanguages = ["en-US", "zh-Hans"];

        private static string? s_currentLanguage = null;

        public static IEnumerable<string> SupportedLanguages => s_supportedLanguages;

        public static string? CurrentLanguage
        {
            get => s_currentLanguage;
            set
            {
                s_currentLanguage = value;
                Texts.Culture = value == null ? null : new CultureInfo(value);
            }
        }
    }
}
