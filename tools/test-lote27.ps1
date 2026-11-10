<#
  Teste de ponta a ponta do lote 27: a arvore de alteracoes detectadas e a
  busca dentro dela.

  O que ele prova:
    1. a linha de comando mostra a arvore em hierarquia (categorias, grupos
       de mudanca e itens);
    2. a busca filtra a arvore e diz quantas alteracoes sobraram;
    3. a janela de rastreio mostra a arvore depois de comparar dois
       snapshots;
    4. a busca da tela filtra a arvore tambem.

  Os snapshots usados aqui sao SINTETICOS (o script escreve o arquivo no
  formato do proprio Tranal): assim o teste nao depende de varrer o sistema
  inteiro, e nao depende do estado da maquina de quem roda.

  O script apaga no final tudo que criou: os snapshots.

  Uso:
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-lote27.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-lote27.ps1 -Exe publish\preview\TranalUninstaller.exe -CliExe publish\cli\tranal.exe
#>
param(
    [string]$Exe = '',
    [string]$CliExe = '',
    [string]$DataDirectory = '',
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

if ([string]::IsNullOrWhiteSpace($DataDirectory)) {
    $DataDirectory = Join-Path $env:LOCALAPPDATA 'Tranal Group\Tranal Uninstaller Pro'
}

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

function Escrever($elemento, [string]$texto) {
    if ($null -eq $elemento) { return $false }
    try {
        $p = $elemento.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
        $p.SetValue($texto)
        return $true
    } catch { return $false }
}

function TextoDe($elemento) {
    if ($null -eq $elemento) { return '' }
    try { return [string]$elemento.Current.Name } catch { }
    return ''
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
        $janelas = $raiz.FindAll([System.Windows.Automation.TreeScope]::Children, $condPid)
        foreach ($janela in $janelas) {
            $achado = PorId $janela $automationId
            if ($null -ne $achado) { return $janela }
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
        $janelas = $raiz.FindAll([System.Windows.Automation.TreeScope]::Children, $condPid)
        foreach ($janela in $janelas) {
            $achado = PorId $janela $automationId
            if ($null -ne $achado) { return $achado }
        }
    } catch { }
    return $null
}

function ItensDaLista($janela, [string]$automationId) {
    if ($null -eq $janela) { return @() }
    try {
        $lista = PorId $janela $automationId
        if ($null -eq $lista) { return @() }

        # Um ListView com GridView e exposto pelo WPF como tabela (DataGrid)
        # e os itens sao DataItem, nao ListItem. Os dois tipos sao procurados
        # porque o que importa aqui e achar os itens, nao o nome do controle.
        $achados = @()
        foreach ($tipo in @([System.Windows.Automation.ControlType]::DataItem,
                            [System.Windows.Automation.ControlType]::ListItem)) {
            $cond = New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::ControlTypeProperty, $tipo)
            $achados += $lista.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
        }
        return $achados
    } catch { return @() }
}

# O nome visivel de um item da tabela: o WPF deixa o nome do proprio item
# com o nome da classe, e o texto mora nos elementos filhos.
function TextoDoItem($item) {
    if ($null -eq $item) { return '' }
    try {
        $cond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::Text)
        $partes = @()
        foreach ($t in $item.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)) {
            $partes += [string]$t.Current.Name
        }
        if ($partes.Count -gt 0) { return ($partes -join ' ') }
        return [string]$item.Current.Name
    } catch { return '' }
}

# Abre todos os nos da arvore: com os nos colapsados, os itens ficam
# escondidos e a contagem mostraria menos do que a tela tem.
function ExpandeArvore($arvore) {
    if ($null -eq $arvore) { return 0 }
    try {
        $cond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::TreeItem)
        $passo = 0
        do {
            $mudou = $false
            foreach ($no in $arvore.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)) {
                try {
                    $p = $no.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
                    if ($p.Current.ExpandCollapseState -eq [System.Windows.Automation.ExpandCollapseState]::Collapsed) {
                        $p.Expand()
                        $mudou = $true
                    }
                } catch { }
            }
            $passo++
        } while ($mudou -and $passo -lt 8)
        return $passo
    } catch { return 0 }
}

