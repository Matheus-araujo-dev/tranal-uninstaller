using System.IO;
using System.Text.Json;

namespace Tranal.Uninstaller.Core.Modules;

/// <summary>
/// O estado do servico de rastreio.
/// </summary>
/// <param name="Version">A versao do servico.</param>
/// <param name="IsUpToDate">A versao gravada e a atual?</param>
/// <param name="LatestKnownVersion">A versao que o Tranal conhece.</param>
/// <param name="PromptShown">O usuario ja viu o aviso de atualizacao?</param>
public sealed record TraceServiceState(
    string Version,
    bool IsUpToDate,
    string LatestKnownVersion,
    bool PromptShown);

/// <summary>
/// O arquivo do servico de rastreio.
/// </summary>
internal sealed class TraceServiceFile
{
    public string Version { get; set; } = string.Empty;
    public bool PromptShown { get; set; }
}

/// <summary>
/// O servico de rastreio, com a sua versao.
///
/// O rastreio de instalacao depende de um servico (o que observa o que muda
/// no disco). O servico evolui: quando a versao gravada e mais velha que a
/// que o Tranal conhece, o Tranal mostra o aviso de atualizacao — e nao
/// atualiza sozinho.
///
/// A regra esta em um lugar so: este modulo sabe a versao do servico e o
/// estado do aviso.
/// </summary>
public static class TraceService
{
    /// <summary>
    /// A versao do servico que este Tranal usa.
    /// </summary>
    public const string CurrentVersion = "1.1.0";

    /// <summary>
    /// Substitui a pasta de dados (so a suite de testes). Nulo em uso normal.
    /// </summary>
    internal static string? FolderOverride { get; set; }

    /// <summary>A pasta onde fica o estado do servico.</summary>
    public static string Folder =>
        FolderOverride ?? Path.Combine(RegistryBackup.DataDirectory, "trace-service");

    /// <summary>O arquivo do estado do servico.</summary>
    public static string FilePath => Path.Combine(Folder, "service.json");

    /// <summary>
    /// O estado do servico: a versao gravada e o aviso ja mostrado.
    /// </summary>
    public static TraceServiceState State()
    {
        var gravado = Read();

        return new TraceServiceState(
            gravado?.Version ?? string.Empty,
            string.Equals(gravado?.Version, CurrentVersion, StringComparison.Ordinal),
            CurrentVersion,
            gravado?.PromptShown ?? false);
    }

    /// <summary>
    /// Ha um aviso de atualizacao a mostrar?
    /// </summary>
    public static bool NeedsUpdatePrompt() => !State().IsUpToDate && !State().PromptShown;

    /// <summary>
    /// Grava a versao do servico (a gravacao acontece quando o servico
    /// 실제로 sobe).
    /// </summary>
    public static bool SaveVersion(string? version = null)
    {
        try
        {
            Directory.CreateDirectory(Folder);

            var arquivo = new TraceServiceFile
            {
                Version = string.IsNullOrWhiteSpace(version) ? CurrentVersion : version!,
                PromptShown = false,
            };

            File.WriteAllText(FilePath, JsonSerializer.Serialize(arquivo, new JsonSerializerOptions
            {
                WriteIndented = true,
            }));

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Marca que o aviso de atualizacao ja foi mostrado.
    /// </summary>
    public static bool MarkPromptShown()
    {
        try
        {
            var gravado = Read() ?? new TraceServiceFile { Version = CurrentVersion };
            gravado.PromptShown = true;

            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(gravado, new JsonSerializerOptions
            {
                WriteIndented = true,
            }));

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static TraceServiceFile? Read()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;

            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<TraceServiceFile>(json);
        }
        catch
        {
            return null;
        }
    }
}
