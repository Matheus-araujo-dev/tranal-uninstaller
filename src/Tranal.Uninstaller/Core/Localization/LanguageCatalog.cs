namespace Tranal.Uninstaller.Core.Localization;

/// <summary>Um idioma disponible na interface.</summary>
/// <param name="Code">Codigo de idioma (pt-BR, en-US, zh-Hans...).</param>
/// <param name="NativeName">Nome do idioma no proprio idioma.</param>
/// <param name="EnglishName">Nome do idioma em ingles.</param>
public sealed record LanguageInfo(string Code, string NativeName, string EnglishName);

/// <summary>
/// Catalogo de idiomas do Tranal Uninstaller Pro.
///
/// A lista tem os mesmos 48 idiomas que o Revo Pro 5.5.4 distribuia (medido no
/// payload extraido: 48 arquivos .ini em app\lang). Os nomes sao dados factuais
/// de lingua; as traducoes em si sao do Tranal.
/// </summary>
public static class LanguageCatalog
{
    public static IReadOnlyList<LanguageInfo> All { get; } = new[]
    {
        new LanguageInfo("sq",       "Shqip",                     "Albanian"),
        new LanguageInfo("ar",       "العربية",                    "Arabic"),
        new LanguageInfo("hy",       "Հայերեն",                   "Armenian"),
        new LanguageInfo("az",       "Azərbaycan dili",           "Azerbaijani"),
        new LanguageInfo("bn",       "বাংলা",                      "Bengali"),
        new LanguageInfo("bg",       "Български",                  "Bulgarian"),
        new LanguageInfo("cs",       "Čeština",                   "Czech"),
        new LanguageInfo("da",       "Dansk",                     "Danish"),
        new LanguageInfo("nl",       "Nederlands",                "Dutch"),
        new LanguageInfo("en",       "English",                   "English"),
        new LanguageInfo("et",       "Eesti",                     "Estonian"),
        new LanguageInfo("fi",       "Suomi",                     "Finnish"),
        new LanguageInfo("fr",       "Français",                  "French"),
        new LanguageInfo("de",       "Deutsch",                   "German"),
        new LanguageInfo("gu",       "ગુજરાતી",                    "Gujarati"),
        new LanguageInfo("he",       "עברית",                      "Hebrew"),
        new LanguageInfo("el",       "Ελληνικά",                   "Greek"),
        new LanguageInfo("hi",       "हिन्दी",                       "Hindi"),
        new LanguageInfo("hr",       "Hrvatski",                  "Croatian"),
        new LanguageInfo("hu",       "Magyar",                    "Hungarian"),
        new LanguageInfo("id",       "Bahasa Indonesia",          "Indonesian"),
        new LanguageInfo("it",       "Italiano",                  "Italian"),
        new LanguageInfo("ja",       "日本語",                      "Japanese"),
        new LanguageInfo("ko",       "한국어",                      "Korean"),
        new LanguageInfo("ku",       "Kurdî",                     "Kurdish"),
        new LanguageInfo("lt",       "Lietuvių",                  "Lithuanian"),
        new LanguageInfo("mk",       "Македонски",                 "Macedonian"),
        new LanguageInfo("nb",       "Norsk bokmål",              "Norwegian"),
        new LanguageInfo("fa",       "فارسی",                      "Persian"),
        new LanguageInfo("pl",       "Polski",                    "Polish"),
        new LanguageInfo("pt",       "Português",                 "Portuguese"),
        new LanguageInfo("pt-PT",    "Português (Portugal)",      "Portuguese (Portugal)"),
        new LanguageInfo("pt-BR",    "Português (Brasil)",        "Portuguese (Brazil)"),
        new LanguageInfo("ro",       "Română",                    "Romanian"),
        new LanguageInfo("ru",       "Русский",                   "Russian"),
        new LanguageInfo("sr",       "Српски",                    "Serbian"),
        new LanguageInfo("sr-Latn",  "Srpski (latinica)",         "Serbian (Latin)"),
        new LanguageInfo("zh-Hans",  "简体中文",                    "Simplified Chinese"),
        new LanguageInfo("sk",       "Slovenčina",                "Slovak"),
        new LanguageInfo("sl",       "Slovenščina",               "Slovenian"),
        new LanguageInfo("es",       "Español",                   "Spanish"),
        new LanguageInfo("sv",       "Svenska",                   "Swedish"),
        new LanguageInfo("th",       "ไทย",                        "Thai"),
        new LanguageInfo("zh-Hant",  "繁體中文",                    "Traditional Chinese"),
        new LanguageInfo("tr",       "Türkçe",                    "Turkish"),
        new LanguageInfo("uk",       "Українська",                 "Ukrainian"),
        new LanguageInfo("uz",       "Oʻzbekcha",                 "Uzbek"),
        new LanguageInfo("vi",       "Tiếng Việt",                "Vietnamese"),
    };

    /// <summary>Numeros de idiomas disponiveis (48, como no Revo Pro 5.5.4).</summary>
    public static int Count => All.Count;

    /// <summary>True quando o codigo existe no catalogo.</summary>
    public static bool Exists(string code) =>
        All.Any(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));

    /// <summary>Devolve o nome nativo de um codigo (ou o proprio codigo).</summary>
    public static string NativeName(string code) =>
        All.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase))?.NativeName ?? code;
}
