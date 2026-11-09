using System.IO;
using System.Text.Json;

namespace Tranal.Uninstaller.Core.Modules;

/// <summary>
/// A ligacao entre um programa instalado e o log de instalacao dele.
/// </summary>
/// <param name="ProgramKey">A identificacao do programa.</param>
/// <param name="ProgramName">O nome do programa.</param>
/// <param name="TraceId">O identificador do log de instalacao.</param>
/// <param name="TracePath">O arquivo do log.</param>
/// <param name="AttachedAt">Quando a ligacao foi feita.</param>
public sealed record TraceAttachmentEntry(
    string ProgramKey,
    string ProgramName,
    string TraceId,
    string TracePath,
    DateTime AttachedAt);

/// <summary>
/// O arquivo das ligacoes.
/// </summary>
internal sealed class AttachmentStore
{
    public List<TraceAttachmentEntry> Items { get; set; } = new();
}

/// <summary>
/// Anexar um log de instalacao a um programa que ja existe.
///
/// Quando o Tranal rastrou uma instalacao e o programa instalado nao tem
/// entrada de desinstalacao, os dois ficam separados: o log e de uma coisa, o
/// programa e de outra. Anexar junta os dois — e o que permite dizer "este
/// log e deste programa" quando o log diz so o nome.
///
/// A regra esta em um lugar so: este modulo sabe o que e uma ligacao e onde
/// elas ficam.
/// </summary>
public static class TraceAttachment
{
    /// <summary>
    /// Substitui a pasta de dados (so a suite de testes). Nulo em uso normal.
    /// </summary>
    internal static string? FolderOverride { get; set; }

    /// <summary>A pasta onde ficam as ligacoes.</summary>
    public static string Folder =>
        FolderOverride ?? Path.Combine(RegistryBackup.DataDirectory, "attachments");

    /// <summary>O arquivo das ligacoes.</summary>
    public static string FilePath => Path.Combine(Folder, "attachments.json");

    /// <summary>As ligacoes gravadas.</summary>
    public static IReadOnlyList<TraceAttachmentEntry> List()
    {
        try
        {
            if (!File.Exists(FilePath)) return Array.Empty<TraceAttachmentEntry>();

            var json = File.ReadAllText(FilePath);
            var loaded = JsonSerializer.Deserialize<AttachmentStore>(json);
            return loaded?.Items ?? (IReadOnlyList<TraceAttachmentEntry>)Array.Empty<TraceAttachmentEntry>();
        }
        catch
        {
            return Array.Empty<TraceAttachmentEntry>();
        }
    }

    /// <summary>
    /// Anexa um log de instalacao a um programa.
    /// </summary>
    /// <param name="programKey">A identificacao do programa.</param>
    /// <param name="programName">O nome do programa.</param>
    /// <param name="traceId">O identificador do log.</param>
    /// <param name="tracePath">O arquivo do log.</param>
    public static bool Attach(string programKey, string programName, string traceId, string tracePath)
    {
        if (string.IsNullOrWhiteSpace(programKey)) return false;
        if (string.IsNullOrWhiteSpace(traceId)) return false;

        try
        {
            var itens = List().ToList();
            itens.RemoveAll(a => string.Equals(a.ProgramKey, programKey, StringComparison.OrdinalIgnoreCase));

            itens.Add(new TraceAttachmentEntry(
                programKey,
                programName ?? string.Empty,
                traceId,
                tracePath ?? string.Empty,
                DateTime.Now));

            Directory.CreateDirectory(Folder);
            var json = JsonSerializer.Serialize(new AttachmentStore { Items = itens }, new JsonSerializerOptions
            {
                WriteIndented = true,
            });

            File.WriteAllText(FilePath, json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// O log anexado a um programa.
    /// </summary>
    /// <param name="programKey">A identificacao do programa.</param>
    public static TraceAttachmentEntry? Of(string? programKey)
    {
        if (string.IsNullOrWhiteSpace(programKey)) return null;

        return List().FirstOrDefault(a =>
            string.Equals(a.ProgramKey, programKey, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Tira a ligacao de um programa.
    /// </summary>
    /// <param name="programKey">A identificacao do programa.</param>
    public static bool Detach(string? programKey)
    {
        if (string.IsNullOrWhiteSpace(programKey)) return false;

        try
        {
            var itens = List().Where(a =>
                !string.Equals(a.ProgramKey, programKey, StringComparison.OrdinalIgnoreCase)).ToList();

            Directory.CreateDirectory(Folder);
            var json = JsonSerializer.Serialize(new AttachmentStore { Items = itens }, new JsonSerializerOptions
            {
                WriteIndented = true,
            });

            File.WriteAllText(FilePath, json);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
