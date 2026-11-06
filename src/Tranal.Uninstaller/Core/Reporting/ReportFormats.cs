using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using Microsoft.Win32;

namespace Tranal.Uninstaller.Core.Reporting;

/// <summary>
/// O que um arquivo <c>.reg</c> deve fazer com uma chave ou com um valor.
///
/// O formato do Editor do Registro do Windows tem duas formas de apagar:
/// o valor some pelo nome (<c>"Nome"=-</c>) e a chave inteira some pelo
/// cabecalho da secao (<c>[-HKEY_...]</c>). Sao operacoes diferentes e a
/// interface precisa distinguir as duas.
/// </summary>
public enum RegistryChangeOperation
{
    /// <summary>Cria a chave (sem valor nenhum).</summary>
    CreateKey,

    /// <summary>Grava (ou sobrescreve) um valor da chave.</summary>
    SetValue,

    /// <summary>Apaga o valor da chave ("-" no lugar do dado).</summary>
    DeleteValue,

    /// <summary>Apaga a chave inteira ("-" antes do nome da chave).</summary>
    DeleteKey,
}

/// <summary>
/// Uma alteracao de registro pronta para o formato <c>.reg</c> do Windows.
///
/// O tipo do dado segue o <see cref="RegistryValueKind"/> do proprio .NET:
/// assim quem monta a alteracao nao precisa inventar um codigo de tipo, e
/// quem escreve o arquivo nao precisa adivinhar o tipo de novo.
/// </summary>
public sealed record RegistryChange
{
    /// <summary>
    /// O caminho da chave, no formato do regedit
    /// (ex.: <c>HKEY_CURRENT_USER\Software\MeuPrograma</c>).
    /// </summary>
    public string KeyPath { get; init; } = string.Empty;

    /// <summary>
    /// O nome do valor. Vazio significa o valor padrao da chave — o "@" do
    /// formato <c>.reg</c>.
    /// </summary>
    public string ValueName { get; init; } = string.Empty;

    /// <summary>O que fazer com a chave ou com o valor.</summary>
    public RegistryChangeOperation Operation { get; init; } = RegistryChangeOperation.SetValue;

    /// <summary>O tipo do valor. Só importa quando a operacao grava um valor.</summary>
    public RegistryValueKind ValueKind { get; init; } = RegistryValueKind.String;

    /// <summary>
    /// O dado do valor, conforme o tipo:
    /// <list type="bullet">
    /// <item><c>String</c> e <c>ExpandString</c>: o proprio texto;</item>
    /// <item><c>Dword</c> e <c>Qword</c>: o numero, em decimal ou em
    /// hexadecimal com o prefixo <c>0x</c>;</item>
    /// <item><c>Binary</c>: os bytes em hexadecimal, com ou sem separadores
    /// (ex.: <c>00,1A,FF</c>);</item>
    /// <item><c>MultiString</c>: os textos separados por <c>\0</c>.</item>
    /// </list>
    /// </summary>
    public string Data { get; init; } = string.Empty;
}

/// <summary>
/// Os formatos de relatorio que o Tranal produz.
///
/// Excel, <c>.reg</c> e script em lote sao gerados AQUI, no nucleo, e nao
/// na interface: a GUI e a CLI precisam escrever o mesmo arquivo, e um
/// arquivo escrito por dois codigos diferentes diverge na primeira
/// mudanca. O TXT/CSV/HTML/XML que ja existia continua mora em
/// <see cref="Exporters"/> — a porta de entrada da exportacao e uma so.
///
/// O Excel sai em **SpreadsheetML 2003** (XML puro): e o unico formato que
/// o Excel abre nativamente sem o Excel instalado, e continua sendo texto
/// plano auditavel. Ver o relatório do lote para o comparativo.
/// </summary>
public static class ReportFormats
{
    private const string SpreadsheetNs = "urn:schemas-microsoft-com:office:spreadsheet";
    private const string OfficeNs = "urn:schemas-microsoft-com:office:office";
    private const string ExcelNs = "urn:schemas-microsoft-com:office:excel";
    private const string HtmlNs = "http://www.w3.org/TR/REC-html40";

