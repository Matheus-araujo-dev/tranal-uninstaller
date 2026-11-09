using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Automation;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Tools;
using Tranal.Uninstaller.Ui.Shared;

namespace Tranal.Uninstaller.Ui.Tools;

/// <summary>
/// Telas de inspecao: o localizador de arquivos e chaves de registro, as
/// acoes agendadas para o proximo logon e o visor hexadecimal com a soma de
/// verificacao do arquivo.
///
/// Todas usam o mesmo painel generico (<see cref="ItemListPanel"/>).
/// </summary>
public static class InspectorViews
{
    /// <summary>Cria a tela da ferramenta pelo id do catalogo.</summary>
    /// <param name="toolId">O id da ferramenta.</param>
    public static FrameworkElement Create(string toolId) => toolId switch
    {
        ToolCatalog.Finder => CreateFinder(),
        ToolCatalog.RunOnce => CreateRunOnce(),
        ToolCatalog.HexViewer => CreateHexViewer(),
        _ => new ItemListPanel(),
    };

    /// <summary>
    /// Linha exibida pelas telas de inspecao.
    /// </summary>
    private sealed class InspectorRow : INotifyPropertyChanged
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
    // Localizador de arquivos e registros
    // ------------------------------------------------------------------

    /// <summary>
    /// Tela do localizador: procura arquivos e chaves de registro pelo nome.
    /// </summary>
    private static FrameworkElement CreateFinder()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<InspectorRow>();
        var pasta = string.Empty;
        var raizRegistro = @"HKEY_LOCAL_MACHINE\SOFTWARE";

