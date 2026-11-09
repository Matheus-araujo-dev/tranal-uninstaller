using Tranal.Uninstaller.Core;
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
        // A Central: o resumo do que precisa de atencao. E a unica tela que
        // junta areas diferentes — cada item leva a ferramenta que age
        // sobre ele.
        ViewRegistry.Register("Central", () => CentralView.Create(host));

        // Modulos de dados
        ViewRegistry.Register("TracedPrograms", () => ModuleViews.Create("TracedPrograms", host));
        ViewRegistry.Register("WindowsApps", () => ModuleViews.Create("WindowsApps", host));
        ViewRegistry.Register("BrowserExtensions", () => ModuleViews.Create("BrowserExtensions", host));
        ViewRegistry.Register("LogsDatabase", () => ModuleViews.Create("LogsDatabase", host));
        ViewRegistry.Register("HunterMode", () => ModuleViews.Create("HunterMode", host));

        // Ferramentas: a lista vem do catalogo do nucleo (fonte unica), para
        // que nenhum botao da barra lateral fique sem a tela correspondente.
        foreach (var id in ToolCatalog.All)
        {
            var toolId = id;
            ViewRegistry.Register(toolId, () => ToolViews.Create(toolId));
        }

        // A tela de aparencia precisa da lista de programas que a janela
        // principal ja escaneou: o mapa de area e o agrupamento sao refeitos
        // com esses dados. Por isso o registro vem **depois** do laco acima.
        ViewRegistry.Register(ToolCatalog.Appearance, () =>
        {
            var view = new Appearance.AppearanceView();
            view.CarregarProgramas(host.ProgramasInstalados);
            return view;
        });
    }
}
