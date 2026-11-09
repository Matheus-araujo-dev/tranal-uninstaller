using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Tranal.Uninstaller.Core.Localization;

/// <summary>
/// Os nomes dos países, no idioma do aplicativo.
///
/// A REGRA de como o nome do país aparece mora aqui: o nome vem da tabela
/// abaixo, no idioma que o Tranal está falando (pt-BR por padrão, en-US
/// como segundo). Ele nunca vem do "DisplayName" da cultura do Windows,
/// porque aquilo responde a uma pergunta errada — o nome do país no
/// idioma do USUÁRIO, e não no idioma do aplicativo.
///
/// A consequência prática é que a máquina em inglês dos EUA continua
/// mostrando "Brasil" e não "Brazil" ou (pior) "Spanish (Brazil)", que é
/// o que acontece quando se procura "alguma cultura da região BR" e a
/// primeira da lista é a espanhola.
///
/// Cada país aparece UMA vez, com o nome em pt-BR e em en-US juntos: uma
/// tabela só, para não existir a chance de um idioma ter mais países que o
/// outro.
/// </summary>
public static class CountryNames
{
    /// <summary>
    /// O código de duas letras e o nome em pt-BR e em en-US.
    /// </summary>
    private static readonly Dictionary<string, (string Pt, string En)> Tabela = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AE"] = ("Emirados Árabes Unidos", "United Arab Emirates"),
        ["AR"] = ("Argentina", "Argentina"),
        ["AT"] = ("Áustria", "Austria"),
        ["AU"] = ("Austrália", "Australia"),
        ["BA"] = ("Bósnia e Herzegovina", "Bosnia and Herzegovina"),
        ["BE"] = ("Bélgica", "Belgium"),
        ["BG"] = ("Bulgária", "Bulgaria"),
        ["BR"] = ("Brasil", "Brazil"),
        ["BY"] = ("Bielorrússia", "Belarus"),
        ["CA"] = ("Canadá", "Canada"),
        ["CH"] = ("Suíça", "Switzerland"),
        ["CL"] = ("Chile", "Chile"),
        ["CN"] = ("China", "China"),
        ["CO"] = ("Colômbia", "Colombia"),
        ["CY"] = ("Chipre", "Cyprus"),
        ["CZ"] = ("República Tcheca", "Czechia"),
        ["DE"] = ("Alemanha", "Germany"),
        ["DK"] = ("Dinamarca", "Denmark"),
        ["EE"] = ("Estônia", "Estonia"),
        ["EG"] = ("Egito", "Egypt"),
        ["ES"] = ("Espanha", "Spain"),
        ["FI"] = ("Finlândia", "Finland"),
        ["FR"] = ("França", "France"),
        ["GB"] = ("Reino Unido", "United Kingdom"),
        ["GR"] = ("Grécia", "Greece"),
        ["HK"] = ("Hong Kong", "Hong Kong"),
        ["HR"] = ("Croácia", "Croatia"),
        ["HU"] = ("Hungria", "Hungary"),
        ["ID"] = ("Indonésia", "Indonesia"),
        ["IE"] = ("Irlanda", "Ireland"),
        ["IL"] = ("Israel", "Israel"),
        ["IN"] = ("Índia", "India"),
        ["IR"] = ("Irã", "Iran"),
        ["IS"] = ("Islândia", "Iceland"),
        ["IT"] = ("Itália", "Italy"),
        ["JP"] = ("Japão", "Japan"),
        ["KE"] = ("Quênia", "Kenya"),
        ["KR"] = ("Coreia do Sul", "South Korea"),
        ["KZ"] = ("Cazaquistão", "Kazakhstan"),
        ["LT"] = ("Lituânia", "Lithuania"),
        ["LU"] = ("Luxemburgo", "Luxembourg"),
        ["LV"] = ("Letônia", "Latvia"),
        ["MA"] = ("Marrocos", "Morocco"),
        ["MD"] = ("Moldávia", "Moldova"),
        ["MX"] = ("México", "Mexico"),
        ["MY"] = ("Malásia", "Malaysia"),
        ["NG"] = ("Nigéria", "Nigeria"),
        ["NL"] = ("Países Baixos", "Netherlands"),
        ["NO"] = ("Noruega", "Norway"),
        ["NZ"] = ("Nova Zelândia", "New Zealand"),
        ["PE"] = ("Peru", "Peru"),
        ["PH"] = ("Filipinas", "Philippines"),
        ["PK"] = ("Paquistão", "Pakistan"),
        ["PL"] = ("Polônia", "Poland"),
        ["PT"] = ("Portugal", "Portugal"),
        ["RO"] = ("Romênia", "Romania"),
        ["RS"] = ("Sérvia", "Serbia"),
        ["RU"] = ("Rússia", "Russia"),
        ["SA"] = ("Arábia Saudita", "Saudi Arabia"),
        ["SE"] = ("Suécia", "Sweden"),
        ["SG"] = ("Singapura", "Singapore"),
        ["SI"] = ("Eslovênia", "Slovenia"),
        ["SK"] = ("Eslováquia", "Slovakia"),
        ["TH"] = ("Tailândia", "Thailand"),
        ["TR"] = ("Turquia", "Turkey"),
        ["TW"] = ("Taiwan", "Taiwan"),
        ["UA"] = ("Ucrânia", "Ukraine"),
        ["US"] = ("Estados Unidos", "United States"),
        ["UY"] = ("Uruguai", "Uruguay"),
        ["UZ"] = ("Uzbequistão", "Uzbekistan"),
        ["VE"] = ("Venezuela", "Venezuela"),
        ["VN"] = ("Vietnã", "Vietnam"),
        ["ZA"] = ("África do Sul", "South Africa"),
    };

    /// <summary>
    /// O nome do país no idioma pedido.
    /// </summary>
    /// <param name="codigo">O código ISO de duas letras ("BR").</param>
    /// <param name="idioma">O idioma ("pt-BR", "en-US"). Padrão: pt-BR.</param>
    public static string Nome(string? codigo, string? idioma = null)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return string.Empty;

        var limpo = codigo.Trim().ToUpperInvariant();
        if (Tabela.TryGetValue(limpo, out var par))
            return EmIngles(idioma) ? par.En : par.Pt;

        // País fora da tabela: o nome nativo da região, que é o nome do
        // país no idioma DELE (o do "Brasil" para o BR, o do "Japan" para
        // o JP). É melhor que devolver nada.
        try
        {
            return new RegionInfo(limpo).NativeName;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// True quando o código é um país que a tabela conhece.
    /// </summary>
    /// <param name="codigo">O código ISO de duas letras.</param>
    public static bool Conhecido(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return false;
        return Tabela.ContainsKey(codigo.Trim().ToUpperInvariant());
    }

    /// <summary>
    /// Todos os países da tabela, em ordem alfabética pelo nome em pt-BR.
    /// Serve para procurar um país dentro de um texto ("ProductName").
    /// </summary>
    public static IReadOnlyList<(string Code, string Pt, string En)> Todos()
    {
        return Tabela
            .Select(p => (Code: p.Key, Pt: p.Value.Pt, En: p.Value.En))
            .OrderBy(p => p.Pt, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Procura o país citado por extenso dentro de um texto.
    ///
    /// O texto é o "ProductName" do executável, que costuma trazer o país
    /// por extenso ("7-Zip, Brasil"). A busca olha o nome em pt-BR e em
    /// en-US, porque o recurso do arquivo pode estar em qualquer um dos
    /// dois idiomas.
    /// </summary>
    /// <param name="texto">O texto onde procurar.</param>
    /// <returns>O código do país encontrado, ou null.</returns>
    public static string? CodigoNoTexto(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;

        // O nome mais longo primeiro: "Países Baixos" tem de ser testado
        // antes de "Países" (que não está na tabela) e "Coreia do Sul"
        // antes de qualquer nome curto que esteja dentro dele.
        var porTamanho = Todos()
            .SelectMany(p => new[] { (Codigo: p.Code, Nome: p.Pt), (Codigo: p.Code, Nome: p.En) })
            .Where(p => p.Nome.Length >= 4)
            .OrderByDescending(p => p.Nome.Length)
            .ToList();

        foreach (var item in porTamanho)
        {
            if (texto.Contains(item.Nome, StringComparison.OrdinalIgnoreCase))
                return item.Codigo;
        }

        return null;
    }

    private static bool EmIngles(string? idioma)
    {
        if (string.IsNullOrWhiteSpace(idioma)) return false;
        return idioma.StartsWith("en", StringComparison.OrdinalIgnoreCase);
    }
}
