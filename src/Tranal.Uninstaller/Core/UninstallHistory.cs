using System.IO;
using System.Text;
using System.Text.Json;

namespace Tranal.Uninstaller.Core;

/// <summary>
/// Registro de uma operacao de desinstalacao concluida.
///
/// E o historico do Revo ("Uninstall History") e tambem a base do "Recuperar":
/// cada entrada sabe qual programa saiu, o que saiu junto e onde esta o
/// backup que permite devolver. Um registro so — a interface, a linha de
/// comando e a recuperacao leem este mesmo objeto.
/// </summary>
public sealed class UninstallHistoryEntry
{
    /// <summary>
    /// O identificador da entrada. Vem do nome do arquivo, entao nao ha dois
    /// iguais.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Quando a operacao aconteceu.</summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;

    /// <summary>Os programas desinstalados (varios, quando a operacao e em lote).</summary>
    public string Programs { get; init; } = string.Empty;

    /// <summary>O modo da desinstalacao (o nivel do assistente).</summary>
    public string Level { get; init; } = string.Empty;

    /// <summary>O que o desinstalador nativo disse.</summary>
    public string BuiltIn { get; init; } = string.Empty;

    /// <summary>O relatorio da remocao.</summary>
    public string Report { get; init; } = string.Empty;

    /// <summary>Quantos itens foram removidos.</summary>
    public int Removed { get; init; }

    /// <summary>Quantos itens falharam ou ficaram protegidos.</summary>
    public int Failed { get; init; }

    /// <summary>
    /// True quando o desinstalador nativo rodou e o programa saiu do
    /// computador.
    /// </summary>
    public bool Uninstalled { get; init; }

    /// <summary>A versao do programa.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>A editora do programa.</summary>
    public string Publisher { get; init; } = string.Empty;

    /// <summary>A pasta de instalacao do programa.</summary>
    public string InstallLocation { get; init; } = string.Empty;

    /// <summary>Os itens de sobra que foram removidos.</summary>
    public List<string> RemovedItems { get; init; } = new();

    /// <summary>
    /// O identificador do backup das sobras, quando houve. E o que permite
    /// devolver as coisas ("Recuperar").
    /// </summary>
    public string? RecoveryId { get; init; }

    /// <summary>
    /// O caminho do perfil do programa (o "restore profile"), quando houve.
    /// </summary>
    public string? ProfilePath { get; init; }
}

/// <summary>
/// Historico de desinstalacoes do Tranal (equivalente ao "Uninstall History" do Revo).
/// Cada operacao vira uma linha JSON em %LOCALAPPDATA%\Tranal Group\Tranal Uninstaller Pro\history.jsonl.
///
/// O arquivo e uma linha JSON por operacao: uma linha corrompida estraga UMA
/// entrada, nao o historico inteiro — a mesma propriedade do
/// <see cref="ProgramCustomizationStore"/>: o que nao pode ser lido e
/// preservado, nunca sobrescrito.
/// </summary>
public static class UninstallHistory
{
    /// <summary>
    /// Substitui o caminho do historico (so a suite de testes).
    /// </summary>
    internal static string? FilePathOverride { get; set; }

    /// <summary>O caminho do arquivo de historico.</summary>
    public static string FilePath =>
        FilePathOverride ?? Path.Combine(RegistryBackup.DataDirectory, "history.jsonl");

    /// <summary>
    /// Acrescenta uma entrada ao historico.
    /// </summary>
    /// <param name="entry">A entrada a gravar.</param>
    public static void Append(UninstallHistoryEntry entry)
    {
        if (entry is null) return;

        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            if (entry.Id.Length == 0)
            {
                var copia = new UninstallHistoryEntry
                {
                    Id = $"{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6]}",
                    Timestamp = entry.Timestamp,
                    Programs = entry.Programs,
                    Level = entry.Level,
                    BuiltIn = entry.BuiltIn,
                    Report = entry.Report,
                    Removed = entry.Removed,
                    Failed = entry.Failed,
                    Uninstalled = entry.Uninstalled,
                    Version = entry.Version,
                    Publisher = entry.Publisher,
                    InstallLocation = entry.InstallLocation,
                    RemovedItems = entry.RemovedItems,
                    RecoveryId = entry.RecoveryId,
                    ProfilePath = entry.ProfilePath,
                };
                entry = copia;
            }

            var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions
            {
                WriteIndented = false,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });

            File.AppendAllText(FilePath, json + Environment.NewLine, Encoding.UTF8);
        }
        catch { /* historico e best effort: nunca deve derrubar a desinstalacao */ }
    }

    /// <summary>Le as ultimas entradas do historico (mais recentes primeiro).</summary>
    public static IReadOnlyList<UninstallHistoryEntry> Read(int take = 50)
    {
        var result = new List<UninstallHistoryEntry>();
        try
        {
            if (!File.Exists(FilePath)) return result;

            var lines = File.ReadAllLines(FilePath);
            for (var i = lines.Length - 1; i >= 0 && result.Count < take; i--)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                try
                {
                    var entry = JsonSerializer.Deserialize<UninstallHistoryEntry>(lines[i]);
                    if (entry is not null) result.Add(entry);
                }
                catch (JsonException)
                {
                    // Uma linha ruim nao derruba as outras: o historico e um
                    // registro do que aconteceu, nao um arquivo de configuracao.
                }
            }
        }
        catch { }
        return result;
    }

    /// <summary>
    /// Procura uma entrada pelo identificador.
    /// </summary>
    /// <param name="id">O identificador.</param>
    /// <param name="entry">A entrada encontrada.</param>
    public static bool TryGet(string? id, out UninstallHistoryEntry? entry)
    {
        entry = null;
        if (string.IsNullOrWhiteSpace(id)) return false;

        foreach (var candidate in Read(int.MaxValue))
        {
            if (string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                entry = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Apaga o historico inteiro.
    /// </summary>
    public static bool Clear()
    {
        try
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
            return true;
        }
        catch { return false; }
    }
}
