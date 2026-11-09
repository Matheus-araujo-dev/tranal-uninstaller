using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Tranal.Uninstaller.Ui.Tools;

/// <summary>
/// O icone do Tranal na bandeja do sistema.
///
/// A bandeja e um recurso da interface: e uma janela do Windows, e nao tem
/// equivalente no nucleo. O que a bandeja **faz** (as regras) mora no nucleo
/// (<see cref="Core.Management.SystemTray"/>); aqui so mora o icone.
///
/// O icone usa a propria API do Windows (<c>Shell_NotifyIcon</c>), sem
/// dependencia extra: e a mesma chamada que o Explorador faz.
/// </summary>
internal sealed class TrayIconManager : IDisposable
{
    private const int NIM_ADD = 0x00000000;
    private const int NIM_DELETE = 0x00000002;
    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_TIP = 0x00000004;
    private const int WM_APP = 0x8000;
    private const int WM_TRAY = WM_APP + 41;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_CONTEXTMENU = 0x007B;
    private const int IMAGE_ICON = 1;
    private const int LR_LOADFROMFILE = 0x00000010;
    private const int LR_DEFAULTSIZE = 0x00000040;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadImage(IntPtr hinst, string name, int type, int cx, int cy, int load);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr hinst, int id);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    private readonly Window _owner;
    private readonly HwndSource? _source;
    private readonly IntPtr _hwnd;
    private IntPtr _icon;
    private bool _visible;

    /// <summary>Cria o gerenciador e ja registra o icone na bandeja.</summary>
    /// <param name="owner">A janela que dono do icone.</param>
    /// <param name="tooltip">O texto que aparece ao passar o mouse no icone.</param>
    public TrayIconManager(Window owner, string? tooltip = null)
    {
        _owner = owner;
        _hwnd = new WindowInteropHelper(owner).Handle;

        if (_hwnd == IntPtr.Zero) return;

        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);

        _icon = LoadIconForApp();
        Show(tooltip ?? Core.Management.SystemTray.Tooltip);
    }

    /// <summary>O icone esta na bandeja?</summary>
    public bool IsVisible => _visible;

    /// <summary>
    /// Pedido de volta para a janela: veio do clique no icone.
    /// </summary>
    public event EventHandler? ClickRequested;

    /// <summary>
    /// Registra (ou tira) o icone da bandeja.
    /// </summary>
    /// <param name="tooltip">O texto que aparece ao passar o mouse no icone.</param>
    public void Show(string? tooltip = null)
    {
        var data = NewData(tooltip);
        try
        {
            _visible = Shell_NotifyIcon(NIM_ADD, ref data);
        }
        catch
        {
            _visible = false;
        }
    }

    /// <summary>Tira o icone da bandeja.</summary>
    public void Hide()
    {
        if (!_visible) return;

        var data = NewData(null);
        try
        {
            Shell_NotifyIcon(NIM_DELETE, ref data);
        }
        catch
        {
            // Sem bandeja, nao ha o que tirar.
        }

        _visible = false;
    }

    /// <summary>
    /// Mostra um aviso perto do icone (a "bolha" do Windows).
    /// </summary>
    /// <param name="title">O titulo do aviso.</param>
    /// <param name="message">O texto do aviso.</param>
    public void Notify(string title, string message)
    {
        if (!_visible) return;

        var data = NewData(null);
        data.szInfoTitle = title ?? string.Empty;
        data.szInfo = message ?? string.Empty;
        data.dwInfoFlags = 0x00000001; // NIIF_INFO

        try
        {
            Shell_NotifyIcon(NIM_MODIFY, ref data);
        }
        catch
        {
            // O aviso e um extra: se a bolha nao aparecer, o Tranal continua.
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Hide();

        _source?.RemoveHook(WndProc);

        if (_icon != IntPtr.Zero)
        {
            DestroyIcon(_icon);
            _icon = IntPtr.Zero;
        }
    }

    private const int NIM_MODIFY = 0x00000001;

    private NOTIFYICONDATA NewData(string? tooltip)
    {
        var data = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAY,
            hIcon = _icon,
            szTip = tooltip ?? string.Empty,
        };

        return data;
    }

    private IntPtr LoadIconForApp()
    {
        try
        {
            var path = Core.Management.SystemTray.ExecutablePath;
            if (!string.IsNullOrWhiteSpace(path))
            {
                var handle = LoadImage(IntPtr.Zero, path, IMAGE_ICON, 0, 0, LR_LOADFROMFILE | LR_DEFAULTSIZE);
                if (handle != IntPtr.Zero) return handle;
            }
        }
        catch
        {
            // Sem icone do executavel, o icone padrao do Windows serve.
        }

        return LoadIcon(IntPtr.Zero, 32512); // IDI_APPLICATION
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_TRAY)
        {
            var tipo = lParam.ToInt32();

            if (tipo is WM_LBUTTONUP or WM_LBUTTONDBLCLK)
            {
                handled = true;
                ClickRequested?.Invoke(this, EventArgs.Empty);
            }
            else if (tipo is WM_RBUTTONUP or WM_CONTEXTMENU)
            {
                // O botao direito nao abre menu: o icone serve para devolver
                // a janela, e e isso que ele faz.
                handled = true;
            }
        }

        return IntPtr.Zero;
    }
}
