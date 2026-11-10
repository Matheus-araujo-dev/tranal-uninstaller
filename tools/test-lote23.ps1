<#
  Teste de ponta a ponta do lote 23: a tela de sobras em arvore, com as
  categorias (e a contagem), a busca dentro das sobras e o comando de
  explorar.

  O que ele prova, na janela de verdade:
    1. a desinstalacao forcada mostra as sobras em arvore, agrupadas por
       categoria (o "File System (%d items)" e o "Registry (%d items)" do
       Geek);
    2. a busca dentro das sobras filtra o que a arvore mostra;
    3. nada e removido: o teste fecha a tela sem clicking em "Remover".

  A suite de testes cobre o nucleo (LeftoverGroups); este script cobre a
  ligacao com a interface, que a suite nao alcanca (a suite nao referencia
  o projeto WPF).

  O script cria uma pasta temporaria com um arquivo (para a varredura
  encontrar algo) e apaga tudo no final.

  Uso:
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-lote23.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-lote23.ps1 -Exe publish\preview\TranalUninstaller.exe
#>
param(
    [string]$Exe = '',
    [string]$EsperaSegundos = 30,
    [int]$EsperaVarreduraSegundos = 60
)


$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Exe)) { $Exe = Join-Path $repoRoot 'publish\preview\TranalUninstaller.exe' }
if (-not [System.IO.Path]::IsPathRooted($Exe)) { $Exe = Join-Path $repoRoot $Exe }
if (-not (Test-Path $Exe)) { throw "Executavel nao encontrado: $Exe" }

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

if (-not ('TranalJanelas23' -as [type])) {
Add-Type @'
using System; using System.Text; using System.Runtime.InteropServices; using System.Collections.Generic;
public class TranalJanelas23 {
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

function JanelaComId([int]$processId, [string]$automationId) {
    foreach ($h in [TranalJanelas23]::VisiveisDe([uint32]$processId)) {
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

function NosDaArvore($arvore) {
    if ($null -eq $arvore) { return @() }
    try {
        $cond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::TreeItem)
        return $arvore.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
    } catch { return @() }
}


# ----------------------------------------------------------------------
# A pasta de teste: a varredura forcada precisa de algo para achar
# ----------------------------------------------------------------------

$alvo = Join-Path $env:TEMP ("tranal-lote23-alvo-{0}" -f [Guid]::NewGuid().ToString('N'))

Write-Host "=== Preparacao ==="
Write-Host "    executavel: $Exe"
Write-Host "    alvo da varredura: $alvo"

[System.IO.Directory]::CreateDirectory($alvo) | Out-Null
[System.IO.File]::WriteAllText((Join-Path $alvo 'config.ini'), 'x')

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
        $janela = JanelaComId $processo.Id 'ProgramStatusText'
    }

    Chk "a janela principal do Tranal aparece" ($null -ne $janela)
    if (-not $janela) { throw "A janela principal nao apareceu: o teste nao tem como continuar." }

    # ------------------------------------------------------------------
    # 1. A tela de desinstalacao forcada.
    # ------------------------------------------------------------------
    $botao = PorId $janela 'CmdForcedUninstall'
    Chk "o botao da desinstalacao forcada existe" ($null -ne $botao)

    Clicar $botao | Out-Null

    $forcada = $null
    $limite2 = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $limite2 -and -not $forcada) {
        Start-Sleep -Milliseconds 500
        $forcada = JanelaComId $processo.Id 'ForcedPathBox'
    }
    Chk "a tela de desinstalacao forcada abre" ($null -ne $forcada)

    if ($null -ne $forcada) {
        $caixa = PorId $forcada 'ForcedPathBox'
        Escrever $caixa $alvo | Out-Null
        Start-Sleep -Milliseconds 500

        Clicar (PorId $forcada 'ForcedAnalyzeButton') | Out-Null

        # A varredura leva um tempo: o teste espera a arvore aparecer.
        $arvore = $null
        $limite3 = (Get-Date).AddSeconds($EsperaVarreduraSegundos)
        while ((Get-Date) -lt $limite3) {
            Start-Sleep -Milliseconds 700
            $arvore = PorId $forcada 'LeftoversTree'
            if ($null -ne $arvore -and (NosDaArvore $arvore).Count -gt 0) { break }
        }

        Chk "a tela mostra a arvore das sobras" ($null -ne $arvore)

        $nos = NosDaArvore $arvore
        Write-Host ("    nos na arvore: {0}" -f $nos.Count)

        $categorias = @()
        foreach ($no in $nos) { $categorias += (TextoDe $no) }
        Write-Host ("    nomes: {0}" -f ($categorias -join ' | '))

        $temGrupo = $false
        foreach ($nome in $categorias) {
            if ($nome -match '\(\d+\)') { $temGrupo = $true }
        }
        Chk "a arvore mostra a categoria com a contagem de itens" $temGrupo ($categorias -join ' | ')

        # ------------------------------------------------------------------
        # 2. A busca dentro das sobras.
        # ------------------------------------------------------------------
        $busca = PorId $forcada 'LeftoversSearchBox'
        Chk "a arvore tem a busca" ($null -ne $busca)

        if ($null -ne $busca) {
            Escrever $busca 'nao-existe-xyz-123' | Out-Null
            Start-Sleep -Seconds 2

            $depois = (NosDaArvore $arvore).Count
            Chk "a busca que nao casa esvazia a arvore" ($depois -eq 0) "nos=$depois"

            Escrever $busca '' | Out-Null
            Start-Sleep -Seconds 2

            $voltou = (NosDaArvore $arvore).Count
            Chk "a busca apagada devolve os itens" ($voltou -gt 0) "nos=$voltou"
        }

        # O comando de explorar existe (e so vale com um item selecionado).
        $explorar = PorId $forcada 'LeftoversBrowseButton'
        Chk "a arvore tem o comando de explorar" ($null -ne $explorar)

        # Fechar SEM remover: nada e apagado da maquina.
        Clicar (PorId $forcada 'ForcedCloseButton') | Out-Null
        Start-Sleep -Seconds 2

        $fechou = JanelaComId $processo.Id 'ForcedPathBox'
        Chk "a tela de desinstalacao forcada fecha" ($null -eq $fechou)
    }
}
finally {
    Write-Host ""
    Write-Host "=== Limpeza ==="

    if ([System.IO.Directory]::Exists($alvo)) {
        [System.IO.Directory]::Delete($alvo, $true)
        Write-Host "    pasta de teste removida"
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
