using Tranal.Uninstaller.Core.Automation;
using System.Globalization;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Recovery;
using Tranal.Uninstaller.Core.Tools;
using Tranal.Uninstaller.Ui.Shared;

namespace Tranal.Uninstaller.Ui.Tools;

/// <summary>
    /// Telas das ferramentas do painel "Tools". Todas usam o mesmo painel generico
    /// (<see cref="ItemListPanel"/>): o que muda entre elas sao as colunas, os
    /// comandos e a fonte dos dados.
    /// </summary>
public static class ToolViews
{
    /// <summary>Cria a tela da ferramenta pelo id usado na barra lateral.</summary>
    public static FrameworkElement Create(string toolId) => toolId switch
    {
        ToolCatalog.AutorunManager => CreateAutorun(),
        ToolCatalog.BootCenter => CreateBootCenter(),
        ToolCatalog.ActiveFiles => CreateActiveFiles(),
        ToolCatalog.FileAssociator => CreateFileAssociator(),
        ToolCatalog.UndoCenter => CreateUndoCenter(),
        ToolCatalog.JunkCleaner => CreateJunk(),
        ToolCatalog.WindowsTools => CreateWindowsTools(),
        ToolCatalog.HistoryCleaner => CreateHistory(),
        ToolCatalog.TracksBrowsers => CreateHistory(TrackModule.Browsers),
        ToolCatalog.TracksOffice => CreateHistory(TrackModule.Office),
        ToolCatalog.TracksWindows => CreateHistory(TrackModule.Windows),
        ToolCatalog.EvidenceRemover => CreateEvidence(),
        ToolCatalog.UnrecoverableDelete => CreateUnrecoverable(),
        ToolCatalog.RegistryOptimizer => CreateRegistryOptimizer(),
        ToolCatalog.RegistryCleaner => CreateRegistryCleaner(),
        ToolCatalog.PrivacyTraces => CreatePrivacyTraces(),
        ToolCatalog.AlternateDataStreams => CreateAlternateDataStreams(),
        ToolCatalog.Unlocker => CleanupViews.Create(toolId),
        ToolCatalog.InternetCleaner => CleanupViews.Create(toolId),
        ToolCatalog.DriveCleaner => CleanupViews.Create(toolId),
        ToolCatalog.ShortcutFixer => CleanupViews.Create(toolId),
        ToolCatalog.MruCleaner => CleanupViews.Create(toolId),
        ToolCatalog.SharedDlls => CleanupViews.Create(toolId),
        ToolCatalog.RecycleBin => CleanupViews.Create(toolId),
        ToolCatalog.Finder => InspectorViews.Create(toolId),
        ToolCatalog.RunOnce => InspectorViews.Create(toolId),
        ToolCatalog.HexViewer => InspectorViews.Create(toolId),
        ToolCatalog.Bloatware => MaintenanceViews.Create(toolId),
        ToolCatalog.Duplicates => MaintenanceViews.Create(toolId),
        ToolCatalog.Printers => MaintenanceViews.Create(toolId),
        ToolCatalog.CrashDumps => MaintenanceViews.Create(toolId),
        ToolCatalog.BrowserCleanup => MaintenanceViews.Create(toolId),
        ToolCatalog.SoftwareUpdates => UpdateViews.Create(toolId),
        ToolCatalog.AppUpdate => UpdateViews.Create(toolId),
        ToolCatalog.CustomTools => CreateCustomTools(),
        ToolCatalog.Processes => CreateProcessPanel(),
        ToolCatalog.SystemManagement => new Management.ManagementView(),
        ToolCatalog.Appearance => new Appearance.AppearanceView(),
        ToolCatalog.SystemInfo => InsightViews.Create(toolId),
        ToolCatalog.ProgramImpact => InsightViews.Create(toolId),
        ToolCatalog.CrashAnalyzer => InsightViews.Create(toolId),
        ToolCatalog.Relocation => InsightViews.Create(toolId),
        ToolCatalog.UninstallHistory => HistoryViews.CreateHistory(),
        _ => CreateUnknown(toolId),
    };

    /// <summary>
    /// Tela de uma ferramenta que o catalogo nao conhece. O erro fica no log
    /// em vez de um painel vazio sem explicacao.
    /// </summary>
    private static FrameworkElement CreateUnknown(string toolId)
    {
        ToolLog.Error($"Ferramenta desconhecida: '{toolId}'.");
        return new ItemListPanel();
    }

    /// <summary>Chave de idioma do titulo de cada modulo do Tracks Cleaner.</summary>
    private static string ModuleTitleKey(TrackModule module) => module switch
    {
        TrackModule.Browsers => "Module.TracksBrowsers",
        TrackModule.Office => "Module.TracksOffice",
        _ => "Module.TracksWindows",
    };

    /// <summary>
    /// Linha uniforme exibida pelas ferramentas de limpeza.
    ///
    /// Implementa <see cref="INotifyPropertyChanged"/> porque a marcacao e
    /// alterada por codigo (menu de contexto: "marcar todos" / "desmarcar
    /// todos"). Sem a notificacao, a caixa de marcacao da tela continuaria
    /// mostrando o estado antigo, mesmo com o modelo ja alterado.
    /// </summary>
    private sealed class ToolRow : INotifyPropertyChanged
    {
        public string Category { get; init; } = string.Empty;
        public string Kind { get; init; } = string.Empty;
        public string Path { get; init; } = string.Empty;
        public string Size { get; init; } = string.Empty;

        /// <summary>Um detalhe extra da linha (a afinidade, por exemplo).</summary>
        public string Detail { get; init; } = string.Empty;

        private bool _selected = true;

        /// <summary>True quando o item esta marcado para remocao.</summary>
        public bool Selected
        {
            get => _selected;
            set
            {
                if (_selected == value) return;

                _selected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
            }
        }

        /// <summary>Item original do nucleo (a remocao usa o item canonico).</summary>
        public object Source { get; init; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    // ------------------------------------------------------------------
    // Gerenciador de Autorun
    // ------------------------------------------------------------------
    private static FrameworkElement CreateAutorun()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<ToolRow>();

        panel.Configure(
            Loc.Get("Module.AutorunManager"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Common.Name"), Width = 240, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Common.Type"), Width = 130, BindingPath = "Kind" },
                new ColumnDef { Header = Loc.Get("Tool.Autorun.Location"), Width = 260, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Tool.Autorun.Command"), Width = 340, BindingPath = "Size" },
            },
            new[]
            {
                new CommandDef { AutomationId = "AutorunRefresh", Text = Loc.Get("Common.Refresh"), Action = () => panel.RunAsync(() => ReloadAsync(panel, rows)) },
                new CommandDef { AutomationId = "AutorunEnable", Text = Loc.Get("Common.Enable"), Action = () => panel.RunAsync(() => SetEnabledAsync(panel, rows, true)) },
                new CommandDef { AutomationId = "AutorunDisable", Text = Loc.Get("Common.Disable"), Action = () => panel.RunAsync(() => SetEnabledAsync(panel, rows, false)) },
                new CommandDef { AutomationId = "AutorunRemove", Text = Loc.Get("Common.Remove"), Action = () => panel.RunAsync(() => RemoveAsync(panel, rows)) },
            });

        panel.SetItems(rows);
        panel.RunAsync(() => ReloadAsync(panel, rows));
        return panel;

