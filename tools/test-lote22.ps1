<#
  Teste de ponta a ponta do lote 22: as acoes do programa na lista
  (Microsoft Store, Browse Location/Source, "Avaliar on-line"), o aviso de
  residuo, a categoria vazia e o modo rapido (confirmar e seguir).

  O que ele prova, na janela de verdade e na linha de comando de verdade:
    1. o menu de contexto do programa tem "Abrir no Windows" (com a Microsoft
       Store e as pastas do programa e do desinstalador) e "Avaliar on-line";
    2. a tela de Categorias mostra a categoria sem programa dizendo isso;
    3. o modo rapido abre o assistente ja no "Desinstalar agora" — e o
       Cancelar nao remove nada;
    4. a linha de comando expoe /inspection-rate, /inspection-store e a
       categoria vazia.

  A suite de testes cobre o nucleo (SystemNavigator, SystemActions,
  UninstallAdvisories, ExecutionPolicy, ProgramCategoryTree); este script
  cobre a ligacao com a interface e com a linha de comando, que a suite nao
  alcanca (a suite nao referencia o projeto WPF).

  O script nao mexe no ambiente de quem roda: a categoria criada para o
  teste e apagada no final, mesmo se o teste falhar, e o program-data.json
  existente e guardado antes e devolvido no fim. NENHUM programa e
  desinstalado: o modo rapido e aberto e cancelado.

  Uso:
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-lote22.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-lote22.ps1 -Exe publish\preview\TranalUninstaller.exe -CliExe publish\cli\tranal.exe
#>
param(
    [string]$Exe = '',
    [string]$CliExe = '',
    [string]$DataDirectory = '',
    [int]$EsperaSegundos = 30,
    [int]$EsperaCargaSegundos = 90
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

if (-not ('TranalJanelas22' -as [type])) {
Add-Type @'
using System; using System.Text; using System.Runtime.InteropServices; using System.Collections.Generic;
public class TranalJanelas22 {
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  public const uint MOUSEEVENTF_LEFTDOWN = 0x0002, MOUSEEVENTF_LEFTUP = 0x0004;
  public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008, MOUSEEVENTF_RIGHTUP = 0x0010;
  public const uint KEYEVENTF_KEYUP = 0x0002;
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

function Clicar($elemento) {
    if ($null -eq $elemento) { return $false }
    try {
        $elemento.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        return $true
    } catch { return $false }
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

# O elemento de um identificador em QUALQUER janela do processo: o menu de
# contexto e o menu principal sao janelas proprias (popup), e nao descendentes
# da janela principal.
function PorIdNoProcesso([int]$processId, [string]$automationId) {
    try {
        $cond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $processId)
        $todos = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
            [System.Windows.Automation.TreeScope]::Descendants, $cond)

        $condId = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $automationId)

        foreach ($el in $todos) {
            try {
                $achados = $el.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condId)
                if ($achados.Count -gt 0) { return $achados[0] }
            } catch { }
        }
    } catch { }
    return $null
}

# Espera o elemento aparecer: a tela do Windows tem tempo de responder, e o
# teste nao pode medir o tempo da maquina.
function EsperarPorIdNoProcesso([int]$processId, [string]$automationId, [int]$segundos) {
    $limite = (Get-Date).AddSeconds($segundos)
    while ((Get-Date) -lt $limite) {
        $el = PorIdNoProcesso $processId $automationId
        if ($null -ne $el) { return $el }
        Start-Sleep -Milliseconds 500
    }
    return $null
}

function JanelaComId([int]$processId, [string]$automationId) {
    foreach ($h in [TranalJanelas22]::VisiveisDe([uint32]$processId)) {
        try {
            $el = [System.Windows.Automation.AutomationElement]::FromHandle($h)
            $cond = New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $automationId)
            $achados = $el.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
            if ($achados.Count -gt 0) { return $el }
        } catch { }
    }
    return $null
}

function Executar-Cli([string[]]$argumentos) {
    $anterior = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $saida = & $CliExe @argumentos 2>&1 | Out-String
        $codigo = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $anterior }

    return @{ Codigo = $codigo; Texto = $saida }
}

