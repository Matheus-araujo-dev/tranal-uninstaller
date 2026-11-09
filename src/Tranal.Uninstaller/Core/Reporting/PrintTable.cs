using System.Collections.Generic;
using System.Globalization;

namespace Tranal.Uninstaller.Core.Reporting;

/// <summary>
/// Uma coluna do layout de impressao.
///
/// A largura e o numero de caracteres: o nucleo nao pode medir texto (isso
/// e coisa de tela), entao o layout entrega a largura em caracteres e a
/// interface usa uma fonte de largura fixa.
/// </summary>
/// <param name="Title">O titulo da coluna, ja traduzido.</param>
/// <param name="Width">A largura da coluna, em caracteres.</param>
public sealed record PrintColumn(string Title, int Width);

/// <summary>
/// Uma pagina do layout de impressao.
///
/// As linhas ja vem prontas: a interface desenha, e o nucleo decide o que
/// entra em cada pagina. A primeira linha de toda pagina e o cabecalho das
/// colunas — e ele que se repete quando a lista passa de uma pagina.
/// </summary>
/// <param name="Number">O numero da pagina, comecando em 1.</param>
/// <param name="TotalPages">Quantas paginas o documento tem ao todo.</param>
/// <param name="Rows">As linhas da pagina, ja formatadas.</param>
/// <param name="Footer">O rodape da pagina ("Pagina X de Y").</param>
public sealed record PrintPage(int Number, int TotalPages, IReadOnlyList<string> Rows, string Footer);

/// <summary>
/// A tabela que sera impressa: as colunas, as linhas e a quebra de pagina.
///
/// Este e o unico lugar que decide como uma tabela vira folha impressa. A
/// lista de programas (<see cref="PrintLayout"/>) e a arvore de alteracoes
/// (<see cref="ChangeTreeReport"/>) usam este mesmo codigo: as duas sao
/// tabelas, e elas nao podem ter regras de impressao diferentes.
///
/// O nucleo nao pode usar WPF nem <c>System.Drawing</c> (e compartilhado
/// entre a interface grafica e a CLI), entao quem produz a impressao aqui
/// e o layout: as colunas, o cabecalho, a quebra de pagina e o rodape. A
/// interface grafica transforma isso em <c>PrintDocument</c>; a CLI pode
/// transformar em texto.
/// </summary>
public sealed class PrintTable
{
    /// <summary>Quantas linhas de dados cabem numa pagina, por padrao.</summary>
    public const int DefaultRowsPerPage = 40;

    /// <summary>A largura maxima de uma linha impressa, em caracteres.</summary>
    public const int MaxLineWidth = 120;

    /// <summary>A largura minima de uma coluna, em caracteres.</summary>
    public const int MinColumnWidth = 6;

    /// <summary>O titulo do documento (o que aparece no topo da primeira pagina).</summary>
    public string Title { get; }

    /// <summary>O cabecalho do documento (aplicativo, quantidade e data).</summary>
    public string Header { get; }

    /// <summary>As colunas do quadro.</summary>
    public IReadOnlyList<PrintColumn> Columns { get; }

    /// <summary>As paginas, na ordem.</summary>
    public IReadOnlyList<PrintPage> Pages { get; }

    /// <summary>Quantas linhas de dados cabem numa pagina.</summary>
    public int RowsPerPage { get; }

    /// <summary>Quantas linhas de dados o documento tem.</summary>
    public int TotalRows { get; }

    /// <summary>Quantas paginas o documento tem.</summary>
    public int TotalPages => Pages.Count;

    private PrintTable(
        string title,
        string header,
        IReadOnlyList<PrintColumn> columns,
        IReadOnlyList<PrintPage> pages,
        int rowsPerPage,
        int totalRows)
    {
        Title = title;
        Header = header;
        Columns = columns;
        Pages = pages;
        RowsPerPage = rowsPerPage;
        TotalRows = totalRows;
    }

