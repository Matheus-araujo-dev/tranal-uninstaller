using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Settings;

namespace Tranal.Uninstaller.Ui;

/// <summary>
/// Desinstalacao forcada: o usuario informa o executavel (ou a pasta) do programa
/// e o Tranal varre o sistema procurando arquivos, pastas e chaves de registro
/// relacionados, permitindo revisar e remover o que foi encontrado.
/// </summary>
public partial class ForcedUninstallWindow : Window
{
    private readonly ObservableCollection<LeftoverItem> _leftovers = new();

    public ForcedUninstallWindow()
    {
        InitializeComponent();

        // Window.Title e GridViewColumn.Header nao aceitam binding: texto via codigo.
        Title = Loc.Get("Forced.Title");
        if (LeftoversList.View is GridView grid && grid.Columns.Count >= 2)
        {
            grid.Columns[0].Header = Loc.Get("Common.Type");
            grid.Columns[1].Header = Loc.Get("Common.Path");
        }

        LeftoversList.ItemsSource = _leftovers;
    }

    /// <summary>Caminho informado pelo usuario (vazio quando nada foi analisado).</summary>
    public string SelectedPath => PathBox.Text.Trim();

    /// <summary>True quando a remocao forcada foi concluida.</summary>
    public bool Removed { get; private set; }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Loc.Get("Forced.BrowseTitle"),
            Filter = Loc.Get("Forced.BrowseFilter"),
            CheckFileExists = false,
        };
        if (dialog.ShowDialog(this) == true)
            PathBox.Text = dialog.FileName;
    }

    private void AnalyzeButton_Click(object sender, RoutedEventArgs e)
    {
        var path = SelectedPath;
        if (path.Length == 0)
        {
            StatusText.Text = Loc.Get("Forced.NotFound");
            return;
        }

        StatusText.Text = Loc.Get("Status.Scanning");

        var items = LeftoverScanner.ScanForced(
            path, UninstallMode.Advanced, AppSettingsStore.Current.CheckAllLeftoversByDefault);

        _leftovers.Clear();
        foreach (var item in items) _leftovers.Add(item);

        RemoveButton.IsEnabled = _leftovers.Count > 0;
        StatusText.Text = Loc.Get("Forced.Found", _leftovers.Count);
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_leftovers.Count == 0) return;

        var report = UninstallEngine.RemoveLeftovers(_leftovers);
        Removed = true;

        StatusText.Text = Loc.Get("Forced.Removed",
            report.RemovedCount, report.AbsentCount,
            report.FailedCount + report.ProtectedCount);

        _leftovers.Clear();
        RemoveButton.IsEnabled = false;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = Removed;
        Close();
    }
}
