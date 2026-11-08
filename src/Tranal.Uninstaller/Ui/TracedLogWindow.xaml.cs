using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Automation;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Reporting;

namespace Tranal.Uninstaller.Ui;

/// <summary>
/// A janela que mostra as entradas do log de instalacao.
///
/// E a casca que faltava: o nucleo (<see cref="LogOperations"/>) ja sabia
/// renomear o log, mesclar logs, editar, ignorar, remover e acrescentar
/// entradas — e a exportacao dos dados do log ja existia
/// (<see cref="TraceLogExporter"/>). Aqui isso vira tela.
///
/// A janela nao decide nada: ela mostra o que o nucleo devolve e devolve ao
/// nucleo o que o usuario pediu. A revisao item a item (Custom Uninstall) e a
/// revisao do que ficou fora do log (Advanced Uninstall) moram em
/// <see cref="TracedReviewWindow"/>.
/// </summary>
public partial class TracedLogWindow : Window
{
    private TracedInstall _log;
    private readonly ObservableCollection<LogEntryRow> _entries = new();
    private readonly ObservableCollection<MergeLogRow> _mergeLogs = new();

    /// <summary>A entrada em edicao. Nulo quando o painel esta para acrescentar.</summary>
    private LogEntry? _editing;

    /// <summary>Abre a janela de um log.</summary>
    /// <param name="log">O log a mostrar.</param>
    public TracedLogWindow(TracedInstall log)
    {
        InitializeComponent();

        _log = log;

        Title = Loc.Get("Mod.Logs.WindowTitle");
        EntriesView.ItemsSource = _entries;
        MergeLogsView.ItemsSource = _mergeLogs;

        BuildKindChoices();
        BuildOperationChoices();
        BuildEntriesColumns();
        ApplyTexts();

        Reload();
    }

    /// <summary>Rotulos dos tipos de entrada, do log e da confianca.</summary>
    private static string KindText(LogEntryKind kind, bool isFolder) => kind switch
    {
        LogEntryKind.RegistryKey => Loc.Get("Mod.Logs.Kind.RegistryKey"),
        LogEntryKind.RegistryValue => Loc.Get("Mod.Logs.Kind.RegistryValue"),
        _ => isFolder ? Loc.Get("Mod.Logs.Kind.Folder") : Loc.Get("Mod.Logs.Kind.File"),
    };

    /// <summary>Rotulo da operacao registrada para a entrada.</summary>
    private static string OperationText(LogEntryOperation operation) => operation switch
    {
        LogEntryOperation.Created => Loc.Get("Mod.Logs.Operation.Created"),
        LogEntryOperation.Modified => Loc.Get("Mod.Logs.Operation.Modified"),
        LogEntryOperation.Deleted => Loc.Get("Mod.Logs.Operation.Deleted"),
        _ => Loc.Get("Mod.Logs.Operation.Unknown"),
    };

    /// <summary>Rotulo da confianca da entrada.</summary>
    private static string ConfidenceText(LeftoverConfidence confidence) => confidence switch
    {
        LeftoverConfidence.High => Loc.Get("Common.High"),
        LeftoverConfidence.Medium => Loc.Get("Common.Medium"),
        _ => Loc.Get("Common.Low"),
    };

    /// <summary>Escreve os textos da janela a partir do catalogo de idioma.</summary>
    private void ApplyTexts()
    {
        EditButton.Content = Loc.Get("Mod.Logs.Edit");
        IgnoreButton.Content = Loc.Get("Mod.Logs.Ignore");
        RestoreButton.Content = Loc.Get("Mod.Logs.Unignore");
        RemoveEntryButton.Content = Loc.Get("Mod.Logs.RemoveEntry");
        AddEntryButton.Content = Loc.Get("Mod.Logs.AddEntry");
        RenameButton.Content = Loc.Get("Mod.Logs.Rename");
        ExportButton.Content = Loc.Get("Mod.Logs.ExportData");
        MergeButton.Content = Loc.Get("Mod.Logs.Merge");
        CustomButton.Content = Loc.Get("Mod.Logs.Custom");
        AdvancedButton.Content = Loc.Get("Mod.Logs.Advanced");
        CloseButton.Content = Loc.Get("Common.Close");

        EditTitleText.Text = Loc.Get("Mod.Logs.EditPanel");
        KindLabel.Text = Loc.Get("Common.Type");
        PathLabel.Text = Loc.Get("Common.Path");
        ValueNameLabel.Text = Loc.Get("Mod.Logs.ValueName");
        ValueLabel.Text = Loc.Get("Mod.Logs.Value");
        OperationLabel.Text = Loc.Get("Common.Operation");
        OriginLabel.Text = Loc.Get("Common.Source");
        SaveEditButton.Content = Loc.Get("Common.Apply");
        ClearEditButton.Content = Loc.Get("Common.Cancel");

        MergeTitleText.Text = Loc.Get("Mod.Logs.MergePanel");
        MergeHintText.Text = Loc.Get("Mod.Logs.MergeHint");
        MergeNameLabel.Text = Loc.Get("Mod.Logs.MergeName");
    }