    /// <summary>
    /// Exporta a lista de programas para o Excel, em SpreadsheetML 2003.
    ///
    /// A lista e as colunas sao as MESMAS do exportador de TXT/CSV/HTML/XML
    /// (<see cref="Exporters.DefaultColumns"/> e
    /// <see cref="ProgramColumns.Value"/>): nao existe uma segunda lista de
    /// colunas para o Excel.
    /// </summary>
    /// <param name="programs">Os programas exportados.</param>
    /// <param name="columns">
    /// As colunas. Nulo ou vazio usa o conjunto padrao do exportador.
    /// </param>
    public static string ToExcel(IReadOnlyList<InstalledProgram> programs, IReadOnlyList<ProgramColumn>? columns)
    {
        var sel = ProgramRows.ColumnsOf(columns);
        var values = ProgramRows.Values(programs, sel);

        var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
        using var stream = new MemoryStream();

        using (var writer = XmlWriter.Create(stream, settings))
        {
            writer.WriteProcessingInstruction("xml", "version=\"1.0\"");
            writer.WriteProcessingInstruction("mso-application", "progid=\"Excel.Sheet\"");

            writer.WriteStartElement("", "Workbook", SpreadsheetNs);
            writer.WriteAttributeString("xmlns", "o", null, OfficeNs);
            writer.WriteAttributeString("xmlns", "x", null, ExcelNs);
            writer.WriteAttributeString("xmlns", "ss", null, SpreadsheetNs);
            writer.WriteAttributeString("xmlns", "html", null, HtmlNs);

            // O unico estilo do arquivo: o cabecalho em negrito.
            writer.WriteStartElement("", "Styles", SpreadsheetNs);
            writer.WriteStartElement("", "Style", SpreadsheetNs);
            writer.WriteAttributeString("ss", "ID", SpreadsheetNs, "Cabecalho");
            writer.WriteStartElement("", "Font", SpreadsheetNs);
            writer.WriteAttributeString("ss", "Bold", SpreadsheetNs, "1");
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();

            writer.WriteStartElement("", "Worksheet", SpreadsheetNs);
            writer.WriteAttributeString("ss", "Name", SpreadsheetNs, "Programas");
            writer.WriteStartElement("", "Table", SpreadsheetNs);

            // O cabecalho usa o nome fixo da coluna (como o CSV e o XML): o
            // arquivo precisa continuar legivel por qualquer ferramenta,
            // independente do idioma da interface.
            WriteRow(writer, sel.Select(c => ProgramColumns.Info(c).ExportName).ToList(), true);

            foreach (var row in values) WriteRow(writer, row, false);

            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Gera o conteudo de um arquivo <c>.reg</c> a partir das alteracoes.
    ///
    /// O formato e o do proprio Windows: cabecalho
    /// <c>Windows Registry Editor Version 5.00</c>, secoes
    /// <c>[HKEY_...]</c>, <c>[-HKEY_...]</c> para apagar a chave e
    /// <c>"Nome"=-</c> para apagar o valor.
    /// </summary>
    /// <param name="changes">As alteracoes, na ordem em que devem aparecer.</param>
    public static string ToReg(IEnumerable<RegistryChange> changes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Windows Registry Editor Version 5.00");
        sb.AppendLine();

        string? chaveAtual = null;
        var abriuSecao = false;

        foreach (var change in changes ?? Enumerable.Empty<RegistryChange>())
        {
            if (change is null) continue;

            var key = (change.KeyPath ?? string.Empty).Trim();
            if (key.Length == 0) continue;

            if (!string.Equals(key, chaveAtual, StringComparison.OrdinalIgnoreCase))
            {
                if (abriuSecao) sb.AppendLine();

                chaveAtual = key;
                sb.AppendLine(change.Operation == RegistryChangeOperation.DeleteKey
                    ? $"[-{key}]"
                    : $"[{key}]");
                abriuSecao = true;
            }

            switch (change.Operation)
            {
                case RegistryChangeOperation.DeleteValue:
                    sb.AppendLine($"\"{ValueLabel(change.ValueName)}\"=-");
                    break;

                case RegistryChangeOperation.SetValue:
                    sb.AppendLine($"\"{ValueLabel(change.ValueName)}\"={FormatRegValue(change.ValueKind, change.Data)}");
                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Grava o arquivo <c>.reg</c> no destino informado.
    ///
    /// O arquivo sai em **UTF-16 LE com BOM**: e o que o <c>reg.exe</c> e o
    /// Editor do Registro esperam, e o mesmo que o backup de registro ja usa
    /// (<see cref="RegistryBackup"/>).
    /// </summary>
    /// <param name="path">O caminho do arquivo a gravar.</param>
    /// <param name="changes">As alteracoes.</param>
    /// <returns>O caminho gravado, ou string vazia quando falhou.</returns>
    public static string WriteReg(string path, IEnumerable<RegistryChange> changes)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;

            var destino = Path.GetFullPath(path);
            var pasta = Path.GetDirectoryName(destino);
            if (!string.IsNullOrEmpty(pasta)) Directory.CreateDirectory(pasta);

            File.WriteAllText(destino, ToReg(changes), new UnicodeEncoding(bigEndian: false, byteOrderMark: true));
            return destino;
        }
        catch
        {
            // Falha de gravacao: quem chama precisa avisar o usuario, e o
            // destino fica vazio para isso.
            return string.Empty;
        }
    }

    /// <summary>
    /// Gera o script de desinstalacao em lote dos programas indicados.
    ///
    /// O script sai em <c>.cmd</c> e usa o comando de desinstalacao de cada
    /// programa — o silencioso quando existe, o registrado quando nao.
    /// </summary>
    /// <param name="programs">Os programas a desinstalar.</param>
    public static string ToBatchScript(IEnumerable<InstalledProgram> programs) =>
        BatchScriptWriter.Build(programs, silent: true);

    /// <summary>
    /// O dado de um valor no formato <c>.reg</c>, conforme o tipo.
    ///
    /// As formas são as mesmas que o Editor do Registro grava:
    /// <c>"texto"</c> para texto, <c>hex(2):</c> para texto expansivel,
    /// <c>hex:</c> para binario, <c>hex(7):</c> para varios textos,
    /// <c>dword:</c> para 32 bits e <c>hex(b):</c> para 64 bits.
    /// </summary>
    /// <param name="kind">O tipo do valor.</param>
    /// <param name="data">O dado, no formato descrito em <see cref="RegistryChange.Data"/>.</param>
    public static string FormatRegValue(RegistryValueKind kind, string data)
    {
        var text = data ?? string.Empty;

        switch (kind)
        {
            case RegistryValueKind.DWord:
                return "dword:" + ToUInt32(text).ToString("X8", CultureInfo.InvariantCulture);

            case RegistryValueKind.QWord:
                return "hex(b):" + ToUInt64(text).ToString("X16", CultureInfo.InvariantCulture);

            case RegistryValueKind.Binary:
            {
                var bytes = TryParseHex(text);
                // Dado que nao e hexadecimal nao vira binario furado: ele sai
                // como texto, que e o que o dado realmente e.
                if (bytes is null) return "\"" + EscapeRegText(text) + "\"";
                return "hex:" + string.Join(",", bytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
            }

            case RegistryValueKind.MultiString:
            {
                var bytes = MultiStringBytes(text);
                return "hex(7):" + string.Join(",", bytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
            }

            case RegistryValueKind.ExpandString:
            {
                var bytes = Encoding.Unicode.GetBytes(text);
                return "hex(2):" + string.Join(",", bytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
            }

            case RegistryValueKind.String:
            case RegistryValueKind.Unknown:
            default:
                return "\"" + EscapeRegText(text) + "\"";
        }
    }

    /// <summary>
    /// O nome do valor no formato <c>.reg</c>: o valor padrao da chave e o
    /// "@", e qualquer outro nome vai entre aspas.
    /// </summary>
    /// <param name="valueName">O nome do valor.</param>
    private static string ValueLabel(string? valueName)
    {
        var name = (valueName ?? string.Empty).Trim();
        if (name.Length == 0) return "@";
        return name.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    /// <summary>
    /// Escapa o texto de um valor do <c>.reg</c>: a barra invertida e a
    /// aspas viram <c>\\</c> e <c>\"</c>.
    ///
    /// A quebra de linha vira <c>\r</c> e <c>\n</c> — o formato <c>.reg</c>
    /// e linha a linha, e um enter solto quebraria o arquivo.
    /// </summary>
    /// <param name="text">O texto do valor.</param>
    private static string EscapeRegText(string text) => text
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"")
        .Replace("\r", "\\r")
        .Replace("\n", "\\n");

    /// <summary>
    /// Os bytes de um valor de varios textos: cada texto termina com o
    /// terminador nulo, como o formato <c>hex(7)</c> exige.
    /// </summary>
    /// <param name="text">Os textos separados por <c>\0</c>.</param>
    private static byte[] MultiStringBytes(string text)
    {
        var partes = text.Split('\0');
        var bytes = new List<byte>();
        foreach (var parte in partes)
        {
            bytes.AddRange(Encoding.Unicode.GetBytes(parte));
            bytes.AddRange(new byte[] { 0x00, 0x00 });
        }

        return bytes.ToArray();
    }

    /// <summary>
    /// Converte um numero em 32 bits, aceitando decimal ou hexadecimal com
    /// o prefixo <c>0x</c>.
    /// </summary>
    /// <param name="text">O numero.</param>
    private static uint ToUInt32(string text)
    {
        var limpo = text.Trim();

        // O prefixo "0x" e a unica forma de pedir hexadecimal: sem ele, o
        // numero e decimal ("16" sao dezesseis, nao vinte e dois).
        if (limpo.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return uint.TryParse(limpo[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex)
                ? hex
                : 0u;
        }

        if (uint.TryParse(limpo, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dec)) return dec;
        if (uint.TryParse(limpo, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex2)) return hex2;
        return 0u;
    }

    /// <summary>
    /// Converte um numero em 64 bits, aceitando decimal ou hexadecimal com
    /// o prefixo <c>0x</c>.
    /// </summary>
    /// <param name="text">O numero.</param>
    private static ulong ToUInt64(string text)
    {
        var limpo = text.Trim();

        // O prefixo "0x" e a unica forma de pedir hexadecimal: sem ele, o
        // numero e decimal.
        if (limpo.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return ulong.TryParse(limpo[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex)
                ? hex
                : 0UL;
        }

        if (ulong.TryParse(limpo, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dec)) return dec;
        if (ulong.TryParse(limpo, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex2)) return hex2;
        return 0UL;
    }

    /// <summary>
    /// Os bytes de um dado hexadecimal, ou nulo quando o dado nao e
    /// hexadecimal.
    /// </summary>
    /// <param name="text">O dado.</param>
    private static byte[]? TryParseHex(string text)
    {
        var limpo = new string((text ?? string.Empty)
            .Where(c => !char.IsWhiteSpace(c) && c != ',' && c != '-' && c != '_')
            .ToArray());

        if (limpo.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) limpo = limpo[2..];
        if (limpo.Length == 0 || limpo.Length % 2 != 0) return null;

        var bytes = new byte[limpo.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            if (!byte.TryParse(limpo.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
                return null;
            bytes[i] = b;
        }

        return bytes;
    }

    /// <summary>
    /// Escreve uma linha da tabela do Excel.
    /// </summary>
    /// <param name="writer">O escritor do XML.</param>
    /// <param name="values">Os valores das celulas.</param>
    /// <param name="cabecalho">True quando a linha e o cabecalho.</param>
    private static void WriteRow(XmlWriter writer, IReadOnlyList<string> values, bool cabecalho)
    {
        writer.WriteStartElement("", "Row", SpreadsheetNs);

        foreach (var value in values)
        {
            writer.WriteStartElement("", "Cell", SpreadsheetNs);
            if (cabecalho) writer.WriteAttributeString("ss", "StyleID", SpreadsheetNs, "Cabecalho");

            writer.WriteStartElement("", "Data", SpreadsheetNs);
            writer.WriteAttributeString("ss", "Type", SpreadsheetNs, IsNumber(value) ? "Number" : "String");
            writer.WriteString(value ?? string.Empty);
            writer.WriteEndElement();

            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    /// <summary>
    /// Verdadeiro quando o valor e um numero — o Excel trata como texto
    /// quando nao e.
    /// </summary>
    /// <param name="value">O valor.</param>
    private static bool IsNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
    }
}

/// <summary>
/// As linhas da lista de programas, do jeito que todo relatorio precisa
/// delas.
///
/// Existe para que a exportacao para o Excel, o layout de impressao e o
/// script leiam a MESMA lista e as MESMAS colunas: o valor de uma coluna
/// vem sempre de <see cref="ProgramColumns.Value"/>, e as colunas vem sempre
/// de <see cref="Exporters.DefaultColumns"/> quando o chamador nao escolhe
/// nenhuma.
/// </summary>
internal static class ProgramRows
{
    /// <summary>
    /// As colunas a usar: as pedidas ou, na falta delas, o conjunto padrao
    /// do exportador.
    /// </summary>
    /// <param name="columns">As colunas pedidas.</param>
    public static IReadOnlyList<ProgramColumn> ColumnsOf(IReadOnlyList<ProgramColumn>? columns) =>
        columns is null || columns.Count == 0 ? Exporters.DefaultColumns : columns;

    /// <summary>
    /// Os valores de cada programa, na ordem das colunas.
    /// </summary>
    /// <param name="programs">Os programas.</param>
    /// <param name="columns">As colunas.</param>
    public static IReadOnlyList<IReadOnlyList<string>> Values(
        IReadOnlyList<InstalledProgram> programs, IReadOnlyList<ProgramColumn> columns)
    {
        var anotacoes = ProgramCustomizationStore.PeekAll();
        var linhas = new List<IReadOnlyList<string>>();

        foreach (var program in programs ?? Array.Empty<InstalledProgram>())
        {
            if (program is null) continue;

            var anotacao = CustomizationOf(program, anotacoes);
            linhas.Add(columns.Select(c => ProgramColumns.Value(c, program, anotacao)).ToList());
        }

        return linhas;
    }

    /// <summary>
    /// A anotacao do usuario sobre o programa, ou nulo quando ele nao tem
    /// anotacao.
    ///
    /// A lista vem de <see cref="ProgramCustomizationStore.PeekAll"/>: uma
    /// leitura que nao mexe no arquivo do usuario.
    /// </summary>
    /// <param name="program">O programa.</param>
    /// <param name="anotacoes">As anotacoes lidas.</param>
    public static ProgramCustomization? CustomizationOf(
        InstalledProgram program, IReadOnlyList<ProgramCustomization> anotacoes)
    {
        var key = ProgramCustomizationStore.KeyOf(program);
        foreach (var item in anotacoes)
        {
            if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)) return item;
        }

        return null;
    }
}
