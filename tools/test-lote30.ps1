<#
  Teste de ponta a ponta do lote 30: a assinatura do desinstalador e o
  comando do usuario.

  O que ele prova:
    1. a linha de comando mostra a assinatura digital de um arquivo assinado
       do proprio Windows (o kernel32.dll);
    2. a linha de comando recusa o comando sem o arquivo e com um arquivo
       que nao existe;
    3. a linha de comando varre a assinatura dos desinstaladores instalados;
    4. a tela de assinatura do desinstalador abre e diz o estado;
    5. a tela de comandos tem o botao que executa o comando do usuario.

  Nenhum software e instalado ou removido: o teste so LE a assinatura de
  arquivos do proprio Windows.

  Uso:
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-lote30.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-lote30.ps1 -Exe publish\preview\TranalUninstaller.exe -CliExe publish\cli\tranal.exe
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

# Uma janela pelo identificador de automacao, dentro do processo.
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

# O item de menu vive em uma janela propria (o popup).
function ItemDeMenu([int]$processId, [string]$automationId) {
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

# O texto visivel de um item: o WPF deixa o nome do proprio item com o nome
# da classe, e o texto mora nos elementos filhos.
function TextoDoItem($item) {
    if ($null -eq $item) { return '' }
    try {
        $partes = @()
        foreach ($t in DoTipo $item 'Text') { $partes += [string]$t.Current.Name }
        if ($partes.Count -gt 0) { return ($partes -join ' ') }
        return [string]$item.Current.Name
    } catch { return '' }
}

# Os itens de um desplegavel: eles vivem em uma janela propria (o popup), e
# so existem depois que o desplegavel abre.
function ItensDoPopup([int]$processId) {
    $nomes = @()
    try {
        $raiz = [System.Windows.Automation.AutomationElement]::RootElement
        $condPid = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $processId)
        foreach ($janela in $raiz.FindAll([System.Windows.Automation.TreeScope]::Children, $condPid)) {
            foreach ($item in (DoTipoOu $janela @('ListItem', 'DataItem'))) {
                $texto = TextoDoItem $item
                if ($texto.Length -gt 0 -and $nomes -notcontains $texto) { $nomes += $texto }
            }
        }
    } catch { }
    return $nomes
}

# Abre um ComboBox: ele nao tem InvokePattern no WPF, so ExpandCollapse.
function Expandir($elemento) {
    if ($null -eq $elemento) { return $false }
    try {
        $padrao = $elemento.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
        $padrao.Expand()
        return $true
    } catch { return $false }
}

