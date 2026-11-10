<#
  Teste de ponta a ponta do lote 26: a remocao de entradas da lista do
  Windows em lote ("Batch Remove Entries").

  O que ele prova:
    1. a linha de comando remove a entrada de desinstalacao de um programa,
       em lote, e avisa que o programa NAO e desinstalado;
    2. a chave de desinstalacao some do registro;
    3. a interface tem a acao em lote no menu "Mais comandos".

  NADA de software instalado e tocado: o teste cria uma entrada de teste no
  HKCU do usuario que o executa, apaga essa entrada e limpa tudo no final.
  O programa de teste nao existe — a entrada e so o registro.

  Uso:
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-lote26.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-lote26.ps1 -Exe publish\preview\TranalUninstaller.exe -CliExe publish\cli\tranal.exe
#>
param(
    [string]$Exe = '',
    [string]$CliExe = '',
    [int]$EsperaSegundos = 30
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Exe)) { $Exe = Join-Path $repoRoot 'publish\preview\TranalUninstaller.exe' }
if (-not [System.IO.Path]::IsPathRooted($Exe)) { $Exe = Join-Path $repoRoot $Exe }
$ExeExiste = (Test-Path $Exe)

if ([string]::IsNullOrWhiteSpace($CliExe)) { $CliExe = Join-Path $repoRoot 'publish\cli\tranal.exe' }
if (-not [System.IO.Path]::IsPathRooted($CliExe)) { $CliExe = Join-Path $repoRoot $CliExe }
$CliExiste = (Test-Path $CliExe)

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

# A janela principal do aplicativo: a que tem a barra de status.
function JanelaPrincipal([int]$processId) {
    try {
        $raiz = [System.Windows.Automation.AutomationElement]::RootElement
        $condPid = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $processId)
        $janelas = $raiz.FindAll([System.Windows.Automation.TreeScope]::Children, $condPid)
        foreach ($janela in $janelas) {
            $achado = PorId $janela 'ProgramStatusText'
            if ($null -ne $achado) { return $janela }
        }
    } catch { }
    return $null
}

# O item de menu vive em uma janela propria (o popup). Por isso a busca e
# feita no desktop, filtrando pelo processo do aplicativo.
function ItemDeMenu([int]$processId, [string]$automationId) {
    try {
        $raiz = [System.Windows.Automation.AutomationElement]::RootElement
        $condPid = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $processId)
        $janelas = $raiz.FindAll([System.Windows.Automation.TreeScope]::Children, $condPid)
        foreach ($janela in $janelas) {
            $achado = PorId $janela $automationId
            if ($null -ne $achado) { return $achado }
        }
    } catch { }
    return $null
}

# ----------------------------------------------------------------------
# A entrada de teste: so o registro, nenhum programa de verdade
# ----------------------------------------------------------------------

$NomeTeste = 'Tranal Lote26 E2E'
$ChaveTeste = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\Tranal Lote26 E2E'

function CriaEntradaDeTeste() {
    $chave = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($ChaveTeste)
    try {
        $chave.SetValue('DisplayName', $NomeTeste)
        $chave.SetValue('DisplayVersion', '1.0')
        $chave.SetValue('Publisher', 'Tranal Testes')
        $chave.SetValue('UninstallString', 'C:\Windows\System32\cmd.exe /c exit')
        $chave.SetValue('InstallLocation', 'C:\Tranal\Lote26 E2E')
    } finally {
        $chave.Close()
    }
}

function EntradaExiste() {
    $chave = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($ChaveTeste)
    if ($null -eq $chave) { return $false }
    $chave.Close()
    return $true
}

function ApagaEntradaDeTeste() {
    try {
        [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($ChaveTeste, $false)
    } catch { }
}

Write-Host "=== Preparacao ==="
Write-Host "    executavel: $Exe (existe: $ExeExiste)"
Write-Host "    linha de comando: $CliExe (existe: $CliExiste)"

ApagaEntradaDeTeste
CriaEntradaDeTeste
Chk "a entrada de teste foi criada no HKCU" (EntradaExiste)

$processo = $null
try {
    Write-Host ""
    Write-Host "=== Execucao ==="

    # ------------------------------------------------------------------
    # 1. A linha de comando remove a entrada, em lote.
    # ------------------------------------------------------------------
    if ($CliExiste) {
        $saida = ''
        try {
            $saida = & $CliExe /remove-entries $NomeTeste /yes 2>&1 | Out-String
        } catch { $saida = $_.Exception.Message }

        Write-Host ($saida.Trim())
        Write-Host ""

        Chk "a linha de comando diz que o programa nao e desinstalado" ($saida -match 'NAO e desinstalado')
        Chk "a linha de comando conta a entrada removida" ($saida -match 'Removidas: 1')
        Chk "a entrada de desinstalacao sumiu do registro" (-not (EntradaExiste))
    }
    else {
        Write-Host "    (linha de comando nao publicada: a verificacao da CLI foi pulada)"
    }

    # ------------------------------------------------------------------
    # 2. A interface tem a acao em lote.
    # ------------------------------------------------------------------
    if ($ExeExiste) {
        Add-Type -AssemblyName UIAutomationClient
        Add-Type -AssemblyName UIAutomationTypes

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

        if ($null -ne $janela) {
            $mais = PorId $janela 'MoreCommandsButton'
            if ($null -eq $mais) {
                $achados = $janela.FindAll([System.Windows.Automation.TreeScope]::Descendants,
                    (New-Object System.Windows.Automation.PropertyCondition(
                        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
                        [System.Windows.Automation.ControlType]::Button)))
                foreach ($b in $achados) {
                    try { if ($b.Current.Name -match 'Mais comandos') { $mais = $b; break } } catch { }
                }
            }
            Chk "o botao de mais comandos existe" ($null -ne $mais)

            Clicar $mais | Out-Null
            Start-Sleep -Seconds 2

            $item = ItemDeMenu $processo.Id 'CmdRemoveEntriesBatch'
            Chk "o menu tem a acao de remover entradas em lote" ($null -ne $item)
            if ($null -ne $item) {
                try { Write-Host ("    item: {0}" -f $item.Current.Name) } catch { }
            }
        }
    }
    else {
        Write-Host "    (executavel nao publicado: a verificacao da interface foi pulada)"
    }
}
finally {
    Write-Host ""
    Write-Host "=== Limpeza ==="

    if (EntradaExiste) {
        ApagaEntradaDeTeste
        Write-Host "    entrada de teste removida"
    }

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
