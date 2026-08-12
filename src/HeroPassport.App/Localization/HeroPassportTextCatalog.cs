using System.Globalization;
using System.Resources;

namespace HeroPassport.App.Localization;

public static class HeroPassportTextCatalog
{
    private static readonly ResourceManager Resources = new(
        "HeroPassport.App.Localization.HeroPassportTexts",
        typeof(HeroPassportTextCatalog).Assembly);

    public static string Get(string locale, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var culture = GetSupportedCulture(locale);
        return Resources.GetString(key, culture)
            ?? throw new InvalidOperationException($"Missing Hero Passport localization resource '{key}' for '{locale}'.");
    }

    public static string Format(string locale, string key, params object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var culture = GetSupportedCulture(locale);
        var template = Resources.GetString(key, culture)
            ?? throw new InvalidOperationException($"Missing Hero Passport localization resource '{key}' for '{locale}'.");
        return string.Format(culture, template, arguments);
    }

    private static CultureInfo GetSupportedCulture(string locale) => locale switch
    {
        "en-US" => CultureInfo.GetCultureInfo("en-US"),
        "ru-RU" => CultureInfo.GetCultureInfo("ru-RU"),
        _ => throw new ArgumentOutOfRangeException(nameof(locale), "Unsupported Hero Passport locale."),
    };
}
