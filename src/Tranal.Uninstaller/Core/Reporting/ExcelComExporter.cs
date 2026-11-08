using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Tranal.Uninstaller.Core.Reporting;

/// <summary>
/// Como terminou a exportacao para o Excel.
/// </summary>
public enum ExcelExportStatus
{
    /// <summary>O arquivo foi gravado.</summary>
    Ok,

    /// <summary>
    /// O Excel nao esta instalado nesta maquina: o arquivo so pode ser
    /// gravado no formato SpreadsheetML (XML), que o Excel abre nativamente.
    /// </summary>
    ExcelNotInstalled,

    /// <summary>O caminho do arquivo e invalido.</summary>
    InvalidPath,

    /// <summary>A gravacao falhou.</summary>
    WriteFailed,

    /// <summary>
    /// O Excel esta instalado, mas a automacao via COM falhou (o Office
    /// esta bloqueado, ou o usuario cancelou a permissao).
    /// </summary>
    ComError,
}

/// <summary>
/// O resultado de uma exportacao para o Excel.
/// </summary>
public sealed class ExcelExportOutcome
{
    /// <summary>Como a operacao terminou.</summary>
    public ExcelExportStatus Status { get; init; } = ExcelExportStatus.Ok;

    /// <summary>O caminho do arquivo gravado (vazio quando falhou).</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// A explicacao, em portugues, do que aconteceu. Nunca fica vazia: a
    /// tela mostra este texto ao usuario.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>True quando o arquivo foi gravado pelo Excel instalado.</summary>
    public bool UsedInstalledExcel { get; init; }

    /// <summary>
    /// True quando o Excel nao estava instalado e o arquivo foi gravado no
    /// formato SpreadsheetML.
    /// </summary>
    public bool UsedFallback { get; init; }
}

/// <summary>
/// Exporta a lista de programas para o **Excel instalado**, via automacao
/// COM (<c>Excel.Application</c>).
///
/// A exportacao em SpreadsheetML (<see cref="ReportFormats.ToExcel"/>) e o
/// formato que funciona sem o Office instalado. Esta e a outra via: quando
/// o Excel esta na maquina, o arquivo sai como .xlsx de verdade, com as
/// formulas e os filtros que o Excel oferece.
///
/// As duas rotas sao a mesma lista de programas e as mesmas colunas — a
/// diferenca e apenas quem grava o arquivo.
/// </summary>
public static class ExcelComExporter
{
    /// <summary>O identificador de programa do Excel.</summary>
    private const string ExcelProgId = "Excel.Application";

    /// <summary>
    /// O formato de arquivo do Excel para a pasta de trabalho (.xlsx).
    /// </summary>
    private const int XlsxFormat = 51;

