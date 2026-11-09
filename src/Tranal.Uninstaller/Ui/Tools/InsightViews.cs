using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Appearance;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Management;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Settings;
using Tranal.Uninstaller.Core.Tools;
using Tranal.Uninstaller.Ui.Shared;

namespace Tranal.Uninstaller.Ui.Tools;

/// <summary>
/// Telas das ferramentas de "diagnostico do sistema": informacoes do sistema,
/// impacto dos programas, relatorio de falhas e relocacao de programas.
///
/// Todas usam o painel generico (<see cref="ItemListPanel"/>): o que muda
/// entre elas sao as colunas, os comandos e a fonte dos dados.
///
/// Estas telas sao o CONSUMIDOR dos modulos do nucleo
/// (<see cref="SystemInfoPanel"/>, <see cref="ProgramImpactAnalyzer"/>,
/// <see cref="CrashAnalyzer"/> e <see cref="RelocationManager"/>). Sem elas,
/// o nucleo teria a capacidade mas a tela nao mostraria — e capacidade sem
/// consumidor nao e capacidade entregue.
/// </summary>
public static class InsightViews
{
    /// <summary>Cria a tela da ferramenta pelo id usado na barra lateral.</summary>
    public static FrameworkElement Create(string toolId) => toolId switch
    {
        ToolCatalog.SystemInfo => CreateSystemInfo(),
        ToolCatalog.ProgramImpact => CreateProgramImpact(),
        ToolCatalog.CrashAnalyzer => CreateCrashAnalyzer(),
        ToolCatalog.Relocation => CreateRelocation(),
        _ => new ItemListPanel(),
    };

    // ------------------------------------------------------------------
    // Informacoes do sistema
    // ------------------------------------------------------------------

    /// <summary>
    /// O painel de informacoes do sistema: sistema operacional, processador,
    /// memoria, disco, video e tempo de atividade.
    ///
    /// E o que o HiBit mostra no banner e o que o usuario olha antes de
    /// decidir desinstalar: "a maquina esta com a memoria acabando?".
    /// </summary>
    private static FrameworkElement CreateSystemInfo()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<InsightRow>();

