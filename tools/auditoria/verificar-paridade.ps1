<#
.SYNOPSIS
    Confere a paridade do Tranal com o Revo Uninstaller Pro 5.5.4 contra o codigo.

.DESCRIPTION
    A tabela da secao 7.1 do docs\REVO_SPEC.md é gerada a partir de
    docs\auditoria\paridade-revo.json. Cada linha do dado traz a EVIDENCIA
    (arquivo::simbolo) que sustenta o estado declarado.

    Este script confere, linha a linha, se a evidencia continua existindo no
    codigo. Uma linha que declara "OK" mas aponta para um arquivo ou simbolo
    que nao existe esta ERRADA - e isso e falha do verificador (codigo de
    saida 1).

    Regra da auditoria: "Medido vale. Inferido nao vale." A tabela so pode
    afirmar o que a evidencia prova.

.PARAMETER Gerar
    Reescreve a tabela da secao 7.1 do REVO_SPEC.md a partir do JSON.

.PARAMETER Repo
    Raiz do repositorio. Padrao: a pasta que contem este script (../..).

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\auditoria\verificar-paridade.ps1

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\auditoria\verificar-paridade.ps1 -Gerar
#>
[CmdletBinding()]
param(
    [switch]$Gerar,
    [string]$Repo
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($Repo)) {
    $Repo = (Resolve-Path (Join-Path $scriptDir '..\..')).Path
}

$dados = Join-Path $Repo 'docs\auditoria\paridade-revo.json'
$spec = Join-Path $Repo 'docs\REVO_SPEC.md'

if (-not (Test-Path -LiteralPath $dados)) {
    Write-Error "Dado de paridade nao encontrado: $dados"
    exit 2
}

$json = Get-Content -LiteralPath $dados -Raw -Encoding UTF8 | ConvertFrom-Json
$linhas = @($json.linhas)

function Resolver-Caminho {
    param([string]$Relativo)

    # Caminhos relativos a raiz do projeto (src/Tranal.Uninstaller/...).
    $candidatos = @(
        (Join-Path $Repo $Relativo),
        (Join-Path $Repo "src\Tranal.Uninstaller\$Relativo")
    )

    foreach ($c in $candidatos) {
        if (Test-Path -LiteralPath $c) { return $c }
    }

    return $null
}

function Testar-Simbolo {
    param([string]$Arquivo, [string]$Simbolo)

    if ([string]::IsNullOrWhiteSpace($Simbolo)) { return $true }

    $texto = Get-Content -LiteralPath $Arquivo -Raw -Encoding UTF8
    return $texto.Contains($Simbolo)
}

$quebradas = New-Object System.Collections.Generic.List[string]
$resumo = @{}

foreach ($linha in $linhas) {
    $status = [string]$linha.status
    if (-not $resumo.ContainsKey($status)) { $resumo[$status] = 0 }
    $resumo[$status]++

    foreach ($ev in @($linha.evidencia)) {
        $relativo = ([string]$ev)
        $simbolo = ''
        if ($relativo.Contains('::')) {
            $partes = $relativo -split '::', 2
            $relativo = $partes[0]
            $simbolo = $partes[1]
        }

        $arquivo = Resolver-Caminho -Relativo $relativo
        if ($null -eq $arquivo) {
            $quebradas.Add("linha $($linha.id): arquivo inexistente -> $relativo")
            continue
        }

        if (-not (Testar-Simbolo -Arquivo $arquivo -Simbolo $simbolo)) {
            $quebradas.Add("linha $($linha.id): simbolo ausente -> $relativo::$simbolo")
        }
    }
}

Write-Output "Paridade Tranal x Revo Pro 5.5.4 — $($linhas.Count) funcionalidades"
foreach ($k in ($resumo.Keys | Sort-Object)) {
    Write-Output ("  {0,-8} {1}" -f $k, $resumo[$k])
}

if ($quebradas.Count -gt 0) {
    Write-Output ''
    Write-Output "EVIDENCIA QUEBRADA ($($quebradas.Count)):"
    foreach ($q in $quebradas) { Write-Output "  $q" }
}

if ($Gerar) {
    if (-not (Test-Path -LiteralPath $spec)) {
        Write-Error "REVO_SPEC.md nao encontrado: $spec"
        exit 2
    }

    $marcadorInicio = '<!-- PARIDADE-REVO:INICIO -->'
    $marcadorFim = '<!-- PARIDADE-REVO:FIM -->'

    $texto = Get-Content -LiteralPath $spec -Raw -Encoding UTF8
    $ini = $texto.IndexOf($marcadorInicio)
    $fim = $texto.IndexOf($marcadorFim)

    if ($ini -lt 0 -or $fim -lt 0 -or $fim -lt $ini) {
        Write-Error "Marcadores $marcadorInicio / $marcadorFim nao encontrados no REVO_SPEC.md"
        exit 2
    }

    $tabela = New-Object System.Collections.Generic.List[string]
    $tabela.Add('| # | Prio | Funcionalidade (Revo Pro 5.5.4) | Evidencia no Revo | Estado do Tranal | Evidencia no codigo do Tranal |')
    $tabela.Add('|---|---|---|---|---|---|')

    foreach ($linha in $linhas) {
        $evidencias = @($linha.evidencia) -join '<br>'
        $nota = [string]$linha.nota
        if (-not [string]::IsNullOrWhiteSpace($nota)) {
            $evidencias = "$evidencias<br>$nota"
        }

        $func = ([string]$linha.funcionalidade) -replace '\|', '\|'
        $revo = ([string]$linha.revo) -replace '\|', '\|'
        $tabela.Add("| $($linha.id) | $($linha.prioridade) | $func | $revo | **$($linha.status)** | $evidencias |")
    }

    $novo = $texto.Substring(0, $ini + $marcadorInicio.Length) + "`r`n" +
            ($tabela -join "`r`n") + "`r`n" +
            $texto.Substring($fim)

    [System.IO.File]::WriteAllText($spec, $novo, (New-Object System.Text.UTF8Encoding($false)))
    Write-Output ''
    Write-Output "Tabela da secao 7.1 regerada em docs\REVO_SPEC.md"
}

if ($quebradas.Count -gt 0) { exit 1 }
exit 0