        panel.Configure(
            Loc.Get("Module.Finder"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Common.Name"), Width = 240, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Common.Type"), Width = 110, BindingPath = "Kind" },
                new ColumnDef { Header = Loc.Get("Common.Path"), Width = 480, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Common.Size"), Width = 110, BindingPath = "Size" },
            },
            new[]
            {
                new CommandDef { AutomationId = "FinderSearch", Text = Loc.Get("Tool.Finder.Search"), IsPrimary = true, Action = () => Procurar() },
                new CommandDef { AutomationId = "FinderFolder", Text = Loc.Get("Tool.Finder.Folder"), Action = () => EscolherPasta() },
            });

        panel.SetItems(rows);
        panel.PanelStatus = Loc.Get("Tool.Finder.Start");
        return panel;

        void EscolherPasta()
        {
            var dialog = new OpenFolderDialog
            {
                Title = Loc.Get("Tool.Finder.Folder"),
            };

            if (dialog.ShowDialog() == true) pasta = dialog.FolderName;
        }

        void Procurar()
        {
            var padrao = Microsoft.VisualBasic.Interaction.InputBox(
                Loc.Get("Tool.Finder.PatternPrompt"),
                Loc.Get("Tool.Finder.Search"),
                "*");

            panel.RunAsync(async () =>
            {
                var achados = new List<FoundItem>();

                if (pasta.Length > 0)
                {
                    achados.AddRange(await Task.Run(() => ResourceFinder.FindFiles(pasta, padrao)));
                }

                achados.AddRange(await Task.Run(() => ResourceFinder.FindRegistryKeys(raizRegistro, padrao)));

                rows.Clear();

                foreach (var achado in achados)
                {
                    rows.Add(new InspectorRow
                    {
                        Category = achado.Name,
                        Kind = achado.Kind,
                        Path = achado.Path,
                        Size = achado.SizeBytes > 0 ? ByteFormatter.Format(achado.SizeBytes) : string.Empty,
                        Detail = achado.ModifiedAt?.ToString("d", System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty,
                        Source = achado,
                    });
                }

                panel.PanelStatus = rows.Count == 0
                    ? Loc.Get("Tool.Finder.Empty")
                    : Loc.Get("Status.Done") + " — " + rows.Count + " item(ns)";
            });
        }
    }

    // ------------------------------------------------------------------
    // Acoes agendadas
    // ------------------------------------------------------------------

    /// <summary>
    /// Tela das acoes agendadas para o proximo logon.
    /// </summary>
    private static FrameworkElement CreateRunOnce()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<InspectorRow>();

        panel.Configure(
            Loc.Get("Module.RunOnce"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Common.Name"), Width = 240, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Tool.RunOnce.Command"), Width = 560, BindingPath = "Path" },
            },
            new[]
            {
                new CommandDef { AutomationId = "RunOnceList", Text = Loc.Get("Tool.RunOnce.List"), Action = () => Listar() },
                new CommandDef { AutomationId = "RunOnceCancel", Text = Loc.Get("Tool.RunOnce.Cancel"), IsPrimary = true, Action = () => Cancelar() },
            });

        panel.SetItems(rows);
        panel.PanelStatus = Loc.Get("Tool.RunOnce.Start");
        return panel;

        void Listar()
        {
            panel.RunAsync(async () =>
            {
                var agendados = await Task.Run(() => RunOnceScheduler.List());

                rows.Clear();

                foreach (var item in agendados)
                {
                    var separador = item.IndexOf('=');
                    var nome = separador > 0 ? item[..separador].Trim() : item;
                    var comando = separador > 0 ? item[(separador + 1)..].Trim() : string.Empty;

                    rows.Add(new InspectorRow
                    {
                        Category = nome,
                        Path = comando,
                        Source = nome,
                    });
                }

                panel.PanelStatus = rows.Count == 0
                    ? Loc.Get("Tool.RunOnce.Empty")
                    : Loc.Get("Status.Done") + " — " + rows.Count + " item(ns)";
            });
        }

        void Cancelar()
        {
            var item = rows.FirstOrDefault(r => r.Selected);
            if (item is null)
            {
                panel.PanelStatus = Loc.Get("Tool.RunOnce.NothingSelected");
                return;
            }

            var nome = (string)item.Source;
            var cancelado = RunOnceScheduler.Cancel(nome);

            panel.PanelStatus = cancelado
                ? Loc.Get("Tool.RunOnce.Canceled")
                : Loc.Get("Tool.RunOnce.CancelFailed");
        }
    }

    // ------------------------------------------------------------------
    // Visor hexadecimal
    // ------------------------------------------------------------------

    /// <summary>
    /// Tela do visor hexadecimal, com a soma MD5 e SHA256 do arquivo.
    /// </summary>
    private static FrameworkElement CreateHexViewer()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<InspectorRow>();

        panel.Configure(
            Loc.Get("Module.HexViewer"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Tool.Hex.Offset"), Width = 130, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Tool.Hex.Hex"), Width = 520, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Tool.Hex.Text"), Width = 180, BindingPath = "Detail" },
            },
            new[]
            {
                new CommandDef { AutomationId = "HexOpen", Text = Loc.Get("Tool.Hex.Open"), Action = () => Abrir() },
            });

        panel.SetItems(rows);
        panel.PanelStatus = Loc.Get("Tool.Hex.Start");
        return panel;

        void Abrir()
        {
            var dialog = new OpenFileDialog
            {
                Title = Loc.Get("Tool.Hex.Open"),
                CheckFileExists = true,
            };

            if (dialog.ShowDialog() != true) return;

            var caminho = dialog.FileName;

            panel.RunAsync(async () =>
            {
                var linhas = await Task.Run(() => HexViewer.Dump(caminho));
                var md5 = await Task.Run(() => HexViewer.Md5Of(caminho));
                var sha = await Task.Run(() => HexViewer.Sha256Of(caminho));

                rows.Clear();

                foreach (var linha in linhas)
                {
                    rows.Add(new InspectorRow
                    {
                        Category = linha.Offset.ToString("X8", System.Globalization.CultureInfo.InvariantCulture),
                        Path = linha.Hex,
                        Detail = linha.Text,
                    });
                }

                panel.PanelStatus = $"{caminho} — MD5 {md5} — SHA256 {sha}";
            });
        }
    }

}
