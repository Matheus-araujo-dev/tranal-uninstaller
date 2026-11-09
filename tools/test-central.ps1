<#
  Teste de ponta a ponta da Central (IOBIT T7#34).

  O que ele prova: na janela de verdade, o botao da Central na barra lateral
  abre a tela da Central, a tela carrega (a linha de status sai do texto de
  "verificando") e os comandos da tela existem.

  A suite de testes cobre o nucleo (CentralOverview); este script cobre a
  ligacao com a interface, que a suite nao alcanca (a suite nao referencia o
  projeto WPF).

  O script nao mexe no ambiente de quem roda: o settings.json existente e
  guardado antes e devolvido no final, mesmo se o teste falhar.

  A busca dos botoes e ANCORADA na janela do aplicativo (e nao na tela
  inteira): MEDIDO, a janela de splash tem o mesmo titulo da janela principal
  ("Tranal Uninstaller Pro").

  Uso:
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-central.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-central.ps1 -Exe publish\preview\TranalUninstaller.exe
#>
param(
    [string]$Exe = '',
    [string]$DataDirectory = '',
    [int]$EsperaSegundos = 25,
    [int]$EsperaCargaSegundos = 60
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

if (-not ('TranalJanelas4' -as [type])) {
Add-Type @'
using System; using System.Text; using System.Runtime.InteropServices; using System.Collections.Generic;
public class TranalJanelas4 {
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
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
}
'@
}

$falhas = 0
function Chk([string]$nome, [bool]$ok, [string]$detalhe = '') {
    if ($ok) { Write-Host "[OK]    $nome" }
    else { Write-Host "[FALHA] $nome $detalhe"; $script:falhas++ }
}

# Elemento da janela que CONTEM o botao da Central: e a janela principal.
# A splash tem o mesmo titulo, mas nao tem os botoes da barra lateral.
function JanelaPrincipal([int]$processId) {
    foreach ($h in [TranalJanelas4]::VisiveisDe([uint32]$processId)) {
        try {
            $el = [System.Windows.Automation.AutomationElement]::FromHandle($h)
            $cond = New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'ModuleCentral')
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

function TextoDe($elemento) {
    if ($null -eq $elemento) { return '' }
    try {
        $p = $elemento.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
        return [string]$p.Current.Value
    } catch { }
    try {
        return [string]$elemento.Current.Name
    } catch { }
    return ''
}

function Clicar($elemento) {
    if ($null -eq $elemento) { return $false }
    try {
        $elemento.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        return $true
    } catch { return $false }
}

# Os textos de "carregando" do catalogo de idioma. Nenhum deles e estado
# final: enquanto um deles estiver na linha, a Central ainda esta carregando.
$carregando = @('Verificando...', 'Scanning...', 'Carregando...', 'Loading...')

# Os estados FINAIS da Central, medidos no codigo (CentralView.ReloadAsync):
# a linha de status passa a ser o resumo ("N item(ns) precisam de atencao")
# ou o texto de "nada precisa de atencao agora". Nenhum dos dois e um texto
# de carregando — por isso o teste espera por eles, e nao por "deixou de
# carregar": aceitar "Carregando..." seria um falso positivo.
$resumoFinal = '^[0-9]+ item\((ns|s)\) (precisam de atencao|need attention)$'
$vazioFinal = @(
    'Nada precisa de atencao agora.',
    'Nothing needs attention right now.'
)

function Eh-Final([string]$texto) {
    if ([string]::IsNullOrWhiteSpace($texto)) { return $false }
    if ($carregando -contains $texto) { return $false }
    if ($vazioFinal -contains $texto) { return $true }
    return $texto -match $resumoFinal
}

$settings = Join-Path $DataDirectory 'settings.json'
$log = Join-Path $DataDirectory 'erro-inicializacao.log'
$reserva = Join-Path $env:TEMP ("tranal-central-reserva-{0}.json" -f [Guid]::NewGuid().ToString('N'))

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

    # 1) O botao da Central existe na barra lateral.
    $botao = PorId $janela 'ModuleCentral'
    Chk "o botao da Central existe na barra lateral" ($null -ne $botao)

    # 2) Clicar abre a tela da Central.
    Clicar $botao | Out-Null
    Start-Sleep -Seconds 4

    $refresh = PorId $janela 'CentralRefresh'
    Chk "a tela da Central abriu (comando de recarregar)" ($null -ne $refresh)

    $abrir = PorId $janela 'CentralOpen'
    Chk "a tela da Central tem o comando de abrir a ferramenta" ($null -ne $abrir)

    # 3) O titulo do painel e o da Central.
    $titulo = TextoDe (PorId $janela 'PanelTitle')
    Chk "o titulo do painel e o da Central" ($titulo -eq 'Central') "titulo='$titulo'"

    # 4) A lista existe.
    $lista = PorId $janela 'PanelItems'
    Chk "a lista da Central existe" ($null -ne $lista)

    # 5) A carga acaba: a linha de status chega ao resumo (ou ao texto de
    #    "nada precisa de atencao").
    $limiteCarga = (Get-Date).AddSeconds($EsperaCargaSegundos)
    $status = ''
    while ((Get-Date) -lt $limiteCarga) {
        $status = TextoDe (PorId $janela 'PanelStatus')
        if (Eh-Final $status) { break }
        Start-Sleep -Milliseconds 800
    }

    Chk "a Central carregou (a linha de status chegou ao resumo)" (Eh-Final $status) "status='$status'"

    # 6) As linhas da Central. MEDIDO no aplicativo de verdade: o ListView do
    #    painel usa GridView, e por isso os itens aparecem no UIA como
    #    ControlType.DataItem (e nao como ListItem).
    $itens = 0
    $cabecalhos = @()
    if ($null -ne $lista) {
        try {
            $condItem = New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
                [System.Windows.Automation.ControlType]::DataItem)
            $achados = $lista.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condItem)
            $itens = $achados.Count

            $condHead = New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
                [System.Windows.Automation.ControlType]::HeaderItem)
            foreach ($h in $lista.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condHead)) {
                $cabecalhos += [string]$h.Current.Name
            }
        } catch { Write-Host ("    aviso: nao deu para ler os itens: " + $_.Exception.Message) }
    }
    Write-Host ("    itens na Central: {0}" -f $itens)
    Write-Host ("    colunas: {0}" -f ($cabecalhos -join ', '))

    # 7) As colunas da Central sao as quatro medidas (item, area, prioridade,
    #    detalhe). O texto das duas ultimas existe nos dois idiomas.
    $temPrioridade = ($cabecalhos -contains 'Prioridade') -or ($cabecalhos -contains 'Priority')
    $temDetalhe = ($cabecalhos -contains 'Detalhe') -or ($cabecalhos -contains 'Detail')
    Chk "a Central mostra as colunas de prioridade e detalhe" ($temPrioridade -and $temDetalhe) `
        ("cabecalhos=" + ($cabecalhos -join ', '))

    # 8) O resumo diz quantos itens precisam de atencao: se ele diz que ha N,
    #    a lista tem de mostrar pelo menos um.
    if ($status -match '^([0-9]+) item\((ns|s)\)') {
        $anunciado = [int]$Matches[1]
        Chk "a lista mostra os itens que o resumo anuncia" `
            (($anunciado -eq 0) -or ($itens -ge 1)) "anunciado=$anunciado itens=$itens"
    }
    Write-Host ("    status final: {0}" -f $status)

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
    Write-Host "RESULTADO: central OK."
    exit 0
}
Write-Host "RESULTADO: $falhas verificacao(oes) falharam."
exit 1
