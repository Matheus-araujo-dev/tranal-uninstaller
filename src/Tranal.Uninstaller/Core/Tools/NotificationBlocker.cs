using Microsoft.Win32;

namespace Tranal.Uninstaller.Core.Tools;

/// <summary>
/// O estado dos avisos do Windows (as notificacoes que aparecem no canto da
/// tela).
/// </summary>
/// <param name="ToastEnabled">As notificacoes do Windows estao ligadas?</param>
/// <param name="SoundsEnabled">O som das notificacoes esta ligado?</param>
/// <param name="FocusEnabled">O "foco de assistencia" esta desligado?</param>
/// <param name="WwwRoot">O caminho da chave que guarda o estado.</param>
public sealed record NotificationBlockerState(
    bool ToastEnabled,
    bool SoundsEnabled,
    bool FocusEnabled,
    string WwwRoot);

/// <summary>
/// O bloqueio dos avisos que aparecem na tela.
///
/// Duas coisas entram aqui, e as duas sao o mesmo interruptor: os avisos que o
/// Windows mostra no canto da tela (as "toasts") e os avisos que aparecem
/// como baloes de programa. O chave de registro que o Windows usa e
/// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\PushNotifications</c>.
///
/// A regra esta em um lugar so: este modulo sabe o caminho, o nome do valor e
/// o que cada estado significa. A tela e a linha de comando consultam o
/// mesmo modulo.
/// </summary>
public static class NotificationBlocker
{
    /// <summary>A chave que guarda o estado dos avisos do Windows.</summary>
    public const string WwwRoot = @"Software\Microsoft\Windows\CurrentVersion\PushNotifications";

    /// <summary>O valor que liga (ou desliga) os avisos na tela.</summary>
    public const string ToastEnabledValue = "ToastEnabled";

    /// <summary>O valor que liga (ou desliga) o som dos avisos.</summary>
    public const string SoundEnabledValue = "SoundEnabled";

    /// <summary>
    /// A chave do "foco de assistencia": quando esta ligada, o Windows
    /// esconde os avisos.
    /// </summary>
    public const string FocusAssistRoot = @"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings";

    /// <summary>
    /// Substitui a chave usada (so a suite de testes). Nulo em uso normal.
    ///
    /// Existe para que o teste possa gravar e ler sem mexer nas preferencias
    /// reais do Windows de quem roda a suite.
    /// </summary>
    internal static string? RootOverride { get; set; }

    /// <summary>A chave efetivamente lida e gravada.</summary>
    private static string EffectiveRoot => RootOverride ?? WwwRoot;

    /// <summary>
    /// O estado atual dos avisos do Windows.
    /// </summary>
    public static NotificationBlockerState State()
    {
        var raiz = EffectiveRoot;

        var toast = ReadInt(raiz, ToastEnabledValue) != 0;
        var som = ReadInt(raiz, SoundEnabledValue) != 0;
        var foco = ReadInt(raiz, "NOC_GLOBAL_SETTING_TOASTS_ENABLED") == 0;

        return new NotificationBlockerState(toast, som, foco, raiz);
    }

    /// <summary>
    /// Liga ou desliga os avisos na tela.
    /// </summary>
    /// <param name="enabled">Verdadeiro para deixar aparecer.</param>
    public static bool SetToastEnabled(bool enabled) => WriteInt(EffectiveRoot, ToastEnabledValue, enabled ? 1 : 0);

    /// <summary>
    /// Liga ou desliga o som dos avisos.
    /// </summary>
    /// <param name="enabled">Verdadeiro para deixar tocar.</param>
    public static bool SetSoundEnabled(bool enabled) => WriteInt(EffectiveRoot, SoundEnabledValue, enabled ? 1 : 0);

    /// <summary>
    /// Desliga o "foco de assistencia": com ele ligado, o Windows esconde os
    /// avisos.
    /// </summary>
    /// <param name="enabled">Verdadeiro para deixar os avisos aparecerem.</param>
    public static bool SetFocusAssistDisabled(bool enabled) =>
        WriteInt(EffectiveRoot, "NOC_GLOBAL_SETTING_TOASTS_ENABLED", enabled ? 0 : 1);

    /// <summary>
    /// A frase que explica o estado, para a tela e para a linha de comando.
    /// </summary>
    public static string Describe()
    {
        var estado = State();

        if (!estado.ToastEnabled)
            return "Os avisos do Windows estao bloqueados nesta maquina.";

        return estado.FocusEnabled
            ? "O foco de assistencia esta ligado: o Windows esconde os avisos."
            : "Os avisos do Windows aparecem normalmente.";
    }

    private static int ReadInt(string keyPath, string valueName)
    {
        try
        {
            using var hive = Registry.CurrentUser;
            using var key = hive.OpenSubKey(keyPath);
            var value = key?.GetValue(valueName);

            return value switch
            {
                int i => i,
                long l => (int)l,
                _ => 0,
            };
        }
        catch
        {
            return 0;
        }
    }

    private static bool WriteInt(string keyPath, string valueName, int value)
    {
        try
        {
            using var hive = Registry.CurrentUser;
            using var key = hive.CreateSubKey(keyPath, writable: true);
            if (key is null) return false;

            key.SetValue(valueName, value, RegistryValueKind.DWord);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
