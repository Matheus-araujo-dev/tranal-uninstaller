using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Tranal.Uninstaller.Core.Modules;

/// <summary>
/// O tipo do item que o log de instalacao registra.
/// </summary>
public enum LogEntryKind
{
    /// <summary>Uma chave de registro.</summary>
    RegistryKey,

    /// <summary>Um valor dentro de uma chave de registro.</summary>
    RegistryValue,

    /// <summary>
    /// Um arquivo ou uma pasta. Os dois sao o mesmo tipo de item do ponto
    /// de vista do log; o que distingue a pasta do arquivo e a flag
    /// <see cref="LogEntry.IsFolder"/>.
    /// </summary>
    File,
}

/// <summary>
/// A operacao que o log registra para a entrada.
/// </summary>
public enum LogEntryOperation
{
    /// <summary>A entrada foi criada durante a instalacao.</summary>
    Created,

    /// <summary>A entrada foi alterada durante a instalacao.</summary>
    Modified,

    /// <summary>A entrada foi removida durante a instalacao.</summary>
    Deleted,

    /// <summary>O log nao diz o que aconteceu com a entrada.</summary>
    Unknown,
}

/// <summary>
/// Uma entrada do log de instalacao: a coisa que o instalador gravou no
/// computador e que o Tranal vai revisar antes de apagar.
///
/// A entrada e <em>dados</em>: quem decide o que fazer com ela e a interface
/// (a "revisao item a item" do Custom Uninstall) e quem executa e o motor
/// canonico de remocao.
/// </summary>
public sealed class LogEntry
{
    /// <summary>
    /// A identidade estavel da entrada dentro do log. E o que permite
    /// editar, ignorar e remover uma entrada sem depender da posicao dela
    /// na lista.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>O tipo do item registrado.</summary>
    public LogEntryKind Kind { get; init; }

    /// <summary>
    /// True quando o item e uma pasta. So faz sentido para
    /// <see cref="LogEntryKind.File"/>.
    /// </summary>
    public bool IsFolder { get; init; }

    /// <summary>
    /// O caminho do arquivo/pasta ou o caminho da chave de registro.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// O nome do valor, quando a entrada e um valor de registro.
    /// </summary>
    public string ValueName { get; init; } = string.Empty;

    /// <summary>
    /// O conteudo do valor, quando a entrada e um valor de registro.
    /// </summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>A operacao registrada para a entrada.</summary>
    public LogEntryOperation Operation { get; init; } = LogEntryOperation.Unknown;

    /// <summary>
    /// A confianca da associacao entre a entrada e o programa.
    ///
    /// E a mesma confianca do resto do Tranal
    /// (<see cref="LeftoverConfidence"/>): uma entrada registrada pelo
    /// proprio instalador e confianca alta; uma entrada acrescentada pelo
    /// usuario e confianca media, porque ninguem mediu que ela pertence ao
    /// programa.
    /// </summary>
    public LeftoverConfidence Confidence { get; init; } = LeftoverConfidence.High;

    /// <summary>
    /// True quando o usuario mandou ignorar a entrada: ela fica no log,
    /// aparece na revisao, e nao entra no plano de remocao.
    /// </summary>
    public bool Ignored { get; init; }

    /// <summary>
    /// True quando a entrada foi acrescentada a mao pelo usuario (e nao
    /// veio do rastreio da instalacao).
    /// </summary>
    public bool Added { get; init; }

    /// <summary>
    /// De qual log a entrada veio. Num log mesclado, e o nome do log de
    /// origem; num log normal, o proprio nome do programa.
    /// </summary>
    public string Origin { get; init; } = string.Empty;

    /// <summary>
    /// O texto que descreve a entrada para a tela. Nao e traducao de
    /// interface: e a descricao do dado, e por isso mora no nucleo.
    /// </summary>
    public string Describe() => Kind switch
    {
        LogEntryKind.RegistryKey => $"Chave de registro: {Path}",
        LogEntryKind.RegistryValue => $"Valor de registro: {Path}\\{ValueName} = {Value}",
        _ => IsFolder ? $"Pasta: {Path}" : $"Arquivo: {Path}",
    };
}

