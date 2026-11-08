using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Tranal.Uninstaller.Core.Reporting;

/// <summary>
/// Uma coluna do relatorio de uma ferramenta.
/// </summary>
public sealed class ToolReportColumn
{
    /// <summary>O titulo da coluna.</summary>
    public string Header { get; init; } = string.Empty;

    /// <summary>A largura em pixels.</summary>
    public int Width { get; init; }
}

/// <summary>
/// Uma linha do relatorio de uma ferramenta.
/// </summary>
public sealed class ToolReportRow
{
    /// <summary>As celulas da linha, na ordem das colunas.</summary>
    public IReadOnlyList<string> Cells { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Relatorio em HTML de uma ferramenta.
///
/// O HiBit mede "Relatorios HTML por ferramenta": cada ferramenta da janela
/// (servicos, drivers, tarefas, inicializacao, menu de contexto) tem o seu
/// relatorio, e nao apenas a lista de programas.
///
/// O relatorio e um arquivo HTML unico, com o estilo embutido: ele abre em
/// qualquer navegador, imprime sem estilo externo e nao depende de rede.
/// </summary>
public static class ToolReportWriter
{
    /// <summary>
    /// Gera o relatorio em HTML.
    ///
    /// </summary>
    /// <param name="title">O titulo do relatorio (o nome da ferramenta).</param>
    /// <param name="subtitle">A linha de sub titulo (quando houver).</param>
    /// <param name="columns">As colunas.</param>
    /// <param name="rows">As linhas.</param>
    public static string ToHtml(
        string title,
        string? subtitle,
        IReadOnlyList<ToolReportColumn> columns,
        IReadOnlyList<ToolReportRow> rows)
    {
        var html = new StringBuilder();

        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"pt-BR\">");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"utf-8\" />");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        html.AppendLine($"<title>{Escapar(title)}</title>");
        html.AppendLine("<style>");
        html.AppendLine(":root { color-scheme: light dark; }");
        html.AppendLine("body { font-family: 'Segoe UI', system-ui, sans-serif; margin: 24px; background: #fbfbfd; color: #1c1c1e; }");
        html.AppendLine("h1 { font-size: 20px; margin: 0 0 4px 0; }");
        html.AppendLine("p.subtitulo { margin: 0 0 16px 0; color: #6b6b70; font-size: 13px; }");
        html.AppendLine("table { border-collapse: collapse; width: 100%; font-size: 13px; background: #fff; box-shadow: 0 1px 3px rgba(0,0,0,.12); }");
        html.AppendLine("th { text-align: left; padding: 8px 10px; background: #f0f0f4; border-bottom: 1px solid #d8d8de; font-weight: 600; }");
        html.AppendLine("td { padding: 6px 10px; border-bottom: 1px solid #ececf0; vertical-align: top; word-break: break-word; }");
        html.AppendLine("tr:nth-child(even) td { background: #fafafc; }");
        html.AppendLine("p.rodape { margin-top: 16px; color: #6b6b70; font-size: 12px; }");
        html.AppendLine("@media print { body { margin: 0; } table { box-shadow: none; } }");
        html.AppendLine("@media (prefers-color-scheme: dark) {");
        html.AppendLine("  body { background: #1c1c1e; color: #f5f5f7; }");
        html.AppendLine("  th { background: #2c2c2e; border-bottom-color: #3a3a3c; }");
        html.AppendLine("  td { border-bottom-color: #2c2c2e; }");
        html.AppendLine("  tr:nth-child(even) td { background: #232325; }");
        html.AppendLine("  p.subtitulo, p.rodape { color: #a0a0a5; }");
        html.AppendLine("}");
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");

        html.AppendLine($"<h1>{Escapar(title)}</h1>");
        if (!string.IsNullOrWhiteSpace(subtitle))
            html.AppendLine($"<p class=\"subtitulo\">{Escapar(subtitle)}</p>");

        html.AppendLine("<table>");
        html.AppendLine("<thead><tr>");
        foreach (var coluna in columns)
        {
            var largura = coluna.Width > 0 ? $" style=\"width:{coluna.Width}px\"" : string.Empty;
            html.AppendLine($"<th{largura}>{Escapar(coluna.Header)}</th>");
        }
        html.AppendLine("</tr></thead>");
        html.AppendLine("<tbody>");

        foreach (var linha in rows)
        {
            html.AppendLine("<tr>");
            for (var i = 0; i < columns.Count; i++)
            {
                var celula = i < linha.Cells.Count ? linha.Cells[i] : string.Empty;
                html.AppendLine($"<td>{Escapar(celula)}</td>");
            }
            html.AppendLine("</tr>");
        }

        html.AppendLine("</tbody>");
        html.AppendLine("</table>");

        html.AppendLine($"<p class=\"rodape\">{rows.Count} item(ns) — Tranal Uninstaller Pro, {DateTime.Now:dd/MM/yyyy HH:mm}</p>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return html.ToString();
    }

    /// <summary>
    /// Grava o relatorio em HTML no caminho informado.
    ///
    /// </summary>
    /// <param name="path">O caminho do arquivo.</param>
    /// <param name="title">O titulo do relatorio.</param>
    /// <param name="subtitle">A linha de sub titulo.</param>
    /// <param name="columns">As colunas.</param>
    /// <param name="rows">As linhas.</param>
    public static string Write(
        string path,
        string title,
        string? subtitle,
        IReadOnlyList<ToolReportColumn> columns,
        IReadOnlyList<ToolReportRow> rows)
    {
        var html = ToHtml(title, subtitle, columns, rows);

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        File.WriteAllText(path, html, new UTF8Encoding(false));
        return path;
    }

    /// <summary>
    /// Escapa o texto para ir dentro do HTML.
    ///
    /// </summary>
    private static string Escapar(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return string.Empty;

        return texto
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }
}