    /// <summary>
    /// Monta o layout de impressao de uma tabela.
    ///
    /// As linhas sao paginadas: cada pagina repete o cabecalho das colunas e
    /// traz o rodape com o numero da pagina. A tabela vazia gera uma pagina
    /// so, com o cabecalho — um documento em branco nao tem pagina nenhuma,
    /// e a interface precisa ter o que desenhar.
    /// </summary>
    /// <param name="title">O titulo do documento.</param>
    /// <param name="header">O cabecalho do documento.</param>
    /// <param name="headers">Os titulos das colunas.</param>
    /// <param name="rows">As linhas, uma por linha da tabela.</param>
    /// <param name="rowsPerPage">Quantas linhas de dados cabem numa pagina.</param>
    public static PrintTable Build(
        string title,
        string header,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows,
        int rowsPerPage = DefaultRowsPerPage)
    {
        var titulos = headers ?? Array.Empty<string>();
        var valores = rows ?? Array.Empty<IReadOnlyList<string>>();

        var larguras = CalcularLarguras(titulos, valores);
        var cabecalho = Formatar(titulos, larguras);

        var porPagina = rowsPerPage <= 0 ? DefaultRowsPerPage : rowsPerPage;
        var totalPaginas = valores.Count == 0 ? 1 : (valores.Count + porPagina - 1) / porPagina;

        var paginas = new List<PrintPage>();
        var indice = 0;

        for (var numero = 1; numero <= totalPaginas; numero++)
        {
            var linhas = new List<string> { cabecalho };

            for (var i = 0; i < porPagina && indice < valores.Count; i++, indice++)
            {
                linhas.Add(Formatar(valores[indice], larguras));
            }

            paginas.Add(new PrintPage(
                numero,
                totalPaginas,
                linhas,
                string.Format(CultureInfo.CurrentCulture, "Página {0} de {1}", numero, totalPaginas)));
        }

        var colunas = new List<PrintColumn>();
        for (var i = 0; i < titulos.Count; i++)
        {
            colunas.Add(new PrintColumn(titulos[i], larguras[i]));
        }

        return new PrintTable(
            string.IsNullOrWhiteSpace(title) ? "Relatorio" : title.Trim(),
            header ?? string.Empty,
            colunas,
            paginas,
            porPagina,
            valores.Count);
    }

    /// <summary>
    /// A largura de cada coluna: a do maior conteudo, encolhida para caber
    /// na largura maxima da linha.
    ///
    /// A tabela tem colunas longas (o caminho de um arquivo, a chave do
    /// registro) e uma linha de 400 caracteres nao cabe em pagina nenhuma.
    /// Encolher proporcionalmente mantem todas as colunas visiveis.
    /// </summary>
    /// <param name="titulos">Os titulos das colunas.</param>
    /// <param name="valores">Os valores das linhas.</param>
    private static int[] CalcularLarguras(IReadOnlyList<string> titulos, IReadOnlyList<IReadOnlyList<string>> valores)
    {
        var larguras = new int[titulos.Count];

        for (var i = 0; i < titulos.Count; i++)
        {
            var maior = titulos[i]?.Length ?? 0;

            foreach (var linha in valores)
            {
                if (i >= linha.Count) continue;
                var tamanho = linha[i]?.Length ?? 0;
                if (tamanho > maior) maior = tamanho;
            }

            larguras[i] = Math.Max(maior, MinColumnWidth);
        }

        var separador = titulos.Count > 1 ? (titulos.Count - 1) * 3 : 0;
        var disponivel = Math.Max(MinColumnWidth, MaxLineWidth - separador);
        var soma = larguras.Sum();
        if (soma <= disponivel) return larguras;

        // Distribuicao proporcional: toda coluna mantem o minimo, e a que
        // sobra do espaco diferente leva o que der.
        var sobra = disponivel - (larguras.Length * MinColumnWidth);
        var extras = soma - (larguras.Length * MinColumnWidth);
        var novas = new int[larguras.Length];

        for (var i = 0; i < larguras.Length; i++)
        {
            var extra = extras > 0 ? (larguras[i] - MinColumnWidth) : 0;
            var parcela = extras > 0 ? (extra * sobra) / extras : 0;
            novas[i] = MinColumnWidth + parcela;
        }

        // A conta pode deixar a linha um pouco acima do limite: corta da
        // coluna mais larga ate caber.
        var total = novas.Sum() + separador;
        while (total > MaxLineWidth)
        {
            var maior = 0;
            for (var i = 1; i < novas.Length; i++)
            {
                if (novas[i] > novas[maior]) maior = i;
            }

            if (novas[maior] <= MinColumnWidth) break;
            novas[maior]--;
            total--;
        }

        return novas;
    }

    /// <summary>
    /// Uma linha do quadro, com as colunas separadas por " | " e cada uma
    /// com a largura da sua coluna.
    /// </summary>
    /// <param name="valores">Os valores da linha.</param>
    /// <param name="larguras">As larguras das colunas.</param>
    private static string Formatar(IReadOnlyList<string> valores, IReadOnlyList<int> larguras)
    {
        var partes = new List<string>(valores.Count);

        for (var i = 0; i < valores.Count; i++)
        {
            var largura = i < larguras.Count ? larguras[i] : MinColumnWidth;
            partes.Add(Ajustar(valores[i] ?? string.Empty, largura));
        }

        return string.Join(" | ", partes);
    }

    /// <summary>
    /// Ajusta o texto a largura da coluna, cortando o que nao couber.
    /// </summary>
    /// <param name="texto">O texto.</param>
    /// <param name="largura">A largura da coluna.</param>
    private static string Ajustar(string texto, int largura)
    {
        if (largura <= 0) return string.Empty;
        if (texto.Length <= largura) return texto.PadRight(largura);
        if (largura == 1) return texto[..1];
        return texto[..(largura - 1)] + "…";
    }
}
