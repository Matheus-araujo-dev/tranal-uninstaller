using System.IO;
using System.Runtime.InteropServices;

namespace Tranal.Uninstaller.Core.Tools;

/// <summary>
/// Um item de lixo Cleaner encontrado no sistema.
/// </summary>
public sealed class JunkItem
{
    /// <summary>"Arquivo" ou "Pasta".</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>Caminho completo do item.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Identificador estavel da categoria (ex.: "temp-user", "wucache").</summary>
    public string CategoryId { get; init; } = string.Empty;

    /// <summary>Tamanho estimado em bytes (0 quando desconhecido).</summary>
    public long SizeBytes { get; init; }

    /// <summary>True quando o item esta marcado para remocao.</summary>
    public bool Selected { get; set; } = true;
}

/// <summary>
/// Limpeza de lixo: temporarios, caches, logs antigos, prefetch, miniaturas e Lixeira.
///
/// Categorias (identificadores estaveis usados pela GUI e pela CLI):
///   "temp-user", "temp-windows", "wucache", "browser-cache", "logs",
///   "prefetch", "thumbnails", "recyclebin".
///
/// A remocao usa SEMPRE o motor canonico <see cref="UninstallEngine.RemoveLeftovers"/>,
/// exceto a Lixeira, que e esvaziada pela API do shell (SHEmptyRecycleBin) porque
/// nao existe um caminho de arquivo individual para o motor canonico interpretar.
/// </summary>
public static class JunkCleaner
{
    /// <summary>Idade minima (em dias) para considerar log ou prefetch antigos.</summary>
    private const int OldFileAgeInDays = 7;

    /// <summary>Extensoes tratadas como arquivo de log.</summary>
    private static readonly string[] LogExtensions = { ".log", ".etl", ".dmp" };

    /// <summary>Pastas de cache dos navegadores (por navegador e por perfil).</summary>
    private static readonly string[] BrowserCacheFolderNames = { "Cache", "Code Cache", "GPUCache" };

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    /// <summary>
    /// Varre o sistema por itens de lixo. Nunca lanca.
    /// </summary>
    public static IReadOnlyList<JunkItem> Scan()
    {
        var items = new List<JunkItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try { ScanUserTemp(items, seen); }
        catch (Exception ex) { ToolLog.Error($"Falha ao varrer os temporarios do usuario: {ex.Message}"); }

        try { ScanFolder(items, seen, "temp-windows", Path.Combine(WindowsFolder, "Temp")); }
        catch (Exception ex) { ToolLog.Error($"Falha ao varrer os temporarios do Windows: {ex.Message}"); }

        try { ScanFolder(items, seen, "wucache", Path.Combine(WindowsFolder, "SoftwareDistribution", "Download")); }
        catch (Exception ex) { ToolLog.Error($"Falha ao varrer o cache do Windows Update: {ex.Message}"); }

        try { ScanBrowserCaches(items, seen); }
        catch (Exception ex) { ToolLog.Error($"Falha ao varrer os caches dos navegadores: {ex.Message}"); }

        try { ScanOldLogs(items, seen); }
        catch (Exception ex) { ToolLog.Error($"Falha ao varrer os logs antigos: {ex.Message}"); }

        try { ScanPrefetch(items, seen); }
        catch (Exception ex) { ToolLog.Error($"Falha ao varrer o Prefetch: {ex.Message}"); }

        try { ScanThumbnails(items, seen); }
        catch (Exception ex) { ToolLog.Error($"Falha ao varrer as miniaturas: {ex.Message}"); }

        try { ScanRecycleBin(items, seen); }
        catch (Exception ex) { ToolLog.Error($"Falha ao varrer a Lixeira: {ex.Message}"); }

        return items;
    }

