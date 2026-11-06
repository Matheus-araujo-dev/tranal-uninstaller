using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Tranal.Uninstaller.Core.Modules;

namespace Tranal.Uninstaller.Core.Tracing;

/// <summary>
/// Os metadados de um snapshot nomeado: o que a lista mostra antes de abrir
/// o retrato.
/// </summary>
public sealed class SnapshotMetadata
{
    /// <summary>Identificador do snapshot (GUID).</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// O nome dado ao snapshot pelo usuario (ex.: "antes do Photoshop").
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// O rotulo do snapshot: uma nota livre sobre o que ele registra.
    /// </summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Quando o snapshot foi tirado (UTC).</summary>
    public DateTimeOffset CreatedUtc { get; init; }

    /// <summary>Quantos arquivos o snapshot guarda.</summary>
    public int FileCount { get; init; }

    /// <summary>Quantas pastas o snapshot guarda.</summary>
    public int FolderCount { get; init; }

    /// <summary>Quantas chaves de registro o snapshot guarda.</summary>
    public int RegistryKeyCount { get; init; }

    /// <summary>Quantos valores de registro o snapshot guarda.</summary>
    public int RegistryValueCount { get; init; }

    /// <summary>O caminho do arquivo do snapshot em disco.</summary>
    public string Path { get; init; } = string.Empty;
}

/// <summary>
/// A biblioteca de snapshots nomeados.
///
/// Antes, a fotografia do sistema "so vivia durante a operacao": o
/// <see cref="TracedPrograms"/> tirava a foto antes, rodava o instalador,
/// tirava a foto depois e comparava — e nada sobrava. Sem o retrato
/// guardado, nao ha como voltar atras, comparar duas instalacoes ou
/// responder "o que mudou entre mardi e hoje".
///
/// Aqui o snapshot e <b>nomeado</b>, <b>gravado em disco</b>, <b>listavel</b>,
/// <b>apagavel</b> e <b>comparavel</b> com outro. E o insumo da analise
/// forense: um snapshot de antes e um de depois dao a resposta.
/// </summary>
public static class SnapshotLibrary
{
    /// <summary>
    /// Identificador do formato do arquivo de snapshot.
    /// </summary>
    public const string SnapshotFormat = "Tranal.Snapshot.v1";

    /// <summary>
    /// Redireciona a pasta dos snapshots. Nulo em uso normal (a pasta de dados
    /// do Tranal); a suite de testes aponta para um temporario, para nao
    /// mexer no ambiente de quem roda.
    /// </summary>
    internal static string? DirectoryOverride { get; set; }

