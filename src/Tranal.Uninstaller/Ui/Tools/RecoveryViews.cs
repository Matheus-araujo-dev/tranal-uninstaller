using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Recovery;
using Tranal.Uninstaller.Ui.Shared;

namespace Tranal.Uninstaller.Ui.Tools;

/// <summary>
/// As telas de recuperacao: o que o Windows deixou pendente e o que o Tranal
/// pode resolver sem esperar o proximo reinicio.
///
/// A REGRA mora no nucleo (<see cref="PendingOperations"/>): a tela mostra a
/// fila, aplica a operacao e tira da fila — sem reescrever nada.
/// </summary>
public static class RecoveryViews
{
    /// <summary>Cria a tela da ferramenta pelo id do catalogo.</summary>
    /// <param name="toolId">O id da ferramenta.</param>
    public static FrameworkElement Create(string toolId) => toolId switch
    {
        ToolCatalog.PendingOperations => CreatePendingOperations(),
        ToolCatalog.DailyRegistryBackup => CreateDailyBackup(),
        _ => new ItemListPanel(),
    };

    /// <summary>
    /// Uma operacao pendente na lista.
    /// </summary>
    private sealed class PendingRow
    {
        /// <summary>O tipo da operacao (apagar ou renomear).</summary>
        public string Category { get; init; } = string.Empty;

        /// <summary>O caminho de origem.</summary>
        public string Path { get; init; } = string.Empty;

        /// <summary>O caminho de destino (vazio quando a operacao apaga).</summary>
        public string Size { get; init; } = string.Empty;

        /// <summary>A situacao da operacao.</summary>
        public string Detail { get; init; } = string.Empty;

        /// <summary>A operacao original do nucleo.</summary>
        public PendingOperation Source { get; init; } = new();
    }

    /// <summary>
    /// Um backup diario na lista.
    /// </summary>
    private sealed class BackupRow
    {
        /// <summary>O dia do backup.</summary>
        public string Category { get; init; } = string.Empty;

        /// <summary>Quantos arquivos o backup tem.</summary>
        public string Kind { get; init; } = string.Empty;

        /// <summary>A pasta do backup.</summary>
        public string Path { get; init; } = string.Empty;
    }

    /// <summary>
    /// A fila de operacoes pendentes do Windows.
    ///
    /// E a tela do "Pending File Rename Operations": o que o Windows vai
    /// renomear ou apagar no proximo arranque, com a opcao de tentar agora —
    /// que funciona quando o arquivo ja nao esta em uso.
    /// </summary>
    private static FrameworkElement CreatePendingOperations()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<PendingRow>();

