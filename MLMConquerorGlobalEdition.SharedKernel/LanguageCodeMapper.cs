namespace MLMConquerorGlobalEdition.SharedKernel;

/// <summary>
/// Maps the 9 short language codes used in the app's profile preference
/// (en, es, pt, fr, de, zh, it, kr, ge) to proper .NET CultureInfo names.
/// Nick chose flag-country-style codes ("kr" instead of strict ISO "ko",
/// "ge" instead of "ka"); this is the single normalization point so the
/// rest of the system can speak standard culture identifiers.
/// </summary>
public static class LanguageCodeMapper
{
    public const string DefaultAppCode      = "en";
    public const string DefaultCultureName  = "en-US";

    private static readonly Dictionary<string, string> AppToCulture =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = "en-US",
            ["es"] = "es-ES",
            ["pt"] = "pt-BR",
            ["fr"] = "fr-FR",
            ["de"] = "de-DE",
            ["zh"] = "zh-CN",
            ["it"] = "it-IT",
            ["kr"] = "ko-KR",
            ["ge"] = "ka-GE",
        };

    private static readonly Dictionary<string, string> CultureToApp =
        AppToCulture.ToDictionary(
            kvp => kvp.Value,
            kvp => kvp.Key,
            StringComparer.OrdinalIgnoreCase);

    /// <summary>App-level codes as they appear on MemberProfile.DefaultLanguage.</summary>
    public static IReadOnlyList<string> SupportedAppCodes { get; } =
        AppToCulture.Keys.ToArray();

    /// <summary>CultureInfo names suitable for RequestLocalizationOptions.AddSupportedCultures.</summary>
    public static IReadOnlyList<string> SupportedCultureNames { get; } =
        AppToCulture.Values.ToArray();

    /// <summary>
    /// Resolves an app code to a CultureInfo name. Unknown values fall back
    /// to <see cref="DefaultCultureName"/> so callers never crash on bad input.
    /// </summary>
    public static string ToCultureName(string? appCode)
    {
        if (string.IsNullOrWhiteSpace(appCode)) return DefaultCultureName;
        return AppToCulture.TryGetValue(appCode, out var culture)
            ? culture
            : DefaultCultureName;
    }

    /// <summary>
    /// Resolves a CultureInfo name back to the short app code. Accepts both
    /// fully qualified names ("en-US") and neutral names ("en"); unknown
    /// values fall back to <see cref="DefaultAppCode"/>.
    /// </summary>
    public static string ToAppCode(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName)) return DefaultAppCode;

        if (CultureToApp.TryGetValue(cultureName, out var directHit))
            return directHit;

        // Treat input as either a neutral culture ("ko") or a full one ("ko-KR")
        // and find the supported culture whose neutral prefix matches.
        var neutral = cultureName.Contains('-')
            ? cultureName[..cultureName.IndexOf('-')]
            : cultureName;

        var match = CultureToApp.FirstOrDefault(kvp =>
            kvp.Key.StartsWith(neutral + "-", StringComparison.OrdinalIgnoreCase));
        if (match.Value is not null) return match.Value;

        return DefaultAppCode;
    }

    /// <summary>True if the given app code is one of the 9 supported codes.</summary>
    public static bool IsSupportedAppCode(string? appCode) =>
        !string.IsNullOrWhiteSpace(appCode) && AppToCulture.ContainsKey(appCode);
}
