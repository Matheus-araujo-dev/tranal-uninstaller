using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Tranal.Uninstaller.Core;

/// <summary>
/// Um motor de busca na web: o nome que o usuario ve e o modelo do
/// endereco de pesquisa.
///
/// O modelo sempre traz <c>{0}</c> no lugar do termo pesquisado. O motor
/// **personalizado** e o mesmo objeto: o que muda e que ele vem do usuario
/// e nao do catalogo.
/// </summary>
public sealed class WebSearchEngine
{
    /// <summary>Identificador do motor. Para o personalizado, e o nome escolhido.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Nome que o usuario ve na lista de motores.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Modelo do endereco, com <c>{0}</c> no lugar do termo.</summary>
    public string UrlTemplate { get; init; } = string.Empty;

    /// <summary>True quando o motor foi definido pelo usuario.</summary>
    public bool IsCustom { get; init; }

    /// <summary>
    /// Monta o endereco de pesquisa para o termo informado.
    ///
    /// Devolve string vazia quando o termo e vazio ou quando o modelo nao
    /// tem o marcador <c>{0}</c> — um modelo invalido nao pode gerar um
    /// endereco quebrado.
    /// </summary>
    /// <param name="term">O termo pesquisado.</param>
    public string BuildUrl(string term)
    {
        var query = (term ?? string.Empty).Trim();
        if (query.Length == 0) return string.Empty;
        if (!UrlTemplate.Contains("{0}", StringComparison.Ordinal)) return string.Empty;

        try
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                UrlTemplate,
                Uri.EscapeDataString(query));
        }
        catch (FormatException)
        {
            // Modelo com chaves invalidas: sem endereco, e melhor do que
            // um endereco quebrado aberto no navegador do usuario.
            return string.Empty;
        }
    }
}

/// <summary>
/// O catalogo dos motores de busca e a regra de escolha do motor.
///
/// A regra mora **aqui**, e nao na tela nem na linha de comando: quem monta
/// o endereco de pesquisa (o item de menu "Search at %s for") e quem mostra
/// a lista de motores na configuracao leem esta mesma lista.
///
/// Uma preferencia pode guardar o motor de duas formas:
/// <list type="bullet">
/// <item>o identificador de um motor do catalogo ("Bing", "DuckDuckGo");</item>
/// <item>a definicao de um motor personalizado ("MeuBusca|https://...?q={0}").</item>
/// </list>
///
/// O valor antigo (<c>AppSettings.WebSearchEngine</c>, uma string) continua
/// valendo: e lido como a primeira opcao quando a lista nova esta vazia.
/// </summary>
public static class WebSearchEngines
{
    /// <summary>O identificador do motor usado quando nada foi escolhido.</summary>
    public const string DefaultId = "Google";

    /// <summary>
    /// O separador entre o nome e o modelo na definicao de um motor
    /// personalizado.
    /// </summary>
    public const string CustomSeparator = "|";

    /// <summary>Os motores que vem com o aplicativo.</summary>
    public static IReadOnlyList<WebSearchEngine> BuiltIn { get; } = new[]
    {
        new WebSearchEngine
        {
            Id = "Google",
            DisplayName = "Google",
            UrlTemplate = "https://www.google.com/search?q={0}",
        },
        new WebSearchEngine
        {
            Id = "Bing",
            DisplayName = "Bing",
            UrlTemplate = "https://www.bing.com/search?q={0}",
        },
        new WebSearchEngine
        {
            Id = "DuckDuckGo",
            DisplayName = "DuckDuckGo",
            UrlTemplate = "https://duckduckgo.com/?q={0}",
        },
        new WebSearchEngine
        {
            Id = "Yandex",
            DisplayName = "Yandex",
            UrlTemplate = "https://yandex.com/search/?text={0}",
        },
        new WebSearchEngine
        {
            Id = "Yahoo",
            DisplayName = "Yahoo",
            UrlTemplate = "https://search.yahoo.com/search?p={0}",
        },
    };

