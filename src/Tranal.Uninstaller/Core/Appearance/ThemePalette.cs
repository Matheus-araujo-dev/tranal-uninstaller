using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Tranal.Uninstaller.Core.Appearance;

/// <summary>
/// O que o usuario pode customize no tema.
///
/// Os papeis sao **fixos** e cada um tem um uso na tela: e a lista unica de
/// "o que pode mudar de cor". Um papel que nao tem uso na tela nao entra
/// aqui — e um papel que a tela usa e nao esta aqui e uma cor que o usuario
/// nao consegue mudar.
/// </summary>
public enum ThemeColorRole
{
    /// <summary>Fundo da janela principal.</summary>
    Background,

    /// <summary>Fundo dos paineis e da lista.</summary>
    Panel,

    /// <summary>Fundo das cabecalhos.</summary>
    Header,

    /// <summary>Cor do texto.</summary>
    Text,

    /// <summary>Cor do texto esmaecido (dicas, rotulos apagados).</summary>
    MutedText,

    /// <summary>Cor da selecao.</summary>
    Selection,

    /// <summary>Cor do texto sobre a selecao.</summary>
    SelectionText,

    /// <summary>Cor de destaque (links, foco).</summary>
    Accent,

    /// <summary>Cor da borda.</summary>
    Border,

    /// <summary>Cor do botao.</summary>
    Button,

    /// <summary>
    /// Cor do texto dos botaos de comando (16133 do Revo: "Command button
    /// text color").
    /// </summary>
    CommandButtonText,

    /// <summary>
    /// Cor de fundo dos dialogos (16150 do Revo: "Dialogs background
    /// color").
    /// </summary>
    DialogBackground,
}

/// <summary>
/// Um tema do Tranal: um conjunto de cores com nome.
///
/// A descricao mora aqui e nao no XAML: e a lista de skins que o painel de
/// configuracao mostra e que a CLI lista. Um tema que existe no
/// <c>Themes/*.xaml</c> e nao esta no catalogo nao aparece para o usuario.
/// </summary>
public sealed class ThemeSkin
{
    /// <summary>O identificador do tema (o nome do dicionario XAML).</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>O nome que o usuario ve.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Uma frase sobre o que o tema e.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>True quando o tema tem fundo escuro.</summary>
    public bool IsDark { get; init; } = true;
}

/// <summary>
/// O catalogo de temas.
///
/// O Revo Pro tem dez skins; o Tranal comeca com as que existem como
/// dicionario XAML. O catalogo e a **fonte unica** do que existe: a opcoes
/// de tema, o painel de configuracao e a linha de comando leem esta lista,
/// e nao cada um a sua.
/// </summary>
public static class ThemeSkinCatalog
{
    /// <summary>Todos os temas, na ordem em que devem ser apresentados.</summary>
    public static IReadOnlyList<ThemeSkin> All { get; } = new[]
    {
        new ThemeSkin
        {
            Id = "Dark",
            DisplayName = "Escuro",
            Description = "O tema escuro, com fundo cinza e destaque azul.",
            IsDark = true,
        },
        new ThemeSkin
        {
            Id = "Slate",
            DisplayName = "Ardosia",
            Description = "Um tema escuro de tom azulado, para telas com muito texto.",
            IsDark = true,
        },
        new ThemeSkin
        {
            Id = "Contrast",
            DisplayName = "Alto contraste",
            Description = "Preto e branco com contorno forte, para leitura com pouca luz.",
            IsDark = true,
        },
        new ThemeSkin
        {
            Id = "Light",
            DisplayName = "Clássico",
            Description = "O tema claro, com fundo claro e tinta escura.",
            IsDark = false,
        },
    };

    /// <summary>O tema usado quando nada foi escolhido.</summary>
    public const string Default = "Dark";

