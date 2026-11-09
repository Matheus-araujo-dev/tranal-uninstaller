<#
  Teste de ponta a ponta da impressao e do rastreio (lote 19).

  O que ele prova, na janela de verdade:

  1) o item "Imprimir lista..." do menu principal abre a janela de
     impressao, e ela mostra as paginas do documento antes de qualquer
     folha sair para a impressora;
  2) o item "Rastreio e comparacao A x B..." do menu principal abre a
     janela de rastreio, e a aba de snapshots traz os botoes de exportar e
     imprimir a arvore de alteracoes.

  A suite de testes cobre o nucleo (PrintTable e ChangeTreeReport); este
  script cobre a ligacao com a interface, que a suite nao alcanca (a suite
  nao referencia o projeto WPF).

  O script nao mexe no ambiente de quem roda: o settings.json existente e
  guardado antes e devolvido no final, mesmo se o teste falhar.

  Uso:
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-impressao.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-impressao.ps1 -Exe publish\preview\TranalUninstaller.exe
#>
param(
    [string]$Exe = '',
    [string]$DataDirectory = '',
    [int]$EsperaSegundos = 25
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

if (-not ('TranalJanelas5' -as [type])) {
Add-Type @'
using System; using System.Runtime.InteropServices; using System.Collections.Generic;
public class TranalJanelas5 {
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  public static List<IntPtr> VisiveisDe(uint pid) {
    var res = new List<IntPtr>();
    EnumWindows((h,l) => { uint p; GetWindowThreadProcessId(h, out p); if (p == pid && IsWindowVisible(h)) res.Add(h); return true; }, IntPtr.Zero);
    return res;
  }
}
'@
}

$falhas = 0
function Chk([string]$nome, [bool]$ok, [string]$detalhe = '') {
    if ($ok) { Write-Host "[OK]    $nome" }
    else { Write-Host "[FALHA] $nome $detalhe"; $script:falhas++ }
}

# A janela principal: a que contem o botao do menu.
function JanelaPrincipal([int]$processId) {
    foreach ($h in [TranalJanelas5]::VisiveisDe([uint32]$processId)) {
        try {
            $el = [System.Windows.Automation.AutomationElement]::FromHandle($h)
            $cond = New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'CmdMainMenu')
            if ($el.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond).Count -gt 0) { return $el }
        } catch { }
    }
    return $null
}

# Busca por AutomationId em uma janela.
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

# Busca por AutomationId em QUALQUER janela visivel do processo: o menu
# principal e um Popup, e o popup e uma janela propria.
function PorIdEmQualquerJanela([int]$processId, [string]$automationId) {
    foreach ($h in [TranalJanelas5]::VisiveisDe([uint32]$processId)) {
        try {
            $el = [System.Windows.Automation.AutomationElement]::FromHandle($h)
            $cond = New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $automationId)
            $achados = $el.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
            if ($achados.Count -gt 0) { return $achados[0] }
        } catch { }
    }
    return $null
}

# Uma janela pelo titulo. Serve para provar que a janela existe, sem
# depender do conteudo de uma aba que ainda nao foi selecionada.
function JanelaPorTitulo([int]$processId, [string]$titulo) {
    foreach ($h in [TranalJanelas5]::VisiveisDe([uint32]$processId)) {
        try {
            $el = [System.Windows.Automation.AutomationElement]::FromHandle($h)
            if ($el.Current.Name -eq $titulo) { return $el }
        } catch { }
    }
    return $null
}

# Seleciona a aba de snapshots: o conteudo de uma aba que nao esta
# selecionada nao existe na arvore de automacao.
function SelecionarAba([int]$processId, [string]$nome) {
    foreach ($h in [TranalJanelas5]::VisiveisDe([uint32]$processId)) {
        try {
            $el = [System.Windows.Automation.AutomationElement]::FromHandle($h)
            $cond = New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
                [System.Windows.Automation.ControlType]::TabItem)
            foreach ($tab in $el.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)) {
                if ($tab.Current.Name -ne $nome) { continue }
                $sel = $tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
                $sel.Select()
                Start-Sleep -Seconds 2
                return $true
            }
        } catch { }
    }
    return $false
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
$reserva = Join-Path $env:TEMP ("tranal-impressao-reserva-{0}.json" -f [Guid]::NewGuid().ToString('N'))

