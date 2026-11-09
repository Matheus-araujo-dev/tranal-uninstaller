using System.IO;

namespace Tranal.Uninstaller.Core.Modules;

/// <summary>
/// O aviso de sincronizacao do Chrome.
///
/// Quando uma extensao do Chrome e removida, o Chrome pode trazela de volta:
/// ela esta sincronizada com a conta do usuario. O aviso existe para o
/// usuario saber disso **antes** de a remocao acontecer.
///
/// O sinal e o proprio arquivo de preferencias da extensao: o Chrome grava
/// as preferencias sincronizadas na pasta do perfil.
///
/// A regra esta em um lugar so: este modulo sabe quando o aviso e
/// necessario.
/// </summary>
public static class ChromeSyncGuard
{
    /// <summary>A pasta onde o Chrome guarda os perfis.</summary>
    public static string ProfilesFolder =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Google", "Chrome", "User Data");

    /// <summary>
    /// O perfil do Chrome esta com a sincronizacao ligada?
    /// </summary>
    public static bool IsSyncEnabled()
    {
        try
        {
            var preferences = Path.Combine(ProfilesFolder, "Default", "Preferences");
            if (!File.Exists(preferences)) return false;

            var texto = File.ReadAllText(preferences);

            // O Chrome grava a sincronizacao no proprio arquivo de
            // preferencias; o sinal procurado e o campo de conta.
            return texto.Contains("\"sync\"", StringComparison.Ordinal)
                && !texto.Contains("\"sync\":{\"disabled\":true", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// A extensao removida esta sincronizada?
    /// </summary>
    /// <param name="extensionId">O identificador da extensao.</param>
    public static bool IsExtensionSynced(string? extensionId)
    {
        if (string.IsNullOrWhiteSpace(extensionId)) return false;

        try
        {
            var sincronizado = Path.Combine(
                ProfilesFolder, "Default", "Sync Extension Settings", extensionId.Trim());

            return Directory.Exists(sincronizado);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// O aviso que o usuario deve ver antes de remover uma extensao.
    /// </summary>
    /// <param name="extensionId">O identificador da extensao.</param>
    /// <returns>O texto do aviso, ou uma string vazia quando nao ha aviso.</returns>
    public static string WarningFor(string? extensionId)
    {
        if (!IsExtensionSynced(extensionId)) return string.Empty;

        return "Esta extensao esta sincronizada com a sua conta do Chrome: "
             + "ela pode voltar a ser instalada pelos outros aparelhos.";
    }
}