/// <summary>
/// A alteracao pedida para uma entrada do log.
///
/// Tudo e opcional: o que vier preenchido e o que muda. O que nao vier
/// continua como esta. E o que permite que a tela edite "so o caminho" ou
/// "so o valor" sem ter que remontar a entrada inteira.
/// </summary>
public sealed class LogEntryEdit
{
    /// <summary>
    /// A identidade da entrada a editar (<see cref="LogEntry.Id"/>).
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>O novo caminho (ou a nova chave de registro).</summary>
    public string? Path { get; init; }

    /// <summary>O novo nome do valor de registro.</summary>
    public string? ValueName { get; init; }

    /// <summary>O novo conteudo do valor de registro.</summary>
    public string? Value { get; init; }

    /// <summary>A nova operacao registrada.</summary>
    public LogEntryOperation? Operation { get; init; }
}

/// <summary>
/// As operacoes sobre os logs de instalacao rastreada: renomear, mesclar e
/// editar entrada a entrada.
///
/// O log e o mesmo arquivo JSON que o <see cref="TraceStore"/> grava. As
/// edicoes mudam o "payload" do log e sao gravadas de volta pelo proprio
/// TraceStore — a regra fica em um lugar so: quem le o log (a desinstalacao,
/// a exportacao, a revisao) continua lendo o mesmo formato.
/// </summary>
public static class LogOperations
{
    /// <summary>
    /// As entradas do log, na ordem em que o arquivo as traz.
    ///
    /// O log guarda arquivos, pastas, chaves de registro e (opcionalmente)
    /// valores de registro. Cada entrada sai com o tipo, o caminho, a
    /// operacao, a confianca e a origem.
    /// </summary>
    /// <param name="install">O log a ler.</param>
    public static IReadOnlyList<LogEntry> Entries(TracedInstall install)
    {
        var payload = TraceStore.LoadPayload(install);
        if (payload is null) return Array.Empty<LogEntry>();

        return Parse(payload.Value);
    }

