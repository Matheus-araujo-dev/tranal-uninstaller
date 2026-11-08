using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Automation;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Modules;

namespace Tranal.Uninstaller.Ui;

/// <summary>
/// A janela de revisao: o "Custom Uninstall" (o que esta no log) e o
/// "Advanced Uninstall" (o que ficou fora do log).
///
/// As duas revisoes sao o mesmo tipo de tela com dados diferentes:
///
///   - <b>Custom Uninstall</b>: as entradas do log, com a decisao de remover
///     ou manter cada uma;
///   - <b>Advanced Uninstall</b>: os candidatos que a varredura achou e que o
///     log nao registrou.
///
/// O plano de remocao e sempre montado pelo nucleo
/// (<see cref="CustomUninstallReviewBuilder"/> e
/// <see cref="AdvancedUninstallReviewBuilder"/>) e executado pelo motor
/// canonico (<see cref="UninstallEngine.RemoveLeftovers"/>). A tela decide
/// apenas o que o usuario marcou.
/// </summary>
public partial class TracedReviewWindow : Window
{
    private readonly TracedInstall _log;
    private readonly bool _advanced;
    private readonly ObservableCollection<ReviewRow> _rows = new();

    /// <summary>Abre a revisao do log (Custom Uninstall).</summary>
    /// <param name="log">O log a revisar.</param>
    public TracedReviewWindow(TracedInstall log)
    {
        InitializeComponent();

        _log = log;
        _advanced = false;

        Title = Loc.Get("Mod.Review.CustomTitle");
        ItemsView.ItemsSource = _rows;
        ApplyTexts();

        LoadCustom();
    }

    /// <summary>Abre a revisao do que ficou fora do log (Advanced Uninstall).</summary>
    /// <param name="log">O log do programa.</param>
    /// <param name="candidates">Os candidatos achados na varredura.</param>
    public TracedReviewWindow(TracedInstall log, IEnumerable<LeftoverItem>? candidates)
    {
        InitializeComponent();

        _log = log;
        _advanced = true;

        Title = Loc.Get("Mod.Review.AdvancedTitle");
        ItemsView.ItemsSource = _rows;
        ApplyTexts();

        LoadAdvanced(candidates ?? Array.Empty<LeftoverItem>());
    }

    /// <summary>Escreve os textos da janela a partir do catalogo de idioma.</summary>
    private void ApplyTexts()
    {
        SelectAllButton.Content = Loc.Get("Mod.Review.SelectAllRemove");
        KeepAllButton.Content = Loc.Get("Mod.Review.KeepAll");
        RemoveButton.Content = Loc.Get("Mod.Review.RemoveSelected");
        CloseButton.Content = Loc.Get("Common.Close");
    }

    /// <summary>Preenche a lista com as entradas do log.</summary>
    private void LoadCustom()
    {
        var review = CustomUninstallReviewBuilder.Build(_log);

        _rows.Clear();
        foreach (var item in review.Items)
        {
            var row = new ReviewRow(
                KindText(item.Entry),
                item.Entry.Path,
                item.Reason,
                item.WillRemove,
                advanced: false)
            {
                Entry = item.Entry,
            };

            row.PropertyChanged += Row_PropertyChanged;
            _rows.Add(row);
        }

        TitleText.Text = $"{Loc.Get("Mod.Review.CustomTitle")} — {_log.ProgramName}";
        UpdateSummary();
    }

    /// <summary>Preenche a lista com o que a varredura achou e o log nao registrou.</summary>
    private void LoadAdvanced(IEnumerable<LeftoverItem> candidates)
    {
        var review = AdvancedUninstallReviewBuilder.Build(_log, candidates);

        _rows.Clear();
        foreach (var candidate in review.Unlogged)
        {
            var row = new ReviewRow(
                candidate.Item.Kind,
                candidate.Item.Path,
                candidate.Detail,
                selected: false,
                advanced: true)
            {
                Item = candidate.Item,
            };

            row.PropertyChanged += Row_PropertyChanged;
            _rows.Add(row);
        }

        TitleText.Text = $"{Loc.Get("Mod.Review.AdvancedTitle")} — {_log.ProgramName}";
        SubtitleText.Text = Loc.Get("Mod.Review.AdvancedSummary", review.UnloggedCount, review.Logged.Count);
    }

    /// <summary>Rotulo do tipo da entrada do log.</summary>
    private static string KindText(LogEntry entry) => entry.Kind switch
    {
        LogEntryKind.RegistryKey => Loc.Get("Mod.Logs.Kind.RegistryKey"),
        LogEntryKind.RegistryValue => Loc.Get("Mod.Logs.Kind.RegistryValue"),
        _ => entry.IsFolder ? Loc.Get("Mod.Logs.Kind.Folder") : Loc.Get("Mod.Logs.Kind.File"),
    };

