# Mescla as chaves de tools\lang-novas.txt nos catalogos de idioma.
# Uso: powershell -NoProfile -ExecutionPolicy Bypass -File tools\lang-adicionar.ps1
[CmdletBinding()]
param(
    [string]$DataFile,
    [string]$LangDirectory
)

$ErrorActionPreference = 'Stop'

$toolRoot = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($toolRoot)) { $toolRoot = Split-Path -Parent $MyInvocation.MyCommand.Path }
if ([string]::IsNullOrWhiteSpace($DataFile)) { $DataFile = Join-Path $toolRoot 'lang-novas.txt' }
if ([string]::IsNullOrWhiteSpace($LangDirectory)) {
    $LangDirectory = Join-Path (Split-Path $toolRoot -Parent) 'src\Tranal.Uninstaller\Lang'
}

if (-not (Test-Path -LiteralPath $DataFile)) { throw "Arquivo de dados ausente: $DataFile" }

$entries = @{}
foreach ($line in Get-Content -LiteralPath $DataFile -Encoding UTF8) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    if ($line.TrimStart().StartsWith('#')) { continue }

    $parts = $line.Split('|')
    if ($parts.Count -lt 3) { throw "Linha invalida: $line" }

    $key = $parts[0].Trim()
    $entries[$key] = @{ Pt = $parts[1]; En = $parts[2] }
}

function Merge-Catalog {
    param([string]$Path, [string]$Language)

    $map = [ordered]@{}
    if (Test-Path -LiteralPath $Path) {
        $json = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($prop in $json.PSObject.Properties) { $map[$prop.Name] = $prop.Value }
    }

    $added = 0
    foreach ($key in $entries.Keys) {
        $value = if ($Language -eq 'en-US') { $entries[$key].En } else { $entries[$key].Pt }
        if ($map.Contains($key)) { $map[$key] = $value } else { $added++ }
        $map[$key] = $value
    }

    $ordered = [ordered]@{}
    foreach ($key in $map.Keys) { $ordered[$key] = $map[$key] }

    $text = $ordered | ConvertTo-Json -Depth 4
    [System.IO.File]::WriteAllText($Path, $text, (New-Object System.Text.UTF8Encoding($false)))
    "  {0}: {1} chave(s) no total, {2} nova(s)." -f (Split-Path $Path -Leaf), $ordered.Count, $added
}

"Mesclando $([System.IO.Path]::GetFileName($DataFile)) ->"
Merge-Catalog -Path (Join-Path $LangDirectory 'pt-BR.json') -Language 'pt-BR'
Merge-Catalog -Path (Join-Path $LangDirectory 'en-US.json') -Language 'en-US'
"Concluido."