    /// <summary>True quando o id informado e um tema do catalogo.</summary>
    public static bool Exists(string? id) =>
        !string.IsNullOrWhiteSpace(id)
        && All.Any(s => string.Equals(s.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Normaliza o nome de um tema: devolve o id do catalogo.
    ///
    /// Aceita o nome traduzido ("claro", "classico") e o id ("Light"), porque
    /// a preferencia pode ter sido gravada por qualquer um dos dois.
    /// </summary>
    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return Default;

        var text = name.Trim();

        foreach (var skin in All)
        {
            if (string.Equals(skin.Id, text, StringComparison.OrdinalIgnoreCase)) return skin.Id;
            if (string.Equals(skin.DisplayName, text, StringComparison.OrdinalIgnoreCase)) return skin.Id;
        }

        // Os apelidos antigos do projeto: "claro" e "classico" eram as duas
        // formas de escrever o tema claro antes de existirem outros.
        if (text.Equals("claro", StringComparison.OrdinalIgnoreCase)
            || text.Equals("clássico", StringComparison.OrdinalIgnoreCase)
            || text.Equals("classico", StringComparison.OrdinalIgnoreCase))
        {
            return "Light";
        }

        return Default;
    }

    /// <summary>O tema do id, ou o padrao quando o id nao existe.</summary>
    public static ThemeSkin? Get(string? id)
    {
        var normalized = Normalize(id);
        return All.FirstOrDefault(s => string.Equals(s.Id, normalized, StringComparison.Ordinal));
    }
}

/// <summary>
/// Um conjunto de cores do tema.
///
/// E um dicionario de papel para cor, e nao uma lista solta de cores: e o
/// papel que diz onde a cor entra na tela. Uma cor sem papel nao tem onde
/// ser usada.
/// </summary>
public sealed class ThemePalette
{
    /// <summary>As cores, por papel.</summary>
    public IReadOnlyDictionary<ThemeColorRole, string> Colors { get; init; } =
        new Dictionary<ThemeColorRole, string>();

    /// <summary>
    /// A cor de um papel, ou string vazia quando o papel nao tem cor.
    /// </summary>
    public string ColorOf(ThemeColorRole role) =>
        Colors.TryGetValue(role, out var value) ? value : string.Empty;

    /// <summary>
    /// Aplica as cores personalizadas por cima desta paleta.
    ///
    /// A regra e simples e a unica: **a cor personalizada ganha**. Uma cor
    /// invalida no arquivo de preferencias e ignorada — ela nao apaga a cor
    /// do tema, porque um arquivo de preferencia corrompido nao pode deixar
    /// a tela sem cor nenhuma.
    /// </summary>
    public ThemePalette Merge(IReadOnlyDictionary<string, string>? custom)
    {
        var merged = new Dictionary<ThemeColorRole, string>(Colors);

        if (custom is null) return new ThemePalette { Colors = merged };

        foreach (var (role, hex) in custom)
        {
            if (!TryParseRole(role, out var parsed)) continue;
            if (!ThemePalette.TryNormalizeHex(hex, out var normalized)) continue;

            merged[parsed] = normalized;
        }

        return new ThemePalette { Colors = merged };
    }

    /// <summary>True quando a paleta nao tem cor nenhuma.</summary>
    public bool IsEmpty => Colors.Count == 0;

