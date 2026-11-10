using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Automation;
using Tranal.Uninstaller.Core.Localization;

namespace Tranal.Uninstaller.Ui;

/// <summary>
/// A desinstalacao personalizada.
///
/// E a tela do Revo e do Wise que "varre por palavra-chave e deixa explorar a
/// pasta": o usuario escolhe o programa (pelo nome ou por uma palavra que
/// aparece nele), ve o que sobrou no sistema e decide o que remover.
///
/// A REGRA mora no nucleo: a selecao por palavra e a
/// <see cref="ForcedRemovalOptions"/>, a lista de sobras e a
/// <see cref="ManualRemoval"/>. A tela so mostra e pergunta.
///
/// Nada e removido sem a escolha do usuario: os itens que veio marcados sao os
/// de alta confianca, e o botao de remocao so age sobre o que estiver
/// marcado na hora.
/// </summary>
public sealed partial class ManualRemovalWindow : Window
{
    /// <summary>Os programas instalados (a lista que o usuario escolhe).</summary>
    private readonly IReadOnlyList<InstalledProgram> _programas;

    /// <summary>Os programas que o filtro deixou.</summary>
    private readonly ObservableCollection<InstalledProgram> _lista = new();

    /// <summary>As sobras do programa escolhido.</summary>
    private readonly ObservableCollection<LeftoverItem> _sobras = new();

    /// <summary>
    /// Cria a janela.
    /// </summary>
    /// <param name="programas">Os programas instalados.</param>
    /// <param name="selecionado">O programa que vem escolhido, quando ha.</param>
    public ManualRemovalWindow(IReadOnlyList<InstalledProgram> programas, InstalledProgram? selecionado = null)
    {
        InitializeComponent();

        // Window.Title nao aceita binding: texto via codigo.
        Title = Loc.Get("Manual.Title");

        _programas = programas;
        ProgramList.ItemsSource = _lista;
        LeftoversTreeControl.SetItems(_sobras);

        Filtrar(string.Empty);

        if (selecionado is not null)
        {
            var alvo = _lista.FirstOrDefault(p =>
                string.Equals(p.DisplayName, selecionado.DisplayName, StringComparison.OrdinalIgnoreCase));

            if (alvo is not null) ProgramList.SelectedItem = alvo;
        }
    }

    /// <summary>
    /// Filtra a lista de programas pela palavra-chave.
    ///
    /// Vazio traz a lista inteira: a palavra-chave e um filtro, nao uma
    /// obrigacao.
    /// </summary>
    /// <param name="keyword">A palavra-chave digitada.</param>
    private void Filtrar(string keyword)
    {
        _lista.Clear();

        var filtrados = keyword.Trim().Length == 0
            ? _programas
            : ManualRemoval.SelectByKeyword(_programas, keyword);

        foreach (var programa in filtrados) _lista.Add(programa);

        StatusText.Text = _lista.Count == 0
            ? Loc.Get("Manual.NoPrograms")
            : Loc.Get("Manual.Programs", _lista.Count);
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e) => Filtrar(KeywordBox.Text);

    /// <summary>
    /// Varre o programa escolhido e mostra o que sobrou dele.
    /// </summary>
    private void ProgramList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProgramList.SelectedItem is not InstalledProgram programa) return;

        StatusText.Text = Loc.Get("Status.Scanning");

        var sobras = ManualRemoval.Candidates(
            programa,
            LeftoverScanOptions.FromSettings(UninstallMode.Advanced));

        _sobras.Clear();
        foreach (var sobra in sobras) _sobras.Add(sobra);
        LeftoversTreeControl.SetItems(_sobras);

        ExportButton.IsEnabled = _sobras.Count > 0;
        RemoveButton.IsEnabled = _sobras.Count > 0;

        StatusText.Text = Loc.Get("Manual.Found", _sobras.Count, programa.DisplayName);
    }

    /// <summary>
    /// Exporta a lista de sobras em CSV.
    /// </summary>
    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_sobras.Count == 0) return;

        var dialog = new SaveFileDialog
        {
            Title = Loc.Get("Manual.ExportTitle"),
            Filter = Loc.Get("Manual.ExportFilter"),
            FileName = "sobras.csv",
        };

        if (dialog.ShowDialog(this) != true) return;

        var gravado = ManualRemoval.SaveToFile(dialog.FileName, _sobras);

        StatusText.Text = gravado
            ? Loc.Get("Manual.Exported", dialog.FileName)
            : Loc.Get("Manual.ExportFailed", dialog.FileName);
    }

    /// <summary>
    /// Remove as sobras marcadas.
    /// </summary>
    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_sobras.Count == 0) return;

        var marcados = _sobras.Where(s => s.Selected).ToList();
        if (marcados.Count == 0)
        {
            StatusText.Text = Loc.Get("Manual.NothingSelected");
            return;
        }

        var programa = ProgramList.SelectedItem as InstalledProgram;
        var nome = programa?.DisplayName ?? Loc.Get("Common.Program");

        // As mesmas opcoes da interface e da linha de comando: backup completo
        // do registro, encerramento dos executaveis, Lixeira e adiamento para
        // o proximo reinicio.
        var options = UninstallRemovalOptions.FromSettings($"Desinstalacao manual de {nome}");

        // O backup completo do registro vem antes da remocao.
        var fullBackup = UninstallEngine.PrepareFullRegistryBackup(options);

        var report = UninstallEngine.RemoveLeftovers(marcados, options, fullBackup);

        StatusText.Text = Loc.Get("Manual.Removed",
            report.RemovedCount, report.AbsentCount,
            report.FailedCount, report.ProtectedCount,
            report.DeferredCount);

        _sobras.Clear();
        LeftoversTreeControl.SetItems(_sobras);
        ExportButton.IsEnabled = false;
        RemoveButton.IsEnabled = false;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
