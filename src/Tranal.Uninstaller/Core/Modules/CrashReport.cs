using System.IO;
using System.Text.Json;

namespace Tranal.Uninstaller.Core.Modules;

/// <summary>
/// O relatorio de uma falha do Tranal.
/// </summary>
/// <param name="CreatedAt">Quando a falha aconteceu.</param>
/// <param name="Version">A versao do Tranal.</param>
/// <param name="Action">O que o usuario estava fazendo.</param>
/// <param name="Message">A mensagem da falha.</param>
/// <param name="StackTrace">A pilha da falha.</param>
/// <param name="ReportPath">O arquivo gravado.</param>
public sealed record CrashReportDetails(
    DateTime CreatedAt,
    string Version,
    string Action,
    string Message,
    string StackTrace,
    string ReportPath);

/// <summary>
/// O arquivo do relatorio.
/// </summary>
internal sealed class CrashReportFile
{
    public DateTime CreatedAt { get; set; } = DateTime.MinValue;
    public string Version { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string StackTrace { get; set; } = string.Empty;
}

/// <summary>
/// O relatorio de erro.
///
/// Quando o Tranal falha, o usuario precisa poder dizer o que aconteceu sem
/// contar a historia de memoria. O relatorio junta a versao, o que ele estava
/// fazendo e a mensagem da falha — e grava isso num arquivo.
///
/// O envio pela web **nao** acontece sozinho: o relatorio fica em disco, e o
/// usuario decide o que fazer com ele. Nenhum dado sai da maquina sem a
/// ordem do usuario.
///
/// A regra esta em um lugar so: este modulo sabe o que e um relatorio e onde
/// ele fica.
/// </summary>
public static class CrashReport
{
    /// <summary>
    /// Substitui a pasta de dados (so a suite de testes). Nulo em uso normal.
    /// </summary>
    internal static string? FolderOverride { get; set; }

    /// <summary>A pasta onde ficam os relatorios.</summary>
    public static string Folder =>
        FolderOverride ?? Path.Combine(RegistryBackup.DataDirectory, "crash-reports");

    /// <summary>Os relatorios gravados, do mais novo para o mais antigo.</summary>
    public static IReadOnlyList<string> List()
    {
        try
        {
            if (!Directory.Exists(Folder)) return Array.Empty<string>();

            return Directory.EnumerateFiles(Folder, "*.json")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Grava o relatorio de uma falha.
    /// </summary>
    /// <param name="action">O que o usuario estava fazendo.</param>
    /// <param name="exception">A falha.</param>
    /// <returns>O relatorio gravado, ou nulo quando a gravacao falhou.</returns>
    public static CrashReportDetails? Write(string action, Exception? exception)
    {
        try
        {
            Directory.CreateDirectory(Folder);

            var path = Path.Combine(
                Folder,
                "crash-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".json");

            var file = new CrashReportFile
            {
                CreatedAt = DateTime.Now,
                Version = CurrentVersion(),
                Action = action ?? string.Empty,
                Message = exception?.Message ?? string.Empty,
                StackTrace = exception?.StackTrace ?? string.Empty,
            };

            File.WriteAllText(path, JsonSerializer.Serialize(file, new JsonSerializerOptions
            {
                WriteIndented = true,
            }));

            return new CrashReportDetails(
                file.CreatedAt,
                file.Version,
                file.Action,
                file.Message,
                file.StackTrace,
                path);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// O texto do relatorio, como o usuario copieria para o suporte.
    /// </summary>
    /// <param name="report">O relatorio.</param>
    public static string Describe(CrashReportDetails? report)
    {
        if (report is null) return string.Empty;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Tranal Uninstaller {report.Version}");
        sb.AppendLine($"Quando: {report.CreatedAt:dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine($"O que estava sendo feito: {report.Action}");
        sb.AppendLine($"Falha: {report.Message}");

        if (!string.IsNullOrWhiteSpace(report.StackTrace))
        {
            sb.AppendLine();
            sb.AppendLine(report.StackTrace);
        }

        return sb.ToString();
    }

    private static string CurrentVersion()
    {
        try
        {
            var assembly = typeof(CrashReport).Assembly;
            var version = assembly.GetName().Version;
            return version?.ToString() ?? "desconhecida";
        }
        catch
        {
            return "desconhecida";
        }
    }
}
