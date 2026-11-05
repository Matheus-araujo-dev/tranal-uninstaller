using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Localization;

namespace Tranal.Uninstaller.Ui;

/// <summary>
/// Gerenciador da lista de exclusoes: as pastas, arquivos e chaves de
/// registro que o Tranal nunca vai remover.
///
/// Corresponde ao original nas strings "Add to Exclude List" (15102),
/// "Folder to exclude" (15315), "Files and Folders to Exclude" (15316) e
/// "Registry Keys to Exclude" (15317).
///
/// A lista vale para o motor inteiro: um item excluido nao aparece na
/// varredura e o motor recusa remove-lo mesmo que ele chegue marcado por
/// outro caminho.
/// </summary>
public partial class ExcludeListWindow : Window
{
    private readonly ObservableCollection<Entry> _paths = new();
    private readonly ObservableCollection<Entry> _registry = new();

    /// <summary>O que o painel de entrada espera: um caminho ou uma chave de registro.</summary>
    private enum InputMode
    {
        None,
        Path,
        RegistryKey,
    }

    private InputMode _inputMode = InputMode.None;

    /// <summary>Cria a janela e ja mostra a lista atual.</summary>
    public ExcludeListWindow()
    {
        InitializeComponent();

        // Window.Title nao e DependencyProperty: o texto vem do catalogo por codigo.
        Title = Loc.Get("ExcludeList.Title");

        BuildViews();

        PathsList.ItemsSource = _paths;
        RegistryList.ItemsSource = _registry;

        Load();
    }

    /// <summary>
    /// Recarrega a lista do disco. A janela pode ser aberta depois de uma
    /// mudanca feita em outra tela (ou pela linha de comando).
    /// </summary>
    private void Load()
    {
        ExcludeList.Reload();

        _paths.Clear();
        _registry.Clear();

        foreach (var path in ExcludeList.Current.Paths)
            _paths.Add(new Entry(path, registry: false));

        foreach (var key in ExcludeList.Current.RegistryKeys)
            _registry.Add(new Entry(key, registry: true));

        RefreshEmptyState();
    }

    /// <summary>Grava a lista no disco e mostra o caminho do arquivo.</summary>
    private bool Save()
    {
        if (ExcludeList.Current.Save())
        {
            SetStatus(Loc.Get("ExcludeList.Saved", ExcludeList.FilePath));
            return true;
        }

        SetStatus(Loc.Get("ExcludeList.SaveFailed"));
        return false;
    }

    /// <summary>Mostra a mensagem de status no rodape da janela.</summary>
    private void SetStatus(string message) => StatusText.Text = message;

    /// <summary>Esconde a mensagem de lista vazia quando ha itens na lista.</summary>
    private void RefreshEmptyState()
    {
        PathsEmptyText.Visibility = _paths.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RegistryEmptyText.Visibility = _registry.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // -----------------------------------------------------------------
    // Comandos
    // -----------------------------------------------------------------

    /// <summary>Adiciona a pasta escolhida pelo usuario a lista de exclusoes.</summary>
    private void AddFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = Loc.Get("ExcludeList.AddFolder"),
            Multiselect = false,
        };

        if (dialog.ShowDialog(this) != true) return;

