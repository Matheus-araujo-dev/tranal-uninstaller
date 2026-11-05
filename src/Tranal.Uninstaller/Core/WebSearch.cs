using System.Globalization;

namespace Tranal.Uninstaller.Core;

/// <summary>
/// Busca na web: monta o endereco de pesquisa a partir de um termo.
///
/// A preferencia "Mecanismo de busca na web"
/// (<see cref="Settings.AppSettings.WebSearchEngine"/>) escolhe o mecanismo;
/// vazio significa o padrao. E o mesmo item de menu "Search at %s for" que
/// o Revo Uninstaller Pro oferece no menu de contexto.
/// </summary>
public static class WebSearch
{
    /// <summary>Mecanismo usado quando a preferencia esta vazia.</summary>
    public const string DefaultEngine = "Google";

    /// <summary>
    /// Endereco de pesquisa para o termo informado. Devolve string vazia
    /// quando o termo e vazio.
    /// </summary>
    public static string BuildUrl(string term, string engine = "")
    {
        var query = (term ?? string.Empty).Trim();
        if (query.Length == 0) return string.Empty;

        var escaped = Uri.EscapeDataString(query);
        var name = string.IsNullOrWhiteSpace(engine) ? DefaultEngine : engine.Trim();

        if (name.Contains("bing", StringComparison.OrdinalIgnoreCase))
            return string.Format(CultureInfo.InvariantCulture, "https://www.bing.com/search?q={0}", escaped);

        if (name.Contains("duck", StringComparison.OrdinalIgnoreCase))
            return string.Format(CultureInfo.InvariantCulture, "https://duckduckgo.com/?q={0}", escaped);

        if (name.Contains("yahoo", StringComparison.OrdinalIgnoreCase))
            return string.Format(CultureInfo.InvariantCulture, "https://search.yahoo.com/search?p={0}", escaped);

        return string.Format(CultureInfo.InvariantCulture, "https://www.google.com/search?q={0}", escaped);
    }
}
