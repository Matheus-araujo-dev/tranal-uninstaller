using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace Tranal.Uninstaller.Core;

/// <summary>
/// Um valor de registro capturado antes de uma remocao. E a unidade que
/// permite tanto a restauracao completa quanto a restauracao "custom"
/// (item a item) do Revo Uninstaller Pro.
/// </summary>
public sealed class RegistrySnapshot
{
    /// <summary>Caminho canonico da chave (mesmo formato do regedit).</summary>
    public string KeyPath { get; init; } = string.Empty;

    /// <summary>Nome do valor. String vazia representa o valor padrao da chave.</summary>
    public string ValueName { get; init; } = string.Empty;

    /// <summary>Tipo do dado.</summary>
    public RegistryValueKind Kind { get; init; } = RegistryValueKind.String;

    /// <summary>
    /// O dado capturado. Para <see cref="RegistryValueKind.String"/> e
    /// <see cref="RegistryValueKind.ExpandString"/> e o proprio texto; para
    /// <see cref="RegistryValueKind.DWord"/> e <see cref="RegistryValueKind.QWord"/>
    /// e o numero em notacao invariante; para
    /// <see cref="RegistryValueKind.Binary"/> e <see cref="RegistryValueKind.MultiString"/>
    /// e base64 (bytes brutos, ou UTF-16 das cadeias unidas por NUL).
    /// </summary>
    public string Data { get; init; } = string.Empty;
}

/// <summary>
/// Um item capturado por um ponto de restauracao: um arquivo, uma pasta ou
/// uma chave de registro (com todos os seus valores).
/// </summary>
public sealed class BackupItem
{
    /// <summary>"Arquivo", "Pasta" ou "Registro" — os mesmos rotulos usados no resto do Tranal.</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>Caminho original do item, como estava antes da remocao.</summary>
    public string OriginalPath { get; init; } = string.Empty;

    /// <summary>Caminho relativo do item dentro da pasta do ponto de restauracao.</summary>
    public string BackupPath { get; init; } = string.Empty;

    /// <summary>Tamanho em bytes. Zero para itens de registro.</summary>
    public long Size { get; init; }

    /// <summary>Valores capturados, quando o item e de registro.</summary>
    public List<RegistrySnapshot> Registry { get; init; } = new();
}

/// <summary>
/// Um ponto de restauracao: o conjunto do que foi removido em uma operacao,
/// guardado de forma que possa ser devolvido ao sistema depois.
/// </summary>
public sealed class RestorePoint
{
    /// <summary>Identificador unico do ponto (tambem e o nome da pasta em disco).</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Nome exibido, definido pelo usuario ou derivado da operacao.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Momento da criacao do ponto.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Tipo da operacao registrada: registro, arquivos ou ambos.</summary>
    public string Operation { get; init; } = string.Empty;

    /// <summary>Tamanho total em bytes dos itens guardados.</summary>
    public long TotalSize { get; init; }

    /// <summary>Itens capturados.</summary>
    public List<BackupItem> Items { get; init; } = new();

    /// <summary>Caminho absoluto da pasta do ponto de restauracao.</summary>
    [JsonIgnore]
    public string FolderPath { get; init; } = string.Empty;

    /// <summary>Numero de itens de registro guardados.</summary>
    public int RegistryItemCount => Items.Count(i => i.Kind == "Registro");

    /// <summary>Numero de arquivos e pastas guardados.</summary>
    public int FileItemCount => Items.Count(i => i.Kind is "Arquivo" or "Pasta");
}

/// <summary>Resultado de uma restauracao.</summary>
public sealed class RestoreResult
{
    /// <summary>Itens devolvidos ao sistema.</summary>
    public int Restored { get; init; }

    /// <summary>Itens que nao puderam ser devolvidos.</summary>
    public int Failed { get; init; }

    /// <summary>Itens ignorados porque nao estavam selecionados.</summary>
    public int Skipped { get; init; }

