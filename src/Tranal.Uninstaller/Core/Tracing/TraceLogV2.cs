using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tranal.Uninstaller.Core.Modules;

namespace Tranal.Uninstaller.Core.Tracing;

/// <summary>
/// O log de rastreio no formato novo (canonico do Lote B).
///
/// O log legado (<c>Core/Modules/TraceStore.cs</c>, formato
/// <c>Tranal.TraceLog.v1</c>) guarda tres listas — arquivos, pastas e chaves —
/// e nao sabe o que aconteceu com cada caminho. Este formato guarda a
/// <b>entrada</b> (<see cref="TraceEntry"/>): o tipo da mudanca, o nome
/// anterior e o dado anterior.
///
/// Os dois formatos convivem durante a migracao: um arquivo v2 gravado nesta
/// pasta e simplesmente ignorado pela listagem legado (o identificador de
/// formato nao bate), e a ponte entre os dois e trabalho de integracao.
///
/// FORMATO DO ARQUIVO (um log = um JSON):
///
/// {
///   "format": "Tranal.TraceLog.v2",
///   "id": "6f1c...-...",
///   "programName": "Meu Programa",
///   "installerPath": "C:\Temp\setup.exe",
///   "timestamp": "2026-11-05T10:22:31.0000000+00:00",
///   "entries": [
///     { "path": "C:\Program Files\Meu Programa\app.exe",
///       "kind": "Overwritten",
///       "oldValue": "1024",
///       "newValue": "2048",
///       "size": 2048,
///       "timestamp": "2026-11-05T10:22:30.0000000+00:00",
///       "isDirectory": false }
///   ]
/// }
/// </summary>
public sealed class TraceLogV2
{
    /// <summary>
    /// Identificador do formato novo do log.
    /// </summary>
    public const string LogFormat = "Tranal.TraceLog.v2";

    /// <summary>
    /// Extensao dos arquivos do formato novo. O sufixo impede que a listagem
    /// legado tente ler estes arquivos como se fossem do formato v1.
    /// </summary>
    public const string FileExtension = ".trace.json";

