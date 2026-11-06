using System.IO;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// Uma pasta vazia ou "pela metade" sob as pastas de programas.
/// </summary>
public sealed class EmptyFolderCandidate
{
    /// <summary>Caminho completo da pasta.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Raiz de programas a que a pasta pertence.</summary>
    public string Root { get; init; } = string.Empty;

    /// <summary>True quando a pasta nao tem nada dentro.</summary>
    public bool IsEmpty { get; init; }

    /// <summary>
    /// True quando a pasta nao tem mais executavel nenhum e o resto e
    /// trivial (so arquivos de log e afins).
    /// </summary>
    public bool IsHalfEmpty { get; init; }

    /// <summary>Tamanho do conteudo restante, em bytes.</summary>
    public long SizeBytes { get; init; }

    /// <summary>Quantidade de arquivos restantes.</summary>
    public int FileCount { get; init; }

    /// <summary>Explicacao em portugues do que foi encontrado.</summary>
    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// Resultado de uma limpeza de pastas vazias.
/// </summary>
public sealed class EmptyFolderCleanupResult
{
    /// <summary>Pastas removidas.</summary>
    public IReadOnlyList<string> Removed { get; init; } = Array.Empty<string>();

    /// <summary>Pastas puladas (com conteudo de verdade).</summary>
    public IReadOnlyList<string> Skipped { get; init; } = Array.Empty<string>();

