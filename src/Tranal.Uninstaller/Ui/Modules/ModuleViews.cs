using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Win32;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Ui.Shared;

namespace Tranal.Uninstaller.Ui.Modules;

/// <summary>
/// Telas dos 5 modulos de dados. Usam o mesmo painel generico
/// (<see cref="ItemListPanel"/>); o Modo Cacador tem uma tela propria porque
/// recebe arrastar-e-soltar.
/// </summary>
public static class ModuleViews
{
    /// <summary>Cria a tela do modulo pelo id usado na barra lateral.</summary>
    public static FrameworkElement Create(string moduleId, MainWindow host) => moduleId switch
    {
        "TracedPrograms" => CreateTraced(),
        "WindowsApps" => CreateWindowsApps(),
        "BrowserExtensions" => CreateBrowserExtensions(),
        "LogsDatabase" => CreateLogsDatabase(),
        "HunterMode" => new HunterView(host),
        _ => new ItemListPanel(),
    };

    // ------------------------------------------------------------------
    // Programas Monitorados
    // ------------------------------------------------------------------
    private static FrameworkElement CreateTraced()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<TraceRow>();

        panel.Configure(
            Loc.Get("Module.TracedPrograms"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Common.Name"), Width = 280, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Common.Date"), Width = 140, BindingPath = "Kind" },
                new ColumnDef { Header = Loc.Get("Mod.Traced.Files"), Width = 90, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Mod.Traced.Folders"), Width = 90, BindingPath = "Size" },
                new ColumnDef { Header = Loc.Get("Mod.Traced.Keys"), Width = 90, BindingPath = "Extra" },
                new ColumnDef { Header = Loc.Get("Mod.Traced.Log"), Width = 380, BindingPath = "Detail" },
            },
            new[]
            {
                new CommandDef { AutomationId = "TracedRefresh", Text = Loc.Get("Common.Refresh"), Action = () => panel.RunAsync(() => ReloadAsync(panel, rows)) },
                new CommandDef { AutomationId = "TracedInstall", Text = Loc.Get("Mod.Traced.Install"), Action = () => Install(panel, rows) },
                new CommandDef { AutomationId = "TracedUninstall", Text = Loc.Get("Mod.Traced.Uninstall"), Action = () => panel.RunAsync(() => UninstallAsync(panel, rows)) },
            });

        panel.SetItems(rows);
        panel.RunAsync(() => ReloadAsync(panel, rows));
        return panel;

        static async Task ReloadAsync(ItemListPanel panel, ObservableCollection<TraceRow> rows)
        {
            var installs = await Task.Run(TracedPrograms.List);

            rows.Clear();
            foreach (var install in installs) rows.Add(new TraceRow(install));

            panel.PanelStatus = rows.Count == 0
                ? Loc.Get("Mod.Traced.Empty")
                : Loc.Get("Status.Done") + $" — {rows.Count} log(s)";
        }

        static void Install(ItemListPanel panel, ObservableCollection<TraceRow> rows)
        {
            var dialog = new OpenFileDialog
            {
                Title = Loc.Get("Mod.Traced.Install"),
                Filter = Loc.Get("Forced.BrowseFilter"),
            };
            if (dialog.ShowDialog() != true) return;

            var name = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
            panel.RunAsync(async () =>
            {
                var install = await Task.Run(() => TracedPrograms.Install(dialog.FileName, name));
                panel.PanelStatus = install is null
                    ? Loc.Get("Status.Error")
                    : Loc.Get("Status.Done") + $" — {install.ProgramName}";

                await ReloadAsync(panel, rows);
            });
        }

        static async Task UninstallAsync(ItemListPanel panel, ObservableCollection<TraceRow> rows)
        {
            var target = Selected(rows, panel);
            if (target is null)
            {
                panel.PanelStatus = Loc.Get("Status.NoSelection");
                return;
            }

            var report = await Task.Run(() => TracedPrograms.Uninstall(target.Source, UninstallMode.Advanced));
            panel.PanelStatus = Loc.Get("Status.Removed") + $": {report.RemovedCount} | "
                + Loc.Get("Common.Error") + $": {report.FailedCount + report.ProtectedCount}";

            await ReloadAsync(panel, rows);
        }
    }

    // ------------------------------------------------------------------
    // Windows Apps
    // ------------------------------------------------------------------
    private static FrameworkElement CreateWindowsApps()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<AppRow>();

        panel.Configure(
            Loc.Get("Module.WindowsApps"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Common.Name"), Width = 280, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Common.Version"), Width = 120, BindingPath = "Kind" },
                new ColumnDef { Header = Loc.Get("Common.Publisher"), Width = 220, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Mod.Apps.Package"), Width = 420, BindingPath = "Size" },
            },
            new[]
            {
                new CommandDef { AutomationId = "WindowsAppsRefresh", Text = Loc.Get("Common.Refresh"), Action = () => panel.RunAsync(() => ReloadAsync(panel, rows)) },
                new CommandDef { AutomationId = "WindowsAppsUninstall", Text = Loc.Get("Cmd.Uninstall"), Action = () => panel.RunAsync(() => UninstallAsync(panel, rows)) },
            });

        panel.SetItems(rows);
        panel.RunAsync(() => ReloadAsync(panel, rows));
        return panel;

        static async Task ReloadAsync(ItemListPanel panel, ObservableCollection<AppRow> rows)
        {
            var apps = await Task.Run(WindowsApps.List);

            rows.Clear();
            foreach (var app in apps) rows.Add(new AppRow(app));

            panel.PanelStatus = rows.Count == 0
                ? Loc.Get("Mod.Apps.Empty")
                : Loc.Get("Status.Done") + $" — {rows.Count} app(s)";
        }

        static async Task UninstallAsync(ItemListPanel panel, ObservableCollection<AppRow> rows)
        {
            var target = Selected(rows, panel);
            if (target is null)
            {
                panel.PanelStatus = Loc.Get("Status.NoSelection");
                return;
            }

            var ok = await Task.Run(() => WindowsApps.Uninstall(target.Source));
            panel.PanelStatus = ok ? Loc.Get("Status.Done") : Loc.Get("Status.Error");

            await ReloadAsync(panel, rows);
        }
    }

    // ------------------------------------------------------------------
    // Extensoes do navegador
    // ------------------------------------------------------------------
    private static FrameworkElement CreateBrowserExtensions()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<ExtensionRow>();

        panel.Configure(
            Loc.Get("Module.BrowserExtensions"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Mod.Ext.Extension"), Width = 280, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Mod.Ext.Browser"), Width = 160, BindingPath = "Kind" },
                new ColumnDef { Header = Loc.Get("Common.Version"), Width = 120, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Mod.Ext.Profile"), Width = 200, BindingPath = "Size" },
                new ColumnDef { Header = Loc.Get("Tool.Autorun.State"), Width = 100, BindingPath = "Extra" },
            },
            new[]
            {
                new CommandDef { AutomationId = "ExtensionsRefresh", Text = Loc.Get("Common.Refresh"), Action = () => panel.RunAsync(() => ReloadAsync(panel, rows)) },
                new CommandDef { AutomationId = "ExtensionsRemove", Text = Loc.Get("Common.Remove"), Action = () => panel.RunAsync(() => RemoveAsync(panel, rows)) },
            });

        panel.SetItems(rows);
        panel.RunAsync(() => ReloadAsync(panel, rows));
        return panel;

        static async Task ReloadAsync(ItemListPanel panel, ObservableCollection<ExtensionRow> rows)
        {
            var extensions = await Task.Run(BrowserExtensions.List);

            rows.Clear();
            foreach (var extension in extensions) rows.Add(new ExtensionRow(extension));

            panel.PanelStatus = rows.Count == 0
                ? Loc.Get("Mod.Ext.Empty")
                : Loc.Get("Status.Done") + $" — {rows.Count} extensão(ões)";
        }

        static async Task RemoveAsync(ItemListPanel panel, ObservableCollection<ExtensionRow> rows)
        {
            var target = Selected(rows, panel);
            if (target is null)
            {
                panel.PanelStatus = Loc.Get("Status.NoSelection");
                return;
            }

            var ok = await Task.Run(() => BrowserExtensions.Remove(target.Source));
            panel.PanelStatus = ok ? Loc.Get("Status.Done") : Loc.Get("Status.Error");

            await ReloadAsync(panel, rows);
        }
    }

    // ------------------------------------------------------------------
    // Base de dados de logs
    // ------------------------------------------------------------------
    private static FrameworkElement CreateLogsDatabase()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<TraceRow>();

        panel.Configure(
            Loc.Get("Module.LogsDatabase"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Common.Name"), Width = 280, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Common.Date"), Width = 140, BindingPath = "Kind" },
                new ColumnDef { Header = Loc.Get("Mod.Traced.Files"), Width = 90, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Mod.Traced.Folders"), Width = 90, BindingPath = "Size" },
                new ColumnDef { Header = Loc.Get("Mod.Traced.Keys"), Width = 90, BindingPath = "Extra" },
                new ColumnDef { Header = Loc.Get("Mod.Traced.Log"), Width = 380, BindingPath = "Detail" },
            },
            new[]
            {
                new CommandDef { AutomationId = "LogsRefresh", Text = Loc.Get("Common.Refresh"), Action = () => panel.RunAsync(() => ReloadAsync(panel, rows)) },
                new CommandDef { AutomationId = "LogsImport", Text = Loc.Get("Mod.Logs.Import"), Action = () => Import(panel, rows) },
                new CommandDef { AutomationId = "LogsExport", Text = Loc.Get("Mod.Logs.Export"), Action = () => Export(rows, panel) },
                new CommandDef { AutomationId = "LogsDelete", Text = Loc.Get("Mod.Logs.Delete"), Action = () => panel.RunAsync(() => DeleteAsync(panel, rows)) },
            });

        panel.SetItems(rows);
        panel.RunAsync(() => ReloadAsync(panel, rows));
        return panel;

        static async Task ReloadAsync(ItemListPanel panel, ObservableCollection<TraceRow> rows)
        {
            var logs = await Task.Run(TraceStore.List);

            rows.Clear();
            foreach (var log in logs) rows.Add(new TraceRow(log));

            panel.PanelStatus = rows.Count == 0
                ? Loc.Get("Mod.Logs.Empty")
                : Loc.Get("Status.Done") + $" — {rows.Count} log(s)";
        }

        static void Import(ItemListPanel panel, ObservableCollection<TraceRow> rows)
        {
            var dialog = new OpenFileDialog
            {
                Title = Loc.Get("Mod.Logs.Import"),
                Filter = "Tranal log (*.json)|*.json|Todos os arquivos (*.*)|*.*",
            };
            if (dialog.ShowDialog() != true) return;

            panel.RunAsync(async () =>
            {
                var imported = await Task.Run(() => TraceStore.Import(dialog.FileName));
                panel.PanelStatus = imported is null
                    ? Loc.Get("Status.Error")
                    : Loc.Get("Status.Done") + $" — {imported.ProgramName}";

                await ReloadAsync(panel, rows);
            });
        }

        static void Export(ObservableCollection<TraceRow> rows, ItemListPanel panel)
        {
            var target = Selected(rows, panel);
            if (target is null)
            {
                panel.PanelStatus = Loc.Get("Status.NoSelection");
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = Loc.Get("Mod.Logs.Export"),
                Filter = "Tranal log (*.json)|*.json",
                FileName = System.IO.Path.GetFileName(target.Source.LogPath),
            };
            if (dialog.ShowDialog() != true) return;

            var path = TraceStore.Export(target.Source, dialog.FileName);
            panel.PanelStatus = Loc.Get("Status.Exported") + $": {path}";
        }

        static async Task DeleteAsync(ItemListPanel panel, ObservableCollection<TraceRow> rows)
        {
            var target = Selected(rows, panel);
            if (target is null)
            {
                panel.PanelStatus = Loc.Get("Status.NoSelection");
                return;
            }

            var ok = await Task.Run(() => TraceStore.Delete(target.Source));
            panel.PanelStatus = ok ? Loc.Get("Status.Done") : Loc.Get("Status.Error");

            await ReloadAsync(panel, rows);
        }
    }

    // ------------------------------------------------------------------
    // Linhas dos modulos
    // ------------------------------------------------------------------
    private sealed class TraceRow
    {
        public TraceRow(TracedInstall install)
        {
            Source = install;
            Category = install.ProgramName;
            Kind = install.Timestamp.ToString("dd/MM/yyyy HH:mm");
            Path = install.FileCount.ToString();
            Size = install.FolderCount.ToString();
            Extra = install.RegistryKeyCount.ToString();
            Detail = install.LogPath;
        }

        public TracedInstall Source { get; }
        public string Category { get; }
        public string Kind { get; }
        public string Path { get; }
        public string Size { get; }
        public string Extra { get; }
        public string Detail { get; }
    }

    private sealed class AppRow
    {
        public AppRow(WindowsApp app)
        {
            Source = app;
            Category = app.Name;
            Kind = app.Version;
            Path = app.Publisher;
            Size = app.PackageFullName;
        }

        public WindowsApp Source { get; }
        public string Category { get; }
        public string Kind { get; }
        public string Path { get; }
        public string Size { get; }
    }

    private sealed class ExtensionRow
    {
        public ExtensionRow(BrowserExtension extension)
        {
            Source = extension;
            Category = extension.Name;
            Kind = extension.BrowserName;
            Path = extension.Version;
            Size = extension.Profile;
            Extra = extension.IsEnabled ? Loc.Get("Tool.Autorun.Enabled") : Loc.Get("Tool.Autorun.Disabled");
        }

        public BrowserExtension Source { get; }
        public string Category { get; }
        public string Kind { get; }
        public string Path { get; }
        public string Size { get; }
        public string Extra { get; }
    }

    /// <summary>Primeiro item selecionado na lista do painel.</summary>
    private static T? Selected<T>(ObservableCollection<T> rows, ItemListPanel panel) where T : class
    {
        foreach (var item in panel.SelectedItems)
            if (item is T typed) return typed;

        return null;
    }
}
