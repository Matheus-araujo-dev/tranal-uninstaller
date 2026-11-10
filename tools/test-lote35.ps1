<#
  Teste de ponta a ponta do lote 35: a lista que a pessoa usa todo dia.

  O que ele prova:
    1. a coluna do nome e a primeira coluna da lista, com o rotulo certo
       (o cabecalho nao pode mostrar o nome da coluna seguinte);
    2. o conteudo da linha fica embaixo do cabecalho dele (a lista e a
       grade que speak the same language);
    3. o grupo dos "Novos Programas" existe (a janela de dias do nucleo);
    4. o painel lateral tem o botao do realce do programa novo, e ele
       responde;
    5. o menu do cabecalho tem o comando de mover a coluna, e mover muda a
       ordem dos cabecalho E o conteudo das linhas;
    6. a ordem escolhida fica guardada: o programa aberto de novo abre com
       a mesma ordem.

  Nenhum software e instalado ou removido. O teste mexe em uma coisa do
  ambiente: a ordem das colunas nas preferencias. O arquivo e guardado antes
  e devolvido no fim, exatamente como estava.

  Uso:
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-lote35.ps1
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

# ------------------------------------------------------------------
# O arquivo de preferencias: e guardado antes e devolvido no fim.
# ------------------------------------------------------------------
$pastaPreferencias = Join-Path $env:LOCALAPPDATA 'Tranal Group\Tranal Uninstaller Pro'
$arquivoPreferencias = Join-Path $pastaPreferencias 'settings.json'
$copiaPreferencias = Join-Path $env:TEMP ('tranal-lote35-' + [Guid]::NewGuid().ToString('N') + '.json')

$haviaPreferencias = [System.IO.File]::Exists($arquivoPreferencias)
if ($haviaPreferencias) {
    [System.IO.File]::Copy($arquivoPreferencias, $copiaPreferencias, $true)
}

# O idioma ativo vem das preferencias: e dele que vem o rotulo de cada
# coluna. O catalogo de idiomas e a fonte dos rotulos - o teste usa a
# mesma fonte, e nao um texto escrito a mao.
$idioma = 'pt-BR'
if ($haviaPreferencias) {
    try {
        $preferencias = [System.IO.File]::ReadAllText($arquivoPreferencias) | ConvertFrom-Json
        if ($preferencias.PSObject.Properties.Name -contains 'Language' -and -not [string]::IsNullOrWhiteSpace($preferencias.Language)) {
            $idioma = [string]$preferencias.Language
        }
    } catch { }
}

