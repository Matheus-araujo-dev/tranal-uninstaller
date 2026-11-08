using System.Collections;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Tranal.Uninstaller.Core.Appearance;
using Tranal.Uninstaller.Core.Settings;

namespace Tranal.Uninstaller.Theming;

/// <summary>
/// Troca de tema em runtime (UI escura / UI classica / ardosia / alto
/// contraste), como no Revo Pro ("Menu -> Opcoes -> Tema" e a escolha no
/// primeiro lancamento).
///
/// Os temas ficam carregados no catalogo (App.xaml) e cada um e apenas um
/// conjunto de brushes. Os estilos sao compartilhados (Themes/Styles.xaml).
///
/// Por que a troca e feita assim — e nao trocando dicionarios XAML:
///
///  1) Trocar o dicionario de tema dentro de um dicionario intermediario
///     troca o conteudo dos recursos, mas a interface NAO se repinta: a
///     notificacao de "os recursos mudaram" nao sobe do nivel aninhado ate os
///     DynamicResource da janela. O sintoma medido era o settings.json
///     confirmar a troca e a tela continuar com as cores do tema anterior.
///
///  2) Carregar um dicionario XAML por pack URI em runtime e instavel nesta
///     maquina: a primeira carga funciona, a seguinte do mesmo arquivo falha
///     com "Nao e possivel localizar o recurso 'themes/dark'". Carregando os
///     dois temas uma unica vez, por XAML (caminho relativo, na inicializacao),
///     esse problema nao existe.
///
/// A troca final e a mais direta do WPF: escrever as brushes em
/// Application.Resources. Isso notifica a interface e todos os DynamicResource
/// sao reavaliados, sem recarregar a janela.
///
/// O **catalogo** de temas (quantos existem, o nome de cada um) vive no
/// nucleo (`Core/Appearance/ThemePalette.cs`), e nao aqui: e a opcoes de tema,
/// o painel de configuracao e a linha de comando que precisam da mesma
/// lista. Aqui mora so a parte que e WPF — a escrita das brushes.
/// </summary>
public static class ThemeManager
{
    public const string Dark = "Dark";
    public const string Light = "Light";
    public const string Slate = "Slate";
    public const string Contrast = "Contrast";

    /// <summary>Chave do catalogo de temas em Application.Resources.</summary>
    private const string CatalogKey = "ThemeCatalog";

    /// <summary>Chave usada para conferir, depois da troca, que o tema pegou.</summary>
    private const string ProbeKey = "SidebarBrush";

    /// <summary>
    /// De qual brush do XAML cada papel de cor do usuario se sobrepoe.
    ///
    /// E o mapa que faz a cor personalizada chegar na tela: o painel de
    /// configuracao fala em "fundo", "texto", "selecao" — e o XAML conhece
    /// "SidebarBrush", "TextBrush", "SelectionBrush". O mapa mora aqui, e nao
    /// espalhado pelo painel, para que o papel e a brush nunca se separem.
    /// </summary>
    private static readonly IReadOnlyDictionary<ThemeColorRole, string> RoleBrushes =
        new Dictionary<ThemeColorRole, string>
        {
            [ThemeColorRole.Background] = "SidebarBrush",
            [ThemeColorRole.Panel] = "PanelBrush",
            [ThemeColorRole.Header] = "HeaderBrush",
            [ThemeColorRole.Text] = "TextBrush",
            [ThemeColorRole.MutedText] = "TextDimBrush",
            [ThemeColorRole.Selection] = "SelectionBrush",
            [ThemeColorRole.SelectionText] = "SelectionTextBrush",
            [ThemeColorRole.Accent] = "AccentBrush",
            [ThemeColorRole.Border] = "BorderBrush",
            [ThemeColorRole.Button] = "ButtonFaceBrush",
        };

    private static string _current = Dark;

    /// <summary>Dispara depois que o tema foi aplicado.</summary>
    public static event EventHandler? ThemeChanged;

    /// <summary>Tema ativo ("Dark", "Slate", "Contrast" ou "Light").</summary>
    public static string Current => _current;

    /// <summary>True quando o tema ativo e escuro.</summary>
    public static bool IsDark => ThemeSkinCatalog.Get(_current)?.IsDark ?? true;

    /// <summary>Nomes dos temas disponiveis.</summary>
    public static IReadOnlyList<string> Available { get; } =
        ThemeSkinCatalog.All.Select(s => s.Id).ToList();

    /// <summary>Normaliza um nome de tema ("claro", "light", "CLARO" -> "Light").</summary>
    public static string Normalize(string? name) => ThemeSkinCatalog.Normalize(name);