    /// <summary>Pastas que nao puderam ser removidas, com o motivo.</summary>
    public IReadOnlyList<string> Failed { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Limpeza das pastas "Arquivos de Programas" que sobraram vazias (ou pela
/// metade) depois de uma desinstalacao (item #14 do backlog).
///
/// Regras de seguranca, aplicadas aqui e em um lugar so:
///
/// <list type="bullet">
/// <item>somente o que esta **sob** as pastas de programas e elegivel —
/// nada fora delas;</item>
/// <item>a propria pasta <c>Program Files</c> (e a <c>Program Files (x86)</c>)
/// **nunca** e removida: e a raiz do alvo, nao uma sobra;</item>
/// <item>"pela metade" significa: nao sobrou nenhum executavel e o resto e
/// trivial (logs e arquivos de configuracao);</item>
/// <item>atalho (junction) nao e sobra: ele aponta para outro lugar.</item>
/// </list>
/// </summary>
public static class EmptyFolderCleaner
{
    /// <summary>
    /// Extensoes que contam como "executavel" — a presenca de uma delas
    /// impede a limpeza da pasta.
    /// </summary>
    private static readonly string[] BlockingExtensions =
    {
        ".exe", ".dll", ".sys", ".ocx", ".scr", ".com", ".cpl", ".msi", ".drv",
    };

    /// <summary>
    /// Extensoes que sao triviais: log, configuracao e afins.
    /// </summary>
    private static readonly string[] TrivialExtensions =
    {
        ".log", ".txt", ".ini", ".cfg", ".xml", ".json", ".dat", ".old", ".bak", ".nfo",
    };

    /// <summary>
    /// As pastas de programas do Windows: <c>Program Files</c>,
    /// <c>Program Files (x86)</c> e <c>%LOCALAPPDATA%\Programs</c>.
    /// </summary>
    public static IReadOnlyList<string> DefaultRoots()
    {
        var roots = new List<string>();

        Add(roots, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        Add(roots, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(local)) Add(roots, Path.Combine(local, "Programs"));

        return roots;
    }

    /// <summary>
    /// True quando o caminho esta sob uma das pastas de programas.
    ///
    /// O casamento e por prefixo com separador: <c>C:\Program Files</c> cobre
    /// <c>C:\Program Files\App</c> e **nao** cobre
    /// <c>C:\Program Files (x86)</c> nem <c>C:\Program Files2</c>.
    /// </summary>
    public static bool IsUnderProgramRoot(string? path, IEnumerable<string> roots)
    {
        if (string.IsNullOrWhiteSpace(path) || roots is null) return false;

        var full = PathUtil.Normalize(path);
        if (full.Length == 0) return false;

        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root)) continue;

            var normalized = PathUtil.Normalize(root);
            if (normalized.Length == 0) continue;

            if (string.Equals(full, normalized, StringComparison.OrdinalIgnoreCase)) return true;

            if (full.StartsWith(normalized + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Procura as pastas vazias ou pela metade sob as raizes dadas.
    ///
    /// A raiz em si **nunca** entra na lista: ela e a pasta de programas, nao
    /// uma sobra.
    /// </summary>
    public static IReadOnlyList<EmptyFolderCandidate> Find(
        IEnumerable<string> roots,
        int maxDepth = 3,
        long maxTrivialBytes = 256 * 1024,
        int maxTrivialFiles = 32)
    {
        var candidates = new List<EmptyFolderCandidate>();
        if (roots is null) return candidates;

        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root)) continue;

            var normalizedRoot = PathUtil.Normalize(root);
            if (normalizedRoot.Length == 0) continue;
            if (!Directory.Exists(normalizedRoot)) continue;

            var queue = new Queue<(string Path, int Depth)>();
            queue.Enqueue((normalizedRoot, 0));

            while (queue.Count > 0)
            {
                var (folder, depth) = queue.Dequeue();

                string[] subFolders;
                try { subFolders = Directory.GetDirectories(folder, "*", SearchOption.TopDirectoryOnly); }
                catch { continue; }

                foreach (var sub in subFolders)
                {
                    // Atalho: aponta para fora, nao e sobra.
                    if (SafeTraversal.IsReparsePoint(sub)) continue;

                    var childDepth = depth + 1;
                    if (childDepth <= maxDepth)
                    {
                        var candidate = Inspect(sub, normalizedRoot, maxTrivialBytes, maxTrivialFiles);
                        if (candidate is not null) candidates.Add(candidate);
                    }

                    queue.Enqueue((sub, childDepth));
                }
            }
        }

        return candidates;
    }

    /// <summary>
    /// Procura e remove as pastas vazias ou pela metade.
    ///
    /// A pasta vai para a Lixeira do Windows, como toda remocao do Tranal.
    /// Quando a Lixeira nao aceita o item (volume sem Lixeira, policy de
    /// grupo), a pasta **nao** e apagada em definitivo: a operacao entra
    /// em <see cref="EmptyFolderCleanupResult.Failed"/> e quem chama decide.
    /// </summary>
    public static EmptyFolderCleanupResult Clean(
        IEnumerable<string> roots,
        int maxDepth = 3,
        long maxTrivialBytes = 256 * 1024,
        int maxTrivialFiles = 32)
    {
        var removed = new List<string>();
        var skipped = new List<string>();
        var failed = new List<string>();

        var candidates = Find(roots, maxDepth, maxTrivialBytes, maxTrivialFiles);

        foreach (var candidate in candidates)
        {
            // Guarda final: so o que esta sob a pasta de programas e elegivel,
            // e a raiz nunca entra na lista.
            if (!IsUnderProgramRoot(candidate.Path, roots))
            {
                skipped.Add(candidate.Path);
                continue;
            }

            try
            {
                if (!RecycleBin.Send(candidate.Path))
                {
                    failed.Add($"{candidate.Path}: a Lixeira do Windows nao aceitou a pasta.");
                    continue;
                }

                removed.Add(candidate.Path);
            }
            catch (Exception ex)
            {
                failed.Add($"{candidate.Path}: {ex.Message}");
            }
        }

        return new EmptyFolderCleanupResult
        {
            Removed = removed,
            Skipped = skipped,
            Failed = failed,
        };
    }

    /// <summary>
    /// Julga uma pasta: devolve o candidato quando ela esta vazia ou quando
    /// sobrou apenas conteudo trivial. Devolve nulo quando a pasta tem
    /// conteudo de verdade.
    /// </summary>
    private static EmptyFolderCandidate? Inspect(
        string folder,
        string root,
        long maxTrivialBytes,
        int maxTrivialFiles)
    {
        string[] files;
        string[] subFolders;

        try
        {
            files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);
            subFolders = Directory.GetDirectories(folder, "*", SearchOption.AllDirectories);
        }
        catch
        {
            // Pasta inacessivel: nao e candidata.
            return null;
        }

        var isEmpty = files.Length == 0 && subFolders.Length == 0;

        long size = 0;
        var trivial = true;

        foreach (var file in files)
        {
            var extension = Path.GetExtension(file);

            foreach (var blocking in BlockingExtensions)
            {
                if (extension.Equals(blocking, StringComparison.OrdinalIgnoreCase))
                {
                    trivial = false;
                    break;
                }
            }

            if (!trivial) break;

            var isTrivialExtension = false;
            foreach (var allowed in TrivialExtensions)
            {
                if (extension.Equals(allowed, StringComparison.OrdinalIgnoreCase))
                {
                    isTrivialExtension = true;
                    break;
                }
            }

            if (!isTrivialExtension) trivial = false;
            if (!trivial) break;

            try { size += new FileInfo(file).Length; }
            catch { /* o arquivo sumiu no meio da conta */ }
        }

        var isHalfEmpty = !isEmpty
            && trivial
            && files.Length <= maxTrivialFiles
            && size <= maxTrivialBytes;

        if (!isEmpty && !isHalfEmpty) return null;

        return new EmptyFolderCandidate
        {
            Path = folder,
            Root = root,
            IsEmpty = isEmpty,
            IsHalfEmpty = isHalfEmpty,
            SizeBytes = size,
            FileCount = files.Length,
            Reason = isEmpty
                ? "Pasta vazia."
                : $"Pasta pela metade: {files.Length} arquivo(s) trivial(is), {size} byte(s).",
        };
    }

    /// <summary>
    /// Acrescenta uma raiz a lista, ignorando caminho vazio.
    /// </summary>
    private static void Add(List<string> roots, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        roots.Add(path);
    }
}