    /// <summary>
    /// Renomeia o log (15235: "Rename Log").
    ///
    /// O nome e o do programa que o log descreve: renomear o log nao muda
    /// o que esta gravado nele, so o nome que aparece na lista. O arquivo
    /// em disco e reescrito com o nome novo.
    /// </summary>
    /// <param name="install">O log a renomear.</param>
    /// <param name="newName">O nome novo.</param>
    /// <returns><c>true</c> quando o log foi renomeado e gravado.</returns>
    public static bool Rename(TracedInstall install, string newName)
    {
        try
        {
            if (install is null) return false;

            var name = (newName ?? string.Empty).Trim();
            if (name.Length == 0) return false;

            var payload = TraceStore.LoadPayload(install);
            if (payload is null) return false;

            var renamed = new TracedInstall
            {
                Id = install.Id,
                ProgramName = name,
                Timestamp = install.Timestamp,
                InstallerPath = install.InstallerPath,
                LogPath = install.LogPath,
                FileCount = install.FileCount,
                FolderCount = install.FolderCount,
                RegistryKeyCount = install.RegistryKeyCount,
                Imported = install.Imported,
            };

            return TraceStore.SavePayload(renamed, payload.Value);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Une varios logs em um so (15312: "Merge selected Logs").
    ///
    /// Cada entrada mesclada guarda a origem: de qual log ela veio. E o
    /// que permite, depois, dizer "isto veio do log do programa X" — sem
    /// isso, o log mesclado seria uma lista sem dono.
    ///
    /// As entradas repetidas (o mesmo caminho em dois logs) entram uma
    /// vez so, e a origem junta os nomes dos logs que a registraram.
    /// </summary>
    /// <param name="sources">Os logs a mesclar.</param>
    /// <param name="newName">O nome do log novo.</param>
    /// <returns>O log mesclado, ou null quando nao deu para gravar.</returns>
    public static TracedInstall? Merge(IReadOnlyList<TracedInstall> sources, string newName)
    {
        try
        {
            if (sources is null || sources.Count == 0) return null;

            var name = (newName ?? string.Empty).Trim();
            if (name.Length == 0) return null;

            var merged = new JsonObject();
            var files = new JsonArray();
            var folders = new JsonArray();
            var keys = new JsonArray();
            var values = new JsonArray();
            var ignored = new JsonArray();
            var added = new JsonArray();
            var origins = new JsonObject();

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var timestamp = DateTime.MinValue;
            var installerPath = string.Empty;

            foreach (var source in sources)
            {
                if (source is null) continue;

                var payload = TraceStore.LoadPayload(source);
                if (payload is null) continue;

                var origin = string.IsNullOrWhiteSpace(source.ProgramName)
                    ? source.Id
                    : source.ProgramName;

                if (source.Timestamp > timestamp) timestamp = source.Timestamp;
                if (installerPath.Length == 0) installerPath = source.InstallerPath;

                foreach (var section in Sections(payload.Value))
                {
                    // Cada secao do log continua no seu lugar no log
                    // mesclado: arquivo nao vira chave.
                    var target = section.Name switch
                    {
                        "folders" => folders,
                        "registryKeys" => keys,
                        "registryValues" => values,
                        _ => files,
                    };

                    foreach (var item in section.Items)
                    {
                        var itemId = ItemId(section.Kind, item, section.IsFolder);
                        if (!seen.Add(itemId)) continue;

                        target.Add(item.DeepClone());
                    }
                }

                foreach (var ignoredId in StringList(payload.Value, "ignored"))
                {
                    if (!ignored.Any(n => string.Equals(n?.GetValue<string>(), ignoredId, StringComparison.OrdinalIgnoreCase)))
                        ignored.Add(ignoredId);
                }

                foreach (var addedId in StringList(payload.Value, "added"))
                {
                    if (!added.Any(n => string.Equals(n?.GetValue<string>(), addedId, StringComparison.OrdinalIgnoreCase)))
                        added.Add(addedId);
                }

                // A origem e anotada por entrada: o mesmo item em dois logs
                // fica com os dois nomes.
                foreach (var entry in Parse(payload.Value))
                {
                    var atual = origins[entry.Id]?.GetValue<string>() ?? string.Empty;
                    if (atual.Length == 0)
                    {
                        origins[entry.Id] = origin;
                    }
                    else if (!atual.Split(';').Contains(origin, StringComparer.OrdinalIgnoreCase))
                    {
                        origins[entry.Id] = atual + "; " + origin;
                    }
                }
            }

            merged["files"] = files;
            merged["folders"] = folders;
            merged["registryKeys"] = keys;
            merged["registryValues"] = values;
            merged["ignored"] = ignored;
            merged["added"] = added;
            merged["origins"] = origins;

            var id = Guid.NewGuid().ToString("D");
            var install = new TracedInstall
            {
                Id = id,
                ProgramName = name,
                Timestamp = timestamp == DateTime.MinValue ? DateTime.UtcNow : timestamp,
                InstallerPath = installerPath,
                LogPath = TraceStore.PathForId(id),
                Imported = false,
            };

            var saved = TraceStore.Save(install, ToElement(merged));
            if (string.IsNullOrWhiteSpace(saved)) return null;

            return new TracedInstall
            {
                Id = id,
                ProgramName = name,
                Timestamp = install.Timestamp,
                InstallerPath = installerPath,
                LogPath = saved,
                FileCount = files.Count,
                FolderCount = folders.Count,
                RegistryKeyCount = keys.Count,
                Imported = false,
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Edita uma entrada do log (15178: "Export,Import or Edit log";
    /// 15257: "Add Key") e grava a alteracao.
    ///
    /// O que muda e o que o <see cref="LogEntryEdit"/> traz preenchido. O
    /// resto da entrada continua como estava.
    /// </summary>
    /// <param name="install">O log a editar.</param>
    /// <param name="edit">A alteracao pedida.</param>
    /// <returns><c>true</c> quando a entrada foi alterada e gravada.</returns>
    public static bool EditEntry(TracedInstall install, LogEntryEdit edit)
    {
        try
        {
            if (install is null || edit is null) return false;

            var id = (edit.Id ?? string.Empty).Trim();
            if (id.Length == 0) return false;

            var payload = TraceStore.LoadPayload(install);
            if (payload is null) return false;

            var root = ToObject(payload.Value);
            var entry = Parse(payload.Value).FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));
            if (entry is null) return false;

            var newPath = edit.Path ?? entry.Path;
            var newValueName = edit.ValueName ?? entry.ValueName;
            var newValue = edit.Value ?? entry.Value;
            var newOperation = edit.Operation ?? entry.Operation;

            // O item precisa ser encontrado de novo pelo caminho antigo: o
            // "id" da entrada e derivado do caminho, e o caminho pode estar
            // mudando agora.
            var applied = ApplyEdit(root, entry, newPath, newValueName, newValue, newOperation);
            if (!applied) return false;

            // A identidade da entrada muda quando o caminho muda: as listas
            // de "ignorado", "acrescentado" e as origens andam com ela.
            if (!string.Equals(entry.Id, IdOf(entry.Kind, entry.IsFolder, newPath, newValueName), StringComparison.OrdinalIgnoreCase))
            {
                MoveId(root, entry.Id, IdOf(entry.Kind, entry.IsFolder, newPath, newValueName));
            }

            return TraceStore.SavePayload(install, ToElement(root));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Remove uma entrada do log: ela deixa de existir para a desinstalacao
    /// e para a revisao. A alteracao e gravada.
    /// </summary>
    /// <param name="install">O log a editar.</param>
    /// <param name="entry">A entrada a remover.</param>
    /// <returns><c>true</c> quando a entrada foi removida e o log gravado.</returns>
    public static bool RemoveEntry(TracedInstall install, LogEntry entry)
    {
        try
        {
            if (install is null || entry is null) return false;

            var payload = TraceStore.LoadPayload(install);
            if (payload is null) return false;

            var root = ToObject(payload.Value);
            if (!RemoveItem(root, entry)) return false;

            RemoveId(root, "ignored", entry.Id);
            RemoveId(root, "added", entry.Id);
            RemoveId(root, "origins", entry.Id);

            return TraceStore.SavePayload(install, ToElement(root));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Marca uma entrada do log como ignorada (ou deixa de ignora-la).
    ///
    /// A entrada continua no log e continua aparecendo na revisao — ela so
    /// nao entra no plano de remocao. E o que o usuario quer quando diz
    /// "esse item aqui eu quero que fique".
    /// </summary>
    /// <param name="install">O log a editar.</param>
    /// <param name="entry">A entrada.</param>
    /// <param name="ignored">True para ignorar, false para voltar a remover.</param>
    /// <returns><c>true</c> quando a marca foi gravada.</returns>
    public static bool IgnoreEntry(TracedInstall install, LogEntry entry, bool ignored = true)
    {
        try
        {
            if (install is null || entry is null) return false;

            var payload = TraceStore.LoadPayload(install);
            if (payload is null) return false;

            var root = ToObject(payload.Value);
            SetId(root, "ignored", entry.Id, ignored);

            return TraceStore.SavePayload(install, ToElement(root));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Acrescenta uma entrada ao log (15257: "Add Key").
    ///
    /// E o caminho que o usuario tem quando o instalador nao registrou
    /// tudo: ele aponta a chave que falta e o log passa a saber dela.
    /// A entrada acrescentada a mao tem confianca media — ninguem mediu que
    /// ela pertence ao programa.
    /// </summary>
    /// <param name="install">O log a editar.</param>
    /// <param name="entry">A entrada a acrescentar.</param>
    /// <returns><c>true</c> quando a entrada foi acrescentada e gravada.</returns>
    public static bool AddEntry(TracedInstall install, LogEntry entry)
    {
        try
        {
            if (install is null || entry is null) return false;

            var path = (entry.Path ?? string.Empty).Trim();
            if (path.Length == 0) return false;

            var payload = TraceStore.LoadPayload(install);
            if (payload is null) return false;

            var root = ToObject(payload.Value);
            var id = IdOf(entry.Kind, entry.IsFolder, path, entry.ValueName);

            switch (entry.Kind)
            {
                case LogEntryKind.RegistryKey:
                    Append(root, "registryKeys", path);
                    break;
                case LogEntryKind.RegistryValue:
                    AppendValue(root, path, entry.ValueName, entry.Value);
                    break;
                default:
                    if (entry.IsFolder) Append(root, "folders", path);
                    else AppendFile(root, path);
                    break;
            }

            SetId(root, "added", id, true);

            return TraceStore.SavePayload(install, ToElement(root));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// A identidade de uma entrada: o que permite editar, ignorar e remover
    /// uma entrada sem depender da posicao dela na lista.
    /// </summary>
    /// <param name="kind">O tipo do item.</param>
    /// <param name="isFolder">True quando o item e uma pasta.</param>
    /// <param name="path">O caminho (ou a chave de registro).</param>
    /// <param name="valueName">O nome do valor, quando houver.</param>
    public static string IdOf(LogEntryKind kind, bool isFolder, string path, string valueName = "")
    {
        var normalized = (path ?? string.Empty).Trim();

        return kind switch
        {
            LogEntryKind.RegistryKey => "key|" + normalized,
            LogEntryKind.RegistryValue => "value|" + normalized + "|" + (valueName ?? string.Empty).Trim(),
            _ => isFolder ? "folder|" + normalized : "file|" + normalized,
        };
    }

    // -----------------------------------------------------------------
    // Leitura do payload
    // -----------------------------------------------------------------

    /// <summary>
    /// As secoes do payload que guardam os itens do log.
    /// </summary>
    private sealed class Section
    {
        public string Name { get; init; } = string.Empty;
        public LogEntryKind Kind { get; init; }
        public bool IsFolder { get; init; }
        public List<JsonNode> Items { get; } = new();
    }

    private static List<Section> Sections(JsonElement payload)
    {
        var result = new List<Section>();

        var files = new Section { Name = "files", Kind = LogEntryKind.File };
        var folders = new Section { Name = "folders", Kind = LogEntryKind.File, IsFolder = true };
        var keys = new Section { Name = "registryKeys", Kind = LogEntryKind.RegistryKey };
        var values = new Section { Name = "registryValues", Kind = LogEntryKind.RegistryValue };

        if (payload.ValueKind == JsonValueKind.Array)
        {
            // Formato alternativo: o payload e a propria lista de arquivos.
            foreach (var item in payload.EnumerateArray())
            {
                var node = JsonNode.Parse(item.GetRawText());
                if (node is not null) files.Items.Add(node);
            }

            result.Add(files);
            return result;
        }

        if (payload.ValueKind != JsonValueKind.Object) return result;

        foreach (var name in new[] { "files", "folders", "registryKeys", "registryValues" })
        {
            if (!TryGet(payload, name, out var array) || array.ValueKind != JsonValueKind.Array) continue;

            var section = name switch
            {
                "files" => files,
                "folders" => folders,
                "registryKeys" => keys,
                _ => values,
            };

            foreach (var item in array.EnumerateArray())
            {
                var node = JsonNode.Parse(item.GetRawText());
                if (node is not null) section.Items.Add(node);
            }
        }

        result.Add(files);
        result.Add(folders);
        result.Add(keys);
        result.Add(values);
        return result;
    }

    private static List<LogEntry> Parse(JsonElement payload)
    {
        var entries = new List<LogEntry>();
        if (payload.ValueKind != JsonValueKind.Object && payload.ValueKind != JsonValueKind.Array)
            return entries;

        var ignored = new HashSet<string>(StringList(payload, "ignored"), StringComparer.OrdinalIgnoreCase);
        var added = new HashSet<string>(StringList(payload, "added"), StringComparer.OrdinalIgnoreCase);
        var origins = ReadOrigins(payload);

        foreach (var section in Sections(payload))
        {
            foreach (var item in section.Items)
            {
                var path = ItemPath(item);
                if (path.Length == 0) continue;

                var valueName = ItemValueName(item);
                var value = ItemValue(item);
                var id = IdOf(section.Kind, section.IsFolder, path, valueName);

                entries.Add(new LogEntry
                {
                    Id = id,
                    Kind = section.Kind,
                    IsFolder = section.IsFolder,
                    Path = path,
                    ValueName = valueName,
                    Value = value,
                    Operation = ItemOperation(item, section),
                    Confidence = added.Contains(id) ? LeftoverConfidence.Medium : LeftoverConfidence.High,
                    Ignored = ignored.Contains(id),
                    Added = added.Contains(id),
                    Origin = origins.TryGetValue(id, out var origin) ? origin : string.Empty,
                });
            }
        }

        return entries;
    }

    private static string ItemPath(JsonNode? item)
    {
        if (item is JsonValue value && value.TryGetValue<string>(out var text))
            return (text ?? string.Empty).Trim();

        if (item is JsonObject obj)
        {
            var path = obj["path"]?.GetValue<string>() ?? string.Empty;
            return path.Trim();
        }

        return string.Empty;
    }

    private static string ItemValueName(JsonNode? item)
    {
        if (item is not JsonObject obj) return string.Empty;
        return (obj["name"]?.GetValue<string>() ?? string.Empty).Trim();
    }

    private static string ItemValue(JsonNode? item)
    {
        if (item is not JsonObject obj) return string.Empty;

        var value = obj["value"];
        if (value is null) return string.Empty;

        return value.GetValueKind() switch
        {
            JsonValueKind.String => value.GetValue<string>() ?? string.Empty,
            JsonValueKind.Null => string.Empty,
            _ => value.ToJsonString(),
        };
    }

    private static LogEntryOperation ItemOperation(JsonNode item, Section section)
    {
        var text = item is JsonObject obj ? obj["operation"]?.GetValue<string>() : null;
        if (string.IsNullOrWhiteSpace(text))
        {
            // Sem informacao no arquivo, o log assume o que ele registra:
            // o que o instalador gravou.
            return section.Kind == LogEntryKind.RegistryValue
                ? LogEntryOperation.Unknown
                : LogEntryOperation.Created;
        }

        return text.Trim().ToLowerInvariant() switch
        {
            "created" or "criado" => LogEntryOperation.Created,
            "modified" or "modificado" => LogEntryOperation.Modified,
            "deleted" or "removido" => LogEntryOperation.Deleted,
            _ => LogEntryOperation.Unknown,
        };
    }

    private static string ItemId(LogEntryKind kind, JsonNode item, bool isFolder)
    {
        var path = ItemPath(item);
        var valueName = ItemValueName(item);
        return IdOf(kind, isFolder, path, valueName);
    }

    // -----------------------------------------------------------------
    // Escrita do payload
    // -----------------------------------------------------------------

    private static bool ApplyEdit(
        JsonObject root,
        LogEntry entry,
        string newPath,
        string newValueName,
        string newValue,
        LogEntryOperation newOperation)
    {
        var sectionName = entry.Kind switch
        {
            LogEntryKind.RegistryKey => "registryKeys",
            LogEntryKind.RegistryValue => "registryValues",
            _ => entry.IsFolder ? "folders" : "files",
        };

        if (root[sectionName] is not JsonArray array) return false;

        foreach (var node in array)
        {
            var path = ItemPath(node);
            if (!string.Equals(path, entry.Path, StringComparison.OrdinalIgnoreCase)) continue;

            if (entry.Kind == LogEntryKind.RegistryValue)
            {
                if (node is not JsonObject obj) continue;
                if (!string.Equals(ItemValueName(node), entry.ValueName, StringComparison.OrdinalIgnoreCase)) continue;

                obj["path"] = newPath;
                obj["name"] = newValueName;
                obj["value"] = newValue;
                obj["operation"] = OperationText(newOperation);
                return true;
            }

            if (node is JsonObject item)
            {
                item["path"] = newPath;
                item["operation"] = OperationText(newOperation);
                return true;
            }

            // O item e uma string simples (pasta ou chave): e preciso
            // trocar o elemento da fila.
            var index = array.IndexOf(node);
            if (index < 0) return false;
            array[index] = newPath;
            return true;
        }

        return false;
    }

    private static bool RemoveItem(JsonObject root, LogEntry entry)
    {
        var sectionName = entry.Kind switch
        {
            LogEntryKind.RegistryKey => "registryKeys",
            LogEntryKind.RegistryValue => "registryValues",
            _ => entry.IsFolder ? "folders" : "files",
        };

        if (root[sectionName] is not JsonArray array) return false;

        for (var i = array.Count - 1; i >= 0; i--)
        {
            var node = array[i];
            var path = ItemPath(node);
            if (!string.Equals(path, entry.Path, StringComparison.OrdinalIgnoreCase)) continue;

            if (entry.Kind == LogEntryKind.RegistryValue)
            {
                if (!string.Equals(ItemValueName(node), entry.ValueName, StringComparison.OrdinalIgnoreCase)) continue;
            }

            array.RemoveAt(i);
            return true;
        }

        return false;
    }

    private static void Append(JsonObject root, string sectionName, string path)
    {
        if (root[sectionName] is not JsonArray array)
        {
            array = new JsonArray();
            root[sectionName] = array;
        }

        foreach (var node in array)
        {
            var atual = ItemPath(node);
            if (string.Equals(atual, path, StringComparison.OrdinalIgnoreCase)) return;
        }

        array.Add(JsonValue.Create(path));
    }

    private static void AppendFile(JsonObject root, string path)
    {
        if (root["files"] is not JsonArray array)
        {
            array = new JsonArray();
            root["files"] = array;
        }

        foreach (var node in array)
        {
            var atual = ItemPath(node);
            if (string.Equals(atual, path, StringComparison.OrdinalIgnoreCase)) return;
        }

        array.Add(new JsonObject { ["path"] = path });
    }

    private static void AppendValue(JsonObject root, string path, string valueName, string value)
    {
        if (root["registryValues"] is not JsonArray array)
        {
            array = new JsonArray();
            root["registryValues"] = array;
        }

        array.Add(new JsonObject
        {
            ["path"] = path,
            ["name"] = valueName ?? string.Empty,
            ["value"] = value ?? string.Empty,
        });
    }

    private static void SetId(JsonObject root, string sectionName, string id, bool present)
    {
        if (root[sectionName] is not JsonArray array)
        {
            array = new JsonArray();
            root[sectionName] = array;
        }

        var index = -1;
        for (var i = 0; i < array.Count; i++)
        {
            if (string.Equals(array[i]?.GetValue<string>(), id, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        if (present && index < 0) array.Add(id);
        if (!present && index >= 0) array.RemoveAt(index);
    }

    private static void RemoveId(JsonObject root, string sectionName, string id)
    {
        if (root[sectionName] is JsonArray array)
        {
            for (var i = array.Count - 1; i >= 0; i--)
            {
                if (string.Equals(array[i]?.GetValue<string>(), id, StringComparison.OrdinalIgnoreCase))
                    array.RemoveAt(i);
            }

            return;
        }

        if (root[sectionName] is JsonObject obj)
        {
            foreach (var property in obj.ToList())
            {
                if (string.Equals(property.Key, id, StringComparison.OrdinalIgnoreCase))
                    obj.Remove(property.Key);
            }
        }
    }

    private static void MoveId(JsonObject root, string oldId, string newId)
    {
        foreach (var section in new[] { "ignored", "added" })
        {
            if (root[section] is not JsonArray array) continue;

            for (var i = 0; i < array.Count; i++)
            {
                if (string.Equals(array[i]?.GetValue<string>(), oldId, StringComparison.OrdinalIgnoreCase))
                    array[i] = newId;
            }
        }

        if (root["origins"] is not JsonObject origins) return;
        if (origins[oldId] is null) return;

        var value = origins[oldId]!.GetValue<string>();
        origins.Remove(oldId);
        origins[newId] = value;
    }

    // -----------------------------------------------------------------
    // Utilidades de JSON
    // -----------------------------------------------------------------

    private static JsonObject ToObject(JsonElement payload)
    {
        if (payload.ValueKind == JsonValueKind.Object)
        {
            var node = JsonNode.Parse(payload.GetRawText());
            if (node is JsonObject obj) return obj;
        }

        return new JsonObject();
    }

    private static JsonElement ToElement(JsonObject obj)
    {
        using var document = JsonDocument.Parse(obj.ToJsonString());
        return document.RootElement.Clone();
    }

    private static List<string> StringList(JsonElement payload, string name)
    {
        var result = new List<string>();
        if (payload.ValueKind != JsonValueKind.Object) return result;
        if (!TryGet(payload, name, out var array) || array.ValueKind != JsonValueKind.Array) return result;

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) continue;
            var text = item.GetString();
            if (!string.IsNullOrWhiteSpace(text)) result.Add(text!);
        }

        return result;
    }

    private static Dictionary<string, string> ReadOrigins(JsonElement payload)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (payload.ValueKind != JsonValueKind.Object) return result;
        if (!TryGet(payload, "origins", out var origins) || origins.ValueKind != JsonValueKind.Object) return result;

        foreach (var property in origins.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String) continue;
            result[property.Name] = property.Value.GetString() ?? string.Empty;
        }

        return result;
    }

    private static bool TryGet(JsonElement element, string name, out JsonElement value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object) return false;

        if (element.TryGetProperty(name, out value)) return true;

        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
            value = property.Value;
            return true;
        }

        return false;
    }

    private static string OperationText(LogEntryOperation operation) => operation switch
    {
        LogEntryOperation.Created => "created",
        LogEntryOperation.Modified => "modified",
        LogEntryOperation.Deleted => "deleted",
        _ => "unknown",
    };
}
