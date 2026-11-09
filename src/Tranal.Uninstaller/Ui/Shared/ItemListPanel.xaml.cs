using System.Collections;
using System.Diagnostics;
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

        BuildContextMenu();
    }

    // ------------------------------------------------------------------
    // Menu de contexto da lista
    // ------------------------------------------------------------------

    /// <summary>
    /// Menu de contexto da lista: marcacao em lote e os verbos do shell.
    /// Corresponde ao menu de contexto do "Junk Files Cleaner" do Revo, que
    /// deixa marcar e desmarcar os itens direto da lista.
    /// </summary>
    private void BuildContextMenu()
    {
        var menu = new ContextMenu();

        menu.Items.Add(ContextItem("ContextMenu.CheckAllItems", "CtxCheckAll",
            () => SetChecked(true, selectedOnly: false)));
        menu.Items.Add(ContextItem("ContextMenu.UncheckAllItems", "CtxUncheckAll",
            () => SetChecked(false, selectedOnly: false)));
        menu.Items.Add(ContextItem("ContextMenu.CheckSelectedItems", "CtxCheckSelected",
            () => SetChecked(true, selectedOnly: true)));
        menu.Items.Add(ContextItem("ContextMenu.UncheckSelectedItems", "CtxUncheckSelected",
            () => SetChecked(false, selectedOnly: true)));

        menu.Items.Add(new Separator());

        menu.Items.Add(ContextItem("ContextMenu.OpenContainingFolder", "CtxOpenContainingFolder",
            OpenContainingFolder));
        menu.Items.Add(ContextItem("ContextMenu.Properties", "CtxProperties",
            ShowProperties));

        // "Search at %s for": o rotulo mostra o nome do item selecionado, como
        // no original. O texto e montado quando o menu abre.
        var search = ContextItem("ContextMenu.SearchWeb", "CtxSearchWeb", SearchWeb);
        menu.Items.Add(search);

        menu.Opened += (_, _) =>
        {
            var term = SelectedTerm();
            search.Header = term.Length > 0
                ? Loc.Get("ContextMenu.SearchWeb", term)
                : Loc.Get("ContextMenu.SearchWebNoSelection");
            search.IsEnabled = term.Length > 0;
        };

        ItemsList.ContextMenu = menu;
    }

    private static MenuItem ContextItem(string key, string automationId, Action action)
    {
        var item = new MenuItem { Header = Loc.Get(key) };
        AutomationProperties.SetAutomationId(item, automationId);
        item.Click += (_, _) => action();
        return item;
    }

    /// <summary>
    /// Marca ou desmarca os itens da lista. Com <c>selectedOnly</c> valente so
    /// os itens selecionados na lista sao alterados; sem ele, todos.
    /// </summary>
    private void SetChecked(bool value, bool selectedOnly)
    {
        var source = selectedOnly ? ItemsList.SelectedItems : ItemsList.Items;

        foreach (var item in source)
        {
            TrySetChecked(item, value);
        }
    }

    /// <summary>
    /// Define a marcacao de um item. Cada ferramenta usa um tipo de linha
    /// proprio: a marcacao e procurada pelo nome da propriedade ("Selected" ou
    /// "IsChecked") ou pela interface <see cref="ISelectableRow"/>.
    /// </summary>
    private static bool TrySetChecked(object? item, bool value)
    {
        if (item is null) return false;

        if (item is ISelectableRow selectable)
        {
            selectable.IsChecked = value;
            return true;
        }

        var property = item.GetType().GetProperty("Selected")
            ?? item.GetType().GetProperty("IsChecked");
        if (property is null || !property.CanWrite) return false;

        property.SetValue(item, value);
        return true;
    }

    /// <summary>Caminho do primeiro item selecionado, quando houver.</summary>
    private string? SelectedPath()
    {
        foreach (var item in ItemsList.SelectedItems)
        {
            if (item is null) continue;

            var value = item.GetType().GetProperty("Path")?.GetValue(item) as string;
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }

        return null;
    }

    /// <summary>Abre a pasta que contem o item selecionado.</summary>
    private void OpenContainingFolder()
    {
        var path = SelectedPath();
        if (path is null) return;

        try
        {
            var folder = System.IO.Directory.Exists(path)
                ? path
                : System.IO.Path.GetDirectoryName(path) ?? string.Empty;

            if (folder.Length == 0) return;

            Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"")
            {
                UseShellExecute = true,
            });
        }
        catch { /* o shell nao aceitou o caminho: nada a fazer */ }
    }

    /// <summary>Abre as propriedades do item selecionado.</summary>
    private void ShowProperties()
    {
        var path = SelectedPath();
        if (path is null) return;

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, Verb = "properties" });
        }
        catch
        {
            // Nem todo manipulador tem o verbo "properties": nesse caso o
            // item e apenas selecionado no Explorer.
            try { OpenContainingFolder(); }
            catch { /* nada a fazer */ }
        }
    }

    /// <summary>
    /// Procura o item selecionado na web. E o item "Search at %s for" do
    /// menu de contexto do Revo: o termo e o nome do arquivo ou da pasta.
    /// </summary>
    private void SearchWeb()
    {
        var term = SelectedTerm();
        if (term.Length == 0) return;

        var url = Core.WebSearch.BuildUrl(term, Core.Settings.AppSettingsStore.Current.WebSearchEngine);
        if (url.Length == 0) return;

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { /* o navegador padrao nao aceitou o endereco */ }
    }

    /// <summary>Nome do item selecionado, sem a extensao (o termo da busca).</summary>
    private string SelectedTerm()
    {
        var path = SelectedPath();
        if (path is null) return string.Empty;

        try
        {
            return System.IO.Directory.Exists(path)
                ? System.IO.Path.GetFileName(path.TrimEnd('\\'))
                : System.IO.Path.GetFileNameWithoutExtension(path);
        }
        catch { return string.Empty; }
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

        // O conteudo mudou: quem mostra a pre-visualizacao da barra lateral
        // precisa saber. O painel nao sabe qual e a ferramenta — quem sabe e
        // quem o criou.
        ContentChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Dispara quando o conteudo da lista muda.
    ///
    /// A pre-visualizacao da barra lateral (o <c>PanelPreview</c> do nucleo)
    /// mostra o que o painel ja carregou: e este evento que avisa que ha
    /// coisa nova para mostrar.
    /// </summary>
    public event EventHandler? ContentChanged;

    /// <summary>
    /// Quantos itens a lista esta mostrando.
    /// </summary>
    public int ItemCount
    {
        get
        {
            try
            {
                var fonte = ItemsList.ItemsSource;
                if (fonte is null) return 0;

                // A fonte da lista e uma colecao na maioria das vezes: o
                // proprio tamanho ja e a resposta.
                if (fonte is System.Collections.ICollection colecao) return colecao.Count;

                return ItemsList.Items.Count;
            }
            catch
            {
                // A lista pode mudar enquanto e contada: a pre-visualizacao
                // mostra o que deu, e nao derruba o painel.
                return 0;
            }
        }
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