        panel.Configure(
            Loc.Get("Module.SystemInfo"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Common.Name"), Width = 240, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Common.Status"), Width = 520, BindingPath = "Kind" },
                new ColumnDef { Header = Loc.Get("Common.Path"), Width = 320, BindingPath = "Path" },
            },
            new[]
            {
                new CommandDef
                {
                    AutomationId = "SystemInfoRefresh",
                    Text = Loc.Get("Common.Refresh"),
                    Action = () => panel.RunAsync(() => LoadAsync(panel, rows)),
                },
            });

        panel.SetItems(rows);
        panel.RunAsync(() => LoadAsync(panel, rows));
        return panel;

        static async Task LoadAsync(ItemListPanel panel, ObservableCollection<InsightRow> rows)
        {
            var itens = await Task.Run(() => SystemInfoPanel.Collect());

            rows.Clear();
            foreach (var item in itens)
            {
                rows.Add(new InsightRow
                {
                    Category = item.Label,
                    Kind = item.Value,
                    Path = item.RawValue,
                });
            }

            panel.PanelStatus = string.Format(
                CultureInfo.CurrentCulture, Loc.Get("Insight.SystemInfo.Count"), itens.Count);
        }
    }

    // ------------------------------------------------------------------
    // Impacto dos programas
    // ------------------------------------------------------------------

    /// <summary>
    /// O ranking de impacto: quais programas pesam mais na maquina (CPU e
    /// memoria). E a coluna "Impact" do Ashampoo.
    /// </summary>
    private static FrameworkElement CreateProgramImpact()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<InsightRow>();

        panel.Configure(
            Loc.Get("Module.ProgramImpact"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Common.Name"), Width = 260, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Insight.Impact.Score"), Width = 90, BindingPath = "Kind" },
                new ColumnDef { Header = Loc.Get("Insight.Impact.Memory"), Width = 140, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Insight.Impact.Cpu"), Width = 140, BindingPath = "Size" },
            },
            new[]
            {
                new CommandDef
                {
                    AutomationId = "ProgramImpactRefresh",
                    Text = Loc.Get("Common.Refresh"),
                    Action = () => panel.RunAsync(() => LoadAsync(panel, rows)),
                },
            });

        panel.SetItems(rows);
        panel.RunAsync(() => LoadAsync(panel, rows));
        return panel;

        static async Task LoadAsync(ItemListPanel panel, ObservableCollection<InsightRow> rows)
        {
            var instalados = ProgramScanner.Scan(new ProgramScanner.ScanOptions
            {
                IncludeSystemComponents = false,
                IncludeMsiProducts = false,
            });

            var ranking = await Task.Run(() => ProgramImpactAnalyzer.Rank(instalados));

            rows.Clear();
            foreach (var impact in ranking)
            {
                rows.Add(new InsightRow
                {
                    Category = impact.Program.DisplayName,
                    Kind = impact.Score.ToString(CultureInfo.CurrentCulture),
                    Path = impact.MemoryText,
                    Size = impact.CpuText,
                    Source = impact.Program,
                });
            }

            panel.PanelStatus = string.Format(
                CultureInfo.CurrentCulture, Loc.Get("Insight.Impact.Count"), ranking.Count);
        }
    }

    // ------------------------------------------------------------------
    // Relatorio de falhas
    // ------------------------------------------------------------------

    /// <summary>
    /// As falhas do Windows agrupadas por programa. E o "Crash Analyzer" do
    /// Ashampoo: os eventos 1000/1001/1002 do Event Log, agrupados.
    /// </summary>
    private static FrameworkElement CreateCrashAnalyzer()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<InsightRow>();
        var falhas = new List<CrashEntry>();

        panel.Configure(
            Loc.Get("Module.CrashAnalyzer"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Common.Name"), Width = 260, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Insight.Crash.Count"), Width = 90, BindingPath = "Kind" },
                new ColumnDef { Header = Loc.Get("Common.Date"), Width = 150, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Insight.Crash.Event"), Width = 300, BindingPath = "Size" },
            },
            new[]
            {
                new CommandDef
                {
                    AutomationId = "CrashAnalyzerRefresh",
                    Text = Loc.Get("Common.Refresh"),
                    Action = () => panel.RunAsync(() => LoadAsync(panel, rows, falhas)),
                },
                new CommandDef
                {
                    AutomationId = "CrashAnalyzerExport",
                    Text = Loc.Get("Insight.Crash.Export"),
                    Action = () => Exportar(panel, falhas),
                },
                new CommandDef
                {
                    AutomationId = "CrashAnalyzerSearch",
                    Text = Loc.Get("Insight.Crash.Search"),
                    Action = () => Pesquisar(panel),
                },
            });

        panel.SetItems(rows);
        panel.RunAsync(() => LoadAsync(panel, rows, falhas));
        return panel;

        static async Task LoadAsync(ItemListPanel panel, ObservableCollection<InsightRow> rows, List<CrashEntry> falhas)
        {
            var entradas = await Task.Run(() => CrashAnalyzer.Scan());

            falhas.Clear();
            falhas.AddRange(entradas);

            rows.Clear();
            foreach (var falha in entradas)
            {
                rows.Add(new InsightRow
                {
                    Category = falha.ProgramName,
                    Kind = falha.Count.ToString(CultureInfo.CurrentCulture),
                    Path = RelativeDate.Format(falha.LastSeen),
                    Size = $"{falha.EventName} ({falha.EventId})",
                    Source = falha,
                });
            }

            panel.PanelStatus = string.Format(
                CultureInfo.CurrentCulture, Loc.Get("Insight.Crash.Count2"), entradas.Count);
        }

        /// <summary>
        /// Exporta as falhas agrupadas para um relatorio HTML.
        /// </summary>
        static void Exportar(ItemListPanel panel, List<CrashEntry> falhas)
        {
            if (falhas.Count == 0)
            {
                panel.PanelStatus = Loc.Get("Common.Empty");
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = Loc.Get("Insight.Crash.Export"),
                Filter = "HTML (*.html)|*.html",
                FileName = "crash-analyzer.html",
            };

            if (dialog.ShowDialog() != true) return;

            var ok = CrashAnalyzer.WriteReport(dialog.FileName, falhas, RelatorioLabels());
            panel.PanelStatus = ok
                ? Loc.Get("Insight.Crash.Exported") + ": " + dialog.FileName
                : Loc.Get("Status.Error");
        }

        /// <summary>
        /// Abre a busca online das falhas do programa selecionado.
        /// </summary>
        static void Pesquisar(ItemListPanel panel)
        {
            CrashEntry? alvo = null;
            foreach (var item in panel.SelectedItems)
            {
                if (item is InsightRow row && row.Source is CrashEntry crash)
                {
                    alvo = crash;
                    break;
                }
            }

            if (alvo is null)
            {
                panel.PanelStatus = Loc.Get("Status.NoSelection");
                return;
            }

            var url = CrashAnalyzer.BuildSearchUrl(alvo, AppSettingsStore.Current);
            if (url.Length == 0)
            {
                panel.PanelStatus = Loc.Get("Status.Error");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                panel.PanelStatus = Loc.Get("Insight.Crash.SearchTerm") + ": " + CrashAnalyzer.BuildSearchTerm(alvo);
            }
            catch (Exception ex)
            {
                panel.PanelStatus = Loc.Get("Status.Error") + ": " + ex.Message;
            }
        }

        /// <summary>
        /// Os textos do relatorio, no idioma da interface.
        /// </summary>
        static CrashReportLabels RelatorioLabels() => new()
        {
            Title = Loc.Get("Module.CrashAnalyzer"),
            Program = Loc.Get("Common.Program"),
            Count = Loc.Get("Insight.Crash.Column.Count"),
            First = Loc.Get("Insight.Crash.Column.First"),
            Last = Loc.Get("Insight.Crash.Column.Last"),
            Event = Loc.Get("Insight.Crash.Column.Event"),
            Executable = Loc.Get("Insight.Crash.Column.Executable"),
            SubtitleFormat = Loc.Get("Insight.Crash.Count2"),
        };
    }

    // ------------------------------------------------------------------
    // Relocacao de programas
    // ------------------------------------------------------------------

    /// <summary>
    /// A relocacao: mover um programa instalado de um disco para outro, e
    /// devolver ao lugar original o que foi movido.
    ///
    /// O historico e o que da a volta: cada movimento fica gravado, e a tela
    /// mostra a lista com a acao de desfazer.
    /// </summary>
    private static FrameworkElement CreateRelocation()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<InsightRow>();

        panel.Configure(
            Loc.Get("Module.Relocation"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Common.Name"), Width = 240, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Insight.Relocation.From"), Width = 300, BindingPath = "Kind" },
                new ColumnDef { Header = Loc.Get("Insight.Relocation.To"), Width = 300, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Common.Date"), Width = 140, BindingPath = "Size" },
            },
            new[]
            {
                new CommandDef
                {
                    AutomationId = "RelocationMove",
                    Text = Loc.Get("Insight.Relocation.Move"),
                    Action = () => panel.RunAsync(() => MoveAsync(panel, rows)),
                },
                new CommandDef
                {
                    AutomationId = "RelocationRevert",
                    Text = Loc.Get("Insight.Relocation.Revert"),
                    Action = () => panel.RunAsync(() => RevertAsync(panel, rows)),
                },
                new CommandDef
                {
                    AutomationId = "RelocationRefresh",
                    Text = Loc.Get("Common.Refresh"),
                    Action = () => panel.RunAsync(() => LoadAsync(panel, rows)),
                },
            });

        panel.SetItems(rows);
        panel.RunAsync(() => LoadAsync(panel, rows));
        return panel;

        static async Task LoadAsync(ItemListPanel panel, ObservableCollection<InsightRow> rows)
        {
            var historico = await Task.Run(() => RelocationManager.Historico());

            rows.Clear();
            foreach (var registro in historico)
            {
                rows.Add(new InsightRow
                {
                    Category = registro.ProgramName,
                    Kind = registro.FromPath,
                    Path = registro.ToPath,
                    Size = RelativeDate.Format(registro.When),
                    Source = registro,
                });
            }

            panel.PanelStatus = string.Format(
                CultureInfo.CurrentCulture, Loc.Get("Insight.Relocation.Count"), historico.Count);
        }

        static async Task MoveAsync(ItemListPanel panel, ObservableCollection<InsightRow> rows)
        {
            // A escolha do programa e do destino sao duas dialogos: o
            // programa vem da lista de instalados, e o destino e uma pasta
            // que o usuario escolhe. Sem os dois, nao ha o que mover.
            var dialog = new OpenFileDialog
            {
                Title = Loc.Get("Insight.Relocation.Move"),
                Filter = Loc.Get("Forced.BrowseFilter"),
                CheckFileExists = false,
            };

            var instalados = ProgramScanner.Scan(new ProgramScanner.ScanOptions
            {
                IncludeSystemComponents = false,
                IncludeMsiProducts = false,
            });

            var comPasta = instalados
                .Where(p => !string.IsNullOrWhiteSpace(p.InstallLocation))
                .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            if (comPasta.Count == 0)
            {
                panel.PanelStatus = Loc.Get("Insight.Relocation.NoProgram");
                return;
            }

            var escolhido = EscolherPrograma(comPasta);
            if (escolhido is null) return;

            var destino = EscolherPasta();
            if (destino is null) return;

            var resultado = await Task.Run(() => RelocationManager.Move(escolhido, destino));
            panel.PanelStatus = resultado.Message;

            if (resultado.Success) await LoadAsync(panel, rows);
        }

        static async Task RevertAsync(ItemListPanel panel, ObservableCollection<InsightRow> rows)
        {
            var registro = rows.FirstOrDefault()?.Source as RelocationRecord;
            if (registro is null)
            {
                panel.PanelStatus = Loc.Get("Insight.Relocation.Nothing");
                return;
            }

            var resultado = await Task.Run(() => RelocationManager.Revert(registro.ProgramName));
            panel.PanelStatus = resultado.Message;

            if (resultado.Success) await LoadAsync(panel, rows);
        }
    }

    /// <summary>
    /// Escolhe o programa a mover. A lista e montada em WPF puro porque o
    /// painel generico nao serve aqui: a escolha e unica e o alvo e um
    /// programa instalado, nao um item de lista.
    /// </summary>
    private static InstalledProgram? EscolherPrograma(IReadOnlyList<InstalledProgram> instalados)
    {
        var janela = new Window
        {
            Title = Loc.Get("Insight.Relocation.Move"),
            Width = 520,
            Height = 460,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Application.Current?.MainWindow,
        };

        var lista = new ListBox { Margin = new Thickness(10) };
        foreach (var p in instalados) lista.Items.Add(p);

        var ok = new Button
        {
            Content = Loc.Get("Common.Ok"),
            Width = 90,
            Margin = new Thickness(6),
            IsDefault = true,
        };
        var cancelar = new Button
        {
            Content = Loc.Get("Common.Cancel"),
            Width = 90,
            Margin = new Thickness(6),
            IsCancel = true,
        };

        ok.Click += (_, _) => janela.DialogResult = true;
        cancelar.Click += (_, _) => janela.DialogResult = false;

        var painel = new DockPanel();
        painel.Children.Add(lista);
        painel.SetValue(DockPanel.DockProperty, Dock.Bottom);

        var botoes = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(10),
        };
        botoes.Children.Add(ok);
        botoes.Children.Add(cancelar);
        painel.Children.Add(botoes);

        janela.Content = painel;

        return janela.ShowDialog() == true ? lista.SelectedItem as InstalledProgram : null;
    }

    /// <summary>
    /// Escolhe a pasta de destino. O dialogo padrao de pasta do Windows e
    /// reusado: a lista de pastas que ele mostra e o proprio Windows.
    /// </summary>
    private static string? EscolherPasta()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = Loc.Get("Insight.Relocation.Destination"),
            Multiselect = false,
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    /// <summary>
    /// Uma linha das telas de diagnostico: as quatro colunas cabem em qualquer
    /// painel de lista, e o item original vai em <c>Source</c>.
    /// </summary>
    private sealed class InsightRow
    {
        /// <summary>A primeira coluna (rotulo do item).</summary>
        public string Category { get; init; } = string.Empty;

        /// <summary>A segunda coluna (valor principal).</summary>
        public string Kind { get; init; } = string.Empty;

        /// <summary>A terceira coluna (detalhe).</summary>
        public string Path { get; init; } = string.Empty;

        /// <summary>A quarta coluna (detalhe extra).</summary>
        public string Size { get; init; } = string.Empty;

        /// <summary>O item original do nucleo.</summary>
        public object Source { get; init; } = new();
    }
}
