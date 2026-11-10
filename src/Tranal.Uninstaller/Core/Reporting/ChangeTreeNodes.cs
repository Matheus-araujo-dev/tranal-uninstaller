using System.Collections.Generic;
using System.Linq;
using Tranal.Uninstaller.Core.Tracing;

namespace Tranal.Uninstaller.Core.Reporting;

/// <summary>
/// Um no da arvore de alteracoes.
///
/// O no e uma categoria (arquivos, pastas, registro, valores), um grupo de
/// mudanca (novo, modificado, removido) ou a propria alteracao.
/// </summary>
public sealed class ChangeTreeNode
{
    /// <summary>A categoria do no, quando ele e de categoria ou de grupo.</summary>
    public ChangeTreeCategory? Category { get; init; }

    /// <summary>A mudanca do no, quando ele e de grupo.</summary>
    public ChangeTreeChange? Change { get; init; }

    /// <summary>
    /// O caminho, a chave ou o valor — quando o no e a propria alteracao.
    /// </summary>
    public string Item { get; init; } = string.Empty;
    /// <summary>
    /// A chave do rotulo do no, quando o no e de categoria ou de grupo
    /// ("files", "added"...). Vazio quando o no e a propria alteracao.
    ///
    /// E o rotulo **estruturado**: quem mostra o no decide o texto (a tela
    /// traduz pelo catalogo, a linha de comando escreve em portugues), mas
    /// a estrutura — qual categoria, qual mudanca — mora aqui, em um lugar
    /// so.
    /// </summary>
    public string LabelKey { get; init; } = string.Empty;

    /// <summary>O detalhe, quando o no e a propria alteracao.</summary>
    public string Detail { get; init; } = string.Empty;

    /// <summary>Os filhos do no.</summary>
    public IReadOnlyList<ChangeTreeNode> Children { get; init; } = System.Array.Empty<ChangeTreeNode>();

    /// <summary>True quando o no e a propria alteracao.</summary>
    public bool IsLeaf => Children.Count == 0;

    /// <summary>
    /// Quantas alteracoes o no contem.
    ///
    /// Uma categoria conta os seus grupos, um grupo conta os seus itens, e um
    /// item conta um. E o numero que o cabecalho do no mostra.
    /// </summary>
    public int Count => IsLeaf ? 1 : Children.Sum(c => c.Count);
}

/// <summary>
/// A arvore de alteracoes, em nos, e a busca dentro dela.
///
/// A comparacao ja e feita pelo nucleo (<see cref="SnapshotComparer"/>) e a
/// classificacao de cada alteracao e de <see cref="ChangeTreeReport"/>: este
/// modulo nao compara nem classifica nada. Ele **agrupa** o que ja existe,
/// em arvore, e filtra a arvore por um texto.
///
/// E o que o Total Uninstall mostra: as alteracoes detectadas em arvore
/// (arquivos, pastas, chaves e valores de registro) e a busca dentro delas.
/// </summary>
public static class ChangeTreeNodes
{
    /// <summary>
    /// As categorias, na ordem em que aparecem na arvore.
    /// </summary>
    public static IReadOnlyList<ChangeTreeCategory> Categories { get; } = new[]
    {
        ChangeTreeCategory.Files,
        ChangeTreeCategory.Folders,
        ChangeTreeCategory.Registry,
        ChangeTreeCategory.Values,
    };

    /// <summary>
    /// Monta a arvore das alteracoes.
    ///
    /// A ordem e fixa: as categorias nesta ordem, e dentro delas os grupos
    /// de mudanca (novo, modificado, removido). Uma arvore que troca a ordem
    /// a cada comparacao treina o olho do usuario a desconfiar dela.
    /// </summary>
    /// <param name="comparison">A comparacao entre os dois snapshots.</param>
    public static IReadOnlyList<ChangeTreeNode> Build(SnapshotComparison? comparison)
    {
        var itens = ChangeTreeReport.Items(comparison);
        var nos = new List<ChangeTreeNode>();

        foreach (var categoria in Categories)
        {
            var daCategoria = itens.Where(i => i.Category == categoria).ToList();
            if (daCategoria.Count == 0) continue;

            var grupos = new List<ChangeTreeNode>();
            foreach (var mudanca in new[]
            {
                ChangeTreeChange.Added,
                ChangeTreeChange.Modified,
                ChangeTreeChange.Removed,
            })
            {
                var doGrupo = daCategoria.Where(i => i.Change == mudanca).ToList();
                if (doGrupo.Count == 0) continue;

                grupos.Add(new ChangeTreeNode
                {
                    Category = categoria,
                    Change = mudanca,
                    LabelKey = ChaveDaMudanca(mudanca),
                    Children = doGrupo.Select(Folha).ToList(),
                });
            }

            nos.Add(new ChangeTreeNode
            {
                Category = categoria,
                LabelKey = ChaveDaCategoria(categoria),
                Children = grupos,
            });
        }

        return nos;
    }

