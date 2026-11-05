using System.Collections.Concurrent;
using System.IO;
using System.IO.Enumeration;
using Microsoft.Win32;

namespace Tranal.Uninstaller.Core.Modules;

/// <summary>
/// Um arquivo registrado na fotografia do sistema.
/// </summary>
public sealed class SnapshotFile
{
    /// <summary>Caminho absoluto normalizado do arquivo.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Tamanho do arquivo em bytes no momento da captura.</summary>
    public long SizeBytes { get; init; }

    /// <summary>Data da ultima modificacao (UTC) no momento da captura.</summary>
    public DateTime ModifiedUtc { get; init; }
}

/// <summary>
/// Uma chave de registro registrada na fotografia do sistema.
/// </summary>
public sealed class SnapshotRegistryKey
{
    /// <summary>
    /// Caminho canonico da chave, no mesmo formato do regedit
    /// (ex.: HKEY_CURRENT_USER\Software\Foo).
    /// </summary>
    public string Path { get; init; } = string.Empty;
}

/// <summary>
/// Fotografia do sistema: o estado dos arquivos e das chaves de registro em um
/// instante. Equivale ao driver revoflt.sys do Revo, que observa tudo o que o
/// instalador cria durante a execucao.
///
/// Uso tipico: capturar ANTES e DEPOIS do instalador e comparar os dois estados
/// (ver <see cref="Diff"/>). O snapshot ANTES e descartavel: so o diff e mantido.
/// </summary>
public sealed class SystemSnapshot
{
    /// <summary>
    /// Tempo maximo da varredura de ARQUIVOS (segundos). Uma varredura sem fim
    /// travaria o modulo "Programas Monitorados"; ao estourar o prazo, o que ja
    /// foi lido e preservado e o restante e ignorado.
    /// </summary>
    private const int FileCaptureTimeoutSeconds = 45;

    /// <summary>
    /// Tempo maximo da varredura de REGISTRO (segundos). O orcamento e
    /// separado do de arquivos: a varredura de arquivos nao pode "gastar" o
    /// tempo do registro (foi assim que a deteccao de chaves se perdeu).
    /// </summary>
    private const int RegistryCaptureTimeoutSeconds = 45;

    /// <summary>Profundidade maxima da varredura recursiva de registro.</summary>
    private const int MaxRegistryDepth = 12;

    /// <summary>
    /// Niveis da arvore de registro em que os filhos sao percorridos em
    /// paralelo. Alem disso a varredura e sequencial (evita multiplicar
    /// threads em arvores muito largas).
    /// </summary>
    private const int RegistryParallelDepth = 2;

    internal SystemSnapshot(
        IReadOnlyDictionary<string, SnapshotFile> files,
        IReadOnlyDictionary<string, SnapshotRegistryKey> registryKeys,
        IReadOnlyDictionary<string, string> folders,
        IReadOnlyList<string> fileRoots)
    {
        Files = files;
        RegistryKeys = registryKeys;
        Folders = folders;
        FileRoots = fileRoots;
    }

    /// <summary>
    /// Arquivos capturados. A chave e o caminho normalizado em MAIUSCULAS
    /// (a comparacao de antes/depois e feita com essa chave).
    /// </summary>
    public IReadOnlyDictionary<string, SnapshotFile> Files { get; }

    /// <summary>
    /// Chaves de registro capturadas. A chave e o caminho canonico em MAIUSCULAS.
    /// </summary>
    public IReadOnlyDictionary<string, SnapshotRegistryKey> RegistryKeys { get; }

    /// <summary>
    /// Pastas capturadas (chave em MAIUSCULAS, valor = caminho original).
    /// Necessario para distinguir "pasta nova" de "pasta que ja existia".
    /// </summary>
    internal IReadOnlyDictionary<string, string> Folders { get; }

    /// <summary>Raizes de arquivo usadas na captura (delimita a deducao de prefixos).</summary>
    internal IReadOnlyList<string> FileRoots { get; }