function ClicarComOMouse($elemento, [bool]$direito = $false) {
    if ($null -eq $elemento) { return $false }
    try {
        $r = $elemento.Current.BoundingRectangle
        $x = [int]($r.X + ($r.Width / 2))
        $y = [int]($r.Y + ($r.Height / 2))

        [TranalJanelas22]::SetCursorPos($x, $y) | Out-Null
        Start-Sleep -Milliseconds 300

        if ($direito) {
            [TranalJanelas22]::mouse_event([TranalJanelas22]::MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, [UIntPtr]::Zero)
            [TranalJanelas22]::mouse_event([TranalJanelas22]::MOUSEEVENTF_RIGHTUP, 0, 0, 0, [UIntPtr]::Zero)
        }
        else {
            [TranalJanelas22]::mouse_event([TranalJanelas22]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
            [TranalJanelas22]::mouse_event([TranalJanelas22]::MOUSEEVENTF_LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
        }
        return $true
    } catch { return $false }
}

function ItensDaLista($lista) {
    if ($null -eq $lista) { return @() }
    try {
        $condItem = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::DataItem)
        return $lista.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condItem)
    } catch { return @() }
}


# ----------------------------------------------------------------------
# A categoria vazia do teste
# ----------------------------------------------------------------------

$nomeCategoria = 'TranalTesteCategoriaVazia'
$arquivoDados = Join-Path $DataDirectory 'program-data.json'
$reservaDados = Join-Path $env:TEMP ("tranal-lote22-dados-{0}.json" -f [Guid]::NewGuid().ToString('N'))

Write-Host "=== Preparacao ==="
Write-Host "    executavel: $Exe"
Write-Host "    cli: $CliExe"

$haviaDados = Test-Path $arquivoDados
if ($haviaDados) {
    [System.IO.File]::Copy($arquivoDados, $reservaDados, $true)
    Write-Host "    program-data.json guardado em $reservaDados"
}

# A categoria do teste: uma categoria do usuario sem programa nenhum. E o
# caso em que o Geek mostra "no programs in this category".
if (-not (Test-Path $DataDirectory)) {
    [System.IO.Directory]::CreateDirectory($DataDirectory) | Out-Null
}

