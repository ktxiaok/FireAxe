using FireAxe.Resources;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;

namespace FireAxe;

public sealed class LanguageManager
{
    private static ImmutableHashSet<string> s_supportedLanguages = ["en", "zh-Hans"];

    private static readonly Lazy<LanguageManager> s_instance = new(() => new LanguageManager());

    private readonly CultureInfo _defaultCulture = CultureInfo.CurrentUICulture; 

    private LanguageManager()
    {

    }

    public static IReadOnlySet<string> SupportedLanguages => s_supportedLanguages;

    public static LanguageManager Instance => s_instance.Value;

    public CultureInfo DefaultCulture => _defaultCulture;

    public void SetCurrentLanguage(CultureInfo? culture)
    {
        culture ??= DefaultCulture;

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    public void SetCurrentLanguage(string? language) => SetCurrentLanguage(language is null ? null : new CultureInfo(language));
}
