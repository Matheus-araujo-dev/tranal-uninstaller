// Probe de auditoria — o que este programa mede esta no cabecalho.
// Ele NAO faz parte do Tranal: e a ferramenta que mede a Central de
// Acoes do Windows (IOBIT T7#34). A medicao e a que esta em
// docs/auditoria/EVIDENCIA-CENTRAL-ACoes.md.
//
using System.Runtime.InteropServices;

// Dispara um balao da bandeja com AppUserModelID e mostra o que o Windows
// aceitou. A leitura da Central de Acoes e feita em outro probe (probe-wpn),
// no banco wpndatabase.db.

const int NIM_ADD = 0x00000000;
const int NIM_MODIFY = 0x00000001;
const int NIM_DELETE = 0x00000002;
const int NIF_MESSAGE = 0x00000001;
const int NIF_ICON = 0x00000002;
const int NIF_TIP = 0x00000004;
const int NIF_INFO = 0x00000010;
const int NIM_SETVERSION = 0x00000004;
const uint NOTIFYICON_VERSION_4 = 4;

var aumid = "Tranal.ProbeBalao";
var janela = CriarJanela();

using (var chave = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
    $@"Software\Classes\AppUserModelId\{aumid}"))
{
    chave.SetValue("DisplayName", "Tranal Probe Balao");
    chave.SetValue("IconUri", Environment.SystemDirectory + "\\notepad.exe,0");
}

var codigo = SetCurrentProcessExplicitAppUserModelID(aumid);
Console.WriteLine($"SetCurrentProcessExplicitAppUserModelID -> 0x{codigo:X8}");

var dados = new NOTIFYICONDATA
{
    cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
    hWnd = janela,
    uID = 1,
    uFlags = NIF_ICON | NIF_MESSAGE | NIF_TIP,
    uVersion = NOTIFYICON_VERSION_4,
    szTip = "Tranal Probe",
    hIcon = LoadIcon(IntPtr.Zero, new IntPtr(32512)),
};

Console.WriteLine($"Shell_NotifyIcon(ADD) -> {Shell_NotifyIcon(NIM_ADD, ref dados)}");
dados.uVersion = NOTIFYICON_VERSION_4;
Console.WriteLine($"Shell_NotifyIcon(SETVERSION) -> {Shell_NotifyIcon(NIM_SETVERSION, ref dados)}");

dados.uFlags = NIF_INFO;
dados.szInfoTitle = "Tranal Probe Balao";
dados.szInfo = "Balao de teste do Tranal (probe)";
dados.dwInfoFlags = 0x00000001; // NIIF_INFO
Console.WriteLine($"Shell_NotifyIcon(MODIFY com NIF_INFO) -> {Shell_NotifyIcon(NIM_MODIFY, ref dados)}");

Console.WriteLine("esperando 8 segundos para o shell processar...");
Thread.Sleep(8000);

Shell_NotifyIcon(NIM_DELETE, ref dados);

try { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\AppUserModelId\{aumid}"); }
catch { }

return;

static IntPtr CriarJanela() =>
    CreateWindowEx(0, "STATIC", "", 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

[DllImport("user32.dll", CharSet = CharSet.Unicode)]
static extern IntPtr CreateWindowEx(int ex, string cls, string titulo, int estilo, int x, int y, int w, int h, IntPtr pai, IntPtr menu, IntPtr instancia, IntPtr param);

[DllImport("user32.dll")]
static extern IntPtr LoadIcon(IntPtr instancia, IntPtr nome);

[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
static extern bool Shell_NotifyIcon(uint msg, ref NOTIFYICONDATA dados);

[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
static extern int SetCurrentProcessExplicitAppUserModelID(string aumid);

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
struct NOTIFYICONDATA
{
    public int cbSize;
    public IntPtr hWnd;
    public uint uID;
    public uint uFlags;
    public uint uCallbackMessage;
    public IntPtr hIcon;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
    public uint dwState;
    public uint dwStateMask;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
    public uint uVersion;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szInfoTitle;
    public uint dwInfoFlags;
    public Guid guidItem;
    public IntPtr hBalloonIcon;
}
