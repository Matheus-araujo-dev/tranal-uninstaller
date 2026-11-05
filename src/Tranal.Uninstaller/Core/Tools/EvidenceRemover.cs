using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace Tranal.Uninstaller.Core.Tools;

/// <summary>
/// Um vestigio de uso encontrado no sistema (item que pode ser eliminado).
/// </summary>
public sealed class EvidenceItem
{
    /// <summary>"Arquivo", "Pasta" ou "Registro".</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>Caminho completo (arquivo/pasta) ou caminho da chave de registro.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Identificador estavel da categoria (ex.: "prefetch", "shellbags").</summary>
    public string CategoryId { get; init; } = string.Empty;

    /// <summary>Tamanho estimado em bytes (0 para chaves de registro).</summary>
    public long SizeBytes { get; init; }

    /// <summary>True quando o item esta marcado para remocao.</summary>
    public bool Selected { get; set; } = true;
}

/// <summary>
/// Remocao de vestigios de uso: prefetch, shellbags, jump lists, documentos recentes,
/// historico de pesquisa, logs de aplicacao e cache DNS.
///
/// Remocao: SEMPRE pelo motor canonico <see cref="UninstallEngine.RemoveLeftovers"/>,
/// exceto o item sintetico de cache DNS, que e um comando do sistema
/// (ipconfig /flushdns) e nao uma chave de registro — por isso e tratado a parte.
/// </summary>
public static class EvidenceRemover
{
    /// <summary>Identificador da categoria sintetica de cache DNS.</summary>
    private const string DnsCacheCategory = "dns-cache";

    /// <summary>Chaves de shellbag (historico de pastas visitadas pelo Explorer).</summary>
    private static readonly string[] ShellBagKeys =
    {
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\Shell\BagMRU",
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\ShellNoRoam\BagMRU",
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\Classes\Local Settings\Software\Microsoft\Windows\Shell\BagMRU",
    };

    /// <summary>Chaves de historico de pesquisa do Explorer.</summary>
    private static readonly string[] SearchHistoryKeys =
    {
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\WordWheelQuery",
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\RunMRU",
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\TypedPaths",
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Searchbox",
    };

    /// <summary>
    /// Varre o sistema por vestigios de uso. Nunca lanca.
    /// </summary>
    public static IReadOnlyList<EvidenceItem> Scan()
    {
        var items = new List<EvidenceItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try { ScanPrefetch(items, seen); }
        catch (Exception ex) { ToolLog.Error($"Falha ao varrer o Prefetch: {ex.Message}"); }

        try { ScanShellBags(items, seen); }
        catch (Exception ex) { ToolLog.Error($"Falha ao varrer os shellbags: {ex.Message}"); }

        try { ScanJumpLists(items, seen); }
        catch (Exception ex) { ToolLog.Error($"Falha ao varrer as jump lists: {ex.Message}"); }

        try { ScanRecentDocuments(items, seen); }
        catch (Exception ex) { ToolLog.Error($"Falha ao varrer os documentos recentes: {ex.Message}"); }

        try { ScanSearchHistory(items, seen); }
        catch (Exception ex) { ToolLog.Error($"Falha ao varrer o historico de pesquisa: {ex.Message}"); }

        try { ScanEventLogs(items, seen); }
        catch (Exception ex) { ToolLog.Error($"Falha ao varrer os logs de evento: {ex.Message}"); }

        try { AddDnsCache(items, seen); }
        catch (Exception ex) { ToolLog.Error($"Falha ao preparar a limpeza do cache DNS: {ex.Message}"); }

        return items;
    }

