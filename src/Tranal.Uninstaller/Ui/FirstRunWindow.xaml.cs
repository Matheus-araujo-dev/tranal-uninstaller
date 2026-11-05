using System.Windows;
using Tranal.Uninstaller.Core.Localization;

namespace Tranal.Uninstaller.Ui;

/// <summary>
/// Escolha de tema no primeiro lancamento (paridade com o Revo Pro, que pergunta
/// entre "UI classica" e "UI dark" na primeira execucao).
/// </summary>
public partial class FirstRunWindow : Window
{
    public FirstRunWindow()
    {
        InitializeComponent();

        // Window.Title nao aceita binding: texto via codigo.
        Title = Loc.Get("Options.Title");
    }

    /// <summary>Tema escolhido pelo usuario ("Dark" ou "Light").</summary>
    public string SelectedTheme => LightRadio.IsChecked == true ? "Light" : "Dark";

    private void ThemeRadio_Changed(object sender, RoutedEventArgs e)
    {
        // O "Checked" dispara durante o carregamento do XAML, antes de o botao
        // existir: sem esta guarda, a partida a frio (sem arquivo de
        // preferencias) terminava em excecao e o aplicativo nao abria.
        if (OkButton is null) return;

        // Apenas sincroniza o destaque visual da opcao marcada.
        OkButton.IsDefault = true;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
