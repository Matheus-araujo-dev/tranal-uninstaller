using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Modules;

namespace Tranal.Uninstaller.Ui.Modules;

/// <summary>
/// Modo Cacador: o usuario arrasta o icone de um programa (atalho .lnk ou
/// executavel) sobre a janela e o Tranal identifica o programa instalado
/// correspondente, offerendo iniciar a desinstalacao.
/// </summary>
public partial class HunterView : UserControl
{
    private readonly MainWindow _host;
    private InstalledProgram? _program;

    public HunterView(MainWindow host)
    {
        InitializeComponent();
        _host = host;
        ResultText.Text = Loc.Get("Hunter.Drop");
    }

    private void Target_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void Target_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop)
            && e.Data.GetData(DataFormats.FileDrop) is string[] files
            && files.Length > 0)
        {
            Resolve(files[0]);
        }

        e.Handled = true;
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Loc.Get("Hunter.Resolved"),
            Filter = Loc.Get("Forced.BrowseFilter"),
            CheckFileExists = false,
        };
        if (dialog.ShowDialog() == true) Resolve(dialog.FileName);
    }

    /// <summary>Resolve o caminho informado e mostra o programa identificado.</summary>
    private void Resolve(string path)
    {
        var resolution = HunterMode.Resolve(path);

        if (!resolution.Found || resolution.Program is null)
        {
            _program = null;
            UninstallButton.IsEnabled = false;
            ResultText.Text = Loc.Get("Hunter.NotFound") + "\n" + resolution.Message;
            return;
        }

        _program = resolution.Program;
        UninstallButton.IsEnabled = true;

        ResultText.Text =
            $"{_program.DisplayName}  ({_program.VersionOrNoVersion})\n"
            + $"{Loc.Get("Common.Path")}: {resolution.ResolvedPath}\n"
            + $"{Loc.Get("Common.RegistryKey")}: {_program.RegistryPath}\n"
            + $"{Loc.Get("Common.InstallLocation")}: {_program.InstallLocation}";
    }

    private void UninstallButton_Click(object sender, RoutedEventArgs e)
    {
        if (_program is null) return;
        _host.StartUninstallFor(_program);
    }
}
