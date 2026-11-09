using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Tools;
using Tranal.Uninstaller.Ui.Shared;

namespace Tranal.Uninstaller.Ui.Tools;

/// <summary>
/// Telas de manutencao: os programas que vieram com o Windows, as
/// instalacoes duplicadas, as impressoras, os dumps de falha e a limpeza do
/// navegador.
///
/// Todas usam o mesmo painel generico (<see cref="ItemListPanel"/>).
/// </summary>
public static class MaintenanceViews
{
    /// <summary>Cria a tela da ferramenta pelo id do catalogo.</summary>
    /// <param name="toolId">O id da ferramenta.</param>
    public static FrameworkElement Create(string toolId) => toolId switch
    {
        ToolCatalog.Bloatware => CreateBloatware(),
        ToolCatalog.Duplicates => CreateDuplicates(),
        ToolCatalog.Printers => CreatePrinters(),
        ToolCatalog.CrashDumps => CreateCrashDumps(),
        ToolCatalog.BrowserCleanup => CreateBrowserCleanup(),
        _ => new ItemListPanel(),
    };

    /// <summary>
    /// Linha exibida pelas telas de manutencao.
    /// </summary>
    private sealed class MaintenanceRow : INotifyPropertyChanged
    {
        public string Category { get; init; } = string.Empty;
        public string Kind { get; init; } = string.Empty;
        public string Path { get; init; } = string.Empty;
        public string Size { get; init; } = string.Empty;
        public string Detail { get; init; } = string.Empty;

        private bool _selected = true;

        /// <summary>True quando o item esta marcado.</summary>
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

        /// <summary>Item original do nucleo.</summary>
        public object Source { get; init; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    // ------------------------------------------------------------------
    // Programas que vieram com o Windows
    // ------------------------------------------------------------------

    /// <summary>
    /// Tela dos programas que vieram com o Windows e quase nunca sao usados.
    /// </summary>
    private static FrameworkElement CreateBloatware()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<MaintenanceRow>();

        panel.Configure(
            Loc.Get("Module.Bloatware"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Common.Name"), Width = 260, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Tool.Bloat.Category"), Width = 220, BindingPath = "Kind" },
                new ColumnDef { Header = Loc.Get("Common.Path"), Width = 420, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Common.Size"), Width = 110, BindingPath = "Size" },
            },
            new[]
            {
                new CommandDef { AutomationId = "BloatwareScan", Text = Loc.Get("Tool.Bloat.Scan"), IsPrimary = true, Action = () => Escanear() },
            });

        panel.SetItems(rows);
        panel.PanelStatus = Loc.Get("Tool.Bloat.Start");
        return panel;

        void Escanear()
        {
            panel.RunAsync(async () =>
            {
                var instalados = await Task.Run(() => ProgramScanner.Scan(new ProgramScanner.ScanOptions
                {
                    IncludeSystemComponents = false,
                    IncludeMsiProducts = false,
                }));

                var encontrados = await Task.Run(() => BundlewareDetector.Detect(instalados));

                rows.Clear();

                foreach (var entrada in encontrados)
                {
                    rows.Add(new MaintenanceRow
                    {
                        Category = entrada.Program.DisplayName,
                        Kind = entrada.Category.Name,
                        Path = entrada.Program.InstallLocation,
                        Size = entrada.Program.SizeBytes > 0 ? ByteFormatter.Format(entrada.Program.SizeBytes) : string.Empty,
                        Detail = entrada.Category.Reason,
                        Source = entrada,
                    });
                }

                panel.PanelStatus = rows.Count == 0
                    ? Loc.Get("Tool.Bloat.Empty")
                    : Loc.Get("Status.Done") + " — " + rows.Count + " item(ns)";
            });
        }
    }

    // ------------------------------------------------------------------
    // Instalacoes duplicadas
    // ------------------------------------------------------------------

    /// <summary>
    /// Tela das instalacoes duplicadas: o mesmo programa em dois lugares.
    /// </summary>
    private static FrameworkElement CreateDuplicates()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<MaintenanceRow>();

