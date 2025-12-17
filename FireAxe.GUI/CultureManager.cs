using Avalonia;
using Semi.Avalonia;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;

namespace FireAxe;

public sealed class CultureManager
{
    private static readonly FrozenSet<string> s_supportedCultureStrings = ["en", "zh-Hans"];

    private static readonly Lazy<CultureManager> s_instance = new(() => new CultureManager());

    private readonly CultureInfo _defaultCulture = CultureInfo.CurrentUICulture;

    private CultureManager()
    {

    }

    public static IReadOnlySet<string> SupportedCultureStrings => s_supportedCultureStrings;

    public static CultureManager Instance => s_instance.Value;

    public CultureInfo DefaultCulture => _defaultCulture;

    public void SetCurrentCulture(CultureInfo? culture)
    {
        culture ??= DefaultCulture;

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        if (Application.Current is { } app) 
        {
            SemiTheme.OverrideLocaleResources(app, culture);
        }
    }

    public void SetCurrentCulture(string? cultureStr) => SetCurrentCulture(cultureStr is null ? null : new CultureInfo(cultureStr));
}
