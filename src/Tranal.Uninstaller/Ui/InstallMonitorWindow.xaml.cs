using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Modules;

namespace Tranal.Uninstaller.Ui;

/// <summary>
/// O monitor de instalacao: a tela que acompanha, ao vivo, tudo o que o
/// instalador cria no sistema.
///
/// E o "Install Monitor" do Revo: o usuario comeca o monitoramento, instala o
/// programa por conta propria e, ao parar, o Tranal grava o log do que mudou.
/// A partir dai o modulo de programas monitorados sabe o que remover.
///
/// A REGRA mora no nucleo (<see cref="InstallMonitor"/>): a tela inicia, para e
/// mostra — sem reescrever a deteccao.
/// </summary>
public sealed partial class InstallMonitorWindow : Window
{
    /// <summary>
    /// Uma linha do que foi detectado.
    /// </summary>
    private sealed class MonitorRow
    {
        /// <summary>O tipo do item (arquivo, pasta ou chave de registro).</summary>
        public string Category { get; init; } = string.Empty;

        /// <summary>O caminho do item.</summary>
        public string Path { get; init; } = string.Empty;

        /// <summary>O tamanho do item (quando ele tem).</summary>
        public string Size { get; init; } = string.Empty;
    }

    /// <summary>O monitoramento em andamento (nulo quando nenhum esta rodando).</summary>
    private InstallMonitor? _monitor;

    /// <summary>O que foi detectado ate agora.</summary>
    private readonly ObservableCollection<MonitorRow> _rows = new();

    /// <summary>O temporizador que atualiza a lista enquanto o monitor roda.</summary>
    private readonly DispatcherTimer _timer;

    /// <summary>
    /// Cria a janela.
    /// </summary>
    /// <param name="programa">O programa que vem escolhido, quando ha.</param>
    public InstallMonitorWindow(InstalledProgram? programa = null)
    {
        InitializeComponent();

        // Window.Title nao aceita binding: texto via codigo.
        Title = Loc.Get("Monitor.Title");

        ProgramNameBox.Text = programa?.DisplayName ?? string.Empty;
        ItemsList.ItemsSource = _rows;
        BuildColunas();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => AtualizarLista();

        StatusText.Text = Loc.Get("Monitor.Waiting");
    }

    /// <summary>
    /// Monta as colunas da lista do que foi detectado.
    /// </summary>
    private void BuildColunas()
    {
        var grid = new GridView();

        grid.Columns.Add(new GridViewColumn
        {
            Header = Loc.Get("Monitor.Column.Type"),
            Width = 110,
            DisplayMemberBinding = new Binding("Category"),
        });

        grid.Columns.Add(new GridViewColumn
        {
            Header = Loc.Get("Monitor.Column.Path"),
            Width = 480,
            DisplayMemberBinding = new Binding("Path"),
        });

        grid.Columns.Add(new GridViewColumn
        {
            Header = Loc.Get("Monitor.Column.Size"),
            Width = 110,
            DisplayMemberBinding = new Binding("Size"),
        });

        foreach (var column in grid.Columns)
        {
            var cabecalho = new GridViewColumnHeader { Content = column.Header, Tag = column };
            if (Application.Current.TryFindResource("ListViewHeaderStyle") is Style style)
                cabecalho.Style = style;
            column.Header = cabecalho;
        }

        ItemsList.View = grid;
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_monitor is not null) return;

        var nome = ProgramNameBox.Text.Trim();
        if (nome.Length == 0)
        {
            StatusText.Text = Loc.Get("Monitor.NameRequired");
            return;
        }

        _monitor = InstallMonitor.Start(nome);
        if (_monitor is null)
        {
            StatusText.Text = Loc.Get("Monitor.StartFailed");
            return;
        }

        _rows.Clear();
        _timer.Start();

        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;

        StatusText.Text = Loc.Get("Monitor.Running", nome);
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        var monitor = _monitor;
        if (monitor is null) return;

        _timer.Stop();
        _monitor = null;

        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;

        // A captura final e a gravacao do log custam alguns segundos: a
        // janela nao pode congelar enquanto isso acontece.
        _ = Task.Run(() =>
        {
            var install = monitor.StopAndSave();

            Dispatcher.Invoke(() =>
            {
                StatusText.Text = install is null
                    ? Loc.Get("Monitor.NothingFound")
                    : Loc.Get("Monitor.Saved", install.ProgramName,
                        install.FileCount, install.FolderCount, install.RegistryKeyCount);
            });
        });

        AtualizarLista();
    }

    /// <summary>
    /// Atualiza a lista com o que foi detectado ate agora.
    /// </summary>
    private void AtualizarLista()
    {
        var monitor = _monitor;
        if (monitor is null) return;

        var arquivos = monitor.Files;
        var pastas = monitor.Folders;
        var chaves = monitor.RegistryKeys;

        _rows.Clear();

        foreach (var arquivo in arquivos)
        {
            _rows.Add(new MonitorRow
            {
                Category = Loc.Get("Monitor.KindFile"),
                Path = arquivo.Path,
                Size = FormatarTamanho(arquivo.SizeBytes),
            });
        }

        foreach (var pasta in pastas)
        {
            _rows.Add(new MonitorRow
            {
                Category = Loc.Get("Monitor.KindFolder"),
                Path = pasta,
                Size = string.Empty,
            });
        }

        foreach (var chave in chaves)
        {
            _rows.Add(new MonitorRow
            {
                Category = Loc.Get("Monitor.KindRegistry"),
                Path = chave,
                Size = string.Empty,
            });
        }

        StatusText.Text = Loc.Get("Monitor.Count",
            arquivos.Count, pastas.Count, chaves.Count);
    }

    /// <summary>
    /// O tamanho em bytes, em kilo e em mega.
    /// </summary>
    /// <param name="bytes">O tamanho em bytes.</param>
    private static string FormatarTamanho(long bytes)
    {
        if (bytes <= 0) return string.Empty;

        if (bytes >= 1024 * 1024)
            return (bytes / 1024d / 1024d).ToString("F1", CultureInfo.InvariantCulture) + " MB";

        return (bytes / 1024d).ToString("F0", CultureInfo.InvariantCulture) + " KB";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        _monitor?.Stop();
        _monitor = null;

        DialogResult = false;
        Close();
    }
}
