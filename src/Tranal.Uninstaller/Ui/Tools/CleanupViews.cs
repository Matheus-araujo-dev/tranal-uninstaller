using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Automation;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Tools;
using Tranal.Uninstaller.Ui.Shared;

namespace Tranal.Uninstaller.Ui.Tools;

/// <summary>
/// Telas das ferramentas de limpeza e de diagnostico que chegaram depois:
/// destravador de arquivos, limpador de internet, Drive Cleaner, conserto de
/// atalhos, limpeza de MRU e componentes compartilhados.
///
/// Todas usam o mesmo painel generico (<see cref="ItemListPanel"/>): o que
/// muda entre elas sao as colunas, os comandos e a fonte dos dados.
/// </summary>
public static class CleanupViews
{
    /// <summary>Cria a tela da ferramenta pelo id do catalogo.</summary>
    /// <param name="toolId">O id da ferramenta.</param>
    public static FrameworkElement Create(string toolId) => toolId switch
    {
        ToolCatalog.Unlocker => CreateUnlocker(),
        ToolCatalog.InternetCleaner => CreateInternetCleaner(),
        ToolCatalog.DriveCleaner => CreateDriveCleaner(),
        ToolCatalog.ShortcutFixer => CreateShortcutFixer(),
        ToolCatalog.MruCleaner => CreateMruCleaner(),
        ToolCatalog.SharedDlls => CreateSharedDlls(),
        ToolCatalog.RecycleBin => CreateRecycleBin(),
        _ => new ItemListPanel(),
    };

    /// <summary>
    /// Linha exibida pelas telas de limpeza.
    /// </summary>
    private sealed class CleanupRow : INotifyPropertyChanged
    {
        public string Category { get; init; } = string.Empty;
        public string Kind { get; init; } = string.Empty;
        public string Path { get; init; } = string.Empty;
        public string Size { get; init; } = string.Empty;
        public string Detail { get; init; } = string.Empty;

        private bool _selected = true;

        /// <summary>True quando o item esta marcado para a acao.</summary>
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

        /// <summary>Item original do nucleo (a acao usa o item canonico).</summary>
        public object Source { get; init; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>
    /// Os programas instalados, usados pelas telas que precisam ligar um item
    /// do sistema a um programa (atalhos, componentes compartilhados).
    /// </summary>
    private static IReadOnlyList<InstalledProgram> InstalledPrograms() =>
        ProgramScanner.Scan(new ProgramScanner.ScanOptions
        {
            IncludeSystemComponents = false,
            IncludeMsiProducts = false,
        });

    // ------------------------------------------------------------------
    // Destravador de arquivos
    // ------------------------------------------------------------------

    /// <summary>
    /// Tela do destravador: mostra quem esta segurando um arquivo e oferece
    /// as acoes do ashampoo Uninstaller (encerrar o processo, reiniciar o
    /// Explorer e desregistrar a DLL).
    /// </summary>
    private static FrameworkElement CreateUnlocker()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<CleanupRow>();

        panel.Configure(
            Loc.Get("Module.Unlocker"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Common.Name"), Width = 220, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Common.Type"), Width = 110, BindingPath = "Kind" },
                new ColumnDef { Header = Loc.Get("Common.Path"), Width = 520, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Common.Size"), Width = 110, BindingPath = "Size" },
            },
            new[]
            {
                new CommandDef { AutomationId = "UnlockerScan", Text = Loc.Get("Tool.Unlocker.Scan"), Action = () => Escanear() },
                new CommandDef { AutomationId = "UnlockerUnlock", Text = Loc.Get("Tool.Unlocker.Unlock"), IsPrimary = true, Action = () => Destravar(false) },
                new CommandDef { AutomationId = "UnlockerUnlockExplorer", Text = Loc.Get("Tool.Unlocker.UnlockExplorer"), Action = () => Destravar(true) },
            });

        panel.SetItems(rows);
        panel.PanelStatus = Loc.Get("Tool.Unlocker.ChooseFile");
        return panel;

