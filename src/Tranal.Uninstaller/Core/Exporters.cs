using System.IO;
using System.Text;
using System.Xml;
using Tranal.Uninstaller.Core;

namespace Tranal.Uninstaller.Core;

/// <summary>
/// Exportacao da lista de programas nos formatos aceitos pelo Revo.
///
/// As colunas exportadas sao escolhidas pelo usuario (15066: "Select the
/// columns you want to export:") e vêm do catalogo unico de colunas
/// (<see cref="ProgramColumns"/>) — o mesmo que a lista da tela usa. Nao
/// existe uma segunda interpretacao do que e uma coluna.
/// </summary>
public static class Exporters
{
    /// <summary>
    /// As colunas exportadas quando o usuario nao escolhe nenhuma.
    ///
    /// E o conjunto do Revo: nome, versao, tipo, data, empresa, pagina inicial,
    /// comentarios, comando de desinstalacao e chave do registro. Fixo e
    /// explicito — o formato do arquivo exportado nao pode depender do
    /// catalogo de colunas da tela, que o usuario pode mudar.
    /// </summary>
    public static IReadOnlyList<ProgramColumn> DefaultColumns { get; } = new[]
    {
        ProgramColumn.Name,
        ProgramColumn.Version,
        ProgramColumn.Type,
        ProgramColumn.Date,
        ProgramColumn.Company,
        ProgramColumn.HomePage,
        ProgramColumn.Comments,
        ProgramColumn.UninstallString,
        ProgramColumn.RegistryKey,
    };

    public static string ToText(IEnumerable<InstalledProgram> programs, IReadOnlyList<ProgramColumn>? columns = null)
    {
        var sel = columns ?? DefaultColumns;
        var anotacoes = ProgramCustomizationStore.PeekAll();
        var sb = new StringBuilder();
        foreach (var p in programs)
            sb.AppendLine(string.Join(" - ", sel.Select(c => ProgramColumns.Value(c, p, CustomizationOf(p, anotacoes)))));
        return sb.ToString();
    }