    /// <summary>
    /// Filtra a arvore pelo texto digitado.
    ///
    /// A hierarquia e preservada: um no so continua na arvore quando ele tem
    /// algum filho que casa, ou quando ele proprio casa. Uma busca que
    /// achasse o item e escondesse o caminho dele nao ajudaria ninguem a
    /// entender onde a coisa esta.
    /// </summary>
    /// <param name="nodes">Os nos.</param>
    /// <param name="busca">O texto digitado.</param>
    public static IReadOnlyList<ChangeTreeNode> Filter(IEnumerable<ChangeTreeNode>? nodes, string? busca)
    {
        var lista = nodes?.Where(n => n is not null).ToList() ?? new List<ChangeTreeNode>();
        if (string.IsNullOrWhiteSpace(busca)) return lista;

        var termo = busca.Trim();
        var resultado = new List<ChangeTreeNode>();

        foreach (var no in lista)
        {
            var filhos = Filter(no.Children, termo);
            if (filhos.Count > 0)
            {
                resultado.Add(new ChangeTreeNode
                {
                    Category = no.Category,
                    Change = no.Change,
                    Item = no.Item,
                    Detail = no.Detail,
                    Children = filhos,
                });
            }
            else if (Casa(no, termo))
            {
                resultado.Add(no);
            }
        }

        return resultado;
    }

    /// <summary>
    /// Quantas alteracoes a arvore tem, no total.
    /// </summary>
    /// <param name="nodes">Os nos.</param>
    public static int Total(IEnumerable<ChangeTreeNode>? nodes) =>
        nodes?.Where(n => n is not null).Sum(n => n.Count) ?? 0;

    /// <summary>
    /// A chave do rotulo de uma categoria.
    /// </summary>
    /// <param name="category">A categoria.</param>
    public static string ChaveDaCategoria(ChangeTreeCategory category) => category switch
    {
        ChangeTreeCategory.Files => "files",
        ChangeTreeCategory.Folders => "folders",
        ChangeTreeCategory.Registry => "registry",
        ChangeTreeCategory.Values => "values",
        _ => "files",
    };

    /// <summary>
    /// A chave do rotulo de uma mudanca.
    /// </summary>
    /// <param name="change">A mudanca.</param>
    public static string ChaveDaMudanca(ChangeTreeChange change) => change switch
    {
        ChangeTreeChange.Added => "added",
        ChangeTreeChange.Modified => "modified",
        ChangeTreeChange.Removed => "removed",
        _ => "added",
    };

    /// <summary>
    /// A arvore em texto, com a hierarquia.
    ///
    /// O texto de cada no e decidido por quem chama (a tela traduz pelo
    /// catalogo, a linha de comando escreve em portugues): a hierarquia e a
    /// contagem moram aqui.
    /// </summary>
    /// <param name="nodes">Os nos.</param>
    /// <param name="titulo">O titulo de um no.</param>
    public static string Describe(IEnumerable<ChangeTreeNode>? nodes, Func<ChangeTreeNode, string> titulo)
    {
        var sb = new System.Text.StringBuilder();
        Descreva(sb, nodes, titulo, 0);
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Escreve os nos, com a indentacao da hierarquia.
    /// </summary>
    /// <param name="sb">Onde escrever.</param>
    /// <param name="nodes">Os nos.</param>
    /// <param name="titulo">O titulo de um no.</param>
    /// <param name="nivel">O nivel da hierarquia.</param>
    private static void Descreva(
        System.Text.StringBuilder sb,
        IEnumerable<ChangeTreeNode>? nodes,
        Func<ChangeTreeNode, string> titulo,
        int nivel)
    {
        if (nodes is null) return;

        foreach (var no in nodes)
        {
            sb.AppendLine(new string(' ', nivel * 2) + titulo(no));
            Descreva(sb, no.Children, titulo, nivel + 1);
        }
    }

    /// <summary>
    /// True quando o no casa com o texto da busca.
    /// </summary>
    /// <param name="no">O no.</param>
    /// <param name="termo">O texto da busca.</param>
    private static bool Casa(ChangeTreeNode no, string termo)
    {
        if (no.IsLeaf)
        {
            return Contem(no.Item, termo) || Contem(no.Detail, termo);
        }

        return Contem(no.Item, termo);
    }

    /// <summary>True quando o texto contem o termo.</summary>
    /// <param name="texto">O texto.</param>
    /// <param name="termo">O termo procurado.</param>
    private static bool Contem(string? texto, string termo) =>
        !string.IsNullOrEmpty(texto)
        && texto.IndexOf(termo, System.StringComparison.OrdinalIgnoreCase) >= 0;

    /// <summary>
    /// O no de uma alteracao (a folha da arvore).
    /// </summary>
    /// <param name="item">A alteracao.</param>
    private static ChangeTreeNode Folha(ChangeTreeItem item) => new()
    {
        Category = item.Category,
        Change = item.Change,
        Item = item.Item,
        Detail = item.Detail,
    };
}
