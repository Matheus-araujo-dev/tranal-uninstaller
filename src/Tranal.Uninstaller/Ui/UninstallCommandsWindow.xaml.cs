using System.Windows;
using Tranal.Uninstaller.Core.Localization;

namespace Tranal.Uninstaller.Ui;

/// <summary>
/// A janela dos comandos que o usuario manda rodar antes e depois da
/// desinstalacao de um programa.
///
/// O comando "antes" existe para derrubar o programa ou fechar o banco de
/// dados antes que o desinstalador tente apagar o arquivo; o "depois" existe
/// para refazer o que o desinstalador desfez. O plano de desinstalacao ja
/// tinha as duas etapas — faltava o lugar onde o usuario guarda o comando.
/// </summary>
public sealed partial class UninstallCommandsWindow : Window
{
    /// <summary>O comando que roda antes do desinstalador nativo.</summary>
    public string PreCommand { get; private set; } = string.Empty;

    /// <summary>O comando que roda depois da desinstalacao.</summary>
    public string PostCommand { get; private set; } = string.Empty;

    public UninstallCommandsWindow(string pre = "", string post = "")
    {
        InitializeComponent();

        Title = Loc.Get("Commands.Title");
        PromptText.Text = Loc.Get("Commands.Prompt");
        PreLabel.Text = Loc.Get("Commands.PreLabel");
        PostLabel.Text = Loc.Get("Commands.PostLabel");
        HintText.Text = Loc.Get("Commands.Hint");

        PreBox.Text = pre ?? string.Empty;
        PostBox.Text = post ?? string.Empty;
        PreBox.Focus();
        PreBox.SelectAll();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        PreCommand = (PreBox.Text ?? string.Empty).Trim();
        PostCommand = (PostBox.Text ?? string.Empty).Trim();
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
