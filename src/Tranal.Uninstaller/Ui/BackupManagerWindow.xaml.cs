using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Localization;

namespace Tranal.Uninstaller.Ui;

/// <summary>
/// Gerenciador de Backups do Tranal Uninstaller Pro (equivalente ao
/// "Backup Manager" do Revo Uninstaller Pro).
///
/// Lista os pontos de restauracao criados pelas remocoes, permite restaurar
/// tudo ou apenas os itens marcados, e exclui pontos antigos por data.
/// A restauracao personalizada e a exclusao por data acontecem em paineis
/// dentro desta propria janela.
/// </summary>
public partial class BackupManagerWindow : Window
{
    /// <summary>Texto do resultado da exclusao de pontos de restauracao.</summary>
    private static string DeletedPointsMessage => Loc.Get("Backup.DeletedPointsMessage");

    /// <summary>Aviso exibido quando nenhum item foi marcado para restauracao.</summary>
    private static string NoItemsSelectedMessage => Loc.Get("Backup.NoItemsSelected");

    private readonly ObservableCollection<RestorePointRow> _points = new();
    private readonly ObservableCollection<BackupItemRow> _items = new();
    private RestorePoint? _current;

    /// <summary>Cria a janela do Gerenciador de Backups.</summary>
    public BackupManagerWindow()
    {
        InitializeComponent();

        // Window.Title e GridViewColumn.Header nao aceitam binding: texto via codigo.
        Title = Loc.Get("Backup.Title");

        BuildPointsView();
        BuildItemsView();

        PointsList.ItemsSource = _points;
        ItemsList.ItemsSource = _items;

        // A data escolhida pelo usuario usa o padrao curto do idioma ativo.
        var pattern = CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern;
        CustomDateHint.Text = pattern;
        CustomDateBox.Text = DateTime.Now.ToString(pattern, CultureInfo.CurrentCulture);
        PeriodWeekRadio.IsChecked = true;

        LoadPoints();
    }

    /// <summary>Ponto de restauracao selecionado na lista.</summary>
    private RestorePointRow? SelectedPoint => PointsList.SelectedItem as RestorePointRow;

    /// <summary>
    /// Monta as colunas da lista de pontos: nome, operacao, tamanho, data,
    /// hora e quantidade de itens.
    /// </summary>
    private void BuildPointsView()
    {
        var grid = new GridView();

        grid.Columns.Add(new GridViewColumn
        {
            Header = Loc.Get("Backup.Column.Name"),
            Width = 240,
            DisplayMemberBinding = new Binding("Name"),
        });

        grid.Columns.Add(new GridViewColumn
        {
            Header = Loc.Get("Backup.Column.Operation"),
            Width = 160,
            DisplayMemberBinding = new Binding("Operation"),
        });

        grid.Columns.Add(new GridViewColumn
        {
            Header = Loc.Get("Backup.Column.Size"),
            Width = 90,
            DisplayMemberBinding = new Binding("SizeText"),
        });

        grid.Columns.Add(new GridViewColumn
        {
            Header = Loc.Get("Backup.Column.Date"),
            Width = 90,
            DisplayMemberBinding = new Binding("DateText"),
        });

        grid.Columns.Add(new GridViewColumn
        {
            Header = Loc.Get("Backup.Column.Time"),
            Width = 80,
            DisplayMemberBinding = new Binding("TimeText"),
        });

        grid.Columns.Add(new GridViewColumn
        {
            Header = Loc.Get("Backup.Column.Items"),
            Width = 60,
            DisplayMemberBinding = new Binding("ItemsCount"),
        });

        ApplyHeaderStyle(grid);
        PointsList.View = grid;
    }