    /// <summary>Preenche a lista de tipos que a entrada pode ter.</summary>
    private void BuildKindChoices()
    {
        KindCombo.Items.Add(new KindChoice(Loc.Get("Mod.Logs.Kind.File"), LogEntryKind.File, false));
        KindCombo.Items.Add(new KindChoice(Loc.Get("Mod.Logs.Kind.Folder"), LogEntryKind.File, true));
        KindCombo.Items.Add(new KindChoice(Loc.Get("Mod.Logs.Kind.RegistryKey"), LogEntryKind.RegistryKey, false));
        KindCombo.Items.Add(new KindChoice(Loc.Get("Mod.Logs.Kind.RegistryValue"), LogEntryKind.RegistryValue, false));
        KindCombo.SelectedIndex = 0;
    }

    /// <summary>Preenche a lista de operacoes que a entrada pode ter.</summary>
    private void BuildOperationChoices()
    {
        OperationCombo.Items.Add(new OperationChoice(Loc.Get("Mod.Logs.Operation.Created"), LogEntryOperation.Created));
        OperationCombo.Items.Add(new OperationChoice(Loc.Get("Mod.Logs.Operation.Modified"), LogEntryOperation.Modified));
        OperationCombo.Items.Add(new OperationChoice(Loc.Get("Mod.Logs.Operation.Deleted"), LogEntryOperation.Deleted));
        OperationCombo.Items.Add(new OperationChoice(Loc.Get("Mod.Logs.Operation.Unknown"), LogEntryOperation.Unknown));
        OperationCombo.SelectedIndex = 0;
    }

    /// <summary>Monta as colunas da lista de entradas.</summary>
    private void BuildEntriesColumns()
    {
        var view = new GridView();

        view.Columns.Add(new GridViewColumn { Header = Loc.Get("Common.Type"), Width = 130, DisplayMemberBinding = new Binding("Tipo") });
        view.Columns.Add(new GridViewColumn { Header = Loc.Get("Common.Path"), Width = 380, DisplayMemberBinding = new Binding("Caminho") });
        view.Columns.Add(new GridViewColumn { Header = Loc.Get("Mod.Logs.Value"), Width = 200, DisplayMemberBinding = new Binding("Valor") });
        view.Columns.Add(new GridViewColumn { Header = Loc.Get("Common.Operation"), Width = 110, DisplayMemberBinding = new Binding("Operacao") });
        view.Columns.Add(new GridViewColumn { Header = Loc.Get("Common.Confidence"), Width = 100, DisplayMemberBinding = new Binding("Confianca") });
        view.Columns.Add(new GridViewColumn { Header = Loc.Get("Common.Status"), Width = 90, DisplayMemberBinding = new Binding("Estado") });

        foreach (var column in view.Columns)
        {
            if (TryFindResource("ListViewHeaderStyle") is Style headerStyle)
                column.Header = new GridViewColumnHeader { Content = column.Header, Style = headerStyle };
        }

        EntriesView.View = view;
    }

    /// <summary>Le o log de novo e redesenha as duas listas.</summary>
    private void Reload()
    {
        _entries.Clear();
        foreach (var entry in LogOperations.Entries(_log))
            _entries.Add(new LogEntryRow(entry));

        TitleText.Text = Loc.Get("Mod.Logs.EntriesTitle", _log.ProgramName);
        SubtitleText.Text = string.Format(
            CultureInfo.CurrentCulture,
            "{0} — {1}",
            Loc.Get("Mod.Logs.EntriesCount", _entries.Count),
            _log.LogPath);

        ReloadMergeLogs();
        UpdateOrigin();
    }

