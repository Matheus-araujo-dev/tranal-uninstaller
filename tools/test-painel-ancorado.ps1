<#
  Teste de ponta a ponta do painel ancoravel (UNINSTALLTOOL T6#85).

  O que ele prova: na janela de verdade, o botao que fixa o painel da
  ferramenta abre o painel na coluna da lateral, e o botao que fecha solta
  esse painel. A suite de testes cobre o nucleo (DockedPanel e PanelPreview);
  este script cobre a ligacao com a interface, que a suite nao alcanca (a
  suite nao referencia o projeto WPF).

  O script nao mexe no ambiente de quem roda: o settings.json existente e
  guardado antes e devolvido no final, mesmo se o teste falhar.

  A busca dos botoes e ANCORADA na janela do aplicativo (e nao na tela
  inteira): MEDIDO, a janela de splash tem o mesmo titulo da janela
  principal ("Tranal Uninstaller Pro"), e a busca pela tela inteira ou
  encontra a splash (que nao tem os botoes) ou demora demais.

  Uso:
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-painel-ancorado.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-painel-ancorado.ps1 -Exe publish\preview\TranalUninstaller.exe
#>
param(
    [string]$Exe = '',
    [string]$DataDirectory = '',
    [int]$EsperaSegundos = 20
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Exe)) { $Exe = Join-Path $repoRoot 'publish\preview\TranalUninstaller.exe' }
if (-not [System.IO.Path]::IsPathRooted($Exe)) { $Exe = Join-Path $repoRoot $Exe }

if ([string]::IsNullOrWhiteSpace($DataDirectory)) {
    $DataDirectory = Join-Path $env:LOCALAPPDATA 'Tranal Group\Tranal Uninstaller Pro'
}

if (-not (Test-Path $Exe)) { throw "Executavel nao encontrado: $Exe" }

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

if (-not ('TranalJanelas3' -as [type])) {
Add-Type @'
using System; using System.Text; using System.Runtime.InteropServices; using System.Collections.Generic;
public class TranalJanelas3 {
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  public static List<IntPtr> VisiveisDe(uint pid) {
    var res = new List<IntPtr>();
    EnumWindows((h,l) => {
      uint p; GetWindowThreadProcessId(h, out p);
      if (p == pid && IsWindowVisible(h)) res.Add(h);
      return true;
    }, IntPtr.Zero);
    return res;
  }
  public static string Texto(IntPtr h){ int n=GetWindowTextLength(h); if(n==0) return ""; var sb=new StringBuilder(n+2); GetWindowText(h,sb,sb.Capacity); return sb.ToString(); }
}
'@
}

$falhas = 0
function Chk([string]$nome, [bool]$ok, [string]$detalhe = '') {
    if ($ok) { Write-Host "[OK]    $nome" }
    else { Write-Host "[FALHA] $nome $detalhe"; $script:falhas++ }
}

# Elemento da janela que CONTEM o botao de fixar: e a janela principal.
# A splash tem o mesmo titulo, mas nao tem os botoes.
function JanelaPrincipal([int]$processId) {
    foreach ($h in [TranalJanelas3]::VisiveisDe([uint32]$processId)) {
        try {
            $el = [System.Windows.Automation.AutomationElement]::FromHandle($h)
            $cond = New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'CmdDockPanel')
            $achados = $el.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
            if ($achados.Count -gt 0) { return $el }
        } catch { }
    }
    return $null
}

# Busca por AutomationId DENTRO de uma janela (e nao na tela inteira).
function PorId($janela, [string]$automationId) {
    if ($null -eq $janela) { return $null }
    try {
        $cond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $automationId)
        $achados = $janela.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
        if ($achados.Count -gt 0) { return $achados[0] }
    } catch { }
    return $null
}

function Clicar($elemento) {
    if ($null -eq $elemento) { return $false }
    try {
        $elemento.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        return $true
    } catch { return $false }
}