    /// <summary>Atualiza o resumo quando o usuario muda a decisao de uma linha.</summary>
    private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReviewRow.Selected)) UpdateSummary();
    }

    /// <summary>Escreve o resumo da revisao (quantos entram e quantos ficam).</summary>
    private void UpdateSummary()
    {
        if (_advanced) return;

        var remove = _rows.Count(r => r.Selected);
        SubtitleText.Text = Loc.Get("Mod.Review.Summary", remove, _rows.Count - remove);
    }

    /// <summary>Marca tudo para remover.</summary>
    private void SelectAllButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in _rows) row.Selected = true;
        UpdateSummary();
    }

    /// <summary>Mantem tudo.</summary>
    private void KeepAllButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in _rows) row.Selected = false;
        UpdateSummary();
    }

    /// <summary>Executa a remocao do que o usuario marcou.</summary>
    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        var marked = _rows.Where(r => r.Selected).ToList();
        if (marked.Count == 0)
        {
            StatusText.Text = Loc.Get("Status.NoSelection");
            return;
        }

        if (!UserMessages.Confirm(this, Loc.Get("Mod.Review.Confirm", marked.Count))) return;

        var report = _advanced ? RemoveAdvanced(marked) : RemoveCustom(marked);

        StatusText.Text = Loc.Get("Mod.Review.Removed", report.RemovedCount, report.FailedCount);
    }

    /// <summary>
    /// Monta o plano de remocao a partir das decisoes do usuario e entrega ao
    /// motor canonico.
    ///
    /// O plano e remontado pelo nucleo (<see cref="CustomUninstallReviewBuilder"/>)
    /// com a decisao do usuario: a tela nao traduz entrada em item de remocao
    /// por conta propria.
    /// </summary>
    private RemovalReport RemoveCustom(List<ReviewRow> marked)
    {
        var ids = marked
            .Where(r => r.Entry is not null)
            .Select(r => r.Entry!.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var review = CustomUninstallReviewBuilder.Build(_log, entry => ids.Contains(entry.Id));

        return UninstallEngine.RemoveLeftovers(review.Leftovers);
    }

    /// <summary>Entrega ao motor canonico os candidatos marcados.</summary>
    private static RemovalReport RemoveAdvanced(List<ReviewRow> marked)
    {
        var items = marked
            .Where(r => r.Item is not null)
            .Select(r => new LeftoverItem
            {
                Kind = r.Item!.Kind,
                Path = r.Item.Path,
                Detail = r.Item.Detail,
                Selected = true,
                Confidence = r.Item.Confidence,
                Reason = r.Item.Reason,
            })
            .ToList();

        return UninstallEngine.RemoveLeftovers(items);
    }

    /// <summary>Fecha a janela.</summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>Uma linha da revisao.</summary>
    private sealed class ReviewRow : INotifyPropertyChanged
    {
        private bool _selected;

        public ReviewRow(string tipo, string caminho, string motivo, bool selected, bool advanced)
        {
            Tipo = tipo;
            Caminho = caminho;
            Motivo = motivo;
            Advanced = advanced;
            _selected = selected;
            Decisao = DecisionText(selected, advanced);
        }

        /// <summary>O tipo do item (arquivo, pasta, chave de registro).</summary>
        public string Tipo { get; }

        /// <summary>O caminho do item.</summary>
        public string Caminho { get; }

        /// <summary>O motivo da decisao, em portugues.</summary>
        public string Motivo { get; }

        /// <summary>True quando esta e a revisao do que ficou fora do log.</summary>
        public bool Advanced { get; }

        /// <summary>A entrada do log (revisao do Custom Uninstall).</summary>
        public LogEntry? Entry { get; init; }

        /// <summary>O candidato achado na varredura (revisao do Advanced Uninstall).</summary>
        public LeftoverItem? Item { get; init; }

        /// <summary>True quando o item entra no plano de remocao.</summary>
        public bool Selected
        {
            get => _selected;
            set
            {
                if (_selected == value) return;

                _selected = value;
                Decisao = DecisionText(value, Advanced);

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Decisao)));
            }
        }

        /// <summary>A decisao mostrada na linha.</summary>
        public string Decisao { get; private set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        private static string DecisionText(bool selected, bool advanced)
        {
            if (advanced) return Loc.Get("Mod.Review.Outside");
            return selected ? Loc.Get("Common.Remove") : Loc.Get("Mod.Review.Keep");
        }
    }
}
