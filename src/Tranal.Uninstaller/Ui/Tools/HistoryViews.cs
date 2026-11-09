using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.History;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Ui.Shared;

namespace Tranal.Uninstaller.Ui.Tools;

/// <summary>
/// Tela do historico de desinstalacoes.
///
/// E o "Uninstall History" do Revo: a lista do que saiu do computador, com
/// a data em tempo relativo, os residuos que foram junto e o botao que
/// devolve as coisas ("Recuperar").
///
/// O dado vem do nucleo (<see cref="HistoryReport"/>), que e o mesmo que a
/// linha de comando le — nao ha uma segunda versao do historico.
/// </summary>
public static class HistoryViews
{
    /// <summary>
    /// Linha exibida pela tela do historico.
    ///
    /// Implementa <see cref="INotifyPropertyChanged"/> porque a marcacao e
    /// alterada por codigo: sem a notificacao, a caixa de marcacao da tela
    /// continuaria mostrando o estado antigo.
    /// </summary>
    private sealed class HistoryRowView : INotifyPropertyChanged
    {
        public string Programs { get; init; } = string.Empty;
        public string When { get; init; } = string.Empty;
        public string WhenExact { get; init; } = string.Empty;
        public string Level { get; init; } = string.Empty;
        public string Removed { get; init; } = string.Empty;
        public string Residues { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;

        private bool _selected = true;

        /// <summary>True quando a entrada esta marcada.</summary>
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

        /// <summary>A linha original do nucleo.</summary>
        public HistoryRow Source { get; init; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>Cria a tela do historico.</summary>
    public static FrameworkElement CreateHistory()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<HistoryRowView>();

        panel.Configure(
            Loc.Get("Module.UninstallHistory"),
            new[]
            {
                new ColumnDef { Header = "", Width = 34, IsCheckable = true },
                new ColumnDef { Header = Loc.Get("Common.Name"), Width = 260, BindingPath = "Programs" },
                new ColumnDef { Header = Loc.Get("Tool.History.When"), Width = 130, BindingPath = "When" },
                new ColumnDef { Header = Loc.Get("Tool.History.Level"), Width = 110, BindingPath = "Level" },
                new ColumnDef { Header = Loc.Get("Tool.History.Removed"), Width = 90, BindingPath = "Removed" },
                new ColumnDef { Header = Loc.Get("Tool.History.Residues"), Width = 240, BindingPath = "Residues" },
                new ColumnDef { Header = Loc.Get("Common.Status"), Width = 130, BindingPath = "Status" },
            },
            new[]
            {
                new CommandDef { AutomationId = "HistoryRefresh", Text = Loc.Get("Common.Refresh"), IsPrimary = true, Action = () => panel.RunAsync(() => LoadAsync(panel, rows)) },
                new CommandDef { AutomationId = "HistoryRecover", Text = Loc.Get("Tool.History.Recover"), Action = () => Recuperar(panel, rows) },
                new CommandDef { AutomationId = "HistoryClear", Text = Loc.Get("Tool.History.Clear"), Action = () => Limpar(panel, rows) },
            });

        panel.SetItems(rows);
        panel.RunAsync(() => LoadAsync(panel, rows));
        return panel;
    }

    /// <summary>
    /// Carrega o historico na tela.
    /// </summary>
    /// <param name="panel">O painel da tela.</param>
    /// <param name="rows">As linhas da tela.</param>
    private static async Task LoadAsync(ItemListPanel panel, ObservableCollection<HistoryRowView> rows)
    {
        var linhas = await Task.Run(() => HistoryReport.Read());

        rows.Clear();
        foreach (var linha in linhas)
        {
            rows.Add(new HistoryRowView
            {
                Programs = linha.Programs,
                When = linha.When,
                WhenExact = linha.WhenExact,
                Level = linha.Level,
                Removed = linha.Removed.ToString(),
                Residues = linha.ResidueCount > 0
                    ? $"{linha.ResidueCount} — {linha.Residues}"
                    : string.Empty,
                Status = linha.Recoverable
                    ? Loc.Get("Tool.History.Recoverable")
                    : (linha.Uninstalled ? Loc.Get("Tool.History.Removed") : Loc.Get("Tool.History.Failed")),
                Source = linha,
            });
        }

        panel.PanelStatus = string.Format(
            System.Globalization.CultureInfo.CurrentCulture, Loc.Get("Tool.History.Count"), rows.Count);
    }

    /// <summary>
    /// Recupera as sobras das entradas marcadas.
    ///
    /// A pergunta vem antes: recuperar devolve arquivos ao disco, e o
    /// usuario precisa saber que isso vai acontecer.
    /// </summary>
    /// <param name="panel">O painel da tela.</param>
    /// <param name="rows">As linhas da tela.</param>
    private static void Recuperar(ItemListPanel panel, ObservableCollection<HistoryRowView> rows)
    {
        var marcadas = rows.Where(r => r.Selected).Select(r => r.Source).ToList();
        if (marcadas.Count == 0)
        {
            panel.PanelStatus = Loc.Get("Status.NoSelection");
            return;
        }

        var recuperaveis = marcadas.Where(l => l.Recoverable).ToList();
        if (recuperaveis.Count == 0)
        {
            panel.PanelStatus = Loc.Get("Tool.History.NotRecoverable");
            return;
        }

        var pergunta = string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            Loc.Get("Tool.History.ConfirmRecover"),
            recuperaveis.Count);

        if (!UserMessages.Confirm(null, pergunta, Loc.Get("Module.UninstallHistory"))) return;

        var recuperadas = 0;
        var falhas = 0;

        foreach (var linha in recuperaveis)
        {
            var (ok, _) = HistoryReport.Recover(linha);
            if (ok) recuperadas++;
            else falhas++;
        }

        panel.PanelStatus = string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            Loc.Get("Tool.History.Recovered"),
            recuperadas,
            falhas);
    }

    /// <summary>
    /// Apaga o historico inteiro.
    ///
    /// E a unica acao da tela que destroi dado: por isso a confirmacao vem
    /// antes, e o historico so e apagado depois que o usuario aceita.
    /// </summary>
    /// <param name="panel">O painel da tela.</param>
    /// <param name="rows">As linhas da tela.</param>
    private static void Limpar(ItemListPanel panel, ObservableCollection<HistoryRowView> rows)
    {
        if (rows.Count == 0)
        {
            panel.PanelStatus = Loc.Get("Tool.History.Empty");
            return;
        }

        if (!UserMessages.Confirm(null, Loc.Get("Tool.History.ConfirmClear"), Loc.Get("Module.UninstallHistory"))) return;

        UninstallHistory.Clear();
        rows.Clear();
        panel.PanelStatus = Loc.Get("Tool.History.Empty");
    }
}
