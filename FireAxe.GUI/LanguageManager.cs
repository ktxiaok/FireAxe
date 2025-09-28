using FireAxe.Resources;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;

namespace FireAxe;

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
            var culture = value == null ? null : new CultureInfo(value);
            Texts.Culture = culture;
            if (culture != null)
            {
                CultureInfo.CurrentUICulture = culture;
            }
        }
    }
}