    /// <summary>Redesenha a lista de logs que podem ser mesclados.</summary>
    private void ReloadMergeLogs()
    {
        _mergeLogs.Clear();
        foreach (var log in TraceStore.List())
            _mergeLogs.Add(new MergeLogRow(log));
    }

    /// <summary>A entrada selecionada na lista de entradas.</summary>
    private LogEntryRow? SelectedEntry()
    {
        foreach (var item in EntriesView.SelectedItems)
            if (item is LogEntryRow row) return row;

        return null;
    }

    /// <summary>Preenche o painel de edicao com a entrada selecionada.</summary>
    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        var row = SelectedEntry();
        if (row is null)
        {
            StatusText.Text = Loc.Get("Status.NoSelection");
            return;
        }

        _editing = row.Source;

        KindCombo.SelectedIndex = KindIndexOf(row.Source.Kind, row.Source.IsFolder);
        PathBox.Text = row.Source.Path;
        ValueNameBox.Text = row.Source.ValueName;
        ValueBox.Text = row.Source.Value;
        OperationCombo.SelectedIndex = OperationIndexOf(row.Source.Operation);

        // O tipo da entrada nao muda em edicao: o nucleo edita o caminho, o
        // valor e a operacao de uma entrada que ja existe.
        KindCombo.IsEnabled = false;

