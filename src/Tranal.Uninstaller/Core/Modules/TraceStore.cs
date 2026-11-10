using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Tranal.Uninstaller.Core.Modules;

/// <summary>
/// Uma instalacao monitorada: o registro de um instalador executado com
/// rastreio. Equivale a uma entrada da "Base de Logs" do Revo.
/// </summary>
public sealed class TracedInstall
{
    /// <summary>Identificador do log (GUID).</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Nome do programa instalado.</summary>
    public string ProgramName { get; init; } = string.Empty;

    /// <summary>Data/hora do rastreio (UTC).</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>Caminho do instalador executado.</summary>
    public string InstallerPath { get; init; } = string.Empty;

    /// <summary>Caminho do arquivo .json do log.</summary>
    public string LogPath { get; init; } = string.Empty;

    /// <summary>Quantidade de arquivos registrados no log.</summary>
    public int FileCount { get; init; }

    /// <summary>Quantidade de pastas registradas no log.</summary>
    public int FolderCount { get; init; }

    /// <summary>Quantidade de chaves de registro registradas no log.</summary>
    public int RegistryKeyCount { get; init; }

    /// <summary>True quando o log veio de um arquivo externo importado.</summary>
    public bool Imported { get; init; }
}

/// <summary>
/// Armazenamento dos logs de instalacao rastreada ("Base de Logs" do Revo).
///
/// FORMATO DO LOG (estavel, versionado por <see cref="LogFormat"/>):
///
/// {
///   "format": "Tranal.TraceLog.v1",
///   "id": "6f1c...-...",
///   "programName": "Meu Programa",
///   "timestamp": "2026-11-05T10:22:31.0000000Z",
///   "installerPath": "C:\Temp\setup.exe",
///   "imported": false,
///   "counts": { "files": 128, "folders": 12, "registryKeys": 4 },
///   "payload": {
///     "files": [ { "path": "C:\Program Files\Meu Programa\app.exe",
///                  "sizeBytes": 1024,
///                  "modifiedUtc": "2026-11-05T10:22:30.0000000Z" } ],
///     "folders": [ "C:\Program Files\Meu Programa" ],
///     "registryKeys": [ { "path": "HKEY_CURRENT_USER\Software\MeuPrograma" } ]
///   }
/// }
///
/// Um log = UM arquivo JSON. O bloco "payload" e o que o modulo de desinstalacao
/// consome; o bloco de metadados identifica a instalacao.
/// </summary>
public static class TraceStore
{
    /// <summary>Identificador do formato de log aceito na importacao.</summary>
    public const string LogFormat = "Tranal.TraceLog.v1";

    private static readonly JsonSerializerOptions DocumentOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Diretorio da base de logs:
    /// %LOCALAPPDATA%\Tranal Group\Tranal Uninstaller Pro\logs
    /// </summary>
    public static string Directory =>
        DirectoryOverride ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Tranal Group", "Tranal Uninstaller Pro", "logs");

    /// <summary>
    /// Redireciona a pasta da base de logs. Existe para os testes: sem ele,
    /// um teste que grava ou apaga um log mexeria no %LOCALAPPDATA% de quem
    /// esta rodando a suite.
    /// </summary>
    internal static string? DirectoryOverride { get; set; }

    /// <summary>
    /// A base de logs passa a apontar para uma pasta temporaria e vazia.
    ///
    /// E o que a suite usa para nao mexer no %LOCALAPPDATA% de quem roda o
    /// teste: com o override nulo, a base de logs seria a de verdade — e um
    /// log criado ou apagado no teste entraria na lista do usuario.
    /// </summary>
    internal static void ResetForTests()
    {
        DirectoryOverride = Path.Combine(
            Path.GetTempPath(),
            "tranal-logs-" + Guid.NewGuid().ToString("N")[..8]);
    }

    /// <summary>Lista todos os logs. Um log corrompido e ignorado, nunca derruba a listagem.</summary>
    public static IReadOnlyList<TracedInstall> List()
    {
        try
        {
            var dir = Directory;
            if (!System.IO.Directory.Exists(dir)) return Array.Empty<TracedInstall>();

            var result = new List<TracedInstall>();
            foreach (var file in System.IO.Directory.EnumerateFiles(dir, "*.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    if (!TryReadLogFile(file, out var log) || log is null) continue;
                    result.Add(ToInstall(log, file));
                }
                catch { /* log corrompido: ignorado */ }
            }

            return result.OrderByDescending(i => i.Timestamp).ToList();
        }
        catch
        {
            return Array.Empty<TracedInstall>();
        }
    }

