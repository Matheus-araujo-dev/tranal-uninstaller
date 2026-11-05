using System.IO;
using System.Text;
using System.Xml;
using Tranal.Uninstaller.Core;

namespace Tranal.Uninstaller.Core;

/// <summary>Exportacao da lista de programas nos formatos aceitos pelo Revo.</summary>
public static class Exporters
{
    public static string ToText(IEnumerable<InstalledProgram> programs)
    {
        var sb = new StringBuilder();
        foreach (var p in programs)
            sb.AppendLine($"{p.DisplayName} - {p.VersionOrNoVersion}");
        return sb.ToString();
    }

    public static string ToCsv(IEnumerable<InstalledProgram> programs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Program;Version;Type;Date;Company;HomePage;Comments;UninstallString;RegistryKey");
        foreach (var p in programs)
        {
            sb.AppendLine(string.Join(";",
                Csv(p.DisplayName), Csv(p.VersionOrNoVersion), Csv(p.Bits), Csv(p.InstallDate),
                Csv(p.Publisher), Csv(p.UrlInfoAbout), Csv(p.Comments),
                Csv(p.UninstallString), Csv(p.RegistryKey)));
        }
        return sb.ToString();
    }

    public static string ToHtml(IEnumerable<InstalledProgram> programs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"pt-BR\"><head><meta charset=\"utf-8\">");
        sb.AppendLine("<title>Tranal Uninstaller - Lista de programas</title>");
        sb.AppendLine("<style>body{font-family:Segoe UI,Arial;background:#525252;color:#e6e6e6}");
        sb.AppendLine("table{border-collapse:collapse;width:100%}th,td{border:1px solid #2e2e2e;padding:4px 8px}");
        sb.AppendLine("th{background:#3c3c3c;text-align:left}tr:nth-child(even){background:#5a5a5a}</style>");
        sb.AppendLine("</head><body><h1>Programas instalados</h1><table>");
        sb.AppendLine("<tr><th>Programa</th><th>Tamanho</th><th>Versão</th><th>Tipo</th><th>Data</th>"
            + "<th>Empresa</th><th>Página Inicial</th><th>Comentários</th><th>Desinstalador</th></tr>");
        foreach (var p in programs)
        {
            sb.AppendLine("<tr>"
                + $"<td>{Html(p.DisplayName)}</td><td>{Html(p.SizeText)}</td><td>{Html(p.VersionOrNoVersion)}</td>"
                + $"<td>{Html(p.Bits)}</td><td>{Html(p.InstallDate)}</td><td>{Html(p.Publisher)}</td>"
                + $"<td>{Html(p.UrlInfoAbout)}</td><td>{Html(p.Comments)}</td>"
                + $"<td>{Html(p.UninstallString)}</td></tr>");
        }
        sb.AppendLine("</table></body></html>");
        return sb.ToString();
    }

    public static string ToXml(IEnumerable<InstalledProgram> programs)
    {
        var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, settings))
        {
            writer.WriteStartElement("TranalUninstaller");
            writer.WriteAttributeString("exported", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"));
            foreach (var p in programs)
            {
                writer.WriteStartElement("Program");
                writer.WriteElementString("Name", p.DisplayName);
                writer.WriteElementString("Version", p.VersionOrNoVersion);
                writer.WriteElementString("Size", p.SizeText);
                writer.WriteElementString("Type", p.Bits);
                writer.WriteElementString("Date", p.InstallDate);
                writer.WriteElementString("Company", p.Publisher);
                writer.WriteElementString("HomePage", p.UrlInfoAbout);
                writer.WriteElementString("Comments", p.Comments);
                writer.WriteElementString("UninstallString", p.UninstallString);
                writer.WriteElementString("RegistryKey", p.RegistryKey);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
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
