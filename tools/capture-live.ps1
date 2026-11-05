<#
  Captura de janela que funciona mesmo com a janela encoberta.

  Por que nao usar só PrintWindow: em WPF ele devolve uma imagem em branco
  quando a janela esta atras de outra (o DWM nao pede a repintura). A captura
  de tela da regiao da janela, com a janela trazida para o primeiro plano de
  verdade, e o metodo confiavel.

  O script tambem mede a variacao da imagem: uma captura "vazia" (tudo de uma
  cor) e reportada como suspeita, em vez de passar por uma captura boa.

  Uso:
    capture-live.ps1 -ProcessId 1234 -OutFile "captura.png"
#>
param(
    [Parameter(Mandatory = $true)][int]$ProcessId,
    [string]$OutFile = '',
    [switch]$NotTopMost
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if (-not ('WinLive' -as [type])) {
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections.Generic;

public class WinLive {
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
  [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
'@
}

$found = New-Object System.Collections.ArrayList
$cb = [WinLive+EnumProc] {
    param($h, $l)
    $pid2 = 0
    [void][WinLive]::GetWindowThreadProcessId($h, [ref]$pid2)
    if ($pid2 -eq $ProcessId -and [WinLive]::IsWindowVisible($h)) {
        [void]$found.Add($h)
    }
    return $true
}
[void][WinLive]::EnumWindows($cb, [IntPtr]::Zero)

if ($found.Count -eq 0) { Write-Output "nenhuma janela visivel para o PID $ProcessId"; exit 1 }

$h = $found[0]
$len = [WinLive]::GetWindowTextLength($h)
$sb = New-Object System.Text.StringBuilder ($len + 2)
[void][WinLive]::GetWindowText($h, $sb, $sb.Capacity)

$r = New-Object WinLive+RECT
[void][WinLive]::GetWindowRect($h, [ref]$r)
$width = $r.Right - $r.Left
$height = $r.Bottom - $r.Top
Write-Output "janela: '$($sb.ToString())'  rect: $($r.Left),$($r.Top) ${width}x${height}"

# Traz para o primeiro plano de verdade: o truque do ALT destrava
# SetForegroundWindow quando o processo chamador nao esta em primeiro plano.
[void][WinLive]::ShowWindow($h, 9)   # SW_RESTORE
[WinLive]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero)   # ALT down
[void][WinLive]::SetForegroundWindow($h)
[void][WinLive]::BringWindowToTop($h)
[WinLive]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)   # ALT up

if (-not $NotTopMost) {
    # TOPMOST temporario: garante que a janela fique acima das outras
    # durante a captura da regiao.
    [void][WinLive]::SetWindowPos($h, [IntPtr](-1), 0, 0, 0, 0, 0x0001 -bor 0x0002 -bor 0x0040)
}
Start-Sleep -Milliseconds 900

if ([string]::IsNullOrWhiteSpace($OutFile)) {
    $OutFile = Join-Path $PSScriptRoot ("live-{0}-{1}.png" -f $ProcessId, (Get-Date -Format 'yyyyMMdd-HHmmss'))
}

$bmp = New-Object System.Drawing.Bitmap $width, $height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size $width, $height))
$g.Dispose()

if (-not $NotTopMost) {
    [void][WinLive]::SetWindowPos($h, [IntPtr](-2), 0, 0, 0, 0, 0x0001 -bor 0x0002 -bor 0x0040)  # NOTOPMOST
}

# Variacao da imagem: uma tela em branco (ou de uma cor so) nao e uma captura
# valida — e assim que um erro de tema "parece" uma tela boa.
$cores = New-Object System.Collections.Generic.HashSet[int]
for ($y = 0; $y -lt $height; $y += 7) {
    for ($x = 0; $x -lt $width; $x += 7) {
        [void]$cores.Add($bmp.GetPixel($x, $y).ToArgb())
        if ($cores.Count -gt 400) { break }
    }
    if ($cores.Count -gt 400) { break }
}

$bmp.Save($OutFile, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output "captura: $OutFile"
Write-Output "cores distintas amostradas: $($cores.Count)"
if ($cores.Count -le 3) { Write-Output "AVISO: imagem praticamente uniforme (janela provavelmente em branco)" }