        AddPath(dialog.FolderName);
    }

    /// <summary>Abre o painel de entrada para digitar um caminho.</summary>
    private void AddPathButton_Click(object sender, RoutedEventArgs e) =>
        ShowInput(InputMode.Path, Loc.Get("ExcludeList.PathPrompt"));

    /// <summary>Abre o painel de entrada para digitar uma chave de registro.</summary>
    private void AddRegistryKeyButton_Click(object sender, RoutedEventArgs e) =>
        ShowInput(InputMode.RegistryKey, Loc.Get("ExcludeList.RegistryKeyPrompt"));

    /// <summary>
    /// Remove da lista o item selecionado. A lista da esquerda e a depaths;
    /// a da direita e a de chaves de registro.
    /// </summary>
    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (PathsList.SelectedItem is Entry path)
        {
            ExcludeList.Current.RemovePath(path.Value);
            _paths.Remove(path);
            RefreshEmptyState();
            Save();
            SetStatus(Loc.Get("ExcludeList.Removed"));
            return;
        }

        if (RegistryList.SelectedItem is Entry key)
        {
            ExcludeList.Current.RemoveRegistryKey(key.Value);
            _registry.Remove(key);
            RefreshEmptyState();
            Save();
            SetStatus(Loc.Get("ExcludeList.Removed"));
            return;
        }

        SetStatus(Loc.Get("ExcludeList.NothingSelected"));
    }

    /// <summary>
    /// Esvazia a lista. A confirmacao nunca e omitida: limpar a lista retira
    /// uma protecao, e os itens protegidos voltam a poder ser removidos.
    /// </summary>
    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        if (_paths.Count == 0 && _registry.Count == 0)
        {
            SetStatus(Loc.Get("ExcludeList.Empty"));
            return;
        }

        var answer = MessageBox.Show(
            this,
            Loc.Get("ExcludeList.ClearConfirm"),
            Loc.Get("ExcludeList.Title"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        if (answer != MessageBoxResult.Yes) return;

        ExcludeList.Current.Clear();
        _paths.Clear();
        _registry.Clear();
        RefreshEmptyState();
        Save();
        SetStatus(Loc.Get("ExcludeList.Cleared"));
    }

    /// <summary>Fecha a janela.</summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    // -----------------------------------------------------------------
    // Entrada manual
    // -----------------------------------------------------------------

    /// <summary>Mostra o painel de entrada no modo pedido.</summary>
    private void ShowInput(InputMode mode, string prompt)
    {
        _inputMode = mode;
        InputPromptText.Text = prompt;
        InputBox.Text = string.Empty;
        InputPanel.Visibility = Visibility.Visible;
        InputBox.Focus();
    }

    /// <summary>Esconde o painel de entrada sem alterar nada.</summary>
    private void InputCancelButton_Click(object sender, RoutedEventArgs e)
    {
        _inputMode = InputMode.None;
        InputPanel.Visibility = Visibility.Collapsed;
    }

    /// <summary>Confirma o que foi digitado no painel de entrada.</summary>
    private void InputOkButton_Click(object sender, RoutedEventArgs e)
    {
        var value = InputBox.Text;
        var mode = _inputMode;

        _inputMode = InputMode.None;
        InputPanel.Visibility = Visibility.Collapsed;

        if (mode == InputMode.RegistryKey) AddRegistryKey(value);
        else if (mode == InputMode.Path) AddPath(value);
    }

    // -----------------------------------------------------------------
    // Inclusao
    // -----------------------------------------------------------------

    /// <summary>Inclui um caminho (pasta ou arquivo) na lista.</summary>
    private void AddPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            SetStatus(Loc.Get("ExcludeList.NothingSelected"));
            return;
        }

        var normalized = PathUtil.Normalize(value);

        if (!ExcludeList.Current.AddPath(normalized))
        {
            SetStatus(Loc.Get("ExcludeList.AlreadyExcluded"));
            return;
        }

        _paths.Add(new Entry(normalized, registry: false));
        RefreshEmptyState();
        Save();
        SetStatus(Loc.Get("ExcludeList.Added"));
    }

    /// <summary>Inclui uma chave de registro na lista.</summary>
    private void AddRegistryKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            SetStatus(Loc.Get("ExcludeList.NothingSelected"));
            return;
        }

        if (!ExcludeList.Current.AddRegistryKey(value))
        {
            SetStatus(Loc.Get("ExcludeList.AlreadyExcluded"));
            return;
        }

        _registry.Add(new Entry(value.Trim(), registry: true));
        RefreshEmptyState();
        Save();
        SetStatus(Loc.Get("ExcludeList.Added"));
    }

    // -----------------------------------------------------------------
    // Visao
    // -----------------------------------------------------------------

    /// <summary>
    /// Monta a coluna das duas listas. O cabecalho nao aceita binding, por
    /// isso o texto vem do catalogo por codigo.
    /// </summary>
    private void BuildViews()
    {
        PathsList.View = BuildGridView(Loc.Get("ExcludeList.PathsTitle"));
        RegistryList.View = BuildGridView(Loc.Get("ExcludeList.RegistryTitle"));
    }

    /// <summary>Cria a coluna unica das listas, no estilo do tema.</summary>
    private static GridView BuildGridView(string header)
    {
        var grid = new GridView();
        grid.Columns.Add(new GridViewColumn
        {
            Header = header,
            Width = 380,
            DisplayMemberBinding = new Binding(nameof(Entry.Value)),
        });

        foreach (var column in grid.Columns)
        {
            var headerElement = new GridViewColumnHeader { Content = column.Header, Tag = column };
            if (Application.Current.TryFindResource("ListViewHeaderStyle") is Style style)
                headerElement.Style = style;
            column.Header = headerElement;
        }

        return grid;
    }

    /// <summary>Um item da lista de exclusoes.</summary>
    private sealed class Entry
    {
        public Entry(string value, bool registry)
        {
            Value = value;
            IsRegistry = registry;
        }

        /// <summary>O caminho ou a chave protegida.</summary>
        public string Value { get; }

        /// <summary>True quando o item e uma chave de registro.</summary>
        public bool IsRegistry { get; }
    }
}