        panel.Configure(
            Loc.Get("Module.Duplicates"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Common.Name"), Width = 240, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Tool.Dup.Count"), Width = 90, BindingPath = "Kind" },
                new ColumnDef { Header = Loc.Get("Common.Path"), Width = 380, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Tool.Dup.Winner"), Width = 260, BindingPath = "Size" },
                new ColumnDef { Header = Loc.Get("Common.Reason"), Width = 200, BindingPath = "Detail" },
            },
            new[]
            {
                new CommandDef { AutomationId = "DuplicateScan", Text = Loc.Get("Tool.Dup.Scan"), IsPrimary = true, Action = () => Escanear() },
            });

        panel.SetItems(rows);
        panel.PanelStatus = Loc.Get("Tool.Dup.Start");
        return panel;

        void Escanear()
        {
            panel.RunAsync(async () =>
            {
                var instalados = await Task.Run(() => ProgramScanner.Scan(new ProgramScanner.ScanOptions
                {
                    IncludeSystemComponents = false,
                    IncludeMsiProducts = false,
                }));

                var grupos = await Task.Run(() => DuplicateInstallDetector.Detect(instalados));

                rows.Clear();

                foreach (var grupo in grupos)
                {
                    var vencedora = grupo.Winner?.InstallLocation ?? string.Empty;

                    foreach (var programa in grupo.Entries)
                    {
                        rows.Add(new MaintenanceRow
                        {
                            Category = grupo.Name,
                            Kind = grupo.Count.ToString(),
                            Path = programa.InstallLocation,
                            Size = vencedora,
                            Detail = grupo.Reason,
                            Source = programa,
                        });
                    }
                }

                panel.PanelStatus = rows.Count == 0
                    ? Loc.Get("Tool.Dup.Empty")
                    : Loc.Get("Status.Done") + " — " + grupos.Count + " grupo(s)";
            });
        }
    }

    // ------------------------------------------------------------------
    // Impressoras
    // ------------------------------------------------------------------

    /// <summary>
    /// Tela das impressoras e da fila de impressao.
    /// </summary>
    private static FrameworkElement CreatePrinters()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<MaintenanceRow>();

        panel.Configure(
            Loc.Get("Module.Printers"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Common.Name"), Width = 240, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Tool.Printer.Port"), Width = 130, BindingPath = "Kind" },
                new ColumnDef { Header = Loc.Get("Tool.Printer.Driver"), Width = 280, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Tool.Printer.Queue"), Width = 100, BindingPath = "Size" },
                new ColumnDef { Header = Loc.Get("Tool.Printer.Location"), Width = 220, BindingPath = "Detail" },
            },
            new[]
            {
                new CommandDef { AutomationId = "PrinterScan", Text = Loc.Get("Tool.Printer.Scan"), IsPrimary = true, Action = () => Escanear() },
            });

        panel.SetItems(rows);
        panel.PanelStatus = Loc.Get("Tool.Printer.Start");
        return panel;

        void Escanear()
        {
            panel.RunAsync(async () =>
            {
                var impressoras = await Task.Run(() => PrinterEnumerator.ListWithQueue());

                rows.Clear();

                foreach (var impressora in impressoras)
                {
                    // A impressora pode vir sem driver: o proprio Windows
                    // sabe qual e o driver dela.
                    var driver = impressora.Driver.Length > 0
                        ? impressora.Driver
                        : PrinterEnumerator.FindDriver(impressora.Name);

                    rows.Add(new MaintenanceRow
                    {
                        Category = impressora.Name + (impressora.IsDefault ? " *" : string.Empty),
                        Kind = impressora.Port,
                        Path = driver,
                        Size = impressora.QueuedJobs.ToString(),
                        Detail = impressora.Location,
                        Source = impressora,
                    });
                }

                panel.PanelStatus = rows.Count == 0
                    ? Loc.Get("Tool.Printer.Empty")
                    : Loc.Get("Status.Done") + " — " + rows.Count + " impressora(s)";
            });
        }
    }

    // ------------------------------------------------------------------
    // Dumps de falha
    // ------------------------------------------------------------------

    /// <summary>
    /// Tela dos dumps de falha do Windows.
    /// </summary>
    private static FrameworkElement CreateCrashDumps()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<MaintenanceRow>();

        panel.Configure(
            Loc.Get("Module.CrashDumps"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Common.Program"), Width = 190, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Tool.Dump.Crashes"), Width = 90, BindingPath = "Kind" },
                new ColumnDef { Header = Loc.Get("Common.Path"), Width = 420, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Common.Size"), Width = 110, BindingPath = "Size" },
                new ColumnDef { Header = Loc.Get("Common.Date"), Width = 140, BindingPath = "Detail" },
            },
            new[]
            {
                new CommandDef { AutomationId = "DumpScan", Text = Loc.Get("Tool.Dump.Scan"), Action = () => Escanear() },
                new CommandDef { AutomationId = "DumpClean", Text = Loc.Get("Tool.Dump.Clean"), IsPrimary = true, Action = () => Limpar() },
            });

        panel.SetItems(rows);
        panel.PanelStatus = Loc.Get("Tool.Dump.Start");
        return panel;

        void Escanear()
        {
            panel.RunAsync(async () =>
            {
                var dumps = await Task.Run(() => CrashDumpCollector.Collect());

                rows.Clear();

                foreach (var dump in dumps)
                {
                    rows.Add(new MaintenanceRow
                    {
                        Category = dump.Suspect,
                        Kind = dump.CrashCount > 0 ? dump.CrashCount.ToString() : string.Empty,
                        Path = dump.Path,
                        Size = ByteFormatter.Format(dump.SizeBytes),
                        Detail = dump.CreatedAt.ToString("d", System.Globalization.CultureInfo.CurrentCulture),
                        Source = dump,
                    });
                }

                var total = CrashDumpCollector.TotalSize();
                panel.PanelStatus = rows.Count == 0
                    ? Loc.Get("Tool.Dump.Empty")
                    : Loc.Get("Status.Done") + " — " + rows.Count + " dump(s), " + ByteFormatter.Format(total);
            });
        }

        void Limpar()
        {
            var marcados = rows.Where(r => r.Selected).Select(r => (CrashDump)r.Source).ToList();
            if (marcados.Count == 0)
            {
                panel.PanelStatus = Loc.Get("Tool.Dump.NothingSelected");
                return;
            }

            panel.RunAsync(async () =>
            {
                var relatorio = await Task.Run(() => CrashDumpCollector.Clear(marcados));

                panel.PanelStatus = Loc.Get("Status.Done") + " — "
                    + relatorio.Outcomes.Count(o => o.Removed) + " removido(s)";
            });
        }
    }

    // ------------------------------------------------------------------
    // Limpeza do navegador
    // ------------------------------------------------------------------

    /// <summary>
    /// Tela da limpeza do navegador: extensoes orfas e reset de padroes.
    /// </summary>
    private static FrameworkElement CreateBrowserCleanup()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<MaintenanceRow>();

        panel.Configure(
            Loc.Get("Module.BrowserCleanup"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Common.Name"), Width = 240, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Tool.Browser.Browser"), Width = 130, BindingPath = "Kind" },
                new ColumnDef { Header = Loc.Get("Common.Path"), Width = 460, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Common.Size"), Width = 110, BindingPath = "Size" },
            },
            new[]
            {
                new CommandDef { AutomationId = "BrowserScan", Text = Loc.Get("Tool.Browser.Scan"), Action = () => Escanear() },
                new CommandDef { AutomationId = "BrowserRemove", Text = Loc.Get("Tool.Browser.Remove"), IsPrimary = true, Action = () => Remover() },
                new CommandDef { AutomationId = "BrowserReset", Text = Loc.Get("Tool.Browser.Reset"), Action = () => Resetar() },
            });

        panel.SetItems(rows);
        panel.PanelStatus = Loc.Get("Tool.Browser.Start");
        return panel;

        void Escanear()
        {
            panel.RunAsync(async () =>
            {
                var orfas = await Task.Run(() => BrowserCleanup.FindOrphanExtensions());

                rows.Clear();

                foreach (var orfa in orfas)
                {
                    rows.Add(new MaintenanceRow
                    {
                        Category = string.IsNullOrWhiteSpace(orfa.Name) ? orfa.Id : orfa.Name,
                        Kind = orfa.Browser,
                        Path = orfa.Path,
                        Size = ByteFormatter.Format(orfa.SizeBytes),
                        Source = orfa,
                    });
                }

                panel.PanelStatus = rows.Count == 0
                    ? Loc.Get("Tool.Browser.Empty")
                    : Loc.Get("Status.Done") + " — " + rows.Count + " extensao(oes) orfa(s)";
            });
        }

        void Remover()
        {
            var marcadas = rows.Where(r => r.Selected).Select(r => (OrphanExtension)r.Source).ToList();
            if (marcadas.Count == 0)
            {
                panel.PanelStatus = Loc.Get("Tool.Browser.NothingSelected");
                return;
            }

            panel.RunAsync(async () =>
            {
                var resultado = await Task.Run(() => BrowserCleanup.RemoveOrphanExtensions(marcadas));

                panel.PanelStatus = resultado.Message;
            });
        }

        void Resetar()
        {
            panel.RunAsync(async () =>
            {
                var resultado = await Task.Run(() => BrowserCleanup.ResetDefaults(null));

                panel.PanelStatus = resultado.Message;
            });
        }
    }
}
