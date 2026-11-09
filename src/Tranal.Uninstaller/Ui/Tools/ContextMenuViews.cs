using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Management;
using Tranal.Uninstaller.Ui.Shared;

namespace Tranal.Uninstaller.Ui.Tools;

/// <summary>
/// A tela do gerenciador de menu de contexto.
///
/// As entradas do Explorer em um lugar so: as do Tranal (a de desinstalar, a
/// de rastrear a instalacao, a de shredar e a do monitor) e as que os outros
/// programas deixaram. A regra mora no nucleo
/// (<c>ContextMenuOverview</c>): a tela so mostra e delega.
/// </summary>
public static class ContextMenuViews
{
    /// <summary>Cria a tela do gerenciador de menu de contexto.</summary>
    public static FrameworkElement Create()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<ContextMenuRow>();

        panel.Configure(
            Loc.Get("Module.ContextMenu"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("ContextMenu.Column.Item"), Width = 300, BindingPath = "Title" },
                new ColumnDef { Header = Loc.Get("ContextMenu.Column.Owner"), Width = 120, BindingPath = "Owner" },
                new ColumnDef { Header = Loc.Get("ContextMenu.Column.Location"), Width = 330, BindingPath = "Location" },
                new ColumnDef { Header = Loc.Get("ContextMenu.Column.State"), Width = 110, BindingPath = "State" },
                new ColumnDef { Header = Loc.Get("ContextMenu.Column.Program"), Width = 220, BindingPath = "Program" },
            },
            new[]
            {
                new CommandDef
                {
                    AutomationId = "ContextMenuRefresh",
                    Text = Loc.Get("Common.Refresh"),
                    Action = () => panel.RunAsync(() => ReloadAsync(panel, rows)),
                },
                new CommandDef
                {
                    AutomationId = "ContextMenuRestore",
                    Text = Loc.Get("ContextMenu.Restore"),
                    Action = () => panel.RunAsync(() => RestoreAsync(panel, rows)),
                },
                new CommandDef
                {
                    AutomationId = "ContextMenuDisable",
                    Text = Loc.Get("ContextMenu.Disable"),
                    Action = () => panel.RunAsync(() => DisableAsync(panel, rows)),
                },
                new CommandDef
                {
                    AutomationId = "ContextMenuRemove",
                    Text = Loc.Get("Common.Remove"),
                    Action = () => panel.RunAsync(() => RemoveAsync(panel, rows)),
                },
                new CommandDef
                {
                    AutomationId = "ContextMenuCreate",
                    Text = Loc.Get("ContextMenu.Create"),
                    Action = () => Criar(panel, rows),
                },
                new CommandDef
                {
                    AutomationId = "ContextMenuRemoveAll",
                    Text = Loc.Get("ContextMenu.RemoveAll"),
                    Action = () => panel.RunAsync(() => RemoveAllAsync(panel, rows)),
                },
            });

        panel.SetItems(rows);
        panel.RunAsync(() => ReloadAsync(panel, rows));
        return panel;
    }

    /// <summary>Recarrega a lista de entradas.</summary>
    /// <param name="panel">O painel.</param>
    /// <param name="rows">As linhas.</param>
    private static async Task ReloadAsync(ItemListPanel panel, ObservableCollection<ContextMenuRow> rows)
    {
        // A leitura do registro de terceiros e lenta: vai para uma thread.
        var items = await Task.Run(ContextMenuOverview.List);

        rows.Clear();
        foreach (var item in items) rows.Add(new ContextMenuRow { Item = item });

        panel.PanelStatus = Loc.Get("ContextMenu.Summary", rows.Count);
    }

    /// <summary>Registra as entradas do Tranal no Explorer.</summary>
    /// <param name="panel">O painel.</param>
    /// <param name="rows">As linhas.</param>
    private static async Task RestoreAsync(ItemListPanel panel, ObservableCollection<ContextMenuRow> rows)
    {
        var result = await Task.Run(ContextMenuOverview.Restore);
        panel.PanelStatus = result.Success ? result.Message : Loc.Get("ContextMenu.Failed", result.Message);

        await ReloadAsync(panel, rows);
    }

    /// <summary>Remove as entradas do Tranal do Explorer.</summary>
    /// <param name="panel">O painel.</param>
    /// <param name="rows">As linhas.</param>
    private static async Task DisableAsync(ItemListPanel panel, ObservableCollection<ContextMenuRow> rows)
    {
        var result = await Task.Run(ContextMenuOverview.Disable);
        panel.PanelStatus = result.Success ? result.Message : Loc.Get("ContextMenu.Failed", result.Message);

        await ReloadAsync(panel, rows);
    }

    /// <summary>Remove a entrada selecionada.</summary>
    /// <param name="panel">O painel.</param>
    /// <param name="rows">As linhas.</param>
    private static async Task RemoveAsync(ItemListPanel panel, ObservableCollection<ContextMenuRow> rows)
    {
        var row = Selected(rows, panel);
        if (row is null)
        {
            panel.PanelStatus = Loc.Get("Status.NoSelection");
            return;
        }

        if (!row.Item.CanRemove)
        {
            panel.PanelStatus = Loc.Get("ContextMenu.NotRemovable", row.Item.Title);
            return;
        }

        var pergunta = Loc.Get("ContextMenu.ConfirmRemove", row.Item.Title);
        if (!UserMessages.Confirm(null, pergunta, Loc.Get("Module.ContextMenu"))) return;

        var result = await Task.Run(() => ContextMenuOverview.Remove(row.Item));
        panel.PanelStatus = result.Success ? result.Message : Loc.Get("ContextMenu.Failed", result.Message);

        await ReloadAsync(panel, rows);
    }

    /// <summary>Cria uma entrada de menu de contexto.</summary>
    /// <param name="panel">O painel.</param>
    /// <param name="rows">As linhas.</param>
    private static void Criar(ItemListPanel panel, ObservableCollection<ContextMenuRow> rows)
    {
        var dialog = new InputDialog(
            Loc.Get("ContextMenu.Create"),
            (Loc.Get("ContextMenu.CreateName"), string.Empty),
            (Loc.Get("ContextMenu.CreateCommand"), string.Empty));

        if (dialog.ShowDialog() != true) return;

        var valores = dialog.Values;
        if (valores.Count < 2) return;

        var nome = valores[0].Trim();
        var comando = valores[1].Trim();

        if (nome.Length == 0 || comando.Length == 0)
        {
            panel.PanelStatus = Loc.Get("ContextMenu.CreateMissing");
            return;
        }

        var result = ContextMenuOverview.Create(nome, comando);
        panel.PanelStatus = result.Success ? result.Message : Loc.Get("ContextMenu.Failed", result.Message);

        if (result.Success) panel.RunAsync(() => ReloadAsync(panel, rows));
    }

    /// <summary>Apaga todas as entradas que o gerenciador pode apagar.</summary>
    /// <param name="panel">O painel.</param>
    /// <param name="rows">As linhas.</param>
    private static async Task RemoveAllAsync(ItemListPanel panel, ObservableCollection<ContextMenuRow> rows)
    {
        var pergunta = Loc.Get("ContextMenu.RemoveAllConfirm");
        if (!UserMessages.Confirm(null, pergunta, Loc.Get("Module.ContextMenu"))) return;

        var result = await Task.Run(ContextMenuOverview.RemoveAll);
        panel.PanelStatus = result.Success ? result.Message : Loc.Get("ContextMenu.Failed", result.Message);

        await ReloadAsync(panel, rows);
    }

    /// <summary>A linha selecionada na lista do painel.</summary>
    /// <param name="rows">As linhas.</param>
    /// <param name="panel">O painel.</param>
    private static ContextMenuRow? Selected(ObservableCollection<ContextMenuRow> rows, ItemListPanel panel)
    {
        foreach (var item in panel.SelectedItems)
            if (item is ContextMenuRow typed) return typed;

        return null;
    }
}

