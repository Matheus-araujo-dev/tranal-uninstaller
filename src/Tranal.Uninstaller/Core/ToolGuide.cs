using System;
using System.Collections.Generic;
using System.Linq;

namespace Tranal.Uninstaller.Core;

/// <summary>
/// As categorias da pagina "Ferramentas".
///
/// A pagina agrupa as ferramentas pelo que a pessoa quer fazer (limpar,
/// acelerar o arranque, cuidar dos programas...), e nao pela origem tecnica
/// de cada uma. A ordem do enum e a ordem da pagina.
/// </summary>
public enum ToolCategory
{
    /// <summary>Liberar espaco e apagar rastros.</summary>
    Cleanup,

    /// <summary>O que sobe com o Windows e o que pesa na maquina.</summary>
    Startup,

    /// <summary>Cuidar dos programas instalados.</summary>
    Programs,

    /// <summary>Registro, atalhos, menus e informacoes do Windows.</summary>
    System,

    /// <summary>Desfazer, recuperar e destravar.</summary>
    Recovery,

    /// <summary>Recursos para quem entende do assunto.</summary>
    Advanced,
}

/// <summary>
/// O guia de cada tela: o que ela faz, o que a pessoa ganha, quando usar e
/// quando evitar.
///
/// POR QUE EXISTE: o Tranal tem dezenas de ferramentas e o nome de cada uma
/// nao diz para que ela serve. O guia aparece no cartao da pagina
/// "Ferramentas" e no topo da tela quando a ferramenta abre — a pessoa le
/// antes de mexer.
///
/// REGRA (uma so interpretacao): a categoria e as chaves do guia moram aqui.
/// A interface consome; o texto mora no catalogo de idiomas
/// (<c>Guide.&lt;id&gt;.What|Gain|When|Avoid</c>). A suite de testes confere que
/// toda ferramenta do <see cref="ToolCatalog"/> tem categoria e as quatro
/// chaves nos dois idiomas.
/// </summary>
public static class ToolGuide
{
    /// <summary>As quatro partes do guia, na ordem em que aparecem.</summary>
    public static IReadOnlyList<string> Parts { get; } = new[] { "What", "Gain", "When", "Avoid" };

    /// <summary>
    /// As telas que nao sao ferramentas mas tambem tem guia: as outras formas
    /// de encontrar e remover programas.
    /// </summary>
    public static IReadOnlyList<string> Modules { get; } = new[]
    {
        "Central", "TracedPrograms", "WindowsApps", "BrowserExtensions", "LogsDatabase", "HunterMode",
    };