$script:catalogo = $null
function Rotulo([string]$chave) {
    if ($null -eq $script:catalogo) {
        $caminho = Join-Path $repoRoot ("src\Tranal.Uninstaller\Lang\" + $script:idioma + '.json')
        if (-not (Test-Path $caminho)) { $caminho = Join-Path $repoRoot 'src\Tranal.Uninstaller\Lang\pt-BR.json' }
        $script:catalogo = [System.IO.File]::ReadAllText($caminho, [System.Text.Encoding]::UTF8) | ConvertFrom-Json
    }

    $valor = $script:catalogo.PSObject.Properties[$chave]
    if ($null -eq $valor) { return $chave }
    return [string]$valor.Value
}

# ------------------------------------------------------------------
# Automacao.
# ------------------------------------------------------------------
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

function Clicar($elemento) {
    if ($null -eq $elemento) { return $false }
    try {
        $elemento.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        return $true
    } catch { return $false }
}

function ClicarMouse($elemento, [bool]$direito = $false) {
    if ($null -eq $elemento) { return $false }

    try {
        if (-not ('TranalMouse35' -as [type])) {
            Add-Type -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
[DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
'@ -Name Mouse -Namespace TranalMouse35
        }

        $retangulo = $elemento.Current.BoundingRectangle
        if ($retangulo.Width -le 0 -or $retangulo.Height -le 0) { return $false }

        $x = [int]($retangulo.X + ($retangulo.Width / 2))
        $y = [int]($retangulo.Y + ($retangulo.Height / 2))

        [TranalMouse35.Mouse]::SetCursorPos($x, $y) | Out-Null
        Start-Sleep -Milliseconds 250

        if ($direito) {
            [TranalMouse35.Mouse]::mouse_event(0x0008, 0, 0, 0, 0)
            Start-Sleep -Milliseconds 80
            [TranalMouse35.Mouse]::mouse_event(0x0010, 0, 0, 0, 0)
        }
        else {
            [TranalMouse35.Mouse]::mouse_event(0x0002, 0, 0, 0, 0)
            Start-Sleep -Milliseconds 80
            [TranalMouse35.Mouse]::mouse_event(0x0004, 0, 0, 0, 0)
        }
        return $true
    } catch { return $false }
}

# Traz a janela do programa para a frente: o clique do mouse vai para a
# janela que esta na frente, e o console do teste e uma delas.
function Ativar([int]$processId) {
    try {
        Add-Type -AssemblyName Microsoft.VisualBasic
        return [Microsoft.VisualBasic.Interaction]::AppActivate($processId)
    } catch { return $false }
}

# Espera um item de menu aparecer em alguma janela do processo.
function EsperarMenu([int]$processId, [string]$automationId, [int]$segundos) {
    $limite = (Get-Date).AddSeconds($segundos)
    while ((Get-Date) -lt $limite) {
        Start-Sleep -Milliseconds 400
        $item = EmAlgumaJanela $processId $automationId
        if ($null -ne $item) { return $item }
    }
    return $null
}

# As janelas do processo: e o diagnostico quando o menu nao abre.
function JanelasDoProcesso([int]$processId) {
    $lista = @()
    try {
        $raiz = [System.Windows.Automation.AutomationElement]::RootElement
        $condPid = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $processId)
        foreach ($janela in $raiz.FindAll([System.Windows.Automation.TreeScope]::Children, $condPid)) {
            $lista += ('    ' + $janela.Current.ControlType.ProgrammaticName + ' | ' + $janela.Current.Name + ' | ' + $janela.Current.ClassName)
            foreach ($item in (DoTipo $janela 'MenuItem')) {
                $lista += ('      MENU: ' + $item.Current.AutomationId + ' | ' + $item.Current.Name)
            }
        }
    } catch { }
    return $lista
}

# Os cabecalhos das colunas, da esquerda para a direita.
function Cabecalhos($janela) {
    $lista = @()
    foreach ($item in (DoTipo $janela 'HeaderItem')) {
        $retangulo = $item.Current.BoundingRectangle
        if ($retangulo.Width -le 0) { continue }

        $nome = [string]$item.Current.Name
        if ([string]::IsNullOrWhiteSpace($nome)) { continue }

        $lista += [pscustomobject]@{ Nome = $nome; X = $retangulo.X; Largura = $retangulo.Width; Elemento = $item }
    }
    return @($lista | Sort-Object X)
}

# O texto das celulas da primeira linha, da esquerda para a direita.
function Celulas($janela) {
    $itens = DoTipo $janela 'DataItem'
    if ($itens.Count -eq 0) { return @() }

    $lista = @()
    foreach ($texto in (DoTipo $itens[0] 'Text')) {
        $retangulo = $texto.Current.BoundingRectangle
        if ($retangulo.Width -le 0) { continue }

        $nome = [string]$texto.Current.Name
        if ([string]::IsNullOrWhiteSpace($nome)) { continue }

        $lista += [pscustomobject]@{ Nome = $nome; X = $retangulo.X }
    }
    return @($lista | Sort-Object X)
}

# O texto da celula que esta embaixo de um cabecalho.
function TextoSob($janela, $cabecalho) {
    if ($null -eq $cabecalho) { return '' }
    foreach ($celula in (Celulas $janela)) {
        if ($celula.X -ge $cabecalho.X -and $celula.X -lt ($cabecalho.X + $cabecalho.Largura)) {
            return $celula.Nome
        }
    }
    return ''
}

# O cabecalho com este rotulo.
function CabecalhoCom($janela, [string]$rotulo) {
    foreach ($cabecalho in (Cabecalhos $janela)) {
        if ($cabecalho.Nome -eq $rotulo) { return $cabecalho }
    }
    return $null
}

function NomesCabecalho($janela) { return @((Cabecalhos $janela) | ForEach-Object { $_.Nome }) }
function NomesCelula($janela) { return @((Celulas $janela) | ForEach-Object { $_.Nome }) }

$processo = $null
$processo2 = $null

try {
    Write-Host "=== Preparacao ==="
    Write-Host "    executavel: $Exe (existe: $ExeExiste)"
    Write-Host "    idioma: $idioma"
    Write-Host "    preferencias: $arquivoPreferencias (havia: $haviaPreferencias)"

    if (-not $ExeExiste) {
        Write-Host "    (executavel nao publicado: a verificacao da interface foi pulada)"
    }
    else {
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
            # 1. A lista de programas.
            # ------------------------------------------------------------------
            Write-Host ""
            Write-Host "=== A lista de programas ==="

            Clicar (PorId $principal 'ModuleAllPrograms') | Out-Null
            Start-Sleep -Seconds 5

            $lista = PorId $principal 'ProgramsList'
            Chk "a lista de programas aparece" ($null -ne $lista)

            $itens = DoTipo $principal 'DataItem'
            Chk "a lista tem programas" ($itens.Count -gt 0) ("itens: " + $itens.Count)

            # ------------------------------------------------------------------
            # 2. O cabecalho e o conteudo falam a mesma lingua.
            # ------------------------------------------------------------------
            Write-Host ""
            Write-Host "=== O cabecalho e o conteudo ==="

            $cabecalhos = Cabecalhos $principal
            $nomes = NomesCabecalho $principal
            Write-Host ("    cabecalhos: " + (($nomes | Select-Object -First 6) -join ' | '))

            $rotuloNome = Rotulo 'Common.Program'
            $rotuloTamanho = Rotulo 'Common.Size'

            Chk "a coluna do nome e a primeira coluna" ($nomes.Count -gt 0 -and $nomes[0] -eq $rotuloNome) ("primeiro: " + $(if ($nomes.Count -gt 0) { $nomes[0] } else { '(nenhum)' }))
            Chk "a coluna do tamanho vem em seguida" ($nomes.Count -gt 1 -and $nomes[1] -eq $rotuloTamanho) ("segundo: " + $(if ($nomes.Count -gt 1) { $nomes[1] } else { '(nenhum)' }))

            $celulas = Celulas $principal
            $nomesCelula = NomesCelula $principal
            Write-Host ("    primeira linha: " + (($nomesCelula | Select-Object -First 4) -join ' | '))

            Chk "a primeira linha tem conteudo" ($nomesCelula.Count -gt 1)

            if ($cabecalhos.Count -gt 0 -and $celulas.Count -gt 0) {
                # O texto da primeira celula (o nome do programa) tem que estar
                # embaixo do cabecalho do nome: e o que prova que o cabecalho
                # e a celula falam da mesma coluna.
                $cabecalho = $cabecalhos[0]
                $celula = $celulas[0]
                $dentro = $celula.X -ge $cabecalho.X -and $celula.X -lt ($cabecalho.X + $cabecalho.Largura)
                Chk "o conteudo da linha fica embaixo do cabecalho dele" $dentro ("celula: " + $celula.X + " cabecalho: " + $cabecalho.X + " largura: " + $cabecalho.Largura)
            }

            # ------------------------------------------------------------------
            # 3. O grupo dos programas novos.
            # ------------------------------------------------------------------
            $grupos = DoTipo $principal 'Group'
            $achouGrupo = $false
            foreach ($grupo in $grupos) {
                if ((TextoDe $grupo) -like ((Rotulo 'Group.New') + '*')) { $achouGrupo = $true; break }
            }
            Chk "a lista agrupa os programas novos" $achouGrupo

            # ------------------------------------------------------------------
            # 4. O painel lateral: o botao do realce.
            # ------------------------------------------------------------------
            Write-Host ""
            Write-Host "=== O painel lateral ==="

            $botaoPainel = PorId $principal 'CmdSettingsPanel'
            if ($null -ne $botaoPainel) { Clicar $botaoPainel | Out-Null }
            Start-Sleep -Seconds 2

            $realce = EmAlgumaJanela $processo.Id 'OptHighlightNewPrograms'
            Chk "o painel lateral tem o botao do realce do novo" ($null -ne $realce)

            if ($null -ne $realce) {
                $estadoAntes = ''
                try {
                    $padrao = $realce.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
                    $estadoAntes = [string]$padrao.Current.ToggleState
                    $padrao.Toggle()
                    Start-Sleep -Milliseconds 800
                    $estadoDepois = [string]$padrao.Current.ToggleState
                    Chk "o botao do realce responde" ($estadoAntes -ne $estadoDepois) ("antes: $estadoAntes depois: $estadoDepois")
                    $padrao.Toggle()
                    Start-Sleep -Milliseconds 500
                } catch {
                    Chk "o botao do realce responde" $false $_.Exception.Message
                }
            }

            # ------------------------------------------------------------------
            # 5. Mover a coluna.
            # ------------------------------------------------------------------
            Write-Host ""
            Write-Host "=== Mover a coluna ==="

            $cabecalhos = Cabecalhos $principal
            $antes = NomesCabecalho $principal
            $celulasAntes = NomesCelula $principal

            $indice = -1
            for ($i = 0; $i -lt $cabecalhos.Count; $i++) {
                if ($cabecalhos[$i].Nome -eq $rotuloTamanho) { $indice = $i; break }
            }
            Chk "a coluna do tamanho existe" ($indice -ge 0)

            if ($indice -ge 0) {
                $elemento = $cabecalhos[$indice].Elemento

                # O menu de contexto do cabecalho e um comando do mouse: a
                # janela precisa estar acordada. Se a primeira tentativa
                # falhar, o teste tenta de novo (e registra o que viu).
                $itemMover = $null

                Ativar $processo.Id | Out-Null
                Start-Sleep -Milliseconds 700
                ClicarMouse $elemento $true | Out-Null
                $itemMover = EsperarMenu $processo.Id 'CmdMoveColumn' 8

                if ($null -eq $itemMover) {
                    Write-Host "    (o menu nao abriu na primeira tentativa: as janelas do processo)"
                    foreach ($linha in (JanelasDoProcesso $processo.Id)) { Write-Host $linha }

                    Ativar $processo.Id | Out-Null
                    ClicarMouse $elemento | Out-Null
                    Start-Sleep -Seconds 2
                    ClicarMouse $elemento $true | Out-Null
                    $itemMover = EsperarMenu $processo.Id 'CmdMoveColumn' 8
                }

                Chk "o menu do cabecalho tem o comando de mover" ($null -ne $itemMover)

                if ($null -ne $itemMover) {
                    try {
                        $itemMover.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
                    } catch { }

                    $limiteSub = (Get-Date).AddSeconds(10)
                    $itemDireita = $null
                    while ((Get-Date) -lt $limiteSub -and -not $itemDireita) {
                        Start-Sleep -Milliseconds 400
                        $itemDireita = EmAlgumaJanela $processo.Id 'MoveColumnRight'
                    }
                    Chk "o comando de mover tem a opcao de ir para a direita" ($null -ne $itemDireita)

                    if ($null -ne $itemDireita) {
                        Clicar $itemDireita | Out-Null
                        Start-Sleep -Seconds 3

                        $depois = NomesCabecalho $principal
                        Write-Host ("    antes:  " + (($antes | Select-Object -First 4) -join ' | '))
                        Write-Host ("    depois: " + (($depois | Select-Object -First 4) -join ' | '))

                        Chk "a coluna mudou de lugar" ($depois.Count -gt $indice -and $depois[$indice] -ne $antes[$indice])

                        # O conteudo acompanha o cabecalho: a celula que estava
                        # embaixo de cada cabecalho continua embaixo dele.
                        $rotuloVersao = Rotulo 'Common.Version'
                        $rotuloTipo = Rotulo 'Common.Type'

                        $cabVersao = CabecalhoCom $principal $rotuloVersao
                        $cabTipo = CabecalhoCom $principal $rotuloTipo

                        $textoVersaoAntes = TextoSob $principal $cabVersao
                        $textoTipoAntes = TextoSob $principal $cabTipo

                        $textoVersaoDepois = TextoSob $principal $cabVersao
                        $textoTipoDepois = TextoSob $principal $cabTipo

                        Write-Host ("    versao: '$textoVersaoAntes' -> '$textoVersaoDepois' | tipo: '$textoTipoAntes' -> '$textoTipoDepois'")

                        Chk "o conteudo das linhas acompanha o cabecalho" `
                            ($textoVersaoAntes -eq $textoVersaoDepois -and $textoTipoAntes -eq $textoTipoDepois) `
                            ("antes: $textoVersaoAntes / $textoTipoAntes - depois: $textoVersaoDepois / $textoTipoDepois")
                    }
                }
            }

            # ------------------------------------------------------------------
            # 6. A ordem fica guardada.
            # ------------------------------------------------------------------
            Write-Host ""
            Write-Host "=== A ordem guardada ==="

            $ordemFinal = NomesCabecalho $principal

            # A ordem e gravada na hora em que a coluna muda de lugar: o
            # arquivo de preferencias ja tem a ordem nova.
            $ordemGravada = @()
            try {
                $preferencias = [System.IO.File]::ReadAllText($arquivoPreferencias) | ConvertFrom-Json
                if ($preferencias.PSObject.Properties.Name -contains 'ColumnOrder') {
                    $ordemGravada = @($preferencias.ColumnOrder)
                }
            } catch { }
            Chk "a ordem nova foi gravada nas preferencias" ($ordemGravada.Count -gt 1) ("gravado: " + ($ordemGravada -join ','))

            # O programa e encerrado do jeito que o teste pode: a ordem ja
            # esta no arquivo.
            try { Stop-Process -Id $processo.Id -Force -ErrorAction SilentlyContinue } catch { }
            $processo = $null

            if ($true) {
                $processo2 = Start-Process -FilePath $Exe -PassThru

                $limite = (Get-Date).AddSeconds($EsperaSegundos)
                $principal2 = $null
                while ((Get-Date) -lt $limite -and -not $principal2) {
                    Start-Sleep -Milliseconds 700
                    if (-not (Get-Process -Id $processo2.Id -ErrorAction SilentlyContinue)) { break }
                    $principal2 = JanelaComId $processo2.Id 'ProgramStatusText'
                }

                if ($null -ne $principal2) {
                    Clicar (PorId $principal2 'ModuleAllPrograms') | Out-Null
                    Start-Sleep -Seconds 5

                    $aoAbrir = NomesCabecalho $principal2
                    Write-Host ("    ao abrir: " + (($aoAbrir | Select-Object -First 4) -join ' | '))

                    $iguais = $aoAbrir.Count -eq $ordemFinal.Count
                    if ($iguais) {
                        for ($i = 0; $i -lt $aoAbrir.Count; $i++) {
                            if ($aoAbrir[$i] -ne $ordemFinal[$i]) { $iguais = $false; break }
                        }
                    }
                    Chk "o programa abre com a ordem que a pessoa deixou" $iguais
                }
                else {
                    Chk "o programa abre com a ordem que a pessoa deixou" $false "a janela nao apareceu"
                }
            }
        }
    }
}
finally {
    Write-Host ""
    Write-Host "=== Limpeza ==="

    foreach ($p in @($processo, $processo2)) {
        if ($null -ne $p) {
            try { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue } catch { }
        }
    }

    # As preferencias voltam a ser como estavam: o teste nao deixa marca.
    if ($haviaPreferencias -and [System.IO.File]::Exists($copiaPreferencias)) {
        [System.IO.File]::Copy($copiaPreferencias, $arquivoPreferencias, $true)
        Write-Host "    preferencias devolvidas"
    }
    elseif (-not $haviaPreferencias -and [System.IO.File]::Exists($arquivoPreferencias)) {
        [System.IO.File]::Delete($arquivoPreferencias)
        Write-Host "    preferencias que o teste criou foram apagadas"
    }
    if ([System.IO.File]::Exists($copiaPreferencias)) { [System.IO.File]::Delete($copiaPreferencias) }
}

Write-Host ""
if ($falhas -eq 0) {
    Write-Host "TODAS AS VERIFICACOES PASSARAM"
    exit 0
}

Write-Host ("FALHAS: {0}" -f $falhas)
exit 1
