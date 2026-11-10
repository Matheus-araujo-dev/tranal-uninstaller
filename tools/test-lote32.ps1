<#
  Teste de ponta a ponta do lote 32: os modulos orfaos do nucleo que
  ganharam dono de produto.

  O que ele prova:
    1. a ferramenta "Operacoes Pendentes" abre pela barra lateral e mostra
       os comandos da fila do Windows;
    2. a ferramenta "Backup Diario do Registro" abre pela barra lateral e
       mostra o comando de execucao;
    3. o comando "Desinstalacao Personalizada" abre a janela, e o filtro por
       palavra-chave de verdade esvazia a lista quando nada casa;
    4. o comando "Monitor de Instalacao" abre a janela, com os botoes de
       iniciar e parar.

  Nenhum software e instalado ou removido: o teste so ABRE telas, digita uma
  palavra-chave que nao existe e fecha. O monitor de instalacao nao e
  iniciado (iniciar exigiria um programa real), e o backup diario nao e
  rodado (o `reg.exe export` leva segundos e grava um backup de verdade).

  Uso:
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-lote32.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-lote32.ps1 -Exe publish\preview\TranalUninstaller.exe
#>
param(
    [string]$Exe = '',
    [int]$EsperaSegundos = 60
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Exe)) { $Exe = Join-Path $repoRoot 'publish\preview\TranalUninstaller.exe' }
if (-not [System.IO.Path]::IsPathRooted($Exe)) { $Exe = Join-Path $repoRoot $Exe }
$ExeExiste = (Test-Path $Exe)

$falhas = 0
function Chk([string]$nome, [bool]$ok, [string]$detalhe = '') {
    if ($ok) { Write-Host "[OK]    $nome" }
    else { Write-Host "[FALHA] $nome $detalhe"; $script:falhas++ }
}

function Clicar($elemento) {
    if ($null -eq $elemento) { return $false }
    try {
        $elemento.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        return $true
    } catch { return $false }
}

# Clica com o mouse no centro do elemento: e o que aciona os botoes que
# nao tem InvokePattern.
function ClicarMouse($elemento) {
    if ($null -eq $elemento) { return $false }

    try {
        if (-not ('TranalMouse32' -as [type])) {
            Add-Type -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
[DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
'@ -Name Mouse -Namespace TranalMouse32
        }

        $retangulo = $elemento.Current.BoundingRectangle
        if ($retangulo.Width -le 0 -or $retangulo.Height -le 0) { return $false }

        $x = [int]($retangulo.X + ($retangulo.Width / 2))
        $y = [int]($retangulo.Y + ($retangulo.Height / 2))

        [TranalMouse32.Mouse]::SetCursorPos($x, $y) | Out-Null
        Start-Sleep -Milliseconds 250

        [TranalMouse32.Mouse]::mouse_event(0x0002, 0, 0, 0, 0)
        Start-Sleep -Milliseconds 80
        [TranalMouse32.Mouse]::mouse_event(0x0004, 0, 0, 0, 0)
        return $true
    } catch { return $false }
}

function ClicarPorId($janela, [string]$automationId) {
    $achado = PorId $janela $automationId
    if ($null -eq $achado) { return $false }
    if (Clicar $achado) { return $true }
    return (ClicarMouse $achado)
}

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

function JanelaComId([int]$processId, [string]$automationId) {
    try {
        $raiz = [System.Windows.Automation.AutomationElement]::RootElement
        $condPid = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $processId)
        foreach ($janela in $raiz.FindAll([System.Windows.Automation.TreeScope]::Children, $condPid)) {
            if ($null -ne (PorId $janela $automationId)) { return $janela }
        }
    } catch { }
    return $null
}

