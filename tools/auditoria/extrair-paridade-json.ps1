<#
.SYNOPSIS
    Extrai a tabela 7.1 do REVO_SPEC.md para docs\auditoria\paridade-revo.json.

.DESCRIPTION
    Ponto de partida da reconciliacao: le a tabela escrita a mao e produz o
    dado estruturado. O estado de cada linha depois e corrigido a mao contra o
    codigo (a evidencia manda) e o verificador
    (tools\auditoria\verificar-paridade.ps1) passa a checar tudo.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\auditoria\extrair-paridade-json.ps1
#>
[CmdletBinding()]
param(
    [string]$Repo
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($Repo)) {
    $Repo = (Resolve-Path (Join-Path $scriptDir '..\..')).Path
}

$spec = Join-Path $Repo 'docs\REVO_SPEC.md'
$saida = Join-Path $Repo 'docs\auditoria\paridade-revo.json'

$linhas = Get-Content -LiteralPath $spec -Encoding UTF8
$itens = @()

foreach ($linha in $linhas) {
    if ($linha -notmatch '^\|\s*(\d+)\s*\|') { continue }
    $id = [int]$Matches[1]

    $celulas = $linha.Trim('|').Split('|')
    if ($celulas.Count -lt 4) { continue }

    $funcionalidade = $celulas[1].Trim()
    $revo = $celulas[2].Trim()
    $analise = $celulas[3].Trim()

    $prioridade = ''
    if ($funcionalidade -match '([\uD83C-\uDBFF][\uDC00-\uDFFF]|[\u2600-\u27BF])') {
        $prioridade = $Matches[1]
        $funcionalidade = $funcionalidade.Substring($prioridade.Length).Trim()
    }

    $estado = 'OK'
    if ($analise -match '^(PARCIAL|AUSENTE|FORA)\b') {
        $estado = $Matches[1]
    }

    $evidencias = @()
    foreach ($m in [regex]::Matches($analise, '`([^`]+)`')) {
        $ref = $m.Groups[1].Value
        if ($ref -match '\.(cs|xaml|json)$') {
            $evidencias += $ref
        }
    }

    $itens += [pscustomobject]@{
        id = $id
        prioridade = $prioridade
        funcionalidade = $funcionalidade
        revo = $revo
        status = $estado
        evidencia = @($evidencias)
        nota = ''
    }
}

$doc = [pscustomobject]@{
    origem = 'Revo Uninstaller Pro 5.5.4 — docs\REVO_SPEC.md secao 7.1'
    geradoPor = 'tools\auditoria\extrair-paridade-json.ps1'
    linhas = @($itens)
}

$json = $doc | ConvertTo-Json -Depth 6
[System.IO.File]::WriteAllText($saida, $json, (New-Object System.Text.UTF8Encoding($false)))
Write-Output "Extraidas $($itens.Count) linhas para $saida"
