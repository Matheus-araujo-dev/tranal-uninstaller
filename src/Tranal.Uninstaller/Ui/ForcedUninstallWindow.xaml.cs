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

        // Window.Title nao aceita binding: texto via codigo.
        Title = Loc.Get("Forced.Title");

        LeftoversTreeControl.SetItems(_leftovers);
    }

    /// <summary>Caminho informado pelo usuario (vazio quando nada foi analisado).</summary>
    public string SelectedPath => PathBox.Text.Trim();

    /// <summary>
    /// Analisa o caminho informado.
    ///
    /// Usado quando o Tranal e aberto pelo menu de contexto do Explorer: o
    /// alvo vem na linha de comando e a desinstalacao forcada ja comeca
    /// funcionando, sem o usuario precisar colar o caminho.
    /// </summary>
    public void Analyze(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        PathBox.Text = path.Trim();
        AnalyzeButton_Click(this, new RoutedEventArgs());
    }

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
            path, new LeftoverScanOptions
            {
                Mode = UninstallMode.Advanced,
                CheckAllLeftovers = AppSettingsStore.Current.CheckAllLeftoversByDefault,
            });

        _leftovers.Clear();
        foreach (var item in items) _leftovers.Add(item);
        LeftoversTreeControl.SetItems(_leftovers);

        RemoveButton.IsEnabled = _leftovers.Count > 0;
        StatusText.Text = Loc.Get("Forced.Found", _leftovers.Count);
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_leftovers.Count == 0) return;

        // A desinstalacao forcada usa as mesmas opcoes da interface e da
        // linha de comando: backup completo do registro, encerramento dos
        // executaveis, Lixeira e adiamento para o proximo reinicio.
        var options = UninstallRemovalOptions.FromSettings($"Desinstalação forçada de {SelectedPath}");

        // O backup completo do registro vem antes da remocao. Aqui nao ha
        // desinstalador nativo, entao este e o ultimo momento possivel.
        var fullBackup = UninstallEngine.PrepareFullRegistryBackup(options);

        var report = UninstallEngine.RemoveLeftovers(_leftovers, options, fullBackup);
        Removed = true;

        var status = Loc.Get("Forced.Removed",
            report.RemovedCount, report.AbsentCount,
            report.FailedCount, report.ProtectedCount,
            report.DeferredCount);

        // O que saiu de dentro das pastas que ficaram no sistema: sem esta
        // linha o resumo mostraria "0 removidos" com arquivos de fato
        // apagados — e esconderia o item que ficou para trás.
        if (report.RemovedInsideCount > 0 || report.ProtectedInsideCount > 0
            || report.FailedInsideCount > 0 || report.DeferredInsideCount > 0)
        {
            status += Environment.NewLine + Loc.Get("Forced.RemovedInside",
                report.RemovedInsideCount, report.ProtectedInsideCount,
                report.FailedInsideCount, report.DeferredInsideCount);
        }

        StatusText.Text = status;

        _leftovers.Clear();
        RemoveButton.IsEnabled = false;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = Removed;
        Close();
    }
}
