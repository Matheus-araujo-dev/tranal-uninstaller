using System.Runtime.InteropServices;

namespace Tranal.Uninstaller.Core.Tools;

/// <summary>As teclas modificadoras de uma combinacao.</summary>
[Flags]
public enum HotkeyModifiers
{
    /// <summary>Nenhuma tecla modificadora.</summary>
    None = 0,

    /// <summary>A tecla Alt.</summary>
    Alt = 0x0001,

    /// <summary>A tecla Ctrl.</summary>
    Control = 0x0002,

    /// <summary>A tecla Shift.</summary>
    Shift = 0x0004,

    /// <summary>A tecla Windows.</summary>
    Win = 0x0008,
}

/// <summary>
/// Um atalho global: a combinacao de teclas que o Tranal registra no
/// Windows, de qualquer janela.
/// </summary>
/// <param name="Id">O identificador fixo do atalho.</param>
/// <param name="DisplayName">O nome do atalho.</param>
/// <param name="Modifiers">As teclas modificadoras.</param>
/// <param name="VirtualKey">O codigo da tecla principal.</param>
/// <param name="Argument">O argumento que o Tranal recebe quando o atalho e apertado.</param>
public sealed record GlobalHotkey(
    string Id,
    string DisplayName,
    HotkeyModifiers Modifiers,
    int VirtualKey,
    string Argument);

/// <summary>
/// Os atalhos globais do Tranal.
///
/// Um atalho global e registrado com a API do Windows
/// (<c>RegisterHotKey</c>): ele funciona em qualquer janela, inclusive
/// quando o Tranal esta escondido.
///
/// O codigo da tecla principal e o codigo de tecla virtual do Windows
/// (<c>VK_T</c> = 0x54, por exemplo). E o proprio Windows quem traduz o
/// codigo em tecla — e nao o Tranal.
///
/// A regra esta em um lugar so: este modulo sabe o que e um atalho e quais
/// sao os atalhos do Tranal. A tela e a linha de comando consomem a mesma
/// lista.
/// </summary>
public static class GlobalHotkeys
{
    /// <summary>O codigo de tecla virtual da tecla T.</summary>
    public const int VirtualKeyT = 0x54;

    /// <summary>O codigo de tecla virtual da tecla U.</summary>
    public const int VirtualKeyU = 0x55;

    /// <summary>
    /// O atalho que abre o Tranal.
    /// </summary>
    public static GlobalHotkey OpenTranal { get; } = new(
        "open-tranal",
        "Abrir o Tranal",
        HotkeyModifiers.Control | HotkeyModifiers.Alt,
        VirtualKeyT,
        "--show");

    /// <summary>
    /// Os atalhos que o Tranal conhece.
    /// </summary>
    public static IReadOnlyList<GlobalHotkey> All { get; } = new[] { OpenTranal };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>
    /// Registra um atalho global na janela informada.
    ///
    /// O identificador numerico e o indice do atalho na lista — e ele que o
    /// Windows devolve quando o atalho e apertado.
    /// </summary>
    /// <param name="windowHandle">O identificador da janela que recebe o aviso.</param>
    /// <param name="hotkey">O atalho.</param>
    /// <param name="index">O indice do atalho na lista.</param>
    /// <returns>Verdadeiro quando o Windows aceitou o registro.</returns>
    public static bool Register(IntPtr windowHandle, GlobalHotkey hotkey, int index)
    {
        if (hotkey is null) return false;

        try
        {
            return RegisterHotKey(windowHandle, index, (uint)hotkey.Modifiers, (uint)hotkey.VirtualKey);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Tira o atalho do Windows.
    /// </summary>
    /// <param name="windowHandle">O identificador da janela.</param>
    /// <param name="index">O indice do atalho na lista.</param>
    public static bool Unregister(IntPtr windowHandle, int index)
    {
        try
        {
            return UnregisterHotKey(windowHandle, index);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// O texto do atalho, como o Windows mostra ("Ctrl+Alt+T").
    /// </summary>
    /// <param name="hotkey">O atalho.</param>
    public static string Describe(GlobalHotkey hotkey)
    {
        if (hotkey is null) return string.Empty;

        var partes = new List<string>();

        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Control)) partes.Add("Ctrl");
        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Alt)) partes.Add("Alt");
        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Shift)) partes.Add("Shift");
        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Win)) partes.Add("Win");

        partes.Add(LetterOf(hotkey.VirtualKey));

        return string.Join("+", partes);
    }

    private static string LetterOf(int virtualKey)
    {
        if (virtualKey >= 0x30 && virtualKey <= 0x5A) return ((char)virtualKey).ToString();
        return "0x" + virtualKey.ToString("X2");
    }
}
