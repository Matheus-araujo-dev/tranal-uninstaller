<#
  Captura a janela de um PID especifico (PrintWindow), mesmo que esteja
  encoberta por outra. Em WPF o PrintWindow funciona; em apps nativos MFC
  ele devolve preto (esses exigem captura de tela).

  Uso: capture-window.ps1 -ProcessId 1234 -OutFile "captura.png"
#>
param(
    [Parameter(Mandatory = $true)][int]$ProcessId,
    [string]$OutFile = '',
    [switch]$Foreground
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if (-not ('WinCap' -as [type])) {
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections.Generic;

public class WinCap {
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
'@
}

$found = New-Object System.Collections.ArrayList
$cb = [WinCap+EnumProc] {
    param($h, $l)
    $pid2 = 0
    [void][WinCap]::GetWindowThreadProcessId($h, [ref]$pid2)
    if ($pid2 -eq $ProcessId -and [WinCap]::IsWindowVisible($h)) {
        [void]$found.Add($h)
    }
    return $true
}
[void][WinCap]::EnumWindows($cb, [IntPtr]::Zero)

if ($found.Count -eq 0) { Write-Output "nenhuma janela visivel para o PID $ProcessId"; exit 1 }

$h = $found[0]
$len = [WinCap]::GetWindowTextLength($h)
$sb = New-Object System.Text.StringBuilder ($len + 2)
[void][WinCap]::GetWindowText($h, $sb, $sb.Capacity)
Write-Output "janela: '$($sb.ToString())'"

$r = New-Object WinCap+RECT
[void][WinCap]::GetWindowRect($h, [ref]$r)
$width = $r.Right - $r.Left
$height = $r.Bottom - $r.Top
Write-Output "rect: $($r.Left),$($r.Top) ${width}x${height}"

if ($Foreground) {
    [void][WinCap]::ShowWindow($h, 9)   # SW_RESTORE
    [void][WinCap]::SetForegroundWindow($h)
    Start-Sleep -Milliseconds 700
}

if ([string]::IsNullOrWhiteSpace($OutFile)) {
    $OutFile = Join-Path $PSScriptRoot ("win-{0}-{1}.png" -f $ProcessId, (Get-Date -Format 'yyyyMMdd-HHmmss'))
}

$bmp = New-Object System.Drawing.Bitmap $width, $height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
$ok = [WinCap]::PrintWindow($h, $hdc, 2)   # PW_RENDERFULLCONTENT
$g.ReleaseHdc($hdc)
$g.Dispose()

if (-not $ok) {
    Write-Output "PrintWindow falhou (tentando captura de tela)"
    $g2 = [System.Drawing.Graphics]::FromImage($bmp)
    $g2.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size $width, $height))
    $g2.Dispose()
}

$bmp.Save($OutFile, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output "captura: $OutFile"
