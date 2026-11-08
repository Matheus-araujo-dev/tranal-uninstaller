using System.IO;
using Microsoft.Win32;

namespace Tranal.Uninstaller.Core.Tools;

/// <summary>
/// Modulo do "Tracks Cleaner". O Revo Uninstaller Pro separa a limpeza de
/// rastros em tres modulos; o Tranal mantem a mesma separacao.
/// </summary>
public enum TrackModule
{
    /// <summary>Rastros dos navegadores (paginas visitadas, cookies, cache, formularios).</summary>
    Browsers,

    /// <summary>Documentos recentes do Microsoft Office.</summary>
    Office,

    /// <summary>Rastros do proprio Windows (documentos recentes, MRU, miniaturas, dumps).</summary>
    Windows,

    /// <summary>
    /// Fragmentos de arquivo recuperados pelo CHKDSK (o "Check Disk" do
    /// Windows), que ficam na raiz das unidades. No Revo eles fazem parte do
    /// limpador do Windows (item 725); aqui eles tambem tem modulo proprio,
    /// porque a regra de quais arquivos sao fragmentos e de como eles sao
    /// removidos mora em <see cref="ChkdskFragmentCleaner"/>.
    /// </summary>
    Chkdsk,
}

/// <summary>
/// Tracks Cleaner do Tranal Uninstaller Pro: rastros de navegadores, do Office
/// e do proprio Windows.
///
/// Os alvos foram **medidos** nesta maquina e no Windows 11, nao adivinhados:
/// cada chave e cada pasta abaixo existe de fato no sistema de quem roda o
/// aplicativo. Rastros que nao existem simplesmente nao sao listados.
///
/// O tipo produzido e o mesmo <see cref="HistoryItem"/> das demais ferramentas
/// de limpeza — nao existe um segundo tipo de "item de limpeza" no projeto.
/// </summary>
public static class TrackCleaner
{
    private const string ExplorerKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer";

    /// <summary>Prefixo de categoria dos rastros de navegador.</summary>
    public const string CategoryBrowsers = "browsers";

    /// <summary>Prefixo de categoria dos rastros do Office.</summary>
    public const string CategoryOffice = "office";

    /// <summary>Prefixo de categoria dos rastros do Windows.</summary>
    public const string CategoryWindows = "windows";

    /// <summary>Subchaves de MRU usadas pelas versoes do Office (medidas no Office 16.0).</summary>
    private static readonly string[] OfficeMruKeys =
    {
        "File MRU", "User MRU", "ResMRU", "MRU", "Place MRU", "Reading Locations",
    };

    /// <summary>Aplicativos do Office que tem lista de documentos recentes (medidos).</summary>
    private static readonly string[] OfficeApps =
    {
        "Word", "Excel", "Access", "PowerPoint", "PowerPnt", "FrontPage", "Publisher", "Visio", "OneNote", "Project",
    };

    private static readonly string[] ChromiumBrowsers =
    {
        @"Google\Chrome",
        @"Microsoft\Edge",
        @"BraveSoftware\Brave-Browser",
        @"Vivaldi",
        @"Chromium",
    };

    // -----------------------------------------------------------------
    // Varredura
    // -----------------------------------------------------------------

    /// <summary>Varre um modulo e devolve os rastros encontrados.</summary>
    public static IReadOnlyList<HistoryItem> Scan(TrackModule module) => module switch
    {
        TrackModule.Browsers => ScanBrowsers(),
        TrackModule.Office => ScanOffice(),
        TrackModule.Chkdsk => ScanChkdsk(),
        _ => ScanWindows(),
    };

    /// <summary>Varre os tres modulos.</summary>
    public static IReadOnlyList<HistoryItem> ScanAll()
    {
        var items = new List<HistoryItem>();
        items.AddRange(ScanBrowsers());
        items.AddRange(ScanOffice());
        items.AddRange(ScanWindows());
        return items;
    }

