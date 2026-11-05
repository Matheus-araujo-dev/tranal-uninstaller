using Tranal.Uninstaller.Ui.Modules;
using Tranal.Uninstaller.Ui.Tools;

namespace Tranal.Uninstaller.Ui;

/// <summary>
/// Registra as telas de modulos e ferramentas no <see cref="ViewRegistry"/>.
/// A MainWindow e a unica que conhece o registro: os modulos e as ferramentas
/// ficam livres de dependencia da janela.
/// </summary>
public static class ViewSetup
{
    public static void RegisterAll(MainWindow host)
    {
        // Modulos de dados
        ViewRegistry.Register("TracedPrograms", () => ModuleViews.Create("TracedPrograms", host));
        ViewRegistry.Register("WindowsApps", () => ModuleViews.Create("WindowsApps", host));
        ViewRegistry.Register("BrowserExtensions", () => ModuleViews.Create("BrowserExtensions", host));
        ViewRegistry.Register("LogsDatabase", () => ModuleViews.Create("LogsDatabase", host));
        ViewRegistry.Register("HunterMode", () => ModuleViews.Create("HunterMode", host));

        // Ferramentas
        ViewRegistry.Register("AutorunManager", () => ToolViews.Create("AutorunManager"));
        ViewRegistry.Register("JunkCleaner", () => ToolViews.Create("JunkCleaner"));
        ViewRegistry.Register("WindowsTools", () => ToolViews.Create("WindowsTools"));
        ViewRegistry.Register("HistoryCleaner", () => ToolViews.Create("HistoryCleaner"));
        ViewRegistry.Register("EvidenceRemover", () => ToolViews.Create("EvidenceRemover"));
        ViewRegistry.Register("UnrecoverableDelete", () => ToolViews.Create("UnrecoverableDelete"));
    }
}