    /// <summary>
    /// True quando o Excel esta instalado nesta maquina.
    ///
    /// A verificacao nao abre o Excel: ela apenas pergunta ao Windows se o
    /// programa existe.
    /// </summary>
    public static bool IsExcelAvailable()
    {
        try
        {
            return Type.GetTypeFromProgID(ExcelProgId) is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Exporta a lista de programas para o Excel instalado.
    ///
    /// Quando o Excel nao esta instalado, o resultado traz o motivo
    /// (<see cref="ExcelExportStatus.ExcelNotInstalled"/>) e nada e
    /// gravado: quem chama decide se usa o formato SpreadsheetML.
    /// </summary>
    /// <param name="programs">Os programas exportados.</param>
    /// <param name="path">O caminho do arquivo .xlsx a gravar.</param>
    /// <param name="columns">
    /// As colunas. Nulo ou vazio usa o conjunto padrao do exportador.
    /// </param>
    public static ExcelExportOutcome Export(
        IEnumerable<InstalledProgram> programs,
        string path,
        IReadOnlyList<ProgramColumn>? columns = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Falha(ExcelExportStatus.InvalidPath, "Informe o caminho do arquivo do Excel.");
        }

        if (!IsExcelAvailable())
        {
            return Falha(
                ExcelExportStatus.ExcelNotInstalled,
                "O Microsoft Excel nao esta instalado nesta maquina.");
        }

        var lista = (programs ?? Enumerable.Empty<InstalledProgram>())
            .Where(p => p is not null)
            .ToList();

        var sel = ProgramRows.ColumnsOf(columns);
        var valores = ProgramRows.Values(lista, sel);

        object? excel = null;
        object? pasta = null;
        object? planilha = null;

        try
        {
            var tipoExcel = Type.GetTypeFromProgID(ExcelProgId);
            if (tipoExcel is null)
            {
                return Falha(
                    ExcelExportStatus.ExcelNotInstalled,
                    "O Microsoft Excel nao esta instalado nesta maquina.");
            }

            excel = Activator.CreateInstance(tipoExcel);
            if (excel is null)
            {
                return Falha(
                    ExcelExportStatus.ComError,
                    "Nao foi possivel iniciar o Microsoft Excel.");
            }

            Invoke(tipoExcel, excel, "Visible", BindingFlags.SetProperty, false);
            Invoke(tipoExcel, excel, "DisplayAlerts", BindingFlags.SetProperty, false);

            var planilhas = Invoke(tipoExcel, excel, "Workbooks", BindingFlags.InvokeMethod);
            pasta = Invoke(tipoExcel, planilhas, "Add", BindingFlags.InvokeMethod, 1);

            var abas = Invoke(tipoExcel, pasta, "Worksheets", BindingFlags.GetProperty);
            planilha = Invoke(tipoExcel, abas, "Item", BindingFlags.GetProperty, 1);

            // O cabecalho usa o nome fixo da coluna (como o CSV e o XML):
            // o arquivo precisa continuar legivel por qualquer ferramenta.
            var cabecalho = sel.Select(c => ProgramColumns.Info(c).ExportName).ToList();
            Preencher(planilha, cabecalho, 1, 1, cabecalho.Count);

            for (var i = 0; i < valores.Count; i++)
                Preencher(planilha, valores[i], i + 2, 1, sel.Count);

            var destino = Path.GetFullPath(path);
            var pastaDestino = Path.GetDirectoryName(destino);
            if (!string.IsNullOrEmpty(pastaDestino)) Directory.CreateDirectory(pastaDestino);

            Invoke(tipoExcel, pasta, "SaveAs", BindingFlags.InvokeMethod, destino, XlsxFormat);

            Invoke(tipoExcel, pasta, "Close", BindingFlags.InvokeMethod, false);
            Invoke(tipoExcel, excel, "Quit", BindingFlags.InvokeMethod);

            return new ExcelExportOutcome
            {
                Status = ExcelExportStatus.Ok,
                Path = destino,
                Message = "Lista exportada pelo Microsoft Excel.",
                UsedInstalledExcel = true,
            };
        }
        catch (Exception ex)
        {
            return Falha(
                ExcelExportStatus.ComError,
                $"O Microsoft Excel nao conseguiu gravar o arquivo: {ex.Message}");
        }
        finally
        {
            Soltar(pasta);
            Soltar(excel);
        }
    }

    /// <summary>
    /// Exporta a lista de programas para o Excel, caindo no formato
    /// SpreadsheetML quando o Excel nao esta instalado.
    ///
    /// E o caminho que a tela usa quando o usuario marcou "exportar para o
    /// Excel instalado": se o Excel estiver na maquina, o arquivo sai como
    /// .xlsx de verdade; se nao estiver, sai no formato XML que o Excel
    /// abre nativamente — o usuario sempre recebe um arquivo.
    /// </summary>
    /// <param name="programs">Os programas exportados.</param>
    /// <param name="path">O caminho do arquivo a gravar.</param>
    /// <param name="columns">
    /// As colunas. Nulo ou vazio usa o conjunto padrao do exportador.
    /// </param>
    /// <param name="preferInstalledExcel">
    /// True para tentar o Excel instalado primeiro.
    /// </param>
    public static ExcelExportOutcome ExportWithFallback(
        IEnumerable<InstalledProgram> programs,
        string path,
        IReadOnlyList<ProgramColumn>? columns = null,
        bool preferInstalledExcel = true)
    {
        if (preferInstalledExcel)
        {
            var resultado = Export(programs, path, columns);
            if (resultado.Status != ExcelExportStatus.ExcelNotInstalled) return resultado;
        }

        var fallback = ExportSpreadsheetMl(programs, path, columns);
        if (fallback is null) return ResultadoFallback(path);

        return new ExcelExportOutcome
        {
            Status = ExcelExportStatus.Ok,
            Path = fallback,
            Message = preferInstalledExcel
                ? "O Microsoft Excel nao esta instalado: o arquivo foi gravado no formato SpreadsheetML (XML), que o Excel abre nativamente."
                : "Lista exportada no formato SpreadsheetML (XML), que o Excel abre nativamente.",
            UsedInstalledExcel = false,
            UsedFallback = true,
        };
    }

    /// <summary>
    /// Grava a lista no formato SpreadsheetML (XML puro). Devolve o caminho
    /// gravado, ou nulo quando nao deu para gravar.
    /// </summary>
    private static string? ExportSpreadsheetMl(
        IEnumerable<InstalledProgram> programs,
        string path,
        IReadOnlyList<ProgramColumn>? columns)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        try
        {
            var lista = (programs ?? Enumerable.Empty<InstalledProgram>())
                .Where(p => p is not null)
                .ToList();

            var sel = ProgramRows.ColumnsOf(columns);
            var conteudo = ReportFormats.ToExcel(lista, sel);

            var destino = Path.GetFullPath(path);
            var pasta = Path.GetDirectoryName(destino);
            if (!string.IsNullOrEmpty(pasta)) Directory.CreateDirectory(pasta);

            File.WriteAllText(destino, conteudo, new UTF8Encoding(false));
            return destino;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Escreve uma linha na planilha, celula a celula.
    /// </summary>
    private static void Preencher(object? planilha, IReadOnlyList<string> valores, int linha, int coluna, int colunas)
    {
        if (planilha is null) return;

        for (var i = 0; i < colunas; i++)
        {
            var texto = i < valores.Count ? valores[i] : string.Empty;

            var celulas = Invoke(planilha.GetType(), planilha, "Cells", BindingFlags.GetProperty, linha, i + 1);
            if (celulas is null) continue;

            Invoke(celulas.GetType(), celulas, "Value", BindingFlags.SetProperty, texto ?? string.Empty);
        }
    }

    /// <summary>
    /// Chama um metodo ou uma propriedade do Excel por reflexao.
    ///
    /// Devolve nulo quando o alvo e nulo: um passo da automacao que falhou
    /// devolve nulo, e o chamador trata como erro em vez de estourar um
    /// <c>NullReferenceException</c> no meio da exportacao.
    /// </summary>
    private static object? Invoke(
        Type tipo,
        object? alvo,
        string nome,
        BindingFlags flags,
        params object?[] argumentos)
    {
        if (alvo is null) return null;

        return tipo.InvokeMember(nome, flags, null, alvo, argumentos);
    }

    /// <summary>
    /// Solta o objeto COM. Sem isso, o Excel continua aberto depois da
    /// exportacao.
    /// </summary>
    private static void Soltar(object? objeto)
    {
        if (objeto is null) return;
        if (!Marshal.IsComObject(objeto)) return;

        try { Marshal.ReleaseComObject(objeto); }
        catch { /* o objeto ja foi liberado */ }
    }

    /// <summary>Um resultado de falha, com a mensagem pronta para a tela.</summary>
    private static ExcelExportOutcome Falha(ExcelExportStatus status, string mensagem) =>
        new()
        {
            Status = status,
            Message = mensagem,
        };

    /// <summary>
    /// O resultado quando nem o Excel nem o SpreadsheetML deu certo: o
    /// caminho continua vazio, para ninguem abrir um arquivo que nao existe.
    /// </summary>
    private static ExcelExportOutcome ResultadoFallback(string path) =>
        Falha(
            ExcelExportStatus.WriteFailed,
            $"Nao foi possivel gravar o arquivo '{path}'.");
}