    private static readonly IReadOnlyDictionary<string, ToolCategory> Categories =
        new Dictionary<string, ToolCategory>(StringComparer.OrdinalIgnoreCase)
        {
            // Limpeza
            [ToolCatalog.JunkCleaner] = ToolCategory.Cleanup,
            [ToolCatalog.DriveCleaner] = ToolCategory.Cleanup,
            [ToolCatalog.InternetCleaner] = ToolCategory.Cleanup,
            [ToolCatalog.BrowserCleanup] = ToolCategory.Cleanup,
            [ToolCatalog.HistoryCleaner] = ToolCategory.Cleanup,
            [ToolCatalog.TracksBrowsers] = ToolCategory.Cleanup,
            [ToolCatalog.TracksOffice] = ToolCategory.Cleanup,
            [ToolCatalog.TracksWindows] = ToolCategory.Cleanup,
            [ToolCatalog.EvidenceRemover] = ToolCategory.Cleanup,
            [ToolCatalog.PrivacyTraces] = ToolCategory.Cleanup,
            [ToolCatalog.MruCleaner] = ToolCategory.Cleanup,
            [ToolCatalog.CrashDumps] = ToolCategory.Cleanup,
            [ToolCatalog.RecycleBin] = ToolCategory.Cleanup,
            [ToolCatalog.UnrecoverableDelete] = ToolCategory.Cleanup,

            // Inicializacao e desempenho
            [ToolCatalog.AutorunManager] = ToolCategory.Startup,
            [ToolCatalog.BootCenter] = ToolCategory.Startup,
            [ToolCatalog.StoreStartup] = ToolCategory.Startup,
            [ToolCatalog.ProgramImpact] = ToolCategory.Startup,
            [ToolCatalog.Processes] = ToolCategory.Startup,
            [ToolCatalog.Hibernation] = ToolCategory.Startup,
            [ToolCatalog.RunOnce] = ToolCategory.Startup,
            [ToolCatalog.PowerPlan] = ToolCategory.Startup,

            // Programas
            [ToolCatalog.UninstallHistory] = ToolCategory.Programs,
            [ToolCatalog.SoftwareUpdates] = ToolCategory.Programs,
            [ToolCatalog.NewInstalls] = ToolCategory.Programs,
            [ToolCatalog.Bloatware] = ToolCategory.Programs,
            [ToolCatalog.Duplicates] = ToolCategory.Programs,
            [ToolCatalog.FailedUninstalls] = ToolCategory.Programs,
            [ToolCatalog.ProgramCategories] = ToolCategory.Programs,
            [ToolCatalog.Relocation] = ToolCategory.Programs,
            [ToolCatalog.AppxCatalog] = ToolCategory.Programs,
            [ToolCatalog.MsiDependencies] = ToolCategory.Programs,
            [ToolCatalog.InstallerGuard] = ToolCategory.Programs,
            [ToolCatalog.Appearance] = ToolCategory.Programs,

            // Registro e sistema
            [ToolCatalog.RegistryCleaner] = ToolCategory.System,
            [ToolCatalog.RegistryOptimizer] = ToolCategory.System,
            [ToolCatalog.ShortcutFixer] = ToolCategory.System,
            [ToolCatalog.ContextMenu] = ToolCategory.System,
            [ToolCatalog.FileAssociator] = ToolCategory.System,
            [ToolCatalog.SharedDlls] = ToolCategory.System,
            [ToolCatalog.SystemDlls] = ToolCategory.System,
            [ToolCatalog.SystemManagement] = ToolCategory.System,
            [ToolCatalog.WindowsTools] = ToolCategory.System,
            [ToolCatalog.WindowsIntegration] = ToolCategory.System,
            [ToolCatalog.Printers] = ToolCategory.System,
            [ToolCatalog.SystemInfo] = ToolCategory.System,
            [ToolCatalog.CrashAnalyzer] = ToolCategory.System,

            // Seguranca e recuperacao
            [ToolCatalog.UndoCenter] = ToolCategory.Recovery,
            [ToolCatalog.DailyRegistryBackup] = ToolCategory.Recovery,
            [ToolCatalog.PendingOperations] = ToolCategory.Recovery,
            [ToolCatalog.Unlocker] = ToolCategory.Recovery,
            [ToolCatalog.FileOwnership] = ToolCategory.Recovery,

            // Avancado
            [ToolCatalog.DeepAnalysis] = ToolCategory.Advanced,
            [ToolCatalog.Finder] = ToolCategory.Advanced,
            [ToolCatalog.HexViewer] = ToolCategory.Advanced,
            [ToolCatalog.AlternateDataStreams] = ToolCategory.Advanced,
            [ToolCatalog.ActiveFiles] = ToolCategory.Advanced,
            [ToolCatalog.CustomTools] = ToolCategory.Advanced,
            [ToolCatalog.OperationProfiles] = ToolCategory.Advanced,
            [ToolCatalog.Notifications] = ToolCategory.Advanced,
            [ToolCatalog.TrayIcon] = ToolCategory.Advanced,
            [ToolCatalog.AppUpdate] = ToolCategory.Advanced,
        };

    /// <summary>
    /// A categoria da ferramenta. Nulo quando o id nao e uma ferramenta do
    /// catalogo (um modulo, por exemplo).
    /// </summary>
    /// <param name="toolId">O id da ferramenta (<see cref="ToolCatalog"/>).</param>
    public static ToolCategory? CategoryOf(string toolId) =>
        toolId is not null && Categories.TryGetValue(toolId, out var categoria) ? categoria : null;

    /// <summary>As ferramentas de uma categoria, na ordem do catalogo.</summary>
    /// <param name="category">A categoria.</param>
    public static IReadOnlyList<string> ToolsOf(ToolCategory category) =>
        ToolCatalog.All.Where(id => CategoryOf(id) == category).ToList();

    /// <summary>
    /// True quando a tela tem guia: toda ferramenta do catalogo e os modulos
    /// de <see cref="Modules"/>.
    /// </summary>
    /// <param name="id">O id da tela.</param>
    public static bool HasGuide(string id) =>
        !string.IsNullOrWhiteSpace(id)
        && (CategoryOf(id) is not null || Modules.Contains(id, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// A chave do catalogo de idiomas de uma parte do guia
    /// (<c>Guide.JunkCleaner.What</c>).
    /// </summary>
    /// <param name="id">O id da tela.</param>
    /// <param name="part">Uma das <see cref="Parts"/>.</param>
    public static string KeyOf(string id, string part) => $"Guide.{id}.{part}";

    /// <summary>A chave do nome da categoria (<c>ToolCategory.Cleanup</c>).</summary>
    /// <param name="category">A categoria.</param>
    public static string LabelKeyOf(ToolCategory category) => $"ToolCategory.{category}";
}