        static async Task ReloadAsync(ItemListPanel panel, ObservableCollection<ToolRow> rows)
        {
            var items = await Task.Run(AutorunManager.Scan);

            rows.Clear();
            foreach (var item in items)
            {
                rows.Add(new ToolRow
                {
                    Category = item.Name,
                    Kind = KindLabel(item.Kind),
                    Path = string.IsNullOrWhiteSpace(item.Location) ? item.Source : item.Location,
                    Size = item.Command,
                    Source = item,
                });
            }

            panel.PanelStatus = Loc.Get("Status.Done") + $" — {rows.Count} item(ns)";
        }

        static async Task SetEnabledAsync(ItemListPanel panel, ObservableCollection<ToolRow> rows, bool enabled)
        {
            var target = FirstAutorun(panel);
            if (target is null)
            {
                panel.PanelStatus = Loc.Get("Status.NoSelection");
                return;
            }

            var ok = await Task.Run(() => AutorunManager.SetEnabled(target, enabled));
            panel.PanelStatus = ok ? Loc.Get("Status.Done") : Loc.Get("Status.Error");
            await ReloadAsync(panel, rows);
        }

        static async Task RemoveAsync(ItemListPanel panel, ObservableCollection<ToolRow> rows)
        {
            var target = FirstAutorun(panel);
            if (target is null)
            {
                panel.PanelStatus = Loc.Get("Status.NoSelection");
                return;
            }

            if (target.IsSystem)
            {
                panel.PanelStatus = Loc.Get("Status.Error") + ": item de sistema";
                return;
            }

            var ok = await Task.Run(() => AutorunManager.Remove(target));
            panel.PanelStatus = ok ? Loc.Get("Status.Done") : Loc.Get("Status.Error");
            await ReloadAsync(panel, rows);
        }

        static AutorunItem? FirstAutorun(ItemListPanel panel)
        {
            foreach (var item in panel.SelectedItems)
                if (item is ToolRow row && row.Source is AutorunItem autorun) return autorun;
            return null;
        }
    }

    // ------------------------------------------------------------------
    // Boot Center: o que sobe junto com a maquina
    // ------------------------------------------------------------------
    private static FrameworkElement CreateBootCenter()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<ToolRow>();

