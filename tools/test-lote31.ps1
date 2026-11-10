<#
  Teste de ponta a ponta do lote 31: o gerenciador do log de rastreio.

  O que ele prova:
    1. a linha de comando acrescenta um item ao log de instalacao;
    2. a linha de comando exclui um item do log;
    3. a linha de comando recusa o uso errado (sem caminho, sem log);
    4. a tela do gerenciador abre a partir do modulo da base de logs;
    5. a tela acrescenta o item ao log e a lista mostra o item novo.

  Nenhum software e instalado ou removido: o teste cria UM log sintetico na
  base de logs, mexe so nele e o apaga no final (com o arquivo .bak que a
  gravacao atomica deixa ao lado).

  Uso:
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-lote31.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-lote31.ps1 -Exe publish\preview\TranalUninstaller.exe -CliExe publish\cli\tranal.exe
#>
param(
    [string]$Exe = '',
    [string]$CliExe = '',
    [int]$EsperaSegundos = 45
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

# Clica com o mouse no centro do elemento.
#
# A barra de comandos do painel e um ItemsControl: o botao de dentro nao
# aparece na arvore de automacao, e o item que aparece nao tem
# InvokePattern. O clique no ponto do elemento e o que aciona o botao.
function ClicarMouse($elemento) {
    if ($null -eq $elemento) { return $false }

    try {
        if (-not ('TranalMouse' -as [type])) {
            Add-Type -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
[DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
'@ -Name Mouse -Namespace TranalMouse
        }

        $retangulo = $elemento.Current.BoundingRectangle
        if ($retangulo.Width -le 0 -or $retangulo.Height -le 0) { return $false }

        $x = [int]($retangulo.X + ($retangulo.Width / 2))
        $y = [int]($retangulo.Y + ($retangulo.Height / 2))

        [TranalMouse.Mouse]::SetCursorPos($x, $y) | Out-Null
        Start-Sleep -Milliseconds 250

        [TranalMouse.Mouse]::mouse_event(0x0002, 0, 0, 0, 0)
        Start-Sleep -Milliseconds 80
        [TranalMouse.Mouse]::mouse_event(0x0004, 0, 0, 0, 0)
        return $true
    } catch { return $false }
}

# Clica no botao de um identificador de automacao.
function ClicarPorId($janela, [string]$automationId) {
    $achado = PorId $janela $automationId
    if ($null -eq $achado) { return $false }

    try {
        $achado.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        return $true
    } catch { }

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

# Roda a linha de comando e devolve a saida (stdout + stderr) e o codigo de
# saida. A saida vai para arquivo: o PowerShell transforma o stderr de um
# comando nativo em registro de erro, e com $ErrorActionPreference='Stop' isso
# engolia o codigo de saida — justamente o que o teste precisa ver.
function RodarCli([string]$cli, [string[]]$argumentos) {
    $id = [Guid]::NewGuid().ToString('N')
    $arquivoSaida = Join-Path $env:TEMP ("lote31-out-$id.txt")
    $arquivoErro = Join-Path $env:TEMP ("lote31-err-$id.txt")
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
$logId = [Guid]::NewGuid().ToString('D')
$logNome = 'Lote 31 (e2e)'
$pastaLogs = Join-Path $env:LOCALAPPDATA 'Tranal Group\Tranal Uninstaller Pro\logs'
$arquivoLog = Join-Path $pastaLogs ($logId + '.json')
$itemNovo = 'C:\Program Files\Lote 31 (e2e)\novo.txt'

try {
    Write-Host "=== Preparacao ==="
    Write-Host "    executavel: $Exe (existe: $ExeExiste)"
    Write-Host "    linha de comando: $CliExe (existe: $CliExiste)"
    Write-Host "    log de teste: $arquivoLog"

    # O log sintetico: o mesmo formato que o TraceStore grava. O teste cria
    # este arquivo e o apaga no final.
    [System.IO.Directory]::CreateDirectory($pastaLogs) | Out-Null

    $conteudo = @'
{
  "format": "Tranal.TraceLog.v1",
  "id": "ID_LOG",
  "programName": "Lote 31 (e2e)",
  "timestamp": "2026-11-10T10:00:00.0000000Z",
  "installerPath": "C:\\Temp\\setup.exe",
  "imported": false,
  "counts": { "files": 1, "folders": 0, "registryKeys": 0 },
  "payload": {
    "files": [ { "path": "C:\\Program Files\\Lote 31 (e2e)\\app.exe", "sizeBytes": 10, "modifiedUtc": "2026-11-10T10:00:00.0000000Z" } ],
    "folders": [],
    "registryKeys": []
  }
}
'@
    $conteudo = $conteudo.Replace('ID_LOG', $logId)
    [System.IO.File]::WriteAllText($arquivoLog, $conteudo, (New-Object System.Text.UTF8Encoding($false)))

    # ------------------------------------------------------------------
    # 1. A linha de comando.
    # ------------------------------------------------------------------
    if ($CliExiste) {
        Write-Host ""
        Write-Host "=== Execucao ==="

        # Acrescentar um item ao log.
        $resultado = RodarCli $CliExe @('/log-add', $logId, '/caminho', $itemNovo, '/tipo', 'arquivo')
        Write-Host ("    add: " + (($resultado.Saida -replace '\s+', ' ').Trim()))
        Chk "a linha de comando acrescenta um item ao log" ($resultado.Codigo -eq 0)

        # O item aparece no log.
        $resultado = RodarCli $CliExe @('/log-entries', $logId)
        Chk "o item acrescentado aparece no log" ($resultado.Saida -match 'novo\.txt')

        # Excluir o item do log.
        $resultado = RodarCli $CliExe @('/log-drop', $logId, '/caminho', $itemNovo)
        Chk "a linha de comando exclui um item do log" ($resultado.Codigo -eq 0)

        $resultado = RodarCli $CliExe @('/log-entries', $logId)
        Chk "o item excluido sumiu do log" ($resultado.Saida -notmatch 'novo\.txt')

        # Sem o caminho: o comando recusa (e nao devolve 0).
        $resultado = RodarCli $CliExe @('/log-add', $logId)
        Chk "a linha de comando recusa o add sem o caminho" ($resultado.Codigo -ne 0)

        # Sem o log: o comando recusa tambem.
        $resultado = RodarCli $CliExe @('/log-add', 'log-que-nao-existe', '/caminho', $itemNovo)
        Chk "a linha de comando recusa o add sem o log" ($resultado.Codigo -ne 0)
    }
    else {
        Write-Host "    (linha de comando nao publicada: a verificacao da CLI foi pulada)"
    }

    # ------------------------------------------------------------------
    # 2. A tela do gerenciador.
    # ------------------------------------------------------------------
    if ($ExeExiste) {
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
            # O modulo da base de logs.
            Clicar (PorId $principal 'ModuleLogsDatabase') | Out-Null
            Start-Sleep -Seconds 2

            $lista = PorId $principal 'PanelItems'
            Chk "a base de logs abre" ($null -ne $lista)

            $botaoGerenciador = PorId $principal 'LogsManager'
            Chk "o modulo tem o comando do gerenciador" ($null -ne $botaoGerenciador)

            if ($null -ne $lista) {
                # O log de teste: a lista so materializa os itens visiveis.
                $limiteLista = (Get-Date).AddSeconds($EsperaSegundos)
                $escolhido = $null
                while ((Get-Date) -lt $limiteLista -and -not $escolhido) {
                    Start-Sleep -Milliseconds 900
                    foreach ($item in (DoTipoOu $lista @('ListItem', 'DataItem'))) {
                        if ((TextoDoItem $item) -match 'Lote 31') { $escolhido = $item; break }
                    }
                }

                Chk "a base de logs mostra o log de teste" ($null -ne $escolhido)

                if ($null -ne $escolhido) {
                    try {
                        $escolhido.GetCurrentPattern(
                            [System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
                    } catch { }
                    Start-Sleep -Milliseconds 700

                    ClicarPorId $principal 'LogsManager' | Out-Null
                    Start-Sleep -Seconds 2

                    $itens = PorId $principal 'TraceManagerItems'
                    Chk "a tela do gerenciador abre" ($null -ne $itens)

                    if ($null -ne $itens) {
                        $campo = PorId $principal 'TraceManagerPath'
                        Chk "a tela tem o campo do caminho" ($null -ne $campo)

                        Preencher $campo $itemNovo | Out-Null
                        Clicar (PorId $principal 'TraceManagerAdd') | Out-Null
                        Start-Sleep -Seconds 2

                        $achou = $false
                        foreach ($item in (DoTipoOu $itens @('ListItem', 'DataItem'))) {
                            if ((TextoDoItem $item) -match 'novo\.txt') { $achou = $true; break }
                        }
                        Chk "a tela acrescenta o item ao log" $achou
                    }
                }
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

    # O log de teste e apagado: e o arquivo que o teste criou.
    foreach ($arquivo in @($arquivoLog, ($arquivoLog + '.bak'), ($arquivoLog + '.tmp'))) {
        if ([System.IO.File]::Exists($arquivo)) { [System.IO.File]::Delete($arquivo) }
    }
    Write-Host ("    log de teste removido: " + (-not [System.IO.File]::Exists($arquivoLog)))

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
