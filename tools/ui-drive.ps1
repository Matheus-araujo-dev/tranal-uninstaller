# Harness de interacao com o app nativo (UI Automation) + captura de tela.
#
# Seletores DETERMINISTICOS: -ClickAutomationId / -ToggleAutomationId casam pelo
# AutomationId, que e estavel mesmo quando o texto visivel se repete (o titulo
# "Desinstalar" da barra lateral colidia com o item de menu "Desinstalar").
#
# Parametros:
#   -ProcessName        nome do processo (padrao TranalUninstaller)
#   -ClickAutomationId  clica no elemento com este AutomationId (busca global no processo)
#   -ClickName          clica pelo nome visivel (parcial por padrao; use -Match exact)
#   -ClickExact         clica pelo nome exato
#   -ToggleAutomationId marca/desmarca checkbox ou seleciona radiobutton
#   -SetAutomationId    escreve texto (ValuePattern) no elemento com este AutomationId
#   -SetText            texto a escrever
#   -ControlType        filtra por tipo de controle (ex.: MenuItem, Button, ListItem)
#   -Dump               lista os elementos encontrados (diagnostico) e nao clica
#   -WindowTitle        filtra a janela alvo pelo titulo
#   -Index              indice quando ha varios elementos com o mesmo seletor
#   -Keys               teclas a enviar depois do clique (ex.: '{ESC}')
#   -WaitSeconds        espera antes da captura
param(
  [string]$ProcessName = 'TranalUninstaller',
  [string]$ClickAutomationId = '',
  [string]$ClickName = '',
  [string]$ClickExact = '',
  [string]$ToggleAutomationId = '',
  [string]$SetAutomationId = '',
  [string]$SetText = '',
  [string]$ControlType = '',
  [string]$Match = 'partial',
  [switch]$Dump,
  [string]$WindowTitle = '',
  [int]$Index = 0,
  [string]$Keys = '',
  [string]$OutDir = "$PSScriptRoot\captures",
  [string]$Tag = 'shot',
  [int]$WaitSeconds = 3
)

$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

if (-not ('Inp' -as [type])) {
Add-Type @'
using System; using System.Text; using System.Runtime.InteropServices; using System.Collections.Generic;
public class Inp {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  public const uint LEFTDOWN = 0x0002, LEFTUP = 0x0004;
  public static void Click(int x, int y) {
    SetCursorPos(x, y);
    System.Threading.Thread.Sleep(120);
    mouse_event(LEFTDOWN, 0, 0, 0, IntPtr.Zero);
    System.Threading.Thread.Sleep(60);
    mouse_event(LEFTUP, 0, 0, 0, IntPtr.Zero);
  }
  public static string FgTitle(){ var h=GetForegroundWindow(); int n=GetWindowTextLength(h); if(n==0) return ""; var sb=new StringBuilder(n+2); GetWindowText(h,sb,sb.Capacity); return sb.ToString(); }
  public static List<IntPtr> WindowsOf(uint pid) {
    var res = new List<IntPtr>();
    EnumWindows((h,l) => { uint p; GetWindowThreadProcessId(h, out p); if (p == pid) res.Add(h); return true; }, IntPtr.Zero);
    return res;
  }
  public static string Text(IntPtr h){ int n=GetWindowTextLength(h); if(n==0) return ""; var sb=new StringBuilder(n+2); GetWindowText(h,sb,sb.Capacity); return sb.ToString(); }
  public static bool Visible(IntPtr h){ return IsWindowVisible(h); }
}
'@
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$stamp = (Get-Date -Format 'yyyyMMdd-HHmmss')

$proc = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { Write-Host "[!] processo '$ProcessName' nao encontrado"; exit 1 }

# ---- coleta de elementos de todas as janelas do processo (inclui popups de menu) ----
function Get-ProcessElements {
  param([int]$ProcessId, [string]$WindowFilter = '')
  $all = New-Object System.Collections.ArrayList
  foreach ($h in [Inp]::WindowsOf([uint32]$ProcessId)) {
    if (-not [Inp]::Visible($h)) { continue }
    if ($WindowFilter -ne '' -and ([Inp]::Text($h) -notlike "*$WindowFilter*")) { continue }
    try {
      $root = [System.Windows.Automation.AutomationElement]::FromHandle($h)
      if ($null -eq $root) { continue }
      $desc = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
                             [System.Windows.Automation.Condition]::TrueCondition)
      foreach ($el in $desc) { $null = $all.Add($el) }
    } catch { }
  }
  return $all
}

function Show-Element($el) {
  try {
    $r = $el.Current.BoundingRectangle
    return ('name="{0}" | type={1} | id={2} | vis={3} | rect={4},{5} {6}x{7}' -f
      $el.Current.Name, $el.Current.ControlType.ProgrammaticName, $el.Current.AutomationId,
      $el.Current.IsOffscreen, [int]$r.X, [int]$r.Y, [int]$r.Width, [int]$r.Height)
  } catch { return '<invalido>' }
}