    /// <summary>Monta as colunas da lista de itens da restauracao personalizada.</summary>
    private void BuildItemsView()
    {
        var grid = new GridView();

        grid.Columns.Add(new GridViewColumn
        {
            Header = string.Empty,
            Width = 34,
            CellTemplate = BuildCheckTemplate(),
        });

        grid.Columns.Add(new GridViewColumn
        {
            Header = Loc.Get("Common.Type"),
            Width = 90,
            DisplayMemberBinding = new Binding("Kind"),
        });

        grid.Columns.Add(new GridViewColumn
        {
            Header = Loc.Get("Common.Path"),
            Width = 520,
            DisplayMemberBinding = new Binding("OriginalPath"),
        });

        grid.Columns.Add(new GridViewColumn
        {
            Header = Loc.Get("Common.Size"),
            Width = 90,
            DisplayMemberBinding = new Binding("SizeText"),
        });

        ApplyHeaderStyle(grid);
        ItemsList.View = grid;
    }

    /// <summary>Aplica o cabecalho no estilo do tema (o padrao do WPF destoa).</summary>
    private static void ApplyHeaderStyle(GridView grid)
    {
        foreach (var column in grid.Columns)
        {
            var header = new GridViewColumnHeader { Content = column.Header, Tag = column };
            if (Application.Current.TryFindResource("ListViewHeaderStyle") is Style style)
                header.Style = style;
            column.Header = header;
        }
    }

    /// <summary>Template da coluna de marcacao: a caixa liga no Selected do item.</summary>
    private static DataTemplate BuildCheckTemplate()
    {
        var xaml =
            "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>"
            + "<CheckBox IsChecked='{Binding Selected, Mode=TwoWay}' VerticalAlignment='Center' />"
            + "</DataTemplate>";
        return (DataTemplate)XamlReader.Parse(xaml);
    }

    /// <summary>Recarrega a lista de pontos de restauracao.</summary>
    private void LoadPoints()
    {
        _points.Clear();

        try
        {
            foreach (var point in BackupManager.List())
                _points.Add(new RestorePointRow(point));
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }

        EmptyText.Visibility = _points.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateCommandState();
    }

    /// <summary>Habilita os comandos que dependem de um ponto selecionado.</summary>
    private void UpdateCommandState()
    {
        var hasSelection = SelectedPoint is not null;

        RestoreAllButton.IsEnabled = hasSelection;
        CustomRestoreButton.IsEnabled = hasSelection;
        DeleteButton.IsEnabled = hasSelection;
        DeleteAllButton.IsEnabled = _points.Count > 0;
        DeleteByTimeButton.IsEnabled = _points.Count > 0;
    }

    /// <summary>Mostra um dos paineis da janela (lista, restauracao ou exclusao).</summary>
    private void ShowPanel(Grid panel)
    {
        PointsPanel.Visibility = panel == PointsPanel ? Visibility.Visible : Visibility.Collapsed;
        CustomRestorePanel.Visibility = panel == CustomRestorePanel ? Visibility.Visible : Visibility.Collapsed;
        DeleteByTimePanel.Visibility = panel == DeleteByTimePanel ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Carrega o ponto selecionado do disco (os dados podem ter sido apagados).</summary>
    private RestorePoint? LoadSelected()
    {
        var row = SelectedPoint;
        if (row is null) return null;

        var point = BackupManager.Load(row.Point.Id);
        if (point is null)
            StatusText.Text = Loc.Get("Backup.RestorePointDataNotFound");

        return point;
    }

    /// <summary>Restaurar tudo: devolve ao sistema todos os itens do ponto.</summary>
    private void RestoreAllButton_Click(object sender, RoutedEventArgs e)
    {
        var point = LoadSelected();
        if (point is null) return;

        var answer = MessageBox.Show(
            this,
            Loc.Get("Backup.RestoreConfirm", point.Operation, point.Name),
            Loc.Get("Backup.Title"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes) return;

        Restore(point, null);
    }

    /// <summary>Abre o painel de restauracao personalizada do ponto selecionado.</summary>
    private void CustomRestoreButton_Click(object sender, RoutedEventArgs e)
    {
        var point = LoadSelected();
        if (point is null) return;

        _current = point;
        _items.Clear();
        foreach (var item in point.Items)
            _items.Add(new BackupItemRow(item));

        CustomRestoreTitle.Text =
            $"{Loc.Get("Backup.CustomRestore")}: {point.Name} — {point.CreatedAt:dd/MM/yyyy HH:mm}";

        CustomEmptyText.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        ShowPanel(CustomRestorePanel);
        StatusText.Text = CustomRestoreTitle.Text;
    }

    /// <summary>Restaurar somente os itens marcados na restauracao personalizada.</summary>
    private void RestoreSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null) return;

        var selected = _items.Where(i => i.Selected).Select(i => i.Item).ToList();
        if (selected.Count == 0)
        {
            StatusText.Text = NoItemsSelectedMessage;
            return;
        }

        Restore(_current, selected);
    }

