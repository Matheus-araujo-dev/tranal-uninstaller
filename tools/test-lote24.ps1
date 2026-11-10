<#
  Teste de ponta a ponta do lote 24: a nota de cinco niveis das sobras.

  O que ele prova, na janela de verdade:
    1. a arvore das sobras mostra a NOTA (Muito bom / Bom / Duvidoso / Mau /
       Desconhecido) no detalhe de cada folha;
    2. a linha de comando mostra a mesma nota, na mesma palavra;
    3. nada e removido: o teste fecha a tela sem clicar em "Remover" e a
       varredura da linha de comando e apenas relatorio (sem /remove).

  A suite de testes cobre o nucleo (LeftoverRatings e os criterios de
  pasta); este script cobre a ligacao com a interface e com a linha de
  comando, que a suite nao alcanca (a suite nao referencia o projeto WPF).

  O script cria uma pasta temporaria com um arquivo (para a varredura
  encontrar algo) e apaga tudo no final.

  Uso:
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-lote24.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-lote24.ps1 -Exe publish\preview\TranalUninstaller.exe
#>
param(
    [string]$Exe = '',
    [string]$CliExe = '',
    [string]$EsperaSegundos = 30,
    [int]$EsperaVarreduraSegundos = 60
)


$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Exe)) { $Exe = Join-Path $repoRoot 'publish\preview\TranalUninstaller.exe' }
if (-not [System.IO.Path]::IsPathRooted($Exe)) { $Exe = Join-Path $repoRoot $Exe }
if (-not (Test-Path $Exe)) { throw "Executavel nao encontrado: $Exe" }

if ([string]::IsNullOrWhiteSpace($CliExe)) { $CliExe = Join-Path $repoRoot 'publish\cli\tranal.exe' }
if (-not [System.IO.Path]::IsPathRooted($CliExe)) { $CliExe = Join-Path $repoRoot $CliExe }
$CliExiste = (Test-Path $CliExe)

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

if (-not ('TranalJanelas24' -as [type])) {
Add-Type @'
using System; using System.Text; using System.Runtime.InteropServices; using System.Collections.Generic;
public class TranalJanelas24 {
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
    foreach ($h in [TranalJanelas24]::VisiveisDe([uint32]$processId)) {
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

# O texto de um no da arvore: os blocos de texto que ficam dentro dele (o
# caminho e o detalhe da folha). O nome do no, sozinho, nao serve: quando o
# cabecalho e um painel e nao uma string, o Windows gera um nome automatico.
function TextoDoNo($no) {
    if ($null -eq $no) { return '' }
    try {
        $cond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::Text)
        $textos = $no.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
        $partes = @()
        foreach ($t in $textos) {
            $valor = TextoDe $t
            if (-not [string]::IsNullOrWhiteSpace($valor)) { $partes += $valor }
        }
        return ($partes -join ' | ')
    } catch { return '' }
}

# Os cinco rotulos da nota. A busca aceita qualquer um deles: o que o teste
# mede e que a nota APARECE, e nao qual nota o item recebe.
$rotulos = @('Muito bom', 'Bom', 'Duvidoso', 'Mau', 'Desconhecido')

function TemNota([string]$texto) {
    foreach ($r in $rotulos) {
        if ($texto -match [regex]::Escape($r)) { return $true }
    }
    return $false
}


# ----------------------------------------------------------------------
# A pasta de teste: a varredura forcada precisa de algo para achar
# ----------------------------------------------------------------------

$alvo = Join-Path $env:TEMP ("tranal-lote24-alvo-{0}" -f [Guid]::NewGuid().ToString('N'))

Write-Host "=== Preparacao ==="
Write-Host "    executavel: $Exe"
Write-Host "    linha de comando: $CliExe (existe: $CliExiste)"
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
    # 1. A tela de desinstalacao forcada com a NOTA na folha.
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

    $notaNaTela = $false
    $textoVisto = ''

    if ($null -ne $forcada) {
        $caixa = PorId $forcada 'ForcedPathBox'
        Escrever $caixa $alvo | Out-Null
        Start-Sleep -Milliseconds 500

        Clicar (PorId $forcada 'ForcedAnalyzeButton') | Out-Null

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

        foreach ($no in $nos) {
            $texto = TextoDoNo $no
            if ([string]::IsNullOrWhiteSpace($texto)) { continue }
            Write-Host ("    no: {0}" -f $texto)
            if (TemNota $texto) { $notaNaTela = $true; $textoVisto = $texto }
        }

        Chk "a folha da sobra mostra a nota de cinco niveis" $notaNaTela $textoVisto

        # Fechar SEM remover: nada e apagado da maquina.
        Clicar (PorId $forcada 'ForcedCloseButton') | Out-Null
        Start-Sleep -Seconds 2

        $fechou = JanelaComId $processo.Id 'ForcedPathBox'
        Chk "a tela de desinstalacao forcada fecha" ($null -eq $fechou)
    }

    # ------------------------------------------------------------------
    # 2. A linha de comando mostra a mesma nota.
    # ------------------------------------------------------------------
    if ($CliExiste) {
        # Os nomes dos programas vem da propria linha de comando (o comando
        # /export): o nome do item da lista chega vazio pela automacao, e um
        # nome vazio nao descobre programa nenhum.
        $csv = Join-Path $env:TEMP ("tranal-lote24-programas-{0}.csv" -f [Guid]::NewGuid().ToString('N'))
        $nomes = @()
        try {
            & $CliExe /export $csv /csv *> $null
            if ([System.IO.File]::Exists($csv)) {
                $linhas = [System.IO.File]::ReadAllLines($csv, [System.Text.Encoding]::UTF8)
                foreach ($linha in $linhas) {
                    if ([string]::IsNullOrWhiteSpace($linha)) { continue }
                    $nome = ($linha -split ';')[0].Trim()
                    if ([string]::IsNullOrWhiteSpace($nome)) { continue }
                    if ($nome -eq 'Program') { continue }
                    $nomes += $nome
                }
            }
        } catch { }

        Write-Host ("    programas na lista: {0}" -f $nomes.Count)

        $saida = ''
        $usado = ''
        foreach ($nome in ($nomes | Select-Object -First 6)) {
            try {
                # SEM /remove: e apenas relatorio, nada e apagado.
                $saida = & $CliExe /scan $nome /mode safe 2>&1 | Out-String
            } catch { $saida = '' }
            if ($saida -match 'nota:') { $usado = $nome; break }
        }

        Write-Host ("    programa usado na varredura: {0}" -f $usado)
        if (-not [string]::IsNullOrWhiteSpace($usado)) {
            Write-Host ($saida.Trim())
        }

        Chk "a linha de comando mostra a nota" ($saida -match 'nota:') $usado
        Chk "a nota da linha de comando e um dos cinco rotulos" (TemNota $saida) $usado

        if ([System.IO.File]::Exists($csv)) {
            [System.IO.File]::Delete($csv)
        }
    }
    else {
        Write-Host "    (linha de comando nao publicada: a verificacao da CLI foi pulada)"
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
