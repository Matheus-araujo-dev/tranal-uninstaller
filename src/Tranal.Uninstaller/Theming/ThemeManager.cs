using System.Collections;
using System.Windows;
using System.Windows.Media;
using Tranal.Uninstaller.Core.Settings;

namespace Tranal.Uninstaller.Theming;

/// <summary>
/// Troca de tema em runtime (UI escura / UI classica), como no Revo Pro
/// ("Menu -&gt; Opcoes -&gt; Tema" e a escolha no primeiro lancamento).
///
/// Os dois temas ficam carregados no catalogo (App.xaml) e cada um e apenas um
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
/// </summary>
public static class ThemeManager
{
    public const string Dark = "Dark";
    public const string Light = "Light";

    /// <summary>Chave do catalogo de temas em Application.Resources.</summary>
    private const string CatalogKey = "ThemeCatalog";

    /// <summary>Chave usada para conferir, depois da troca, que o tema pegou.</summary>
    private const string ProbeKey = "SidebarBrush";

    private static string _current = Dark;

    /// <summary>Dispara depois que o tema foi aplicado.</summary>
    public static event EventHandler? ThemeChanged;

    /// <summary>Tema ativo ("Dark" ou "Light").</summary>
    public static string Current => _current;

    /// <summary>True quando o tema ativo e o escuro.</summary>
    public static bool IsDark => string.Equals(_current, Dark, StringComparison.OrdinalIgnoreCase);

    /// <summary>Nomes dos temas disponiveis.</summary>
    public static IReadOnlyList<string> Available { get; } = new[] { Dark, Light };

    /// <summary>Normaliza um nome de tema ("claro", "light", "CLARO" -&gt; "Light").</summary>
    public static string Normalize(string? name) =>
        !string.IsNullOrWhiteSpace(name)
        && (name.Trim().Equals(Light, StringComparison.OrdinalIgnoreCase)
            || name.Trim().Equals("claro", StringComparison.OrdinalIgnoreCase)
            || name.Trim().Equals("clássico", StringComparison.OrdinalIgnoreCase)
            || name.Trim().Equals("classico", StringComparison.OrdinalIgnoreCase))
            ? Light
            : Dark;

    /// <summary>Aplica o tema e persiste a preferencia.</summary>
    public static void Apply(string theme)
    {
        var normalized = Normalize(theme);
        _current = normalized;

        Swap(normalized);

        AppSettingsStore.Update(s => s.Theme = normalized);
        ThemeChanged?.Invoke(null, EventArgs.Empty);
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
