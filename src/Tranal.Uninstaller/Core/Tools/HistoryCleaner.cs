using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Tranal.Uninstaller.Core.Tools;

/// <summary>
/// Um vestigio de historico encontrado no sistema.
/// </summary>
public sealed class HistoryItem
{
    /// <summary>"Arquivo", "Pasta" ou "Registro".</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>Caminho completo do item (ou da chave de registro).</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Identificador estavel da categoria (ex.: "run-history", "clipboard").</summary>
    public string CategoryId { get; init; } = string.Empty;

    /// <summary>Descricao legivel do item (titulo e origem).</summary>
    public string Detail { get; init; } = string.Empty;

    /// <summary>Tamanho estimado em bytes (0 quando desconhecido).</summary>
    public long SizeBytes { get; init; }

    /// <summary>True quando o item esta marcado para remocao.</summary>
    public bool Selected { get; set; } = true;

    /// <summary>
    /// Quais arquivos dentro da pasta sao o alvo da limpeza. "*" significa
    /// todo o conteudo da pasta (a pasta em si nunca e removida).
    /// </summary>
    public string Filter { get; init; } = "*";

    /// <summary>
    /// True quando o item so pode ser apagado com o sistema reiniciado
    /// (arquivos travados pelo navegador ou pelo Explorer).
    /// </summary>
    public bool RequiresRestart { get; init; }
}

/// <summary>
/// Limpeza de historico: programas executados, area de transferencia, downloads,
/// recentes, historico de pesquisa e historico dos navegadores.
///
/// Remocao: SEMPRE pelo motor canonico <see cref="UninstallEngine.RemoveLeftovers"/>,
/// exceto a area de transferencia, que e um item sintetico tratado a parte
/// (a API da area de transferencia nao expoe nenhum caminho removivel).
/// </summary>
public static class HistoryCleaner
{
    /// <summary>Identificador da categoria sintetica da area de transferencia.</summary>
    private const string ClipboardCategory = "clipboard";

    /// <summary>Rotulo exibido do item sintetico da area de transferencia.</summary>
    private const string ClipboardLabel = "Area de transferencia";

    /// <summary>Chaves do historico de pesquisa.</summary>
    private static readonly string[] SearchHistoryKeys =
    {
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Searchbox",
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Search\RecentApps",
    };

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    /// <summary>
    /// Varre o sistema por vestigios de historico. Nunca lanca.
    /// </summary>
    public static IReadOnlyList<HistoryItem> Scan()
    {
        var items = new List<HistoryItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Rastreios de navegadores, do Office e do Windows: a varredura vive
        // no TrackCleaner para nao existir duas interpretacoes do mesmo rastro.
        try { items.AddRange(TrackCleaner.ScanAll()); }
        catch (Exception ex) { ToolLog.Error($"Falha ao varrer os rastros: {ex.Message}"); }

        try { AddClipboard(items, seen); }
        catch (Exception ex) { ToolLog.Error($"Falha ao preparar a limpeza da area de transferencia: {ex.Message}"); }

        try { ScanDownloads(items, seen); }
        catch (Exception ex) { ToolLog.Error($"Falha ao varrer os downloads: {ex.Message}"); }

        try { ScanRecent(items, seen); }
        catch (Exception ex) { ToolLog.Error($"Falha ao varrer os itens recentes: {ex.Message}"); }

        try { ScanSearchHistory(items, seen); }
        catch (Exception ex) { ToolLog.Error($"Falha ao varrer o historico de pesquisa: {ex.Message}"); }

        return items;
    }

