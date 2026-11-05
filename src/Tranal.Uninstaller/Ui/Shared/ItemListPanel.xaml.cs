using System.Collections;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using Tranal.Uninstaller.Core.Localization;

namespace Tranal.Uninstaller.Ui.Shared;

/// <summary>Definicao de uma coluna do painel generico de lista.</summary>
public sealed class ColumnDef
{
    public string Header { get; init; } = string.Empty;
    public double Width { get; init; } = 140;
    public string BindingPath { get; init; } = string.Empty;

    /// <summary>Quando true, a celula mostra uma caixa de marcacao ligada ao item.</summary>
    public bool IsCheckable { get; init; }

    /// <summary>Propriedade ligada a caixa de marcacao (padrao: "Selected").</summary>
    public string CheckBindingPath { get; init; } = "Selected";
}

/// <summary>Definicao de um botao da barra de comandos do painel.</summary>
public sealed class CommandDef
{
    public string AutomationId { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public Action? Action { get; init; }
    public bool IsPrimary { get; init; }
}

/// <summary>
/// Item de lista que expoe uma caixa de marcacao.
/// Usado pelos modulos e ferramentas que oferecem selecao.
/// </summary>
public interface ISelectableRow
{
    bool IsChecked { get; set; }
}

/// <summary>
/// Painel de lista generico usado pelos modulos de dados (Programas Monitorados,
/// Windows Apps, Extensoes do Navegador, Base de Logs) e pelas ferramentas do
/// painel Tools. A coluna e a barra de comandos sao definidas em runtime, o que
/// evita uma janela quase igual para cada modulo.
/// </summary>
public partial class ItemListPanel : UserControl
{
    private GridView? _grid;

    public ItemListPanel()
    {
        InitializeComponent();
    }

    /// <summary>Titulo exibido na faixa superior.</summary>
    public string PanelTitle
    {
        get => TitleText.Text;
        set => TitleText.Text = value;
    }

    /// <summary>Texto da linha de status.</summary>
    public string PanelStatus
    {
        get => PanelStatusText.Text;
        set => PanelStatusText.Text = value;
    }

    /// <summary>Itens selecionaos na lista.</summary>
    public IList SelectedItems => ItemsList.SelectedItems;

    /// <summary>Todos os itens apresentados.</summary>
    public IEnumerable Items => ItemsList.ItemsSource is IEnumerable e ? e : Array.Empty<object>();

    /// <summary>Define titulo, colunas e barra de comandos.</summary>
    public void Configure(string title, IReadOnlyList<ColumnDef> columns, IReadOnlyList<CommandDef> commands)
    {
        PanelTitle = title;

        var grid = new GridView();
        foreach (var column in columns)
        {
            var gvColumn = new GridViewColumn { Header = column.Header, Width = column.Width };

            if (column.IsCheckable)
            {
                gvColumn.CellTemplate = BuildCheckableTemplate(column.CheckBindingPath);
            }
            else if (!string.IsNullOrEmpty(column.BindingPath))
            {
                gvColumn.DisplayMemberBinding = new Binding(column.BindingPath);
            }

            grid.Columns.Add(gvColumn);
        }

        // cabecalho no estilo do tema (o padrao do WPF e claro e destoa no tema escuro)
        foreach (var column in grid.Columns)
        {
            var header = new GridViewColumnHeader { Content = column.Header, Tag = column };
            if (TryFindResource("ListViewHeaderStyle") is Style style) header.Style = style;
            column.Header = header;
        }

        ItemsList.View = grid;
        _grid = grid;

        // A faixa do cabecalho que sobra (quando as colunas nao preenchem a
        // largura) e pintada pelo WPF com a cor padrao do sistema (branca) e
        // destoa no tema escuro. A ultima coluna e esticada para cobrir a sobra.
        SizeChanged += (_, _) => StretchLastColumn();
        Loaded += (_, _) => StretchLastColumn();

        CommandBar.Items.Clear();
        foreach (var command in commands)
        {
            var button = new Button
            {
                Content = command.Text,
                Style = (Style)FindResource("FlatButton"),
                Margin = new Thickness(0, 0, 6, 0),
                Padding = new Thickness(9, 4, 9, 4),
            };
            AutomationProperties.SetAutomationId(button, command.AutomationId);
            if (command.Action is not null)
            {
                var action = command.Action;
                button.Click += (_, _) => action();
            }
            CommandBar.Items.Add(button);
        }
    }

    /// <summary>Estica a ultima coluna para cobrir a largura restante do painel.</summary>
    private void StretchLastColumn()
    {
        if (_grid is null || _grid.Columns.Count == 0) return;

        var fixedWidth = 0.0;
        for (var i = 0; i < _grid.Columns.Count - 1; i++)
            fixedWidth += _grid.Columns[i].Width;

        var available = ItemsList.ActualWidth - fixedWidth - 2;
        if (available > 40)
            _grid.Columns[^1].Width = available;
    }

    /// <summary>Substitui os itens da lista.</summary>
    public void SetItems(IEnumerable items)
    {
        ItemsList.ItemsSource = items;
    }

    /// <summary>True quando o item implementa ISelectableRow e esta marcado.</summary>
    public static bool IsChecked(object? item) =>
        item is ISelectableRow selectable && selectable.IsChecked;

    private static DataTemplate BuildCheckableTemplate(string bindingPath)
    {
        // Template escrito em XAML: evita FrameworkElementFactory (obsoleto) e
        // mantem a ligacao ao Selected do item.
        var xaml =
            "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>"
            + "<CheckBox IsChecked='{Binding " + bindingPath + ", Mode=TwoWay}' "
            + "Margin='0,0,6,0' VerticalAlignment='Center' /></DataTemplate>";

        return (DataTemplate)XamlReader.Parse(xaml);
    }

    /// <summary>
    /// Executa uma operacao demorada sem travar a interface e escreve o
    /// resultado na linha de status.
    /// </summary>
    public async void RunAsync(Func<Task> work)
    {
        try
        {
            PanelStatus = Loc.Get("Status.Scanning");
            await work();
        }
        catch (Exception ex)
        {
            PanelStatus = ex.Message;
        }
    }
}