        void Escanear()
        {
            var dialog = new OpenFileDialog
            {
                Title = Loc.Get("Tool.Unlocker.Scan"),
                CheckFileExists = false,
                ValidateNames = false,
            };

            if (dialog.ShowDialog() != true) return;

            var arquivo = dialog.FileName;
            panel.RunAsync(async () =>
            {
                var item = await Task.Run(() => Unlocker.Scan(arquivo));

                rows.Clear();

                if (item is null)
                {
                    panel.PanelStatus = Loc.Get("Tool.Unlocker.NotLocked");
                    return;
                }

                var quem = item.Lockers.Count == 0
                    ? Loc.Get("Tool.Unlocker.UnknownLocker")
                    : string.Join(", ", item.Lockers.Select(p => p.ProcessName + " (" + p.ProcessId + ")"));

                rows.Add(new CleanupRow
                {
                    Category = Path.GetFileName(item.Path),
                    Kind = item.IsDirectory ? "Pasta" : "Arquivo",
                    Path = item.Path,
                    Size = ByteFormatter.Format(item.SizeBytes),
                    Detail = quem,
                    Source = item,
                });

                panel.PanelStatus = Loc.Get("Tool.Unlocker.LockedBy") + ": " + quem;
            });
        }

        void Destravar(bool reiniciarExplorer)
        {
            var item = rows.FirstOrDefault();
            if (item is null)
            {
                panel.PanelStatus = Loc.Get("Tool.Unlocker.NothingSelected");
                return;
            }

            var caminho = item.Path;
            panel.RunAsync(async () =>
            {
                var resultado = await Task.Run(() => Unlocker.Unlock(caminho, reiniciarExplorer, reiniciarExplorer));
                panel.PanelStatus = resultado.Message;
            });
        }
    }

    // ------------------------------------------------------------------
    // Limpador de internet
    // ------------------------------------------------------------------

    /// <summary>
    /// Tela do limpador de internet: cookies, historico, cache, sessoes e
    /// formularios, com a lista de excecao ("cookies seguros").
    /// </summary>
    private static FrameworkElement CreateInternetCleaner()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<CleanupRow>();

