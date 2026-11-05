param(
    [int]$ProcessId,
    [string]$OutFile
)

$ErrorActionPreference = 'Stop'

Add-Type @'
using System; using System.Text; using System.Runtime.InteropServices; using System.Collections.Generic;
public class RC2 {
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

$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]

$windows = [RC2]::WindowsOf([uint32]$ProcessId)
$main = $null
foreach ($w in $windows) {
    if ([RC2]::Title($w) -like '*Tranal Uninstaller Pro*') { $main = $w; break }
}
if ($null -eq $main) { Write-Output 'janela principal nao encontrada'; exit 1 }
[void][RC2]::SetForegroundWindow($main)
Start-Sleep -Milliseconds 400

$rect = New-Object RC2+RECT
[void][RC2]::GetWindowRect($main, [ref]$rect)

function Count-Checked {
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        $AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::CheckBox)
    $boxes = $AE::RootElement.FindAll($TS::Descendants, $cond)
    $on = 0
    foreach ($b in $boxes) {
        try {
            $state = $b.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState
            if ($state -eq [System.Windows.Automation.ToggleState]::On) { $on++ }
        } catch { }
    }
    return @{ total = $boxes.Count; marcados = $on }
}

$antes = Count-Checked
Write-Output ("antes: " + $antes.total + " caixa(s), " + $antes.marcados + " marcada(s)")

function Invoke-Menu([string]$automationId) {
    # Clique do botao direito sobre a lista
    [RC2]::Right($rect.Left + 420, $rect.Top + 200)
    Start-Sleep -Seconds 2

    $cond = New-Object System.Windows.Automation.PropertyCondition(
        $AE::AutomationIdProperty, $automationId)
    $item = $AE::RootElement.FindFirst($TS::Descendants, $cond)

    if ($null -eq $item) { Write-Output ("item " + $automationId + " nao encontrado"); return $null }

    $invoke = $item.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $invoke.Invoke()
    Start-Sleep -Seconds 2

    return $item.Current.Name
}

$nome = Invoke-Menu 'CtxUncheckAll'
Write-Output ("acionado: " + $nome)
$depois = Count-Checked
Write-Output ("depois de desmarcar todos: " + $depois.total + " caixa(s), " + $depois.marcados + " marcada(s)")

$nome = Invoke-Menu 'CtxCheckAll'
Write-Output ("acionado: " + $nome)
$final = Count-Checked
Write-Output ("depois de marcar todos: " + $final.total + " caixa(s), " + $final.marcados + " marcada(s)")

@("antes_total=$($antes.total)", "antes_marcados=$($antes.marcados)",
  "depois_marcados=$($depois.marcados)", "final_marcados=$($final.marcados)") |
    Set-Content -LiteralPath $OutFile -Encoding UTF8
