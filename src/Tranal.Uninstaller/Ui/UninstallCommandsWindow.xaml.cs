using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Tranal.Uninstaller.Core.Automation;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Settings;

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

        RunButton.Content = Loc.Get("Commands.Run");

        // O usuario pode desligar a execucao de aplicativos externos (a
        // opcao do Bulk Cookie Uninstaller). Desligada, o botao de executar
        // some: o comando continua sendo gravado e roda no desinstalador,
        // se o usuario pediu.
        if (!AppSettingsStore.Current.AllowRunningUserCommands)
        {
            RunButton.Visibility = Visibility.Collapsed;
            HintText.Text = Loc.Get("Commands.Hint")
                + Environment.NewLine
                + Loc.Get("Commands.RunDisabled");
        }
    }

    /// <summary>
    /// Roda o comando do usuario na hora, para ele ver o que o comando faz
    /// antes de confiar nele.
    ///
    /// E o "executar aplicativos externos" do Bulk Cookie Uninstaller: o
    /// comando sai do teclado do usuario e roda pelo <c>cmd.exe</c>, que e o
    /// que faz a regra morar em um lugar so
    /// (<see cref="ShellCommand"/>).
    /// </summary>
    private async void RunButton_Click(object sender, RoutedEventArgs e)
    {
        var comando = ComandoEscolhido();
        if (comando.Length == 0)
        {
            OutputBox.Text = Loc.Get("Commands.EmptyToRun");
            return;
        }

        RunButton.IsEnabled = false;
        OutputBox.Text = Loc.Get("Commands.Running");

        try
        {
            var resultado = await ShellCommand.RunAsync(comando);

            var texto = resultado.Message;
            if (resultado.Output.Length > 0) texto += Environment.NewLine + resultado.Output;
            if (resultado.Error.Length > 0) texto += Environment.NewLine + resultado.Error;

            OutputBox.Text = texto;
        }
        finally
        {
            RunButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// O comando que o botao vai rodar: o do campo com o foco, ou o
    /// "antes" quando nenhum tem o foco.
    /// </summary>
    private string ComandoEscolhido()
    {
        if (ReferenceEquals(Keyboard.FocusedElement, PostBox))
            return (PostBox.Text ?? string.Empty).Trim();

        return (PreBox.Text ?? string.Empty).Trim();
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