    private static readonly JsonSerializerOptions Opcoes = new()
    {
        WriteIndented = true,
        // O arquivo gravado usa o nome da propriedade em camelCase; a leitura
        // precisa casar as duas escritas.
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Identificador do log (GUID).</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Nome do programa que este log descreve.</summary>
    public string ProgramName { get; init; } = string.Empty;

    /// <summary>Caminho do instalador executado, quando houve um.</summary>
    public string InstallerPath { get; init; } = string.Empty;

    /// <summary>Quando o log foi fechado (UTC).</summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// As entradas do log, na ordem em que foram observadas.
    /// </summary>
    public IReadOnlyList<TraceEntry> Entries { get; init; } = Array.Empty<TraceEntry>();

    /// <summary>
    /// Cria um log novo, com as entradas informadas. Devolve um log com
    /// identificador e carimbo proprios quando nenhum sao informados.
    /// </summary>
    /// <param name="programName">Nome do programa observado.</param>
    /// <param name="entries">Entradas do log (pode ser vazio).</param>
    /// <param name="installerPath">Caminho do instalador, quando houve um.</param>
    public static TraceLogV2 Create(
        string programName,
        IEnumerable<TraceEntry>? entries = null,
        string? installerPath = null)
    {
        return new TraceLogV2
        {
            Id = Guid.NewGuid().ToString("D"),
            ProgramName = programName ?? string.Empty,
            InstallerPath = installerPath ?? string.Empty,
            Timestamp = DateTimeOffset.UtcNow,
            Entries = entries?.Where(e => e is not null).ToList() ?? new List<TraceEntry>(),
        };
    }

    /// <summary>
    /// Une varios logs do mesmo programa num log so.
    ///
    /// Unir <b>nao</b> e duplicar: uma entrada identica (mesmo caminho, mesmo
    /// tipo de mudanca, mesmo carimbo) entra uma vez so. As entradas
    /// distintas sao preservadas, e a ordem delas e a ordem de chegada
    /// (log mais antigo primeiro).
    ///
    /// O nome do programa e o carimbo do log resultante sao os do log mais
    /// recente: e o log unificado que descreve a instalacao inteira.
    /// </summary>
    /// <param name="logs">Os logs a unir. Logs nulos sao ignorados.</param>
    public static TraceLogV2 Merge(IEnumerable<TraceLogV2>? logs)
    {
        var lista = (logs ?? Array.Empty<TraceLogV2>())
            .Where(l => l is not null)
            .OrderBy(l => l.Timestamp)
            .ToList();

        if (lista.Count == 0) return Create(string.Empty);

        var vistas = new HashSet<string>(StringComparer.Ordinal);
        var entradas = new List<TraceEntry>();

        foreach (var log in lista)
        {
            foreach (var entrada in log.Entries)
            {
                if (entrada is null) continue;

                // A identidade decide: entrada identica entra uma vez so.
                if (!vistas.Add(entrada.Identity())) continue;
                entradas.Add(entrada);
            }
        }

        var maisRecente = lista[^1];

        return new TraceLogV2
        {
            Id = Guid.NewGuid().ToString("D"),
            ProgramName = maisRecente.ProgramName,
            InstallerPath = maisRecente.InstallerPath,
            Timestamp = maisRecente.Timestamp,
            Entries = entradas,
        };
    }

    /// <summary>
    /// Grava o log em disco e devolve o caminho do arquivo gravado (string
    /// vazia quando a gravacao falha). Sem caminho informado, o arquivo vai
    /// para a pasta de logs do Tranal, com o nome derivado do identificador.
    /// </summary>
    /// <param name="path">Caminho do arquivo; quando vazio, o padrao e usado.</param>
    public string Save(string? path = null)
    {
        try
        {
            var destino = string.IsNullOrWhiteSpace(path)
                ? PathForId(string.IsNullOrWhiteSpace(Id) ? Guid.NewGuid().ToString("D") : Id)
                : PathUtil.Normalize(path);

            if (destino.Length == 0) return string.Empty;

            var pasta = System.IO.Path.GetDirectoryName(destino);
            if (!string.IsNullOrEmpty(pasta)) System.IO.Directory.CreateDirectory(pasta);

            var documento = new
            {
                format = LogFormat,
                id = Id,
                programName = ProgramName,
                installerPath = InstallerPath,
                timestamp = Timestamp.ToString("O", CultureInfo.InvariantCulture),
                entries = Entries,
            };

            var json = JsonSerializer.Serialize(documento, Opcoes);
            File.WriteAllText(destino, json, new System.Text.UTF8Encoding(false));
            return destino;
        }
        catch
        {
            // Falha de gravacao nao derruba o rastreio: o log volta vazio e
            // quem chamou decide o que fazer.
            return string.Empty;
        }
    }

    /// <summary>
    /// Le um log do disco. Devolve null quando o arquivo nao existe, nao e do
    /// formato novo, ou esta corrompido.
    /// </summary>
    /// <param name="path">Caminho do arquivo de log.</param>
    public static TraceLogV2? Load(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            var arquivo = PathUtil.Normalize(path);
            if (arquivo.Length == 0 || !File.Exists(arquivo)) return null;

            var json = File.ReadAllText(arquivo);

            using (var documento = JsonDocument.Parse(json))
            {
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

                // Um arquivo de outro formato (o legado v1, por exemplo) nao e
                // um log v2: a ponte entre os dois e trabalho de integracao.
                if (!string.Equals(formato, LogFormat, StringComparison.Ordinal)) return null;
            }

            var log = JsonSerializer.Deserialize<TraceLogV2>(json, Opcoes);
            if (log is null) return null;

            return new TraceLogV2
            {
                Id = string.IsNullOrWhiteSpace(log.Id) ? Guid.NewGuid().ToString("D") : log.Id,
                ProgramName = log.ProgramName ?? string.Empty,
                InstallerPath = log.InstallerPath ?? string.Empty,
                Timestamp = log.Timestamp,
                Entries = log.Entries?.Where(e => e is not null).ToList() ?? new List<TraceEntry>(),
            };
        }
        catch
        {
            // Arquivo ilegivel ou corrompido: o log e ignorado, nunca derruba
            // a listagem.
            return null;
        }
    }

