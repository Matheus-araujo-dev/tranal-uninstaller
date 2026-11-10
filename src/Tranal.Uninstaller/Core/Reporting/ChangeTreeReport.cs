using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Tranal.Uninstaller.Core.Tracing;

namespace Tranal.Uninstaller.Core.Reporting;

/// <summary>
/// O formato em que a arvore de alteracoes e exportada.
/// </summary>
public enum ChangeTreeFormat
{
    /// <summary>Texto simples, para ler no Bloco de Notas.</summary>
    Text,

    /// <summary>CSV, para abrir em planilha.</summary>
    Csv,

    /// <summary>HTML, para abrir no navegador.</summary>
    Html,
}

/// <summary>
/// A arvore de alteracoes: o que mudou entre dois snapshots.
///
/// A comparacao ja e feita pelo nucleo (<see cref="SnapshotComparer"/>): este
/// modulo nao compara nada. Ele transforma a comparacao em uma **tabela**
/// (tipo, mudanca, item, detalhe), e essa tabela e a que sai impressa e a
/// que sai exportada.
///
/// A tabela e a mesma nos tres formatos e na impressao: quem exporta e quem
/// imprime consomem as mesmas linhas. E o layout de impressao e o mesmo
/// (<see cref="PrintTable"/>) que a lista de programas usa.
/// </summary>
public static class ChangeTreeReport
{
    /// <summary>O cabecalho da coluna do tipo do item.</summary>
    public const string ColumnKind = "Tipo";

    /// <summary>O cabecalho da coluna da mudanca.</summary>
    public const string ColumnChange = "Mudanca";

    /// <summary>O cabecalho da coluna do item (o caminho, a chave, o valor).</summary>
    public const string ColumnItem = "Item";

    /// <summary>O cabecalho da coluna do detalhe.</summary>
    public const string ColumnDetail = "Detalhe";

    /// <summary>As colunas da arvore, na ordem.</summary>
    public static IReadOnlyList<string> Headers { get; } = new[]
    {
        ColumnKind,
        ColumnChange,
        ColumnItem,
        ColumnDetail,
    };

    /// <summary>
    /// As linhas da arvore de alteracoes.
    ///
    /// A ordem e a mesma em toda execucao: primeiro o que foi criado, depois
    /// o que foi alterado, depois o que sumiu. Dentro de cada grupo, a ordem
    /// alfabetica. Uma arvore que troca a ordem a cada comparacao
    /// treina o olho do usuario a desconfiar dela.
    /// </summary>
    /// <param name="comparison">A comparacao entre os dois snapshots.</param>
    public static IReadOnlyList<IReadOnlyList<string>> Rows(SnapshotComparison? comparison)
    {
        var linhas = new List<IReadOnlyList<string>>();
        foreach (var item in Items(comparison))
            linhas.Add(Linha(item.Kind, item.ChangeText, item.Item, item.Detail));

        return linhas;
    }