    /// <summary>Recupera um log pelo identificador.</summary>
    public static TracedInstall? Get(string id)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            foreach (var install in List())
            {
                if (string.Equals(install.Id, id, StringComparison.OrdinalIgnoreCase)) return install;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Grava o log e devolve o caminho do arquivo criado (ou string vazia em caso
    /// de falha).
    /// </summary>
    public static string Save(TracedInstall install, JsonElement payload)
    {
        try
        {
            if (install is null) return string.Empty;

            EnsureDirectory();
            var id = string.IsNullOrWhiteSpace(install.Id) ? Guid.NewGuid().ToString("D") : install.Id;
            var file = PathForId(id);

            var counts = CountPayload(payload);
            if (!WriteLogFile(file, id, install, payload, counts)) return string.Empty;
            return file;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Le o payload (lista de arquivos/pastas/chaves) do log. Devolve null quando
    /// o log nao existe ou esta corrompido.
    /// </summary>
    public static JsonElement? LoadPayload(TracedInstall install)
    {
        try
        {
            if (install is null) return null;

            var path = ResolvePath(install);
            if (path.Length == 0 || !File.Exists(path)) return null;

            if (!TryReadLogFile(path, out var log) || log is null) return null;
            if (log.Payload.ValueKind == JsonValueKind.Undefined) return null;

            return log.Payload.Clone();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Grava de volta um payload alterado, no mesmo arquivo do log.
    ///
    /// E o que as operacoes sobre o log (editar entrada, ignorar, remover,
    /// renomear) usam: o arquivo e reescrito no lugar, com os metadados do
    /// log (id, nome do programa, data) preservados.
    /// </summary>
    /// <param name="install">O log a reescrever.</param>
    /// <param name="payload">O payload novo.</param>
    /// <returns><c>true</c> quando o arquivo foi reescrito.</returns>
    public static bool SavePayload(TracedInstall install, JsonElement payload)
    {
        try
        {
            if (install is null) return false;

            var path = ResolvePath(install);
            if (path.Length == 0) return false;

            EnsureDirectory();

            var id = string.IsNullOrWhiteSpace(install.Id)
                ? Path.GetFileNameWithoutExtension(path)
                : install.Id;

            var counts = CountPayload(payload);
            return WriteLogFile(path, id, install, payload, counts);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Exclui o log. Devolve true quando o arquivo nao existe mais.
    /// A exclusao passa pela rotina canonica <see cref="UninstallEngine.RemoveLeftovers"/>.
    /// </summary>
    public static bool Delete(TracedInstall install)
    {
        try
        {
            if (install is null) return false;

            var path = ResolvePath(install);
            if (path.Length == 0) return false;

            // So e permitido apagar dentro da propria base de logs.
            if (!IsInsideStore(path)) return false;

            var items = new List<LeftoverItem>
            {
                new() { Kind = "Arquivo", Path = path, Detail = "Log de instalacao monitorada" },
            };

            var report = UninstallEngine.RemoveLeftovers(items);
            return report.Outcomes.Count > 0 && report.Outcomes.All(o => o.Gone);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Importa um .json de log externo (mesmo formato do Tranal). O arquivo e
    /// copiado para a base de logs e marcado como importado. Devolve null quando
    /// o arquivo nao existe ou o formato e invalido.
    /// </summary>
    public static TracedInstall? Import(string logFilePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(logFilePath)) return null;

            var source = PathUtil.Normalize(logFilePath);
            if (source.Length == 0 || !File.Exists(source)) return null;

            if (!TryReadLogFile(source, out var log) || log is null) return null;

            EnsureDirectory();

            var id = string.IsNullOrWhiteSpace(log.Id) ? Guid.NewGuid().ToString("D") : log.Id;
            var target = PathForId(id);

            // O log ja estava na base: reaproveita o proprio arquivo.
            if (!string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(target))
                {
                    id = Guid.NewGuid().ToString("D");
                    target = PathForId(id);
                }
                File.Copy(source, target, overwrite: true);
            }

            var counts = CountPayload(log.Payload);
            if (counts.Files == 0 && counts.Folders == 0 && counts.RegistryKeys == 0)
                counts = log.Counts;

            var imported = new TracedInstall
            {
                Id = id,
                ProgramName = log.ProgramName,
                Timestamp = log.Timestamp,
                InstallerPath = log.InstallerPath,
                LogPath = target,
                FileCount = counts.Files,
                FolderCount = counts.Folders,
                RegistryKeyCount = counts.RegistryKeys,
                Imported = true,
            };

            WriteLogFile(target, id, imported, log.Payload, counts);
            return imported;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Exporta o log para o destino informado (JSON indentado, legivel).
    /// Devolve o caminho gravado ou string vazia em caso de falha.
    /// </summary>
    public static string Export(TracedInstall install, string destinationPath)
    {
        try
        {
            if (install is null || string.IsNullOrWhiteSpace(destinationPath)) return string.Empty;

            var destination = PathUtil.Normalize(destinationPath);
            if (destination.Length == 0) return string.Empty;

            var folder = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(folder)) System.IO.Directory.CreateDirectory(folder);

            // O payload original e relido para que a exportacao seja autonoma.
            var payload = LoadPayload(install) ?? EmptyPayload();
            var counts = CountPayload(payload);
            if (counts.Files == 0 && counts.Folders == 0 && counts.RegistryKeys == 0)
                counts = new TraceCounts(install.FileCount, install.FolderCount, install.RegistryKeyCount);

            var exported = new TracedInstall
            {
                Id = install.Id,
                ProgramName = install.ProgramName,
                Timestamp = install.Timestamp,
                InstallerPath = install.InstallerPath,
                LogPath = destination,
                FileCount = counts.Files,
                FolderCount = counts.Folders,
                RegistryKeyCount = counts.RegistryKeys,
                Imported = install.Imported,
            };

            WriteLogFile(destination, exported.Id, exported, payload, counts);
            return destination;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>Caminho canonico do arquivo de log de um identificador.</summary>
    internal static string PathForId(string id)
    {
        var safe = SanitizeFileName(id);
        if (safe.Length == 0) safe = Guid.NewGuid().ToString("D");
        return Path.Combine(Directory, safe + ".json");
    }

    /// <summary>
    /// Serializa o payload canonico (usado pelo modulo de programas monitorados).
    /// </summary>
    internal static JsonElement SerializePayload(TracePayload payload)
    {
        try
        {
            return JsonSerializer.SerializeToElement(payload, PayloadOptions);
        }
        catch
        {
            return EmptyPayload();
        }
    }

    /// <summary>Le o payload canonico de um elemento JSON.</summary>
    internal static TracePayload ReadPayload(JsonElement payload)
    {
        var result = new TracePayload();
        try
        {
            if (payload.ValueKind == JsonValueKind.Array)
            {
                // Formato alternativo: o payload e a propria lista de arquivos.
                foreach (var item in payload.EnumerateArray())
                {
                    var file = ReadFileEntry(item);
                    if (file is not null) result.Files.Add(file);
                }
                return result;
            }

            if (payload.ValueKind != JsonValueKind.Object) return result;

            if (TryGetProperty(payload, "files", out var files) && files.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in files.EnumerateArray())
                {
                    var file = ReadFileEntry(item);
                    if (file is not null) result.Files.Add(file);
                }
            }

            if (TryGetProperty(payload, "folders", out var folders) && folders.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in folders.EnumerateArray())
                {
                    var text = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(text)) result.Folders.Add(text!);
                }
            }

            if (TryGetProperty(payload, "registryKeys", out var keys) && keys.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in keys.EnumerateArray())
                {
                    string text;
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        text = item.GetString() ?? string.Empty;
                    }
                    else if (item.ValueKind == JsonValueKind.Object && TryGetProperty(item, "path", out var path))
                    {
                        text = path.GetString() ?? string.Empty;
                    }
                    else
                    {
                        text = string.Empty;
                    }

                    if (!string.IsNullOrWhiteSpace(text)) result.RegistryKeys.Add(text);
                }
            }
        }
        catch { }
        return result;
    }

    /// <summary>
    /// Normalizacao de texto compartilhada pelos modulos: remove acentos e caixa.
    /// E o que faz "Joao" casar com "João" na busca por nome.
    /// </summary>
    internal static class TraceText
    {
        /// <summary>Texto normalizado (sem acentos, em minusculas).</summary>
        public static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            try
            {
                var decomposed = value.Normalize(NormalizationForm.FormD);
                var builder = new StringBuilder(decomposed.Length);
                foreach (var ch in decomposed)
                {
                    if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
                    builder.Append(char.ToLowerInvariant(ch));
                }
                return builder.ToString().Normalize(NormalizationForm.FormC);
            }
            catch
            {
                return value.ToLowerInvariant();
            }
        }

        /// <summary>
        /// Verdadeiro quando <paramref name="word"/> aparece em
        /// <paramref name="candidate"/> como palavra inteira (sem letras/digitos
        /// colados nas pontas). "Steam" casa com "Steam Tools" e nao com "Steamworks".
        /// </summary>
        public static bool ContainsWholeWord(string? candidate, string? word)
        {
            var text = Normalize(candidate);
            var needle = Normalize(word);
            if (text.Length == 0 || needle.Length == 0) return false;

            var index = text.IndexOf(needle, StringComparison.Ordinal);
            while (index >= 0)
            {
                var beforeOk = index == 0 || !IsWordChar(text[index - 1]);
                var after = index + needle.Length;
                var afterOk = after >= text.Length || !IsWordChar(text[after]);
                if (beforeOk && afterOk) return true;

                index = text.IndexOf(needle, index + 1, StringComparison.Ordinal);
            }

            return false;
        }

        /// <summary>
        /// Variantes de um nome de programa para o casamento por caminho.
        /// Alem do nome normalizado, inclui a variante "compacta" (sem espacos e
        /// pontuacao), porque o instalador nem sempre repete os separadores:
        /// "Meu Programa" vira a pasta "MeuPrograma".
        /// </summary>
        public static IReadOnlyList<string> NameVariants(string? name)
        {
            var normalized = Normalize(name);
            var result = new List<string>();
            if (normalized.Length == 0) return result;

            result.Add(normalized);

            var compact = new string(normalized.Where(char.IsLetterOrDigit).ToArray());
            if (compact.Length >= 4 && !string.Equals(compact, normalized, StringComparison.Ordinal))
                result.Add(compact);

            return result;
        }

        /// <summary>Verdadeiro quando o texto contem alguma das palavras indicadas.</summary>
        public static bool ContainsAnyWholeWord(string? candidate, IEnumerable<string> words)
        {
            if (words is null) return false;
            foreach (var word in words)
            {
                if (ContainsWholeWord(candidate, word)) return true;
            }
            return false;
        }

        private static bool IsWordChar(char ch) => char.IsLetterOrDigit(ch);
    }

    /// <summary>Um arquivo registrado no payload do log.</summary>
    internal sealed class TraceFileEntry
    {
        public string Path { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public DateTime ModifiedUtc { get; set; }
    }

    /// <summary>O payload canonico de um log: o que o instalador criou.</summary>
    internal sealed class TracePayload
    {
        public List<TraceFileEntry> Files { get; set; } = new();
        public List<string> Folders { get; set; } = new();
        public List<string> RegistryKeys { get; set; } = new();
    }

    private readonly record struct TraceCounts(int Files, int Folders, int RegistryKeys);

    /// <summary>Conteudo lido de um arquivo de log.</summary>
    private sealed class TraceLogFile
    {
        public string Id { get; set; } = string.Empty;
        public string ProgramName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string InstallerPath { get; set; } = string.Empty;
        public bool Imported { get; set; }
        public JsonElement Payload { get; set; }
        public TraceCounts Counts { get; set; }
    }

    private static string EnsureDirectory()
    {
        var dir = Directory;
        System.IO.Directory.CreateDirectory(dir);
        return dir;
    }

    private static bool WriteLogFile(
        string path, string id, TracedInstall install, JsonElement payload, TraceCounts counts)
    {
        var document = new
        {
            format = LogFormat,
            id,
            programName = install.ProgramName,
            timestamp = install.Timestamp.ToUniversalTime().ToString("O"),
            installerPath = install.InstallerPath,
            imported = install.Imported,
            counts = new
            {
                files = counts.Files,
                folders = counts.Folders,
                registryKeys = counts.RegistryKeys,
            },
            payload,
        };

        var json = JsonSerializer.Serialize(document, DocumentOptions);
        return GravarAtomicamente(path, json);
    }

    /// <summary>
    /// Grava o arquivo de log de forma atomica: o conteudo novo vai para um
    /// temporario, o original vira <c>.bak</c> e o temporario assume o lugar.
    ///
    /// A GRAVACAO DO LOG ESTA AQUI, EM UM LUGAR SO: criar o log, editar uma
    /// entrada, ignorar, remover, acrescentar e mesclar passam todos por esta
    /// funcao. Um log pela metade derrubaria a desinstalacao dirigida pelo log
    /// — e o log e a unica fonte do que o instalador gravou.
    /// </summary>
    /// <param name="caminho">O caminho do arquivo.</param>
    /// <param name="conteudo">O conteudo novo.</param>
    /// <returns><c>true</c> quando o arquivo foi gravado.</returns>
    private static bool GravarAtomicamente(string caminho, string conteudo)
    {
        var temporario = caminho + ".tmp";

        try
        {
            File.WriteAllText(temporario, conteudo, new UTF8Encoding(false));

            if (File.Exists(caminho))
            {
                try { File.Replace(temporario, caminho, caminho + ".bak", true); }
                catch (PlatformNotSupportedException) { SubstituirComBackup(temporario, caminho); }
                catch (IOException) { SubstituirComBackup(temporario, caminho); }
            }
            else
            {
                File.Move(temporario, caminho);
            }

            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            // O temporario nao pode sobrar na pasta de logs.
            try { if (File.Exists(temporario)) File.Delete(temporario); }
            catch { /* o temporario fica para a proxima gravacao sobrescrever */ }
        }
    }

    /// <summary>
    /// Substitui o arquivo quando <c>File.Replace</c> nao esta disponivel
    /// (volume sem suporte a ReplaceFile). A copia para <c>.bak</c> vem
    /// ANTES da substituicao: e melhor avisar do que fingir que houve
    /// backup.
    /// </summary>
    /// <param name="temporario">O arquivo temporario.</param>
    /// <param name="caminho">O arquivo original.</param>
    private static void SubstituirComBackup(string temporario, string caminho)
    {
        File.Copy(caminho, caminho + ".bak", true);
        File.Move(temporario, caminho, true);
    }

    private static bool TryReadLogFile(string path, out TraceLogFile? log)
    {
        log = null;
        try
        {
            if (!File.Exists(path)) return false;

            var json = File.ReadAllText(path);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;

            var format = GetString(root, "format");
            var hasPayload = TryGetProperty(root, "payload", out var payload);

            // Formato valido: identificador de formato do Tranal OU (sem
            // identificador, mas com payload) — tolerado para logs antigos.
            if (format.Length > 0 && !string.Equals(format, LogFormat, StringComparison.Ordinal)) return false;
            if (format.Length == 0 && !hasPayload) return false;

            var file = new TraceLogFile
            {
                Id = GetString(root, "id"),
                ProgramName = GetString(root, "programName"),
                InstallerPath = GetString(root, "installerPath"),
                Imported = GetBoolean(root, "imported"),
            };

            file.Timestamp = ParseTimestamp(GetString(root, "timestamp"), path);
            file.Counts = ReadCounts(root);

            if (hasPayload && payload.ValueKind != JsonValueKind.Undefined)
                file.Payload = payload.Clone();

            log = file;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static TracedInstall ToInstall(TraceLogFile log, string filePath)
    {
        var payload = ReadPayload(log.Payload);
        var counts = new TraceCounts(payload.Files.Count, payload.Folders.Count, payload.RegistryKeys.Count);
        if (counts.Files == 0 && counts.Folders == 0 && counts.RegistryKeys == 0)
            counts = log.Counts;

        return new TracedInstall
        {
            Id = log.Id.Length > 0 ? log.Id : Path.GetFileNameWithoutExtension(filePath),
            ProgramName = log.ProgramName,
            Timestamp = log.Timestamp,
            InstallerPath = log.InstallerPath,
            LogPath = filePath,
            FileCount = counts.Files,
            FolderCount = counts.Folders,
            RegistryKeyCount = counts.RegistryKeys,
            Imported = log.Imported,
        };
    }

    private static TraceCounts CountPayload(JsonElement payload)
    {
        var trace = ReadPayload(payload);
        return new TraceCounts(trace.Files.Count, trace.Folders.Count, trace.RegistryKeys.Count);
    }

    private static TraceCounts ReadCounts(JsonElement root)
    {
        if (!TryGetProperty(root, "counts", out var counts) || counts.ValueKind != JsonValueKind.Object)
            return new TraceCounts(0, 0, 0);

        return new TraceCounts(
            GetInt(counts, "files"),
            GetInt(counts, "folders"),
            GetInt(counts, "registryKeys"));
    }

    private static TraceFileEntry? ReadFileEntry(JsonElement element)
    {
        try
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                var path = element.GetString();
                if (string.IsNullOrWhiteSpace(path)) return null;
                return new TraceFileEntry { Path = path! };
            }

            if (element.ValueKind != JsonValueKind.Object) return null;

            var filePath = GetString(element, "path");
            if (filePath.Length == 0) return null;

            return new TraceFileEntry
            {
                Path = filePath,
                SizeBytes = GetInt(element, "sizeBytes"),
                ModifiedUtc = ParseTimestamp(GetString(element, "modifiedUtc"), string.Empty),
            };
        }
        catch
        {
            return null;
        }
    }

    private static JsonElement EmptyPayload()
    {
        try
        {
            using var document = JsonDocument.Parse("{}");
            return document.RootElement.Clone();
        }
        catch
        {
            return default;
        }
    }

    private static DateTime ParseTimestamp(string text, string fallbackFile)
    {
        if (!string.IsNullOrWhiteSpace(text)
            && DateTime.TryParse(text, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
        {
            return parsed;
        }

        if (!string.IsNullOrEmpty(fallbackFile))
        {
            try { return File.GetLastWriteTimeUtc(fallbackFile); }
            catch { }
        }

        return DateTime.MinValue;
    }

    private static string ResolvePath(TracedInstall install)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(install.LogPath))
            {
                var candidate = PathUtil.Normalize(install.LogPath);
                if (candidate.Length > 0 && File.Exists(candidate)) return candidate;
            }

            if (!string.IsNullOrWhiteSpace(install.Id)) return PathForId(install.Id);
            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsInsideStore(string path)
    {
        try
        {
            var store = PathUtil.Normalize(Directory);
            return store.Length > 0
                && path.StartsWith(store + "\\", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string SanitizeFileName(string id)
    {
        try
        {
            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(id.Length);
            foreach (var ch in id)
            {
                builder.Append(Array.IndexOf(invalid, ch) >= 0 ? '_' : ch);
            }
            return builder.ToString().Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        value = default;
        try
        {
            if (element.ValueKind != JsonValueKind.Object) return false;

            if (element.TryGetProperty(name, out value)) return true;

            // Fallback tolerante ao BOX/CAsT do JSON.
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            return false;
        }
        catch
        {
            value = default;
            return false;
        }
    }

    private static string GetString(JsonElement element, string name)
    {
        try
        {
            if (!TryGetProperty(element, name, out var value)) return string.Empty;
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Number => value.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => string.Empty,
            };
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool GetBoolean(JsonElement element, string name)
    {
        try
        {
            if (!TryGetProperty(element, name, out var value)) return false;
            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => bool.TryParse(value.GetString(), out var parsed) && parsed,
                JsonValueKind.Number => value.TryGetInt32(out var number) && number != 0,
                _ => false,
            };
        }
        catch
        {
            return false;
        }
    }

    private static int GetInt(JsonElement element, string name)
    {
        try
        {
            if (!TryGetProperty(element, name, out var value)) return 0;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)) return (int)number;
            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed)) return parsed;
            return 0;
        }
        catch
        {
            return 0;
        }
    }
}