    /// <summary>Descricao por item, para o relatorio ao usuario.</summary>
    public List<string> Details { get; init; } = new();

    /// <summary>True quando todos os itens selecionados foram devolvidos.</summary>
    public bool Succeeded => Failed == 0;
}

/// <summary>
/// Gerenciador de backups do Tranal Uninstaller Pro.
///
/// O Revo Uninstaller Pro guarda, apos cada entrada de registro, arquivo ou
/// pasta removida, uma informacao de backup que pode ser restaurada depois
/// — inteira ou item a item — e que pode ser apagada por tempo. Este e o
/// equivalente: cada remocao cria um ponto de restauracao em disco, com o
/// conteudo real do que foi removido.
///
/// O armazenamento e um diretorio por ponto de restauracao:
///
/// <code>
/// Backups\&lt;id&gt;\manifest.json
/// Backups\&lt;id&gt;registry\&lt;n&gt;.json
/// Backups\&lt;id&gt;files\&lt;n&gt;\...
/// </code>
/// </summary>
public static class BackupManager
{
    /// <summary>Operacao: itens de registro.</summary>
    public const string OperationRegistry = "Itens de Registro";

    /// <summary>Operacao: arquivos e pastas.</summary>
    public const string OperationFiles = "Arquivos e Pastas";

    /// <summary>Operacao: os dois tipos juntos.</summary>
    public const string OperationMixed = "Registro e Arquivos";

    private static readonly JsonSerializerOptions ManifestOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Diretorio raiz de todos os pontos de restauracao.
    ///
    /// <see cref="RootDirectoryOverride"/> existe para os testes: sem ele, um
    /// teste que exclui pontos de restauracao apagaria os pontos REAIS do
    /// usuario.
    /// </summary>
    public static string RootDirectory =>
        RootDirectoryOverride ?? Path.Combine(RegistryBackup.DataDirectory, "Backups");

    /// <summary>Diretorio alternativo dos pontos de restauracao (so para testes).</summary>
    internal static string? RootDirectoryOverride { get; set; }

    /// <summary>
    /// Cria um novo ponto de restauracao vazio em disco.
    /// </summary>
    /// <param name="name">Nome exibido.</param>
    /// <param name="operation">Tipo da operacao (use as constantes <c>Operation*</c>).</param>
    public static RestorePoint Create(string name, string operation)
    {
        var id = $"{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..8]}";
        var folder = Path.Combine(RootDirectory, id);
        Directory.CreateDirectory(Path.Combine(folder, "registry"));
        Directory.CreateDirectory(Path.Combine(folder, "files"));

        var point = new RestorePoint
        {
            Id = id,
            Name = string.IsNullOrWhiteSpace(name) ? id : name.Trim(),
            CreatedAt = DateTime.Now,
            Operation = operation,
            FolderPath = folder,
        };

        Save(point);
        return point;
    }

    /// <summary>
    /// Captura uma chave de registro e todos os seus valores.
    /// </summary>
    /// <returns>
    /// <c>true</c> quando a chave existia e foi capturada.
    /// <c>false</c> quando a chave nao existe, nao pode ser lida ou e uma chave
    /// protegida — uma chave que nao pode ser removida tambem nao e copiada.
    /// </returns>
    public static bool AddRegistryKey(RestorePoint point, string registryPath)
    {
        if (SafetyRules.IsProtectedRegistryKey(registryPath)) return false;

        var snapshots = CaptureRegistry(registryPath);
        if (snapshots.Count == 0) return false;

        var file = Path.Combine(point.FolderPath, "registry", $"{snapshots[0].GetHashCode():X8}.json");
        var index = 0;
        while (File.Exists(file))
        {
            file = Path.Combine(point.FolderPath, "registry", $"{snapshots[0].GetHashCode():X8}-{++index}.json");
        }

        File.WriteAllText(file, JsonSerializer.Serialize(snapshots, ManifestOptions));

        point.Items.Add(new BackupItem
        {
            Kind = "Registro",
            OriginalPath = registryPath,
            BackupPath = Path.Combine("registry", Path.GetFileName(file)),
            Registry = snapshots,
        });

        return true;
    }

