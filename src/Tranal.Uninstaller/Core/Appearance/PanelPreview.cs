using System.Collections.Generic;
using Tranal.Uninstaller.Core.Localization;

namespace Tranal.Uninstaller.Core.Appearance;

/// <summary>
/// O que a pre-visualizacao de um painel mostra.
/// </summary>
/// <param name="ToolId">O identificador da ferramenta.</param>
/// <param name="ItemCount">Quantos itens o painel listou.</param>
/// <param name="HasCount">
/// Verdadeiro quando o painel ja carregou os itens. Enquanto for falso, a
/// pre-visualizacao mostra so o nome e a descricao — nada de numero
/// inventado.
/// </param>
public sealed record PanelPreviewInfo(
    string ToolId,
    int ItemCount,
    bool HasCount);

/// <summary>
/// A pre-visualizacao dos paineis de ferramenta.
///
/// O Uninstall Tool mostra, ao passar o mouse sobre uma ferramenta, uma
/// pre-visualizacao do conteudo do painel. Aqui a pre-visualizacao e o que o
/// painel ja carregou de verdade: quantos itens ele achou e a linha de
/// status.
///
/// <para><b>A regra fica em um lugar so.</b> Este modulo e o unico que sabe
/// o que e uma pre-visualizacao e como ela se escreve. Quem mostra (a barra
/// lateral da janela) e quem carrega (o painel) falam com este modulo — nao
/// ha uma segunda montagem do texto em cada tela.</para>
/// </summary>
public static class PanelPreview
{
    private static readonly Dictionary<string, PanelPreviewInfo> Publicados = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Trava = new();

    /// <summary>
    /// Publica o que um painel carregou.
    ///
    /// O painel publica depois de carregar os itens; enquanto isso, a
    /// pre-visualizacao mostra apenas o nome e a descricao.
    /// </summary>
    /// <param name="toolId">O identificador da ferramenta.</param>
    /// <param name="itemCount">Quantos itens foram listados.</param>
    public static void Publish(string toolId, int itemCount)
    {
        if (string.IsNullOrWhiteSpace(toolId)) return;

        var info = new PanelPreviewInfo(toolId.Trim(), itemCount, true);

        lock (Trava) Publicados[info.ToolId] = info;
    }

    /// <summary>
    /// Esquece o que foi publicado sobre um painel.
    ///
    /// A pre-visualizacao mostra o que o painel ja carregou. Quando o painel
    /// e recriado (troca de idioma, por exemplo), o numero antigo deixa de
    /// valer: e melhor nao mostrar nada do que mostrar um numero que ja
    /// nao e verdade.
    /// </summary>
    /// <param name="toolId">O identificador da ferramenta.</param>
    public static void Forget(string toolId)
    {
        if (string.IsNullOrWhiteSpace(toolId)) return;

        lock (Trava) Publicados.Remove(toolId.Trim());
    }

    /// <summary>
    /// O que foi publicado sobre um painel. Nulo quando o painel ainda nao
    /// carregou nada.
    /// </summary>
    /// <param name="toolId">O identificador da ferramenta.</param>
    public static PanelPreviewInfo? Find(string toolId)
    {
        if (string.IsNullOrWhiteSpace(toolId)) return null;

        lock (Trava)
        {
            return Publicados.TryGetValue(toolId.Trim(), out var info) ? info : null;
        }
    }

    /// <summary>
    /// O texto da pre-visualizacao de um painel.
    ///
    /// Sao tres pedacos, nesta ordem: o nome do painel, o que ele mostra e o
    /// que ele ja encontrou. O ultimo pedaco so entra quando o painel
    /// carregou de verdade.
    /// </summary>
    /// <param name="toolId">O identificador da ferramenta.</param>
    /// <param name="title">O nome do painel.</param>
    /// <param name="description">O que o painel mostra.</param>
    public static string Text(string toolId, string title, string description)
    {
        var partes = new List<string>();

        if (!string.IsNullOrWhiteSpace(title)) partes.Add(title.Trim());
        if (!string.IsNullOrWhiteSpace(description)) partes.Add(description.Trim());

        var publicado = Find(toolId);
        if (publicado is not null)
        {
            if (publicado.HasCount)
            {
                partes.Add(publicado.ItemCount == 1
                    ? Loc.Get("Preview.OneItem", publicado.ItemCount)
                    : Loc.Get("Preview.ManyItems", publicado.ItemCount));
            }
        }

        return string.Join(" ", partes);
    }

    /// <summary>
    /// As pre-visualizacoes ja carregadas, na ordem em que foram publicadas.
    /// </summary>
    public static IReadOnlyList<PanelPreviewInfo> All()
    {
        lock (Trava) return Publicados.Values.ToList();
    }

    /// <summary>
    /// Esquece tudo o que foi publicado. Usado pela suite de testes: um
    /// painel carregado por um teste nao pode aparecer na pre-visualizacao de
    /// outro.
    /// </summary>
    public static void Reset()
    {
        lock (Trava) Publicados.Clear();
    }
}