$settings = Join-Path $DataDirectory 'settings.json'
$log = Join-Path $DataDirectory 'erro-inicializacao.log'
$reserva = Join-Path $env:TEMP ("tranal-painel-reserva-{0}.json" -f [Guid]::NewGuid().ToString('N'))

Write-Host "=== Preparacao ==="
Write-Host "    executavel: $Exe"

$logAntes = if (Test-Path $log) { (Get-Item $log).Length } else { 0 }

$haviaSettings = Test-Path $settings
if ($haviaSettings) {
    # Copia (e nao move): o aplicativo sobe no estado normal, sem o
    # primeiro lancamento, e o arquivo e devolvido no final.
    [System.IO.File]::Copy($settings, $reserva, $true)
    Write-Host "    settings.json guardado em $reserva"
}

$processo = $null
try {
    Write-Host ""
    Write-Host "=== Execucao ==="
    $processo = Start-Process -FilePath $Exe -PassThru
    Write-Host ("    pid: {0}" -f $processo.Id)

    $limite = (Get-Date).AddSeconds($EsperaSegundos)
    $janela = $null
    while ((Get-Date) -lt $limite -and -not $janela) {
        Start-Sleep -Milliseconds 700
        if (-not (Get-Process -Id $processo.Id -ErrorAction SilentlyContinue)) { break }
        $janela = JanelaPrincipal $processo.Id
    }

    Chk "a janela principal do Tranal aparece" ($null -ne $janela)
    if (-not $janela) { throw "A janela principal nao apareceu: o teste nao tem como continuar." }

    # 1) O painel ancorado NAO existe antes de fixar.
    Chk "o painel ancorado comeca fechado" ($null -eq (PorId $janela 'CmdDockPanelClose'))

    # 2) Abre uma ferramenta: e o botao de fixar que precisa saber qual
    #    ferramenta e.
    $ferramenta = PorId $janela 'ToolAutorunManager'
    Chk "a ferramenta AutorunManager existe na barra lateral" ($null -ne $ferramenta)
    Clicar $ferramenta | Out-Null
    Start-Sleep -Seconds 5

    # 3) Fixa o painel.
    $fixar = PorId $janela 'CmdDockPanel'
    Chk "o botao de fixar existe" ($null -ne $fixar)
    Clicar $fixar | Out-Null
    Start-Sleep -Seconds 5

    # 4) O painel ancorado apareceu na lateral.
    $fechar = PorId $janela 'CmdDockPanelClose'
    Chk "fixar abre o painel ancorado na lateral" ($null -ne $fechar)

    # 5) A preferencia foi gravada.
    Chk "a preferencia do painel ancorado foi gravada" `
        ((Test-Path $settings) -and ([System.IO.File]::ReadAllText($settings) -match '"DockedToolId"\s*:\s*"AutorunManager"'))

    # 6) Solta o painel: o botao de fechar tem de sumir de novo.
    Clicar $fechar | Out-Null
    Start-Sleep -Seconds 4
    Chk "o botao de fechar solta o painel" ($null -eq (PorId $janela 'CmdDockPanelClose'))

    $logDepois = if (Test-Path $log) { (Get-Item $log).Length } else { 0 }
    Chk "nenhuma excecao nova no log de inicializacao" ($logDepois -eq $logAntes) `
        ("antes $logAntes, depois $logDepois")
}
finally {
    if ($processo) { Stop-Process -Id $processo.Id -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Seconds 2

    if ($haviaSettings -and (Test-Path $reserva)) {
        [System.IO.File]::Copy($reserva, $settings, $true)
        Write-Host "    settings.json original devolvido"
    }
}

Write-Host ""
if ($falhas -eq 0) {
    Write-Host "RESULTADO: painel ancoravel OK."
    exit 0
}
Write-Host "RESULTADO: $falhas verificacao(oes) falharam."
exit 1