# Roda a linha de comando e devolve a saida (stdout + stderr) e o codigo de
# saida.
#
# A saida vai para arquivo: o PowerShell transforma o stderr de um comando
# nativo em registro de erro, e com $ErrorActionPreference='Stop' isso
# engolia o codigo de saida — justamente o que o teste precisa ver.
function RodarCli([string]$cli, [string[]]$argumentos) {
    $id = [Guid]::NewGuid().ToString('N')
    $arquivoSaida = Join-Path $env:TEMP ("lote30-out-$id.txt")
    $arquivoErro = Join-Path $env:TEMP ("lote30-err-$id.txt")
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
$pacote = Join-Path $env:TEMP ('lote29-pacote-' + [Guid]::NewGuid().ToString('N') + '.zip')
$lista = Join-Path $env:TEMP ('lote29-lista-' + [Guid]::NewGuid().ToString('N') + '.csv')

try {
    Write-Host "=== Preparacao ==="
    Write-Host "    executavel: $Exe (existe: $ExeExiste)"
    Write-Host "    linha de comando: $CliExe (existe: $CliExiste)"

    # ------------------------------------------------------------------
    # 1. A linha de comando.
    # ------------------------------------------------------------------
    if ($CliExiste) {
        Write-Host ""
        Write-Host "=== Execucao ==="

        # A assinatura de um arquivo assinado do proprio Windows: o
        # kernel32.dll e assinado pela Microsoft e a cadeia fecha na raiz
        # confiavel. E o caso que prova que a verificacao funciona.
        $kernel = Join-Path $env:SystemRoot 'System32\kernel32.dll'
        $resultado = RodarCli $CliExe @('/signature', $kernel)
        $saida = $resultado.Saida
        $codigo = $resultado.Codigo
        Write-Host ("    assinatura: " + (($saida -replace '\s+', ' ').Trim()))
        Chk "a linha de comando mostra a assinatura de um arquivo assinado" (
            $saida -match 'Assinado: sim' -and $saida -match 'Valid' -and $codigo -eq 0)

        # Sem o arquivo, o comando recusa (e nao devolve 0).
        $resultado = RodarCli $CliExe @('/signature')
        $saida = $resultado.Saida
        $codigo = $resultado.Codigo
        Chk "a linha de comando recusa a assinatura sem o arquivo" ($saida -match '/signature' -or $codigo -ne 0)

        # Com um arquivo que nao existe, o comando recusa tambem.
        $resultado = RodarCli $CliExe @('/signature', 'C:\lote30-arquivo-que-nao-existe.exe')
        $saida = $resultado.Saida
        $codigo = $resultado.Codigo
        Chk "a linha de comando recusa a assinatura de arquivo inexistente" (
            $codigo -ne 0 -and $saida -match 'nao encontrado')

        # A varredura de todos os desinstaladores instalados: a saida traz a
        # contagem e o tempo que a verificacao levou.
        $resultado = RodarCli $CliExe @('/signatures')
        $saida = $resultado.Saida
        $codigo = $resultado.Codigo
        Write-Host ("    varredura: " + (($saida -split "`n" | Select-Object -Last 3) -join ' | '))
        Chk "a linha de comando varre a assinatura dos desinstaladores" (
            $saida -match 'Desinstaladores verificados:' -and $codigo -eq 0)
    }
    else {
        Write-Host "    (linha de comando nao publicada: a verificacao da CLI foi pulada)"
    }

    # ------------------------------------------------------------------
    # 2. A tela: a assinatura do desinstalador e o comando do usuario.
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
            # A visao de todos os programas, e a lista carregada.
            Clicar (PorId $principal 'ModuleAllPrograms') | Out-Null
            Start-Sleep -Seconds 2

            $lista = PorId $principal 'ProgramsList'
            Chk "a lista de programas existe" ($null -ne $lista)

            if ($null -ne $lista) {
                # A lista so materializa os itens visiveis: espera ela encher.
                $limiteLista = (Get-Date).AddSeconds($EsperaSegundos)
                $itens = @()
                while ((Get-Date) -lt $limiteLista) {
                    Start-Sleep -Milliseconds 900
                    if ($null -eq $lista) { $lista = PorId $principal 'ProgramsList' }
                    if ($null -eq $lista) { continue }

                    $itens = DoTipoOu $lista @('ListItem', 'DataItem')
                    if ($itens.Count -gt 0) { break }
                }

                Chk "a lista carregou os programas" ($itens.Count -gt 0)

                if ($itens.Count -gt 0) {
                    # Seleciona o primeiro programa: as acoes por programa so
                    # aparecem quando a selecao tem uma linha so.
                    $selecionado = $itens[0]
                    try {
                        $padrao = $selecionado.GetCurrentPattern(
                            [System.Windows.Automation.SelectionItemPattern]::Pattern)
                        $padrao.Select()
                    } catch { }
                    Start-Sleep -Seconds 1

                    # O menu "Mais comandos" carrega as acoes do programa.
                    Clicar (PorId $principal 'CmdMoreCommands') | Out-Null
                    Start-Sleep -Seconds 1

                    $itemAssinatura = ItemDeMenu $processo.Id 'CmdUninstallerSignature'
                    $itemComandos = ItemDeMenu $processo.Id 'CmdEditCommands'

                    Chk "o menu tem a assinatura do desinstalador" ($null -ne $itemAssinatura)
                    Chk "o menu tem os comandos de desinstalacao" ($null -ne $itemComandos)

                    if ($null -ne $itemAssinatura) {
                        Clicar $itemAssinatura | Out-Null
                        Start-Sleep -Seconds 3

                        $detalhe = PorId $principal 'SignatureDetails'
                        Chk "a tela de assinatura do desinstalador abre" ($null -ne $detalhe)

                        if ($null -ne $detalhe) {
                            $texto = [string]$detalhe.GetCurrentPattern(
                                [System.Windows.Automation.ValuePattern]::Pattern).Current.Value
                            Write-Host ("    assinatura na tela: " + (($texto -replace '\s+', ' ').Trim()))
                            Chk "a tela diz o estado da assinatura" ($texto -match 'Estado:')

                            # A tela fala a lingua do catalogo: nenhuma chave
                            # pode aparecer crua no texto.
                            Chk "a tela traduz o texto da assinatura" ($texto -notmatch 'Signature\.')
                        }
                    }

                    if ($null -ne $itemComandos) {
                        Clicar $itemComandos | Out-Null
                        Start-Sleep -Seconds 2

                        $botao = PorId $principal 'CommandsRun'
                        Chk "a tela de comandos tem o botao de executar" ($null -ne $botao)
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

    if ([System.IO.File]::Exists($pacote)) { [System.IO.File]::Delete($pacote) }
    if ([System.IO.File]::Exists($lista)) { [System.IO.File]::Delete($lista) }

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