        panel.Configure(
            Loc.Get("Module.PendingOperations"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Tool.Pending.Type"), Width = 120, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Common.Path"), Width = 400, BindingPath = "Path" },
                new ColumnDef { Header = Loc.Get("Tool.Pending.Destination"), Width = 340, BindingPath = "Size" },
                new ColumnDef { Header = Loc.Get("Tool.Pending.State"), Width = 130, BindingPath = "Detail" },
            },
            new[]
            {
                new CommandDef { AutomationId = "PendingRefresh", Text = Loc.Get("Common.Refresh"), Action = () => Recarregar() },
                new CommandDef { AutomationId = "PendingApply", Text = Loc.Get("Tool.Pending.Apply"), IsPrimary = true, Action = () => Aplicar() },
                new CommandDef { AutomationId = "PendingRemove", Text = Loc.Get("Tool.Pending.Remove"), Action = () => Remover() },
            });

        panel.SetItems(rows);
        panel.PanelStatus = Loc.Get("Tool.Pending.Start");
        Recarregar();
        return panel;

        // A linha escolhida na lista: e sobre ela que os comandos agem.
        PendingRow? Escolhida()
        {
            foreach (var item in panel.SelectedItems)
            {
                if (item is PendingRow row) return row;
            }

            return null;
        }

        void Recarregar()
        {
            var operacoes = PendingOperations.List();

            rows.Clear();
            foreach (var operacao in operacoes)
            {
                rows.Add(new PendingRow
                {
                    Category = operacao.IsDelete
                        ? Loc.Get("Tool.Pending.Delete")
                        : Loc.Get("Tool.Pending.Rename"),
                    Path = operacao.Source,
                    Size = operacao.IsDelete ? string.Empty : operacao.Destination,
                    Detail = File.Exists(operacao.Source) || Directory.Exists(operacao.Source)
                        ? Loc.Get("Tool.Pending.Waiting")
                        : Loc.Get("Tool.Pending.Missing"),
                    Source = operacao,
                });
            }

            panel.PanelStatus = rows.Count == 0
                ? Loc.Get("Tool.Pending.Empty")
                : Loc.Get("Tool.Pending.Count", rows.Count);
        }

        void Aplicar()
        {
            var row = Escolhida();
            if (row is null)
            {
                panel.PanelStatus = Loc.Get("Status.NoSelection");
                return;
            }

            var (ok, mensagem) = PendingOperations.Apply(row.Source);

            panel.PanelStatus = ok
                ? Loc.Get("Tool.Pending.Applied")
                : Loc.Get("Tool.Pending.Failed", mensagem);

            Recarregar();
        }

        void Remover()
        {
            var row = Escolhida();
            if (row is null)
            {
                panel.PanelStatus = Loc.Get("Status.NoSelection");
                return;
            }

            var ok = PendingOperations.Remove(row.Source);

            panel.PanelStatus = ok
                ? Loc.Get("Tool.Pending.Removed")
                : Loc.Get("Tool.Pending.FailedRemove");

            Recarregar();
        }
    }

    // ------------------------------------------------------------------
    // O backup diario do registro
    // ------------------------------------------------------------------

    /// <summary>
    /// O backup diario do registro: as copias ja feitas e o botao que pede
    /// uma nova.
    ///
    /// O Tranal tambem faz esse backup sozinho, uma vez por dia, no
    /// arranque. A tela e o que mostra o que ja foi feito e o que pode ser
    /// pedido na hora.
    /// </summary>
    private static FrameworkElement CreateDailyBackup()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<BackupRow>();

        panel.Configure(
            Loc.Get("Module.DailyRegistryBackup"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Tool.DailyBackup.When"), Width = 130, BindingPath = "Category" },
                new ColumnDef { Header = Loc.Get("Tool.DailyBackup.Files"), Width = 110, BindingPath = "Kind" },
                new ColumnDef { Header = Loc.Get("Tool.DailyBackup.Folder"), Width = 520, BindingPath = "Path" },
            },
            new[]
            {
                new CommandDef { AutomationId = "DailyBackupRefresh", Text = Loc.Get("Common.Refresh"), Action = () => Recarregar() },
                new CommandDef { AutomationId = "DailyBackupRun", Text = Loc.Get("Tool.DailyBackup.Run"), IsPrimary = true, Action = () => Executar() },
            });

        panel.SetItems(rows);
        Recarregar();
        return panel;

        void Recarregar()
        {
            var backups = DailyRegistryBackup.List();

            rows.Clear();
            foreach (var backup in backups)
            {
                rows.Add(new BackupRow
                {
                    Category = backup.Date == DateTime.MinValue
                        ? Loc.Get("Tool.DailyBackup.Unknown")
                        : backup.Date.ToString("yyyy-MM-dd"),
                    Kind = backup.Files.Count.ToString(),
                    Path = backup.Directory,
                });
            }

            panel.PanelStatus = rows.Count == 0
                ? Loc.Get("Tool.DailyBackup.Empty")
                : Loc.Get("Tool.DailyBackup.Count", rows.Count);

            var ultimo = DailyRegistryBackup.LastRunUtc;
            if (ultimo is not null)
            {
                panel.PanelStatus += " — " + Loc.Get("Tool.DailyBackup.LastRun",
                    ultimo.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm"));
            }
        }

        void Executar()
        {
            // O reg.exe export leva alguns segundos: a tela nao pode
            // congelar enquanto o backup e feito.
            panel.RunAsync(async () =>
            {
                var resultado = await Task.Run(() => DailyRegistryBackup.Run());

                panel.PanelStatus = resultado.Success
                    ? Loc.Get("Tool.DailyBackup.Done")
                    : Loc.Get("Tool.DailyBackup.Failed", resultado.Message);

                Recarregar();
            });
        }
    }
}
