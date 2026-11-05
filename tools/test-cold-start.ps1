<#
  Teste de ponta a ponta da partida a frio.

  O que ele prova: sem o arquivo de preferencias (settings.json), o Tranal
  Uninstaller Pro ABRE, pergunta o tema, grava a preferencia e mostra a
  janela principal.

  Por que existe: dois defeitos de ordenacao na inicializacao so apareciam
  na primeira execucao de uma maquina (o "Checked" do RadioButton disparando
  durante o InitializeComponent, e o Owner apontando para uma janela que
  ainda nao tinha sido exibida). Nenhum dos dois aparece no caminho normal,
  e nenhum e coberto por teste de unidade: a suite nao referencia o projeto
  WPF.

  Uso:
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-cold-start.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-cold-start.ps1 -Exe publish\gui\TranalUninstaller.exe

  O script nao mexe no ambiente de quem roda: o settings.json existente e
  guardado antes e devolvido no final, mesmo se o teste falhar.
#>
param(
    [string]$Exe = '',
    [string]$DataDirectory = '',
    [int]$EsperaSegundos = 12
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Exe)) { $Exe = Join-Path $repoRoot 'publish\preview\TranalUninstaller.exe' }
if (-not [System.IO.Path]::IsPathRooted($Exe)) { $Exe = Join-Path $repoRoot $Exe }

if ([string]::IsNullOrWhiteSpace($DataDirectory)) {
    $DataDirectory = Join-Path $env:LOCALAPPDATA 'Tranal Group\Tranal Uninstaller Pro'
}

if (-not (Test-Path $Exe)) { throw "Executavel nao encontrado: $Exe" }

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

if (-not ('TranalJanelas' -as [type])) {
Add-Type @'
using System; using System.Text; using System.Runtime.InteropServices; using System.Collections.Generic;
public class TranalJanelas {
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
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
  public static string Texto(IntPtr h){ int n=GetWindowTextLength(h); if(n==0) return ""; var sb=new StringBuilder(n+2); GetWindowText(h,sb,sb.Capacity); return sb.ToString(); }
}
'@
}

$falhas = 0
function Chk([string]$nome, [bool]$ok, [string]$detalhe = '') {
    if ($ok) { Write-Host "[OK]    $nome" }
    else { Write-Host "[FALHA] $nome $detalhe"; $script:falhas++ }
}

function JanelasVisiveis([int]$processId) {
    $r = @()
    foreach ($h in [TranalJanelas]::VisiveisDe([uint32]$processId)) {
        $r += [pscustomobject]@{ Hwnd = $h; Titulo = [TranalJanelas]::Texto($h) }
    }
    return $r
}

function EncontrarPorNome([int]$processId, [string]$nome) {
    try {
        $cond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty, $nome)
        $achados = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
            [System.Windows.Automation.TreeScope]::Descendants, $cond)
        foreach ($e in $achados) {
            try { if ($e.Current.ProcessId -eq $processId) { return $e } } catch { }
        }
    } catch { }
    return $null
}

$settings = Join-Path $DataDirectory 'settings.json'
$log = Join-Path $DataDirectory 'erro-inicializacao.log'
$reserva = Join-Path $env:TEMP ("tranal-settings-reserva-{0}.json" -f [Guid]::NewGuid().ToString('N'))

Write-Host "=== Preparacao (partida a frio) ==="
Write-Host "    executavel: $Exe"
Write-Host "    dados:      $DataDirectory"

$logAntes = if (Test-Path $log) { (Get-Item $log).Length } else { 0 }
Write-Host ("    log de erro antes: {0} bytes" -f $logAntes)

$haviaSettings = Test-Path $settings
if ($haviaSettings) {
    # Guarda as preferencias de quem esta rodando e devolve no final.
    [System.IO.File]::Copy($settings, $reserva, $true)
    Move-Item $settings $reserva -Force
    Write-Host "    settings.json guardado em $reserva"
}

$processo = $null
try {
    Write-Host ""
    Write-Host "=== Execucao ==="
    $processo = Start-Process -FilePath $Exe -PassThru
    Write-Host ("    pid: {0}" -f $processo.Id)

    $limite = (Get-Date).AddSeconds($EsperaSegundos)
    $dialogo = $null
    while ((Get-Date) -lt $limite -and -not $dialogo) {
        Start-Sleep -Milliseconds 700
        if (-not (Get-Process -Id $processo.Id -ErrorAction SilentlyContinue)) { break }
        $dialogo = JanelasVisiveis $processo.Id |
            Where-Object { $_.Titulo -like 'Op*' } |
            Select-Object -First 1
    }

    Chk "o aplicativo continua vivo" ($null -ne (Get-Process -Id $processo.Id -ErrorAction SilentlyContinue))
    Chk "a janela de primeiro lancamento aparece" ($null -ne $dialogo) `
        ("janelas: " + ((JanelasVisiveis $processo.Id | ForEach-Object { $_.Titulo }) -join ' | '))

    # Confirma o tema: e o que grava o settings.json.
    $ok = EncontrarPorNome $processo.Id 'OK'
    if ($null -eq $ok) {
        Chk "o botao OK existe" $false
    } else {
        $ok.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        Write-Host "    OK clicado"
        Start-Sleep -Seconds 5
    }

    $principal = JanelasVisiveis $processo.Id |
        Where-Object { $_.Titulo -like '*Tranal*' } |
        Select-Object -First 1
    Chk "a janela principal do Tranal aparece" ($null -ne $principal)

    if ($principal) {
        $root = [System.Windows.Automation.AutomationElement]::FromHandle($principal.Hwnd)
        $descendants = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
                                     [System.Windows.Automation.Condition]::TrueCondition)
        Chk "a janela principal esta populada" ($descendants.Count -gt 10) `
            ("elementos: " + $descendants.Count)
    }

    Chk "o settings.json foi criado" (Test-Path $settings)
    if (Test-Path $settings) {
        $txt = [System.IO.File]::ReadAllText($settings)
        Chk "a preferencia FirstRunCompleted foi gravada" `
            ($txt -match '"FirstRunCompleted"\s*:\s*true')
    }

    $logDepois = if (Test-Path $log) { (Get-Item $log).Length } else { 0 }
    Chk "nenhuma excecao nova no log de inicializacao" ($logDepois -eq $logAntes) `
        ("antes $logAntes, depois $logDepois")
}
finally {
    if ($processo) { Stop-Process -Id $processo.Id -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Seconds 2

    if ($haviaSettings) {
        # Devolve as preferencias de quem rodou o teste.
        if (Test-Path $reserva) { Move-Item $reserva $settings -Force }
        Write-Host "    settings.json original devolvido"
    } elseif (Test-Path $settings) {
        # A partida a frio criou o arquivo: ele nao pertence a ninguem.
        Move-Item $settings ($reserva + '.criado') -Force
        Write-Host "    settings.json criado pelo teste foi removido"
    }
}

Write-Host ""
if ($falhas -eq 0) {
    Write-Host "RESULTADO: partida a frio OK."
    exit 0
}
Write-Host "RESULTADO: $falhas verificacao(oes) falharam."
exit 1