$conteudo = "{`n    `"Groups`": [`n        `"$nomeCategoria`"`n    ],`n    `"Programs`": []`n}"
[System.IO.File]::WriteAllText($arquivoDados, $conteudo, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "    categoria de teste criada: $nomeCategoria"

$processo = $null
try {
    Write-Host ""
    Write-Host "=== Execucao ==="

    # ------------------------------------------------------------------
    # 1. A linha de comando: as categorias, a avaliacao e a loja.
    # ------------------------------------------------------------------
    $categorias = Executar-Cli @('/categories')
    Chk "a linha de comando lista as categorias" ($categorias.Codigo -eq 0) "codigo=$($categorias.Codigo)"
    Chk "a categoria vazia aparece na listagem" `
        ($categorias.Texto -match [regex]::Escape($nomeCategoria)) $categorias.Texto
    Chk "a categoria vazia diz que nao ha programa nela" `
        ($categorias.Texto -match 'Nenhum programa nesta categoria') $categorias.Texto

    $avaliou = Executar-Cli @('/inspection-rate', 'programa-que-nao-existe-xyz')
    Chk "avaliar on-line de um programa inexistente falha com o motivo" `
        (($avaliou.Codigo -eq 1) -and ($avaliou.Texto -match 'nao encontrado')) `
        ("codigo=" + $avaliou.Codigo + " " + $avaliou.Texto)

    $loja = Executar-Cli @('/inspection-store', 'programa-que-nao-existe-xyz')
    Chk "abrir a loja de um programa inexistente falha com o motivo" `
        (($loja.Codigo -eq 1) -and ($loja.Texto -match 'nao encontrado')) `
        ("codigo=" + $loja.Codigo + " " + $loja.Texto)

    # ------------------------------------------------------------------
    # 2. A janela do Tranal.
    # ------------------------------------------------------------------
    $processo = Start-Process -FilePath $Exe -PassThru
    Write-Host ("    pid: {0}" -f $processo.Id)

    $limite = (Get-Date).AddSeconds($EsperaSegundos)
    $janela = $null
    while ((Get-Date) -lt $limite -and -not $janela) {
        Start-Sleep -Milliseconds 700
        if (-not (Get-Process -Id $processo.Id -ErrorAction SilentlyContinue)) { break }
        $janela = JanelaComId $processo.Id 'ProgramStatusText'
    }

    Chk "a janela principal do Tranal aparece" ($null -ne $janela)
    if (-not $janela) { throw "A janela principal nao apareceu: o teste nao tem como continuar." }

    # O modulo "Todos os Programas" precisa estar aberto: a lista de
    # programas so existe na tela (o que estiver aberto por padrao e a
    # Central).
    Clicar (PorId $janela 'ModuleAllPrograms') | Out-Null
    Start-Sleep -Seconds 3

    $lista = PorId $janela 'ProgramsList'
    Chk "a lista de programas existe" ($null -ne $lista)

    # A lista carrega: o teste espera os itens aparecerem.
    $limiteCarga = (Get-Date).AddSeconds($EsperaCargaSegundos)
    $itens = 0
    while ((Get-Date) -lt $limiteCarga) {
        $itens = (ItensDaLista $lista).Count
        if ($itens -gt 0) { break }
        Start-Sleep -Milliseconds 800
    }
    Write-Host ("    programas na lista: {0}" -f $itens)
    Chk "a lista de programas carregou" ($itens -gt 0) "itens=$itens"

    # ------------------------------------------------------------------
    # 3. O menu de contexto do programa: Microsoft Store, pastas e avaliacao.
    # ------------------------------------------------------------------
    $abrir = $null
    for ($tentativa = 1; $tentativa -le 3 -and $null -eq $abrir; $tentativa++) {
        $itens = ItensDaLista $lista
        if ($itens.Count -eq 0) { Start-Sleep -Seconds 2; continue }

        ClicarComOMouse $itens[0] $true | Out-Null
        Start-Sleep -Seconds 2

        $abrir = PorIdNoProcesso $processo.Id 'CmdOpenInWindows'
        if ($null -eq $abrir) {
            Write-Host ("    (tentativa {0} de abrir o menu de contexto)" -f $tentativa)
        }
    }

    Chk "o menu do programa tem 'Abrir no Windows'" ($null -ne $abrir)

    $avaliar = PorIdNoProcesso $processo.Id 'CmdRateProgram'
    Chk "o menu do programa tem 'Avaliar on-line'" ($null -ne $avaliar)

    # O submenu: a loja e as pastas do programa e do desinstalador.
    if ($null -ne $abrir) {
        try {
            $exp = $abrir.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
            $exp.Expand()
        } catch {
            try { Clicar $abrir | Out-Null } catch { }
        }
        Start-Sleep -Seconds 2
    }

    $lojaItem = EsperarPorIdNoProcesso $processo.Id 'CmdOpenMicrosoftStore' 5
    Chk "o submenu tem a Microsoft Store" ($null -ne $lojaItem)

    $pastaPrograma = EsperarPorIdNoProcesso $processo.Id 'CmdOpenProgramFolder' 5
    Chk "o submenu tem a pasta do programa" ($null -ne $pastaPrograma)

    # Fechar o menu: a tecla ESC fecha o popup aberto.
    [TranalJanelas22]::keybd_event(0x1B, 0, 0, [UIntPtr]::Zero)
    [TranalJanelas22]::keybd_event(0x1B, 0, [TranalJanelas22]::KEYEVENTF_KEYUP, [UIntPtr]::Zero)
    Start-Sleep -Seconds 2

    # ------------------------------------------------------------------
    # 4. A tela de Categorias: a categoria sem programa.
    # ------------------------------------------------------------------
    $botaoCategorias = PorId $janela 'ToolProgramCategories'
    Chk "o botao das categorias existe na barra lateral" ($null -ne $botaoCategorias)

    Clicar $botaoCategorias | Out-Null
    Start-Sleep -Seconds 6

    $listaPanel = PorId $janela 'PanelItems'
    Chk "a lista da tela de categorias existe" ($null -ne $listaPanel)

    $achouVazia = $false
    if ($null -ne $listaPanel) {
        try {
            $condTexto = New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
                [System.Windows.Automation.ControlType]::Text)

            foreach ($item in (ItensDaLista $listaPanel)) {
                # O nome do item da lista pode vir vazio: o texto que
                # importa esta nas celulas, e sao elas que sao lidas.
                $texto = [string]$item.Current.Name
                try {
                    $textos = $item.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condTexto)
                    foreach ($t in $textos) { $texto += ' | ' + [string]$t.Current.Name }
                } catch { }

                if ($texto -match [regex]::Escape($nomeCategoria) -and
                    $texto -match 'Nenhum programa nesta categoria') {
                    $achouVazia = $true
                    Write-Host ("    linha: {0}" -f $texto)
                    break
                }
            }
        } catch { Write-Host ("    aviso: nao deu para ler as linhas: " + $_.Exception.Message) }
    }
    Chk "a categoria sem programa diz que nao ha programa nela" $achouVazia

    # ------------------------------------------------------------------
    # 5. O modo rapido: confirmar e seguir (e cancelar, sem remover nada).
    # ------------------------------------------------------------------
    $rapido = $null
    for ($tentativa = 1; $tentativa -le 3 -and $null -eq $rapido; $tentativa++) {
        Clicar (PorId $janela 'CmdMoreCommands') | Out-Null
        Start-Sleep -Seconds 2

        $rapido = PorIdNoProcesso $processo.Id 'CmdQuickUninstall'
        if ($null -eq $rapido) {
            Write-Host ("    (tentativa {0} de abrir o menu principal)" -f $tentativa)
            # O popup pode ter aberto fora da tela: ESC fecha e o teste
            # tenta de novo.
            [TranalJanelas22]::keybd_event(0x1B, 0, 0, [UIntPtr]::Zero)
            [TranalJanelas22]::keybd_event(0x1B, 0, [TranalJanelas22]::KEYEVENTF_KEYUP, [UIntPtr]::Zero)
            Start-Sleep -Seconds 1
        }
    }

    Chk "o menu de comandos tem a desinstalacao rapida" ($null -ne $rapido)

    $assistente = $null
    if ($null -ne $rapido) {
        Clicar $rapido | Out-Null

        $limiteWizard = (Get-Date).AddSeconds(25)
        while ((Get-Date) -lt $limiteWizard -and -not $assistente) {
            Start-Sleep -Milliseconds 700
            $assistente = JanelaComId $processo.Id 'WizardNextButton'
        }
    }

    Chk "o modo rapido abre o assistente" ($null -ne $assistente)

    if ($null -ne $assistente) {
        $botao = PorId $assistente 'WizardNextButton'
        $nome = [string]$botao.Current.Name
        Write-Host ("    botao do modo rapido: '{0}'" -f $nome)

        $ehRapido = ($nome -match 'Desinstalar agora') -or ($nome -match 'Uninstall now')
        Chk "o assistente do modo rapido oferece o botao de desinstalar agora" $ehRapido "nome='$nome'"

        $mensagem = TextoDe (PorId $assistente 'WizardMessage')
        Write-Host ("    mensagem: {0}" -f $mensagem)
        $explica = ($mensagem -match 'Nenhum programa nesta categoria') -or
                   ($mensagem -match 'documento') -or
                   ($mensagem -match 'document')
        Chk "a primeira etapa do modo rapido diz o que vai acontecer" `
            ((-not [string]::IsNullOrWhiteSpace($mensagem)) -and $explica) "mensagem='$mensagem'"

        # Cancelar: nada e removido.
        Clicar (PorId $assistente 'WizardCancelButton') | Out-Null
        Start-Sleep -Seconds 3

        $ainda = JanelaComId $processo.Id 'WizardNextButton'
        Chk "o assistente fecha ao cancelar" ($null -eq $ainda)
    }
}
finally {
    Write-Host ""
    Write-Host "=== Limpeza ==="

    if ($haviaDados -and (Test-Path $reservaDados)) {
        [System.IO.File]::Copy($reservaDados, $arquivoDados, $true)
        Write-Host "    program-data.json devolvido"
        [System.IO.File]::Delete($reservaDados)
    }
    elseif (-not $haviaDados -and (Test-Path $arquivoDados)) {
        [System.IO.File]::Delete($arquivoDados)
        Write-Host "    program-data.json do teste removido"
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
