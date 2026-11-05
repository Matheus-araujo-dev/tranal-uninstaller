<#
  Publica o Tranal Uninstaller com verificacao do manifesto embutido.

  Por que existe: no publish single-file, o apphost gerado (com o RT_MANIFEST)
  fica CACHED no obj intermediate. Trocar o valor de ApplicationManifest sem
  limpar o obj faz o exe seguinte sair com o manifesto do publish anterior —
  foi assim que um build requireAdministrator saiu como asInvoker.

  Uso:
    publish-tranal.ps1 -Variant gui     -> publish\gui      (requireAdministrator)
    publish-tranal.ps1 -Variant preview -> publish\preview  (asInvoker, so para inspecao visual)
    publish-tranal.ps1 -Variant cli     -> publish\cli
#>
param(
    [ValidateSet('gui', 'preview', 'cli')]
    [string]$Variant = 'gui'
)

$ErrorActionPreference = 'Stop'

# A raiz do repositorio e o proprio projeto (src\Tranal.Uninstaller, src\Tranal.Cli).
# Cuidado: o PowerShell nao diferencia maiusculas de minusculas em nomes de variavel
# — nao use $repoRoot e $repoRoot2 nem $proj e $Proj no mesmo script.
$repoRoot = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $repoRoot 'src\Tranal.Uninstaller'
$cliProj = Join-Path $repoRoot 'src\Tranal.Cli'

function Reset-Intermediate([string]$projectDir) {
    $obj = Join-Path $projectDir 'obj'
    if (Test-Path $obj) {
        $stash = Join-Path $repoRoot ('publish\_obj-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
        Move-Item $obj $stash -Force
        Write-Host "obj anterior movido para $stash"
    }
}

Push-Location $repoRoot
try {
    switch ($Variant) {
        'gui' {
            Reset-Intermediate $proj
            dotnet publish $proj\Tranal.Uninstaller.csproj -c Release -r win-x64 --self-contained true `
                -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
                -o publish\gui
            if ($LASTEXITCODE -ne 0) { throw "Falha no publish do GUI (exit $LASTEXITCODE)" }
            $expected = 'requireAdministrator'
            $target = 'publish\gui\TranalUninstaller.exe'
        }
        'preview' {
            Reset-Intermediate $proj
            dotnet publish $proj\Tranal.Uninstaller.csproj -c Release -r win-x64 --self-contained true `
                -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
                -p:ApplicationManifest=app-preview.manifest `
                -o publish\preview
            if ($LASTEXITCODE -ne 0) { throw "Falha no publish do preview (exit $LASTEXITCODE)" }
            $expected = 'asInvoker'
            $target = 'publish\preview\TranalUninstaller.exe'
        }
        'cli' {
            Reset-Intermediate $cliProj
            dotnet publish $cliProj\Tranal.Cli.csproj -c Release -r win-x64 --self-contained true `
                -p:PublishSingleFile=true `
                -o publish\cli
            if ($LASTEXITCODE -ne 0) { throw "Falha no publish da CLI (exit $LASTEXITCODE)" }
            $expected = $null
            $target = 'publish\cli\tranal.exe'
        }
    }

    if ($expected) {
        $env:PYTHONIOENCODING = 'utf-8'
        $level = (& python (Join-Path $repoRoot 'tools\dump_manifest.py') $target |
            Select-String -Pattern 'requestedExecutionLevel' |
            ForEach-Object { if ($_ -match 'level="([^"]+)"') { $Matches[1] } } | Select-Object -First 1)
        Write-Host "manifesto: $level (esperado: $expected)"
        if ($level -ne $expected) {
            throw "MANIFESTO DIVERGENTE: obtido '$level', esperado '$expected'"
        }
    }

    Get-Item (Join-Path $repoRoot $target) |
        Select-Object @{n='Arquivo'; e={$_.FullName.Substring($repoRoot.Length + 1)}},
                      @{n='Tamanho'; e={'{0:N0}' -f $_.Length}},
                      LastWriteTime
}
finally {
    Pop-Location
}