# Procura um identificador em TODAS as janelas do processo: o menu de
# comandos ("Mais comandos") e um popup, e popup e janela propria — ele nao
# fica dentro da janela principal na arvore de automacao.
function EmAlgumaJanela([int]$processId, [string]$automationId) {
    try {
        $raiz = [System.Windows.Automation.AutomationElement]::RootElement
        $condPid = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $processId)
        foreach ($janela in $raiz.FindAll([System.Windows.Automation.TreeScope]::Children, $condPid)) {
            $achado = PorId $janela $automationId
            if ($null -ne $achado) { return $achado }
        }
    } catch { }
    return $null
}

function DoTipo($janela, [string]$tipo) {
    if ($null -eq $janela) { return @() }
    try {
        $cond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::$tipo)
        return $janela.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
    } catch { return @() }
}

function DoTipoOu($janela, [string[]]$tipos) {
    $achados = @()
    foreach ($tipo in $tipos) { $achados += DoTipo $janela $tipo }
    return $achados
}

function TextoDoItem($item) {
    if ($null -eq $item) { return '' }
    try {
        $partes = @()
        foreach ($t in DoTipo $item 'Text') { $partes += [string]$t.Current.Name }
        if ($partes.Count -gt 0) { return ($partes -join ' ') }
        return [string]$item.Current.Name
    } catch { return '' }
}

function Preencher($elemento, [string]$texto) {
    if ($null -eq $elemento) { return $false }
    try {
        $elemento.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($texto)
        return $true
    } catch { return $false }
}

function TextoDe($elemento) {
    if ($null -eq $elemento) { return '' }
    try { return [string]$elemento.Current.Name } catch { return '' }
}

# Abre o menu "Mais comandos" e devolve o item com o identificador pedido.
# O menu e um ContextMenu: ele so existe enquanto esta aberto.
function AbrirComando([int]$processId, $principal, [string]$automationId) {
    ClicarPorId $principal 'CmdMoreCommands' | Out-Null

    $limite = (Get-Date).AddSeconds(15)
    while ((Get-Date) -lt $limite) {
        Start-Sleep -Milliseconds 400
        $item = EmAlgumaJanela $processId $automationId
        if ($null -ne $item) { return $item }
    }

    return $null
}

$processo = $null