# Todo o texto visivel na arvore (o do no e o dos itens dentro dele).
function TextoDaArvore($arvore) {
    if ($null -eq $arvore) { return '' }
    try {
        $partes = @()
        $condNo = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::TreeItem)
        foreach ($no in $arvore.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condNo)) {
            $partes += [string]$no.Current.Name
        }
        $condTexto = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::Text)
        foreach ($t in $arvore.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condTexto)) {
            $partes += [string]$t.Current.Name
        }
        return ($partes -join ' | ')
    } catch { return '' }
}

function NosDaArvore($arvore) {
    if ($null -eq $arvore) { return @() }
    try {
        $cond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::TreeItem)
        return $arvore.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
    } catch { return @() }
}

function Seleciona($elemento) {
    if ($null -eq $elemento) { return $false }
    try {
        $elemento.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
        return $true
    } catch { return $false }
}

# Marca dois itens ao mesmo tempo na tabela. O Select() do padrao de
# automacao substitui a selecao, e o AddToSelection nao e respeitado pelo
# WPF nesta tabela: o que funciona e o teclado (Ctrl+Espaco alterna o item
# que esta com o foco).
function SelecionaDois($lista, $primeiro, $segundo) {
    if ($null -eq $primeiro -or $null -eq $segundo) { return $false }
    try {
        $primeiro.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
        Start-Sleep -Milliseconds 300
        $segundo.SetFocus()
        Start-Sleep -Milliseconds 300
        [System.Windows.Forms.SendKeys]::SendWait('^ ')
        return $true
    } catch { return $false }
}

# Quantos itens da tabela estao marcados agora.
function Selecionados($lista) {
    if ($null -eq $lista) { return 0 }
    try {
        $selecao = $lista.GetCurrentPattern([System.Windows.Automation.SelectionPattern]::Pattern)
        return @($selecao.Current.GetSelection()).Count
    } catch { return 0 }
}

# ----------------------------------------------------------------------
# Os snapshots sinteticos, no formato que o proprio Tranal grava
# ----------------------------------------------------------------------

$snapshotAntes = 'teste-arvore-antes'
$snapshotDepois = 'teste-arvore-depois'

function Gravar-Snapshot([string]$nome, $files, $folders, $keys, $values) {
    $pastaSnapshots = Join-Path $DataDirectory 'snapshots'
    [System.IO.Directory]::CreateDirectory($pastaSnapshots) | Out-Null

    $id = [Guid]::NewGuid().ToString('D')
    $arquivo = Join-Path $pastaSnapshots ($id + '.json')

    $documento = [ordered]@{
        format    = 'Tranal.Snapshot.v1'
        metadata  = [ordered]@{
            id                 = $id
            name               = $nome
            label              = 'sintetico'
            createdUtc         = [DateTime]::UtcNow.ToString('o')
            fileCount          = @($files).Count
            folderCount        = @($folders).Count
            registryKeyCount   = @($keys).Count
            registryValueCount = @($values).Count
            path               = $arquivo
        }
        state     = [ordered]@{
            files          = @($files)
            folders        = @($folders)
            registryKeys   = @($keys)
            registryValues = @($values)
        }
    }

    $json = $documento | ConvertTo-Json -Depth 8
    [System.IO.File]::WriteAllText($arquivo, $json, (New-Object System.Text.UTF8Encoding($false)))
    return $id
}

Write-Host "=== Preparacao ==="
Write-Host "    executavel: $Exe (existe: $ExeExiste)"
Write-Host "    linha de comando: $CliExe (existe: $CliExiste)"

$antesId = Gravar-Snapshot $snapshotAntes @(
    @{ path = 'C:\Programa\app.exe'; sizeBytes = 1000; modifiedUtc = '2026-11-01T10:00:00Z'; isDirectory = $false }
    @{ path = 'C:\Programa\config.ini'; sizeBytes = 100; modifiedUtc = '2026-11-01T10:00:00Z'; isDirectory = $false }
) @(
    @{ path = 'C:\Programa\Dados'; sizeBytes = 0; modifiedUtc = '2026-11-01T10:00:00Z'; isDirectory = $true }
) @(
    'HKCU\Software\Programa'
) @(
    @{ key = 'HKCU\Software\Programa'; valueName = 'Versao'; value = '1.0'; valueKind = 'String' }
)

