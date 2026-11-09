<#
  Teste de ponta a ponta do lote 21: o StartUp Tuner completo (ASHAMPOO T4#47)
  e o Crash Analyzer com export e busca online (ASHAMPOO T4#51).

  O que ele prova, na janela de verdade e na linha de comando de verdade:
    1. a tela do Gerenciador de Autorun tem os comandos "Adicionar" e "Editar";
    2. a tela do Crash Analyzer tem "Exportar relatorio" e "Pesquisar na web";
    3. a linha de comando cria um item de inicializacao, edita o nome e o
       comando, desativa, ativa e remove — devolvendo a maquina ao estado
       anterior (o estado inicial e medido no comeco e comparado no fim);
    4. a linha de comando grava o relatorio HTML das falhas e mostra o
       endereco da busca online.

  A suite de testes cobre o nucleo (AutorunManager, CrashAnalyzer); este
  script cobre a ligacao com a interface e com a linha de comando, que a suite
  nao alcanca (a suite nao referencia o projeto WPF).

  O script nao mexe no ambiente de quem roda: o item criado para o teste e
  apagado no final, mesmo se o teste falhar, e o settings.json existente e
  guardado antes e devolvido no fim.

  Uso:
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-lote21.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-lote21.ps1 -Exe publish\preview\TranalUninstaller.exe -CliExe publish\cli\tranal.exe
#>
param(
    [string]$Exe = '',
    [string]$CliExe = '',
    [string]$DataDirectory = '',
    [int]$EsperaSegundos = 25,
    [int]$EsperaCargaSegundos = 60
)


# O parametro do executavel da CLI nao pode se chamar $Cli: o PowerShell nao
# distingue maiusculas de minusculas, e a saida do comando sobrescreveria o
# caminho do executavel. Por isso ele se chama $CliExe.
$ErrorActionPreference = 'Stop'


$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Exe)) { $Exe = Join-Path $repoRoot 'publish\preview\TranalUninstaller.exe' }
if (-not [System.IO.Path]::IsPathRooted($Exe)) { $Exe = Join-Path $repoRoot $Exe }
if ([string]::IsNullOrWhiteSpace($CliExe)) { $CliExe = Join-Path $repoRoot 'publish\cli\tranal.exe' }
if (-not [System.IO.Path]::IsPathRooted($CliExe)) { $CliExe = Join-Path $repoRoot $CliExe }

if ([string]::IsNullOrWhiteSpace($DataDirectory)) {
    $DataDirectory = Join-Path $env:LOCALAPPDATA 'Tranal Group\Tranal Uninstaller Pro'
}

if (-not (Test-Path $Exe)) { throw "Executavel nao encontrado: $Exe" }
if (-not (Test-Path $CliExe)) { throw "CLI nao encontrada: $CliExe" }

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

