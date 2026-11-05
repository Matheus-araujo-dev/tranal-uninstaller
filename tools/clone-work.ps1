# Clona a arvore de codigo-fonte do Tranal para um diretorio de trabalho isolado.
# Uso: powershell -NoProfile -ExecutionPolicy Bypass -File tools\clone-work.ps1 -Destination <dir>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Destination,
    [string]$Source = 'E:\Projetos\tranal-uninstaller'
)

$ErrorActionPreference = 'Stop'

# ATENCAO: o PowerShell nao diferencia maiusculas de minusculas em nomes de
# variavel. Nao use $Destination e $destination (nem $Source e $source) no mesmo
# script: a sobrescrita silenciosa corrompe os caminhos.
$excludedDirs = @('bin', 'obj', 'publish', '.vs', '.idea', '.git')

if ([System.IO.Directory]::Exists($Destination)) {
    [System.IO.Directory]::Delete($Destination, $true)
}
[void][System.IO.Directory]::CreateDirectory($Destination)

$copied = 0
foreach ($sourceFile in [System.IO.Directory]::GetFiles($Source, '*', [System.IO.SearchOption]::AllDirectories)) {
    $relative = $sourceFile.Substring($Source.Length).TrimStart('\', '/')
    $skip = $false
    foreach ($part in ($relative -split '[\\/]')) {
        if ($excludedDirs -contains $part -or $part -like '_lixo-*') { $skip = $true; break }
    }
    if ($skip) { continue }

    $destFile = [System.IO.Path]::Combine($Destination, $relative)
    $separator = $destFile.LastIndexOf('\')
    $parentFolder = if ($separator -gt 0) { $destFile.Substring(0, $separator) } else { $Destination }

    if (-not [System.IO.Directory]::Exists($parentFolder)) {
        [void][System.IO.Directory]::CreateDirectory($parentFolder)
    }

    [System.IO.File]::Copy($sourceFile, $destFile, $true)
    $copied++
}

"clonado: $copied arquivo(s) -> $Destination"
