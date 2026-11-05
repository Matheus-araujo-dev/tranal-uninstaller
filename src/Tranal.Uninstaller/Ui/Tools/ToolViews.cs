using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Tools;
using Tranal.Uninstaller.Ui.Shared;

namespace Tranal.Uninstaller.Ui.Tools;

/// <summary>
/// Telas das 6 ferramentas do painel "Tools". Todas usam o mesmo painel generico
/// (<see cref="ItemListPanel"/>): o que muda entre elas sao as colunas, os
/// comandos e a fonte dos dados.
/// </summary>
public static class ToolViews
{
    /// <summary>Cria a tela da ferramenta pelo id usado na barra lateral.</summary>
    public static FrameworkElement Create(string toolId) => toolId switch
    {
        "AutorunManager" => CreateAutorun(),
        "JunkCleaner" => CreateJunk(),
        "WindowsTools" => CreateWindowsTools(),
        "HistoryCleaner" => CreateHistory(),
        "EvidenceRemover" => CreateEvidence(),
        "UnrecoverableDelete" => CreateUnrecoverable(),
        _ => new ItemListPanel(),
    };

    /// <summary>Linha uniforme exibida pelas ferramentas de limpeza.</summary>
    private sealed class ToolRow
    {
        public string Category { get; init; } = string.Empty;
        public string Kind { get; init; } = string.Empty;
        public string Path { get; init; } = string.Empty;
        public string Size { get; init; } = string.Empty;
        public bool Selected { get; set; } = true;

        /// <summary>Item original do nucleo (a remocao usa o item canonico).</summary>
        public object Source { get; init; } = new();
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
    private static FrameworkElement CreateHistory()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<ToolRow>();
        var items = new List<HistoryItem>();

        panel.Configure(
            Loc.Get("Module.HistoryCleaner"),
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

        static async Task ScanAsync(ItemListPanel panel, ObservableCollection<ToolRow> rows, List<HistoryItem> items)
        {
            var found = await Task.Run(HistoryCleaner.Scan);

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

        static async Task ClearAsync(ItemListPanel panel, ObservableCollection<ToolRow> rows, List<HistoryItem> items, bool all)
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
}