    /// <summary>
    /// As alteracoes, de forma estruturada.
    ///
    /// E a fonte unica da classificacao: a tabela (exportacao e impressao) e a
    /// arvore (a tela) sao **as mesmas** alteracoes em duas formas. Sem isso, a
    /// arvore teria a sua propria classificacao, e as duas poderiam divergir
    /// — e o que a pessoa le na tela deixaria de ser o que sai no arquivo.
    /// </summary>
    /// <param name="comparison">A comparacao entre os dois snapshots.</param>
    public static IReadOnlyList<ChangeTreeItem> Items(SnapshotComparison? comparison)
    {
        var itens = new List<ChangeTreeItem>();
        if (comparison is null) return itens;

        foreach (var arquivo in comparison.NewFiles)
            itens.Add(Item(ChangeTreeCategory.Files, ChangeTreeChange.Added, "Arquivo", "Novo", arquivo.Path, Tamanho(arquivo.SizeBytes)));

        foreach (var pasta in comparison.NewFolders)
            itens.Add(Item(ChangeTreeCategory.Folders, ChangeTreeChange.Added, "Pasta", "Nova", pasta.Path, string.Empty));

        foreach (var chave in comparison.NewRegistryKeys)
            itens.Add(Item(ChangeTreeCategory.Registry, ChangeTreeChange.Added, "Registro", "Nova chave", chave, string.Empty));

        foreach (var valor in comparison.NewRegistryValues)
            itens.Add(Item(ChangeTreeCategory.Values, ChangeTreeChange.Added, "Valor", "Novo", ItemDoValor(valor), valor.Value));

        foreach (var arquivo in comparison.ModifiedFiles)
            itens.Add(Item(ChangeTreeCategory.Files, ChangeTreeChange.Modified, "Arquivo", "Modificado", arquivo.Path, Tamanho(arquivo.SizeBytes)));

        foreach (var mudanca in comparison.ModifiedRegistryValues)
            itens.Add(Item(ChangeTreeCategory.Values, ChangeTreeChange.Modified, "Valor", "Modificado", ItemDoValor(mudanca),
                (mudanca.OldValue ?? string.Empty) + " -> " + (mudanca.NewValue ?? string.Empty)));

        foreach (var arquivo in comparison.RemovedFiles)
            itens.Add(Item(ChangeTreeCategory.Files, ChangeTreeChange.Removed, "Arquivo", "Removido", arquivo.Path, Tamanho(arquivo.SizeBytes)));

        foreach (var pasta in comparison.RemovedFolders)
            itens.Add(Item(ChangeTreeCategory.Folders, ChangeTreeChange.Removed, "Pasta", "Removida", pasta.Path, string.Empty));

        foreach (var chave in comparison.RemovedRegistryKeys)
            itens.Add(Item(ChangeTreeCategory.Registry, ChangeTreeChange.Removed, "Registro", "Chave removida", chave, string.Empty));

        foreach (var valor in comparison.RemovedRegistryValues)
            itens.Add(Item(ChangeTreeCategory.Values, ChangeTreeChange.Removed, "Valor", "Removido", ItemDoValor(valor), valor.Value));

        return itens;
    }

    /// <summary>
    /// A tabela da arvore, pronta para imprimir.
    /// </summary>
    /// <param name="comparison">A comparacao entre os dois snapshots.</param>
    /// <param name="antes">O nome do snapshot de antes.</param>
    /// <param name="depois">O nome do snapshot de depois.</param>
    /// <param name="rowsPerPage">Quantas linhas cabem numa pagina.</param>
    public static PrintTable Table(
        SnapshotComparison? comparison,
        string antes,
        string depois,
        int rowsPerPage = PrintTable.DefaultRowsPerPage)
    {
        var linhas = Rows(comparison);
        var cabecalho = string.Format(
            CultureInfo.CurrentCulture,
            "Tranal Uninstaller Pro — {0} alteracao(oes) — antes: {1} | depois: {2} — {3}",
            linhas.Count,
            antes,
            depois,
            DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture));