    /// <summary>
    /// Raizes de arquivo monitoradas por padrao. Sao as pastas em que um
    /// instalador normalmente cria coisas; C:\Windows nao entra (custoso e
    /// irrelevante para o modulo).
    /// </summary>
    public static IReadOnlyList<string> DefaultFileRoots()
    {
        var roots = new List<string>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Path.GetTempPath(),
        };

        return roots
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Raizes de registro monitoradas por padrao.</summary>
    public static IReadOnlyList<string> DefaultRegistryRoots() => new[]
    {
        @"HKEY_CURRENT_USER\Software",
        @"HKEY_LOCAL_MACHINE\SOFTWARE",
    };

    /// <summary>
    /// Fotografa o sistema. Nunca lanca: em caso de falha devolve um snapshot
    /// vazio (o diff resultante sera vazio tambem, mas o fluxo do instalador
    /// monitorado continua).
    /// </summary>
    public static SystemSnapshot Capture(
        IReadOnlyList<string>? fileRoots = null,
        IReadOnlyList<string>? registryRoots = null)
    {
        try
        {
            var roots = NormalizeRoots(fileRoots ?? DefaultFileRoots());

            // Cada thread mantem o SEU dicionario e o resultado e fundido no fim:
            // um dicionario compartilhado entre as threads viraria gargalo.
            var fileDeadline = DateTime.UtcNow.AddSeconds(FileCaptureTimeoutSeconds);
            var files = new Dictionary<string, SnapshotFile>(StringComparer.Ordinal);
            var folders = new Dictionary<string, string>(StringComparer.Ordinal);
            CaptureFiles(roots, fileDeadline, files, folders);

            var regRoots = (registryRoots ?? DefaultRegistryRoots())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var registryResults = new ConcurrentBag<Dictionary<string, SnapshotRegistryKey>>();
            var registryDeadline = DateTime.UtcNow.AddSeconds(RegistryCaptureTimeoutSeconds);
            Parallel.ForEach(regRoots, root => registryResults.Add(CaptureRegistryRoot(root, registryDeadline)));

            var keys = new Dictionary<string, SnapshotRegistryKey>(StringComparer.Ordinal);
            foreach (var result in registryResults)
            {
                foreach (var pair in result) keys[pair.Key] = pair.Value;
            }

            return new SystemSnapshot(files, keys, folders, roots);
        }
        catch
        {
            return new SystemSnapshot(
                new Dictionary<string, SnapshotFile>(StringComparer.Ordinal),
                new Dictionary<string, SnapshotRegistryKey>(StringComparer.Ordinal),
                new Dictionary<string, string>(StringComparer.Ordinal),
                Array.Empty<string>());
        }
    }

    /// <summary>
    /// Compara duas fotografias e devolve apenas o que nasceu entre elas.
    /// </summary>
    public static SnapshotDiff Diff(SystemSnapshot before, SystemSnapshot after)
    {
        try
        {
            before ??= Empty();
            after ??= Empty();

            var newFiles = new List<SnapshotFile>();
            foreach (var pair in after.Files)
            {
                if (before.Files.ContainsKey(pair.Key)) continue;
                newFiles.Add(pair.Value);
            }
            newFiles.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase));

            var newFolders = new List<string>();
            var seenFolders = new HashSet<string>(StringComparer.Ordinal);

            // 1) Pastas capturadas explicitamente que nao existiam antes.
            foreach (var pair in after.Folders)
            {
                if (before.Folders.ContainsKey(pair.Key)) continue;
                if (!seenFolders.Add(pair.Key)) continue;
                newFolders.Add(pair.Value);
            }

            // 2) Pastas deduzidas dos prefixos dos arquivos novos: se um arquivo
            //    novo apareceu, as pastas que o contem podem ter sido recem-criadas.
            foreach (var file in newFiles)
            {
                var current = Path.GetDirectoryName(file.Path);
                while (!string.IsNullOrEmpty(current))
                {
                    string normalized;
                    try { normalized = PathUtil.Normalize(current); }
                    catch { break; }

                    if (normalized.Length == 0) break;

                    var key = normalized.ToUpperInvariant();
                    if (!before.Folders.ContainsKey(key) && seenFolders.Add(key))
                        newFolders.Add(normalized);

                    // Para na raiz capturada ou fora das raizes capturadas.
                    if (IsCapturedRoot(normalized, after.FileRoots)) break;
                    if (!IsUnderAnyRoot(normalized, after.FileRoots)) break;

                    string? parent;
                    try { parent = Path.GetDirectoryName(normalized); }
                    catch { break; }
                    if (string.IsNullOrEmpty(parent) || string.Equals(parent, normalized, StringComparison.OrdinalIgnoreCase)) break;
                    current = parent;
                }
            }

