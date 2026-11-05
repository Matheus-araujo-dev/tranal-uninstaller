using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Localization;

namespace Tranal.Uninstaller.Ui;

/// <summary>
/// A janela de escolha de colunas.
///
/// Serve a dois usos, que no Revo sao duas telas diferentes: "Columns"
/// (15054), que escolhe as colunas da lista, e "Select the columns you
/// want to export:" (15066), que escolhe as colunas da exportacao.
///
/// As colunas vem do catalogo unico (<see cref="ProgramColumns"/>): a tela
/// e a exportacao nunca discordam sobre o que e uma coluna.
/// </summary>
public sealed partial class ColumnsWindow : Window
{
    /// <summary>
    /// Uma coluna da janela. A marcacao e ligada em modo bidirecional: o
    /// valor vem do usuario e e lido de volta no OK.
    /// </summary>
    private sealed class Item
    {
        public ProgramColumn Id { get; init; }
        public string Label { get; init; } = string.Empty;

        /// <summary>True quando a coluna esta marcada.</summary>
        public bool IsChecked { get; set; }

        /// <summary>True quando a coluna nao pode ser desmarcada.</summary>
        public bool Locked { get; init; }

        /// <summary>False quando a coluna e fixa e nao pode ser desmarcada.</summary>
        public bool CanToggle => !Locked;
    }

    private readonly ObservableCollection<Item> _items = new();

    /// <summary>
    /// True quando a janela escolhe as colunas da exportacao. Na lista, a
    /// coluna do nome nunca pode sumir; na exportacao, nao ha coluna fixa.
    /// </summary>
    private readonly bool _forExport;

    /// <summary>As colunas escolhidas (na ordem do catalogo).</summary>
    public IReadOnlyList<ProgramColumn> SelectedColumns { get; private set; } = Array.Empty<ProgramColumn>();

    /// <param name="forExport">True para escolher as colunas da exportacao.</param>
    /// <param name="initial">As colunas que comecam marcados.</param>
    public ColumnsWindow(bool forExport = false, IReadOnlyList<ProgramColumn>? initial = null)
    {
        InitializeComponent();

        _forExport = forExport;

        Title = Loc.Get(forExport ? "Columns.ExportTitle" : "Columns.Title");
        PromptText.Text = Loc.Get(forExport ? "Columns.ExportPrompt" : "Columns.Prompt");
        OkButton.Content = Loc.Get(forExport ? "Columns.Export" : "Common.Ok");

        // O seletor estavel de automacao precisa distinguir os dois usos da
        // janela: "ColumnsOk" na lista, "ColumnsExport" na exportacao.
        AutomationProperties.SetAutomationId(OkButton, forExport ? "ColumnsExport" : "ColumnsOk");

        var marcados = initial?.ToHashSet() ?? new HashSet<ProgramColumn>();

        foreach (var info in ProgramColumns.All)
        {
            // Na lista a coluna do nome e fixa: sem ela a lista perde o
            // sentido. Na exportacao o usuario escolhe livremente.
            var travada = !forExport && info.Id == ProgramColumn.Name;

            _items.Add(new Item
            {
                Id = info.Id,
                Label = ProgramColumns.Label(info.Id),
                IsChecked = travada || marcados.Contains(info.Id),
                Locked = travada,
            });
        }

        ColumnsList.ItemsSource = _items;
    }

    /// <summary>
    /// True quando o usuario escolheu pelo menos uma coluna. Na exportacao
    /// isso e obrigatorio (15298: "You must select at least one column to
    /// export!").
    /// </summary>
    private bool HasSelection => _items.Any(i => i.IsChecked);

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (!HasSelection)
        {
            WarningText.Text = Loc.Get("Columns.AtLeastOne");
            WarningText.Visibility = Visibility.Visible;
            return;
        }

        SelectedColumns = ProgramColumns.All
            .Where(c => _items.Any(i => i.Id == c.Id && i.IsChecked))
            .Select(c => c.Id)
            .ToList();

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