    /// <summary>
    /// As palavras que identificam um motor quando a preferencia nao traz o
    /// identificador exato. E o que mantem compatibilidade com o valor
    /// antigo, que o usuario podia digitar ("duck", "bing").
    /// </summary>
    private static readonly Dictionary<string, string[]> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Google"] = new[] { "google" },
        ["Bing"] = new[] { "bing" },
        ["DuckDuckGo"] = new[] { "duckduckgo", "duck" },
        ["Yandex"] = new[] { "yandex" },
        ["Yahoo"] = new[] { "yahoo" },
    };

    /// <summary>O motor padrao (Google).</summary>
    public static WebSearchEngine Default => BuiltIn[0];

    /// <summary>
    /// True quando a definicao informada e de um motor personalizado.
    /// </summary>
    /// <param name="definition">A definicao guardada na preferencia.</param>
    public static bool IsCustomDefinition(string? definition) =>
        (definition ?? string.Empty).Contains(CustomSeparator, StringComparison.Ordinal);

    /// <summary>
    /// Monta a definicao de um motor personalizado.
    ///
    /// Devolve string vazia quando o nome e vazio ou quando o modelo nao
    /// tem o marcador <c>{0}</c> — sem ele nao ha como montar o endereco.
    /// </summary>
    /// <param name="name">O nome do motor.</param>
    /// <param name="urlTemplate">O modelo do endereco, com <c>{0}</c>.</param>
    public static string FormatCustom(string name, string urlTemplate)
    {
        var rotulo = (name ?? string.Empty).Trim();
        var modelo = (urlTemplate ?? string.Empty).Trim();

        if (rotulo.Length == 0) return string.Empty;
        if (!modelo.Contains("{0}", StringComparison.Ordinal)) return string.Empty;

        return rotulo + CustomSeparator + modelo;
    }

    /// <summary>
    /// Le a definicao guardada na preferencia e devolve o motor.
    ///
    /// Devolve nulo quando a definicao nao corresponde a nenhum motor — quem
    /// chama decide o que fazer (o padrao, ou avisar o usuario).
    /// </summary>
    /// <param name="definition">A definicao guardada na preferencia.</param>
    public static WebSearchEngine? TryParse(string? definition)
    {
        var texto = (definition ?? string.Empty).Trim();
        if (texto.Length == 0) return null;

        if (texto.Contains(CustomSeparator, StringComparison.Ordinal))
        {
            var partes = texto.Split(new[] { CustomSeparator }, 2, StringSplitOptions.None);
            var nome = partes[0].Trim();
            var modelo = partes.Length > 1 ? partes[1].Trim() : string.Empty;

            if (nome.Length == 0) return null;
            if (!modelo.Contains("{0}", StringComparison.Ordinal)) return null;

            return new WebSearchEngine
            {
                Id = nome,
                DisplayName = nome,
                UrlTemplate = modelo,
                IsCustom = true,
            };
        }

        foreach (var motor in BuiltIn)
        {
            if (string.Equals(motor.Id, texto, StringComparison.OrdinalIgnoreCase)) return motor;
            if (string.Equals(motor.DisplayName, texto, StringComparison.OrdinalIgnoreCase)) return motor;
        }

        if (Keywords.TryGetValue(texto, out var chaves))
        {
            foreach (var motor in BuiltIn)
            {
                if (chaves.Contains(motor.Id, StringComparer.OrdinalIgnoreCase)) return motor;
            }
        }

        // O valor antigo gravava o nome solto ("duck", "Bing"): a busca por
        // palavra mantem esses nomes funcionando.
        foreach (var motor in BuiltIn)
        {
            if (!Keywords.TryGetValue(motor.Id, out var palavras)) continue;
            foreach (var palavra in palavras)
            {
                if (texto.Contains(palavra, StringComparison.OrdinalIgnoreCase)) return motor;
            }
        }

        return null;
    }

    /// <summary>
    /// Le a definicao guardada na preferencia e devolve o motor; quando a
    /// definicao nao vale, devolve o motor padrao.
    /// </summary>
    /// <param name="definition">A definicao guardada na preferencia.</param>
    public static WebSearchEngine Resolve(string? definition) =>
        TryParse(definition) ?? Default;

    /// <summary>
    /// Resolve uma lista de definicoes, na ordem em que foram guardadas.
    ///
    /// As definicoes invalidas somem e as repetidas contam uma vez so: a
    /// lista que a tela mostra e a lista que a busca usa.
    /// </summary>
    /// <param name="definitions">As definicoes guardadas na preferencia.</param>
    public static IReadOnlyList<WebSearchEngine> ResolveAll(IEnumerable<string>? definitions)
    {
        var resultado = new List<WebSearchEngine>();
        if (definitions is null) return resultado;

        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var definicao in definitions)
        {
            var motor = TryParse(definicao);
            if (motor is null) continue;
            if (!vistos.Add(motor.Id)) continue;

            resultado.Add(motor);
        }

        return resultado;
    }

    /// <summary>
    /// O motor que a busca usa, dada a lista de motores configurada e o
    /// valor antigo da preferencia.
    ///
    /// A regra e uma so: a **primeira** da lista vence; quando a lista esta
    /// vazia, vale o valor antigo (<c>WebSearchEngine</c>); quando nem ele
    /// existe, vale o padrao (Google).
    /// </summary>
    /// <param name="engines">A lista de motores configurada.</param>
    /// <param name="legacy">O valor antigo da preferencia.</param>
    public static WebSearchEngine Preferred(IEnumerable<string>? engines, string? legacy)
    {
        var lista = ResolveAll(engines);
        if (lista.Count > 0) return lista[0];

        return Resolve(legacy);
    }
}