        StatusText.Text = row.Source.Path;
    }

    /// <summary>Acrescenta uma entrada ao log.</summary>
    private void AddEntryButton_Click(object sender, RoutedEventArgs e)
    {
        _editing = null;
        KindCombo.IsEnabled = true;
        ClearEditFields();
        StatusText.Text = Loc.Get("Mod.Logs.AddEntry");
        PathBox.Focus();
    }

    /// <summary>Grava a edicao (ou a entrada nova).</summary>
    private void SaveEditButton_Click(object sender, RoutedEventArgs e)
    {
        var path = (PathBox.Text ?? string.Empty).Trim();
        if (path.Length == 0)
        {
            StatusText.Text = Loc.Get("Mod.Logs.PathRequired");
            return;
        }

        var choice = KindCombo.SelectedItem as KindChoice ?? KindCombo.Items[0] as KindChoice;
        var operation = (OperationCombo.SelectedItem as OperationChoice)?.Operation ?? LogEntryOperation.Unknown;
        var valueName = (ValueNameBox.Text ?? string.Empty).Trim();
        var value = ValueBox.Text ?? string.Empty;

        if (_editing is null)
        {
            var entry = new LogEntry
            {
                Kind = choice?.Kind ?? LogEntryKind.File,
                IsFolder = choice?.IsFolder ?? false,
                Path = path,
                ValueName = valueName,
                Value = value,
                Operation = operation,
            };

            if (!LogOperations.AddEntry(_log, entry))
            {
                StatusText.Text = Loc.Get("Mod.Logs.Fail");
                return;
            }

            StatusText.Text = Loc.Get("Mod.Logs.EntryAdded");
        }
        else
        {
            var edit = new LogEntryEdit
            {
                Id = _editing.Id,
                Path = path,
                ValueName = valueName,
                Value = value,
                Operation = operation,
            };

            if (!LogOperations.EditEntry(_log, edit))
            {
                StatusText.Text = Loc.Get("Mod.Logs.Fail");
                return;
            }

            StatusText.Text = Loc.Get("Mod.Logs.Saved");
        }

        ClearEditFields();
        Reload();
    }

    /// <summary>Limpa o painel de edicao.</summary>
    private void ClearEditButton_Click(object sender, RoutedEventArgs e)
    {
        ClearEditFields();
        StatusText.Text = Loc.Get("Mod.Logs.EditPanel");
    }

    /// <summary>Esvazia os campos do painel de edicao.</summary>
    private void ClearEditFields()
    {
        _editing = null;
        KindCombo.IsEnabled = true;
        KindCombo.SelectedIndex = 0;
        PathBox.Text = string.Empty;
        ValueNameBox.Text = string.Empty;
        ValueBox.Text = string.Empty;
        OperationCombo.SelectedIndex = 0;
        UpdateOrigin();
    }

    /// <summary>Marca a entrada selecionada como ignorada.</summary>
    private void IgnoreButton_Click(object sender, RoutedEventArgs e)
    {
        var row = SelectedEntry();
        if (row is null)
        {
            StatusText.Text = Loc.Get("Status.NoSelection");
            return;
        }

        if (!LogOperations.IgnoreEntry(_log, row.Source, true))
        {
            StatusText.Text = Loc.Get("Mod.Logs.Fail");
            return;
        }

        StatusText.Text = Loc.Get("Mod.Logs.EntryIgnored");
        Reload();
    }

    /// <summary>Devolve a entrada selecionada ao plano de remocao.</summary>
    private void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        var row = SelectedEntry();
        if (row is null)
        {
            StatusText.Text = Loc.Get("Status.NoSelection");
            return;
        }

        if (!LogOperations.IgnoreEntry(_log, row.Source, false))
        {
            StatusText.Text = Loc.Get("Mod.Logs.Fail");
            return;
        }

        StatusText.Text = Loc.Get("Mod.Logs.EntryRestored");
        Reload();
    }

    /// <summary>Remove a entrada selecionada do log.</summary>
    private void RemoveEntryButton_Click(object sender, RoutedEventArgs e)
    {
        var row = SelectedEntry();
        if (row is null)
        {
            StatusText.Text = Loc.Get("Status.NoSelection");
            return;
        }

        if (!UserMessages.Confirm(this, Loc.Get("Mod.Logs.RemoveConfirm", row.Source.Path))) return;

        if (!LogOperations.RemoveEntry(_log, row.Source))
        {
            StatusText.Text = Loc.Get("Mod.Logs.Fail");
            return;
        }

        StatusText.Text = Loc.Get("Mod.Logs.EntryRemoved");
        Reload();
    }

    /// <summary>Renomeia o log.</summary>
    private void RenameButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new RenameWindow(_log.ProgramName, _log.ProgramName) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        var name = (dialog.DisplayName ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            StatusText.Text = Loc.Get("Mod.Logs.NameRequired");
            return;
        }

        if (!LogOperations.Rename(_log, name))
        {
            StatusText.Text = Loc.Get("Mod.Logs.Fail");
            return;
        }

        // O nome do log mudou: o log gravado e o que vale a pena mostrar.
        _log = TraceStore.Get(_log.Id) ?? _log;

        StatusText.Text = Loc.Get("Mod.Logs.Renamed", name);
        Reload();
    }

    /// <summary>Exporta os dados do log (CSV, texto ou HTML).</summary>
    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = Loc.Get("Mod.Logs.ExportData"),
            Filter = Loc.Get("Mod.Logs.ExportFilter"),
            FileName = string.IsNullOrWhiteSpace(_log.ProgramName) ? "log.csv" : _log.ProgramName + ".csv",
            DefaultExt = ".csv",
            AddExtension = true,
        };

        if (dialog.ShowDialog(this) != true) return;

        var format = Path.GetExtension(dialog.FileName).ToLowerInvariant() switch
        {
            ".txt" => TraceLogExportFormat.Text,
            ".html" or ".htm" => TraceLogExportFormat.Html,
            _ => TraceLogExportFormat.Csv,
        };

        var written = TraceLogExporter.Write(_log, dialog.FileName, format);
        if (string.IsNullOrEmpty(written))
        {
            StatusText.Text = Loc.Get("Status.Error");
            return;
        }

        StatusText.Text = Loc.Get("Mod.Logs.Exported", written);
    }

    /// <summary>Mescla os logs marcados num log novo.</summary>
    private void MergeButton_Click(object sender, RoutedEventArgs e)
    {
        var marked = _mergeLogs.Where(l => l.Selected).Select(l => l.Source).ToList();
        if (marked.Count == 0)
        {
            StatusText.Text = Loc.Get("Status.NoSelection");
            return;
        }

        var name = (MergeNameBox.Text ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            StatusText.Text = Loc.Get("Mod.Logs.NameRequired");
            return;
        }

        var merged = LogOperations.Merge(marked, name);
        if (merged is null)
        {
            StatusText.Text = Loc.Get("Mod.Logs.Fail");
            return;
        }

        MergeNameBox.Text = string.Empty;
        StatusText.Text = Loc.Get("Mod.Logs.Merged", merged.ProgramName);
        ReloadMergeLogs();
    }

    /// <summary>Abre a revisao item a item do log (Custom Uninstall).</summary>
    private void CustomButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new TracedReviewWindow(_log) { Owner = this };
        dialog.ShowDialog();
        Reload();
    }

    /// <summary>Abre a revisao do que ficou fora do log (Advanced Uninstall).</summary>
    private async void AdvancedButton_Click(object sender, RoutedEventArgs e)
    {
        AdvancedButton.IsEnabled = false;
        StatusText.Text = Loc.Get("Status.Scanning");

        try
        {
            var candidates = await Task.Run(() => TracedLogCandidates.Scan(_log));

            var dialog = new TracedReviewWindow(_log, candidates) { Owner = this };
            dialog.ShowDialog();
            Reload();
        }
        finally
        {
            AdvancedButton.IsEnabled = true;
        }
    }

    /// <summary>Fecha a janela.</summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>Escreve a origem da entrada em edicao.</summary>
    private void UpdateOrigin()
    {
        OriginText.Text = _editing?.Origin ?? string.Empty;
    }

    /// <summary>A posicao do tipo da entrada na lista de tipos.</summary>
    private static int KindIndexOf(LogEntryKind kind, bool isFolder) => kind switch
    {
        LogEntryKind.RegistryKey => 2,
        LogEntryKind.RegistryValue => 3,
        _ => isFolder ? 1 : 0,
    };

    /// <summary>A posicao da operacao da entrada na lista de operacoes.</summary>
    private static int OperationIndexOf(LogEntryOperation operation) => operation switch
    {
        LogEntryOperation.Created => 0,
        LogEntryOperation.Modified => 1,
        LogEntryOperation.Deleted => 2,
        _ => 3,
    };

    /// <summary>Um tipo de entrada escolhido na lista.</summary>
    private sealed class KindChoice
    {
        public KindChoice(string text, LogEntryKind kind, bool isFolder)
        {
            Text = text;
            Kind = kind;
            IsFolder = isFolder;
        }

        public string Text { get; }
        public LogEntryKind Kind { get; }
        public bool IsFolder { get; }

        public override string ToString() => Text;
    }

    /// <summary>Uma operacao escolhida na lista.</summary>
    private sealed class OperationChoice
    {
        public OperationChoice(string text, LogEntryOperation operation)
        {
            Text = text;
            Operation = operation;
        }

        public string Text { get; }
        public LogEntryOperation Operation { get; }

        public override string ToString() => Text;
    }

    /// <summary>Uma linha da lista de entradas do log.</summary>
    private sealed class LogEntryRow
    {
        public LogEntryRow(LogEntry entry) => Source = entry;

        public LogEntry Source { get; }

        public string Tipo => KindText(Source.Kind, Source.IsFolder);
        public string Caminho => Source.Path;
        public string Valor => Source.Kind == LogEntryKind.RegistryValue
            ? $"{Source.ValueName} = {Source.Value}"
            : Source.Value;
        public string Operacao => OperationText(Source.Operation);
        public string Confianca => ConfidenceText(Source.Confidence);
        public string Estado => Source.Ignored ? Loc.Get("Mod.Logs.Ignored") : string.Empty;
    }

    /// <summary>Uma linha da lista de logs que podem ser mesclados.</summary>
    private sealed class MergeLogRow
    {
        public MergeLogRow(TracedInstall install)
        {
            Source = install;
            Name = install.ProgramName;
            Date = install.Timestamp.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
        }

        public TracedInstall Source { get; }
        public string Name { get; }
        public string Date { get; }

        /// <summary>True quando o log esta marcado para a mesclagem.</summary>
        public bool Selected { get; set; }
    }
}

/// <summary>
/// Os candidatos de sobra que a varredura encontra para o programa de um log.
///
/// Vive aqui porque duas telas precisam dele: a janela do log (botao
/// "Advanced Uninstall") e o modulo da base de logs. A regra e uma so.
/// </summary>
public static class TracedLogCandidates
{
    /// <summary>Varre o sistema e devolve os candidatos do programa do log.</summary>
    /// <param name="log">O log do programa.</param>
    public static List<LeftoverItem> Scan(TracedInstall log)
    {
        var program = ProgramScanner
            .Scan(new ProgramScanner.ScanOptions { RequireUninstallString = false })
            .FirstOrDefault(p => p.DisplayName.Equals(log.ProgramName, StringComparison.CurrentCultureIgnoreCase));

        if (program is null) return new List<LeftoverItem>();

        return LeftoverScanner.Scan(program, LeftoverScanOptions.Default).ToList();
    }
}