    public static string ToCsv(IEnumerable<InstalledProgram> programs, IReadOnlyList<ProgramColumn>? columns = null)
    {
        var sel = columns ?? DefaultColumns;
        var anotacoes = ProgramCustomizationStore.PeekAll();
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(";", sel.Select(c => ExportNameOf(c))));
        foreach (var p in programs)
        {
            sb.AppendLine(string.Join(";",
                sel.Select(c => Csv(ProgramColumns.Value(c, p, CustomizationOf(p, anotacoes))))));
        }
        return sb.ToString();
    }

    public static string ToHtml(IEnumerable<InstalledProgram> programs, IReadOnlyList<ProgramColumn>? columns = null)
    {
        var sel = columns ?? DefaultColumns;
        var anotacoes = ProgramCustomizationStore.PeekAll();
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"pt-BR\"><head><meta charset=\"utf-8\">");
        sb.AppendLine("<title>Tranal Uninstaller - Lista de programas</title>");
        sb.AppendLine("<style>body{font-family:Segoe UI,Arial;background:#525252;color:#e6e6e6}");
        sb.AppendLine("table{border-collapse:collapse;width:100%}th,td{border:1px solid #2e2e2e;padding:4px 8px}");
        sb.AppendLine("th{background:#3c3c3c;text-align:left}tr:nth-child(even){background:#5a5a5a}</style>");
        sb.AppendLine("</head><body><h1>Programas instalados</h1><table>");
        sb.AppendLine("<tr>" + string.Join("", sel.Select(c => $"<th>{Html(ProgramColumns.Label(c))}</th>")) + "</tr>");
        foreach (var p in programs)
        {
            sb.AppendLine("<tr>"
                + string.Join("", sel.Select(c => $"<td>{Html(ProgramColumns.Value(c, p, CustomizationOf(p, anotacoes)))}</td>"))
                + "</tr>");
        }
        sb.AppendLine("</table></body></html>");
        return sb.ToString();
    }

    /// <summary>
    /// Exporta a lista para Microsoft Excel.
    ///
    /// O formato e o SpreadsheetML 2003 (XML puro): o Excel abre nativamente,
    /// sem o Office instalado, e o arquivo continua auditavel. E o **mesmo**
    /// formato que <c>Core.Reporting.ReportFormats.ToExcel</c> produz — este
    /// metodo e so a porta de entrada, para que exista uma unica
    /// implementacao de cada formato.
    /// </summary>
    public static string ToExcel(IEnumerable<InstalledProgram> programs, IReadOnlyList<ProgramColumn>? columns = null)
    {
        var lista = programs?.ToList() ?? new List<InstalledProgram>();
        return Core.Reporting.ReportFormats.ToExcel(lista, columns ?? DefaultColumns);
    }

    /// <summary>
    /// Exporta alteracoes de registro para o formato .reg do Windows.
    ///
    /// O arquivo e escrito em UTF-16 LE com BOM, que e o que o Editor do
    /// Registro e o <c>reg.exe</c> esperam.
    /// </summary>
    public static string WriteReg(string path, IEnumerable<Core.Reporting.RegistryChange> changes)
    {
        return Core.Reporting.ReportFormats.WriteReg(path, changes);
    }

    /// <summary>
    /// Exporta a lista de programas em JSON.
    ///
    /// O Bullseye NortonCleanup e o BCU exportam a lista em JSON: e o formato
    /// que as ferramentas de linha de comando consumem sem depender do
    /// separador regional.
    ///
    /// As chaves sao os identificadores das colunas do catalogo unico
    /// (<see cref="ProgramColumns"/>), e o valor de cada uma vem do mesmo
    /// metodo que a tela usa — nao existe uma segunda interpretacao do que e
    /// uma coluna.
    /// </summary>
    /// <param name="programs">Os programas a exportar.</param>
    /// <param name="columns">As colunas exportadas.</param>
    public static string ToJson(IEnumerable<InstalledProgram> programs, IReadOnlyList<ProgramColumn>? columns = null)
    {
        var sel = columns ?? DefaultColumns;
        var anotacoes = ProgramCustomizationStore.PeekAll();

        var lista = (programs ?? Enumerable.Empty<InstalledProgram>())
            .Where(p => p is not null)
            .Select(p =>
            {
                var item = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var coluna in sel)
                {
                    item[ExportNameOf(coluna)] = ProgramColumns.Value(coluna, p, CustomizationOf(p, anotacoes));
                }

                return item;
            })
            .ToList();

        var documento = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["exported"] = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
            ["count"] = lista.Count,
            ["programs"] = lista,
        };

        return System.Text.Json.JsonSerializer.Serialize(
            documento,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    public static string ToXml(IEnumerable<InstalledProgram> programs, IReadOnlyList<ProgramColumn>? columns = null)
    {
        var sel = columns ?? DefaultColumns;
        var anotacoes = ProgramCustomizationStore.PeekAll();
        var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, settings))
        {
            writer.WriteStartElement("TranalUninstaller");
            writer.WriteAttributeString("exported", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"));
            foreach (var p in programs)
            {
                // O registro e um "Entry": as colunas sao os elementos, com o
                // nome fixo da coluna. O elemento do registro nao pode ter o
                // mesmo nome da coluna "Program".
                writer.WriteStartElement("Entry");
                foreach (var c in sel)
                    writer.WriteElementString(ExportNameOf(c), ProgramColumns.Value(c, p, CustomizationOf(p, anotacoes)));
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// O nome da coluna no CSV e no XML: fixo e independente do idioma, para
    /// o arquivo continuar legivel por qualquer ferramenta. O HTML, que e
    /// lido por gente, usa o rotulo traduzido.
    /// </summary>
    private static string ExportNameOf(ProgramColumn column) => ProgramColumns.Info(column).ExportName;

    /// <summary>
    /// A anotacao do usuario sobre o programa.
    ///
    /// A lista vem de <see cref="ProgramCustomizationStore.PeekAll"/>: uma
    /// leitura que nao mexe no arquivo do usuario. A exportacao e uma
    /// operacao de leitura — renomear o arquivo corrompido de quem so queria
    /// exportar a lista seria efeito colateral inaceitavel.
    /// </summary>
    private static ProgramCustomization? CustomizationOf(
        InstalledProgram program, IReadOnlyList<ProgramCustomization> anotacoes)
    {
        var key = ProgramCustomizationStore.KeyOf(program);
        foreach (var item in anotacoes)
        {
            if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)) return item;
        }

        return null;
    }

    private static string Csv(string? value)
    {
        var text = value ?? string.Empty;
        return text.Contains(';') || text.Contains('"')
            ? '"' + text.Replace("\"", "\"\"") + '"'
            : text;
    }

    private static string Html(string? value)
    {
        var text = value ?? string.Empty;
        return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
