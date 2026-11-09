using System.Management;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// Uma impressora instalada.
/// </summary>
public sealed class PrinterInfo
{
    /// <summary>O nome da impressora.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>O nome do driver.</summary>
    public string Driver { get; init; } = string.Empty;

    /// <summary>A porta (LPT1:, USB001, ...).</summary>
    public string Port { get; init; } = string.Empty;

    /// <summary>True quando a impressora e a padrao do Windows.</summary>
    public bool IsDefault { get; init; }

    /// <summary>True quando a impressora aceita impressao em rede.</summary>
    public bool IsNetwork { get; init; }

    /// <summary>True quando a impressora esta compartilhada.</summary>
    public bool IsShared { get; init; }

    /// <summary>O caminho da pasta de arquivos da impressora.</summary>
    public string Location { get; init; } = string.Empty;

    /// <summary>Quantos trabalhos estao na fila.</summary>
    public int QueuedJobs { get; init; }
}

/// <summary>
/// Um trabalho de impressao na fila.
/// </summary>
public sealed class PrintJobInfo
{
    /// <summary>O nome do documento.</summary>
    public string Document { get; init; } = string.Empty;

    /// <summary>O dono do trabalho.</summary>
    public string Owner { get; init; } = string.Empty;

    /// <summary>A impressora que vai imprimir.</summary>
    public string Printer { get; init; } = string.Empty;

    /// <summary>A data em que o trabalho foi enviado.</summary>
    public DateTime SubmittedAt { get; init; }

    /// <summary>O tamanho do trabalho, em paginas (quando o Windows sabe).</summary>
    public int Pages { get; init; }
}

/// <summary>
/// Enumera as impressoras e a fila de impressao.
///
/// O Soft Organizer tem "enumeracao de impressoras e fila" e o Uninstall
/// Tool avisa quando a fila de impressao tem trabalho preso.
///
/// A REGRA mora aqui, em um lugar so: a consulta das impressoras e da fila
/// fica aqui, lendo o WMI do Windows (a mesma interface que ja e usada pelo
/// painel de sistema). Quem mostra consome o veredito.
///
/// Remover a fila e uma acao do Windows, nao do Tranal: quem precisa pode
/// usar a propria ferramenta do sistema.
/// </summary>
public static class PrinterEnumerator
{
    /// <summary>
    /// As impressoras instaladas.
    /// </summary>
    public static IReadOnlyList<PrinterInfo> List()
    {
        var impressoras = new List<PrinterInfo>();

        try
        {
            using var busca = new ManagementObjectSearcher("SELECT * FROM Win32_Printer");
            using var resultados = busca.Get();

            foreach (var item in resultados)
            {
                using var objeto = item;

                var nome = ReadString(objeto, "Name");
                if (nome.Length == 0) continue;

                impressoras.Add(new PrinterInfo
                {
                    Name = nome,
                    Driver = ReadString(objeto, "DriverName"),
                    Port = ReadString(objeto, "PortName"),
                    IsDefault = ReadBool(objeto, "Default"),
                    IsNetwork = ReadBool(objeto, "Network"),
                    IsShared = ReadBool(objeto, "Shared"),
                    Location = ReadString(objeto, "Location"),
                });
            }
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao enumerar as impressoras: {ex.Message}");
        }

        return impressoras.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Os trabalhos que estao na fila de impressao.
    /// </summary>
    public static IReadOnlyList<PrintJobInfo> ListQueue()
    {
        var trabalhos = new List<PrintJobInfo>();

        try
        {
            using var busca = new ManagementObjectSearcher("SELECT * FROM Win32_PrintJob");
            using var resultados = busca.Get();

            foreach (var item in resultados)
            {
                using var objeto = item;

                var documento = ReadString(objeto, "Name");
                if (documento.Length == 0) continue;

                trabalhos.Add(new PrintJobInfo
                {
                    Document = documento,
                    Owner = ReadString(objeto, "Owner"),
                    Printer = ReadString(objeto, "Name"),
                    SubmittedAt = ReadDate(objeto, "TimeSubmitted"),
                    Pages = ReadInt(objeto, "PagesPrinted"),
                });
            }
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao enumerar a fila de impressao: {ex.Message}");
        }

        return trabalhos.OrderBy(t => t.Document, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// As impressoras que tem algum trabalho na fila.
    /// </summary>
    public static IReadOnlyList<PrinterInfo> ListWithQueue()
    {
        var fila = ListQueue();
        var porNome = fila
            .GroupBy(t => t.Printer, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        return List()
            .Select(p => new PrinterInfo
            {
                Name = p.Name,
                Driver = p.Driver,
                Port = p.Port,
                IsDefault = p.IsDefault,
                IsNetwork = p.IsNetwork,
                IsShared = p.IsShared,
                Location = p.Location,
                QueuedJobs = porNome.TryGetValue(p.Name, out var total) ? total : 0,
            })
            .ToList();
    }

    /// <summary>
    /// Lê uma propriedade de texto do WMI.
    /// </summary>
    private static string ReadString(ManagementBaseObject objeto, string propriedade)
    {
        try
        {
            var valor = objeto[propriedade];
            return valor?.ToString()?.Trim() ?? string.Empty;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao ler '{propriedade}': {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Lê uma propriedade booleana do WMI.
    /// </summary>
    private static bool ReadBool(ManagementBaseObject objeto, string propriedade)
    {
        var texto = ReadString(objeto, propriedade);
        return bool.TryParse(texto, out var valor) && valor;
    }

    /// <summary>
    /// Lê uma propriedade numerica do WMI.
    /// </summary>
    private static int ReadInt(ManagementBaseObject objeto, string propriedade)
    {
        var texto = ReadString(objeto, propriedade);
        return int.TryParse(texto, out var valor) ? valor : 0;
    }

    /// <summary>
    /// Lê uma propriedade de data do WMI.
    /// </summary>
    private static DateTime ReadDate(ManagementBaseObject objeto, string propriedade)
    {
        try
        {
            var valor = objeto[propriedade];
            if (valor is null) return DateTime.MinValue;

            if (valor is DateTime data) return data;

            var texto = valor.ToString();
            return DateTime.TryParse(texto, out var resultado) ? resultado : DateTime.MinValue;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao ler '{propriedade}': {ex.Message}");
            return DateTime.MinValue;
        }
    }
}