/// <summary>
/// Uma linha do gerenciador: a entrada medida e os textos que a tela mostra.
/// </summary>
public sealed class ContextMenuRow
{
    /// <summary>A entrada medida.</summary>
    public ContextMenuItem Item { get; init; } = new();

    /// <summary>O nome que aparece no menu.</summary>
    public string Title => Item.Title;

    /// <summary>Onde a entrada aparece.</summary>
    public string Location => Item.Location;

    /// <summary>O programa que deixou a entrada.</summary>
    public string Program => Item.Program;

    /// <summary>De quem e a entrada, em texto.</summary>
    public string Owner => Item.Kind switch
    {
        ContextMenuKind.Tranal => Loc.Get("ContextMenu.Owner.Tranal"),
        ContextMenuKind.Windows => Loc.Get("ContextMenu.Owner.Windows"),
        ContextMenuKind.Pessoal => Loc.Get("ContextMenu.Owner.Custom"),
        _ => Loc.Get("ContextMenu.Owner.Other"),
    };

    /// <summary>O estado da entrada, em texto.</summary>
    public string State => Item.Kind switch
    {
        ContextMenuKind.Tranal => Item.IsEnabled
            ? Loc.Get("Common.Enable")
            : Loc.Get("Common.Disable"),

        ContextMenuKind.Programa => Loc.Get("ContextMenu.State.Removable"),

        _ => Loc.Get("ContextMenu.State.Windows"),
    };
}
