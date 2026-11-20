using Tranal.Uninstaller.Core;

namespace Tranal.Uninstaller.Ui;

/// <summary>
/// O icone (Segoe Fluent Icons / Segoe MDL2 Assets) de cada tela.
///
/// POR QUE EXISTE: a barra lateral antiga usava emojis coloridos, um por
/// ferramenta — pesados de desenhar (fonte de cor) e fora do estilo
/// monocromatico do Tranal Sentinel. O icone e so desenho: o nome e o guia de
/// cada ferramenta continuam no catalogo de idiomas.
/// </summary>
public static class ToolGlyphs
{
    /// <summary>Icone padrao, quando a tela nao tem um proprio.</summary>
    public const string Default = ""; // Repair

    private static readonly IReadOnlyDictionary<string, string> Map =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AllPrograms"] = "",          // AllApps
            ["WindowsApps"] = "",          // TVMonitor
            ["BrowserExtensions"] = "",    // Puzzle
            ["TracedPrograms"] = "",       // Diagnostic
            ["LogsDatabase"] = "",         // Library
            ["HunterMode"] = "",           // Target-ish
            ["Central"] = "",              // Home
            ["ToolsHome"] = "",            // Repair

            [ToolCatalog.JunkCleaner] = "",          // Delete
            [ToolCatalog.DriveCleaner] = "",         // HardDrive
            [ToolCatalog.InternetCleaner] = "",      // Globe
            [ToolCatalog.BrowserCleanup] = "",       // Refresh
            [ToolCatalog.HistoryCleaner] = "",       // History
            [ToolCatalog.TracksBrowsers] = "",
            [ToolCatalog.TracksOffice] = "",         // Document
            [ToolCatalog.TracksWindows] = "",        // System
            [ToolCatalog.EvidenceRemover] = "",      // View
            [ToolCatalog.PrivacyTraces] = "",        // Lock
            [ToolCatalog.MruCleaner] = "",           // Recent
            [ToolCatalog.CrashDumps] = "",           // Warning
            [ToolCatalog.RecycleBin] = "",
            [ToolCatalog.UnrecoverableDelete] = "",  // Shield

            [ToolCatalog.AutorunManager] = "",       // PowerButton
            [ToolCatalog.BootCenter] = "",           // Stopwatch
            [ToolCatalog.StoreStartup] = "",         // Shop
            [ToolCatalog.ProgramImpact] = "",
            [ToolCatalog.Processes] = "",            // Processing
            [ToolCatalog.Hibernation] = "",          // QuietHours (lua)
            [ToolCatalog.RunOnce] = "",
            [ToolCatalog.PowerPlan] = "",

            [ToolCatalog.UninstallHistory] = "",
            [ToolCatalog.SoftwareUpdates] = "",      // Sync
            [ToolCatalog.NewInstalls] = "",          // Add
            [ToolCatalog.Bloatware] = "",            // DeviceLaptop
            [ToolCatalog.Duplicates] = "",           // Copy
            [ToolCatalog.FailedUninstalls] = "",
            [ToolCatalog.ProgramCategories] = "",    // Tag
            [ToolCatalog.Relocation] = "",           // Move
            [ToolCatalog.AppxCatalog] = "",          // Package
            [ToolCatalog.MsiDependencies] = "",      // Link
            [ToolCatalog.InstallerGuard] = "",
            [ToolCatalog.Appearance] = "",           // Color

            [ToolCatalog.RegistryCleaner] = "",      // RegistryCleaner-ish
            [ToolCatalog.RegistryOptimizer] = "",    // LightningBolt
            [ToolCatalog.ShortcutFixer] = "",
            [ToolCatalog.ContextMenu] = "",          // BulletedList
            [ToolCatalog.FileAssociator] = "",       // OpenFile
            [ToolCatalog.SharedDlls] = "",           // Component
            [ToolCatalog.SystemDlls] = "",
            [ToolCatalog.SystemManagement] = "",     // Manage
            [ToolCatalog.WindowsTools] = "",
            [ToolCatalog.WindowsIntegration] = "",
            [ToolCatalog.Printers] = "",             // Print
            [ToolCatalog.SystemInfo] = "",           // Info
            [ToolCatalog.CrashAnalyzer] = "",

            [ToolCatalog.UndoCenter] = "",           // Undo
            [ToolCatalog.DailyRegistryBackup] = "",  // Save
            [ToolCatalog.PendingOperations] = "",
            [ToolCatalog.Unlocker] = "",             // Unlock
            [ToolCatalog.FileOwnership] = "",        // Permissions

            [ToolCatalog.DeepAnalysis] = "",         // Search
            [ToolCatalog.Finder] = "",
            [ToolCatalog.HexViewer] = "",            // Code
            [ToolCatalog.AlternateDataStreams] = "", // Folder
            [ToolCatalog.ActiveFiles] = "",
            [ToolCatalog.CustomTools] = "",
            [ToolCatalog.OperationProfiles] = "",    // Setting
            [ToolCatalog.Notifications] = "",        // Ringer
            [ToolCatalog.TrayIcon] = "",             // TaskView
            [ToolCatalog.AppUpdate] = "",            // Download
        };

    /// <summary>O icone da tela. Nunca vazio.</summary>
    /// <param name="id">O id da tela.</param>
    public static string Of(string? id) =>
        id is not null && Map.TryGetValue(id, out var glyph) ? glyph : Default;
}
