<#
  Teste de ponta a ponta do lote 29: o que ja existia e nao tinha dono.

  O que ele prova:
    1. a linha de comando lista as atualizacoes do Windows instaladas;
    2. a linha de comando monta o pacote portatil e traz ele de volta;
    3. a linha de comando traz uma lista exportada e diz o que casou e o que
       nao casou;
    4. a linha de comando mostra o centro de mudancas (o que pode ser
       desfeito);
    5. a tela de Descoberta tem a categoria dos atalhos do Menu Iniciar e da
       Area de Trabalho, e o botao que remove a atualizacao do Windows.

  Nenhum software e instalado ou removido: o teste so LE a lista das
  atualizacoes e grava um pacote sintetico na pasta temporaria, que e apagada
  no final. A remocao de uma atualizacao NAO e exercitada aqui (ela roda o
  wusa.exe do Windows): o que e medido e o que a tela e a linha de comando
  oferecem.

  Uso:
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-lote29.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-lote29.ps1 -Exe publish\preview\TranalUninstaller.exe -CliExe publish\cli\tranal.exe
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

        # As atualizacoes do Windows: o catalogo existe e e lido.
        $saida = (& $CliExe /windows-updates 2>&1 | Out-String)
        $codigo = $LASTEXITCODE
        Write-Host ("    atualizacoes do Windows: " + (($saida -replace '\s+', ' ').Trim()))
        Chk "a linha de comando lista as atualizacoes do Windows" ($saida -match 'Atualizacoes do Windows instaladas' -and $codigo -eq 0)

        # O pacote portatil: monta e traz de volta.
        $saida = (& $CliExe /portable-export $pacote 2>&1 | Out-String)
        $codigo = $LASTEXITCODE
        Write-Host ("    pacote: " + (($saida -replace '\s+', ' ').Trim()))
        Chk "a linha de comando monta o pacote portatil" ((Test-Path $pacote) -and $saida -match 'Pacote portatil gravado' -and $codigo -eq 0)

        $saida = (& $CliExe /portable-import $pacote 2>&1 | Out-String)
        $codigo = $LASTEXITCODE
        Write-Host ("    importacao: " + (($saida -replace '\s+', ' ').Trim()))
        Chk "a linha de comando importa o pacote portatil" ($saida -match 'Logs importados' -and $codigo -eq 0)

        # A lista exportada: o que casou e o que nao casou.
        [System.IO.File]::WriteAllText($lista, "Program;RegistryKey`r`nPrograma Ausente Do Lote 29;`r`n")
        $saida = (& $CliExe /portable-import-list $lista 2>&1 | Out-String)
        $codigo = $LASTEXITCODE
        Write-Host ("    lista importada: " + (($saida -replace '\s+', ' ').Trim()))
        Chk "a linha de comando traz a lista exportada" ($saida -match 'Entradas da lista' -and $saida -match 'NAO CASOU' -and $codigo -eq 0)

        # O centro de mudancas: o que pode ser desfeito.
        $saida = (& $CliExe /undo-center 2>&1 | Out-String)
        $codigo = $LASTEXITCODE
        Write-Host ("    centro de mudancas: " + (($saida -replace '\s+', ' ').Trim()))
        Chk "a linha de comando mostra o centro de mudancas" ($saida -match 'Mudancas:' -and $codigo -eq 0)

        # Sem o arquivo, o comando recusa (e nao cria nada).
        $saida = ''
        $codigo = 0
        try {
            $saida = & $CliExe /portable-export 2>&1 | Out-String
            $codigo = $LASTEXITCODE
        } catch {
            $saida = [string]$_.Exception.Message
        }
        Chk "a linha de comando recusa o pacote sem arquivo" ($saida -match 'portable-export' -or $codigo -ne 0)
    }
    else {
        Write-Host "    (linha de comando nao publicada: a verificacao da CLI foi pulada)"
    }

    # ------------------------------------------------------------------
    # 2. A tela: a janela de Descoberta.
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
            # A descoberta ampliada esta no popup do botao "Mais comandos",
            # e nao no menu principal.
            Clicar (PorId $principal 'CmdMoreCommands') | Out-Null
            Start-Sleep -Seconds 1

            $itemDescoberta = ItemDeMenu $processo.Id 'CmdDiscovery'
            Chk "o menu tem a escolha da descoberta ampliada" ($null -ne $itemDescoberta)

            if ($null -ne $itemDescoberta) {
                Clicar $itemDescoberta | Out-Null
                Start-Sleep -Seconds 2

                # A janela de Descoberta e filha da principal: a lista de
                # categorias e procurada dentro dela.
                $categorias = PorId $principal 'DiscoveryCategories'
                Chk "a janela de descoberta abre" ($null -ne $categorias)

                if ($null -ne $categorias) {
                    # O ComboBox do WPF nao tem InvokePattern: ele abre pelo
                    # padrao ExpandCollapse. E o popup do menu "Mais comandos"
                    # continua na arvore depois de fechado, entao o que conta
                    # e o que APARECEU depois de abrir o desplegavel.
                    $antes = ItensDoPopup $processo.Id
                    Expandir $categorias | Out-Null
                    Start-Sleep -Seconds 1

                    $depois = ItensDoPopup $processo.Id
                    $novos = @($depois | Where-Object { $antes -notcontains $_ })

                    Write-Host ("    categorias: " + ($depois -join ' | '))
                    Write-Host ("    novas: " + ($novos -join ' | '))
                    Chk "a tela de descoberta tem a categoria dos atalhos" (($novos -join ' | ') -match 'Atalhos')
                }

                $botao = PorId $principal 'DiscoveryRemoveUpdate'
                Chk "a tela de descoberta tem o botao de remover a atualizacao" ($null -ne $botao)
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