    /// <summary>Aplica o tema e persiste a preferencia.</summary>
    public static void Apply(string theme)
    {
        var normalized = Normalize(theme);
        _current = normalized;

        Swap(normalized);
        ApplyCustomColors();
        ApplyColorVisionPalette();

        AppSettingsStore.Update(s => s.Theme = normalized);
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// O modo de visao de cores gravado nas preferencias ("Standard",
    /// "Protanopia", "Deuteranopia" ou "Tritanopia").
    /// </summary>
    public static ColorVisionMode ColorVision =>
        ColorVisionModeOf(AppSettingsStore.Current.ColorVisionMode);

    /// <summary>
    /// Converte o nome gravado nas preferencias no modo do nucleo.
    ///
    /// Um nome desconhecido (ou uma preferencia corrompida) vira o modo
    /// padrao: a tela nunca fica sem cor por causa de um texto invalido.
    /// </summary>
    /// <param name="name">O nome gravado nas preferencias.</param>
    public static ColorVisionMode ColorVisionModeOf(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return ColorVisionMode.Standard;

        return Enum.TryParse(name.Trim(), ignoreCase: true, out ColorVisionMode mode)
            ? mode
            : ColorVisionMode.Standard;
    }

    /// <summary>
    /// Reaplica o tema com a paleta adaptada ao modo de visao de cores.
    ///
    /// O tema e as cores do usuario sao recolocados antes da adaptacao:
    /// sem isso, voltar para o modo padrao deixaria na tela a cor de
    /// destaque adaptada.
    /// </summary>
    public static void ApplyColorVisionMode()
    {
        Swap(_current);
        ApplyCustomColors();
        ApplyColorVisionPalette();
    }

    /// <summary>
    /// Aplica por cima do tema a paleta adaptada ao modo de visao de cores.
    ///
    /// A regra da adaptacao mora no nucleo
    /// (<c>Core/Appearance/ColorBlindPalette.cs</c>): aqui so se escreve a
    /// cor adaptada nas brushes que o tema usa.
    /// </summary>
    private static void ApplyColorVisionPalette()
    {
        var app = Application.Current;
        if (app is null) return;

        var mode = ColorVision;
        if (!ColorBlindPalette.IsAdjusted(mode)) return;

        var palette = ColorBlindPalette.Adapt(ThemePalettes.Of(_current), mode);

        // So as cores que carregam sinal mudam: fundo, texto e borda
        // continuam iguais, para que o tema nao mude de forma abrupta.
        foreach (var role in new[] { ThemeColorRole.Accent, ThemeColorRole.Selection })
        {
            if (!RoleBrushes.TryGetValue(role, out var brushKey)) continue;
            if (!ThemePalettes.TryParseColor(palette.ColorOf(role), out var argb)) continue;

            app.Resources[brushKey] = new SolidColorBrush(Color.FromRgb(
                (byte)((argb >> 16) & 0xFF),
                (byte)((argb >> 8) & 0xFF),
                (byte)(argb & 0xFF)));
        }
    }

    /// <summary>Alterna entre os dois temas.</summary>
    public static void Toggle() => Apply(IsDark ? Light : Dark);

    /// <summary>
    /// Aplica o tema salvo nas preferencias. Chamado na inicializacao da aplicacao,
    /// antes de a janela principal ser construida.
    /// </summary>
    public static void ApplyStored()
    {
        var stored = Normalize(AppSettingsStore.Current.Theme);
        _current = stored;

        Swap(stored);
        ApplyCustomColors();
        ApplyColorVisionPalette();
    }

    /// <summary>
    /// Escreve as brushes do tema em Application.Resources.
    /// A interface inteira reavalia os DynamicResource em seguida.
    /// </summary>
    private static void Swap(string theme)
    {
        var app = Application.Current;
        if (app is null) return;

        var source = FindTheme(app, theme);
        if (source is null) return;

        foreach (DictionaryEntry entry in source)
        {
            if (entry.Value is not SolidColorBrush brush) continue;
            app.Resources[entry.Key] = brush;
        }

        Verify(theme);
    }

    /// <summary>
    /// Aplica as cores personalizadas do usuario por cima do tema.
    ///
    /// A cor personalizada **ganha** do tema: e o que o usuario pediu. Uma
    /// cor invalida no arquivo de preferencias e ignorada — o tema continua
    /// no lugar, em vez de a tela ficar sem cor nenhuma.
    /// </summary>
    public static void ApplyCustomColors()
    {
        var app = Application.Current;
        if (app is null) return;

        var custom = AppSettingsStore.Current.CustomThemeColors;
        if (custom is null || custom.Count == 0) return;

        foreach (var (role, hex) in custom)
        {
            if (!ThemePalette.TryParseRole(role, out var parsed)) continue;
            if (!RoleBrushes.TryGetValue(parsed, out var brushKey)) continue;
            if (!ThemePalettes.TryParseColor(hex, out var argb)) continue;

            app.Resources[brushKey] = new SolidColorBrush(Color.FromRgb(
                (byte)((argb >> 16) & 0xFF),
                (byte)((argb >> 8) & 0xFF),
                (byte)(argb & 0xFF)));
        }
    }

    /// <summary>Brushes do tema pedido, vindas do catalogo de App.xaml.</summary>
    private static ResourceDictionary? FindTheme(Application app, string theme)
    {
        if (app.Resources[CatalogKey] is not ResourceDictionary catalog) return null;
        return catalog[theme] as ResourceDictionary;
    }

    /// <summary>
    /// Confere que a troca realmente pegou. Sem esta checagem, uma falha
    /// passava em silencio: o app gravava "Light" no settings.json e continuava
    /// na tela com as cores do tema anterior.
    /// </summary>
    private static void Verify(string theme)
    {
        var app = Application.Current;
        if (app is null) return;

        if (app.Resources[ProbeKey] is not SolidColorBrush)
            throw new InvalidOperationException(
                $"Troca de tema para '{theme}' falhou: a chave '{ProbeKey}' nao foi resolvida.");
    }
}