$depoisId = Gravar-Snapshot $snapshotDepois @(
    @{ path = 'C:\Programa\app.exe'; sizeBytes = 2000; modifiedUtc = '2026-11-09T10:00:00Z'; isDirectory = $false }
    @{ path = 'C:\Programa\novo.dll'; sizeBytes = 500; modifiedUtc = '2026-11-09T10:00:00Z'; isDirectory = $false }
) @(
    @{ path = 'C:\Programa\Dados'; sizeBytes = 0; modifiedUtc = '2026-11-01T10:00:00Z'; isDirectory = $true }
    @{ path = 'C:\Programa\Novo'; sizeBytes = 0; modifiedUtc = '2026-11-09T10:00:00Z'; isDirectory = $true }
) @(
    'HKCU\Software\Programa'
    'HKCU\Software\Programa\Extra'
) @(
    @{ key = 'HKCU\Software\Programa'; valueName = 'Versao'; value = '2.0'; valueKind = 'String' }
)

$processo = $null
try {
    Write-Host ""
    Write-Host "=== Execucao ==="

    # ------------------------------------------------------------------
    # 1. A arvore pela linha de comando.
    # ------------------------------------------------------------------
    if ($CliExiste) {
        $arvore = & $CliExe /comparar $snapshotAntes $snapshotDepois /arvore 2>&1 | Out-String
        Write-Host ($arvore.Trim())
        Write-Host ""

        Chk "a arvore mostra a categoria de arquivos" ($arvore -match 'Arquivos \(\d+\)')
        Chk "a arvore mostra a categoria de pastas" ($arvore -match 'Pastas \(\d+\)')
        Chk "a arvore mostra a categoria de registro" ($arvore -match 'Registro \(\d+\)')
        Chk "a arvore mostra a categoria de valores" ($arvore -match 'Valores de registro \(\d+\)')
        Chk "a arvore mostra o grupo de mudanca" ($arvore -match 'Novos \(\d+\)' -or $arvore -match 'Modificados \(\d+\)')
        Chk "a arvore mostra o item com a hierarquia" ($arvore -match '(?m)^\s+C:\\Programa\\novo\.dll')

        $busca = & $CliExe /comparar $snapshotAntes $snapshotDepois /busca 'novo.dll' 2>&1 | Out-String
        Write-Host ($busca.Trim())
        Write-Host ""

        Chk "a busca filtra a arvore" ($busca -match 'Alteracoes: 1 de \d+')
        Chk "a busca mostra o item procurado" ($busca.Contains('C:\Programa\novo.dll'))
        Chk "a busca esconde o que nao casa" (-not $busca.Contains('C:\Programa\config.ini'))
    }
    else {
        Write-Host "    (linha de comando nao publicada: a verificacao da CLI foi pulada)"
    }

    # ------------------------------------------------------------------
    # 2. A arvore na janela de rastreio.
    # ------------------------------------------------------------------
    if ($ExeExiste) {
        Add-Type -AssemblyName UIAutomationClient
        Add-Type -AssemblyName UIAutomationTypes
        Add-Type -AssemblyName System.Windows.Forms

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
            Clicar (PorId $principal 'CmdMainMenu') | Out-Null
            Start-Sleep -Seconds 1

            Clicar (ItemDeMenu $processo.Id 'CmdTracing') | Out-Null
            Start-Sleep -Seconds 2

            # A lista de snapshots so existe na aba 'Snapshots', que nao e a
            # aba padrao: a janela e procurada por um elemento de todas.
            $rastreio = JanelaComId $processo.Id 'TracingWatchFolder'
            if ($null -eq $rastreio) {
                # Diagnostico: quais janelas o aplicativo tem agora.
                try {
                    $raiz = [System.Windows.Automation.AutomationElement]::RootElement
                    $condPid = New-Object System.Windows.Automation.PropertyCondition(
                        [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $processo.Id)
                    $janelas = $raiz.FindAll([System.Windows.Automation.TreeScope]::Children, $condPid)
                    foreach ($j in $janelas) {
                        Write-Host ("    janela: " + (TextoDe $j))
                    }
                } catch { }
            }

            Chk "a janela de rastreio abre" ($null -ne $rastreio)

            if ($null -ne $rastreio) {
                # A aba dos snapshots.
                try {
                    $cond = New-Object System.Windows.Automation.PropertyCondition(
                        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
                        [System.Windows.Automation.ControlType]::TabItem)
                    $abas = $rastreio.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
                    foreach ($aba in $abas) {
                        if ((TextoDe $aba) -eq 'Snapshots') {
                            $aba.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
                            break
                        }
                    }
                } catch { }
                Start-Sleep -Seconds 1

                $itens = ItensDaLista $rastreio 'TracingSnapshots'
                Write-Host ("    snapshots na lista: {0}" -f $itens.Count)

                # A comparacao e "A x B": o antes vem primeiro na selecao.
                $itemAntes = $null
                $itemDepois = $null
                foreach ($item in $itens) {
                    $nome = TextoDoItem $item
                    Write-Host ("    item: " + $nome)
                    if ($null -eq $itemAntes -and $nome -like "*$snapshotAntes*") { $itemAntes = $item }
                    if ($null -eq $itemDepois -and $nome -like "*$snapshotDepois*") { $itemDepois = $item }
                }

                Chk "a lista mostra o snapshot de antes" ($null -ne $itemAntes)
                Chk "a lista mostra o snapshot de depois" ($null -ne $itemDepois)

                $tabela = PorId $rastreio 'TracingSnapshots'
                SelecionaDois $tabela $itemAntes $itemDepois | Out-Null
                Start-Sleep -Milliseconds 600
                Write-Host ("    selecionados: {0}" -f (Selecionados $tabela))
                Chk "os dois snapshots ficam marcados" ((Selecionados $tabela) -eq 2)

                Clicar (PorId $rastreio 'TracingCompareSnapshots') | Out-Null
                Start-Sleep -Seconds 3

                Write-Host ("    aviso da tela: " + (TextoDe (PorId $rastreio 'StatusText')))

                $arvoreTela = PorId $rastreio 'TracingChangeTree'
                Chk "a tela mostra a arvore de alteracoes" ($null -ne $arvoreTela)

                ExpandeArvore $arvoreTela | Out-Null
                Start-Sleep -Milliseconds 800

                $nos = NosDaArvore $arvoreTela
                Write-Host ("    nos na arvore: {0}" -f $nos.Count)
                Chk "a arvore da tela tem nos" ($nos.Count -gt 0)

                $textoTela = TextoDaArvore $arvoreTela
                Write-Host ("    texto da arvore: {0}" -f $textoTela)
                Chk "a arvore da tela mostra a categoria" ($textoTela -match 'Arquivos')
                Chk "a arvore da tela mostra o item novo" ($textoTela.Contains('novo.dll'))

                $contador = TextoDe (PorId $rastreio 'TracingChangeTreeCount')
                Write-Host ("    contador: {0}" -f $contador)
                Chk "a tela conta as alteracoes" ($contador -match '\d+') $contador

                # A busca da tela filtra a arvore.
                $antes = $nos.Count
                Escrever (PorId $rastreio 'TracingChangeTreeSearch') 'novo.dll' | Out-Null
                Start-Sleep -Seconds 2

                $arvoreDepois = PorId $rastreio 'TracingChangeTree'
                ExpandeArvore $arvoreDepois | Out-Null
                Start-Sleep -Milliseconds 800

                $depois = (NosDaArvore $arvoreDepois).Count
                Write-Host ("    nos antes da busca: {0}; depois: {1}" -f $antes, $depois)
                Chk "a busca da tela filtra a arvore" ($depois -lt $antes -and $depois -gt 0)

                $textoDepois = TextoDaArvore $arvoreDepois
                Chk "a busca da tela mostra o item procurado" ($textoDepois.Contains('novo.dll'))
                Chk "a busca da tela esconde o que nao casa" (-not $textoDepois.Contains('config.ini'))

                $contadorDepois = TextoDe (PorId $rastreio 'TracingChangeTreeCount')
                Write-Host ("    contador depois da busca: {0}" -f $contadorDepois)
                Chk "a tela conta o que sobrou" ($contadorDepois -match '1 de \d+') $contadorDepois
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

    if ($CliExiste) {
        & $CliExe /snapshots /delete $snapshotAntes *> $null
        & $CliExe /snapshots /delete $snapshotDepois *> $null
    }

    foreach ($id in @($antesId, $depoisId)) {
        $arquivo = Join-Path (Join-Path $DataDirectory 'snapshots') ($id + '.json')
        if ([System.IO.File]::Exists($arquivo)) { [System.IO.File]::Delete($arquivo) }
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