    /// <summary>
    /// Captura um arquivo, copiando-o para a pasta do ponto de restauracao.
    /// Arquivos dentro de caminho protegido nao sao copiados: o que nao pode
    /// ser removido tambem nao precisa de backup.
    /// </summary>
    public static bool AddFile(RestorePoint point, string filePath)
    {
        var full = PathUtil.Normalize(filePath);
        if (full.Length == 0 || !File.Exists(full)) return false;
        if (SafetyRules.IsProtectedFolder(Path.GetDirectoryName(full) ?? string.Empty)) return false;

        var target = Path.Combine(point.FolderPath, "files", $"{point.Items.Count:0000}-{Path.GetFileName(full)}");
        try
        {
            var parent = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
            File.Copy(full, target, overwrite: true);

            var size = new FileInfo(target).Length;
            point.Items.Add(new BackupItem
            {
                Kind = "Arquivo",
                OriginalPath = full,
                BackupPath = Path.Combine("files", Path.GetFileName(target)),
                Size = size,
            });
            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// Captura uma pasta, copiando-a recursivamente.
    /// Pasta protegida pelo Tranal nao e copiada: uma pasta que nao pode ser
    /// removida nao precisa de backup — e copiar o <c>C:\Windows\System32</c>
    /// inteiro ocuparia centenas de megabytes sem qualquer ganho.
    /// </summary>
    public static bool AddFolder(RestorePoint point, string folderPath)
    {
        var full = PathUtil.Normalize(folderPath);
        if (full.Length == 0 || !Directory.Exists(full)) return false;
        if (SafetyRules.IsProtectedFolder(full)) return false;

        var target = Path.Combine(point.FolderPath, "files", $"{point.Items.Count:0000}-{Path.GetFileName(full)}");
        try
        {
            CopyDirectory(full, target, overwrite: true);
            var size = MeasureDirectory(target);

            point.Items.Add(new BackupItem
            {
                Kind = "Pasta",
                OriginalPath = full,
                BackupPath = Path.Combine("files", Path.GetFileName(target)),
                Size = size,
            });
            return true;
        }
        catch { return false; }
    }

    /// <summary>Grava o manifesto do ponto de restauracao em disco.</summary>
    public static void Save(RestorePoint point)
    {
        try
        {
            Directory.CreateDirectory(point.FolderPath);
            var manifest = Path.Combine(point.FolderPath, "manifest.json");

            // O total e recalculado a partir dos itens: quem chamou o Create
            // ainda nao sabia o tamanho do que seria guardado.
            var snapshot = new RestorePoint
            {
                Id = point.Id,
                Name = point.Name,
                CreatedAt = point.CreatedAt,
                Operation = point.Operation,
                TotalSize = ComputeTotalSize(point.Items),
                Items = point.Items,
                FolderPath = point.FolderPath,
            };

            File.WriteAllText(manifest, JsonSerializer.Serialize(snapshot, ManifestOptions));
        }
        catch { /* manifesto e a unica parte dispensavel do backup */ }
    }

    /// <summary>
    /// Soma o tamanho dos itens guardados. O total e calculado a partir dos
    /// proprios itens — e nao informado por quem grava — para que a listagem
    /// nunca mostre "0 KB" num ponto de restauracao que tem conteudo.
    /// </summary>
    private static long ComputeTotalSize(IEnumerable<BackupItem> items)
    {
        long total = 0;
        foreach (var item in items) total += item.Size;
        return total;
    }

    /// <summary>Lista todos os pontos de restauracao, do mais novo para o mais antigo.</summary>
    public static IReadOnlyList<RestorePoint> List()
    {
        var result = new List<RestorePoint>();
        if (!Directory.Exists(RootDirectory)) return result;

        foreach (var dir in Directory.EnumerateDirectories(RootDirectory))
        {
            var point = Load(Path.GetFileName(dir));
            if (point is not null) result.Add(point);
        }

        return result.OrderByDescending(p => p.CreatedAt).ToList();
    }

    /// <summary>Carrega um ponto de restauracao pelo identificador.</summary>
    public static RestorePoint? Load(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        try
        {
            var folder = Path.Combine(RootDirectory, id);
            var manifest = Path.Combine(folder, "manifest.json");
            if (!File.Exists(manifest)) return null;

            var point = JsonSerializer.Deserialize<RestorePoint>(
                ReadManifest(manifest), ManifestOptions);
            if (point is null) return null;

            return new RestorePoint
            {
                Id = point.Id,
                Name = point.Name,
                CreatedAt = point.CreatedAt,
                Operation = point.Operation,
                TotalSize = point.TotalSize > 0 ? point.TotalSize : ComputeTotalSize(point.Items),
                Items = point.Items,
                FolderPath = folder,
            };
        }
        catch { return null; }
    }

    /// <summary>
    /// Le o manifesto detectando a codificacao pelo BOM. Um manifesto gravado
    /// pelo PowerShell 5.1 vem em UTF-16 com BOM: lido como UTF-8, o JSON
    /// pareceria corrompido e o ponto de restauracao sumiria da listagem sem
    /// aviso nenhum.
    /// </summary>
    private static string ReadManifest(string path)
    {
        var bytes = File.ReadAllBytes(path);

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Apaga um ponto de restauracao (pasta inteira).
    ///
    /// O manifesto e removido <b>por ultimo</b>: se a pasta estiver com arquivos
    /// travados (DLLs carregadas), a exclusao falha mas o ponto continua
    /// visivel na listagem e pode ser tentada de novo. Remover o manifesto
    /// primeiro deixaria a pasta orfa e invisivel para o app.
    /// </summary>
    public static bool Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        try
        {
            var folder = Path.Combine(RootDirectory, id);
            if (!Directory.Exists(folder)) return false;

            var manifest = Path.Combine(folder, "manifest.json");

            // 1) apaga o conteudo preservando o manifesto
            if (!ClearFolderContent(folder, manifest)) return false;

            // 2) agora sim: o manifesto e a propria pasta
            if (File.Exists(manifest)) File.Delete(manifest);
            Directory.Delete(folder, recursive: false);
            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// Pastas de backup que sobraram sem manifesto: lixo de uma exclusao
    /// anterior que falhou. Sao invisiveis para <see cref="List"/> e nunca
    /// seriam removidas sozinhas.
    /// </summary>
    public static IReadOnlyList<string> ListOrphans()
    {
        var result = new List<string>();
        if (!Directory.Exists(RootDirectory)) return result;

        foreach (var dir in Directory.EnumerateDirectories(RootDirectory))
        {
            var manifest = Path.Combine(dir, "manifest.json");
            if (!File.Exists(manifest)) result.Add(Path.GetFileName(dir));
        }

        return result;
    }

    /// <summary>Remove as pastas orfas (backup sem manifesto).</summary>
    public static int DeleteOrphans()
    {
        var count = 0;
        foreach (var id in ListOrphans())
        {
            try
            {
                var folder = Path.Combine(RootDirectory, id);
                if (!Directory.Exists(folder)) continue;
                if (!ClearFolderContent(folder, null)) continue;
                Directory.Delete(folder, recursive: false);
                count++;
            }
            catch { /* arquivo travado: fica para a proxima tentativa */ }
        }
        return count;
    }

    /// <summary>
    /// Apaga o conteudo de uma pasta de backup, opcionalmente preservando um
    /// arquivo (o manifesto).
    ///
    /// Devolve <c>false</c> quando sobrou algo — normalmente uma DLL carregada
    /// que impede a exclusao. Nesse caso o manifesto permanece intacto e o
    /// ponto de restauracao continua visivel na listagem, podendo ser tentado
    /// de novo depois. E o que evita a pasta orfa e invisivel.
    /// </summary>
    private static bool ClearFolderContent(string folder, string? preserveFile)
    {
        var (files, directories) = CollectTree(folder);

        foreach (var file in files)
        {
            if (preserveFile is not null && IsSamePath(file, preserveFile)) continue;
            TryDeleteFile(file);
        }

        // das subpastas mais profundas para as mais rasas
        foreach (var directory in directories.OrderByDescending(d => d.Length))
        {
            TryDeleteDirectory(directory);
        }

        // sobrou alguma coisa?
        var entries = SafeEnumerateEntries(folder);

        // A enumeracao falhou: nao da para afirmar que a pasta esta vazia.
        // O manifesto e preservado, porque um ponto sem manifesto e uma pasta
        // orfa invisivel para o app.
        if (entries is null) return false;

        foreach (var entry in entries)
        {
            if (preserveFile is not null && IsSamePath(entry, preserveFile)) continue;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Percorre a pasta recursivamente devolvendo arquivos e subpastas.
    /// Uma subpasta inacessivel nao interrompe o percurso: ela simplesmente
    /// nao entra na lista.
    /// </summary>
    private static (List<string> Files, List<string> Directories) CollectTree(string root)
    {
        var files = new List<string>();
        var directories = new List<string>();
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            try { files.AddRange(Directory.GetFiles(current)); }
            catch { /* pasta inacessivel */ }

            try
            {
                foreach (var directory in Directory.GetDirectories(current))
                {
                    directories.Add(directory);
                    pending.Push(directory);
                }
            }
            catch { /* pasta inacessivel */ }
        }

        return (files, directories);
    }

    /// <summary>
    /// Lista o conteudo direto de uma pasta.
    ///
    /// Devolve <c>null</c> quando a enumeracao falha: nesse caso nao da para
    /// afirmar que a pasta esta vazia, e quem chama precisa conservar o
    /// manifesto.
    /// </summary>
    private static List<string>? SafeEnumerateEntries(string folder)
    {
        try { return Directory.GetFileSystemEntries(folder).ToList(); }
        catch { return null; }
    }

    /// <summary>Apaga um arquivo, ignorando atributos de leitura e erros.</summary>
    private static void TryDeleteFile(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
            }

            File.Delete(path);
        }
        catch { /* arquivo travado: fica para a proxima tentativa */ }
    }

    /// <summary>Apaga uma pasta vazia, ignorando erros.</summary>
    private static void TryDeleteDirectory(string path)
    {
        try { Directory.Delete(path, recursive: false); }
        catch { /* pasta com conteudo ou travada */ }
    }

    /// <summary>Compara dois caminhos ignorando caixa e barra final.</summary>
    private static bool IsSamePath(string left, string right) =>
        string.Equals(
            PathUtil.Normalize(left),
            PathUtil.Normalize(right),
            StringComparison.OrdinalIgnoreCase);

    /// <summary>Apaga todos os pontos de restauracao.</summary>
    public static int DeleteAll()
    {
        var count = 0;
        foreach (var point in List())
        {
            if (Delete(point.Id)) count++;
        }
        return count;
    }

    /// <summary>
    /// Apaga todos os pontos de restauracao criados antes da data informada.
    /// Corresponde ao "Excluir backups anteriores a" do Revo.
    /// </summary>
    public static int DeleteOlderThan(DateTime cutoff)
    {
        var count = 0;
        foreach (var point in List())
        {
            if (point.CreatedAt >= cutoff) continue;
            if (Delete(point.Id)) count++;
        }
        return count;
    }

    /// <summary>
    /// Devolve ao sistema os itens de um ponto de restauracao.
    /// Quando <paramref name="selected"/> e nulo, restaura tudo.
    /// </summary>
    public static RestoreResult Restore(RestorePoint point, IEnumerable<BackupItem>? selected = null)
    {
        // A selecao e casada por identidade de conteudo (caminho + tipo), e
        // nao por referencia: uma selecao montada a partir de outra leitura do
        // manifesto precisa funcionar igual.
        var chosen = selected is null
            ? point.Items
            : point.Items.Where(item => selected.Any(s => SameItem(s, item))).ToList();

        var restored = 0;
        var failed = 0;
        var skipped = point.Items.Count - chosen.Count;
        var details = new List<string>();

        foreach (var item in chosen)
        {
            if (RestoreItem(point, item))
            {
                restored++;
                details.Add($"restaurado: [{item.Kind}] {item.OriginalPath}");
            }
            else
            {
                failed++;
                details.Add($"falhou: [{item.Kind}] {item.OriginalPath}");
            }
        }

        return new RestoreResult
        {
            Restored = restored,
            Failed = failed,
            Skipped = skipped,
            Details = details,
        };
    }

    /// <summary>
    /// Duas entradas de backup sao a mesma quando apontam para o mesmo lugar
    /// e tem o mesmo tipo.
    /// </summary>
    private static bool SameItem(BackupItem a, BackupItem b) =>
        string.Equals(a.Kind, b.Kind, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.OriginalPath, b.OriginalPath, StringComparison.OrdinalIgnoreCase);

    private static bool RestoreItem(RestorePoint point, BackupItem item)
    {
        try
        {
            if (item.Kind == "Registro") return RestoreRegistry(item);
            return RestoreFile(point, item);
        }
        catch { return false; }
    }

    private static bool RestoreFile(RestorePoint point, BackupItem item)
    {
        var source = Path.Combine(point.FolderPath, item.BackupPath);
        if (!File.Exists(source) && !Directory.Exists(source)) return false;

        var target = item.OriginalPath;
        if (target.Length == 0) return false;

        // O destino vem do manifesto em disco: antes de escrever, ele passa
        // pelas mesmas regras que protegem a remocao. Um manifesto adulterado
        // nao vira escrita arbitraria em disco.
        if (item.Kind == "Pasta")
        {
            if (SafetyRules.IsProtectedFolder(target)) return false;
        }
        else if (SafetyRules.IsProtectedFolder(Path.GetDirectoryName(target) ?? string.Empty))
        {
            return false;
        }

        var parent = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);

        if (item.Kind == "Pasta")
        {
            // Mesclar, e nao apagar: o que foi criado depois do backup nao pode
            // sumir em silencio quando o ponto e restaurado.
            CopyDirectory(source, target, overwrite: false);
        }
        else
        {
            File.Copy(source, target, overwrite: true);
        }

        return true;
    }

    private static bool RestoreRegistry(BackupItem item)
    {
        var snapshots = item.Registry;
        if (snapshots.Count == 0) return false;

        var ok = true;
        foreach (var snapshot in snapshots)
        {
            if (!WriteRegistryValue(snapshot)) ok = false;
        }
        return ok;
    }

    /// <summary>
    /// Grava de volta um valor de registro capturado, recriando a chave se preciso.
    /// </summary>
    internal static bool WriteRegistryValue(RegistrySnapshot snapshot)
    {
        if (snapshot.KeyPath.Length == 0) return false;

        // A chave de destino vem do manifesto: passa pelas mesmas regras que
        // protegem a remocao antes de ser recriada.
        if (SafetyRules.IsProtectedRegistryKey(snapshot.KeyPath)) return false;

        try
        {
            var (hive, sub) = SafetyRules.SplitRegistryPath(snapshot.KeyPath);
            if (sub.Length == 0) return false;

            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);

            using var key = baseKey.CreateSubKey(sub, writable: true)
                ?? throw new InvalidOperationException("chave nao criada");
            key.SetValue(snapshot.ValueName, DecodeValue(snapshot), snapshot.Kind);
            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// Converte o dado capturado de volta para o tipo do .NET.
    /// </summary>
    /// <param name="snapshot">Valor capturado.</param>
    /// <returns>O valor pronto para <c>RegistryKey.SetValue</c>.</returns>
    public static object DecodeValue(RegistrySnapshot snapshot)
    {
        switch (snapshot.Kind)
        {
            case RegistryValueKind.DWord:
                return int.TryParse(snapshot.Data, out var d) ? d : 0;

            case RegistryValueKind.QWord:
                return long.TryParse(snapshot.Data, out var q) ? q : 0L;

            case RegistryValueKind.Binary:
                return TryFromBase64(snapshot.Data) ?? Array.Empty<byte>();

            case RegistryValueKind.MultiString:
            {
                var bytes = TryFromBase64(snapshot.Data) ?? Array.Empty<byte>();
                var text = Encoding.Unicode.GetString(bytes);

                // O formato canonico e "str1\0str2\0...\0": o terminador final
                // NUL fecha a lista. Remover todos os NUL perderia os elementos
                // vazios que existem legitimamente no valor.
                if (text.Length == 0) return Array.Empty<string>();

                var parts = text.Split('\0');
                var last = parts.Length - 1;
                if (last >= 0 && parts[last].Length == 0) parts = parts[..last];
                return parts;
            }

            default:
                return snapshot.Data;
        }
    }

    /// <summary>
    /// Serializa um valor do registro no formato de armazenamento do backup.
    /// </summary>
    /// <param name="value">Valor lido do registro.</param>
    /// <param name="kind">Tipo do valor.</param>
    /// <returns>O dado serializado, pronto para <see cref="RegistrySnapshot.Data"/>.</returns>
    public static string EncodeValue(object? value, RegistryValueKind kind)
    {
        switch (kind)
        {
            case RegistryValueKind.DWord:
                return Convert.ToInt32(value ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture);

            case RegistryValueKind.QWord:
                return Convert.ToInt64(value ?? 0L).ToString(System.Globalization.CultureInfo.InvariantCulture);

            case RegistryValueKind.Binary:
                return Convert.ToBase64String(value as byte[] ?? Array.Empty<byte>());

            case RegistryValueKind.MultiString:
            {
                var list = value as string[] ?? Array.Empty<string>();
                var bytes = list.SelectMany(s => Encoding.Unicode.GetBytes(s + "\0")).ToArray();
                return Convert.ToBase64String(bytes);
            }

            default:
                return value?.ToString() ?? string.Empty;
        }
    }

    /// <summary>
    /// Captura todos os valores de uma chave de registro (incluindo o valor padrao).
    /// Devolve lista vazia quando a chave nao existe ou nao pode ser lida.
    /// </summary>
    internal static List<RegistrySnapshot> CaptureRegistry(string registryPath)
    {
        var result = new List<RegistrySnapshot>();
        if (string.IsNullOrWhiteSpace(registryPath)) return result;

        try
        {
            var (hive, sub) = SafetyRules.SplitRegistryPath(registryPath);
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(sub);
            if (key is null) return result;

            foreach (var name in key.GetValueNames())
            {
                var raw = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                var kind = key.GetValueKind(name);

                result.Add(new RegistrySnapshot
                {
                    KeyPath = registryPath,
                    ValueName = name,
                    Kind = kind,
                    Data = EncodeValue(raw, kind),
                });
            }
        }
        catch { /* chave inacessivel: nada a capturar */ }

        return result;
    }

    private static byte[]? TryFromBase64(string data)
    {
        try { return Convert.FromBase64String(data); }
        catch { return null; }
    }

    private static void CopyDirectory(string source, string target, bool overwrite)
    {
        Directory.CreateDirectory(target);

        foreach (var file in Directory.EnumerateFiles(source))
        {
            var destination = Path.Combine(target, Path.GetFileName(file));
            if (!overwrite && File.Exists(destination)) continue;
            File.Copy(file, destination, overwrite);
        }

        foreach (var dir in Directory.EnumerateDirectories(source))
            CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)), overwrite);
    }

    private static long MeasureDirectory(string path)
    {
        try
        {
            long total = 0;
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                total += new FileInfo(file).Length;
            return total;
        }
        catch { return 0; }
    }
}
