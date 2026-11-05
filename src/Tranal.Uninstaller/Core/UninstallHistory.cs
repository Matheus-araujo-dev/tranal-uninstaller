using System.IO;
using System.Text;
using System.Text.Json;

namespace Tranal.Uninstaller.Core;

/// <summary>Registro de uma operacao de desinstalacao concluida.</summary>
public sealed class UninstallHistoryEntry
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string Programs { get; init; } = string.Empty;
    public string Level { get; init; } = string.Empty;
    public string BuiltIn { get; init; } = string.Empty;
    public string Report { get; init; } = string.Empty;
    public int Removed { get; init; }
    public int Failed { get; init; }
}

/// <summary>
/// Historico de desinstalacoes do Tranal (equivalente ao "Uninstall History" do Revo).
/// Cada operacao vira uma linha JSON em %LOCALAPPDATA%\Tranal Group\Tranal Uninstaller Pro\history.jsonl.
/// </summary>
public static class UninstallHistory
{
    public static string FilePath =>
        Path.Combine(RegistryBackup.DataDirectory, "history.jsonl");

    public static void Append(UninstallHistoryEntry entry)
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

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
                var entry = JsonSerializer.Deserialize<UninstallHistoryEntry>(lines[i]);
                if (entry is not null) result.Add(entry);
            }
        }
        catch { }
        return result;
    }
}