try {
    Write-Host "=== Preparacao ==="
    Write-Host "    executavel: $Exe (existe: $ExeExiste)"

    if (-not $ExeExiste) {
        Write-Host ""
        Write-Host "EXECUTAVEL NAO PUBLICADO: a verificacao da interface foi pulada."
        exit 0
    }

    Add-Type -AssemblyName UIAutomationClient
    Add-Type -AssemblyName UIAutomationTypes

    $processo = Start-Process -FilePath $Exe -PassThru
    Write-Host ("    pid: {0}" -f $processo.Id)

    $limite = (Get-Date).AddSeconds($EsperaSegundos)
    $principal = $null
    while ((Get-Date) -lt $limite -and -not $principal) {
        Start-Sleep -Milliseconds 700
        if (-not (Get-Process -Id $processo.Id -ErrorAction SilentlyContinue)) { break }
        $principal = JanelaComId $processo.Id 'ProgramStatusText'
    }
    Chk "a janela principal do Tranal aparece" ($null -ne $principal)

    if ($null -eq $principal) {
        Write-Host "FALHA: a janela principal nao apareceu."
        exit 1
    }

    # ------------------------------------------------------------------
    # 1. A ferramenta "Operacoes Pendentes".
    # ------------------------------------------------------------------
    Write-Host ""
    Write-Host "=== A barra lateral ==="

    ClicarPorId $principal 'ToolPendingOperations' | Out-Null
    Start-Sleep -Seconds 2

    $atualizar = PorId $principal 'PendingRefresh'
    Chk "a ferramenta de operacoes pendentes abre" ($null -ne $atualizar)
    Chk "a tela da fila tem o comando de aplicar agora" ($null -ne (PorId $principal 'PendingApply'))
    Chk "a tela da fila tem o comando de remover da fila" ($null -ne (PorId $principal 'PendingRemove'))

    # ------------------------------------------------------------------
    # 2. A ferramenta "Backup Diario do Registro".
    # ------------------------------------------------------------------
    ClicarPorId $principal 'ToolDailyRegistryBackup' | Out-Null
    Start-Sleep -Seconds 2

    Chk "a ferramenta de backup diario abre" ($null -ne (PorId $principal 'DailyBackupRefresh'))
    Chk "a tela do backup diario tem o comando de executar agora" ($null -ne (PorId $principal 'DailyBackupRun'))

    # ------------------------------------------------------------------
    # 3. A desinstalacao personalizada.
    # ------------------------------------------------------------------
    Write-Host ""
    Write-Host "=== A desinstalacao personalizada ==="

    $itemMenu = AbrirComando $processo.Id $principal 'CmdManualRemoval'
    Chk "o menu de comandos tem a desinstalacao personalizada" ($null -ne $itemMenu)

    if ($null -ne $itemMenu) {
        Clicar $itemMenu | Out-Null
        Start-Sleep -Seconds 3

        $campo = EmAlgumaJanela $processo.Id 'ManualKeywordBox'
        Chk "a janela da desinstalacao personalizada abre" ($null -ne $campo)
        Chk "a janela tem o botao de buscar" ($null -ne (EmAlgumaJanela $processo.Id 'ManualSearchButton'))
        Chk "a janela tem o botao de exportar" ($null -ne (EmAlgumaJanela $processo.Id 'ManualExportButton'))
        Chk "a janela tem o botao de remover" ($null -ne (EmAlgumaJanela $processo.Id 'ManualRemoveButton'))

        if ($null -ne $campo) {
            Preencher $campo 'zzzqqqnaoexistequalquer' | Out-Null
            Clicar (EmAlgumaJanela $processo.Id 'ManualSearchButton') | Out-Null
            Start-Sleep -Seconds 2

            $status = EmAlgumaJanela $processo.Id 'ManualStatusText'
            Chk "o filtro por palavra-chave responde" ($null -ne $status)
            Chk "o filtro esvazia a lista quando nada casa" ((TextoDe $status) -match 'Nenhum programa')
        }

        Clicar (EmAlgumaJanela $processo.Id 'ManualCloseButton') | Out-Null
        Start-Sleep -Seconds 2
    }

    # ------------------------------------------------------------------
    # 4. O monitor de instalacao.
    # ------------------------------------------------------------------
    Write-Host ""
    Write-Host "=== O monitor de instalacao ==="

    $itemMenu = AbrirComando $processo.Id $principal 'CmdInstallMonitor'
    Chk "o menu de comandos tem o monitor de instalacao" ($null -ne $itemMenu)

    if ($null -ne $itemMenu) {
        Clicar $itemMenu | Out-Null
        Start-Sleep -Seconds 3

        Chk "a janela do monitor abre" ($null -ne (EmAlgumaJanela $processo.Id 'MonitorProgramNameBox'))
        Chk "a janela tem o botao de iniciar" ($null -ne (EmAlgumaJanela $processo.Id 'MonitorStartButton'))
        Chk "a janela tem o botao de parar" ($null -ne (EmAlgumaJanela $processo.Id 'MonitorStopButton'))
        Chk "a janela tem a lista do que mudou" ($null -ne (EmAlgumaJanela $processo.Id 'MonitorItemsList'))

        Clicar (EmAlgumaJanela $processo.Id 'MonitorCloseButton') | Out-Null
        Start-Sleep -Seconds 2
    }
}
finally {
    Write-Host ""
    Write-Host "=== Limpeza ==="

    if ($null -ne $processo) {
        try { Stop-Process -Id $processo.Id -Force -ErrorAction SilentlyContinue } catch { }
    }
}

Write-Host ""
if ($falhas -eq 0) {
    Write-Host "TODAS AS VERIFICACOES PASSARAM"
    exit 0
}

Write-Host ("FALHAS: {0}" -f $falhas)
exit 1
