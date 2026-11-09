using Microsoft.Win32;

namespace Tranal.Uninstaller.Core.Management;

/// <summary>
/// O icone do Tranal no "Apps e recursos" do Windows (o painel de controle
/// dos programas instalados).
///
/// A entrada e a mesma que o Windows le para todo programa instalado: uma
/// chave em <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall</c>.
/// Nao existe um caminho especial para "aparecer no painel de controle" —
/// o Windows le a mesma chave.
///
/// A regra esta em um lugar so: este modulo sabe o caminho, o nome da entrada
/// e os valores que o Windows espera.
/// </summary>
public static class ControlPanelIcon
{
    /// <summary>O nome da entrada do Tranal na lista de programas do Windows.</summary>
    public const string EntryName = "Tranal Uninstaller";

    /// <summary>A chave do registro da entrada.</summary>
    public static string RegistryPath =>
        @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + EntryName;

    /// <summary>A entrada existe?</summary>
    public static bool IsRegistered()
    {
        try
        {
            using var hive = Registry.CurrentUser;
            using var key = hive.OpenSubKey(RegistryPath);
            return key is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Cria (ou atualiza) a entrada do Tranal na lista de programas do
    /// Windows.
    /// </summary>
    /// <param name="displayName">O nome que aparece na lista.</param>
    /// <param name="uninstallCommand">O comando que desinstala o Tranal.</param>
    /// <param name="iconPath">O caminho do icone.</param>
    /// <param name="version">A versao mostrada na lista.</param>
    /// <returns>Verdadeiro quando a entrada ficou gravada.</returns>
    public static bool Register(
        string displayName,
        string uninstallCommand,
        string iconPath,
        string version = "1.4.0")
    {
        try
        {
            using var hive = Registry.CurrentUser;
            using var key = hive.CreateSubKey(RegistryPath, writable: true);
            if (key is null) return false;

            key.SetValue("DisplayName", string.IsNullOrWhiteSpace(displayName) ? EntryName : displayName, RegistryValueKind.String);
            key.SetValue("UninstallString", uninstallCommand ?? string.Empty, RegistryValueKind.String);
            key.SetValue("QuietUninstallString", uninstallCommand ?? string.Empty, RegistryValueKind.String);
            key.SetValue("DisplayIcon", iconPath ?? string.Empty, RegistryValueKind.String);
            key.SetValue("Publisher", "Tranal Group", RegistryValueKind.String);
            key.SetValue("DisplayVersion", version, RegistryValueKind.String);
            key.SetValue("InstallLocation", AppContext.BaseDirectory, RegistryValueKind.String);
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            key.SetValue("EstimatedSize", 0, RegistryValueKind.DWord);

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Remove a entrada do Tranal da lista de programas do Windows.</summary>
    public static bool Unregister()
    {
        try
        {
            using var hive = Registry.CurrentUser;
            hive.DeleteSubKeyTree(RegistryPath, throwOnMissingSubKey: false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// O caminho do icone que o Windows mostra na lista: o executavel do
    /// proprio Tranal, com o indice zero de icone.
    /// </summary>
    public static string IconPath
    {
        get
        {
            var path = SystemTray.ExecutablePath;
            return path.Length == 0 ? string.Empty : path + ",0";
        }
    }

    /// <summary>O estado da entrada, para a tela e para a linha de comando.</summary>
    public static string Describe() =>
        IsRegistered()
            ? "O icone do Tranal esta no painel de controle do Windows."
            : "O icone do Tranal nao esta no painel de controle do Windows.";
}