    /// <summary>
    /// Limpa os vestigios selecionados e devolve o relatorio canonico.
    /// </summary>
    public static RemovalReport Clear(IEnumerable<HistoryItem> items)
    {
        var outcomes = new List<RemovalOutcome>();
        var regular = new List<LeftoverItem>();
        var tracks = new List<HistoryItem>();

        foreach (var item in items)
        {
            if (!item.Selected) continue;

            // Item sintetico: area de transferencia. Nao existe caminho removivel
            // para o motor canonico — a operacao e feita pela API da area de
            // transferencia (user32) e por isso NAO passa pelo RemoveLeftovers.
            if (string.Equals(item.CategoryId, ClipboardCategory, StringComparison.OrdinalIgnoreCase))
            {
                outcomes.Add(ClearClipboard(item));
                continue;
            }

            // Rastros de navegador, Office e Windows: sao removidos pelo
            // TrackCleaner, que sabe adiar para o proximo reinicio o que estiver
            // travado pelo processo que o mantem aberto.
            if (IsTrack(item.CategoryId))
            {
                tracks.Add(item);
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

        if (tracks.Count > 0)
        {
            try { outcomes.AddRange(TrackCleaner.Clean(tracks).Outcomes); }
            catch (Exception ex) { ToolLog.Error($"Falha na limpeza de rastros: {ex.Message}"); }
        }

        if (regular.Count > 0)
        {
            try
            {
                // Limpeza de vestigios e definitiva: nao vai para a Lixeira e
                // nao cria ponto de restauracao (so a desinstalacao cria backup).
                outcomes.AddRange(UninstallEngine.RemoveLeftovers(regular, new UninstallRemovalOptions
                {
                    Backup = false,
                    UseRecycleBin = false,
                    DeferToNextRestart = true,
                }).Outcomes);
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha na limpeza de historico: {ex.Message}");
            }
        }

        return new RemovalReport(outcomes);
    }

    /// <summary>True quando a categoria pertence a um dos modulos do Tracks Cleaner.</summary>
    private static bool IsTrack(string categoryId)
    {
        if (string.IsNullOrEmpty(categoryId)) return false;

        return categoryId.StartsWith(TrackCleaner.CategoryBrowsers + "-", StringComparison.OrdinalIgnoreCase)
            || categoryId.StartsWith(TrackCleaner.CategoryOffice + "-", StringComparison.OrdinalIgnoreCase)
            || categoryId.StartsWith(TrackCleaner.CategoryWindows + "-", StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------------
    // Varredura
    // ---------------------------------------------------------------------

    /// <summary>
    /// Item sintetico da area de transferencia. O "tipo" e apenas um rotulo:
    /// a limpeza e feita pela API da area de transferencia.
    /// </summary>
    private static void AddClipboard(List<HistoryItem> items, HashSet<string> seen)
    {
        if (!seen.Add(ClipboardCategory + "|" + ClipboardLabel)) return;

        items.Add(new HistoryItem
        {
            Kind = "Pasta",
            Path = ClipboardLabel,
            CategoryId = ClipboardCategory,
            SizeBytes = 0,
            Selected = true,
        });
    }

    /// <summary>
    /// Downloads: arquivos da pasta Downloads e a chave que guarda suas preferencias.
    /// </summary>
    private static void ScanDownloads(List<HistoryItem> items, HashSet<string> seen)
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (Directory.Exists(folder))
        {
            foreach (var file in SafeEnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    AddFile(items, seen, "downloads", PathUtil.Normalize(file));
                }
                catch (Exception ex)
                {
                    ToolLog.Error($"Falha ao inspecionar '{file}': {ex.Message}");
                }
            }
        }

        try
        {
            const string downloadFolder =
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\DownloadFolder";
            if (RegistryKeyExists(downloadFolder)) AddRegistry(items, seen, "downloads", downloadFolder);
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao inspecionar a chave DownloadFolder: {ex.Message}");
        }
    }

    /// <summary>
    /// Itens recentes: atalhos da pasta Recent do Windows.
    /// </summary>
    private static void ScanRecent(List<HistoryItem> items, HashSet<string> seen)
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Recent");

        if (!Directory.Exists(folder)) return;

        foreach (var file in SafeEnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly))
        {
            try
            {
                AddFile(items, seen, "recent", PathUtil.Normalize(file));
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao inspecionar '{file}': {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Historico de pesquisa: Searchbox e a lista de aplicativos recentes pesquisados.
    /// </summary>
    private static void ScanSearchHistory(List<HistoryItem> items, HashSet<string> seen)
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

    // ---------------------------------------------------------------------
    // Limpeza
    // ---------------------------------------------------------------------

    /// <summary>
    /// Esvazia a area de transferencia pela API do user32.
    /// Item sintetico: nao passa pelo motor canonico porque nao existe caminho removivel.
    /// </summary>
    private static RemovalOutcome ClearClipboard(HistoryItem item)
    {
        var leftover = new LeftoverItem
        {
            Kind = "Pasta",
            Path = item.Path,
            Detail = item.CategoryId,
            Selected = true,
        };

        var opened = false;
        try
        {
            // Outro aplicativo pode estar com a area de transferencia aberta: ha algumas tentativas.
            for (var attempt = 0; attempt < 5 && !opened; attempt++)
            {
                opened = OpenClipboard(IntPtr.Zero);
                if (!opened) Thread.Sleep(50);
            }

            if (!opened)
            {
                return new RemovalOutcome
                {
                    Item = leftover,
                    Status = RemovalStatus.Failed,
                    Message = "a area de transferencia esta em uso por outro aplicativo",
                };
            }

            if (!EmptyClipboard())
            {
                return new RemovalOutcome
                {
                    Item = leftover,
                    Status = RemovalStatus.Failed,
                    Message = "nao foi possivel esvaziar a area de transferencia",
                };
            }

            return new RemovalOutcome
            {
                Item = leftover,
                Status = RemovalStatus.Removed,
                Message = "area de transferencia esvaziada",
            };
        }
        catch (Exception ex)
        {
            ToolLog.Error("Falha ao esvaziar a area de transferencia: " + ex.Message);
            return new RemovalOutcome
            {
                Item = leftover,
                Status = RemovalStatus.Failed,
                Message = ex.Message,
            };
        }
        finally
        {
            if (opened)
            {
                try { CloseClipboard(); }
                catch (Exception ex) { ToolLog.Error("Falha ao fechar a area de transferencia: " + ex.Message); }
            }
        }
    }

    // ---------------------------------------------------------------------
    // Auxiliares
    // ---------------------------------------------------------------------

    private static void AddFile(List<HistoryItem> items, HashSet<string> seen, string categoryId, string path)
    {
        if (path.Length == 0 || !seen.Add(categoryId + "|" + path)) return;

        items.Add(new HistoryItem
        {
            Kind = "Arquivo",
            Path = path,
            CategoryId = categoryId,
            SizeBytes = SafeFileLength(path),
            Selected = true,
        });
    }

    private static void AddRegistry(List<HistoryItem> items, HashSet<string> seen, string categoryId, string keyPath)
    {
        if (keyPath.Length == 0 || !seen.Add(categoryId + "|" + keyPath)) return;

        items.Add(new HistoryItem
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

    private static IEnumerable<string> SafeEnumerateDirectories(string folder)
    {
        try { return Directory.EnumerateDirectories(folder).ToList(); }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao listar as pastas de '{folder}': {ex.Message}");
            return Array.Empty<string>();
        }
    }
}
