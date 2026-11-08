using System;
using System.Collections.Generic;
using System.Globalization;

namespace Tranal.Uninstaller.Core.Appearance;

/// <summary>
/// O tipo de deficiencia de visao de cores que a paleta precisa atender.
/// </summary>
public enum ColorVisionMode
{
    /// <summary>Sem ajuste: as cores padrao do tema.</summary>
    Standard,

    /// <summary>Protanopia: o vermelho some (nao distingue o vermelho do verde).</summary>
    Protanopia,

    /// <summary>Deuteranopia: o verde parece vermelho (a confusao mais comum).</summary>
    Deuteranopia,

    /// <summary>Tritanopia: o azul e o amarelo se confundem.</summary>
    Tritanopia,
}

/// <summary>
/// A paleta para quem nao distingue o vermelho do verde.
///
/// O par vermelho/verde e o sinal mais usado da interface (perigo x
/// sucesso) e e justamente o par que quem tem deuteranopia ou protanopia
/// nao distingue. Aqui o par vira laranja x azul, que se distingue em
/// qualquer deficiencia de visao de cores — e cada cor ganha um
/// luminancia diferente, para que o brilho seja o segundo sinal quando a
/// cor nao basta.
///
/// A regra mora aqui, em um lugar so: o tema e o realce da lista consomem
/// esta paleta e nunca escolhem a cor por conta propria.
/// </summary>
public static class ColorBlindPalette
{
    /// <summary>A cor de realce do aviso, no modo padrao.</summary>
    public const string StandardWarning = "#FFE3B341";

    /// <summary>A cor de realce do perigo, no modo padrao.</summary>
    public const string StandardDanger = "#FFE05C5C";

    /// <summary>A cor de realce do sucesso, no modo padrao.</summary>
    public const string StandardSuccess = "#FF4CAF50";

    /// <summary>A cor de realce do destaque, no modo padrao.</summary>
    public const string StandardAccent = "#FF4C9AFF";

    /// <summary>
    /// As cores de realce por modo, em ordem: aviso, perigo, sucesso,
    /// destaque.
    ///
    /// O par perigo x sucesso e laranja x azul (e nao vermelho x verde) em
    /// protanopia e deuteranopia; em tritanopia, que confunde azul e
    /// amarelo, o par e vermelho x verde com luminancias bem diferentes.
    /// </summary>
    private static readonly Dictionary<ColorVisionMode, string[]> HighlightColors = new()
    {
        [ColorVisionMode.Standard] =
            new[] { StandardWarning, StandardDanger, StandardSuccess, StandardAccent },

        [ColorVisionMode.Protanopia] =
            new[] { "#FFE14D", "#FF7A1A", "#2E7BE0", "#6C3FD1" },

        [ColorVisionMode.Deuteranopia] =
            new[] { "#FFE14D", "#FF7A1A", "#2E7BE0", "#6C3FD1" },

        [ColorVisionMode.Tritanopia] =
            new[] { "#F0E14D", "#FF8080", "#1E8A3C", "#D060FF" },
    };

    /// <summary>
    /// A cor de destaque (e de selecao) de cada modo.
    /// </summary>
    private static readonly Dictionary<ColorVisionMode, string> AccentColors = new()
    {
        [ColorVisionMode.Standard] = StandardAccent,
        [ColorVisionMode.Protanopia] = "#6C3FD1",
        [ColorVisionMode.Deuteranopia] = "#6C3FD1",
        [ColorVisionMode.Tritanopia] = "#D060FF",
    };

    /// <summary>
    /// True quando o modo pede cores diferentes das padrao.
    /// </summary>
    /// <param name="mode">O modo de visao de cores.</param>
    public static bool IsAdjusted(ColorVisionMode mode) => mode != ColorVisionMode.Standard;

    /// <summary>
    /// A cor de realce de uma chave da paleta de realce
    /// (<see cref="HighlightPalette"/>), no modo informado.
    ///
    /// Chave desconhecida devolve string vazia — quem chama decide.
    /// </summary>
    /// <param name="colorKey">A chave da cor ("warning", "danger", "success", "accent").</param>
    /// <param name="mode">O modo de visao de cores.</param>
    public static string HighlightHexOf(string? colorKey, ColorVisionMode mode)
    {
        var cores = HighlightColors.TryGetValue(mode, out var value)
            ? value
            : HighlightColors[ColorVisionMode.Standard];

        return colorKey switch
        {
            HighlightPalette.Warning => cores[0],
            HighlightPalette.Danger => cores[1],
            HighlightPalette.Success => cores[2],
            HighlightPalette.Accent => cores[3],
            _ => string.Empty,
        };
    }

