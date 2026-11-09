<#
  Teste de ponta a ponta do gerenciador de menu de contexto (HIBIT T2#18 e T4#71).

  O que ele prova, na janela de verdade e na linha de comando de verdade:
    1. o botao do gerenciador existe na barra lateral e abre a tela;
    2. a tela carrega e mostra as colunas e as entradas medidas;
    3. a linha de comando lista as mesmas entradas da janela;
    4. "restaurar" e "desativar" funcionam e devolvem a maquina ao estado
       anterior (o estado inicial e medido no comeco e comparado no fim);
    5. uma entrada de terceiro, criada no registro so para o teste, aparece na
       listagem e pode ser removida pelo identificador.

  A suite de testes cobre o nucleo (ContextMenuOverview, ContextMenuManager);
  este script cobre a ligacao com a interface e com a linha de comando, que a
  suite nao alcanca (a suite nao referencia o projeto WPF).

  O script nao mexe no ambiente de quem roda: o settings.json existente e o
  .reg do registro sao guardados antes e devolvidos no final, mesmo se o
  teste falhar. A chave de registro criada para o teste e apagada no final.

  Uso:
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-contextmenu.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-contextmenu.ps1 -Exe publish\preview\TranalUninstaller.exe -CliExe publish\cli\tranal.exe
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

if (-not ('TranalJanelas5' -as [type])) {
Add-Type @'
using System; using System.Text; using System.Runtime.InteropServices; using System.Collections.Generic;
public class TranalJanelas5 {
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
# A janela: e a que CONTEM o botao do gerenciador (a splash tem o mesmo
# titulo, mas nao tem a barra lateral).
# ----------------------------------------------------------------------
function JanelaPrincipal([int]$processId) {
    foreach ($h in [TranalJanelas5]::VisiveisDe([uint32]$processId)) {
        try {
            $el = [System.Windows.Automation.AutomationElement]::FromHandle($h)
            $cond = New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'ToolContextMenu')
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
# O registro: a chave de teste e o estado das entradas do Tranal.
#
# O "using" do PowerShell so vale no comeco do script: aqui a chave e
# fechada a mao.
# ----------------------------------------------------------------------
$pastaHandlers = 'Software\Classes\*\shellex\ContextMenuHandlers'
$nomeTeste = 'TranalTesteTemporario'
$clsidTeste = '{7A1B4C10-9E2D-4B6E-9C21-0F3A5D8E1B77}'
$pastaClsid = 'Software\Classes\CLSID\' + $clsidTeste
$dllTeste = '"C:\TranalTesteTemporario\inexistente.dll"'

function ChaveTesteExiste() {
    $base = $null
    try {
        $base = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($pastaHandlers, $false)
        if ($null -eq $base) { return $false }
        return ($base.GetSubKeyNames() -contains $nomeTeste)
    } catch { return $false }
    finally { if ($null -ne $base) { $base.Close() } }
}

function CriarChaveTeste() {
    $base = $null
    $sub = $null
    $clsid = $null
    $servidor = $null
    try {
        $base = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($pastaHandlers, $true)
        if ($null -eq $base) { return $false }
        $sub = $base.CreateSubKey($nomeTeste, $true)
        if ($null -eq $sub) { return $false }
        $sub.SetValue($null, $clsidTeste, [Microsoft.Win32.RegistryValueKind]::String)
        $sub.Close()
        $sub = $null

        # A CLSID declara uma DLL que nao existe: o caminho e o que classifica
        # a entrada como "de programa" (e nao do Windows), e o Explorer nunca
        # carrega uma DLL inexistente — o botao direito nao muda na maquina.
        $clsid = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($pastaClsid, $true)
        if ($null -eq $clsid) { return $false }
        $servidor = $clsid.CreateSubKey('InprocServer32', $true)
        if ($null -eq $servidor) { return $false }
        $servidor.SetValue($null, $dllTeste, [Microsoft.Win32.RegistryValueKind]::String)
        return $true
    } catch { return $false }
    finally {
        if ($null -ne $servidor) { $servidor.Close() }
        if ($null -ne $clsid) { $clsid.Close() }
        if ($null -ne $sub) { $sub.Close() }
        if ($null -ne $base) { $base.Close() }
    }
}

function ApagarChaveTeste() {
    $base = $null
    $clsid = $null
    try {
        $base = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($pastaHandlers, $true)
        if ($null -ne $base) {
            if ($base.GetSubKeyNames() -contains $nomeTeste) {
                $base.DeleteSubKeyTree($nomeTeste, $false)
            }
        }

        # A CLSID criada para o teste tambem tem de sair.
        $clsid = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Classes\CLSID', $true)
        if ($null -ne $clsid) {
            if ($clsid.GetSubKeyNames() -contains $clsidTeste.Trim('{', '}')) {
                $clsid.DeleteSubKeyTree($clsidTeste.Trim('{', '}'), $false)
            }
        }
        return $true
    } catch { return $false }
    finally {
        if ($null -ne $clsid) { $clsid.Close() }
        if ($null -ne $base) { $base.Close() }
    }
}

# A entrada que o teste cria (o "criar" do gerenciador): ela e um verbo
# do Explorer, e por isso aparece no botao direito enquanto existe. O
# teste apaga ela no final.
$idPersonalizado = 'TranalTesteTemporario'

function ApagarEntradaPersonalizada() {
    $base = $null
    try {
        $base = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Classes\*\shell', $true)
        if ($null -eq $base) { return $false }
        if ($base.GetSubKeyNames() -notcontains $idPersonalizado) { return $true }
        $base.DeleteSubKeyTree($idPersonalizado, $false)
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
$resumoFinal = '^[0-9]+ (entrada\(s\) de menu de contexto|context menu entry\(ies\))\.$'

function Eh-Final([string]$texto) {
    if ([string]::IsNullOrWhiteSpace($texto)) { return $false }
    if ($carregando -contains $texto) { return $false }
    return $texto -match $resumoFinal
}

# O estado das entradas do Tranal, lido da saida da linha de comando.
function Estados-Tranal([string]$texto) {
    $estados = @{}
    $id = $null
    foreach ($linha in ($texto -split "`r?`n")) {
        if ($linha -match '^\s+id:\s+(\S+)') { $id = $Matches[1] }
        elseif ($linha -match 'estado:\s+(\S+)') {
            if ($id) { $estados[$id] = $Matches[1] }
        }
    }
    return $estados
}

$settings = Join-Path $DataDirectory 'settings.json'
$log = Join-Path $DataDirectory 'erro-inicializacao.log'
$reserva = Join-Path $env:TEMP ("tranal-contextmenu-reserva-{0}.json" -f [Guid]::NewGuid().ToString('N'))

# O "restaurar" tambem grava uma copia legivel do registro em .reg, e o
# "desativar" apaga esse arquivo. Ele e guardado tambem: quem roda o teste
# nao pode perder um arquivo que ja existia.
$regScript = Join-Path $DataDirectory 'TranalContextMenu.reg'
$reservaReg = Join-Path $env:TEMP ("tranal-contextmenu-reg-{0}.reg" -f [Guid]::NewGuid().ToString('N'))
$haviaReg = Test-Path $regScript

Write-Host "=== Preparacao ==="
Write-Host "    executavel: $Exe"
Write-Host "    cli: $CliExe"

$logAntes = if (Test-Path $log) { (Get-Item $log).Length } else { 0 }

$haviaSettings = Test-Path $settings
if ($haviaSettings) {
    [System.IO.File]::Copy($settings, $reserva, $true)
    Write-Host "    settings.json guardado em $reserva"
}

if ($haviaReg) {
    [System.IO.File]::Copy($regScript, $reservaReg, $true)
    Write-Host "    TranalContextMenu.reg guardado em $reservaReg"
}

# O estado inicial das entradas do Tranal: o teste devolve a maquina a ele.
$inicial = Executar-Cli @('/contextmenu')
$estadosIniciais = Estados-Tranal $inicial.Texto
Write-Host ("    entradas do Tranal no estado inicial: {0}" -f $estadosIniciais.Count)

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

    # 1) O botao do gerenciador existe na barra lateral.
    $botao = PorId $janela 'ToolContextMenu'
    Chk "o botao do gerenciador de menu de contexto existe na barra lateral" ($null -ne $botao)

    # 2) Clicar abre a tela.
    Clicar $botao | Out-Null
    Start-Sleep -Seconds 4

    foreach ($id in @('ContextMenuRefresh', 'ContextMenuRestore', 'ContextMenuDisable', 'ContextMenuRemove')) {
        Chk ("a tela tem o comando '{0}'" -f $id) ($null -ne (PorId $janela $id))
    }

    # 3) O titulo do painel.
    $titulo = TextoDe (PorId $janela 'PanelTitle')
    $tituloOk = ($titulo -eq 'Gerenciador de menu de contexto') -or ($titulo -eq 'Context Menu Manager')
    Chk "o titulo do painel e o do gerenciador" $tituloOk "titulo='$titulo'"

    # 4) A lista existe.
    $lista = PorId $janela 'PanelItems'
    Chk "a lista do gerenciador existe" ($null -ne $lista)

    # 5) A carga acaba: a linha de status chega ao resumo.
    $limiteCarga = (Get-Date).AddSeconds($EsperaCargaSegundos)
    $status = ''
    while ((Get-Date) -lt $limiteCarga) {
        $status = TextoDe (PorId $janela 'PanelStatus')
        if (Eh-Final $status) { break }
        Start-Sleep -Milliseconds 800
    }
    Chk "o gerenciador carregou (a linha de status chegou ao resumo)" (Eh-Final $status) "status='$status'"

    # 6) As colunas e as linhas.
    $itens = 0
    $cabecalhos = @()
    if ($null -ne $lista) {
        try {
            $condItem = New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
                [System.Windows.Automation.ControlType]::DataItem)
            $achados = $lista.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condItem)
            $itens = $achados.Count

            $condHead = New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
                [System.Windows.Automation.ControlType]::HeaderItem)
            foreach ($h in $lista.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condHead)) {
                $cabecalhos += [string]$h.Current.Name
            }
        } catch { Write-Host ("    aviso: nao deu para ler os itens: " + $_.Exception.Message) }
    }
    Write-Host ("    itens na janela: {0}" -f $itens)
    Write-Host ("    colunas: {0}" -f ($cabecalhos -join ', '))

    $temColunas = ($cabecalhos -contains 'Entrada') -or ($cabecalhos -contains 'Entry')
    $temDono = ($cabecalhos -contains 'Dono') -or ($cabecalhos -contains 'Owner')
    $temImplementacao = ($cabecalhos -contains 'Implementacao') -or ($cabecalhos -contains 'Implementation')
    Chk "a tela mostra as colunas de entrada, dono e implementacao" ($temColunas -and $temDono -and $temImplementacao) `
        ("cabecalhos=" + ($cabecalhos -join ', '))

    # 7) A janela e a linha de comando mostram o mesmo numero de entradas.
    $cli = Executar-Cli @('/contextmenu')
    Chk "a linha de comando lista as entradas do menu de contexto" ($cli.Codigo -eq 0) "codigo=$($cli.Codigo)"
    Chk "a linha de comando mostra o total de entradas" ($cli.Texto -match 'Entradas de menu de contexto:\s*(\d+)') $cli.Texto

    $anunciado = 0
    if ($cli.Texto -match 'Entradas de menu de contexto:\s*(\d+)') { $anunciado = [int]$Matches[1] }
    Chk "a janela e a linha de comando mostram a mesma quantidade" ($anunciado -eq $itens) "cli=$anunciado janela=$itens"

    # 8) As entradas do Tranal aparecem pelo identificador.
    foreach ($id in @('TranalUninstaller', 'TranalInstallAndTrace', 'TranalShred', 'TranalMonitor')) {
        Chk ("a linha de comando mostra a entrada '{0}'" -f $id) ($cli.Texto -match [regex]::Escape($id))
    }

    # 9) A ajuda da CLI menciona o comando.
    $ajuda = Executar-Cli @('/?')
    Chk "a ajuda da linha de comando menciona /contextmenu" ($ajuda.Texto -match '/contextmenu')

    # 10) O catalogo de ferramentas conhece a tela.
    $painel = Executar-Cli @('/painel')
    Chk "o catalogo de ferramentas lista o gerenciador de menu de contexto" ($painel.Texto -match 'ContextMenu\s+-')

    # 11) Remover uma entrada que nao existe falha com o motivo.
    $inexistente = Executar-Cli @('/contextmenu', '/remove', 'entrada-que-nao-existe-xyz')
    Chk "remover uma entrada inexistente falha com o motivo" `
        (($inexistente.Codigo -eq 1) -and ($inexistente.Texto -match 'nao encontrada')) `
        ("codigo=" + $inexistente.Codigo)

    # 11b) Uma entrada do Windows e recusada na remocao: remover a quebraria.
    $doWindows = Executar-Cli @('/contextmenu', '/remove', 'Open With')
    Chk "remover uma entrada do Windows e recusado" `
        (($doWindows.Codigo -eq 1) -and ($doWindows.Texto -match 'do Windows')) `
        ("codigo=" + $doWindows.Codigo + " " + $doWindows.Texto)

    # 12) Uma entrada de terceiro aparece na listagem e pode ser removida.
    Chk "a chave de teste nao existia antes" (-not (ChaveTesteExiste))

    $criou = CriarChaveTeste
    Chk "a chave de teste foi criada" $criou

    $comTerceiro = Executar-Cli @('/contextmenu')
    Chk "a entrada de terceiro aparece na listagem" ($comTerceiro.Texto -match $nomeTeste) $comTerceiro.Texto

    $removeu = Executar-Cli @('/contextmenu', '/remove', $nomeTeste)
    Chk "a entrada de terceiro pode ser removida pelo nome" ($removeu.Codigo -eq 0) `
        ("codigo=" + $removeu.Codigo + " " + $removeu.Texto)
    Chk "a chave de teste saiu do registro" (-not (ChaveTesteExiste))

    # 12b) Uma entrada criada pelo gerenciador aparece e pode ser removida.
    $criouEntrada = Executar-Cli @('/contextmenu', '/create', 'Teste Temporario', 'notepad.exe')
    Chk "criar uma entrada de menu de contexto funciona" ($criouEntrada.Codigo -eq 0) `
        ("codigo=" + $criouEntrada.Codigo + " " + $criouEntrada.Texto)

    $comPersonal = Executar-Cli @('/contextmenu')
    Chk "a entrada criada aparece na listagem" ($comPersonal.Texto -match 'Teste Temporario') $comPersonal.Texto

    $removeuEntrada = Executar-Cli @('/contextmenu', '/remove', 'Teste Temporario')
    Chk "a entrada criada pode ser removida pelo nome" ($removeuEntrada.Codigo -eq 0) `
        ("codigo=" + $removeuEntrada.Codigo + " " + $removeuEntrada.Texto)

    $semPersonal = Executar-Cli @('/contextmenu')
    Chk "a entrada criada saiu da listagem" ($semPersonal.Texto -notmatch 'Teste Temporario')

    # 13) Restaurar e desativar as entradas do Tranal: ida e volta.
    $restaurar = Executar-Cli @('/contextmenu', '/restore')
    Chk "restaurar as entradas do Tranal funciona" ($restaurar.Codigo -eq 0) `
        ("codigo=" + $restaurar.Codigo + " " + $restaurar.Texto)

    $depois = Executar-Cli @('/contextmenu')
    $estadosDepois = Estados-Tranal $depois.Texto
    $ativas = 0
    foreach ($chave in $estadosDepois.Keys) { if ($estadosDepois[$chave] -eq 'ativa') { $ativas++ } }
    Chk "as entradas do Tranal ficam ativas depois de restaurar" ($ativas -ge 4) "ativas=$ativas"

    $desativar = Executar-Cli @('/contextmenu', '/disable')
    Chk "desativar as entradas do Tranal funciona" ($desativar.Codigo -eq 0) `
        ("codigo=" + $desativar.Codigo + " " + $desativar.Texto)

    $final = Executar-Cli @('/contextmenu')
    $estadosFinais = Estados-Tranal $final.Texto

    $igual = $estadosIniciais.Count -eq $estadosFinais.Count
    if ($igual) {
        foreach ($chave in $estadosIniciais.Keys) {
            if ($estadosFinais[$chave] -ne $estadosIniciais[$chave]) { $igual = $false; break }
        }
    }
    Chk "a maquina volta ao estado inicial das entradas do Tranal" $igual `
        ("inicial=" + (($estadosIniciais.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join ', '))

    $logDepois = if (Test-Path $log) { (Get-Item $log).Length } else { 0 }
    Chk "nenhuma excecao nova no log de inicializacao" ($logDepois -eq $logAntes) `
        ("antes $logAntes, depois $logDepois")
}
finally {
    if ($processo) { Stop-Process -Id $processo.Id -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Seconds 2

    # A chave de teste nao pode sobrar: o registro volta ao estado anterior.
    if (ChaveTesteExiste) {
        ApagarChaveTeste | Out-Null
        Write-Host "    chave de teste removida"
    }

    # A entrada criada pelo teste tambem nao pode sobrar.
    if (ApagarEntradaPersonalizada) {
        Write-Host "    entrada criada pelo teste removida"
    }

    if ($haviaSettings -and (Test-Path $reserva)) {
        [System.IO.File]::Copy($reserva, $settings, $true)
        Write-Host "    settings.json original devolvido"
    }

    if ($haviaReg) {
        if (Test-Path $reservaReg) {
            [System.IO.File]::Copy($reservaReg, $regScript, $true)
            Write-Host "    TranalContextMenu.reg original devolvido"
        }
    }
    elseif (Test-Path $regScript) {
        # O .reg foi criado pelo "restaurar" do teste e nao existia antes:
        # ele nao pode sobrar.
        [System.IO.File]::Delete($regScript)
        Write-Host "    TranalContextMenu.reg criado pelo teste removido"
    }
}

Write-Host ""
if ($falhas -eq 0) {
    Write-Host "RESULTADO: gerenciador de menu de contexto OK."
    exit 0
}
Write-Host "RESULTADO: $falhas verificacao(oes) falharam."
exit 1
