using System.Windows;
using Tranal.Uninstaller.Core.Settings;

namespace Tranal.Uninstaller.Ui;

/// <summary>
/// Mensagens ao usuario.
///
/// As preferencias "Ocultar mensagens de informacao" e "Ocultar mensagens de
/// aviso" do Revo Uninstaller Pro sao respeitadas aqui.
///
/// A confirmacao de uma acao destrutiva NUNCA e omitida: essas opcoes do Revo
/// tratam apenas das mensagens informativas e de aviso, nao da pergunta
/// "tem certeza?".
/// </summary>
public static class UserMessages
{
    /// <summary>True quando as mensagens de informacao devem ser omitidas.</summary>
    public static bool SkipInformation => AppSettingsStore.Current.SkipInformationMessages;

    /// <summary>True quando as mensagens de aviso devem ser omitidas.</summary>
    public static bool SkipWarning => AppSettingsStore.Current.SkipWarningMessages;

    /// <summary>
    /// Mostra uma mensagem de informacao. Devolve <c>true</c> quando a
    /// mensagem foi exibida e <c>false</c> quando foi omitida.
    /// </summary>
    public static bool Information(Window owner, string message, string caption = "Tranal Uninstaller")
    {
        if (SkipInformation) return false;

        MessageBox.Show(owner, message, caption, MessageBoxButton.OK, MessageBoxImage.Information);
        return true;
    }

    /// <summary>
    /// Mostra uma mensagem de aviso. Devolve <c>true</c> quando a mensagem foi
    /// exibida e <c>false</c> quando foi omitida.
    /// </summary>
    public static bool Warning(Window owner, string message, string caption = "Tranal Uninstaller")
    {
        if (SkipWarning) return false;

        MessageBox.Show(owner, message, caption, MessageBoxButton.OK, MessageBoxImage.Warning);
        return true;
    }

    /// <summary>
    /// Faz uma confirmacao e devolve <c>true</c> quando o usuario aceitou.
    ///
    /// Esta confirmacao NUNCA e omitida pelas preferencias de mensagem: e a
    /// pergunta que antecede uma acao destrutiva, e nao uma mensagem
    /// informativa. O botao padrao e "Nao", para que "Enter" sem querer nao
    /// autorize a remocao.
    ///
    /// O dono pode ser nulo: um painel embutido na janela principal nem
    /// sempre tem uma janela proprietaria no momento da pergunta. A caixa
    /// aparece sem dono nesse caso — e **aparece**.
    /// </summary>
    public static bool Confirm(Window? owner, string message, string caption = "Tranal Uninstaller")
    {
        var result = owner is null
            ? MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
            : MessageBox.Show(owner, message, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }
}