        panel.Configure(
            Loc.Get("Module.BootCenter"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Common.Name"), Width = 240, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Common.Type"), Width = 130, BindingPath = "Kind" },
                new ColumnDef { Header = Loc.Get("Common.Status"), Width = 110, BindingPath = "Detail" },
                new ColumnDef { Header = Loc.Get("Tool.Autorun.Location"), Width = 260, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Tool.Autorun.Command"), Width = 340, BindingPath = "Size" },
            },
            new[]
            {
                new CommandDef { AutomationId = "BootCenterRefresh", Text = Loc.Get("Common.Refresh"), Action = () => panel.RunAsync(() => ReloadAsync(panel, rows)) },
                new CommandDef { AutomationId = "BootCenterEnable", Text = Loc.Get("Common.Enable"), Action = () => panel.RunAsync(() => SetEnabledAsync(panel, rows, true)) },
                new CommandDef { AutomationId = "BootCenterDisable", Text = Loc.Get("Common.Disable"), Action = () => panel.RunAsync(() => SetEnabledAsync(panel, rows, false)) },
                new CommandDef { AutomationId = "BootCenterRemove", Text = Loc.Get("Common.Remove"), Action = () => panel.RunAsync(() => RemoveAsync(panel, rows)) },
            });

        panel.SetItems(rows);
        panel.RunAsync(() => ReloadAsync(panel, rows));
        return panel;

        static async Task ReloadAsync(ItemListPanel panel, ObservableCollection<ToolRow> rows)
        {
            var report = await Task.Run(BootCenter.Scan);

            rows.Clear();
            foreach (var item in report.Items)
            {
                rows.Add(new ToolRow
                {
                    Category = item.Name,
                    Kind = item.Kind == BootCenterItemKind.ScheduledTask
                        ? Loc.Get("Tool.Autorun.Tasks")
                        : Loc.Get("Tool.Autorun.Run"),
                    Detail = item.Enabled ? Loc.Get("Common.Enable") : Loc.Get("Common.Disable"),
                    Path = item.Location,
                    Size = item.Command,
                    Source = item,
                });
            }

            panel.PanelStatus = Loc.Get("Tool.BootCenter.Status",
                report.BootTime.ToString("g"),
                report.Uptime,
                rows.Count,
                report.EnabledCount);
        }

        static async Task SetEnabledAsync(ItemListPanel panel, ObservableCollection<ToolRow> rows, bool enabled)
        {
            var target = FirstBootItem(panel);
            if (target is null)
            {
                panel.PanelStatus = Loc.Get("Status.NoSelection");
                return;
            }

            var ok = await Task.Run(() => BootCenter.SetEnabled(target, enabled));
            panel.PanelStatus = ok ? Loc.Get("Status.Done") : Loc.Get("Status.Error");
            await ReloadAsync(panel, rows);
        }

        static async Task RemoveAsync(ItemListPanel panel, ObservableCollection<ToolRow> rows)
        {
            var target = FirstBootItem(panel);
            if (target is null)
            {
                panel.PanelStatus = Loc.Get("Status.NoSelection");
                return;
            }

            var ok = await Task.Run(() => BootCenter.Remove(target));
            panel.PanelStatus = ok ? Loc.Get("Status.Done") : Loc.Get("Status.Error");
            await ReloadAsync(panel, rows);
        }

        static BootCenterItem? FirstBootItem(ItemListPanel panel)
        {
            foreach (var item in panel.SelectedItems)
                if (item is ToolRow row && row.Source is BootCenterItem boot) return boot;
            return null;
        }
    }

    // ------------------------------------------------------------------
    // Arquivos em uso: quem segura o arquivo
    // ------------------------------------------------------------------
    private static FrameworkElement CreateActiveFiles()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<ToolRow>();
        var paths = new List<string>();

        panel.Configure(
            Loc.Get("Module.ActiveFiles"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Common.Path"), Width = 560, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Tool.ActiveFiles.Processes"), Width = 320, BindingPath = "Size" },
            },
            new[]
            {
                new CommandDef { AutomationId = "ActiveFilesPick", Text = Loc.Get("Tool.ActiveFiles.Pick"), IsPrimary = true, Action = () => Escolher() },
                new CommandDef { AutomationId = "ActiveFilesScan", Text = Loc.Get("Common.Scan"), Action = () => panel.RunAsync(() => VerificarAsync(panel, rows, paths)) },
            });

        panel.SetItems(rows);
        panel.PanelStatus = Loc.Get("Tool.ActiveFiles.Start");
        return panel;

        void Escolher()
        {
            var dialog = new OpenFileDialog
            {
                Title = Loc.Get("Tool.ActiveFiles.Pick"),
                Multiselect = true,
            };

            if (dialog.ShowDialog() != true) return;

            paths.Clear();
            paths.AddRange(dialog.FileNames);
            panel.PanelStatus = Loc.Get("Tool.ActiveFiles.Selected", paths.Count);
        }

        static async Task VerificarAsync(ItemListPanel panel, ObservableCollection<ToolRow> rows, List<string> paths)
        {
            if (paths.Count == 0)
            {
                panel.PanelStatus = Loc.Get("Tool.ActiveFiles.Start");
                return;
            }

            var locked = await Task.Run(() => ActiveFiles.Scan(paths));

            rows.Clear();
            foreach (var file in locked)
            {
                rows.Add(new ToolRow
                {
                    Path = file.Path,
                    Size = file.DescribeProcesses(),
                    Source = file,
                });
            }

            panel.PanelStatus = locked.Count == 0
                ? Loc.Get("Common.Empty")
                : Loc.Get("Tool.ActiveFiles.Found", locked.Count);
        }
    }

    // ------------------------------------------------------------------
    // File Associator: os tipos de arquivo e o programa que os abre
    // ------------------------------------------------------------------
    private static FrameworkElement CreateFileAssociator()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<ToolRow>();

        panel.Configure(
            Loc.Get("Module.FileAssociator"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Tool.FileAssociator.Extension"), Width = 90, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Tool.FileAssociator.Description"), Width = 240, BindingPath = "Detail" },
                new ColumnDef { Header = Loc.Get("Tool.FileAssociator.Program"), Width = 240, BindingPath = "Size" },
                new ColumnDef { Header = Loc.Get("Tool.FileAssociator.Path"), Width = 380, BindingPath = "Path" },
            },
            new[]
            {
                new CommandDef { AutomationId = "FileAssocScan", Text = Loc.Get("Common.Scan"), Action = () => panel.RunAsync(() => ReloadAsync(panel, rows)) },
                new CommandDef { AutomationId = "FileAssocSet", Text = Loc.Get("Tool.FileAssociator.Set"), Action = () => Definir(panel, rows) },
                new CommandDef { AutomationId = "FileAssocUndo", Text = Loc.Get("Tool.FileAssociator.Undo"), Action = () => panel.RunAsync(() => DesfazerAsync(panel, rows)) },
            });

        panel.SetItems(rows);
        panel.RunAsync(() => ReloadAsync(panel, rows));
        return panel;

        static async Task ReloadAsync(ItemListPanel panel, ObservableCollection<ToolRow> rows)
        {
            var report = await Task.Run(FileAssociator.Scan);

            rows.Clear();
            foreach (var item in report.Items)
            {
                rows.Add(new ToolRow
                {
                    Category = item.Extension,
                    Detail = item.Description,
                    Size = item.HasProgram ? item.ProgramName : Loc.Get("Tool.FileAssociator.None"),
                    Path = item.ProgramPath,
                    Source = item,
                });
            }

            panel.PanelStatus = Loc.Get("Tool.FileAssociator.Status",
                report.Items.Count,
                report.UnassociatedCount,
                report.BrokenCount);
        }

        void Definir(ItemListPanel panel, ObservableCollection<ToolRow> rows)
        {
            var alvo = Primeiro(panel);
            if (alvo is null)
            {
                panel.PanelStatus = Loc.Get("Status.NoSelection");
                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = Loc.Get("Tool.FileAssociator.Pick"),
                Filter = Loc.Get("Tool.FileAssociator.PickFilter"),
                CheckFileExists = true,
            };

            if (dialog.ShowDialog() != true) return;

            var (ok, mensagem) = FileAssociator.SetDefault(alvo.Extension, dialog.FileName);
            panel.PanelStatus = ok ? mensagem : Loc.Get("Tool.FileAssociator.Error", mensagem);
            panel.RunAsync(() => ReloadAsync(panel, rows));
        }

        static async Task DesfazerAsync(ItemListPanel panel, ObservableCollection<ToolRow> rows)
        {
            var alvo = Primeiro(panel);
            if (alvo is null)
            {
                panel.PanelStatus = Loc.Get("Status.NoSelection");
                return;
            }

            var (ok, mensagem) = await Task.Run(() => FileAssociator.Undo(alvo.Extension));
            panel.PanelStatus = ok ? mensagem : Loc.Get("Tool.FileAssociator.Error", mensagem);
            await ReloadAsync(panel, rows);
        }

        static FileAssociationItem? Primeiro(ItemListPanel panel)
        {
            foreach (var item in panel.SelectedItems)
                if (item is ToolRow row && row.Source is FileAssociationItem associacao) return associacao;
            return null;
        }
    }

    // ------------------------------------------------------------------
    // Centro de mudancas: o que o Tranal mudou e como voltar atras
    // ------------------------------------------------------------------
    private static FrameworkElement CreateUndoCenter()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<ToolRow>();

        panel.Configure(
            Loc.Get("Module.UndoCenter"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Tool.UndoCenter.When"), Width = 150, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Tool.UndoCenter.Program"), Width = 240, BindingPath = "Size" },
                new ColumnDef { Header = Loc.Get("Tool.UndoCenter.What"), Width = 260, BindingPath = "Detail" },
                new ColumnDef { Header = Loc.Get("Tool.UndoCenter.Items"), Width = 80, BindingPath = "Path" },
            },
            new[]
            {
                new CommandDef { AutomationId = "UndoCenterRefresh", Text = Loc.Get("Common.Refresh"), Action = () => panel.RunAsync(() => ReloadAsync(panel, rows)) },
                new CommandDef { AutomationId = "UndoCenterUndo", Text = Loc.Get("Tool.UndoCenter.Undo"), Action = () => panel.RunAsync(() => DesfazerAsync(panel, rows)) },
                new CommandDef { AutomationId = "UndoCenterRedo", Text = Loc.Get("Tool.UndoCenter.Redo"), Action = () => panel.RunAsync(() => RefazerAsync(panel, rows)) },
            });

        panel.SetItems(rows);
        panel.RunAsync(() => ReloadAsync(panel, rows));
        return panel;

        static async Task ReloadAsync(ItemListPanel panel, ObservableCollection<ToolRow> rows)
        {
            var report = await Task.Run(UndoChangesCenter.Scan);

            rows.Clear();
            foreach (var item in report.Items)
            {
                rows.Add(new ToolRow
                {
                    Category = item.Timestamp.ToString("g"),
                    Size = item.ProgramName,
                    Detail = item.Description,
                    Path = item.ItemCount.ToString(),
                    Source = item,
                });
            }

            panel.PanelStatus = report.Total == 0
                ? Loc.Get("Tool.UndoCenter.Empty")
                : Loc.Get("Tool.UndoCenter.Status", report.Total, report.UndoneCount);
        }

        static async Task DesfazerAsync(ItemListPanel panel, ObservableCollection<ToolRow> rows)
        {
            var alvo = Primeiro(panel);
            if (alvo is null)
            {
                panel.PanelStatus = Loc.Get("Status.NoSelection");
                return;
            }

            var (ok, mensagem) = await Task.Run(() => UndoChangesCenter.Undo(alvo.Id));
            panel.PanelStatus = ok ? mensagem : Loc.Get("Status.Error");
            await ReloadAsync(panel, rows);
        }

        static async Task RefazerAsync(ItemListPanel panel, ObservableCollection<ToolRow> rows)
        {
            var alvo = Primeiro(panel);
            if (alvo is null)
            {
                panel.PanelStatus = Loc.Get("Status.NoSelection");
                return;
            }

            var (ok, mensagem) = await Task.Run(() => UndoChangesCenter.Redo(alvo.Id));
            panel.PanelStatus = ok ? mensagem : Loc.Get("Status.Error");
            await ReloadAsync(panel, rows);
        }

        static UndoChangeItem? Primeiro(ItemListPanel panel)
        {
            foreach (var item in panel.SelectedItems)
                if (item is ToolRow row && row.Source is UndoChangeItem mudanca) return mudanca;
            return null;
        }
    }

    private static string KindLabel(AutorunItemKind kind) => kind switch
    {
        AutorunItemKind.RunKey => Loc.Get("Tool.Autorun.Run"),
        AutorunItemKind.StartupFolder => Loc.Get("Tool.Autorun.Folders"),
        AutorunItemKind.Service => Loc.Get("Tool.Autorun.Services"),
        AutorunItemKind.ScheduledTask => Loc.Get("Tool.Autorun.Tasks"),
        _ => kind.ToString(),
    };

    // ------------------------------------------------------------------
    // Limpeza de arquivos temporarios
    // ------------------------------------------------------------------
    private static FrameworkElement CreateJunk()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<ToolRow>();
        var items = new List<JunkItem>();

        panel.Configure(
            Loc.Get("Module.JunkCleaner"),
            new[]
            {
                new ColumnDef { Header = "", Width = 34, IsCheckable = true },
                new ColumnDef { Header = Loc.Get("Tool.Junk.Category"), Width = 220, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Common.Type"), Width = 90, BindingPath = "Kind" },
                new ColumnDef { Header = Loc.Get("Common.Path"), Width = 520, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Common.Size"), Width = 100, BindingPath = "Size" },
            },
            new[]
            {
                new CommandDef { AutomationId = "JunkScan", Text = Loc.Get("Tool.Junk.Scan"), Action = () => panel.RunAsync(() => ScanAsync(panel, rows, items)) },
                new CommandDef { AutomationId = "JunkDelete", Text = Loc.Get("Tool.Junk.Delete"), Action = () => panel.RunAsync(() => CleanAsync(panel, rows, items)) },
                new CommandDef { AutomationId = "JunkDiskCleanup", Text = Loc.Get("Tool.Junk.DiskCleanup"), Action = () => SystemActions.AbrirLimpezaDeDisco() },
            });

        panel.SetItems(rows);
        panel.RunAsync(() => ScanAsync(panel, rows, items));
        return panel;

        static async Task ScanAsync(ItemListPanel panel, ObservableCollection<ToolRow> rows, List<JunkItem> items)
        {
            var found = await Task.Run(JunkCleaner.Scan);

            items.Clear();
            items.AddRange(found);

            rows.Clear();
            foreach (var item in found)
            {
                rows.Add(new ToolRow
                {
                    Category = CategoryLabel.For(item.CategoryId),
                    Kind = item.Kind,
                    Path = item.Path,
                    Size = ByteFormatter.Format(item.SizeBytes),
                    Source = item,
                });
            }

            panel.PanelStatus = Loc.Get("Status.Done") + $" — {rows.Count} item(ns)";
        }

        static async Task CleanAsync(ItemListPanel panel, ObservableCollection<ToolRow> rows, List<JunkItem> items)
        {
            var selected = rows.Where(r => r.Selected).Select(r => r.Source).OfType<JunkItem>().ToList();
            if (selected.Count == 0)
            {
                panel.PanelStatus = Loc.Get("Status.NoSelection");
                return;
            }

            var report = await Task.Run(() => JunkCleaner.Clean(selected));
            panel.PanelStatus = Loc.Get("Status.Removed") + $": {report.RemovedCount} | "
                + Loc.Get("Common.Error") + $": {report.FailedCount + report.ProtectedCount}";

            await ScanAsync(panel, rows, items);
        }
    }

    // ------------------------------------------------------------------
    // Removedor de evidencias
    // ------------------------------------------------------------------
    private static FrameworkElement CreateEvidence()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<ToolRow>();
        var items = new List<EvidenceItem>();

        panel.Configure(
            Loc.Get("Module.EvidenceRemover"),
            new[]
            {
                new ColumnDef { Header = "", Width = 34, IsCheckable = true },
                new ColumnDef { Header = Loc.Get("Tool.Junk.Category"), Width = 220, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Common.Type"), Width = 90, BindingPath = "Kind" },
                new ColumnDef { Header = Loc.Get("Common.Path"), Width = 560, BindingPath = "Path" },
            },
            new[]
            {
                new CommandDef { AutomationId = "EvidenceScan", Text = Loc.Get("Tool.Junk.Scan"), Action = () => panel.RunAsync(() => ScanAsync(panel, rows, items)) },
                new CommandDef { AutomationId = "EvidenceExecute", Text = Loc.Get("Tool.Evidence.Execute"), Action = () => panel.RunAsync(() => RemoveAsync(panel, rows, items)) },
            });

        panel.SetItems(rows);
        panel.RunAsync(() => ScanAsync(panel, rows, items));
        return panel;

        static async Task ScanAsync(ItemListPanel panel, ObservableCollection<ToolRow> rows, List<EvidenceItem> items)
        {
            var found = await Task.Run(EvidenceRemover.Scan);

            items.Clear();
            items.AddRange(found);

            rows.Clear();
            foreach (var item in found)
            {
                rows.Add(new ToolRow
                {
                    Category = CategoryLabel.For(item.CategoryId),
                    Kind = item.Kind,
                    Path = item.Path,
                    Source = item,
                });
            }

            panel.PanelStatus = Loc.Get("Status.Done") + $" — {rows.Count} item(ns)";
        }

        static async Task RemoveAsync(ItemListPanel panel, ObservableCollection<ToolRow> rows, List<EvidenceItem> items)
        {
            var selected = rows.Where(r => r.Selected).Select(r => r.Source).OfType<EvidenceItem>().ToList();
            if (selected.Count == 0)
            {
                panel.PanelStatus = Loc.Get("Status.NoSelection");
                return;
            }

            var report = await Task.Run(() => EvidenceRemover.Remove(selected));
            panel.PanelStatus = Loc.Get("Status.Removed") + $": {report.RemovedCount} | "
                + Loc.Get("Common.Error") + $": {report.FailedCount + report.ProtectedCount}";

            await ScanAsync(panel, rows, items);
        }
    }

    // ------------------------------------------------------------------
    // Limpeza de historico
    // ------------------------------------------------------------------
    private static FrameworkElement CreateHistory(TrackModule? module = null)
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<ToolRow>();
        var items = new List<HistoryItem>();

        panel.Configure(
            module is null ? Loc.Get("Module.HistoryCleaner") : Loc.Get(ModuleTitleKey(module.Value)),
            new[]
            {
                new ColumnDef { Header = "", Width = 34, IsCheckable = true },
                new ColumnDef { Header = Loc.Get("Tool.Junk.Category"), Width = 220, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Common.Type"), Width = 90, BindingPath = "Kind" },
                new ColumnDef { Header = Loc.Get("Common.Path"), Width = 520, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Common.Size"), Width = 100, BindingPath = "Size" },
            },
            new[]
            {
                new CommandDef { AutomationId = "HistoryScan", Text = Loc.Get("Tool.Junk.Scan"), Action = () => panel.RunAsync(() => ScanAsync(panel, rows, items)) },
                new CommandDef { AutomationId = "HistoryClear", Text = Loc.Get("Tool.History.Clear"), Action = () => panel.RunAsync(() => ClearAsync(panel, rows, items, false)) },
                new CommandDef { AutomationId = "HistoryClearAll", Text = Loc.Get("Tool.History.ClearAll"), Action = () => panel.RunAsync(() => ClearAsync(panel, rows, items, true)) },
            });

        panel.SetItems(rows);
        panel.RunAsync(() => ScanAsync(panel, rows, items));
        return panel;

        async Task ScanAsync(ItemListPanel panel, ObservableCollection<ToolRow> rows, List<HistoryItem> items)
        {
            var found = await Task.Run(() => module is null
                ? HistoryCleaner.Scan()
                : HistoryCleaner.Scan(module.Value));

            items.Clear();
            items.AddRange(found);

            rows.Clear();
            foreach (var item in found)
            {
                rows.Add(new ToolRow
                {
                    Category = CategoryLabel.For(item.CategoryId),
                    Kind = item.Kind,
                    Path = item.Path,
                    Size = ByteFormatter.Format(item.SizeBytes),
                    Source = item,
                });
            }

            panel.PanelStatus = Loc.Get("Status.Done") + $" — {rows.Count} item(ns)";
        }

        async Task ClearAsync(ItemListPanel panel, ObservableCollection<ToolRow> rows, List<HistoryItem> items, bool all)
        {
            var selected = all
                ? items
                : rows.Where(r => r.Selected).Select(r => r.Source).OfType<HistoryItem>().ToList();

            if (selected.Count == 0)
            {
                panel.PanelStatus = Loc.Get("Status.NoSelection");
                return;
            }

            var report = await Task.Run(() => HistoryCleaner.Clear(selected));
            panel.PanelStatus = Loc.Get("Status.Removed") + $": {report.RemovedCount} | "
                + Loc.Get("Common.Error") + $": {report.FailedCount + report.ProtectedCount}";

            await ScanAsync(panel, rows, items);
        }
    }

    // ------------------------------------------------------------------
    // Otimizador de Registro (entradas obsoletas do Menu Iniciar)
    // ------------------------------------------------------------------
    private static FrameworkElement CreateRegistryOptimizer()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<ToolRow>();
        var items = new List<RegistryOptimizerEntry>();

        panel.Configure(
            Loc.Get("Module.RegistryOptimizer"),
            new[]
            {
                new ColumnDef { Header = "", Width = 34, IsCheckable = true },
                new ColumnDef { Header = Loc.Get("Common.Name"), Width = 260, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Common.Path"), Width = 560, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Common.Detail"), Width = 340, BindingPath = "Size" },
            },
            new[]
            {
                new CommandDef { AutomationId = "RegistryOptimizerScan", Text = Loc.Get("RegistryOptimizer.Scan"), Action = () => panel.RunAsync(() => ScanAsync(panel, rows, items)) },
                new CommandDef { AutomationId = "RegistryOptimizerRemove", Text = Loc.Get("RegistryOptimizer.RemoveSelected"), Action = () => panel.RunAsync(() => RemoveAsync(panel, rows, items)) },
            });

        panel.SetItems(rows);
        panel.RunAsync(() => ScanAsync(panel, rows, items));
        return panel;

        static async Task ScanAsync(ItemListPanel panel, ObservableCollection<ToolRow> rows, List<RegistryOptimizerEntry> items)
        {
            var found = await Task.Run(RegistryOptimizer.ScanStartMenu);

            items.Clear();
            items.AddRange(found);

            rows.Clear();
            foreach (var item in found)
            {
                rows.Add(new ToolRow
                {
                    Category = item.DisplayName,
                    Path = item.Path,
                    Size = item.Detail,
                    Source = item,
                });
            }

            panel.PanelStatus = found.Count == 0
                ? Loc.Get("RegistryOptimizer.Empty")
                : string.Format(CultureInfo.CurrentCulture, Loc.Get("RegistryOptimizer.Found"), found.Count);
        }

        static async Task RemoveAsync(ItemListPanel panel, ObservableCollection<ToolRow> rows, List<RegistryOptimizerEntry> items)
        {
            var selected = rows.Where(r => r.Selected).Select(r => r.Source).OfType<RegistryOptimizerEntry>().ToList();

            if (selected.Count == 0)
            {
                panel.PanelStatus = Loc.Get("Status.NoSelection");
                return;
            }

            var report = await Task.Run(() => RegistryOptimizer.Remove(selected));
            panel.PanelStatus = Loc.Get("Status.Removed") + $": {report.RemovedCount} | "
                + Loc.Get("Common.Error") + $": {report.FailedCount + report.ProtectedCount}";

            await ScanAsync(panel, rows, items);
        }
    }

    // ------------------------------------------------------------------
    // Limpador de Registro por categoria
    // ------------------------------------------------------------------
    private static FrameworkElement CreateRegistryCleaner()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<ToolRow>();
        var items = new List<RegistryCleanerEntry>();

        panel.Configure(
            Loc.Get("Module.RegistryCleaner"),
            new[]
            {
                new ColumnDef { Header = "", Width = 34, IsCheckable = true },
                new ColumnDef { Header = Loc.Get("Common.Type"), Width = 240, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Common.Path"), Width = 560, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Common.Detail"), Width = 340, BindingPath = "Size" },
            },
            new[]
            {
                new CommandDef { AutomationId = "RegistryCleanerScan", Text = Loc.Get("RegistryCleaner.Scan"), Action = () => panel.RunAsync(() => ScanAsync(panel, rows, items)) },
                new CommandDef { AutomationId = "RegistryCleanerCategories", Text = Loc.Get("RegistryCleaner.Categories"), Action = () => ShowCategories() },
                new CommandDef { AutomationId = "RegistryCleanerRemove", Text = Loc.Get("RegistryCleaner.RemoveSelected"), Action = () => panel.RunAsync(() => RemoveAsync(panel, rows, items)) },
            });

        panel.SetItems(rows);
        panel.RunAsync(() => ScanAsync(panel, rows, items));
        return panel;

        static void ShowCategories()
        {
            var linhas = RegistryCleaner.Categories
                .Select(c => $"{c.Id} — {c.Name}");

            UserMessages.Information(
                Application.Current?.MainWindow,
                string.Join(Environment.NewLine, linhas),
                Loc.Get("RegistryCleaner.Categories"));
        }

        static async Task ScanAsync(ItemListPanel panel, ObservableCollection<ToolRow> rows, List<RegistryCleanerEntry> items)
        {
            var found = await Task.Run(() => RegistryCleaner.Scan());

            items.Clear();
            items.AddRange(found);

            rows.Clear();
            foreach (var item in found)
            {
                rows.Add(new ToolRow
                {
                    Category = item.CategoryName,
                    Path = item.IsValue ? $"{item.Path} \\ {item.ValueName}" : item.Path,
                    Size = item.Detail,
                    Source = item,
                });
            }

            panel.PanelStatus = found.Count == 0
                ? Loc.Get("RegistryCleaner.Empty")
                : string.Format(CultureInfo.CurrentCulture, Loc.Get("RegistryCleaner.Found"), found.Count);
        }

        static async Task RemoveAsync(ItemListPanel panel, ObservableCollection<ToolRow> rows, List<RegistryCleanerEntry> items)
        {
            var selected = rows.Where(r => r.Selected).Select(r => r.Source).OfType<RegistryCleanerEntry>().ToList();

            if (selected.Count == 0)
            {
                panel.PanelStatus = Loc.Get("Status.NoSelection");
                return;
            }

            var report = await Task.Run(() => RegistryCleaner.Remove(selected));
            panel.PanelStatus = Loc.Get("Status.Removed") + $": {report.RemovedCount} | "
                + Loc.Get("Common.Error") + $": {report.FailedCount + report.ProtectedCount}";

            await ScanAsync(panel, rows, items);
        }
    }

    // ------------------------------------------------------------------
    // Painel de processos
    // ------------------------------------------------------------------
    private static FrameworkElement CreateProcessPanel()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<ToolRow>();

        panel.Configure(
            Loc.Get("Module.Processes"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Common.Name"), Width = 220, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Module.ProcessOwner"), Width = 220, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Common.Path"), Width = 380, BindingPath = "Size" },
                new ColumnDef { Header = Loc.Get("Common.Size"), Width = 110, BindingPath = "Kind" },
                new ColumnDef { Header = Loc.Get("Tool.Processes.Affinity"), Width = 130, BindingPath = "Detail" },
            },
            new[]
            {
                new CommandDef { AutomationId = "ProcessesRefresh", Text = Loc.Get("Common.Refresh"), Action = () => panel.RunAsync(() => LoadAsync(panel, rows)) },
            });

        panel.SetItems(rows);
        panel.RunAsync(() => LoadAsync(panel, rows));
        return panel;

        static async Task LoadAsync(ItemListPanel panel, ObservableCollection<ToolRow> rows)
        {
            var programas = ProgramScanner.Scan(new ProgramScanner.ScanOptions
            {
                IncludeSystemComponents = false,
                IncludeMsiProducts = false,
            });

            var rodando = await Task.Run(() => ProcessInspector.List(programas));

            // A afinidade diz quantos nucleos o Windows deixa cada
            // programa usar: e o que explica um programa preso a um so.
            var afinidades = await Task.Run(() => ProcessAffinity.List(rodando));
            var porProcesso = afinidades.ToDictionary(a => a.ProcessId);

            rows.Clear();
            foreach (var processo in rodando)
            {
                rows.Add(new ToolRow
                {
                    Category = $"{processo.Name} ({processo.Id})",
                    Path = processo.InstalledProgram,
                    Size = processo.Path,
                    Kind = processo.WorkingSetText,
                    Detail = porProcesso.TryGetValue(processo.Id, out var afinidade)
                        ? afinidade.Text
                        : string.Empty,
                    Source = processo,
                });
            }

            panel.PanelStatus = string.Format(
                CultureInfo.CurrentCulture, Loc.Get("Processes.Found"), rodando.Count);
        }
    }

    // ------------------------------------------------------------------
    // Exclusao irrecuperavel
    // ------------------------------------------------------------------
    private static FrameworkElement CreateUnrecoverable()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<ToolRow>();
        var paths = new List<string>();

        panel.Configure(
            Loc.Get("Module.UnrecoverableDelete"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Common.Type"), Width = 90, BindingPath = "Kind" },
                new ColumnDef { Header = Loc.Get("Common.Path"), Width = 620, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Common.Size"), Width = 120, BindingPath = "Size" },
            },
            new[]
            {
                new CommandDef { AutomationId = "UnrecoverableAddFiles", Text = Loc.Get("Tool.Unrecoverable.AddFiles"), Action = () => AddFiles(panel, rows, paths) },
                new CommandDef { AutomationId = "UnrecoverableAddFolder", Text = Loc.Get("Tool.Unrecoverable.AddFolder"), Action = () => AddFolder(panel, rows, paths) },
                new CommandDef { AutomationId = "UnrecoverableDelete", Text = Loc.Get("Common.DeleteForever"), Action = () => panel.RunAsync(() => DeleteAsync(panel, rows, paths)) },
            });

        panel.SetItems(rows);
        panel.PanelStatus = Loc.Get("Tool.Unrecoverable.Overwrite", UnrecoverableDelete.DefaultPasses);
        return panel;

        static void AddFiles(ItemListPanel panel, ObservableCollection<ToolRow> rows, List<string> paths)
        {
            var dialog = new OpenFileDialog
            {
                Title = Loc.Get("Tool.Unrecoverable.AddFiles"),
                Multiselect = true,
                Filter = Loc.Get("Forced.BrowseFilter"),
            };
            if (dialog.ShowDialog() != true) return;

            foreach (var file in dialog.FileNames)
            {
                if (paths.Contains(file)) continue;
                paths.Add(file);
            }

            Refresh(panel, rows, paths);
        }

        static void AddFolder(ItemListPanel panel, ObservableCollection<ToolRow> rows, List<string> paths)
        {
            var dialog = new OpenFileDialog
            {
                Title = Loc.Get("Tool.Unrecoverable.AddFolder"),
                CheckFileExists = false,
                ValidateNames = false,
                FileName = "Selecionar pasta",
            };
            if (dialog.ShowDialog() != true) return;

            var folder = System.IO.Path.GetDirectoryName(dialog.FileName);
            if (string.IsNullOrWhiteSpace(folder)) return;
            if (paths.Contains(folder)) return;

            paths.Add(folder);
            Refresh(panel, rows, paths);
        }

        static void Refresh(ItemListPanel panel, ObservableCollection<ToolRow> rows, List<string> paths)
        {
            rows.Clear();
            foreach (var path in paths)
            {
                var isFolder = System.IO.Directory.Exists(path);
                rows.Add(new ToolRow
                {
                    Kind = isFolder ? "Pasta" : "Arquivo",
                    Path = path,
                    Size = isFolder ? string.Empty : ByteFormatter.Format(new System.IO.FileInfo(path).Length),
                });
            }

            panel.PanelStatus = Loc.Get("Tool.Unrecoverable.Overwrite", UnrecoverableDelete.DefaultPasses);
        }

        static async Task DeleteAsync(ItemListPanel panel, ObservableCollection<ToolRow> rows, List<string> paths)
        {
            if (paths.Count == 0)
            {
                panel.PanelStatus = Loc.Get("Status.NoSelection");
                return;
            }

            var results = await Task.Run(() => UnrecoverableDelete.Delete(paths, UnrecoverableDelete.DefaultPasses));
            var ok = results.Count(r => r.Succeeded);

            panel.PanelStatus = Loc.Get("Tool.Unrecoverable.Done") + $": {ok}/{results.Count}";

            paths.Clear();
            rows.Clear();
        }
    }

    // ------------------------------------------------------------------
    // Ferramentas do Windows
    // ------------------------------------------------------------------
    private static FrameworkElement CreateWindowsTools()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<ToolRow>();

        panel.Configure(
            Loc.Get("Module.WindowsTools"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Common.Name"), Width = 260, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Tool.WindowsTools.Category"), Width = 130, BindingPath = "Kind" },
                new ColumnDef { Header = Loc.Get("Common.Description"), Width = 380, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Common.Program"), Width = 220, BindingPath = "Size" },
            },
            new[]
            {
                new CommandDef { AutomationId = "WindowsToolsRun", Text = Loc.Get("Tool.WindowsTools.Run"), Action = () => Run(panel, rows) },
            });

        panel.SetItems(rows);

        foreach (var tool in WindowsToolsCatalog.All)
        {
            rows.Add(new ToolRow
            {
                Category = tool.Name,
                Kind = CategoryLabel.For(tool.CategoryId),
                Path = tool.Description,
                Size = tool.Command,
                Source = tool,
            });
        }

        panel.PanelStatus = Loc.Get("Status.Done") + $" — {rows.Count} ferramenta(s)";
        return panel;

        static void Run(ItemListPanel panel, ObservableCollection<ToolRow> rows)
        {
            foreach (var item in panel.SelectedItems)
            {
                if (item is not ToolRow row || row.Source is not WindowsTool tool) continue;

                var ok = WindowsToolsCatalog.Run(tool);
                panel.PanelStatus = ok
                    ? $"{tool.Name}: {Loc.Get("Status.Done")}"
                    : $"{tool.Name}: {Loc.Get("Status.Error")}";
                return;
            }

            panel.PanelStatus = Loc.Get("Status.NoSelection");
        }
    }

    // ------------------------------------------------------------------
    // Rastros de privacidade
    // ------------------------------------------------------------------

    /// <summary>
    /// Tela dos rastros de privacidade: as regras do firewall, o historico
    /// UserAssist, os relatorios de erro, as classes COM do perfil do
    /// usuario, a reproducao de midia e o log de Aplicacao.
    ///
    /// A lista e a limpeza sao as do nucleo
    /// (<c>Core/Tools/PrivacyTraceCleaner.cs</c>): a tela mostra o que o
    /// nucleo achou e devolve os itens marcados. Um grupo nunca invade o
    /// outro — essa regra e do nucleo.
    /// </summary>
    public static FrameworkElement CreatePrivacyTraces()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<ToolRow>();
        var items = new List<PrivacyTraceItem>();

        panel.Configure(
            Loc.Get("Module.PrivacyTraces"),
            new[]
            {
                new ColumnDef { Header = "", Width = 34, IsCheckable = true },
                new ColumnDef { Header = Loc.Get("Common.Group"), Width = 170, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Common.Type"), Width = 140, BindingPath = "Kind" },
                new ColumnDef { Header = Loc.Get("Common.Path"), Width = 470, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Common.Detail"), Width = 300, BindingPath = "Size" },
            },
            new[]
            {
                new CommandDef { AutomationId = "PrivacyTracesScan", Text = Loc.Get("Tool.Privacy.Scan"), Action = () => panel.RunAsync(() => ScanAsync(panel, rows, items)) },
                new CommandDef { AutomationId = "PrivacyTracesClean", Text = Loc.Get("Tool.Privacy.Clean"), Action = () => panel.RunAsync(() => CleanAsync(panel, rows, items)) },
            });

        panel.SetItems(rows);
        panel.RunAsync(() => ScanAsync(panel, rows, items));
        return panel;

        static async Task ScanAsync(ItemListPanel panel, ObservableCollection<ToolRow> rows, List<PrivacyTraceItem> items)
        {
            var found = await Task.Run(() => PrivacyTraceCleaner.Scan());

            items.Clear();
            items.AddRange(found);

            rows.Clear();
            foreach (var item in found)
            {
                rows.Add(new ToolRow
                {
                    Category = PrivacyGroupLabel(item.Group),
                    Kind = item.Kind,
                    Path = item.Path,
                    Size = item.Detail,
                    Source = item,
                });
            }

            panel.PanelStatus = Loc.Get("Status.Done") + $" — {rows.Count} item(ns)";
        }

        static async Task CleanAsync(ItemListPanel panel, ObservableCollection<ToolRow> rows, List<PrivacyTraceItem> items)
        {
            var selected = rows.Where(r => r.Selected).Select(r => r.Source).OfType<PrivacyTraceItem>().ToList();
            if (selected.Count == 0)
            {
                panel.PanelStatus = Loc.Get("Status.NoSelection");
                return;
            }

            var report = await Task.Run(() => PrivacyTraceCleaner.Clean(selected));
            panel.PanelStatus = Loc.Get("Status.Removed") + $": {report.RemovedCount} | "
                + Loc.Get("Common.Error") + $": {report.FailedCount + report.ProtectedCount}";

            await ScanAsync(panel, rows, items);
        }
    }

    /// <summary>
    /// O rotulo do grupo de rastro, tirado do catalogo do nucleo.
    /// </summary>
    /// <param name="group">O grupo do rastro.</param>
    private static string PrivacyGroupLabel(PrivacyTraceGroup group)
    {
        var info = PrivacyTraceCleaner.Groups.FirstOrDefault(g => g.Group == group);
        return info is null ? group.ToString() : Loc.Get(info.LabelKey);
    }

    // ------------------------------------------------------------------
    // Fluxos alternativos (ADS)
    // ------------------------------------------------------------------

    /// <summary>
    /// Tela dos fluxos alternativos do NTFS (os "streams" ocultos que um
    /// arquivo pode ter).
    ///
    /// A varredura e a do nucleo
    /// (<c>Core/Discovery/AlternateDataStreamScanner.cs</c>): a tela escolhe
    /// a pasta e mostra o que o nucleo achou.
    /// </summary>
    public static FrameworkElement CreateAlternateDataStreams()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<ToolRow>();

        panel.Configure(
            Loc.Get("Module.AlternateDataStreams"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Tool.Ads.Stream"), Width = 200, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Common.Type"), Width = 110, BindingPath = "Kind" },
                new ColumnDef { Header = Loc.Get("Common.Path"), Width = 560, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Common.Size"), Width = 110, BindingPath = "Size" },
            },
            new[]
            {
                new CommandDef { AutomationId = "AdsScanFolder", Text = Loc.Get("Tool.Ads.ScanFolder"), Action = () => Escanear(false) },
                new CommandDef { AutomationId = "AdsScanFolderRecursive", Text = Loc.Get("Tool.Ads.ScanFolderRecursive"), Action = () => Escanear(true) },
            });

        panel.SetItems(rows);
        panel.PanelStatus = Loc.Get("Tool.Ads.ChooseFolder");
        return panel;

        void Escanear(bool recursivo)
        {
            var dialog = new OpenFileDialog
            {
                Title = Loc.Get("Tool.Ads.ScanFolder"),
                CheckFileExists = false,
                ValidateNames = false,
                FileName = "Selecionar pasta",
            };

            if (dialog.ShowDialog() != true) return;

            var pasta = System.IO.Path.GetDirectoryName(dialog.FileName);
            if (string.IsNullOrWhiteSpace(pasta)) return;

            panel.RunAsync(() => ScanAsync(pasta, recursivo));
        }

        async Task ScanAsync(string pasta, bool recursivo)
        {
            var streams = await Task.Run(() => AlternateDataStreamScanner.ScanFolder(pasta, recursivo));

            rows.Clear();
            foreach (var stream in streams)
            {
                rows.Add(new ToolRow
                {
                    Category = stream.Name,
                    Kind = stream.Type,
                    Path = stream.StreamPath,
                    Size = ByteFormatter.Format(stream.Size),
                    Source = stream,
                });
            }

            panel.PanelStatus = rows.Count == 0
                ? Loc.Get("Tool.Ads.Empty")
                : Loc.Get("Status.Done") + $" — {rows.Count} item(ns)";
        }
    }

    // ------------------------------------------------------------------
    // Ferramentas personalizadas
    // ------------------------------------------------------------------

    /// <summary>
    /// Tela das ferramentas personalizadas: as que o usuario cria com um
    /// nome e um comando, ao lado das ferramentas do catalogo do Windows.
    ///
    /// O registro e a fonte unica (<c>Core/Tools/CustomToolRegistry.cs</c>):
    /// a tela lista, acrescenta, renomeia, remove e executa o que o nucleo
    /// guarda em disco.
    /// </summary>
    public static FrameworkElement CreateCustomTools()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<ToolRow>();

        panel.Configure(
            Loc.Get("Module.CustomTools"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Common.Name"), Width = 220, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Tool.Autorun.Command"), Width = 260, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Tool.CustomTools.Arguments"), Width = 180, BindingPath = "Kind" },
                new ColumnDef { Header = Loc.Get("Common.Description"), Width = 240, BindingPath = "Size" },
            },
            new[]
            {
                new CommandDef { AutomationId = "CustomToolsAdd", Text = Loc.Get("Options.AddCustomTool"), Action = () => Adicionar(panel, rows) },
                new CommandDef { AutomationId = "CustomToolsRename", Text = Loc.Get("Options.Rename"), Action = () => Renomear(panel, rows) },
                new CommandDef { AutomationId = "CustomToolsRemove", Text = Loc.Get("Options.RemoveTool"), Action = () => Remover(panel, rows) },
                new CommandDef { AutomationId = "CustomToolsRun", Text = Loc.Get("Common.Run"), Action = () => Executar(panel) },
            });

        panel.SetItems(rows);
        Atualizar(panel, rows);
        return panel;

        static void Atualizar(ItemListPanel panel, ObservableCollection<ToolRow> rows)
        {
            rows.Clear();
            foreach (var tool in CustomToolRegistry.List())
            {
                rows.Add(new ToolRow
                {
                    Category = tool.Name,
                    Path = tool.Command,
                    Kind = tool.Arguments,
                    Size = tool.Description,
                    Source = tool,
                });
            }

            panel.PanelStatus = Loc.Get("Status.Done") + $" — {rows.Count} ferramenta(s)";
        }

        static void Adicionar(ItemListPanel panel, ObservableCollection<ToolRow> rows)
        {
            var campos = AskForFields(panel, Loc.Get("Options.AddCustomTool"),
                ("name", Loc.Get("Common.Name"), string.Empty),
                ("command", Loc.Get("Tool.Autorun.Command"), string.Empty),
                ("arguments", Loc.Get("Tool.CustomTools.Arguments"), string.Empty));

            if (campos is null) return;

            var status = CustomToolRegistry.Add(new CustomTool
            {
                Name = campos["name"],
                Command = campos["command"],
                Arguments = campos["arguments"],
            });

            panel.PanelStatus = status == CustomToolStatus.Ok
                ? Loc.Get("Tool.CustomTools.Added")
                : CustomToolStatusMessage(status);

            Atualizar(panel, rows);
        }

        static void Renomear(ItemListPanel panel, ObservableCollection<ToolRow> rows)
        {
            var atual = SelectedTool(panel);
            if (atual is null)
            {
                panel.PanelStatus = Loc.Get("Status.NoSelection");
                return;
            }

            var campos = AskForFields(panel, Loc.Get("Options.Rename"),
                ("name", Loc.Get("Common.Name"), atual.Name));

            if (campos is null) return;

            var status = CustomToolRegistry.Rename(atual.Name, campos["name"]);
            panel.PanelStatus = status == CustomToolStatus.Ok
                ? Loc.Get("Tool.CustomTools.Renamed")
                : CustomToolStatusMessage(status);

            Atualizar(panel, rows);
        }

        static void Remover(ItemListPanel panel, ObservableCollection<ToolRow> rows)
        {
            var atual = SelectedTool(panel);
            if (atual is null)
            {
                panel.PanelStatus = Loc.Get("Status.NoSelection");
                return;
            }

            var dono = Window.GetWindow(panel);
            var resposta = MessageBox.Show(dono,
                $"{Loc.Get("Options.RemoveTool")}: {atual.Name}?",
                Loc.Get("Options.RemoveTool"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (resposta != MessageBoxResult.Yes) return;

            var status = CustomToolRegistry.Remove(atual.Name);
            panel.PanelStatus = status == CustomToolStatus.Ok
                ? Loc.Get("Tool.CustomTools.Removed")
                : CustomToolStatusMessage(status);

            Atualizar(panel, rows);
        }

        static void Executar(ItemListPanel panel)
        {
            var atual = SelectedTool(panel);
            if (atual is null)
            {
                panel.PanelStatus = Loc.Get("Status.NoSelection");
                return;
            }

            var ok = CustomToolRegistry.Run(atual);
            panel.PanelStatus = ok ? Loc.Get("Status.Done") : Loc.Get("Status.Error");
        }
    }

    /// <summary>
    /// A ferramenta personalizada selecionada na lista (null quando nada
    /// foi selecionado).
    /// </summary>
    /// <param name="panel">O painel da tela.</param>
    private static CustomTool? SelectedTool(ItemListPanel panel)
    {
        foreach (var item in panel.SelectedItems)
        {
            if (item is ToolRow row && row.Source is CustomTool tool) return tool;
        }

        return null;
    }

    /// <summary>
    /// A mensagem de um recusa do registro de ferramentas personalizadas.
    /// </summary>
    /// <param name="status">O resultado da operacao.</param>
    private static string CustomToolStatusMessage(CustomToolStatus status) => status switch
    {
        CustomToolStatus.AlreadyExists => CustomToolRegistry.DuplicateNameMessage,
        CustomToolStatus.NotFound => Loc.Get("Tool.CustomTools.NotFound"),
        CustomToolStatus.InvalidName => Loc.Get("Tool.CustomTools.InvalidName"),
        CustomToolStatus.InvalidCommand => Loc.Get("Tool.CustomTools.InvalidCommand"),
        _ => Loc.Get("Common.Error"),
    };

    /// <summary>
    /// Abre uma janela pequena com os campos pedidos e devolve o que o
    /// usuario digitou (null quando cancelou).
    /// </summary>
    /// <param name="ownerElement">O controle que indica a janela dona.</param>
    /// <param name="title">O titulo da janela.</param>
    /// <param name="fields">Os campos: chave, rotulo e valor inicial.</param>
    private static Dictionary<string, string>? AskForFields(
        DependencyObject ownerElement,
        string title,
        params (string Key, string Label, string Value)[] fields)
    {
        var caixas = new Dictionary<string, TextBox>(StringComparer.OrdinalIgnoreCase);
        var painel = new StackPanel { Margin = new Thickness(16) };

        foreach (var field in fields)
        {
            painel.Children.Add(new TextBlock
            {
                Text = field.Label,
                Margin = new Thickness(0, 6, 0, 2),
            });

            var box = new TextBox { Text = field.Value, Margin = new Thickness(0, 0, 0, 4) };
            AutomationProperties.SetAutomationId(box, "CustomToolField" + field.Key);

            caixas[field.Key] = box;
            painel.Children.Add(box);
        }

        var ok = new Button
        {
            Content = Loc.Get("Common.Ok"),
            Width = 90,
            IsDefault = true,
            Margin = new Thickness(0, 12, 8, 0),
        };

        var cancelar = new Button
        {
            Content = Loc.Get("Common.Cancel"),
            Width = 90,
            IsCancel = true,
        };

        AutomationProperties.SetAutomationId(ok, "CustomToolOk");
        AutomationProperties.SetAutomationId(cancelar, "CustomToolCancel");

        var botoes = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        botoes.Children.Add(ok);
        botoes.Children.Add(cancelar);
        painel.Children.Add(botoes);

        var window = new Window
        {
            Title = title,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
            Content = painel,
            Owner = Window.GetWindow(ownerElement),
            Background = Application.Current?.TryFindResource("PanelBrush") as Brush ?? Brushes.White,
            FontFamily = Application.Current?.TryFindResource("UiFont") as FontFamily ?? new FontFamily("Segoe UI"),
        };

        AutomationProperties.SetAutomationId(window, "CustomToolDialog");

        ok.Click += (_, _) => window.DialogResult = true;

        if (window.ShowDialog() != true) return null;

        var resultado = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            resultado[field.Key] = (caixas[field.Key].Text ?? string.Empty).Trim();
        }

        return resultado;
    }

    /// <summary>
    /// Abre uma tela de ferramenta em uma janela propria.
    ///
    /// As ferramentas que ainda nao tem botao na barra lateral sao abertas
    /// por aqui: a janela e a moldura, a tela e o painel.
    /// </summary>
    /// <param name="owner">A janela dona (pode ser nula).</param>
    /// <param name="content">A tela da ferramenta.</param>
    /// <param name="title">O titulo da janela.</param>
    /// <param name="automationId">O identificador da janela para a automacao.</param>
    public static Window? ShowTool(Window? owner, FrameworkElement content, string title, string automationId)
    {
        var window = new Window
        {
            Title = title,
            Width = 920,
            Height = 580,
            WindowStartupLocation = owner is null
                ? WindowStartupLocation.CenterScreen
                : WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
            Content = content,
            Owner = owner,
            Background = Application.Current?.TryFindResource("PanelBrush") as Brush ?? Brushes.White,
            FontFamily = Application.Current?.TryFindResource("UiFont") as FontFamily ?? new FontFamily("Segoe UI"),
        };

        AutomationProperties.SetAutomationId(window, automationId);
        window.ShowDialog();

        return window;
    }
}
