using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Settings;

namespace Tranal.Uninstaller.Core.Appearance;

/// <summary>
/// O painel ancorado: a ferramenta que fica fixa na lateral da janela.
///
/// O Uninstall Tool deixa o usuario fixar paineis: o painel fica ancorado e
/// nao some quando o usuario troca de tela. Aqui a mesma coisa, com uma
/// diferenca: ancorar um painel nao muda o que a janela principal mostra — o
/// painel ancorado vive na coluna da direita, ao lado do conteudo.
///
/// <para><b>A regra fica em um lugar so.</b> Este modulo e o unico que sabe
/// qual painel esta ancorado. A janela (que mostra), a linha de comando (que
/// informa) e as preferencias (que guardam) leem este mesmo modulo.</para>
/// </summary>
public static class DockedPanel
{
    /// <summary>
    /// O identificador da ferramenta ancorada. Vazio quando nenhum painel esta
    /// ancorado.
    /// </summary>
    public static string ToolId => (AppSettingsStore.Current.DockedToolId ?? string.Empty).Trim();

    /// <summary>Existe um painel ancorado?</summary>
    public static bool HasPanel => ToolId.Length > 0;

    /// <summary>
    /// Ancora um painel — ou solta, quando e o mesmo que ja estava ancorado.
    ///
    /// O painel ancorado e o da ferramenta informada; um painel de modulo
    /// (a lista de programas) nao pode ser ancorado.
    /// </summary>
    /// <param name="toolId">O identificador da ferramenta.</param>
    /// <returns>Verdadeiro quando ha painel ancorado depois da chamada.</returns>
    public static bool Toggle(string toolId)
    {
        var id = (toolId ?? string.Empty).Trim();
        if (id.Length == 0) return HasPanel;

        // So uma ferramenta pode ser ancorada: a lista de programas e a tela
        // principal nao podem virar um painel lateral.
        if (!ToolCatalog.All.Contains(id, StringComparer.OrdinalIgnoreCase)) return HasPanel;

        var settings = AppSettingsStore.Current;
        var novo = string.Equals(ToolId, id, StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : id;

        settings.DockedToolId = novo;
        AppSettingsStore.Save(settings);

        return novo.Length > 0;
    }

    /// <summary>
    /// Solta o painel ancorado.
    /// </summary>
    public static void Undock()
    {
        var settings = AppSettingsStore.Current;
        if (string.IsNullOrEmpty(settings.DockedToolId)) return;

        settings.DockedToolId = string.Empty;
        AppSettingsStore.Save(settings);
    }

    /// <summary>
    /// A ferramenta informada e o painel ancorado?
    /// </summary>
    /// <param name="toolId">O identificador da ferramenta.</param>
    public static bool IsDocked(string toolId)
    {
        var id = (toolId ?? string.Empty).Trim();
        return id.Length > 0 && string.Equals(ToolId, id, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// O que esta ancorado agora, em uma frase.
    /// </summary>
    public static string Describe()
    {
        if (!HasPanel) return Loc.Get("Panel.NoneDocked");

        return Loc.Get("Panel.Docked", Loc.Get($"Module.{ToolId}"));
    }
}