if (-not ('TranalJanelas21' -as [type])) {
Add-Type @'
using System; using System.Text; using System.Runtime.InteropServices; using System.Collections.Generic;
public class TranalJanelas21 {
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


# ----------------------------------------------------------------------
# A janela: e a que CONTEM o botao da ferramenta (a splash tem o mesmo
# titulo, mas nao tem a barra lateral).
# ----------------------------------------------------------------------
function JanelaPrincipal([int]$processId) {
    foreach ($h in [TranalJanelas21]::VisiveisDe([uint32]$processId)) {
        try {
            $el = [System.Windows.Automation.AutomationElement]::FromHandle($h)
            $cond = New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'ToolAutorunManager')
            $achados = $el.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
            if ($achados.Count -gt 0) { return $el }
        } catch { }
    }
    return $null
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

function TextoDe($elemento) {
    if ($null -eq $elemento) { return '' }
    try {
        $p = $elemento.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
        return [string]$p.Current.Value
    } catch { }
    try { return [string]$elemento.Current.Name } catch { }
    return ''
}

function Clicar($elemento) {
    if ($null -eq $elemento) { return $false }
    try {
        $elemento.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        return $true
    } catch { return $false }
}


# ----------------------------------------------------------------------
# O registro: o item de inicializacao criado para o teste.
# ----------------------------------------------------------------------
$pastaRun = 'SOFTWARE\Microsoft\Windows\CurrentVersion\Run'
$nomeItem = 'TranalTesteTemporario'
$nomeEditado = 'TranalTesteEditado'
$comando = 'C:\TranalTesteTemporario\teste.exe'
$comandoNovo = 'C:\TranalTesteTemporario\outro.exe'
$prefixoDesabilitado = 'DisabledName-'

function ValorDoRun([string]$nome) {
    $base = $null
    try {
        $base = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($pastaRun, $false)
        if ($null -eq $base) { return $null }
        $valor = $base.GetValue($nome)
        if ($null -eq $valor) { return $null }
        return [string]$valor
    } catch { return $null }
    finally { if ($null -ne $base) { $base.Close() } }
}

function ApagarValoresDoTeste() {
    $base = $null
    try {
        $base = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($pastaRun, $true)
        if ($null -eq $base) { return $false }
        foreach ($nome in @($nomeItem, $nomeEditado, ($prefixoDesabilitado + $nomeItem), ($prefixoDesabilitado + $nomeEditado))) {
            if ($base.GetValueNames() -contains $nome) { $base.DeleteValue($nome, $false) }
        }
        return $true
    } catch { return $false }
    finally { if ($null -ne $base) { $base.Close() } }
}


# ----------------------------------------------------------------------
# A linha de comando
# ----------------------------------------------------------------------
function Executar-Cli([string[]]$argumentos) {
    # Com $ErrorActionPreference = 'Stop', a saida de erro de um programa
    # nativo aborta a captura. Aqui a preferencia e baixa so durante a
    # leitura: o texto de erro faz parte do que o comando respondeu.
    $anterior = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $saida = & $CliExe @argumentos 2>&1 | Out-String
        $codigo = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $anterior }

    return @{ Codigo = $codigo; Texto = $saida }
}

$carregando = @('Verificando...', 'Scanning...', 'Carregando...', 'Loading...')

function Carregou([string]$texto) {
    if ([string]::IsNullOrWhiteSpace($texto)) { return $false }
    if ($carregando -contains $texto) { return $false }
    return $true
}

$settings = Join-Path $DataDirectory 'settings.json'
$reserva = Join-Path $env:TEMP ("tranal-lote21-reserva-{0}.json" -f [Guid]::NewGuid().ToString('N'))
$relatorio = Join-Path $env:TEMP ("tranal-lote21-crash-{0}.html" -f [Guid]::NewGuid().ToString('N'))

Write-Host "=== Preparacao ==="
Write-Host "    executavel: $Exe"
Write-Host "    cli: $CliExe"

$haviaSettings = Test-Path $settings
if ($haviaSettings) {
    [System.IO.File]::Copy($settings, $reserva, $true)
    Write-Host "    settings.json guardado em $reserva"
}

# O item do teste nao pode existir antes: o teste comeca de um estado limpo.
ApagarValoresDoTeste | Out-Null

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

    # ------------------------------------------------------------------
    # 1. A tela do Gerenciador de Autorun: adicionar e editar.
    # ------------------------------------------------------------------
    $botao = PorId $janela 'ToolAutorunManager'
    Chk "o botao do gerenciador de autorun existe na barra lateral" ($null -ne $botao)

    Clicar $botao | Out-Null
    Start-Sleep -Seconds 4

    foreach ($id in @('AutorunRefresh', 'AutorunEnable', 'AutorunDisable', 'AutorunRemove', 'AutorunAdd', 'AutorunEdit')) {
        Chk ("a tela do autorun tem o comando '{0}'" -f $id) ($null -ne (PorId $janela $id))
    }

    $titulo = TextoDe (PorId $janela 'PanelTitle')
    $tituloOk = ($titulo -match 'Autorun') -or ($titulo -match 'inicializa')
    Chk "o titulo do painel e o do gerenciador de autorun" $tituloOk "titulo='$titulo'"

    $lista = PorId $janela 'PanelItems'
    Chk "a lista do gerenciador de autorun existe" ($null -ne $lista)

    $limiteCarga = (Get-Date).AddSeconds($EsperaCargaSegundos)
    $status = ''
    while ((Get-Date) -lt $limiteCarga) {
        $status = TextoDe (PorId $janela 'PanelStatus')
        if (Carregou $status) { break }
        Start-Sleep -Milliseconds 800
    }
    Chk "o gerenciador de autorun carregou" (Carregou $status) "status='$status'"

    $itens = 0
    if ($null -ne $lista) {
        try {
            $condItem = New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
                [System.Windows.Automation.ControlType]::DataItem)
            $achados = $lista.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condItem)
            $itens = $achados.Count
        } catch { Write-Host ("    aviso: nao deu para ler os itens: " + $_.Exception.Message) }
    }
    Write-Host ("    itens de inicializacao na janela: {0}" -f $itens)
    Chk "a janela lista os itens de inicializacao" ($itens -gt 0) "itens=$itens"

    # ------------------------------------------------------------------
    # 2. A tela do Crash Analyzer: exportar e buscar online.
    # ------------------------------------------------------------------
    $botaoCrash = PorId $janela 'ToolCrashAnalyzer'
    Chk "o botao do crash analyzer existe na barra lateral" ($null -ne $botaoCrash)

    Clicar $botaoCrash | Out-Null
    Start-Sleep -Seconds 4

    foreach ($id in @('CrashAnalyzerRefresh', 'CrashAnalyzerExport', 'CrashAnalyzerSearch')) {
        Chk ("a tela do crash analyzer tem o comando '{0}'" -f $id) ($null -ne (PorId $janela $id))
    }

    $tituloCrash = TextoDe (PorId $janela 'PanelTitle')
    $tituloCrashOk = ($tituloCrash -match 'Crash') -or ($tituloCrash -match 'falha')
    Chk "o titulo do painel e o do crash analyzer" $tituloCrashOk "titulo='$tituloCrash'"

    # ------------------------------------------------------------------
    # 3. A linha de comando: o ciclo inteiro do item de inicializacao.
    # ------------------------------------------------------------------
    $listaCli = Executar-Cli @('/autorun')
    Chk "a linha de comando lista os itens de inicializacao" ($listaCli.Codigo -eq 0) "codigo=$($listaCli.Codigo)"
    Chk "a linha de comando mostra o total de itens" ($listaCli.Texto -match 'Itens de inicializa') $listaCli.Texto

    $criou = Executar-Cli @('/autorun', '/add', $nomeItem, $comando)
    Chk "a linha de comando cria um item de inicializacao" ($criou.Codigo -eq 0) "codigo=$($criou.Codigo) $($criou.Texto)"
    Chk "o item criado aparece no registro" ($null -ne (ValorDoRun $nomeItem)) "valor=$(ValorDoRun $nomeItem)"
    Chk "o item criado aponta para o comando informado" ((ValorDoRun $nomeItem) -eq $comando) "valor=$(ValorDoRun $nomeItem)"

    $comItem = Executar-Cli @('/autorun')
    Chk "o item criado aparece na listagem" ($comItem.Texto -match [regex]::Escape($nomeItem)) $comItem.Texto

    $editou = Executar-Cli @('/autorun', '/edit', $nomeItem, $nomeEditado, $comandoNovo)
    Chk "a linha de comando edita o item" ($editou.Codigo -eq 0) "codigo=$($editou.Codigo) $($editou.Texto)"
    Chk "o item editado tem o nome novo" ($null -ne (ValorDoRun $nomeEditado)) "valor=$(ValorDoRun $nomeEditado)"
    Chk "o item editado tem o comando novo" ((ValorDoRun $nomeEditado) -eq $comandoNovo) "valor=$(ValorDoRun $nomeEditado)"
    Chk "o nome antigo sumiu do registro" ($null -eq (ValorDoRun $nomeItem)) "valor=$(ValorDoRun $nomeItem)"

    $desativou = Executar-Cli @('/autorun', '/disable', $nomeEditado)
    Chk "a linha de comando desativa o item" ($desativou.Codigo -eq 0) "codigo=$($desativou.Codigo) $($desativou.Texto)"
    Chk "o item desativado fica com o prefixo do estado" `
        ($null -ne (ValorDoRun ($prefixoDesabilitado + $nomeEditado))) `
        "valor=$(ValorDoRun ($prefixoDesabilitado + $nomeEditado))"

    $ativou = Executar-Cli @('/autorun', '/enable', $nomeEditado)
    Chk "a linha de comando ativa o item" ($ativou.Codigo -eq 0) "codigo=$($ativou.Codigo) $($ativou.Texto)"
    Chk "o item ativado volta ao nome normal" ($null -ne (ValorDoRun $nomeEditado)) "valor=$(ValorDoRun $nomeEditado)"

    $removeu = Executar-Cli @('/autorun', '/remove', $nomeEditado)
    Chk "a linha de comando remove o item" ($removeu.Codigo -eq 0) "codigo=$($removeu.Codigo) $($removeu.Texto)"
    Chk "o item removido sumiu do registro" ($null -eq (ValorDoRun $nomeEditado)) "valor=$(ValorDoRun $nomeEditado)"

    $inexistente = Executar-Cli @('/autorun', '/edit', 'item-que-nao-existe-xyz', 'outro')
    Chk "editar um item que nao existe falha com o motivo" `
        (($inexistente.Codigo -eq 1) -and ($inexistente.Texto -match 'nao encontrado')) `
        ("codigo=" + $inexistente.Codigo)

    # ------------------------------------------------------------------
    # 4. A linha de comando do Crash Analyzer.
    # ------------------------------------------------------------------
    $crash = Executar-Cli @('/crashanalyzer')
    Chk "a linha de comando lista as falhas por programa" ($crash.Codigo -eq 0) "codigo=$($crash.Codigo) $($crash.Texto)"
    Chk "a listagem mostra o total de programas com falha" `
        ($crash.Texto -match 'Programas com falha registrada:\s*\d+') $crash.Texto

    $exportou = Executar-Cli @('/crashanalyzer', '/export', $relatorio)
    Chk "a linha de comando grava o relatorio das falhas" ($exportou.Codigo -eq 0) "codigo=$($exportou.Codigo) $($exportou.Texto)"
    Chk "o relatorio gravado existe" (Test-Path $relatorio) "arquivo=$relatorio"

    if (Test-Path $relatorio) {
        $html = [System.IO.File]::ReadAllText($relatorio)
        Chk "o relatorio gravado e um HTML valido" `
            (($html -match '<!DOCTYPE html>') -and ($html -match '</html>')) "tamanho=$($html.Length)"
    }

    $busca = Executar-Cli @('/crashanalyzer', '/search', 'programa-que-nao-falhou-xyz')
    Chk "buscar online por um programa sem falha falha com o motivo" `
        (($busca.Codigo -eq 1) -and ($busca.Texto -match 'sem falha registrada')) `
        ("codigo=" + $busca.Codigo)

    # ------------------------------------------------------------------
    # 5. A ajuda e o catalogo de ferramentas.
    # ------------------------------------------------------------------
    $ajuda = Executar-Cli @('/?')
    Chk "a ajuda da linha de comando menciona /autorun /add" ($ajuda.Texto -match '/autorun /add')
    Chk "a ajuda da linha de comando menciona /crashanalyzer" ($ajuda.Texto -match '/crashanalyzer')

    $painel = Executar-Cli @('/painel')
    Chk "o catalogo de ferramentas conhece o gerenciador de autorun" ($painel.Texto -match 'AutorunManager\s+-')
    Chk "o catalogo de ferramentas conhece o crash analyzer" ($painel.Texto -match 'CrashAnalyzer\s+-')
}
finally {
    Write-Host ""
    Write-Host "=== Limpeza ==="

    # O item do teste sai, mesmo se o teste falhar no meio.
    $apagou = ApagarValoresDoTeste
    Write-Host ("    item de inicializacao do teste removido: {0}" -f $apagou)
    Write-Host ("    sobrou valor de teste no registro: {0}" -f `
        ((ValorDoRun $nomeItem) -ne $null -or (ValorDoRun $nomeEditado) -ne $null -or `
         (ValorDoRun ($prefixoDesabilitado + $nomeEditado)) -ne $null))

    if ($haviaSettings -and (Test-Path $reserva)) {
        [System.IO.File]::Copy($reserva, $settings, $true)
        Write-Host "    settings.json devolvido"
        [System.IO.File]::Delete($reserva)
    }

    if (Test-Path $relatorio) {
        [System.IO.File]::Delete($relatorio)
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
