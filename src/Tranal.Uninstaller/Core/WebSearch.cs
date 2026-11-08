using System.Collections.Generic;
using System.Globalization;
using Tranal.Uninstaller.Core.Settings;

namespace Tranal.Uninstaller.Core;

/// <summary>
/// Busca na web: monta o endereco de pesquisa a partir de um termo.
///
/// O motor usado vem da preferencia: a lista nova
/// (<see cref="AppSettings.WebSearchEngines"/>) e, quando ela esta vazia, o
/// valor antigo (<see cref="AppSettings.WebSearchEngine"/>). A regra de
/// escolha e a lista de motores vivem em
/// <see cref="WebSearchEngines"/> — aqui so monta o endereco.
///
/// E o mesmo item de menu "Search at %s for" que o Revo Uninstaller Pro
/// oferece no menu de contexto.
/// </summary>
public static class WebSearch
{
    /// <summary>Mecanismo usado quando a preferencia esta vazia.</summary>
    public const string DefaultEngine = WebSearchEngines.DefaultId;

    /// <summary>
    /// Endereco de pesquisa para o termo informado. Devolve string vazia
    /// quando o termo e vazio.
    /// </summary>
    /// <param name="term">O termo pesquisado.</param>
    /// <param name="engine">
    /// O motor: o identificador de um motor do catalogo ("Bing") ou a
    /// definicao de um motor personalizado ("MeuBusca|https://...?q={0}").
    /// Vazio significa o padrao.
    /// </param>
    public static string BuildUrl(string term, string engine = "")
    {
        var query = (term ?? string.Empty).Trim();
        if (query.Length == 0) return string.Empty;

        return WebSearchEngines.Resolve(engine).BuildUrl(query);
    }

    /// <summary>
    /// Endereco de pesquisa usando a **primeira** mecanismo da lista
    /// configurada. Devolve string vazia quando o termo e vazio ou quando a
    /// lista nao tem nenhum motor valido.
    /// </summary>
    /// <param name="term">O termo pesquisado.</param>
    /// <param name="engines">Os motores configurados, na ordem.</param>
    public static string BuildUrl(string term, IEnumerable<string>? engines)
    {
        var query = (term ?? string.Empty).Trim();
        if (query.Length == 0) return string.Empty;

        var lista = WebSearchEngines.ResolveAll(engines);
        if (lista.Count == 0) return string.Empty;

        return lista[0].BuildUrl(query);
    }

    /// <summary>
    /// Endereco de pesquisa para o termo, usando as preferencias do usuario.
    ///
    /// A regra: a lista configurada vence; quando ela esta vazia, vale o
    /// valor antigo; quando nem ele existe, vale o padrao (Google).
    /// </summary>
    /// <param name="term">O termo pesquisado.</param>
    /// <param name="settings">As preferencias do usuario.</param>
    public static string BuildUrl(string term, AppSettings settings)
    {
        var query = (term ?? string.Empty).Trim();
        if (query.Length == 0) return string.Empty;

        var motor = WebSearchEngines.Preferred(settings.WebSearchEngines, settings.WebSearchEngine);
        return motor.BuildUrl(query);
    }
}
