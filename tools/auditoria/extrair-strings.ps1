<#
  Extracao rapida de strings de binarios de concorrentes.
  Substitui o laco byte-a-byte do coleta.ps1 por uma implementacao C# vetorizada.
  Saida: <destino>\<id>.strings.txt  (ASCII + UTF-16LE, unicos, ordenados por aparicao)
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Workspace,
    [Parameter(Mandatory = $true)][string]$Destino,
    [int]$Min = 5
)

$ErrorActionPreference = 'Stop'
$utf8 = New-Object System.Text.UTF8Encoding($false)

Add-Type -Path (Join-Path $PSScriptRoot 'FastStrings.cs')

if (-not (Test-Path $Destino)) { [System.IO.Directory]::CreateDirectory($Destino) | Out-Null }

$a = Join-Path $Workspace 'auditoria'

$alvos = @(
    @{ id = 'bcu';                arq = "$a\bcu\app\BCUninstaller.dll" },
    @{ id = 'bcu-tools';          arq = "$a\bcu\app\UninstallTools.dll" },
    @{ id = 'geek';               arq = "$a\geek\app\geek.exe" },
    @{ id = 'hibit';              arq = "$a\hibit\app\setup-inno\app\HiBitUninstaller.exe" },
    @{ id = 'instalr';            arq = "$a\instalr\downloads\Uninstalr_Portable.exe" },
    @{ id = 'iobit';              arq = "$a\iobit\app\payload\IObitUninstaler.exe" },
    @{ id = 'iobit-iush';         arq = "$a\iobit\app\payload\iush.exe" },
    @{ id = 'iobit-monitor';      arq = "$a\iobit\app\payload\UninstallMonitor.exe" },
    @{ id = 'iobit-promote';      arq = "$a\iobit\app\payload\UninstallPromote.exe" },
    @{ id = 'iobit-explorer';     arq = "$a\iobit\app\payload\UninstallExplorer.dll" },
    @{ id = 'softorganizer';      arq = "$a\softorganizer\app\so1050-10.50\SoftOrganizer.exe" },
    @{ id = 'totaluninstall';     arq = "$a\totaluninstall\app\professional\app\Tu.exe" },
    @{ id = 'totaluninstall-res'; arq = "$a\totaluninstall\app\professional\app\TuRes.dll" },
    @{ id = 'uninstalltool';      arq = "$a\uninstalltool\app\bin\UninstallTool_x64.exe" },
    @{ id = 'wise';               arq = "$a\wise\app\app\WiseProgramUninstaller.exe" },
    @{ id = 'wise-monitor';       arq = "$a\wise\app\app\UnMonitor.exe" },
    @{ id = 'wise-manager';       arq = "$a\wise\app\app\WiseAppManager.dll" },
    @{ id = 'ashampoo-instalador'; arq = "$a\ashampoo\downloads\ashampoo_uninstaller_pro_27.exe" },
    @{ id = 'revo-pro';          arq = "$Workspace\revo-extracted\app\RevoUninPro.exe" },
    @{ id = 'revo-appbar';       arq = "$Workspace\revo-extracted\app\RevoAppBar.exe" },
    @{ id = 'revo-srp';          arq = "$Workspace\revo-extracted\app\RevoSrp.exe" },
    @{ id = 'revo-cmd';          arq = "$Workspace\revo-extracted\app\RevoCmd.exe" },
    @{ id = 'revo-ext-dll';      arq = "$Workspace\revo-extracted\app\RUExt.dll" },
    @{ id = 'revo-winapps';      arq = "$Workspace\revo-extracted\app\WinAppsDll.dll" }
)

$indice = New-Object System.Collections.Generic.List[string]

foreach ($t in $alvos) {
    if (-not (Test-Path $t.arq)) {
        Write-Output "AUSENTE  $($t.id)  -> $($t.arq)"
        continue
    }
    $bytes = [System.IO.File]::ReadAllBytes($t.arq)
    $sha = (Get-FileHash -Path $t.arq -Algorithm SHA256).Hash.ToLowerInvariant()
    $fv = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($t.arq)

    $strs = [FastStrings]::Extract($bytes, $Min)
    $set = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    foreach ($s in $strs) { [void]$set.Add($s) }

    # triagem de tecnologia
    $tem = {
        param($p)
        foreach ($x in $strs) { if ($x.IndexOf($p, [StringComparison]::OrdinalIgnoreCase) -ge 0) { return $true } }
        return $false
    }
    $triagem = @()
    if (& $tem 'BSJB') { $triagem += 'gerenciado (.NET)' }
    if (& $tem 'v4.0.30319') { $triagem += 'CLR v4' }
    if (& $tem 'PACKAGEINFO') { $triagem += 'Delphi' }
    if (& $tem 'Embarcadero') { $triagem += 'Embarcadero' }
    if (& $tem 'Sciter') { $triagem += 'Sciter' }
    if (& $tem 'Electron') { $triagem += 'Electron' }
    if (& $tem 'NSIS') { $triagem += 'NSIS' }
    if (& $tem 'Inno Setup') { $triagem += 'Inno Setup' }
    if (& $tem 'Qt5Core') { $triagem += 'Qt5' }
    if (& $tem 'MSVCP') { $triagem += 'MSVC++' }

    $cab = New-Object System.Collections.Generic.List[string]
    $cab.Add("# id: $($t.id)")
    $cab.Add("# arquivo: $($t.arq)")
    $cab.Add("# bytes: $($bytes.Length)")
    $cab.Add("# sha256: $sha")
    $cab.Add("# versao: $($fv.FileVersion) / $($fv.ProductVersion)")
    $cab.Add("# empresa: $($fv.CompanyName)")
    $cab.Add("# titulo: $($fv.FileDescription)")
    $cab.Add("# triagem: $(if ($triagem.Count) { $triagem -join ', ' } else { 'n/d' })")
    $cab.Add("# strings unicas: $($set.Count)")
    $cab.Add('')
    foreach ($s in $strs) { $cab.Add($s) }

    $out = Join-Path $Destino "$($t.id).strings.txt"
    [System.IO.File]::WriteAllText($out, ($cab -join "`r`n"), $utf8)

    $indice.Add("$($t.id)`t$($bytes.Length)`t$($set.Count)`t$($triagem -join ', ')`t$out")
    Write-Output ("OK  {0,-22} {1,12:N0} bytes  {2,7:N0} strings  [{3}]" -f $t.id, $bytes.Length, $set.Count, ($triagem -join ', '))
}

[System.IO.File]::WriteAllText((Join-Path $Destino 'INDICE.tsv'), (($indice -join "`r`n") + "`r`n"), $utf8)
Write-Output ""
Write-Output "indice: $(Join-Path $Destino 'INDICE.tsv')"