    private static readonly JsonSerializerOptions Opcoes = new()
    {
        WriteIndented = true,
        // O arquivo gravado usa o nome da propriedade em camelCase; a leitura
        // precisa casar as duas escritas.
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// A pasta onde os snapshots moram: ao lado dos outros dados do Tranal.
    /// </summary>
    public static string SnapshotDirectory =>
        DirectoryOverride ?? Path.Combine(RegistryBackup.DataDirectory, "snapshots");

    /// <summary>
    /// Grava um retrato ja pronto como snapshot nomeado. Devolve os
    /// metadados (null quando a gravacao falha).
    /// </summary>
    /// <param name="name">O nome dado pelo usuario.</param>
    /// <param name="estado">O retrato a guardar.</param>
    /// <param name="label">Uma nota sobre o que o snapshot registra.</param>
    public static SnapshotMetadata? Save(string? name, SnapshotState? estado, string? label = null)
    {
        try
        {
            var id = Guid.NewGuid().ToString("D");
            var pasta = SnapshotDirectory;
            Directory.CreateDirectory(pasta);

            var arquivo = Path.Combine(pasta, id + ".json");

            var metadados = new SnapshotMetadata
            {
                Id = id,
                Name = string.IsNullOrWhiteSpace(name) ? id : name.Trim(),
                Label = (label ?? string.Empty).Trim(),
                CreatedUtc = DateTimeOffset.UtcNow,
                FileCount = estado?.Files.Count ?? 0,
                FolderCount = estado?.Folders.Count ?? 0,
                RegistryKeyCount = estado?.RegistryKeys.Count ?? 0,
                RegistryValueCount = estado?.RegistryValues.Count ?? 0,
                Path = arquivo,
            };

            var documento = new
            {
                format = SnapshotFormat,
                metadata = metadados,
                state = estado ?? SnapshotState.Empty,
            };

            File.WriteAllText(arquivo, JsonSerializer.Serialize(documento, Opcoes), new UTF8Encoding(false));
            return metadados;
        }
        catch
        {
            // Falha de gravacao devolve null: quem chamou decide o que fazer.
            return null;
        }
    }

    /// <summary>
    /// Tira a fotografia do sistema e grava como snapshot nomeado.
    ///
    /// A fotografia e cara (o sistema inteiro e varrido) e por isso a captura
    /// nunca lanca: em caso de falha, devolve null.
    /// </summary>
    /// <param name="name">O nome dado pelo usuario.</param>
    /// <param name="label">Uma nota sobre o que o snapshot registra.</param>
    /// <param name="fileRoots">Raizes de arquivo a fotografar.</param>
    /// <param name="registryRoots">Raizes de registro a fotografar.</param>
    public static SnapshotMetadata? Capture(
        string? name,
        string? label = null,
        IReadOnlyList<string>? fileRoots = null,
        IReadOnlyList<string>? registryRoots = null)
    {
        try
        {
            var fotografia = SystemSnapshot.Capture(fileRoots, registryRoots);
            var retrato = SnapshotState.FromSnapshot(fotografia);
            return Save(name, retrato, label);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Lista os snapshots, do mais recente para o mais antigo. Um arquivo
    /// corrompido e ignorado, nunca derruba a listagem.
    /// </summary>
    public static IReadOnlyList<SnapshotMetadata> List()
    {
        try
        {
            var pasta = SnapshotDirectory;
            if (!Directory.Exists(pasta)) return Array.Empty<SnapshotMetadata>();

            var resultado = new List<SnapshotMetadata>();
            foreach (var arquivo in Directory.EnumerateFiles(pasta, "*.json", SearchOption.TopDirectoryOnly))
            {
                var metadados = LerMetadados(arquivo);
                if (metadados is null) continue;
                resultado.Add(metadados);
            }

            return resultado.OrderByDescending(m => m.CreatedUtc).ToList();
        }
        catch
        {
            return Array.Empty<SnapshotMetadata>();
        }
    }

    /// <summary>
    /// Os metadados de um snapshot pelo identificador (null quando nao
    /// existe).
    /// </summary>
    /// <param name="id">Identificador do snapshot.</param>
    public static SnapshotMetadata? Get(string? id)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            foreach (var metadados in List())
            {
                if (string.Equals(metadados.Id, id, StringComparison.OrdinalIgnoreCase)) return metadados;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// O retrato guardado no snapshot (null quando o snapshot nao existe ou
    /// esta corrompido).
    /// </summary>
    /// <param name="id">Identificador do snapshot.</param>
    public static SnapshotState? LoadState(string? id)
    {
        try
        {
            var arquivo = CaminhoDoSnapshot(id);
            if (arquivo.Length == 0 || !File.Exists(arquivo)) return null;

            using var documento = JsonDocument.Parse(File.ReadAllText(arquivo));
            var raiz = documento.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object) return null;

            var formato = "";
            foreach (var propriedade in raiz.EnumerateObject())
            {
                if (!string.Equals(propriedade.Name, "format", StringComparison.OrdinalIgnoreCase)) continue;
                formato = propriedade.Value.ValueKind == JsonValueKind.String
                    ? propriedade.Value.GetString() ?? string.Empty
                    : string.Empty;
                break;
            }

            if (!string.Equals(formato, SnapshotFormat, StringComparison.Ordinal)) return null;

            foreach (var propriedade in raiz.EnumerateObject())
            {
                if (!string.Equals(propriedade.Name, "state", StringComparison.OrdinalIgnoreCase)) continue;
                if (propriedade.Value.ValueKind != JsonValueKind.Object) return null;

                return propriedade.Value.Deserialize<SnapshotState>(Opcoes) ?? SnapshotState.Empty;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Apaga um snapshot. Devolve true quando o arquivo nao existe mais.
    /// </summary>
    /// <param name="id">Identificador do snapshot.</param>
    public static bool Delete(string? id)
    {
        try
        {
            var arquivo = CaminhoDoSnapshot(id);
            if (arquivo.Length == 0) return false;

            // So e permitido apagar dentro da propria biblioteca.
            if (!EstaDentroDaBiblioteca(arquivo)) return false;

            if (!File.Exists(arquivo)) return true;

            File.Delete(arquivo);
            return !File.Exists(arquivo);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Compara dois snapshots guardados. Devolve null quando algum dos dois
    /// nao existe.
    /// </summary>
    /// <param name="idAntes">Identificador do snapshot de antes.</param>
    /// <param name="idDepois">Identificador do snapshot de depois.</param>
    public static SnapshotComparison? Compare(string? idAntes, string? idDepois)
    {
        try
        {
            var antes = LoadState(idAntes);
            var depois = LoadState(idDepois);
            if (antes is null || depois is null) return null;

            return SnapshotComparer.Compare(antes, depois);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// O caminho do arquivo de um snapshot. O identificador e sanitizado: um
    /// identificador com barra nao consegue escapar da biblioteca.
    /// </summary>
    private static string CaminhoDoSnapshot(string? id)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id)) return string.Empty;

            var invalidos = Path.GetInvalidFileNameChars();
            var construtor = new StringBuilder(id.Length);
            foreach (var caractere in id)
            {
                construtor.Append(Array.IndexOf(invalidos, caractere) >= 0 ? '_' : caractere);
            }

            var seguro = construtor.ToString().Trim();
            if (seguro.Length == 0) return string.Empty;

            return Path.Combine(SnapshotDirectory, seguro + ".json");
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// True quando o arquivo esta dentro da pasta da biblioteca.
    /// </summary>
    private static bool EstaDentroDaBiblioteca(string arquivo)
    {
        try
        {
            var pasta = PathUtil.Normalize(SnapshotDirectory);
            if (pasta.Length == 0) return false;

            return arquivo.StartsWith(pasta + "\\", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Le os metadados de um arquivo de snapshot (null quando o arquivo nao e
    /// do formato ou esta corrompido).
    /// </summary>
    private static SnapshotMetadata? LerMetadados(string arquivo)
    {
        try
        {
            using var documento = JsonDocument.Parse(File.ReadAllText(arquivo));
            var raiz = documento.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object) return null;

            var formato = "";
            JsonElement bloco = default;
            var temBloco = false;

            foreach (var propriedade in raiz.EnumerateObject())
            {
                if (string.Equals(propriedade.Name, "format", StringComparison.OrdinalIgnoreCase))
                {
                    formato = propriedade.Value.ValueKind == JsonValueKind.String
                        ? propriedade.Value.GetString() ?? string.Empty
                        : string.Empty;
                    continue;
                }

                if (string.Equals(propriedade.Name, "metadata", StringComparison.OrdinalIgnoreCase))
                {
                    bloco = propriedade.Value;
                    temBloco = true;
                }
            }

            if (!string.Equals(formato, SnapshotFormat, StringComparison.Ordinal)) return null;
            if (!temBloco || bloco.ValueKind != JsonValueKind.Object) return null;

            var metadados = bloco.Deserialize<SnapshotMetadata>(Opcoes);
            if (metadados is null) return null;

            return new SnapshotMetadata
            {
                Id = string.IsNullOrWhiteSpace(metadados.Id)
                    ? Path.GetFileNameWithoutExtension(arquivo)
                    : metadados.Id,
                Name = metadados.Name ?? string.Empty,
                Label = metadados.Label ?? string.Empty,
                CreatedUtc = metadados.CreatedUtc,
                FileCount = metadados.FileCount,
                FolderCount = metadados.FolderCount,
                RegistryKeyCount = metadados.RegistryKeyCount,
                RegistryValueCount = metadados.RegistryValueCount,
                Path = arquivo,
            };
        }
        catch
        {
            // Arquivo corrompido: ignorado na listagem.
            return null;
        }
    }
}
