<#
  Teste de ponta a ponta do lote 28: as marcas do programa (32 bits,
  atualizacao, orfao, invalido, protegido).

  O que ele prova:
    1. a linha de comando exporta a coluna "Marcas" com a marca medida de
       cada programa;
    2. a linha de comando recusa um nome de coluna que nao existe;
    3. a janela de colunas da tela oferece a coluna "Marcas";
    4. o nome exibido na lista carrega a marca de 32 bits, e a comparacao
       com a linha de comando e feita programa por programa.

  Nenhum software e instalado, removido ou tocado: o teste so le a lista.

  Uso:
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-lote28.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-lote28.ps1 -Exe publish\preview\TranalUninstaller.exe -CliExe publish\cli\tranal.exe
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

# Todos os descendentes de um tipo de controle.
function DoTipo($janela, [string]$tipo) {
    if ($null -eq $janela) { return @() }
    try {
        $cond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::$tipo)
        return $janela.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
    } catch { return @() }
}

# Os descendentes de um de varios tipos de controle. O WPF expoe uma lista
# com coluna como DataGrid (itens como DataItem) e uma lista simples como
# ListItem: os dois contam.
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

# A janela de colunas e FILHA da janela principal (ela tem Owner), entao a
# automacao nao a lista entre as janelas de topo. A busca e feita pelo titulo,
# entre as janelas do processo.
#
# E a janela e ampliada porque a lista e virtualizada: sem espaco para
# todos os itens, a automacao so enxerga os que cabem na tela.
function AmpliaJanelaDeColunas([int]$processId, [string]$titulo, [int]$altura) {
    try {
        if (-not ('JanelaColunas' -as [type])) {
            Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class JanelaColunas
{
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [StructLayout(LayoutKind.Sequential)]
    public struct RETANGULO { public int Esquerda, Topo, Direita, Base; }

    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RETANGULO r);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);

    public static long Acha(string titulo, uint processo)
    {
        long achado = 0;
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            uint pid;
            GetWindowThreadProcessId(h, out pid);
            if (pid != processo) return true;
            if (!IsWindowVisible(h)) return true;

            int n = GetWindowTextLength(h);
            var sb = new StringBuilder(n + 1);
            GetWindowText(h, sb, sb.Capacity);
            if (sb.ToString() != titulo) return true;

            achado = h.ToInt64();
            return false;
        }, IntPtr.Zero);
        return achado;
    }

    public static bool Redimensiona(long hwnd, int altura)
    {
        var h = new IntPtr(hwnd);
        RETANGULO r;
        GetWindowRect(h, out r);
        int largura = r.Direita - r.Esquerda;
        if (largura <= 0) largura = 380;

        // SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOMOVE: muda so o tamanho.
        return SetWindowPos(h, IntPtr.Zero, 0, 0, largura, altura, 0x0004 | 0x0010 | 0x0002);
    }
}
'@
        }

        $hwnd = [JanelaColunas]::Acha($titulo, [uint32]$processId)
        if ($hwnd -eq 0) { return $false }

        return [JanelaColunas]::Redimensiona($hwnd, $altura)
    } catch { return $false }
}

$processo = $null
$arquivoCsv = Join-Path $env:TEMP ('marcas-lote28-' + [Guid]::NewGuid().ToString('N') + '.csv')

