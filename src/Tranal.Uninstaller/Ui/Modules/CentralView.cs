using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Updates;
using Tranal.Uninstaller.Ui.Shared;

namespace Tranal.Uninstaller.Ui.Modules;

/// <summary>
/// A tela da Central: o que precisa de atencao na maquina, em um lugar so.
///
/// E o "Action Center" do IObit, medido no binario dele: um painel que
/// junta o estado de cada area e a acao de cada item. Aqui cada item leva
/// a ferramenta do Tranal que age sobre ele.
/// </summary>
public static class CentralView
{
    /// <summary>Cria a tela da Central.</summary>
    /// <param name="host">A janela principal (e quem sabe trocar de tela).</param>
    public static FrameworkElement Create(MainWindow host)
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<CentralRow>();

        panel.Configure(
            Loc.Get("Module.Central"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Central.Column.Item"), Width = 380, BindingPath = "Title" },
                new ColumnDef { Header = Loc.Get("Central.Column.Kind"), Width = 150, BindingPath = "KindText" },
                new ColumnDef { Header = Loc.Get("Central.Column.Priority"), Width = 110, BindingPath = "PriorityText" },
                new ColumnDef { Header = Loc.Get("Central.Column.Detail"), Width = 340, BindingPath = "Detail" },
            },
            new[]
            {
                new CommandDef
                {
                    AutomationId = "CentralRefresh",
                    Text = Loc.Get("Common.Refresh"),
                    Action = () => panel.RunAsync(() => ReloadAsync(panel, rows)),
                },
                new CommandDef
                {
                    AutomationId = "CentralOpen",
                    Text = Loc.Get("Central.Open"),
                    Action = () => Open(host, panel, rows),
                },
            });

        panel.SetItems(rows);
        panel.RunAsync(() => ReloadAsync(panel, rows));
        return panel;
    }

    /// <summary>
    /// Recarrega a Central.
    ///
    /// A varredura dos programas e a analise de saude sao caras: vao para
    /// uma thread. O feed de atualizacoes e a rede: quando ele falha, a
    /// Central continua mostrando o que ela sabe — e o que ela nao sabe
    /// aparece como ausencia, nunca como "nada a fazer".
    /// </summary>
    private static async Task ReloadAsync(ItemListPanel panel, ObservableCollection<CentralRow> rows)
    {
        var instalados = await Task.Run(() => ProgramScanner.Scan(new ProgramScanner.ScanOptions
        {
            IncludeSystemComponents = false,
            IncludeMsiProducts = true,
        }));

        UpdatePlan? plano = null;
        try
        {
            plano = await UpdateService.AtualizadorDeProgramas().MontarPlanoAsync(instalados);
        }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or TaskCanceledException or IOException)
        {
            // Sem rede (ou feed fora do ar): a Central mostra as outras areas.
        }

        string? versaoTranal = null;
        try
        {
            var check = await UpdateService.VerificarTranalAsync();
            if (check.Success) versaoTranal = check.LatestVersion;
        }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or TaskCanceledException or IOException)
        {
            // Mesma coisa: a versao do Tranal e uma das areas, nao a Central.
        }

        var report = await Task.Run(() => CentralOverview.Build(instalados, plano, versaoTranal));

        rows.Clear();
        foreach (var item in report.Items) rows.Add(new CentralRow { Item = item });

        panel.PanelStatus = rows.Count == 0
            ? Loc.Get("Central.Empty")
            : Loc.Get("Central.Summary", rows.Count);
    }

    /// <summary>
    /// Leva para a ferramenta que age sobre o item selecionado.
    /// </summary>
    private static void Open(MainWindow host, ItemListPanel panel, ObservableCollection<CentralRow> rows)
    {
        var row = Selected(rows, panel);
        if (row is null)
        {
            panel.PanelStatus = Loc.Get("Status.NoSelection");
            return;
        }

        if (string.IsNullOrEmpty(row.Item.ToolId))
        {
            panel.PanelStatus = Loc.Get("Central.NoAction");
            return;
        }

        host.OpenTool(row.Item.ToolId);
    }

    /// <summary>Primeiro item selecionado na lista do painel.</summary>
    private static CentralRow? Selected(ObservableCollection<CentralRow> rows, ItemListPanel panel)
    {
        foreach (var item in panel.SelectedItems)
            if (item is CentralRow typed) return typed;

        return null;
    }
}

/// <summary>
/// Uma linha da Central: o item e os textos que a tela mostra.
/// </summary>
public sealed class CentralRow
{
    /// <summary>O item medido.</summary>
    public CentralItem Item { get; init; } = new();

    /// <summary>O que acontece.</summary>
    public string Title => Item.Title;

    /// <summary>O detalhe.</summary>
    public string Detail => Item.Detail;

    /// <summary>A area do item, em texto.</summary>
    public string KindText => Item.Kind switch
    {
        CentralKind.Update => Loc.Get("Central.Kind.Update"),
        CentralKind.NewProgram => Loc.Get("Central.Kind.NewProgram"),
        CentralKind.Health => Loc.Get("Central.Kind.Health"),
        CentralKind.SelfUpdate => Loc.Get("Central.Kind.SelfUpdate"),
        _ => string.Empty,
    };

    /// <summary>A prioridade do item, em texto.</summary>
    public string PriorityText => Item.Priority switch
    {
        CentralPriority.High => Loc.Get("Central.Priority.High"),
        CentralPriority.Medium => Loc.Get("Central.Priority.Medium"),
        _ => Loc.Get("Central.Priority.Low"),
    };
}
