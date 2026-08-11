using System.Globalization;
using System.Resources;
using Windows.Win32;

namespace MetroOsd.Translations;

/// <summary>
/// Localized UI strings loaded from Translations/string.*.resx.
///
/// The culture is picked once at startup from the current Windows display language
/// (<c>GetUserDefaultUILanguage</c>, i.e. the MUI language shown in Settings > Time &amp; Language)
/// and narrowed to the nearest supported Translations/string.&lt;lang&gt;.resx. Languages without a
/// matching file fall back through the culture chain to the neutral Translations/string.resx
/// (English).
/// </summary>
internal static class Strings
{
    /// <summary>Manifest base name of the Translations/string.resx resource set.</summary>
    private const string BaseName = "MetroOsd.Translations.string";

    private static readonly ResourceManager Manager = new(BaseName, typeof(Strings).Assembly);

    private static readonly HashSet<string> SupportedCultures = new(StringComparer.OrdinalIgnoreCase)
    {
        "zh-cn", "zh-tw", "ja-jp", "en-us", "de-de", "fr-fr", "ru-ru",
    };

    /// <summary>Culture resolved from the Windows display language (set once at startup).</summary>
    internal static CultureInfo DisplayCulture { get; } = ResolveDisplayCulture();

    /// <summary>Caps Lock ON label.</summary>
    internal static string CapsLockOn => GetString("CapsLockOn");

    /// <summary>Caps Lock OFF label.</summary>
    internal static string CapsLockOff => GetString("CapsLockOff");

    /// <summary>UI font for the display language (CJK languages get their own font).</summary>
    internal static string FontName => Manager.GetString("FontName", DisplayCulture) ?? "Segoe UI";

    private static string GetString(string name) => Manager.GetString(name, DisplayCulture) ?? name;

    private static CultureInfo ResolveDisplayCulture()
    {
        CultureInfo culture = SystemDisplayCulture();
        return NarrowToSupported(culture);
    }

    /// <summary>Reads the user's Windows display language via CsWin32 (returns a LANGID).</summary>
    private static CultureInfo SystemDisplayCulture()
    {
        try
        {
            ushort langId = PInvoke.GetUserDefaultUILanguage();
            return CultureInfo.GetCultureInfo(langId);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.CurrentUICulture;
        }
    }

    /// <summary>
    /// Narrows a display culture to the closest supported one. Exact matches win; Chinese
    /// variants split by script/region (traditional → zh-TW, otherwise → zh-CN); other languages
    /// match on the two-letter language code. Unsupported languages are returned untouched so
    /// ResourceManager falls back to the neutral English resource.
    /// </summary>
    private static CultureInfo NarrowToSupported(CultureInfo culture)
    {
        string name = culture.Name.ToLowerInvariant();
        if (SupportedCultures.Contains(name))
        {
            return culture;
        }

        // Chinese: traditional/region variants -> zh-TW, everything else -> zh-CN.
        if (name.StartsWith("zh", StringComparison.Ordinal))
        {
            bool traditional = name.Contains("hant") || name is "zh-hk" or "zh-mo" or "zh-tw";
            return CultureInfo.GetCultureInfo(traditional ? "zh-TW" : "zh-CN");
        }

        // Language-only match for the remaining supported languages.
        string lang = culture.TwoLetterISOLanguageName.ToLowerInvariant();
        string? byLang = lang switch
        {
            "en" => "en-US",
            "ja" => "ja-JP",
            "de" => "de-DE",
            "fr" => "fr-FR",
            "ru" => "ru-RU",
            _ => null,
        };
        if (byLang is not null)
        {
            return CultureInfo.GetCultureInfo(byLang);
        }

        // Unsupported display language: let ResourceManager fall back to the neutral file.
        return culture;
    }
}