try {
    Write-Host "=== Preparacao ==="
    Write-Host "    executavel: $Exe (existe: $ExeExiste)"
    Write-Host "    linha de comando: $CliExe (existe: $CliExiste)"

    # ------------------------------------------------------------------
    # 1. A linha de comando: a coluna "Marcas".
    # ------------------------------------------------------------------
    $marcasNaCli = 0
    $bits32NaCli = 0
    $bits32PorNome = @{}

    if ($CliExiste) {
        Write-Host ""
        Write-Host "=== Execucao ==="

        & $CliExe /export $arquivoCsv /csv /columns "Program,Type,Marks" *> $null

        Chk "a linha de comando gera o arquivo da lista" (Test-Path $arquivoCsv)

        if (Test-Path $arquivoCsv) {
            $linhas = @(Get-Content $arquivoCsv -Encoding UTF8)

            Chk "o arquivo tem o cabecalho com a coluna Marcas" ($linhas.Count -gt 0 -and $linhas[0] -match 'Marks')

            $marcasNaCli = @($linhas | Select-Object -Skip 1 | Where-Object { $_ -match ';' -and ($_ -split ';')[-1].Trim().Length -gt 0 }).Count
            $bits32NaCli = @($linhas | Select-Object -Skip 1 | Where-Object { $_ -match ';32-bits;' }).Count

            foreach ($linha in ($linhas | Select-Object -Skip 1)) {
                $celulas = $linha -split ';'
                if ($celulas.Length -lt 3) { continue }
                if ($celulas[1].Trim() -ne '32-bits') { continue }
                $bits32PorNome[$celulas[0].Trim()] = $true
            }

            Write-Host ("    programas com marca (linha de comando): {0}" -f $marcasNaCli)
            Write-Host ("    programas de 32 bits (linha de comando): {0}" -f $bits32NaCli)

            Chk "a coluna Marcas traz a marca de algum programa" ($marcasNaCli -gt 0)
        }

        # Uma coluna que nao existe tem que ser recusada: senão a exportacao
        # sairia com o nome errado no cabecalho.
        #
        # A recusa sai no stderr, e o stderr de um programa nativo aborta o
        # script quando o ErrorActionPreference e 'Stop'. Por isso a saida e
        # capturada dentro de um try: o que se quer medir e a recusa.
        $saida = ''
        $codigo = 0
        try {
            $saida = & $CliExe /export (Join-Path $env:TEMP 'nao-existe-lote28.csv') /csv /columns "ColunaQueNaoExiste" 2>&1 | Out-String
            $codigo = $LASTEXITCODE
        } catch {
            $saida = [string]$_.Exception.Message
        }

        Write-Host ("    recusa da coluna invalida: " + ($saida -replace '\s+', ' ').Trim())
        Chk "a linha de comando recusa uma coluna que nao existe" ($saida -match 'ColunaQueNaoExiste' -or $codigo -ne 0)
    }
    else {
        Write-Host "    (linha de comando nao publicada: a verificacao da CLI foi pulada)"
    }

    # ------------------------------------------------------------------
    # 2. A tela: a janela de colunas e o nome exibido.
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
            Clicar (PorId $principal 'CmdMainMenu') | Out-Null
            Start-Sleep -Seconds 1

            $itemColunas = ItemDeMenu $processo.Id 'CmdColumns'
            Chk "o menu tem a escolha de colunas" ($null -ne $itemColunas)

            if ($null -ne $itemColunas) {
                Clicar $itemColunas | Out-Null
                Start-Sleep -Seconds 2

                # A janela de colunas e filha da principal: a lista e
                # procurada dentro dela.
                $listaColunas = PorId $principal 'ColumnsList'
                Chk "a janela de colunas abre" ($null -ne $listaColunas)

                if ($null -ne $listaColunas) {
                    # A lista e virtualizada: a automacao so enxerga os
                    # itens que cabem na tela. A janela e ampliada para
                    # todas as colunas ficarem visiveis.
                    AmpliaJanelaDeColunas $processo.Id 'Colunas' 1600 | Out-Null
                    Start-Sleep -Seconds 1

                    $nomes = @()
                    foreach ($item in DoTipoOu $listaColunas @('ListItem', 'DataItem')) {
                        $texto = TextoDoItem $item
                        if ($texto.Length -gt 0 -and $nomes -notcontains $texto) { $nomes += $texto }
                    }

                    Write-Host ("    colunas na janela: {0}" -f $nomes.Count)
                    Write-Host ("    colunas: " + (($nomes -join ' | ')))
                    Chk "a janela de colunas oferece a coluna Marcas" (($nomes -join ' | ') -match 'Marcas')

                    # Fecha a janela sem mudar nada.
                    Clicar (PorId $principal 'ColumnsCancel') | Out-Null
                    Start-Sleep -Seconds 1
                }
            }

            # A lista de programas mora na visao "Todos os Programas".
            Clicar (PorId $principal 'ModuleAllPrograms') | Out-Null
            Start-Sleep -Seconds 2

            $lista = PorId $principal 'ProgramsList'
            Chk "a lista de programas existe" ($null -ne $lista)

            $limiteLista = (Get-Date).AddSeconds($EsperaSegundos)
            $nomesDaLista = @()
            while ((Get-Date) -lt $limiteLista) {
                Start-Sleep -Milliseconds 900
                if ($null -eq $lista) { $lista = PorId $principal 'ProgramsList' }
                if ($null -eq $lista) { continue }

                $itens = DoTipoOu $lista @('ListItem', 'DataItem')
                if ($itens.Count -gt 0) {
                    $nomesDaLista = @()
                    foreach ($item in $itens) { $nomesDaLista += TextoDoItem $item }
                    break
                }
            }

            $comMarca = @($nomesDaLista | Where-Object { $_ -match '\(32-bit\)' }).Count
            Write-Host ("    programas na lista: {0}; com a marca de 32 bits: {1}" -f $nomesDaLista.Count, $comMarca)

            Chk "a lista carregou os programas" ($nomesDaLista.Count -gt 0)

            # A comparacao e POR PROGRAMA: a lista so materializa os itens
            # visiveis, entao o que vale e cada nome que aparece na tela
            # carregar a marca que a linha de comando mediu para ele.
            $conferidos = 0
            $divergentes = 0
            foreach ($nome in $nomesDaLista) {
                foreach ($par in $bits32PorNome.GetEnumerator()) {
                    # O item da lista carrega varios textos: o nome do programa
                    # esta no comeco dele.
                    if (-not $nome.StartsWith($par.Key, [System.StringComparison]::OrdinalIgnoreCase)) { continue }

                    $conferidos++
                    if ($nome -notmatch '\(32-bit\)') { $divergentes++ }
                    break
                }
            }

            Write-Host ("    nomes conferidos contra a linha de comando: {0}; divergentes: {1}" -f $conferidos, $divergentes)

            if ($conferidos -gt 0) {
                Chk "o nome exibido carrega a marca de 32 bits" ($divergentes -eq 0)
            }
            else {
                Write-Host "    (nenhum programa de 32 bits visivel nesta tela: a comparacao da marca nao se aplica)"
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

    if ([System.IO.File]::Exists($arquivoCsv)) { [System.IO.File]::Delete($arquivoCsv) }

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