    /// <summary>
    /// Remove os vestigios selecionados e devolve o relatorio canonico.
    /// </summary>
    public static RemovalReport Remove(IEnumerable<EvidenceItem> items)
    {
        var outcomes = new List<RemovalOutcome>();
        var regular = new List<LeftoverItem>();

        foreach (var item in items)
        {
            if (!item.Selected) continue;

            // Item sintetico: cache DNS. Nao e uma chave de registro — e um comando
            // do sistema — portanto NAO passa pelo motor canonico (que interpretaria
            // o caminho como chave e tentaria remove-la).
            if (string.Equals(item.CategoryId, DnsCacheCategory, StringComparison.OrdinalIgnoreCase))
            {
                outcomes.Add(FlushDnsCache(item));
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
                ToolLog.Error($"Falha na remocao de vestigios: {ex.Message}");
            }
        }

        return new RemovalReport(outcomes);
    }

    // ---------------------------------------------------------------------
    // Varredura
    // ---------------------------------------------------------------------

    /// <summary>
    /// Prefetch: arquivos .pf da pasta de prefetch do Windows.
    /// </summary>
    private static void ScanPrefetch(List<EvidenceItem> items, HashSet<string> seen)
    {
        var folder = Path.Combine(WindowsFolder, "Prefetch");
        if (!Directory.Exists(folder)) return;

        foreach (var file in SafeEnumerateFiles(folder, "*.pf", SearchOption.TopDirectoryOnly))
        {
            try
            {
                AddFile(items, seen, "prefetch", PathUtil.Normalize(file));
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao inspecionar '{file}': {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Shellbags: chaves de registro com o historico de pastas visitadas.
    /// </summary>
    private static void ScanShellBags(List<EvidenceItem> items, HashSet<string> seen)
    {
        foreach (var keyPath in ShellBagKeys)
        {
            try
            {
                if (!RegistryKeyExists(keyPath)) continue;
                AddRegistry(items, seen, "shellbags", keyPath);
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao inspecionar '{keyPath}': {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Jump lists: listas de acesso rapido em Recent\AutomaticDestinations e CustomDestinations.
    /// </summary>
    private static void ScanJumpLists(List<EvidenceItem> items, HashSet<string> seen)
    {
        var recent = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Recent");

        var roots = new[]
        {
            Path.Combine(recent, "AutomaticDestinations"),
            Path.Combine(recent, "CustomDestinations"),
        };

        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;

            foreach (var file in SafeEnumerateFiles(root, "*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    AddFile(items, seen, "jumplists", PathUtil.Normalize(file));
                }
                catch (Exception ex)
                {
                    ToolLog.Error($"Falha ao inspecionar '{file}': {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Documentos recentes: atalhos .lnk da pasta Recent e a chave RecentDocs.
    /// </summary>
    private static void ScanRecentDocuments(List<EvidenceItem> items, HashSet<string> seen)
    {
        var recent = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Recent");

        if (Directory.Exists(recent))
        {
            foreach (var file in SafeEnumerateFiles(recent, "*.lnk", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    AddFile(items, seen, "recent-docs", PathUtil.Normalize(file));
                }
                catch (Exception ex)
                {
                    ToolLog.Error($"Falha ao inspecionar '{file}': {ex.Message}");
                }
            }
        }

        try
        {
            const string recentDocs = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\RecentDocs";
            if (RegistryKeyExists(recentDocs)) AddRegistry(items, seen, "recent-docs", recentDocs);
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao inspecionar a chave RecentDocs: {ex.Message}");
        }
    }

    /// <summary>
    /// Historico de pesquisa do Explorer: WordWheelQuery, RunMRU, TypedPaths e Searchbox.
    /// </summary>
    private static void ScanSearchHistory(List<EvidenceItem> items, HashSet<string> seen)
    {
        foreach (var keyPath in SearchHistoryKeys)
        {
            try
            {
                if (!RegistryKeyExists(keyPath)) continue;
                AddRegistry(items, seen, "search-history", keyPath);
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao inspecionar '{keyPath}': {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Logs de evento: APENAS o log de Aplicacao.
    /// Os logs de Sistema e de Seguranca nunca sao expostos.
    /// </summary>
    private static void ScanEventLogs(List<EvidenceItem> items, HashSet<string> seen)
    {
        var folder = Path.Combine(WindowsFolder, "System32", "winevt", "Logs");
        if (!Directory.Exists(folder)) return;

        var applicationLog = Path.Combine(folder, "Application.evtx");
        try
        {
            if (File.Exists(applicationLog)) AddFile(items, seen, "event-logs", PathUtil.Normalize(applicationLog));
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao inspecionar '{applicationLog}': {ex.Message}");
        }
    }

    /// <summary>
    /// Item sintetico do cache DNS: o "endereco" e um comando do sistema,
    /// nao uma chave de registro.
    /// </summary>
    private static void AddDnsCache(List<EvidenceItem> items, HashSet<string> seen)
    {
        const string path = "DNS Cache";
        if (!seen.Add(DnsCacheCategory + "|" + path)) return;

        items.Add(new EvidenceItem
        {
            Kind = "Registro",
            Path = path,
            CategoryId = DnsCacheCategory,
            SizeBytes = 0,
            Selected = true,
        });
    }

    // ---------------------------------------------------------------------
    // Remocao
    // ---------------------------------------------------------------------

    /// <summary>
    /// Executa "ipconfig /flushdns" e reporta o resultado.
    /// </summary>
    private static RemovalOutcome FlushDnsCache(EvidenceItem item)
    {
        var leftover = new LeftoverItem
        {
            Kind = "Registro",
            Path = item.Path,
            Detail = item.CategoryId,
            Selected = true,
        };

        try
        {
            var startInfo = new ProcessStartInfo("ipconfig.exe", "/flushdns")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new RemovalOutcome
                {
                    Item = leftover,
                    Status = RemovalStatus.Failed,
                    Message = "o processo ipconfig nao pôde ser iniciado",
                };
            }

            process.WaitForExit(10_000);
            var output = process.StandardOutput.ReadToEnd();

            if (process.ExitCode == 0)
            {
                return new RemovalOutcome
                {
                    Item = leftover,
                    Status = RemovalStatus.Removed,
                    Message = string.IsNullOrWhiteSpace(output) ? "cache DNS limpo" : output.Trim(),
                };
            }

            return new RemovalOutcome
            {
                Item = leftover,
                Status = RemovalStatus.Failed,
                Message = $"ipconfig retornou o codigo {process.ExitCode}",
            };
        }
        catch (Exception ex)
        {
            ToolLog.Error("Falha ao limpar o cache DNS: " + ex.Message);
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

    private static void AddFile(List<EvidenceItem> items, HashSet<string> seen, string categoryId, string path)
    {
        if (path.Length == 0 || !seen.Add(categoryId + "|" + path)) return;

        items.Add(new EvidenceItem
        {
            Kind = "Arquivo",
            Path = path,
            CategoryId = categoryId,
            SizeBytes = SafeFileLength(path),
            Selected = true,
        });
    }

    private static void AddRegistry(List<EvidenceItem> items, HashSet<string> seen, string categoryId, string keyPath)
    {
        if (keyPath.Length == 0 || !seen.Add(categoryId + "|" + keyPath)) return;

        items.Add(new EvidenceItem
        {
            Kind = "Registro",
            Path = keyPath,
            CategoryId = categoryId,
            SizeBytes = 0,
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

    /// <summary>
    /// Verdadeiro quando a chave existe na visao padrao do registro.
    /// </summary>
    private static bool RegistryKeyExists(string keyPath)
    {
        try
        {
            var (hive, sub) = SafetyRules.SplitRegistryPath(keyPath);
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(sub);
            return key is not null;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao consultar '{keyPath}': {ex.Message}");
            return false;
        }
    }

    private static IEnumerable<string> SafeEnumerateFiles(string folder, string pattern, SearchOption option)
    {
        try { return Directory.EnumerateFiles(folder, pattern, option).ToList(); }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao listar os arquivos de '{folder}': {ex.Message}");
            return Array.Empty<string>();
        }
    }
}