        return PrintTable.Build("Arvore de alteracoes", cabecalho, Headers, linhas, rowsPerPage);
    }

    /// <summary>
    /// A arvore de alteracoes em texto simples.
    /// </summary>
    /// <param name="comparison">A comparacao entre os dois snapshots.</param>
    /// <param name="antes">O nome do snapshot de antes.</param>
    /// <param name="depois">O nome do snapshot de depois.</param>
    public static string ToText(SnapshotComparison? comparison, string antes, string depois)
    {
        var linhas = Rows(comparison);
        var sb = new StringBuilder();

        sb.AppendLine("Arvore de alteracoes");
        sb.AppendLine("====================");
        sb.AppendLine();
        sb.AppendLine("Antes:  " + antes);
        sb.AppendLine("Depois: " + depois);
        sb.AppendLine("Alteracoes: " + linhas.Count);
        sb.AppendLine();

        foreach (var linha in linhas)
        {
            sb.AppendLine(string.Join(" | ", linha));
        }

        return sb.ToString();
    }

    /// <summary>
    /// A arvore de alteracoes em CSV.
    ///
    /// O CSV tem uma linha por alteracao, com o tipo do item, a mudanca, o
    /// item e o detalhe. Os snapshots comparados vem na primeira linha, para
    /// que o arquivo aberto sozinho ainda diga de onde veio.
    /// </summary>
    /// <param name="comparison">A comparacao entre os dois snapshots.</param>
    /// <param name="antes">O nome do snapshot de antes.</param>
    /// <param name="depois">O nome do snapshot de depois.</param>
    public static string ToCsv(SnapshotComparison? comparison, string antes, string depois)
    {
        var sb = new StringBuilder();

        sb.AppendLine(Csv(ColumnKind) + ";" + Csv(ColumnChange) + ";" + Csv(ColumnItem) + ";" + Csv(ColumnDetail));
        sb.AppendLine(Csv("Snapshot antes") + ";" + Csv(antes) + ";;");

        foreach (var linha in Rows(comparison))
        {
            sb.AppendLine(string.Join(";", new[]
            {
                Csv(linha[0]),
                Csv(linha[1]),
                Csv(linha[2]),
                Csv(linha[3]),
            }));
        }

        return sb.ToString();
    }

    /// <summary>
    /// A arvore de alteracoes em HTML.
    /// </summary>
    /// <param name="comparison">A comparacao entre os dois snapshots.</param>
    /// <param name="antes">O nome do snapshot de antes.</param>
    /// <param name="depois">O nome do snapshot de depois.</param>
    public static string ToHtml(SnapshotComparison? comparison, string antes, string depois)
    {
        var linhas = Rows(comparison);
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"pt-BR\"><head><meta charset=\"utf-8\">");
        sb.AppendLine("<title>Tranal Uninstaller - Arvore de alteracoes</title>");
        sb.AppendLine("<style>body{font-family:Segoe UI,Arial;background:#525252;color:#e6e6e6}");
        sb.AppendLine("table{border-collapse:collapse;width:100%;margin-bottom:24px}th,td{border:1px solid #2e2e2e;padding:4px 8px;text-align:left}");
        sb.AppendLine("th{background:#3c3c3c}tr:nth-child(even){background:#5a5a5a}h2{margin:18px 0 6px}</style>");
        sb.AppendLine("</head><body>");
        sb.AppendLine("<h1>Arvore de alteracoes</h1>");
        sb.AppendLine("<table><tr><th>Campo</th><th>Valor</th></tr>");
        sb.AppendLine("<tr><td>Antes</td><td>" + Html(antes) + "</td></tr>");
        sb.AppendLine("<tr><td>Depois</td><td>" + Html(depois) + "</td></tr>");
        sb.AppendLine("<tr><td>Alteracoes</td><td>" + linhas.Count + "</td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine("<table><tr><th>" + Html(ColumnKind) + "</th><th>" + Html(ColumnChange)
            + "</th><th>" + Html(ColumnItem) + "</th><th>" + Html(ColumnDetail) + "</th></tr>");

        foreach (var linha in linhas)
        {
            sb.AppendLine("<tr><td>" + Html(linha[0]) + "</td><td>" + Html(linha[1])
                + "</td><td>" + Html(linha[2]) + "</td><td>" + Html(linha[3]) + "</td></tr>");
        }

        sb.AppendLine("</table>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    /// <summary>
    /// Exporta a arvore no formato pedido e grava no destino.
    /// </summary>
    /// <param name="comparison">A comparacao entre os dois snapshots.</param>
    /// <param name="antes">O nome do snapshot de antes.</param>
    /// <param name="depois">O nome do snapshot de depois.</param>
    /// <param name="path">O caminho do arquivo a gravar.</param>
    /// <param name="format">O formato.</param>
    /// <returns>O caminho gravado, ou string vazia quando falhou.</returns>
    public static string Write(
        SnapshotComparison? comparison,
        string antes,
        string depois,
        string path,
        ChangeTreeFormat format)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;

            var conteudo = format switch
            {
                ChangeTreeFormat.Csv => ToCsv(comparison, antes, depois),
                ChangeTreeFormat.Html => ToHtml(comparison, antes, depois),
                _ => ToText(comparison, antes, depois),
            };

            File.WriteAllText(path, conteudo, new UTF8Encoding(false));
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Monta um item de alteracao.
    /// </summary>
    /// <param name="category">A categoria.</param>
    /// <param name="change">A mudanca.</param>
    /// <param name="kind">O tipo, como a tabela mostra.</param>
    /// <param name="changeText">O texto da mudanca, como a tabela mostra.</param>
    /// <param name="item">O caminho, a chave ou o valor.</param>
    /// <param name="detail">O detalhe.</param>
    private static ChangeTreeItem Item(
        ChangeTreeCategory category,
        ChangeTreeChange change,
        string kind,
        string changeText,
        string item,
        string detail) => new()
    {
        Category = category,
        Change = change,
        Kind = kind,
        ChangeText = changeText,
        Item = item,
        Detail = detail,
    };

    /// <summary>Uma linha da arvore.</summary>
    /// <param name="tipo">O tipo do item.</param>
    /// <param name="mudanca">A mudanca.</param>
    /// <param name="item">O item.</param>
    /// <param name="detalhe">O detalhe.</param>
    private static IReadOnlyList<string> Linha(string tipo, string mudanca, string item, string detalhe) =>
        new[] { tipo, mudanca, item, detalhe };

    /// <summary>
    /// O item de um valor de registro: a chave e o nome do valor, juntos.
    /// </summary>
    /// <param name="valor">O valor.</param>
    private static string ItemDoValor(SnapshotRegistryValueState valor)
    {
        var chave = valor?.Key ?? string.Empty;
        var nome = valor?.ValueName ?? string.Empty;
        if (nome.Length == 0) return chave;
        if (chave.Length == 0) return nome;
        return chave + "\\" + nome;
    }

    /// <summary>
    /// O item de um valor de registro que mudou: a chave e o nome do valor.
    /// </summary>
    /// <param name="mudanca">A mudanca do valor.</param>
    private static string ItemDoValor(RegistryValueChange mudanca)
    {
        var chave = mudanca?.Key ?? string.Empty;
        var nome = mudanca?.ValueName ?? string.Empty;
        if (nome.Length == 0) return chave;
        if (chave.Length == 0) return nome;
        return chave + "\\" + nome;
    }

    /// <summary>O tamanho de um arquivo, em texto legivel.</summary>
    /// <param name="bytes">O tamanho, em bytes.</param>
    private static string Tamanho(long bytes)
    {
        string[] unidades = { "B", "KB", "MB", "GB", "TB" };
        double valor = bytes;
        var indice = 0;

        while (valor >= 1024 && indice < unidades.Length - 1)
        {
            valor /= 1024;
            indice++;
        }

        return string.Format(CultureInfo.CurrentCulture, "{0:0.#} {1}", valor, unidades[indice]);
    }

    /// <summary>Um valor pronto para o CSV (o separador e o ponto e virgula).</summary>
    /// <param name="valor">O valor.</param>
    private static string Csv(string? valor)
    {
        var texto = valor ?? string.Empty;
        if (texto.Contains(';') || texto.Contains('"') || texto.Contains('\n') || texto.Contains('\r'))
        {
            return "\"" + texto.Replace("\"", "\"\"") + "\"";
        }

        return texto;
    }

    /// <summary>Um valor pronto para o HTML.</summary>
    /// <param name="valor">O valor.</param>
    private static string Html(string? valor)
    {
        var texto = valor ?? string.Empty;
        return texto
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }
}
