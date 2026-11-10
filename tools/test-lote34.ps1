<#
  Teste de ponta a ponta do lote 34: o que o nucleo ja sabia e a tela nao dizia.

  O que ele prova:
    1. a linha de comando exporta a varredura de sobras em XML de verdade
       (o arquivo e criado, e o XML tem a raiz esperada);
    2. a tela de opcoes tem o comando de reconstruir o cache, e ele responde
       (a caixa de confirmacao aparece);
    3. a tela de desinstalacao personalizada tem os dois button de exportar a
       varredura: o CSV (que ja existia) e o XML (que o lote 34 acrescenta).

  Nenhum software e instalado ou removido: o teste so VARRE (leitura), grava
  UM arquivo XML na pasta temporaria e apaga o cache de programas - que a
  proxima varredura reconstroi. O cache apagado e o unico estado tocado, e o
  programa avisa que ele vai ser reconstruido.

  Uso:
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-lote34.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-lote34.ps1 -Exe publish\preview\TranalUninstaller.exe -CliExe publish\cli\tranal.exe
#>
param(
    [string]$Exe = '',
    [string]$CliExe = '',
    [int]$EsperaSegundos = 60
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

# Clica com o mouse no centro do elemento: e o que aciona os botoes que
# nao tem InvokePattern.
function ClicarMouse($elemento) {
    if ($null -eq $elemento) { return $false }

    try {
        if (-not ('TranalMouse34' -as [type])) {
            Add-Type -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
[DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
'@ -Name Mouse -Namespace TranalMouse34
        }

        $retangulo = $elemento.Current.BoundingRectangle
        if ($retangulo.Width -le 0 -or $retangulo.Height -le 0) { return $false }

        $x = [int]($retangulo.X + ($retangulo.Width / 2))
        $y = [int]($retangulo.Y + ($retangulo.Height / 2))

        [TranalMouse34.Mouse]::SetCursorPos($x, $y) | Out-Null
        Start-Sleep -Milliseconds 250

        [TranalMouse34.Mouse]::mouse_event(0x0002, 0, 0, 0, 0)
        Start-Sleep -Milliseconds 80
        [TranalMouse34.Mouse]::mouse_event(0x0004, 0, 0, 0, 0)
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
# comandos ("Mais comandos") e um popup, e popup e janela propria - ele nao
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

function TextoDe($elemento) {
    if ($null -eq $elemento) { return '' }
    try { return [string]$elemento.Current.Name } catch { return '' }
}

# Abre o menu "Mais comandos" e devolve o item com o identificador pedido.
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

# Roda a linha de comando e devolve a saida (stdout + stderr) e o codigo de
# saida. A saida vai para arquivo: o PowerShell transforma o stderr de um
# comando nativo em registro de erro, e com $ErrorActionPreference='Stop' isso
# engolia o codigo de saida - justamente o que o teste precisa ver.
function RodarCli([string]$cli, [string[]]$argumentos) {
    $id = [Guid]::NewGuid().ToString('N')
    $arquivoSaida = Join-Path $env:TEMP ("lote34-out-$id.txt")
    $arquivoErro = Join-Path $env:TEMP ("lote34-err-$id.txt")
    $texto = ''
    $codigo = -1

    try {
        $preparados = @()
        foreach ($argumento in $argumentos) {
            if ($argumento -match '\s') { $preparados += ('"' + $argumento + '"') }
            else { $preparados += $argumento }
        }

        $linha = Start-Process -FilePath $cli -ArgumentList $preparados -NoNewWindow -Wait -PassThru `
            -RedirectStandardOutput $arquivoSaida -RedirectStandardError $arquivoErro
        $codigo = $linha.ExitCode

        if ([System.IO.File]::Exists($arquivoSaida)) { $texto += [System.IO.File]::ReadAllText($arquivoSaida) }
        if ([System.IO.File]::Exists($arquivoErro)) { $texto += [System.IO.File]::ReadAllText($arquivoErro) }
    }
    catch {
        $texto = [string]$_.Exception.Message
    }
    finally {
        if ([System.IO.File]::Exists($arquivoSaida)) { [System.IO.File]::Delete($arquivoSaida) }
        if ([System.IO.File]::Exists($arquivoErro)) { [System.IO.File]::Delete($arquivoErro) }
    }

    return @{ Saida = $texto; Codigo = $codigo }
}

$processo = $null
$arquivoXml = Join-Path $env:TEMP ("lote34-sobras-" + [Guid]::NewGuid().ToString('N') + ".xml")

try {
    Write-Host "=== Preparacao ==="
    Write-Host "    executavel: $Exe (existe: $ExeExiste)"
    Write-Host "    linha de comando: $CliExe (existe: $CliExiste)"
    Write-Host "    arquivo de teste: $arquivoXml"

    # ------------------------------------------------------------------
    # 1. A linha de comando exporta a varredura em XML.
    # ------------------------------------------------------------------
    if ($CliExiste) {
        Write-Host ""
        Write-Host "=== A exportacao da varredura em XML ==="

        $resultado = RodarCli $CliExe @('/inspection-leftovers', $arquivoXml)
        Write-Host ("    saida: " + (($resultado.Saida -replace '\s+', ' ').Trim()))
        Chk "a linha de comando exporta a varredura" ($resultado.Codigo -eq 0)

        $existe = [System.IO.File]::Exists($arquivoXml)
        Chk "o arquivo XML foi gravado" $existe

        if ($existe) {
            $xml = [System.IO.File]::ReadAllText($arquivoXml)
            Chk "o XML tem a raiz da varredura" ($xml -match '<LeftoverScan')
            Chk "o XML declara quantos itens tem" ($xml -match 'count="\d+"')
        }
    }
    else {
        Write-Host "    (linha de comando nao publicada: a verificacao da CLI foi pulada)"
    }

    # ------------------------------------------------------------------
    # 2. As telas.
    # ------------------------------------------------------------------
    if ($ExeExiste) {
        Write-Host ""
        Write-Host "=== A tela ==="

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

        if ($null -ne $principal) {
            # ------------------------------------------------------------------
            # 2.1. A tela de opcoes: reconstruir o cache.
            # ------------------------------------------------------------------
            Write-Host ""
            Write-Host "=== O cache de programas ==="

            $itemMenu = AbrirComando $processo.Id $principal 'CmdOptions'
            Chk "o menu de comandos tem as opcoes" ($null -ne $itemMenu)

            if ($null -ne $itemMenu) {
                Clicar $itemMenu | Out-Null
                Start-Sleep -Seconds 3

                $botao = EmAlgumaJanela $processo.Id 'OptRebuildCache'
                Chk "a tela de opcoes tem o comando de reconstruir o cache" ($null -ne $botao)

                if ($null -ne $botao) {
                    ClicarPorId $principal 'OptRebuildCache' | Out-Null
                    Start-Sleep -Seconds 2

                    # A caixa de confirmacao: e uma janela do processo.
                    $achou = $false
                    foreach ($tipo in @('Window', 'Pane', 'Text')) {
                        foreach ($item in (DoTipo $principal $tipo)) {
                            if ((TextoDe $item) -match 'cache de programas foi apagado') { $achou = $true; break }
                        }
                        if ($achou) { break }
                    }

                    # A caixa de mensagem e uma janela propria do processo.
                    if (-not $achou) {
                        $raiz = [System.Windows.Automation.AutomationElement]::RootElement
                        $condPid = New-Object System.Windows.Automation.PropertyCondition(
                            [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $processo.Id)
                        foreach ($janela in $raiz.FindAll([System.Windows.Automation.TreeScope]::Children, $condPid)) {
                            if ((TextoDe $janela) -match 'cache de programas foi apagado') { $achou = $true; break }
                            foreach ($item in (DoTipo $janela 'Text')) {
                                if ((TextoDe $item) -match 'cache de programas foi apagado') { $achou = $true; break }
                            }
                            if ($achou) { break }
                        }
                    }

                    Chk "o comando avisa que o cache foi apagado" $achou

                    # Fecha a caixa de mensagem (o botao OK).
                    foreach ($tipo in @('Button')) {
                        foreach ($item in (DoTipo $principal $tipo)) {
                            if ((TextoDe $item) -match '^OK$') { Clicar $item | Out-Null; break }
                        }
                    }
                    Start-Sleep -Seconds 1
                }

                # Fecha a janela de opcoes.
                Clicar (EmAlgumaJanela $processo.Id 'OptCloseButton') | Out-Null
                Start-Sleep -Seconds 2
            }

            # ------------------------------------------------------------------
            # 2.2. A desinstalacao personalizada: os dois button de exportar.
            # ------------------------------------------------------------------
            Write-Host ""
            Write-Host "=== A desinstalacao personalizada ==="

            $itemMenu = AbrirComando $processo.Id $principal 'CmdManualRemoval'
            Chk "o menu de comandos tem a desinstalacao personalizada" ($null -ne $itemMenu)

            if ($null -ne $itemMenu) {
                Clicar $itemMenu | Out-Null
                Start-Sleep -Seconds 3

                Chk "a janela da desinstalacao personalizada abre" ($null -ne (EmAlgumaJanela $processo.Id 'ManualKeywordBox'))
                Chk "a janela tem o botao de exportar em CSV" ($null -ne (EmAlgumaJanela $processo.Id 'ManualExportButton'))
                Chk "a janela tem o botao de exportar em XML" ($null -ne (EmAlgumaJanela $processo.Id 'ManualExportXmlButton'))

                Clicar (EmAlgumaJanela $processo.Id 'ManualCloseButton') | Out-Null
                Start-Sleep -Seconds 2
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

    if ([System.IO.File]::Exists($arquivoXml)) { [System.IO.File]::Delete($arquivoXml) }

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
