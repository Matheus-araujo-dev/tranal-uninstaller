param(
    [int]$ProcessId,
    [string]$OutFile
)

$ErrorActionPreference = 'Stop'

Add-Type @'
using System; using System.Text; using System.Runtime.InteropServices; using System.Collections.Generic;
public class RClick {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  public const uint RIGHTDOWN = 0x0008, RIGHTUP = 0x0010;
  public static List<IntPtr> WindowsOf(uint pid) {
    var res = new List<IntPtr>();
    EnumWindows((h,l) => { uint p; GetWindowThreadProcessId(h, out p); if (p == pid && IsWindowVisible(h)) res.Add(h); return true; }, IntPtr.Zero);
    return res;
  }
  public static string Title(IntPtr h) { int n = GetWindowTextLength(h); if (n == 0) return ""; var sb = new StringBuilder(n+2); GetWindowText(h, sb, sb.Capacity); return sb.ToString(); }
  public static void Right(int x, int y) {
    SetCursorPos(x, y);
    System.Threading.Thread.Sleep(150);
    mouse_event(RIGHTDOWN, 0, 0, 0, IntPtr.Zero);
    System.Threading.Thread.Sleep(80);
    mouse_event(RIGHTUP, 0, 0, 0, IntPtr.Zero);
  }
}
'@

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$windows = [RClick]::WindowsOf([uint32]$ProcessId)
$main = $null
foreach ($w in $windows) {
    $t = [RClick]::Title($w)
    if ($t -like '*Tranal Uninstaller Pro*') { $main = $w; break }
}

if ($null -eq $main) { Write-Output 'janela principal nao encontrada'; exit 1 }

[void][RClick]::SetForegroundWindow($main)
Start-Sleep -Milliseconds 400

$rect = New-Object RClick+RECT
[void][RClick]::GetWindowRect($main, [ref]$rect)

# Clique do botao direito sobre a primeira linha da lista de ferramentas.
$x = $rect.Left + 420
$y = $rect.Top + 200

Write-Output ("janela em " + $rect.Left + "," + $rect.Top + " -> clique em " + $x + "," + $y)
[RClick]::Right($x, $y)
Start-Sleep -Seconds 2

$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
    [System.Windows.Automation.ControlType]::MenuItem)

$items = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)

$lines = New-Object System.Collections.Generic.List[string]
foreach ($item in $items) {
    $name = $item.Current.Name
    if ([string]::IsNullOrWhiteSpace($name)) { continue }
    $lines.Add($name)
}

$lines | Set-Content -LiteralPath $OutFile -Encoding UTF8
Write-Output ("itens de menu encontrados: " + $lines.Count)
$lines | ForEach-Object { Write-Output ("  - " + $_) }
