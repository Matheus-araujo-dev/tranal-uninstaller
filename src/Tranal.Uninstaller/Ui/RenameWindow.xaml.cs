using System.Windows;
using Tranal.Uninstaller.Core.Localization;

namespace Tranal.Uninstaller.Ui;

/// <summary>
/// A janela de renomear o programa na lista.
///
/// Corresponde ao "Rename" do Revo Pro: o nome que o Windows registra
/// continua valendo, e o que o usuario escreve aqui e o que a lista do
/// Tranal mostra.
/// </summary>
public sealed partial class RenameWindow : Window
{
    /// <summary>O nome escrito (vazio quando o usuario apagou tudo).</summary>
    public string DisplayName { get; private set; } = string.Empty;

    public RenameWindow(string initial = "", string currentName = "")
    {
        InitializeComponent();

        Title = Loc.Get(string.IsNullOrWhiteSpace(initial) ? "Rename.AddTitle" : "Rename.EditTitle");
        PromptText.Text = Loc.Get("Rename.Prompt", currentName);
        NameBox.Text = initial ?? string.Empty;
        NameBox.Focus();
        NameBox.SelectAll();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DisplayName = (NameBox.Text ?? string.Empty).Trim();
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
