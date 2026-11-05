# Alterna o tema em runtime pelo menu principal (UI Automation).
#
# Por que existe: o submenu "Tema" so cria os itens filhos quando e expandido,
# e o InvokePattern do item pai nao expande. O script expande o submenu via
# ExpandCollapsePattern e depois invoca o item de tema pedido.
#
# A captura usa a mesma tecnica do capture-live.ps1 (ALT trick + TOPMOST
# temporario): o PrintWindow devolve branco quando a janela esta encoberta.
#
# Uso:
#   switch-theme.ps1 -ProcessName TranalUninstaller -Theme dark -OutFile captura.png
param(
    [string]$ProcessName = 'TranalUninstaller',
    [int]$ProcessId = 0,
    [ValidateSet('dark', 'light')]
    [string]$Theme = 'dark',
    [string]$OutFile = '',
    [int]$WaitSeconds = 3
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms

if (-not ('WinLive' -as [type])) {
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;

public class WinLive {
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
'@
}

$proc = if ($ProcessId -gt 0) {
    Get-Process -Id $ProcessId -ErrorAction Stop
} else {
    Get-Process -Name $ProcessName -ErrorAction Stop | Select-Object -First 1
}

# Janelas do processo (a raiz da arvore de UI vem do HWND, nao do PID).
$handles = New-Object System.Collections.ArrayList
$cb = [WinLive+EnumProc] {
    param($h, $l)
    $pid2 = 0
    [void][WinLive]::GetWindowThreadProcessId($h, [ref]$pid2)
    if ($pid2 -eq $proc.Id -and [WinLive]::IsWindowVisible($h)) { [void]$handles.Add($h) }
    return $true
}
[void][WinLive]::EnumWindows($cb, [IntPtr]::Zero)
if ($handles.Count -eq 0) { throw "nenhuma janela visivel do processo '$ProcessName'" }

$hwnd = $handles[0]
$root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)

function Find-ById($scope, [string]$id) {
    $c = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
    return $scope.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)
}

function Find-MenuItemByName($scope, [string]$name) {
    $c = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::MenuItem)
    $items = $scope.FindAll([System.Windows.Automation.TreeScope]::Descendants, $c)
    foreach ($i in $items) { if ($i.Current.Name -eq $name) { return $i } }
    return $null
}

# 1) abre o menu principal
$btn = Find-ById $root 'CmdMainMenu'
if ($null -eq $btn) { throw "CmdMainMenu nao encontrado" }
$btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Start-Sleep -Milliseconds 800

# 2) expande o submenu "Tema"
$tema = Find-MenuItemByName $root 'Tema'
if ($null -eq $tema) { throw "item de menu 'Tema' nao encontrado" }

try {
    $ec = $tema.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $ec.Expand()
}
catch {
    $r = $tema.Current.BoundingRectangle
    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point(
        [int]($r.X + $r.Width / 2), [int]($r.Y + $r.Height / 2))
}
Start-Sleep -Milliseconds 1000

# 3) invoca o item de tema
$target = if ($Theme -eq 'dark') { 'CmdThemeDark' } else { 'CmdThemeLight' }
$item = Find-ById $root $target
if ($null -eq $item) { throw "item '$target' nao encontrado no submenu" }
$item.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()

Start-Sleep -Seconds $WaitSeconds
Write-Host "tema aplicado: $Theme"

if ($OutFile) {
    $r = New-Object WinLive+RECT
    [void][WinLive]::GetWindowRect($hwnd, [ref]$r)
    $width = $r.Right - $r.Left
    $height = $r.Bottom - $r.Top

    [void][WinLive]::ShowWindow($hwnd, 9)
    [WinLive]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero)
    [void][WinLive]::SetForegroundWindow($hwnd)
    [void][WinLive]::BringWindowToTop($hwnd)
    [WinLive]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
    [void][WinLive]::SetWindowPos($hwnd, [IntPtr](-1), 0, 0, 0, 0, 0x0001 -bor 0x0002 -bor 0x0040)
    Start-Sleep -Milliseconds 800

    $bmp = New-Object System.Drawing.Bitmap $width, $height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size $width, $height))
    $g.Dispose()
    $bmp.Save($OutFile, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()

    [void][WinLive]::SetWindowPos($hwnd, [IntPtr](-2), 0, 0, 0, 0, 0x0001 -bor 0x0002 -bor 0x0040)
    Write-Host "captura: $OutFile"
}
