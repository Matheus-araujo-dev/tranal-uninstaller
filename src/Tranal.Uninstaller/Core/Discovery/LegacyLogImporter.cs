using System.Globalization;
using System.IO;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// Uma entrada importada de um log antigo.
/// </summary>
/// <param name="ProgramName">O nome do programa.</param>
/// <param name="Action">O que o log registra (instalado, removido...).</param>
/// <param name="When">Quando aconteceu.</param>
/// <param name="Detail">O detalhe da linha.</param>
public sealed record LegacyLogEntry(
    string ProgramName,
    string Action,
    DateTime? When,
    string Detail);

/// <summary>
/// A importacao dos logs do UnInstaller 4.
///
/// O UnInstaller 4 (o antepassado do Ashampoo) gravava um log por sessao, em
/// texto. Tranal consegue ler esse formato: e o que permite mostrar ao
/// usuario o que aconteceu nas desinstalacoes de antes.
///
/// O formato lido e o proprio do log: data no comeco da linha, o nome do
/// programa e a acao. Uma linha que nao segue o formato e ignorada — e o
/// numero de linhas lidas e o de linhas aceitas aparecem no resultado.
///
/// A regra esta em um lugar so: este modulo sabe ler o log antigo.
/// </summary>
public static class LegacyLogImporter
{
    /// <summary>
    /// O nome do programa de onde vem o log.
    /// </summary>
    public const string SourceName = "UnInstaller 4";

    /// <summary>
    /// Le um log antigo e devolve as entradas reconhecidas.
    /// </summary>
    /// <param name="path">O arquivo de log.</param>
    public static IReadOnlyList<LegacyLogEntry> Import(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return Array.Empty<LegacyLogEntry>();

        try
        {
            if (!File.Exists(path)) return Array.Empty<LegacyLogEntry>();

            var entradas = new List<LegacyLogEntry>();

            foreach (var linha in File.ReadAllLines(path))
            {
                var entrada = ParseLine(linha);
                if (entrada is not null) entradas.Add(entrada);
            }

            return entradas;
        }
        catch
        {
            return Array.Empty<LegacyLogEntry>();
        }
    }

    /// <summary>
    /// O texto do relatorio da importacao.
    /// </summary>
    /// <param name="path">O arquivo de log.</param>
    public static string Describe(string? path)
    {
        var entradas = Import(path);
        return $"{entradas.Count} entrada(s) importada(s) de {SourceName}.";
    }

    /// <summary>
    /// Le uma linha do log. Devolve nulo quando a linha nao segue o formato.
    /// </summary>
    private static LegacyLogEntry? ParseLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        var texto = line.Trim();

        // As linhas de comentario e os cabecalhos do proprio log ficam de
        // fora: elas nao descrevem um programa.
        if (texto.StartsWith("#", StringComparison.Ordinal)) return null;
        if (texto.StartsWith("[", StringComparison.Ordinal)) return null;

        // A data do comeco: "2024-05-06 12:34:56" ou "2024-05-06".
        var quando = default(DateTime?);
        var resto = texto;

        if (DateTime.TryParseExact(texto[..Math.Min(19, texto.Length)],
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dataCompleta))
        {
            quando = dataCompleta;
            resto = texto[19..].Trim();
        }
        else if (DateTime.TryParseExact(texto[..Math.Min(10, texto.Length)],
                     "yyyy-MM-dd",
                     CultureInfo.InvariantCulture,
                     DateTimeStyles.None,
                     out var data))
        {
            quando = data;
            resto = texto[10..].Trim();
        }

        // O separador pode vir logo depois da data.
        resto = resto.TrimStart(';', '|', '\t').TrimStart();

        if (resto.Length == 0) return null;

        // Depois da data vem o nome do programa e a acao, separados por
        // ponto e virgula ou por barra.
        var separador = resto.IndexOfAny(new[] { ';', '|', '\t' });
        if (separador <= 0) return null;

        var programa = resto[..separador].Trim();
        var acao = resto[(separador + 1)..].Trim();

        if (programa.Length == 0) return null;

        return new LegacyLogEntry(programa, acao, quando, resto);
    }
}