    /// <summary>Marca todos os itens da restauracao personalizada.</summary>
    private void SelectAllItemsButton_Click(object sender, RoutedEventArgs e) => SetAllItems(true);

    /// <summary>Desmarca todos os itens da restauracao personalizada.</summary>
    private void DeselectAllItemsButton_Click(object sender, RoutedEventArgs e) => SetAllItems(false);

    /// <summary>Altera a marcacao de todos os itens da restauracao personalizada.</summary>
    private void SetAllItems(bool selected)
    {
        foreach (var item in _items)
            item.Selected = selected;
    }

    /// <summary>Volta para a lista de pontos de restauracao.</summary>
    private void BackFromCustomButton_Click(object sender, RoutedEventArgs e)
    {
        _current = null;
        ShowPanel(PointsPanel);
        UpdateCommandState();
    }

    /// <summary>Executa a restauracao e escreve o resultado na linha de status.</summary>
    private void Restore(RestorePoint point, IEnumerable<BackupItem>? selected)
    {
        try
        {
            var result = BackupManager.Restore(point, selected);

            StatusText.Text = Loc.Get("Backup.RestoreDone", result.Restored, result.Failed);
            if (result.Failed > 0)
                StatusText.Text += " — " + Loc.Get("Backup.RestoreFailed");

            LoadPoints();
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    /// <summary>Exclui o ponto de restauracao selecionado.</summary>
    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var row = SelectedPoint;
        if (row is null) return;

        var answer = MessageBox.Show(
            this,
            Loc.Get("Backup.DeleteConfirm", row.Name),
            Loc.Get("Backup.Title"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer != MessageBoxResult.Yes) return;

        try
        {
            BackupManager.Delete(row.Point.Id);
            LoadPoints();
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    /// <summary>Exclui todos os pontos de restauracao.</summary>
    private void DeleteAllButton_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(
            this,
            Loc.Get("Backup.DeleteAllConfirm"),
            Loc.Get("Backup.Title"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer != MessageBoxResult.Yes) return;

        try
        {
            var count = BackupManager.DeleteAll();
            LoadPoints();
            StatusText.Text = string.Format(CultureInfo.CurrentCulture, DeletedPointsMessage, count);
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    /// <summary>Abre o painel de exclusao por data.</summary>
    private void DeleteByTimeButton_Click(object sender, RoutedEventArgs e)
    {
        ShowPanel(DeleteByTimePanel);
    }

    /// <summary>Volta para a lista de pontos de restauracao.</summary>
    private void BackFromDeleteByTimeButton_Click(object sender, RoutedEventArgs e)
    {
        ShowPanel(PointsPanel);
        UpdateCommandState();
    }

    /// <summary>Exclui todos os pontos de restauracao anteriores ao periodo escolhido.</summary>
    private void DeleteByTimeConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        var cutoff = ComputeCutoff();
        if (cutoff is null)
        {
            StatusText.Text = Loc.Get("Common.Error");
            return;
        }

        try
        {
            var count = BackupManager.DeleteOlderThan(cutoff.Value);
            LoadPoints();
            ShowPanel(PointsPanel);
            StatusText.Text = string.Format(CultureInfo.CurrentCulture, DeletedPointsMessage, count);
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    /// <summary>
    /// Data de corte do periodo escolhido pelo usuario. Null quando a data
    /// personalizada nao pode ser lida.
    /// </summary>
    private DateTime? ComputeCutoff()
    {
        var now = DateTime.Now;

        if (PeriodCustomRadio.IsChecked == true)
        {
            var pattern = CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern;
            return DateTime.TryParseExact(
                CustomDateBox.Text.Trim(),
                pattern,
                CultureInfo.CurrentCulture,
                DateTimeStyles.None,
                out var custom)
                ? custom
                : null;
        }

        if (PeriodTwoWeeksRadio.IsChecked == true) return now.AddDays(-14);
        if (Period30DaysRadio.IsChecked == true) return now.AddDays(-30);
        if (Period90DaysRadio.IsChecked == true) return now.AddDays(-90);
        if (Period180DaysRadio.IsChecked == true) return now.AddDays(-180);
        if (PeriodOneYearRadio.IsChecked == true) return now.AddDays(-365);

        return now.AddDays(-7);
    }
}

/// <summary>
/// Linha da lista de pontos de restauracao exibida no Gerenciador de Backups.
/// </summary>
public sealed class RestorePointRow
{
    /// <summary>Cria a linha a partir do ponto de restauracao.</summary>
    /// <param name="point">Ponto de restauracao exibido na linha.</param>
    public RestorePointRow(RestorePoint point) => Point = point;

    /// <summary>Ponto de restauracao exibido na linha.</summary>
    public RestorePoint Point { get; }

    /// <summary>Nome do ponto de restauracao.</summary>
    public string Name => Point.Name;

    /// <summary>Operacao registrada: itens de registro, arquivos ou os dois.</summary>
    public string Operation => Point.Operation;

    /// <summary>Tamanho total guardado, em texto legivel.</summary>
    public string SizeText => ByteFormatter.Format(Point.TotalSize);

    /// <summary>Data de criacao do ponto.</summary>
    public string DateText => Point.CreatedAt.ToString("dd/MM/yyyy");

    /// <summary>Hora de criacao do ponto.</summary>
    public string TimeText => Point.CreatedAt.ToString("HH:mm:ss");

    /// <summary>Quantidade total de itens guardados (registro + arquivos).</summary>
    public int ItemsCount => Point.RegistryItemCount + Point.FileItemCount;
}

/// <summary>
/// Item de backup exibido na restauracao personalizada: liga a marcacao do
/// usuario ao item guardado pelo ponto de restauracao.
/// </summary>
public sealed class BackupItemRow : INotifyPropertyChanged
{
    private bool _selected;

    /// <summary>Cria a linha a partir do item guardado pelo ponto de restauracao.</summary>
    /// <param name="item">Item original do ponto de restauracao.</param>
    public BackupItemRow(BackupItem item)
    {
        Item = item;
        _selected = true;
    }

    /// <summary>Item original do ponto de restauracao (e o que sera restaurado).</summary>
    public BackupItem Item { get; }

    /// <summary>Tipo do item: "Arquivo", "Pasta" ou "Registro".</summary>
    public string Kind => Item.Kind;

    /// <summary>Caminho original do item, como estava antes da remocao.</summary>
    public string OriginalPath => Item.OriginalPath;

    /// <summary>Tamanho do item em texto legivel (vazio para itens de registro).</summary>
    public string SizeText => Item.Kind == "Registro" ? string.Empty : ByteFormatter.Format(Item.Size);

    /// <summary>True quando o usuario marcou o item para restauracao.</summary>
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

    /// <summary>Dispara quando a marcacao do item muda.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;
}
