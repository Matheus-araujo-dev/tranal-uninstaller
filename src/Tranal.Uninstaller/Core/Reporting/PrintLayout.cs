using System.Collections.Generic;
using System.Globalization;

namespace Tranal.Uninstaller.Core.Reporting;

/// <summary>
/// O layout de impressao da lista de programas.
///
/// A lista e as colunas sao as mesmas do exportador
/// (<see cref="Exporters.DefaultColumns"/> e <see cref="ProgramColumns"/>):
/// a impressao nao tem uma segunda lista de colunas.
///
/// A regra de como uma tabela vira folha mora no <see cref="PrintTable"/>:
/// este modulo so diz **o que** e impresso (a lista de programas), e nao
/// como a pagina se quebra. A arvore de alteracoes usa o mesmo
/// <see cref="PrintTable"/> — as duas sao tabelas, e elas nao podem ter
/// regras de impressao diferentes.
/// </summary>
public sealed class PrintLayout
{
    private readonly PrintTable _table;

    /// <summary>O titulo do documento (o que aparece no topo da primeira pagina).</summary>
    public string Title => _table.Title;

    /// <summary>O cabecalho do documento (aplicativo, quantidade e data).</summary>
    public string Header => _table.Header;

    /// <summary>As colunas do quadro.</summary>
    public IReadOnlyList<PrintColumn> Columns => _table.Columns;

    /// <summary>As paginas, na ordem.</summary>
    public IReadOnlyList<PrintPage> Pages => _table.Pages;

    /// <summary>Quantas linhas de dados cabem numa pagina.</summary>
    public int RowsPerPage => _table.RowsPerPage;

    /// <summary>Quantas paginas o documento tem.</summary>
    public int TotalPages => _table.TotalPages;

    /// <summary>Quantos programas o documento lista.</summary>
    public int TotalRows => _table.TotalRows;

    /// <summary>
    /// A tabela por tras do layout. Quem quiser imprimir qualquer tabela
    /// (a lista de programas, a arvore de alteracoes) usa esta.
    /// </summary>
    public PrintTable Table => _table;

    private PrintLayout(PrintTable table)
    {
        _table = table;
    }

    /// <summary>
    /// Monta o layout de impressao da lista.
    ///
    /// A quebra de pagina e o cabecalho das colunas ficam com o
    /// <see cref="PrintTable"/>: este modulo decide o que entra na lista, e
    /// nao como ela e impressa.
    /// </summary>
    /// <param name="programs">Os programas a listar.</param>
    /// <param name="columns">
    /// As colunas. Nulo ou vazio usa o conjunto padrao do exportador.
    /// </param>
    /// <param name="rowsPerPage">Quantas linhas de dados cabem numa pagina.</param>
    /// <param name="title">O titulo do documento.</param>
    /// <param name="header">O cabecalho do documento.</param>
    public static PrintLayout Build(
        IReadOnlyList<InstalledProgram> programs,
        IReadOnlyList<ProgramColumn>? columns = null,
        int rowsPerPage = PrintTable.DefaultRowsPerPage,
        string? title = null,
        string? header = null)
    {
        var sel = ProgramRows.ColumnsOf(columns);
        var valores = ProgramRows.Values(programs, sel);
        var titulos = sel.Select(c => ProgramColumns.Label(c)).ToList();

        var tabela = PrintTable.Build(
            string.IsNullOrWhiteSpace(title) ? "Programas instalados" : title.Trim(),
            string.IsNullOrWhiteSpace(header) ? CabecalhoPadrao(valores.Count) : header.Trim(),
            titulos,
            valores,
            rowsPerPage);

        return new PrintLayout(tabela);
    }

    /// <summary>
    /// O cabecalho padrao do documento: quem gerou, quantos programas e
    /// quando.
    /// </summary>
    /// <param name="quantidade">A quantidade de programas.</param>
    private static string CabecalhoPadrao(int quantidade) => string.Format(
        CultureInfo.CurrentCulture,
        "Tranal Uninstaller Pro — {0} programa(s) — {1}",
        quantidade,
        DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture));
}