        panel.Configure(
            Loc.Get("Module.InternetCleaner"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Common.Name"), Width = 140, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Tool.Internet.Kind"), Width = 130, BindingPath = "Kind" },
                new ColumnDef { Header = Loc.Get("Common.Path"), Width = 520, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Common.Size"), Width = 110, BindingPath = "Size" },
            },
            new[]
            {
                new CommandDef { AutomationId = "InternetScan", Text = Loc.Get("Tool.Internet.Scan"), Action = () => Escanear() },
                new CommandDef { AutomationId = "InternetClean", Text = Loc.Get("Tool.Internet.Clean"), IsPrimary = true, Action = () => Limpar() },
                new CommandDef { AutomationId = "InternetException", Text = Loc.Get("Tool.Internet.Exception"), Action = () => Excecao() },
            });

        panel.SetItems(rows);
        panel.PanelStatus = Loc.Get("Tool.Internet.Start");
        return panel;

        void Escanear()
        {
            panel.RunAsync(async () =>
            {
                var itens = await Task.Run(() => InternetCleaner.Scan());

                rows.Clear();

                foreach (var item in itens)
                {
                    rows.Add(new CleanupRow
                    {
                        Category = item.Browser,
                        Kind = item.Kind,
                        Path = item.Path,
                        Size = ByteFormatter.Format(item.SizeBytes),
                        Detail = item.IsProtected ? string.Join(", ", item.ExceptedHosts) : string.Empty,
                        Source = item,
                    });
                }

                panel.PanelStatus = rows.Count == 0
                    ? Loc.Get("Tool.Internet.Empty")
                    : Loc.Get("Status.Done") + " — " + rows.Count + " item(ns)";
            });
        }

        void Limpar()
        {
            var marcados = rows.Where(r => r.Selected).Select(r => (InternetCleanerItem)r.Source).ToList();
            if (marcados.Count == 0)
            {
                panel.PanelStatus = Loc.Get("Tool.Internet.NothingSelected");
                return;
            }

            // O navegador aberto segura os proprios arquivos: quem limpa sem
            // saber disso descobre que parte do que era para apagar ficou
            // para tras. O aviso nao impede: quem decide e o usuario.
            var avisos = OperationGuards.NavegadoresRodando();
            if (avisos.Count > 0)
            {
                var texto = string.Join(Environment.NewLine, avisos.Select(a => a.Message));
                if (!UserMessages.Warning(null, texto + Environment.NewLine + Environment.NewLine + Loc.Get("Guard.ContinueAnyway")))
                {
                    return;
                }
            }

            panel.RunAsync(async () =>
            {
                var relatorio = await Task.Run(() => InternetCleaner.Clear(marcados));

                panel.PanelStatus = Loc.Get("Status.Done") + " — "
                    + relatorio.Outcomes.Count(o => o.Removed) + " removido(s), "
                    + relatorio.Outcomes.Count(o => o.Status == RemovalStatus.Deferred) + " adiado(s)";
            });
        }

        void Excecao()
        {
            var entrada = Microsoft.VisualBasic.Interaction.InputBox(
                Loc.Get("Tool.Internet.ExceptionPrompt"),
                Loc.Get("Tool.Internet.Exception"),
                string.Empty);

            if (string.IsNullOrWhiteSpace(entrada)) return;

            var ok = InternetCleaner.AddException(entrada);
            panel.PanelStatus = ok
                ? Loc.Get("Tool.Internet.ExceptionAdded") + ": " + entrada
                : Loc.Get("Tool.Internet.ExceptionExists");
        }
    }

    // ------------------------------------------------------------------
    // Drive Cleaner
    // ------------------------------------------------------------------

    /// <summary>
    /// Tela do Drive Cleaner: arquivos desnecessarios agrupados por
    /// categoria, com o relatorio em HTML.
    /// </summary>
    private static FrameworkElement CreateDriveCleaner()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<CleanupRow>();

        panel.Configure(
            Loc.Get("Module.DriveCleaner"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Tool.Drive.Category"), Width = 220, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Common.Type"), Width = 110, BindingPath = "Kind" },
                new ColumnDef { Header = Loc.Get("Common.Path"), Width = 520, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Common.Size"), Width = 110, BindingPath = "Size" },
            },
            new[]
            {
                new CommandDef { AutomationId = "DriveScan", Text = Loc.Get("Tool.Drive.Scan"), Action = () => Escanear() },
                new CommandDef { AutomationId = "DriveClean", Text = Loc.Get("Tool.Drive.Clean"), IsPrimary = true, Action = () => Limpar() },
                new CommandDef { AutomationId = "DriveReport", Text = Loc.Get("Tool.Drive.Report"), Action = () => Relatorio() },
            });

        panel.SetItems(rows);
        panel.PanelStatus = Loc.Get("Tool.Drive.Start");
        return panel;

        void Escanear()
        {
            panel.RunAsync(async () =>
            {
                var itens = await Task.Run(() => DriveCleaner.Scan());

                rows.Clear();

                foreach (var item in itens)
                {
                    rows.Add(new CleanupRow
                    {
                        Category = item.Category,
                        Kind = item.Kind,
                        Path = item.Path,
                        Size = ByteFormatter.Format(item.SizeBytes),
                        Source = item,
                    });
                }

                var total = DriveCleaner.Summary(itens).Sum(p => p.Bytes);
                panel.PanelStatus = rows.Count == 0
                    ? Loc.Get("Tool.Drive.Empty")
                    : Loc.Get("Status.Done") + " — " + rows.Count + " item(ns), " + ByteFormatter.Format(total);
            });
        }

        void Limpar()
        {
            var selecionados = rows.Where(r => r.Selected).Select(r => (DriveCleanerItem)r.Source).ToList();
            if (selecionados.Count == 0)
            {
                panel.PanelStatus = Loc.Get("Tool.Drive.NothingSelected");
                return;
            }

            panel.RunAsync(async () =>
            {
                var relatorio = await Task.Run(() => DriveCleaner.Clear(selecionados));

                panel.PanelStatus = Loc.Get("Status.Done") + " — "
                    + relatorio.Outcomes.Count(o => o.Removed) + " removido(s), "
                    + relatorio.Outcomes.Count(o => o.Status == RemovalStatus.Deferred) + " adiado(s)";
            });
        }

        void Relatorio()
        {
            var dialog = new SaveFileDialog
            {
                Title = Loc.Get("Tool.Drive.Report"),
                Filter = "HTML (*.html)|*.html",
                FileName = "drive-cleaner.html",
            };

            if (dialog.ShowDialog() != true) return;

            var itens = rows.Select(r => (DriveCleanerItem)r.Source).ToList();
            DriveCleaner.WriteReport(dialog.FileName, itens, null);
            panel.PanelStatus = Loc.Get("Tool.Drive.ReportSaved") + ": " + dialog.FileName;
        }
    }

    // ------------------------------------------------------------------
    // Atalhos quebrados
    // ------------------------------------------------------------------

    /// <summary>
    /// Tela do conserto de atalhos: varre o Menu Iniciar e a Area de Trabalho
    /// e aponta os atalhos cujo alvo nao existe mais.
    /// </summary>
    private static FrameworkElement CreateShortcutFixer()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<CleanupRow>();

        panel.Configure(
            Loc.Get("Module.ShortcutFixer"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Common.Name"), Width = 220, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Tool.Shortcut.Kind"), Width = 140, BindingPath = "Kind" },
                new ColumnDef { Header = Loc.Get("Common.Path"), Width = 480, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Tool.Shortcut.Location"), Width = 160, BindingPath = "Size" },
            },
            new[]
            {
                new CommandDef { AutomationId = "ShortcutScan", Text = Loc.Get("Tool.Shortcut.Scan"), Action = () => Escanear() },
                new CommandDef { AutomationId = "ShortcutRepair", Text = Loc.Get("Tool.Shortcut.Repair"), IsPrimary = true, Action = () => Reparar(false) },
                new CommandDef { AutomationId = "ShortcutRemove", Text = Loc.Get("Tool.Shortcut.Remove"), Action = () => Reparar(true) },
            });

        panel.SetItems(rows);
        panel.PanelStatus = Loc.Get("Tool.Shortcut.Start");
        return panel;

        void Escanear()
        {
            panel.RunAsync(async () =>
            {
                var programas = await Task.Run(InstalledPrograms);
                var problemas = await Task.Run(() => ShortcutFixer.Scan(programas));

                rows.Clear();

                foreach (var problema in problemas)
                {
                    rows.Add(new CleanupRow
                    {
                        Category = problema.Name,
                        Kind = problema.Kind,
                        Path = problema.ShortcutPath,
                        Size = problema.Location,
                        Detail = problema.RepairTarget,
                        Source = problema,
                    });
                }

                panel.PanelStatus = rows.Count == 0
                    ? Loc.Get("Tool.Shortcut.Empty")
                    : Loc.Get("Status.Done") + " — " + rows.Count + " atalho(s)";
            });
        }

        void Reparar(bool remover)
        {
            var problemas = rows.Where(r => r.Selected).Select(r => (ShortcutIssue)r.Source).ToList();
            if (problemas.Count == 0)
            {
                panel.PanelStatus = Loc.Get("Tool.Shortcut.NothingSelected");
                return;
            }

            var reparados = 0;
            var removidos = 0;

            foreach (var problema in problemas)
            {
                var resultado = ShortcutFixer.Repair(problema, remover);
                if (resultado.Repaired) reparados++;
                if (resultado.Removed) removidos++;
            }

            if (remover && reparados == 0 && removidos == 0)
            {
                var relatorio = ShortcutFixer.Remove(problemas);
                panel.PanelStatus = Loc.Get("Status.Done") + " — "
                    + relatorio.Outcomes.Count(o => o.Removed) + " removido(s)";
                return;
            }

            panel.PanelStatus = Loc.Get("Status.Done") + " — " + reparados + " reparado(s), " + removidos + " removido(s)";
        }
    }

    // ------------------------------------------------------------------
    // Cache MRU
    // ------------------------------------------------------------------

    /// <summary>
    /// Tela da limpeza do cache MRU do Windows.
    /// </summary>
    private static FrameworkElement CreateMruCleaner()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<CleanupRow>();

        panel.Configure(
            Loc.Get("Module.MruCleaner"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Tool.Mru.Key"), Width = 420, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Tool.Mru.Value"), Width = 140, BindingPath = "Kind" },
                new ColumnDef { Header = Loc.Get("Common.Path"), Width = 420, BindingPath = "Path" },
            },
            new[]
            {
                new CommandDef { AutomationId = "MruScan", Text = Loc.Get("Tool.Mru.Scan"), Action = () => Escanear() },
                new CommandDef { AutomationId = "MruClean", Text = Loc.Get("Tool.Mru.Clean"), IsPrimary = true, Action = () => Limpar() },
            });

        panel.SetItems(rows);
        panel.PanelStatus = Loc.Get("Tool.Mru.Start");
        return panel;

        void Escanear()
        {
            panel.RunAsync(async () =>
            {
                var itens = await Task.Run(() => MruCleaner.Scan());

                rows.Clear();

                foreach (var item in itens)
                {
                    rows.Add(new CleanupRow
                    {
                        Category = item.RegistryPath,
                        Kind = item.ValueName,
                        Path = item.Content,
                        Source = item,
                    });
                }

                panel.PanelStatus = rows.Count == 0
                    ? Loc.Get("Tool.Mru.Empty")
                    : Loc.Get("Status.Done") + " — " + rows.Count + " item(ns)";
            });
        }

        void Limpar()
        {
            var marcados = rows.Where(r => r.Selected).Select(r => (MruItem)r.Source).ToList();
            if (marcados.Count == 0)
            {
                panel.PanelStatus = Loc.Get("Tool.Mru.NothingSelected");
                return;
            }

            panel.RunAsync(async () =>
            {
                var relatorio = await Task.Run(() => MruCleaner.Clear(marcados));

                panel.PanelStatus = Loc.Get("Status.Done") + " — "
                    + relatorio.Outcomes.Count(o => o.Removed) + " removido(s)";
            });
        }
    }

    // ------------------------------------------------------------------
    // Componentes compartilhados
    // ------------------------------------------------------------------

    /// <summary>
    /// Tela do catalogo de componentes compartilhados: as DLLs que o Windows
    /// registra e quantos programas ainda dependem de cada uma.
    /// </summary>
    private static FrameworkElement CreateSharedDlls()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<CleanupRow>();

        panel.Configure(
            Loc.Get("Module.SharedDlls"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Common.Name"), Width = 240, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Tool.Shared.Count"), Width = 110, BindingPath = "Kind" },
                new ColumnDef { Header = Loc.Get("Common.Path"), Width = 460, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Common.Size"), Width = 110, BindingPath = "Size" },
            },
            new[]
            {
                new CommandDef { AutomationId = "SharedScan", Text = Loc.Get("Tool.Shared.Scan"), Action = () => Escanear() },
                new CommandDef { AutomationId = "SharedRemove", Text = Loc.Get("Tool.Shared.Remove"), IsPrimary = true, Action = () => Remover() },
            });

        panel.SetItems(rows);
        panel.PanelStatus = Loc.Get("Tool.Shared.Start");
        return panel;

        void Escanear()
        {
            panel.RunAsync(async () =>
            {
                var programas = await Task.Run(InstalledPrograms);
                var entradas = await Task.Run(() => SharedDllCatalog.List(programas));

                rows.Clear();

                foreach (var entrada in entradas)
                {
                    rows.Add(new CleanupRow
                    {
                        Category = entrada.Name,
                        Kind = entrada.ReferenceCount.ToString(),
                        Path = entrada.Path,
                        Size = ByteFormatter.Format(entrada.SizeBytes),
                        Detail = string.Join(", ", entrada.UsedBy),
                        Source = entrada,
                    });
                }

                panel.PanelStatus = rows.Count == 0
                    ? Loc.Get("Tool.Shared.Empty")
                    : Loc.Get("Status.Done") + " — " + rows.Count + " item(ns)";
            });
        }

        void Remover()
        {
            var entrada = rows.FirstOrDefault(r => r.Selected);
            if (entrada is null)
            {
                panel.PanelStatus = Loc.Get("Tool.Shared.NothingSelected");
                return;
            }

            var resultado = SharedDllCatalog.Remove((SharedDllEntry)entrada.Source);
            panel.PanelStatus = resultado.Message;
        }
    }

    // ------------------------------------------------------------------
    // Lixeira
    // ------------------------------------------------------------------

    /// <summary>
    /// Tela da Lixeira: o que foi apagado sem querer, com o caminho original
    /// e a data do apagamento.
    /// </summary>
    private static FrameworkElement CreateRecycleBin()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<CleanupRow>();

        panel.Configure(
            Loc.Get("Module.RecycleBin"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Common.Name"), Width = 240, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Common.Path"), Width = 480, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Common.Size"), Width = 110, BindingPath = "Size" },
                new ColumnDef { Header = Loc.Get("Common.Date"), Width = 150, BindingPath = "Detail" },
            },
            new[]
            {
                new CommandDef { AutomationId = "RecycleScan", Text = Loc.Get("Tool.Recycle.Scan"), Action = () => Escanear() },
            });

        panel.SetItems(rows);
        panel.PanelStatus = Loc.Get("Tool.Recycle.Start");
        return panel;

        void Escanear()
        {
            panel.RunAsync(async () =>
            {
                var itens = await Task.Run(() => RecycleBinInspector.List());

                rows.Clear();

                foreach (var item in itens)
                {
                    rows.Add(new CleanupRow
                    {
                        Category = item.Name,
                        Path = item.OriginalPath,
                        Size = ByteFormatter.Format(item.SizeBytes),
                        Detail = item.DeletedAt == DateTime.MinValue
                            ? string.Empty
                            : item.DeletedAt.ToString("d", System.Globalization.CultureInfo.CurrentCulture),
                        Source = item,
                    });
                }

                var total = RecycleBinInspector.TotalSize();
                panel.PanelStatus = rows.Count == 0
                    ? Loc.Get("Tool.Recycle.Empty")
                    : Loc.Get("Status.Done") + " — " + rows.Count + " item(ns), " + ByteFormatter.Format(total);
            });
        }
    }
}