    /// <summary>
    /// A paleta do tema escuro ja adaptada ao modo de visao de cores.
    ///
    /// O_parent_ integra esta paleta ao catalogo de temas: o
    /// <c>ThemePalette</c> nao e alterado aqui porque a regra do projeto e
    /// que cada tela leia a mesma cor.
    /// </summary>
    /// <param name="mode">O modo de visao de cores.</param>
    public static ThemePalette PaletteOf(ColorVisionMode mode)
    {
        var accent = AccentColors.TryGetValue(mode, out var value)
            ? value
            : StandardAccent;

        return new ThemePalette
        {
            Colors = new Dictionary<ThemeColorRole, string>
            {
                [ThemeColorRole.Background] = "#FF3C3C3C",
                [ThemeColorRole.Panel] = "#FF525252",
                [ThemeColorRole.Header] = "#FF3C3C3C",
                [ThemeColorRole.Text] = "#FFE6E6E6",
                [ThemeColorRole.MutedText] = "#FFB4B4B4",
                [ThemeColorRole.Selection] = accent,
                [ThemeColorRole.SelectionText] = "#FFFFFFFF",
                [ThemeColorRole.Accent] = accent,
                [ThemeColorRole.Border] = "#FF2E2E2E",
                [ThemeColorRole.Button] = "#FF4A4A4A",
            },
        };
    }

    /// <summary>
    /// Ajusta uma paleta existente ao modo de visao de cores.
    ///
    /// Só as cores que carregam sinal (destaque e selecao) mudam: fundo,
    /// texto e borda continuam iguais, para que o tema nao mude de forma
    /// abrupta.
    /// </summary>
    /// <param name="palette">A paleta original.</param>
    /// <param name="mode">O modo de visao de cores.</param>
    public static ThemePalette Adapt(ThemePalette? palette, ColorVisionMode mode)
    {
        if (palette is null) return PaletteOf(mode);
        if (!IsAdjusted(mode)) return palette;

        var accent = AccentColors.TryGetValue(mode, out var value)
            ? value
            : StandardAccent;

        var merged = new Dictionary<ThemeColorRole, string>(palette.Colors);

        merged[ThemeColorRole.Accent] = accent;
        merged[ThemeColorRole.Selection] = accent;

        return new ThemePalette { Colors = merged };
    }

    /// <summary>
    /// O brilho relativo de uma cor (de 0 a 1).
    ///
    /// E o que separa as cores quando o matiz nao separa: duas cores com
    /// brilhos bem diferentes continuam distinguiveis para quem nao distingue
    /// o matiz.
    /// </summary>
    /// <param name="hex">A cor em hexadecimal (#RRGGBB ou #AARRGGBB).</param>
    public static double Luminance(string? hex)
    {
        if (!ThemePalette.TryNormalizeHex(hex, out var normalized)) return 0;

        var text = normalized.TrimStart('#');
        if (text.Length == 8) text = text[2..];

        if (text.Length != 6) return 0;

        var r = Channel(text, 0);
        var g = Channel(text, 2);
        var b = Channel(text, 4);

        return (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
    }

    /// <summary>
    /// True quando as duas cores se distinguem pelo brilho (e nao so pelo
    /// matiz).
    ///
    /// E o teste que garante que o modo daltônico nao depende so da cor.
    /// </summary>
    /// <param name="hexA">A primeira cor.</param>
    /// <param name="hexB">A segunda cor.</param>
    /// <param name="minDifference">A diferenca minima de brilho exigida.</param>
    public static bool DistinguishesByLuminance(
        string? hexA, string? hexB, double minDifference = 0.08)
    {
        var a = Luminance(hexA);
        var b = Luminance(hexB);

        return Math.Abs(a - b) >= minDifference;
    }

    /// <summary>Converte um par de caracteres do hexadecimal em canal (0 a 1).</summary>
    private static double Channel(string text, int index)
    {
        if (!int.TryParse(text.Substring(index, 2), NumberStyles.HexNumber,
                CultureInfo.InvariantCulture, out var value))
            return 0;

        return value / 255.0;
    }
}