    /// <summary>
    /// Converte o nome de um papel (como aparece no arquivo de preferencias)
    /// para o papel do enum.
    /// </summary>
    public static bool TryParseRole(string? name, out ThemeColorRole role)
    {
        role = ThemeColorRole.Background;
        if (string.IsNullOrWhiteSpace(name)) return false;

        var text = name.Trim().Replace("_", string.Empty).Replace(" ", string.Empty);

        foreach (var candidate in Enum.GetValues<ThemeColorRole>())
        {
            if (string.Equals(candidate.ToString(), text, StringComparison.OrdinalIgnoreCase))
            {
                role = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Normaliza uma cor para <c>#RRGGBB</c> (ou <c>#AARRGGBB</c>).
    ///
    /// Aceita <c>#RGB</c>, <c>#RRGGBB</c>, <c>#AARRGGBB</c>, com ou sem o
    /// sinal de numero. Devolve <c>false</c> para o que nao e cor — e quem
    /// chama decide o que fazer com o erro.
    /// </summary>
    public static bool TryNormalizeHex(string? value, out string hex)
    {
        hex = string.Empty;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var text = value.Trim();
        if (text.StartsWith("#", StringComparison.Ordinal)) text = text[1..];
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];

        if (text.Length == 3)
        {
            var r = text[0];
            var g = text[1];
            var b = text[2];
            text = new string(new[] { r, r, g, g, b, b });
        }

        if (text.Length != 6 && text.Length != 8) return false;

        foreach (var ch in text)
        {
            if (!Uri.IsHexDigit(ch)) return false;
        }

        hex = "#" + text.ToUpperInvariant();
        return true;
    }
}

/// <summary>
/// As paletas que o Tranal usa por padrao.
///
/// Sao os **mesmos** valores dos dicionarios XAML (<c>Themes/Dark.xaml</c> e
/// <c>Themes/Light.xaml</c>). A duplicacao aqui e intencional e limitada: a
/// paleta e o que a linha de comando e o painel de configuracao mostram,
/// e o XAML e o que a tela usa. Mudar um sem o outro e um defeito — por isso
/// o teste do projeto compara as duas.
/// </summary>
public static class ThemePalettes
{
    /// <summary>A paleta do tema escuro.</summary>
    public static ThemePalette Dark { get; } = new()
    {
        Colors = new Dictionary<ThemeColorRole, string>
        {
            [ThemeColorRole.Background] = "#FF3C3C3C",
            [ThemeColorRole.Panel] = "#FF525252",
            [ThemeColorRole.Header] = "#FF3C3C3C",
            [ThemeColorRole.Text] = "#FFE6E6E6",
            [ThemeColorRole.MutedText] = "#FFB4B4B4",
            [ThemeColorRole.Selection] = "#FF2F6FB5",
            [ThemeColorRole.SelectionText] = "#FFFFFFFF",
            [ThemeColorRole.Accent] = "#FF3399FF",
            [ThemeColorRole.Border] = "#FF2E2E2E",
            [ThemeColorRole.Button] = "#FF4A4A4A",
            [ThemeColorRole.CommandButtonText] = "#FFE6E6E6",
            [ThemeColorRole.DialogBackground] = "#FF525252",
        },
    };

    /// <summary>A paleta do tema claro.</summary>
    public static ThemePalette Light { get; } = new()
    {
        Colors = new Dictionary<ThemeColorRole, string>
        {
            [ThemeColorRole.Background] = "#FFF0F0F0",
            [ThemeColorRole.Panel] = "#FFFFFFFF",
            [ThemeColorRole.Header] = "#FFE4E4E4",
            [ThemeColorRole.Text] = "#FF1A1A1A",
            [ThemeColorRole.MutedText] = "#FF5A5A5A",
            [ThemeColorRole.Selection] = "#FFCCE4FF",
            [ThemeColorRole.SelectionText] = "#FF101010",
            [ThemeColorRole.Accent] = "#FF0B5CAB",
            [ThemeColorRole.Border] = "#FFC8C8C8",
            [ThemeColorRole.Button] = "#FFE8E8E8",
            [ThemeColorRole.CommandButtonText] = "#FF1A1A1A",
            [ThemeColorRole.DialogBackground] = "#FFFFFFFF",
        },
    };

    /// <summary>A paleta do tema ardosia.</summary>
    public static ThemePalette Slate { get; } = new()
    {
        Colors = new Dictionary<ThemeColorRole, string>
        {
            [ThemeColorRole.Background] = "#FF1E2A38",
            [ThemeColorRole.Panel] = "#FF27384C",
            [ThemeColorRole.Header] = "#FF223142",
            [ThemeColorRole.Text] = "#FFE8EEF5",
            [ThemeColorRole.MutedText] = "#FF9FB0C4",
            [ThemeColorRole.Selection] = "#FF2A6FA8",
            [ThemeColorRole.SelectionText] = "#FFFFFFFF",
            [ThemeColorRole.Accent] = "#FF5AA9E6",
            [ThemeColorRole.Border] = "#FF16202C",
            [ThemeColorRole.Button] = "#FF31465E",
            [ThemeColorRole.CommandButtonText] = "#FFE8EEF5",
            [ThemeColorRole.DialogBackground] = "#FF27384C",
        },
    };

    /// <summary>A paleta do tema de alto contraste.</summary>
    public static ThemePalette Contrast { get; } = new()
    {
        Colors = new Dictionary<ThemeColorRole, string>
        {
            [ThemeColorRole.Background] = "#FF000000",
            [ThemeColorRole.Panel] = "#FF000000",
            [ThemeColorRole.Header] = "#FF000000",
            [ThemeColorRole.Text] = "#FFFFFFFF",
            [ThemeColorRole.MutedText] = "#FFD0D0D0",
            [ThemeColorRole.Selection] = "#FF0000AA",
            [ThemeColorRole.SelectionText] = "#FFFFFFFF",
            [ThemeColorRole.Accent] = "#FFFFFF00",
            [ThemeColorRole.Border] = "#FFFFFFFF",
            [ThemeColorRole.Button] = "#FF202020",
            [ThemeColorRole.CommandButtonText] = "#FFFFFFFF",
            [ThemeColorRole.DialogBackground] = "#FF000000",
        },
    };

    /// <summary>A paleta de um tema pelo id.</summary>
    public static ThemePalette Of(string? skinId) => Normalize(skinId) switch
    {
        "Light" => Light,
        "Slate" => Slate,
        "Contrast" => Contrast,
        _ => Dark,
    };

    /// <summary>Normaliza o id do tema para um dos quatro conhecidos.</summary>
    public static string Normalize(string? skinId) => ThemeSkinCatalog.Normalize(skinId);

    /// <summary>
    /// Os papeis de cor, na ordem em que aparecem no painel de configuracao.
    /// </summary>
    public static IReadOnlyList<ThemeColorRole> Roles { get; } = Enum.GetValues<ThemeColorRole>();

    /// <summary>
    /// A descricao de um papel, para o painel e para a linha de comando.
    ///
    /// O texto mora aqui, e nao no XAML nem na tela: e a mesma frase que o
    /// usuario ve na configuracao e que a CLI imprime.
    /// </summary>
    public static string DescribeRole(ThemeColorRole role) => role switch
    {
        ThemeColorRole.Background => "Fundo da janela",
        ThemeColorRole.Panel => "Fundo dos painéis",
        ThemeColorRole.Header => "Fundo dos cabeçalhos",
        ThemeColorRole.Text => "Texto",
        ThemeColorRole.MutedText => "Texto esmaecido",
        ThemeColorRole.Selection => "Seleção",
        ThemeColorRole.SelectionText => "Texto da seleção",
        ThemeColorRole.Accent => "Destaque",
        ThemeColorRole.Border => "Borda",
        ThemeColorRole.Button => "Botão",
        ThemeColorRole.CommandButtonText => "Texto dos botões de comando",
        ThemeColorRole.DialogBackground => "Fundo dos diálogos",
        _ => role.ToString(),
    };

    /// <summary>
    /// Converte a paleta para o dicionario de cores que vai para o arquivo
    /// de preferencias.
    /// </summary>
    public static Dictionary<string, string> ToCustomColors(ThemePalette palette)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (role, color) in palette.Colors)
            result[role.ToString()] = color;

        return result;
    }

    /// <summary>
    /// Converte o dicionario de cores das preferencias para uma paleta.
    ///
    /// As cores invalidas sao descartadas: uma preferencia corrompida nao
    /// pode deixar a tela sem cor.
    /// </summary>
    public static ThemePalette FromCustomColors(
        IReadOnlyDictionary<string, string>? custom, string? skinId)
    {
        var basePalette = Of(skinId);
        return basePalette.Merge(custom);
    }

    /// <summary>
    /// Uma cor como o WPF a entende, a partir do hex.
    ///
    /// O hex aceito aqui e o mesmo do painel: <c>#RRGGBB</c> ou
    /// <c>#AARRGGBB</c>.
    /// </summary>
    public static bool TryParseColor(string? hex, out uint argb)
    {
        argb = 0;
        if (!ThemePalette.TryNormalizeHex(hex, out var normalized)) return false;

        var text = normalized[1..];
        if (text.Length == 6) text = "FF" + text;

        if (!uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out argb))
            return false;

        return true;
    }
}