            newFolders.Sort((a, b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase));

            var newKeys = new List<SnapshotRegistryKey>();
            foreach (var pair in after.RegistryKeys)
            {
                if (before.RegistryKeys.ContainsKey(pair.Key)) continue;
                newKeys.Add(pair.Value);
            }
            newKeys.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase));

            return new SnapshotDiff(newFiles, newFolders, newKeys);
        }
        catch
        {
            return new SnapshotDiff(
                Array.Empty<SnapshotFile>(),
                Array.Empty<string>(),
                Array.Empty<SnapshotRegistryKey>());
        }
    }

    private static SystemSnapshot Empty() => new(
        new Dictionary<string, SnapshotFile>(StringComparer.Ordinal),
        new Dictionary<string, SnapshotRegistryKey>(StringComparer.Ordinal),
        new Dictionary<string, string>(StringComparer.Ordinal),
        Array.Empty<string>());

    private static List<string> NormalizeRoots(IEnumerable<string>? roots)
    {
        var result = new List<string>();
        if (roots is null) return result;

        foreach (var root in roots)
        {
            try
            {
                var normalized = PathUtil.Normalize(root);
                if (normalized.Length == 0) continue;
                if (!Directory.Exists(normalized)) continue;
                if (result.Contains(normalized, StringComparer.OrdinalIgnoreCase)) continue;
                result.Add(normalized);
            }
            catch { /* raiz inacessivel: apenas ignorada */ }
        }

        return result;
    }

    /// <summary>
    /// Percorre as raizes de arquivos recursivamente. Erros de acesso
    /// (UnauthorizedAccessException, caminho longo, arquivo em uso) sao ignorados:
    /// a fotografia e melhor-effort, como no driver do Revo.
    ///
    /// As pastas vao para uma FILA compartilhada: assim as threads retiram o
    /// trabalho mais pesado que houver (em vez de uma thread ficar presa numa
    /// raiz gigante enquanto as outras ja acabaram).
    /// </summary>
    private static void CaptureFiles(
        IReadOnlyList<string> roots,
        DateTime deadlineUtc,
        Dictionary<string, SnapshotFile> files,
        Dictionary<string, string> folders)
    {
        var queue = new ConcurrentQueue<string>(roots);
        var visited = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        var results = new ConcurrentBag<FileRootResult>();
        var workers = Math.Max(Environment.ProcessorCount, 4);

        Parallel.For(0, workers, _ =>
        {
            var result = new FileRootResult();

            while (queue.TryDequeue(out var current))
            {
                if (DateTime.UtcNow >= deadlineUtc) break;

                string normalized;
                try { normalized = PathUtil.Normalize(current); }
                catch { continue; }
                if (normalized.Length == 0) continue;

                // A mesma pasta nunca e percorrida duas vezes (as raizes padrao se
                // sobrepoem em %LOCALAPPDATA% e %TEMP%).
                if (!visited.TryAdd(normalized.ToUpperInvariant(), 0)) continue;
                result.Folders[normalized.ToUpperInvariant()] = normalized;

                var index = 0;
                try
                {
                    // FileSystemEnumerable entrega tamanho e data direto do
                    // FindNextFile: evita um stat por arquivo (era o gargalo que
                    // levava a captura a mais de um minuto).
                    foreach (var entry in EnumerateDirectory(normalized))
                    {
                        // O prazo e conferido de tempos em tempos: uma pasta com
                        // centenas de milhares de arquivos nao pode passar dele.
                        if ((++index & 0x3FF) == 0 && DateTime.UtcNow >= deadlineUtc) break;

                        if (entry.IsDirectory)
                        {
                            queue.Enqueue(entry.Path);
                            continue;
                        }

                        result.Files[entry.Path.ToUpperInvariant()] = new SnapshotFile
                        {
                            Path = entry.Path,
                            SizeBytes = entry.Length,
                            ModifiedUtc = entry.LastWriteTimeUtc,
                        };
                    }
                }
                catch { /* pasta sem permissao: segue para as demais */ }
            }

            results.Add(result);
        });

        foreach (var result in results)
        {
            foreach (var pair in result.Files) files[pair.Key] = pair.Value;
            foreach (var pair in result.Folders) folders[pair.Key] = pair.Value;
        }
    }

    /// <summary>
    /// Enumera o CONTEUDO de uma pasta numa unica passada, devolvendo arquivos e
    /// subpastas. O enumerador nativo ja traz tamanho e data, sem I/O extra.
    /// </summary>
    private static IEnumerable<DirectoryEntry> EnumerateDirectory(string directory)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = false,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,

            // Nada e pulado: instaladores criam arquivos ocultos e de sistema.
            AttributesToSkip = 0,
        };

        var enumerable = new FileSystemEnumerable<DirectoryEntry>(
            directory,
            (ref FileSystemEntry entry) => new DirectoryEntry(
                entry.IsDirectory,
                entry.ToFullPath(),
                entry.Length,
                entry.LastWriteTimeUtc.UtcDateTime),
            options);

        foreach (var entry in enumerable) yield return entry;
    }

    /// <summary>Fotografa uma raiz de registro (somente nomes de chaves).</summary>
    private static Dictionary<string, SnapshotRegistryKey> CaptureRegistryRoot(string root, DateTime deadlineUtc)
    {
        var all = new Dictionary<string, SnapshotRegistryKey>(StringComparer.Ordinal);
        try
        {
            var (hive, subPath) = SplitRegistryRoot(root);

            // As visoes 32 e 64 bits sao varridas separadamente: o mesmo software
            // 32 bits grava em HKLM\SOFTWARE\WOW6432Node.
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    using var key = baseKey.OpenSubKey(subPath);
                    if (key is null) continue;

                    var basePath = CanonicalBasePath(hive, view, subPath);
                    CaptureRegistrySubtree(baseKey, subPath, basePath, deadlineUtc, 0, all);
                }
                catch { }
            }
        }
        catch { }

        return all;
    }

    /// <summary>
    /// Percorre uma subarvore de registro. Nos primeiros niveis os filhos sao
    /// processados em paralelo (a arvore tem centenas de milhares de chaves e a
    /// varredura sequencial era, sozinha, a etapa mais lenta da captura).
    /// </summary>
    private static void CaptureRegistrySubtree(
        RegistryKey baseKey,
        string subPath,
        string displayPath,
        DateTime deadlineUtc,
        int depth,
        Dictionary<string, SnapshotRegistryKey> keys)
    {
        if (depth > MaxRegistryDepth || DateTime.UtcNow >= deadlineUtc) return;

        string[] names;
        try
        {
            using var key = baseKey.OpenSubKey(subPath);
            if (key is null) return;

            keys[displayPath.ToUpperInvariant()] = new SnapshotRegistryKey { Path = displayPath };
            names = key.GetSubKeyNames();
        }
        catch { return; }

        if (names.Length == 0) return;

        if (depth < RegistryParallelDepth && names.Length > 1)
        {
            var results = new ConcurrentBag<Dictionary<string, SnapshotRegistryKey>>();
            Parallel.ForEach(names, name =>
            {
                var local = new Dictionary<string, SnapshotRegistryKey>(StringComparer.Ordinal);
                CaptureRegistrySubtree(
                    baseKey,
                    subPath + "\\" + name,
                    displayPath + "\\" + name,
                    deadlineUtc,
                    depth + 1,
                    local);
                results.Add(local);
            });

            foreach (var local in results)
            {
                foreach (var pair in local) keys[pair.Key] = pair.Value;
            }

            return;
        }

        foreach (var name in names)
        {
            CaptureRegistrySubtree(
                baseKey,
                subPath + "\\" + name,
                displayPath + "\\" + name,
                deadlineUtc,
                depth + 1,
                keys);
        }
    }

    /// <summary>Resultado da varredura de UMA raiz de arquivos.</summary>
    private sealed class FileRootResult
    {
        public Dictionary<string, SnapshotFile> Files { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> Folders { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>Uma entrada de diretorio (arquivo ou subpasta).</summary>
    private readonly record struct DirectoryEntry(
        bool IsDirectory,
        string Path,
        long Length,
        DateTime LastWriteTimeUtc);

    /// <summary>Separa "HKEY_...\Sub\Path" na raiz de registro e no subcaminho.</summary>
    private static (RegistryHive Hive, string SubPath) SplitRegistryRoot(string path)
    {
        var text = path.Trim().Trim('\\');

        if (text.StartsWith(@"HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase))
            return (RegistryHive.CurrentUser, StripPrefix(text, @"HKEY_CURRENT_USER"));
        if (text.StartsWith(@"HKEY_CLASSES_ROOT", StringComparison.OrdinalIgnoreCase))
            return (RegistryHive.ClassesRoot, StripPrefix(text, @"HKEY_CLASSES_ROOT"));
        if (text.StartsWith(@"HKEY_USERS", StringComparison.OrdinalIgnoreCase))
            return (RegistryHive.Users, StripPrefix(text, @"HKEY_USERS"));
        if (text.StartsWith(@"HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase))
            return (RegistryHive.LocalMachine, StripPrefix(text, @"HKEY_LOCAL_MACHINE"));

        return (RegistryHive.LocalMachine, text);
    }

    private static string StripPrefix(string text, string prefix) =>
        text.Length > prefix.Length ? text[prefix.Length..].TrimStart('\\') : string.Empty;

    /// <summary>
    /// Caminho canonico da raiz no formato do regedit. A visao de 32 bits de
    /// HKLM\SOFTWARE e, na verdade, HKLM\SOFTWARE\WOW6432Node.
    /// </summary>
    private static string CanonicalBasePath(RegistryHive hive, RegistryView view, string subPath)
    {
        var hiveName = SafetyRules.HiveName(hive);

        if (hive == RegistryHive.LocalMachine
            && view == RegistryView.Registry32
            && Environment.Is64BitOperatingSystem
            && subPath.StartsWith(@"SOFTWARE\", StringComparison.OrdinalIgnoreCase)
            && !subPath.StartsWith(@"SOFTWARE\WOW6432Node", StringComparison.OrdinalIgnoreCase))
        {
            subPath = @"SOFTWARE\WOW6432Node\" + subPath[@"SOFTWARE\".Length..];
        }

        return subPath.Length == 0 ? hiveName : $@"{hiveName}\{subPath}";
    }

    private static bool IsCapturedRoot(string path, IReadOnlyList<string> roots)
    {
        foreach (var root in roots)
        {
            if (string.Equals(root, path, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static bool IsUnderAnyRoot(string path, IReadOnlyList<string> roots)
    {
        foreach (var root in roots)
        {
            if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}

/// <summary>
/// Diferenca entre duas fotografias do sistema: o que o instalador criou.
/// </summary>
public sealed class SnapshotDiff
{
    public SnapshotDiff(
        IReadOnlyList<SnapshotFile> newFiles,
        IReadOnlyList<string> newFolders,
        IReadOnlyList<SnapshotRegistryKey> newRegistryKeys)
    {
        NewFiles = newFiles;
        NewFolders = newFolders;
        NewRegistryKeys = newRegistryKeys;
    }

    /// <summary>Arquivos que nao existiam na fotografia anterior.</summary>
    public IReadOnlyList<SnapshotFile> NewFiles { get; }

    /// <summary>Pastas que nao existiam na fotografia anterior.</summary>
    public IReadOnlyList<string> NewFolders { get; }

    /// <summary>Chaves de registro que nao existiam na fotografia anterior.</summary>
    public IReadOnlyList<SnapshotRegistryKey> NewRegistryKeys { get; }
}