# ---- janela alvo para foco ----
$target = $proc.MainWindowHandle
if ($WindowTitle -ne '') {
  $target = [IntPtr]::Zero
  foreach ($h in [Inp]::WindowsOf([uint32]$proc.Id)) {
    if (-not [Inp]::Visible($h)) { continue }
    if ([Inp]::Text($h) -like "*$WindowTitle*") { $target = $h; break }
  }
  if ($target -eq [IntPtr]::Zero) { Write-Host "[!] janela '$WindowTitle' nao encontrada"; exit 2 }
}
[void][Inp]::SetForegroundWindow($target)
Start-Sleep -Milliseconds 700

# ---- escrita de texto ----
if ($SetAutomationId -ne '') {
  $els = Get-ProcessElements -ProcessId $proc.Id
  $el = $null
  foreach ($e in $els) { try { if ($e.Current.AutomationId -eq $SetAutomationId) { $el = $e; break } } catch { } }
  if ($null -eq $el) { Write-Host "[!] AutomationId '$SetAutomationId' nao encontrado"; exit 4 }
  $pattern = $el.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
  $pattern.SetValue($SetText)
  Write-Host "[+] escrito '$SetText' em '$SetAutomationId'"
  Start-Sleep -Milliseconds 900
}

# ---- marcacao de checkbox / radiobutton ----
if ($ToggleAutomationId -ne '') {
  $els = Get-ProcessElements -ProcessId $proc.Id
  $el = $null
  foreach ($e in $els) { try { if ($e.Current.AutomationId -eq $ToggleAutomationId) { $el = $e; break } } catch { } }
  if ($null -eq $el) { Write-Host "[!] AutomationId '$ToggleAutomationId' nao encontrado"; exit 4 }

  $done = $false
  try {
    $tp = $el.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    $tp.Toggle(); $done = $true
    Write-Host "[+] alternado '$ToggleAutomationId'"
  } catch { }
  if (-not $done) {
    try {
      $sp = $el.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
      $sp.Select(); $done = $true
      Write-Host "[+] selecionado '$ToggleAutomationId'"
    } catch { }
  }
  if (-not $done) { Write-Host "[!] nao foi possivel alternar '$ToggleAutomationId'"; exit 5 }
  Start-Sleep -Milliseconds 700
}

# ---- selecao do alvo do clique ----
$el = $null
if ($ClickAutomationId -ne '') {
  $els = Get-ProcessElements -ProcessId $proc.Id
  $matches = @()
  foreach ($e in $els) {
    try {
      if ($e.Current.AutomationId -ne $ClickAutomationId) { continue }
      if ($ControlType -ne '' -and $e.Current.ControlType.ProgrammaticName -notlike "*$ControlType*") { continue }
      $matches += $e
    } catch { }
  }
  Write-Host "[*] AutomationId '$ClickAutomationId': $($matches.Count) candidato(s)"
  if ($matches.Count -eq 0) { Write-Host "[!] nao encontrado"; exit 3 }
  $el = $matches[[Math]::Min($Index, $matches.Count - 1)]
}
elseif ($ClickName -ne '' -or $ClickExact -ne '') {
  $els = Get-ProcessElements -ProcessId $proc.Id
  $matches = @()
  foreach ($e in $els) {
    try {
      $n = $e.Current.Name
      if ($ControlType -ne '' -and $e.Current.ControlType.ProgrammaticName -notlike "*$ControlType*") { continue }
      if ($ClickExact -ne '') { if ($n -eq $ClickExact) { $matches += $e } }
      elseif ($Match -eq 'exact') { if ($n -eq $ClickName) { $matches += $e } }
      else { if ($n -like "*$ClickName*") { $matches += $e } }
    } catch { }
  }
  Write-Host "[*] elementos casando: $($matches.Count)"
  if ($matches.Count -eq 0) { Write-Host "[!] nenhum elemento encontrado para clique"; exit 3 }
  $el = $matches[[Math]::Min($Index, $matches.Count - 1)]
}

if ($Dump) {
  $els = Get-ProcessElements -ProcessId $proc.Id
  Write-Host "--- elementos do processo ($($els.Count)) ---"
  foreach ($e in $els) { Write-Host ('  ' + (Show-Element $e)) }
}

if ($null -ne $el) {
  Write-Host "[+] alvo: $(Show-Element $el)"

  $clicked = $false
  # InvokePattern e mais deterministico que clicar com o mouse.
  try {
    $ip = $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $ip.Invoke(); $clicked = $true
    Write-Host "[+] invocado via InvokePattern"
  } catch { }

  if (-not $clicked) {
    $r = $el.Current.BoundingRectangle
    $x = [int]($r.X + $r.Width / 2)
    $y = [int]($r.Y + $r.Height / 2)
    Write-Host "[+] clicando em ($x,$y)"
    [Inp]::Click($x, $y)
  }
  Start-Sleep -Milliseconds 900
}

if ($Keys -ne '') {
  Write-Host "[+] enviando teclas: $Keys"
  [System.Windows.Forms.SendKeys]::SendWait($Keys)
  Start-Sleep -Milliseconds 600
}

Start-Sleep -Seconds $WaitSeconds
Write-Host "[*] foreground agora: '$([Inp]::FgTitle())'"

$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap($b.Width, $b.Height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($b.X, $b.Y, 0, 0, $bmp.Size)
$g.Dispose()
$png = Join-Path $OutDir "$Tag-$stamp.png"
$bmp.Save($png, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
Write-Host "[+] screenshot: $png ($((Get-Item $png).Length) bytes)"
