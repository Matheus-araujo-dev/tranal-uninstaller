using System.IO;
using System.Text.Json;

namespace Tranal.Uninstaller.Core.Appearance;

/// <summary>
/// Um voto registrado.
/// </summary>
/// <param name="ProgramKey">A identificacao do programa.</param>
/// <param name="ProgramName">O nome do programa.</param>
/// <param name="Score">A nota (1 a 5 estrelas).</param>
/// <param name="Comment">O comentario do usuario.</param>
/// <param name="CreatedAt">Quando o voto foi dado.</param>
/// <param name="Sent">O voto ja foi enviado?</param>
public sealed record CommunityVote(
    string ProgramKey,
    string ProgramName,
    int Score,
    string Comment,
    DateTime CreatedAt,
    bool Sent);

/// <summary>
/// O arquivo dos votos.
/// </summary>
internal sealed class VoteStore
{
    public List<CommunityVote> Items { get; set; } = new();
}

/// <summary>
/// O voto do usuario sobre um programa.
///
/// O voto fica **na maquina do usuario**: e a avaliacao dele, sobre o que
/// aconteceu na maquina dele. O voto pode ser exportado para ser enviado —
/// mas nada sai da maquina sem a ordem do usuario.
///
/// A nota e a mesma que a coluna de estrelas da lista usa: uma regra so
/// para o que o usuario pensou de um programa.
///
/// A regra esta em um lugar so: este modulo sabe o que e um voto e onde ele
/// fica.
/// </summary>
public static class CommunityVotes
{
    /// <summary>
    /// Substitui a pasta de dados (so a suite de testes). Nulo em uso normal.
    /// </summary>
    internal static string? FolderOverride { get; set; }

    /// <summary>A pasta onde ficam os votos.</summary>
    public static string Folder =>
        FolderOverride ?? Path.Combine(RegistryBackup.DataDirectory, "votes");

    /// <summary>O arquivo dos votos.</summary>
    public static string FilePath => Path.Combine(Folder, "votes.json");

    /// <summary>Os votos registrados.</summary>
    public static IReadOnlyList<CommunityVote> List()
    {
        try
        {
            if (!File.Exists(FilePath)) return Array.Empty<CommunityVote>();

            var json = File.ReadAllText(FilePath);
            var loaded = JsonSerializer.Deserialize<VoteStore>(json);
            return loaded?.Items ?? (IReadOnlyList<CommunityVote>)Array.Empty<CommunityVote>();
        }
        catch
        {
            return Array.Empty<CommunityVote>();
        }
    }

    /// <summary>
    /// Registra o voto de um programa.
    /// </summary>
    /// <param name="programKey">A identificacao do programa.</param>
    /// <param name="programName">O nome do programa.</param>
    /// <param name="score">A nota, de 1 a 5 estrelas.</param>
    /// <param name="comment">O comentario.</param>
    public static bool Vote(string programKey, string programName, int score, string comment = "")
    {
        if (string.IsNullOrWhiteSpace(programKey)) return false;

        var nota = Math.Clamp(score, 1, 5);

        try
        {
            var itens = List()
                .Where(v => !string.Equals(v.ProgramKey, programKey, StringComparison.OrdinalIgnoreCase))
                .ToList();

            itens.Add(new CommunityVote(programKey, programName ?? string.Empty, nota, comment ?? string.Empty, DateTime.Now, false));

            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new VoteStore { Items = itens }, new JsonSerializerOptions
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
    /// O voto de um programa.
    /// </summary>
    /// <param name="programKey">A identificacao do programa.</param>
    public static CommunityVote? Of(string? programKey)
    {
        if (string.IsNullOrWhiteSpace(programKey)) return null;

        return List().FirstOrDefault(v =>
            string.Equals(v.ProgramKey, programKey, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Exporta os votos para um arquivo, para o usuario enviar.
    /// </summary>
    /// <param name="destinationPath">O arquivo a ser gravado.</param>
    /// <returns>O caminho gravado, ou uma string vazia.</returns>
    public static string Export(string? destinationPath)
    {
        if (string.IsNullOrWhiteSpace(destinationPath)) return string.Empty;

        try
        {
            var dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(destinationPath, JsonSerializer.Serialize(
                new VoteStore { Items = List().ToList() },
                new JsonSerializerOptions { WriteIndented = true }));

            return destinationPath;
        }
        catch
        {
            return string.Empty;
        }
    }
}