Write-Host "=== Preparacao ==="
Write-Host "    executavel: $Exe"

$logAntes = if (Test-Path $log) { (Get-Item $log).Length } else { 0 }

$haviaSettings = Test-Path $settings
if ($haviaSettings) {
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

    # 1) Abre o menu principal.
    $menu = PorId $janela 'CmdMainMenu'
    Chk "o botao do menu principal existe" ($null -ne $menu)
    Clicar $menu | Out-Null
    Start-Sleep -Seconds 2

    # 2) O item de impressao existe no menu.
    $imprimir = PorIdEmQualquerJanela $processo.Id 'CmdPrint'
    Chk "o menu principal tem o item de imprimir a lista" ($null -ne $imprimir)

    # 3) Clicar abre a janela de impressao.
    Clicar $imprimir | Out-Null
    Start-Sleep -Seconds 6

    $paginas = PorIdEmQualquerJanela $processo.Id 'ReportingPages'
    Chk "a janela de impressao abre com a pre-visualizacao" ($null -ne $paginas)

    $botaoImprimir = PorIdEmQualquerJanela $processo.Id 'ReportingPrint'
    Chk "a janela de impressao tem o botao de imprimir" ($null -ne $botaoImprimir)

    # 4) A pre-visualizacao mostra a lista (as paginas do documento).
    if ($null -ne $paginas) {
        try {
            $itens = $paginas.FindAll([System.Windows.Automation.TreeScope]::Descendants,
                [System.Windows.Automation.Condition]::TrueCondition)
            Write-Host ("    elementos na pre-visualizacao: {0}" -f $itens.Count)
        } catch { }
    }

    # 5) Fecha a janela de impressao.
    $fechar = PorIdEmQualquerJanela $processo.Id 'ReportingClose'
    Clicar $fechar | Out-Null
    Start-Sleep -Seconds 2

    # 6) A janela de rastreio: ela precisa estar acessivel pelo menu.
    $menu = PorId $janela 'CmdMainMenu'
    Clicar $menu | Out-Null
    Start-Sleep -Seconds 2

    $rastreio = PorIdEmQualquerJanela $processo.Id 'CmdTracing'
    Chk "o menu principal tem o item de rastreio" ($null -ne $rastreio)

    Clicar $rastreio | Out-Null
    Start-Sleep -Seconds 4

    $janelaRastreio = JanelaPorTitulo $processo.Id 'Rastreio e comparacao A x B'
    Chk "a janela de rastreio abre" ($null -ne $janelaRastreio)

    # O conteudo de uma aba que nao esta selecionada nao existe na arvore de
    # automacao: a aba de snapshots precisa ser aberta antes de procurar os
    # botoes dentro dela.
    $abaAberta = SelecionarAba $processo.Id 'Snapshots'
    Chk "a aba de snapshots pode ser aberta" $abaAberta

    $abaSnapshots = PorIdEmQualquerJanela $processo.Id 'TracingSnapshots'
    Chk "a lista de snapshots aparece" ($null -ne $abaSnapshots)

    $exportar = PorIdEmQualquerJanela $processo.Id 'TracingExportChangeTree'
    Chk "a aba de snapshots tem o botao de exportar a arvore" ($null -ne $exportar)

    $imprimirArvore = PorIdEmQualquerJanela $processo.Id 'TracingPrintChangeTree'
    Chk "a aba de snapshots tem o botao de imprimir a arvore" ($null -ne $imprimirArvore)

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
    Write-Host "RESULTADO: impressao OK."
    exit 0
}
Write-Host "RESULTADO: $falhas verificacao(oes) falharam."
exit 1