    /// <summary>
    /// Remove os itens selecionados e devolve o relatorio canonico.
    /// Itens sob uma pasta protegida pelas regras de seguranca sao marcados como Protected.
    /// </summary>
    public static RemovalReport Clean(IEnumerable<JunkItem> items)
    {
        var outcomes = new List<RemovalOutcome>();
        var regular = new List<LeftoverItem>();

        foreach (var item in items)
        {
            if (!item.Selected) continue;

            if (string.Equals(item.CategoryId, "recyclebin", StringComparison.OrdinalIgnoreCase))
            {
                outcomes.Add(EmptyRecycleBin(item));
                continue;
            }

            // O arquivo de regras do usuario vale para o limpador tambem: um
            // caminho protegido pelas regras nao e lixo, nao importa o que a
            // varredura achou.
            if (Rules.RuleEngine.Current.IsPathProtected(item.Path))
            {
                outcomes.Add(new RemovalOutcome
                {
                    Item = new LeftoverItem
                    {
                        Kind = item.Kind,
                        Path = item.Path,
                        Detail = item.CategoryId,
                    },
                    Status = RemovalStatus.Protected,
                    Message = "protegido pelas regras",
                });
                continue;
            }

            regular.Add(new LeftoverItem
            {
                Kind = item.Kind,
                Path = item.Path,
                Detail = item.CategoryId,
                Selected = true,
            });
        }

        if (regular.Count > 0)
        {
            try
            {
                outcomes.AddRange(UninstallEngine.RemoveLeftovers(regular).Outcomes);
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha na limpeza de lixo: {ex.Message}");
            }
        }

        return new RemovalReport(outcomes);
    }

    // ---------------------------------------------------------------------
    // Varredura
    // ---------------------------------------------------------------------