    /// <summary>
    /// Lista os logs do formato novo gravados numa pasta. Arquivos corrompidos
    /// (ou de outro formato) sao ignorados.
    /// </summary>
    /// <param name="directory">Pasta onde procurar; quando vazia, a pasta de logs do Tranal.</param>
    public static IReadOnlyList<TraceLogV2> List(string? directory = null)
    {
        try
        {
            var pasta = string.IsNullOrWhiteSpace(directory) ? DefaultDirectory() : PathUtil.Normalize(directory);
            if (pasta.Length == 0 || !System.IO.Directory.Exists(pasta)) return Array.Empty<TraceLogV2>();

            var resultado = new List<TraceLogV2>();
            foreach (var arquivo in System.IO.Directory.EnumerateFiles(pasta, "*" + FileExtension, SearchOption.TopDirectoryOnly))
            {
                var log = Load(arquivo);
                if (log is null) continue;
                resultado.Add(log);
            }

            return resultado.OrderByDescending(l => l.Timestamp).ToList();
        }
        catch
        {
            return Array.Empty<TraceLogV2>();
        }
    }

    /// <summary>
    /// Converte o log para a entrada da base de logs do Tranal
    /// (<see cref="TracedInstall"/>), para que a integracao possa oferecer o
    /// log na lista de programas monitorados sem inventar um segundo formato.
    /// </summary>
    /// <param name="logPath">Caminho do arquivo do log em disco.</param>
    public TracedInstall ToTracedInstall(string logPath)
    {
        var arquivos = Entries.Count(e => e.Kind != TraceChangeKind.Deleted && !e.IsDirectory);
        var pastas = Entries.Count(e => e.IsDirectory);

        return new TracedInstall
        {
            Id = Id,
            ProgramName = ProgramName,
            Timestamp = Timestamp.UtcDateTime,
            InstallerPath = InstallerPath,
            LogPath = logPath ?? string.Empty,
            FileCount = arquivos,
            FolderCount = pastas,
            RegistryKeyCount = 0,
            Imported = false,
        };
    }

    /// <summary>
    /// Caminho canonico do arquivo de um log, dentro da pasta de logs do Tranal.
    /// </summary>
    /// <param name="id">Identificador do log.</param>
    public static string PathForId(string id)
    {
        var seguro = SanitizeFileName(id);
        if (seguro.Length == 0) seguro = Guid.NewGuid().ToString("D");
        return System.IO.Path.Combine(DefaultDirectory(), seguro + FileExtension);
    }

    /// <summary>
    /// A pasta onde os logs de rastreio moram. E a mesma do log legado — a
    /// regra do lugar dos logs fica em um lugar so.
    /// </summary>
    private static string DefaultDirectory() =>
        Tranal.Uninstaller.Core.Modules.TraceStore.Directory;

    /// <summary>
    /// Remove do nome do arquivo tudo que nao pode estar num nome de arquivo.
    /// </summary>
    private static string SanitizeFileName(string id)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id)) return string.Empty;

            var invalidos = System.IO.Path.GetInvalidFileNameChars();
            var construtor = new System.Text.StringBuilder(id.Length);
            foreach (var caractere in id)
            {
                construtor.Append(Array.IndexOf(invalidos, caractere) >= 0 ? '_' : caractere);
            }

            return construtor.ToString().Trim();
        }
        catch
        {
            return string.Empty;
        }
    }
}