    /// <summary>
    /// Rastreios dos navegadores: paginas visitadas, barra de enderecos,
    /// cookies, historico de formularios, downloads, sessoes e arquivos
    /// temporarios da internet.
    /// </summary>
    private static IReadOnlyList<HistoryItem> ScanBrowsers()
    {
        var items = new List<HistoryItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var browser in ChromiumBrowsers)
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), browser);
            if (!Directory.Exists(root)) continue;

            var userData = Path.Combine(root, "User Data");
            var dataRoot = Directory.Exists(userData) ? userData : root;

            foreach (var profile in EnumerateProfiles(dataRoot))
            {
                var profilePath = Path.Combine(dataRoot, profile);
                var label = $"{browser} ({profile})";

                AddFile(items, seen, "browsers-history",
                    "Histórico de páginas visitadas", label, Path.Combine(profilePath, "History"));

                AddFile(items, seen, "browsers-historyjournal",
                    "Diário do histórico de páginas visitadas", label, Path.Combine(profilePath, "History-journal"));

                AddFile(items, seen, "browsers-topsites",
                    "Histórico da barra de endereços", label, Path.Combine(profilePath, "Top Sites"));

                AddFile(items, seen, "browsers-cookies",
                    "Cookies", label, Path.Combine(profilePath, "Network", "Cookies"));

                AddFile(items, seen, "browsers-formhistory",
                    "Histórico de formulários", label, Path.Combine(profilePath, "Web Data"));

                AddFolder(items, seen, "browsers-sessions",
                    "Histórico de sessão", label, Path.Combine(profilePath, "Sessions"));

                AddFolder(items, seen, "browsers-cache",
                    "Arquivos temporários da internet", label, Path.Combine(profilePath, "Cache"));

                AddFolder(items, seen, "browsers-codecache",
                    "Cache de código", label, Path.Combine(profilePath, "Code Cache"));

                AddFolder(items, seen, "browsers-gpucache",
                    "Cache da GPU", label, Path.Combine(profilePath, "GPUCache"));

                AddFolder(items, seen, "browsers-serviceworker",
                    "Cache de service workers", label, Path.Combine(profilePath, "Service Worker", "CacheStorage"));
            }
        }

        // Firefox
        var firefox = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Mozilla", "Firefox", "Profiles");
        if (Directory.Exists(firefox))
        {
            foreach (var profile in Directory.EnumerateDirectories(firefox))
            {
                var name = Path.GetFileName(profile);
                var label = $"Firefox ({name})";

                AddFile(items, seen, "browsers-history",
                    "Histórico de páginas visitadas", label, Path.Combine(profile, "places.sqlite"));
                AddFile(items, seen, "browsers-cookies",
                    "Cookies", label, Path.Combine(profile, "cookies.sqlite"));
                AddFile(items, seen, "browsers-formhistory",
                    "Histórico de formulários", label, Path.Combine(profile, "formhistory.sqlite"));
                AddFolder(items, seen, "browsers-cache",
                    "Arquivos temporários da internet", label, Path.Combine(profile, "cache2"));
                AddFolder(items, seen, "browsers-session",
                    "Histórico de sessão", label, Path.Combine(profile, "sessionstore-backups"));
            }
        }

        // Internet Explorer / WebCache legado. Os arquivos ficam travados pelo
        // processo do navegador: so podem ser apagados no proximo reinicio.
        var webCache = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Windows", "WebCache");
        AddFile(items, seen, "browsers-indexdat",
            "Index.dat (Internet Explorer)", "Internet Explorer",
            Path.Combine(webCache, "WebCacheV01.dat"), requiresRestart: true);

        var inetCache = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Windows", "INetCache");
        AddFolder(items, seen, "browsers-inetcache",
            "Arquivos temporários da internet", "Internet Explorer", inetCache);

        // O rastro da barra de enderecos (TypedPaths) fica no limpador do
        // Windows: e onde o Revo o coloca ("Clear the Printers, Computers and
        // People Find History").

        return items;
    }

    /// <summary>
    /// Documentos recentes do Microsoft Office. A varredura desce nas versoes
    /// instaladas e procura as chaves de MRU medidas em cada aplicativo.
    /// </summary>
    private static IReadOnlyList<HistoryItem> ScanOffice()
    {
        var items = new List<HistoryItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        const string officeRoot = @"HKEY_CURRENT_USER\Software\Microsoft\Office";
        if (!RegistryKeyExists(officeRoot)) return items;

        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
        using var root = baseKey.OpenSubKey(@"Software\Microsoft\Office");
        if (root is null) return items;

        foreach (var versionName in root.GetSubKeyNames())
        {
            using var version = root.OpenSubKey(versionName);
            if (version is null) continue;

            foreach (var app in OfficeApps)
            {
                using var appKey = version.OpenSubKey(app);
                if (appKey is null) continue;

                foreach (var mru in OfficeMruKeys)
                {
                    if (!appKey.GetSubKeyNames().Contains(mru, StringComparer.OrdinalIgnoreCase)) continue;

                    AddRegistry(items, seen, "office-mru",
                        $"Documentos recentes do {app}", $"Office {versionName}",
                        $@"HKEY_CURRENT_USER\Software\Microsoft\Office\{versionName}\{app}\{mru}");
                }
            }
        }

        return items;
    }

    /// <summary>
    /// Rastros do proprio Windows: documentos recentes, historicos do MRU,
    /// miniaturas, dumps de memoria, fragmentos do chkdsk e arquivos temporarios.
    /// </summary>
    private static IReadOnlyList<HistoryItem> ScanWindows()
    {
        var items = new List<HistoryItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddRegistry(items, seen, "windows-recentdocs",
            "Documentos recentes", "Windows", $@"HKEY_CURRENT_USER\{ExplorerKey}\RecentDocs");

        AddRegistry(items, seen, "windows-runmru",
            "Histórico do menu Iniciar (Executar)", "Windows", $@"HKEY_CURRENT_USER\{ExplorerKey}\RunMRU");

        AddRegistry(items, seen, "windows-wordwheel",
            "Histórico de pesquisa de arquivos", "Windows", $@"HKEY_CURRENT_USER\{ExplorerKey}\WordWheelQuery");

        AddRegistry(items, seen, "windows-opensave",
            "Histórico de Abrir/Salvar", "Windows", $@"HKEY_CURRENT_USER\{ExplorerKey}\ComDlg32\OpenSavePidlMRU");

        AddRegistry(items, seen, "windows-lastvisited",
            "Última pasta visitada nos diálogos", "Windows", $@"HKEY_CURRENT_USER\{ExplorerKey}\ComDlg32\LastVisitedPidlMRU");

        AddRegistry(items, seen, "windows-userassist",
            "Histórico de programas executados", "Windows", $@"HKEY_CURRENT_USER\{ExplorerKey}\UserAssist");

        AddRegistry(items, seen, "windows-appswitched",
            "Histórico de troca de aplicativo", "Windows", $@"HKEY_CURRENT_USER\{ExplorerKey}\FeatureUsage\AppSwitched");

        AddRegistry(items, seen, "windows-jumplist",
            "Histórico do Alt+Tab", "Windows", $@"HKEY_CURRENT_USER\{ExplorerKey}\FeatureUsage\ShowJumpView");

        AddRegistry(items, seen, "windows-shellbags",
            "Histórico de uso do Explorer", "Windows",
            @"HKEY_CURRENT_USER\Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\Bags");

        // Miniaturas do Explorer: os arquivos ficam travados pelo processo.
        var explorerCache = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Windows", "Explorer");
        AddFolder(items, seen, "windows-thumbnailcache",
            "Cache de miniaturas", "Windows", explorerCache, requiresRestart: true,
            filter: "thumbcache_*.db");

        AddFolder(items, seen, "windows-crashdumps",
            "Dumps de memória de falha", "Windows",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps"));

        AddFolder(items, seen, "windows-minidump",
            "Mini dumps de falha", "Windows", Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Minidump"));

        AddFolder(items, seen, "windows-livekernel",
            "Relatos de falha do kernel", "Windows", Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows), "LiveKernelReports"));

        AddFolder(items, seen, "windows-temp",
            "Arquivos temporários do Windows", "Windows", Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"));

        AddFolder(items, seen, "windows-usertemp",
            "Arquivos temporários do usuário", "Windows", Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"));

        // Fragmentos recuperados pelo chkdsk ficam na raiz das unidades.
        // A regra — quais arquivos sao fragmentos, e como eles sao removidos —
        // mora em ChkdskFragmentCleaner.
        items.AddRange(ScanChkdsk());

        // "Empty the Recycle Bin" do Windows Tracks Cleaner do Revo. E um item
        // sintetico: nao existe caminho removivel, a operacao e a da propria
        // Lixeira.
        items.Add(new HistoryItem
        {
            Kind = "Lixeira",
            Path = "Lixeira",
            CategoryId = "windows-recyclebin",
            Detail = "Esvaziar a Lixeira — todas as unidades",
            SizeBytes = 0,
            Selected = true,
        });

        // "Clear the Printers, Computers and People Find History": o Revo
        // coloca este rastro no limpador do Windows, e no de navegadores.
        AddRegistry(items, seen, "windows-typedpaths",
            "Histórico de impressoras, computadores e pessoas", "Windows",
            $@"HKEY_CURRENT_USER\{ExplorerKey}\TypedPaths");

        // "Clear the Regedit Last Opened Key History".
        AddRegistry(items, seen, "windows-regedit-lastkey",
            "Histórico da última chave aberta no Regedit", "Windows",
            $@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Applets\Regedit");

        return items;
    }

    /// <summary>
    /// Fragmentos de arquivo recuperados pelo CHKDSK (item 725 do Windows
    /// Tracks Cleaner do Revo: "Delete Chkdsk (Check Disk) recovered file
    /// fragments").
    ///
    /// Cada fragmento e um item: o arquivo em si, e nao a raiz da unidade.
    /// Listar a raiz com um filtro "*.CHK" esconderia o que existe de verdade
    /// — e, com o filtro trocado, apagaria o conteudo inteiro da unidade.
    /// </summary>
    private static IReadOnlyList<HistoryItem> ScanChkdsk()
    {
        var items = new List<HistoryItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var fragment in ChkdskFragmentCleaner.FindFragments())
        {
            if (!seen.Add(ChkdskFragmentCleaner.CategoryId + "|" + fragment)) continue;

            items.Add(new HistoryItem
            {
                Kind = "Arquivo",
                Path = fragment,
                CategoryId = ChkdskFragmentCleaner.CategoryId,
                Detail = $"Fragmentos recuperados pelo CHKDSK — {DriveLabel(fragment)}",
                SizeBytes = SafeLength(fragment),
                Selected = true,
            });
        }

        return items;
    }

    /// <summary>
    /// A unidade a que o arquivo pertence (ex.: "C:\").
    /// </summary>
    private static string DriveLabel(string path)
    {
        try
        {
            var root = Path.GetPathRoot(path);
            if (!string.IsNullOrEmpty(root)) return root;
        }
        catch { /* caminho invalido: sem unidade */ }

        return string.Empty;
    }

    // -----------------------------------------------------------------
    // Limpeza
    // -----------------------------------------------------------------

    /// <summary>
    /// Limpa os rastros selecionados e devolve o relatorio canonico.
    /// Itens travados que nao puderam ser removidos sao agendados para o
    /// proximo reinicio, como faz o Revo com o Index.dat.
    /// </summary>
    public static RemovalReport Clean(IEnumerable<HistoryItem> entries)
    {
        var outcomes = new List<RemovalOutcome>();

        foreach (var entry in entries)
        {
            if (!entry.Selected) continue;

            try
            {
                outcomes.Add(ClearOne(entry));
            }
            catch (Exception ex)
            {
                outcomes.Add(new RemovalOutcome
                {
                    Item = ToLeftover(entry),
                    Status = RemovalStatus.Failed,
                    Message = ex.Message,
                });
            }
        }

        return new RemovalReport(outcomes);
    }

    private static RemovalOutcome ClearOne(HistoryItem entry)
    {
        try
        {
            // A lista de exclusoes protege tambem as ferramentas: um item
            // excluido nao e limpo pelo Tracks Cleaner. O item sintetico da
            // Lixeira nao tem caminho e nao passa por aqui.
            if (entry.Kind is "Registro" or "Pasta" or "Arquivo"
                && ExcludeList.Current.Contains(entry.Kind, entry.Path))
            {
                return Fail(entry, "item na lista de exclusões");
            }

            // Os fragmentos do CHKDSK tem regra propria de remocao (vão para a
            // Lixeira), e essa regra mora em ChkdskFragmentCleaner.
            if (entry.CategoryId == ChkdskFragmentCleaner.CategoryId && entry.Kind == "Arquivo")
                return ClearChkdsk(entry);

            switch (entry.Kind)
            {
                case "Lixeira":
                {
                    // Item sintetico: nao ha caminho removivel. A operacao e a
                    // da propria Lixeira.
                    return RecycleBin.Empty()
                        ? new RemovalOutcome { Item = ToLeftover(entry), Status = RemovalStatus.Removed, Message = "lixeira esvaziada" }
                        : Fail(entry, "nao foi possivel esvaziar a Lixeira");
                }

                case "Registro":
                {
                    if (SafetyRules.IsProtectedRegistryKey(entry.Path))
                        return Fail(entry, "chave de registro protegida");

                    var (hive, sub) = SafetyRules.SplitRegistryPath(entry.Path);
                    if (sub.Length == 0) return Fail(entry, "caminho de registro invalido");

                    using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                    using var key = baseKey.OpenSubKey(sub);
                    if (key is null)
                        return Absent(entry);

                    baseKey.DeleteSubKeyTree(sub, throwOnMissingSubKey: false);
                    return new RemovalOutcome { Item = ToLeftover(entry), Status = RemovalStatus.Removed, Message = "removida" };
                }

                case "Pasta":
                {
                    var full = PathUtil.Normalize(entry.Path);
                    if (SafetyRules.IsProtectedFolder(full))
                        return Fail(entry, "pasta de sistema protegida");

                    if (!Directory.Exists(full)) return Absent(entry);

                    var targets = ResolveFolderTargets(entry);
                    if (targets.Count == 0) return Absent(entry);

                    var removed = 0;
                    var deferred = 0;
                    foreach (var target in targets)
                    {
                        try
                        {
                            if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
                            else if (File.Exists(target)) File.Delete(target);
                            else continue;
                            removed++;
                        }
                        catch
                        {
                            // Travado pelo navegador ou pelo Explorer: vai para o proximo reinicio.
                            if (DeferredDelete.Schedule(target)) deferred++;
                        }
                    }

                    if (removed == 0 && deferred == 0) return Absent(entry);

                    return new RemovalOutcome
                    {
                        Item = ToLeftover(entry),
                        Status = removed > 0 ? RemovalStatus.Removed : RemovalStatus.Deferred,
                        Message = deferred > 0
                            ? $"{removed} removido(s), {deferred} agendado(s) para o próximo reinício"
                            : $"{removed} removido(s)",
                    };
                }

                case "Arquivo":
                {
                    var full = PathUtil.Normalize(entry.Path);
                    if (SafetyRules.IsProtectedFolder(Path.GetDirectoryName(full) ?? string.Empty))
                        return Fail(entry, "caminho de sistema protegido");

                    if (!File.Exists(full)) return Absent(entry);

                    try
                    {
                        File.Delete(full);
                        return new RemovalOutcome { Item = ToLeftover(entry), Status = RemovalStatus.Removed, Message = "removido" };
                    }
                    catch
                    {
                        if (DeferredDelete.Schedule(full))
                            return new RemovalOutcome
                            {
                                Item = ToLeftover(entry),
                                Status = RemovalStatus.Deferred,
                                Message = "agendado para o próximo reinício do sistema",
                            };

                        return new RemovalOutcome { Item = ToLeftover(entry), Status = RemovalStatus.Failed, Message = "não foi possível remover" };
                    }
                }

                default:
                    return new RemovalOutcome
                    {
                        Item = ToLeftover(entry),
                        Status = RemovalStatus.Failed,
                        Message = $"tipo desconhecido: {entry.Kind}",
                    };
            }
        }
        catch (Exception ex)
        {
            return new RemovalOutcome { Item = ToLeftover(entry), Status = RemovalStatus.Failed, Message = ex.Message };
        }
    }

    // -----------------------------------------------------------------
    // Auxiliares
    // -----------------------------------------------------------------

    /// <summary>
    /// Limpa um fragmento do CHKDSK e traduz o resultado para o relatorio
    /// canonico. A remocao em si e feita por <see cref="ChkdskFragmentCleaner"/>.
    /// </summary>
    private static RemovalOutcome ClearChkdsk(HistoryItem entry)
    {
        var result = ChkdskFragmentCleaner.Clean(entry.Path);

        return new RemovalOutcome
        {
            Item = ToLeftover(entry),
            Status = result.Status switch
            {
                ChkdskCleanupStatus.Removed => RemovalStatus.Removed,
                ChkdskCleanupStatus.AlreadyAbsent => RemovalStatus.AlreadyAbsent,
                _ => RemovalStatus.Failed,
            },
            Message = result.Message,
        };
    }

    /// <summary>
    /// Resolve os alvos concretos dentro de uma pasta de rastro: os arquivos
    /// filtrados, ou todo o conteudo quando a limpeza e da pasta inteira.
    /// A propria pasta nunca e removida — apenas o conteudo.
    /// </summary>
    private static List<string> ResolveFolderTargets(HistoryItem entry)
    {
        var full = PathUtil.Normalize(entry.Path);
        var result = new List<string>();

        try
        {
            // A raiz de uma unidade NUNCA e alvo. O unico caso legitimo e o
            // dos fragmentos do CHKDSK, que ficam na raiz e tem filtro proprio
            // (".CHK"): qualquer outro filtro ali seria uma lista de alvos
            // indevida.
            if (IsDriveRoot(full))
            {
                if (entry.Filter == "*" || entry.Filter.Length == 0) return result;
                if (!Directory.Exists(full)) return result;

                foreach (var file in Directory.EnumerateFiles(full, entry.Filter, SearchOption.TopDirectoryOnly))
                    result.Add(file);
                return result;
            }

            if (!Directory.Exists(full)) return result;

            if (entry.Filter == "*")
            {
                foreach (var file in Directory.EnumerateFiles(full, "*", SearchOption.TopDirectoryOnly))
                    result.Add(file);
                foreach (var dir in Directory.EnumerateDirectories(full, "*", SearchOption.TopDirectoryOnly))
                    result.Add(dir);
                return result;
            }

            foreach (var file in Directory.EnumerateFiles(full, entry.Filter, SearchOption.AllDirectories))
                result.Add(file);
        }
        catch { /* pasta inacessivel: nada a remover */ }

        return result;
    }

    private static bool IsDriveRoot(string path)
    {
        try
        {
            var root = Path.GetPathRoot(path);
            return !string.IsNullOrEmpty(root) && path.TrimEnd('\\').Length <= root.TrimEnd('\\').Length;
        }
        catch { return false; }
    }

    private static IEnumerable<string> EnumerateProfiles(string userData)
    {
        var result = new List<string>();
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(userData))
            {
                var name = Path.GetFileName(dir);
                if (name.Equals("Default", StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(name);
                }
            }
        }
        catch { /* sem perfis: nada a listar */ }

        return result;
    }

    private static void AddFile(List<HistoryItem> items, HashSet<string> seen, string id,
        string title, string detail, string path, bool requiresRestart = false)
    {
        var full = PathUtil.Normalize(path);
        if (full.Length == 0) return;
        if (!File.Exists(full)) return;
        if (!seen.Add(id + "|" + full)) return;

        items.Add(new HistoryItem
        {
            Kind = "Arquivo",
            Path = full,
            CategoryId = id,
            Detail = $"{title} — {detail}",
            SizeBytes = SafeLength(full),
            Selected = true,
            RequiresRestart = requiresRestart,
        });
    }

    private static void AddFolder(List<HistoryItem> items, HashSet<string> seen, string id,
        string title, string detail, string path,
        bool requiresRestart = false, string filter = "*")
    {
        var full = PathUtil.Normalize(path);
        if (full.Length == 0) return;
        if (!Directory.Exists(full)) return;
        if (!seen.Add(id + "|" + full)) return;

        items.Add(new HistoryItem
        {
            Kind = "Pasta",
            Path = full,
            CategoryId = id,
            Detail = $"{title} — {detail}",
            SizeBytes = SafeFolderSize(full, filter),
            Selected = true,
            RequiresRestart = requiresRestart,
            Filter = filter,
        });
    }

    private static void AddRegistry(List<HistoryItem> items, HashSet<string> seen, string id,
        string title, string detail, string path)
    {
        if (!RegistryKeyExists(path)) return;
        if (!seen.Add(id + "|" + path)) return;

        items.Add(new HistoryItem
        {
            Kind = "Registro",
            Path = path,
            CategoryId = id,
            Detail = $"{title} — {detail}",
            SizeBytes = 0,
            Selected = true,
        });
    }

    private static LeftoverItem ToLeftover(HistoryItem entry) => new()
    {
        Kind = entry.Kind,
        Path = entry.Path,
        Detail = entry.Detail,
        Confidence = LeftoverConfidence.High,
        Reason = LeftoverReason.History,
        Selected = true,
    };

    private static RemovalOutcome Absent(HistoryItem entry) => new()
    {
        Item = ToLeftover(entry),
        Status = RemovalStatus.AlreadyAbsent,
        Message = "já não existia",
    };

    private static RemovalOutcome Fail(HistoryItem entry, string message) => new()
    {
        Item = ToLeftover(entry),
        Status = RemovalStatus.Protected,
        Message = message,
    };

    private static bool RegistryKeyExists(string path)
    {
        try
        {
            var (hive, sub) = SafetyRules.SplitRegistryPath(path);
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(sub);
            return key is not null;
        }
        catch { return false; }
    }

    private static long SafeLength(string path)
    {
        try { return new FileInfo(path).Length; }
        catch { return 0; }
    }

    private static long SafeFolderSize(string path, string filter)
    {
        try
        {
            // A raiz de uma unidade nao e varrida: a enumeracao recursiva
            // Passing por C:\$Recycle.Bin e por tudo mais seria lenta e
            // invariavelmente falharia em pasta sem permissao.
            if (IsDriveRoot(path)) return 0;

            long total = 0;
            foreach (var file in Directory.EnumerateFiles(path, filter, SearchOption.AllDirectories))
                total += new FileInfo(file).Length;
            return total;
        }
        catch { return 0; }
    }
}