    /// <summary>
    /// Temporarios do usuario: %TEMP% e %LOCALAPPDATA%\Temp.
    /// Os arquivos de log antigos sao separados para a categoria "logs".
    /// </summary>
    private static void ScanUserTemp(List<JunkItem> items, HashSet<string> seen)
    {
        var roots = new List<string>();
        var temp = Environment.GetEnvironmentVariable("TEMP");
        if (!string.IsNullOrWhiteSpace(temp)) roots.Add(temp);

        var localTemp = Path.Combine(LocalAppData, "Temp");
        if (!roots.Any(r => string.Equals(r, localTemp, StringComparison.OrdinalIgnoreCase)))
            roots.Add(localTemp);

        var limit = DateTime.Now.AddDays(-OldFileAgeInDays);

        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;

            foreach (var entry in SafeEnumerate(root))
            {
                try
                {
                    var full = PathUtil.Normalize(entry);
                    if (full.Length == 0) continue;

                    var isDirectory = Directory.Exists(entry);
                    var isOld = File.Exists(entry) && File.GetLastWriteTime(entry) < limit;

                    if (isOld && IsLogFile(entry))
                    {
                        AddFile(items, seen, "logs", full);
                        continue;
                    }

                    if (isDirectory)
                    {
                        AddFolder(items, seen, "temp-user", full);
                    }
                    else
                    {
                        AddFile(items, seen, "temp-user", full);
                    }
                }
                catch (Exception ex)
                {
                    ToolLog.Error($"Falha ao inspecionar '{entry}': {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Conteudo de uma pasta do sistema (temporarios do Windows, cache do Windows Update).
    /// </summary>
    private static void ScanFolder(List<JunkItem> items, HashSet<string> seen, string categoryId, string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return;

        foreach (var entry in SafeEnumerate(folder))
        {
            try
            {
                var full = PathUtil.Normalize(entry);
                if (full.Length == 0) continue;

                if (Directory.Exists(entry))
                {
                    AddFolder(items, seen, categoryId, full);
                }
                else
                {
                    AddFile(items, seen, categoryId, full);
                }
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao inspecionar '{entry}': {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Cache dos navegadores (Chrome, Edge, Opera): pastas Cache, Code Cache e GPUCache.
    /// </summary>
    private static void ScanBrowserCaches(List<JunkItem> items, HashSet<string> seen)
    {
        var roots = new[]
        {
            Path.Combine(LocalAppData, "Google", "Chrome", "User Data"),
            Path.Combine(LocalAppData, "Microsoft", "Edge", "User Data"),
            Path.Combine(LocalAppData, "Opera Software", "Opera Stable"),
            Path.Combine(LocalAppData, "Opera Software", "Opera GX Stable"),
        };

        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;

            var profiles = new List<string> { root };
            try { profiles.AddRange(SafeEnumerateDirectories(root)); }
            catch (Exception ex) { ToolLog.Error($"Falha ao listar perfis de '{root}': {ex.Message}"); }

            foreach (var profile in profiles)
            {
                foreach (var name in BrowserCacheFolderNames)
                {
                    try
                    {
                        var folder = Path.Combine(profile, name);
                        if (!Directory.Exists(folder)) continue;
                        AddFolder(items, seen, "browser-cache", folder);
                    }
                    catch (Exception ex)
                    {
                        ToolLog.Error($"Falha ao inspecionar o cache '{name}': {ex.Message}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Logs antigos (mais de 7 dias) em %LOCALAPPDATA%\Temp e na pasta de logs do Windows.
    /// </summary>
    private static void ScanOldLogs(List<JunkItem> items, HashSet<string> seen)
    {
        var limit = DateTime.Now.AddDays(-OldFileAgeInDays);
        var roots = new[]
        {
            Path.Combine(LocalAppData, "Temp"),
            Path.Combine(WindowsFolder, "Logs"),
        };

        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;

            foreach (var file in SafeEnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var info = new FileInfo(file);
                    if (!info.Exists || info.LastWriteTime >= limit) continue;
                    if (!IsLogFile(file)) continue;
                    AddFile(items, seen, "logs", PathUtil.Normalize(file));
                }
                catch (Exception ex)
                {
                    ToolLog.Error($"Falha ao inspecionar '{file}': {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Prefetch do Windows (arquivos com mais de 7 dias).
    /// </summary>
    private static void ScanPrefetch(List<JunkItem> items, HashSet<string> seen)
    {
        var folder = Path.Combine(WindowsFolder, "Prefetch");
        if (!Directory.Exists(folder)) return;

        var limit = DateTime.Now.AddDays(-OldFileAgeInDays);

        foreach (var file in SafeEnumerateFiles(folder, "*.pf", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var info = new FileInfo(file);
                if (!info.Exists || info.LastWriteTime >= limit) continue;
                AddFile(items, seen, "prefetch", PathUtil.Normalize(file));
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao inspecionar '{file}': {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Cache de miniaturas do Windows Explorer.
    /// </summary>
    private static void ScanThumbnails(List<JunkItem> items, HashSet<string> seen)
    {
        var explorer = Path.Combine(LocalAppData, "Microsoft", "Windows", "Explorer");
        var roots = new[] { explorer, Path.Combine(WindowsFolder, "SoftwareDistribution") };

        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;

            foreach (var file in SafeEnumerateFiles(root, "thumbcache_*.db", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    AddFile(items, seen, "thumbnails", PathUtil.Normalize(file));
                }
                catch (Exception ex)
                {
                    ToolLog.Error($"Falha ao inspecionar '{file}': {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Lixeira: uma entrada por unidade fixa pronta.
    /// </summary>
    private static void ScanRecycleBin(List<JunkItem> items, HashSet<string> seen)
    {
        foreach (var drive in SafeDrives())
        {
            try
            {
                var folder = Path.Combine(drive, "$Recycle.Bin");
                if (!Directory.Exists(folder)) continue;
                AddFolder(items, seen, "recyclebin", PathUtil.Normalize(folder));
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao inspecionar a Lixeira de '{drive}': {ex.Message}");
            }
        }
    }

    // ---------------------------------------------------------------------
    // Remocao
    // ---------------------------------------------------------------------

    /// <summary>
    /// Esvazia a Lixeira da unidade pelo shell.
    /// Item sintetico: nao passa pelo motor canonico porque nao existe um caminho
    /// de arquivo individual a remover — a operacao e feita pela API do shell.
    /// </summary>
    private static RemovalOutcome EmptyRecycleBin(JunkItem item)
    {
        var leftover = new LeftoverItem
        {
            Kind = "Pasta",
            Path = item.Path,
            Detail = "Lixeira",
            Selected = true,
        };

        try
        {
            var root = Path.GetPathRoot(item.Path);
            if (string.IsNullOrEmpty(root))
            {
                return new RemovalOutcome
                {
                    Item = leftover,
                    Status = RemovalStatus.Failed,
                    Message = "unidade da Lixeira nao identificada",
                };
            }

            if (!Directory.Exists(item.Path))
            {
                return new RemovalOutcome
                {
                    Item = leftover,
                    Status = RemovalStatus.AlreadyAbsent,
                    Message = "ja nao existia",
                };
            }

            const uint flags = 0x00000001 | 0x00000002 | 0x00000004; // sem confirmacao, sem progresso, sem som
            var result = SHEmptyRecycleBin(IntPtr.Zero, root, flags);

            return new RemovalOutcome
            {
                Item = leftover,
                Status = result == 0 ? RemovalStatus.Removed : RemovalStatus.Failed,
                Message = result == 0 ? "Lixeira esvaziada" : $"falha ao esvaziar a Lixeira (codigo {result})",
            };
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao esvaziar a Lixeira '{item.Path}': {ex.Message}");
            return new RemovalOutcome
            {
                Item = leftover,
                Status = RemovalStatus.Failed,
                Message = ex.Message,
            };
        }
    }

    // ---------------------------------------------------------------------
    // Auxiliares
    // ---------------------------------------------------------------------

    private static string WindowsFolder =>
        Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    private static string LocalAppData =>
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private static void AddFile(List<JunkItem> items, HashSet<string> seen, string categoryId, string path)
    {
        if (path.Length == 0 || !seen.Add(categoryId + "|" + path)) return;

        items.Add(new JunkItem
        {
            Kind = "Arquivo",
            Path = path,
            CategoryId = categoryId,
            SizeBytes = SafeFileLength(path),
            Selected = true,
        });
    }

    private static void AddFolder(List<JunkItem> items, HashSet<string> seen, string categoryId, string path)
    {
        if (path.Length == 0 || !seen.Add(categoryId + "|" + path)) return;

        items.Add(new JunkItem
        {
            Kind = "Pasta",
            Path = path,
            CategoryId = categoryId,
            SizeBytes = ProgramScanner.MeasureFolder(path),
            Selected = true,
        });
    }

    private static long SafeFileLength(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? info.Length : 0;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao medir '{path}': {ex.Message}");
            return 0;
        }
    }

    private static bool IsLogFile(string path)
    {
        try
        {
            var extension = Path.GetExtension(path);
            foreach (var candidate in LogExtensions)
            {
                if (extension.Equals(candidate, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }
        catch { /* extensao invalida: nao e log */ }

        return false;
    }

    private static IEnumerable<string> SafeEnumerate(string folder)
    {
        try { return Directory.EnumerateFileSystemEntries(folder); }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao listar '{folder}': {ex.Message}");
            return Array.Empty<string>();
        }
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string folder)
    {
        try { return Directory.EnumerateDirectories(folder); }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao listar as pastas de '{folder}': {ex.Message}");
            return Array.Empty<string>();
        }
    }

    private static IEnumerable<string> SafeEnumerateFiles(string folder, string pattern, SearchOption option)
    {
        try
        {
            // A travessia profunda nao atravessa atalho (junction, symlink):
            // o limpador nao pode remover arquivos que estao fora da pasta
            // que ele limpou — e o alvo do link pode ser o perfil de outro
            // usuario da maquina.
            if (option == SearchOption.AllDirectories)
                return SafeTraversal.EnumerateFilesDeep(folder, pattern);

            return Directory.EnumerateFiles(folder, pattern, option).ToList();
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao listar os arquivos de '{folder}': {ex.Message}");
            return Array.Empty<string>();
        }
    }

    private static IEnumerable<string> SafeDrives()
    {
        var result = new List<string>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;
                if (drive.DriveType != DriveType.Fixed) continue;
                var root = drive.RootDirectory.FullName;
                if (!string.IsNullOrEmpty(root)) result.Add(root);
            }
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao listar as unidades: {ex.Message}");
        }

        return result;
    }
}
