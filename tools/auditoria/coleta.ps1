<#
.SYNOPSIS
    Coleta de evidência para a auditoria de concorrentes.

.DESCRIPTION
    Três modos, todos offline e determinísticos:

      strings  - extrai cadeias ASCII e UTF-16LE imprimíveis de um binário.
                 É o que revela menus, legendas de formulário, URLs e
                 textos de erro em programas nativos (Delphi, C++), onde não
                 existe metadata .NET para consultar.

      resx     - extrai o par nome/valor de um .resx (localização de
                 programas .NET), incluindo o texto traduzido pt-BR.

      pe       - cabeçalho PE (bits, machine, timestamp, subsistema),
                 assinatura Authenticode, versão do arquivo e a lista de
                 DLLs importadas. Não depende de ferramentas externas.

.EXAMPLE
    .\coleta.ps1 strings -Arquivo 'C:\app\app.exe' -Min 5 -Saida 'C:\ev\app.strings.txt'
    .\coleta.ps1 resx -Arquivo 'C:\app\pt-BR\App.resources.resx' -Saida 'C:\ev\app.resx.txt'
    .\coleta.ps1 pe -Arquivo 'C:\app\app.exe'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('strings', 'resx', 'pe')]
    [string]$Modo,

    [Parameter(Mandatory = $true)]
    [string]$Arquivo,

    [int]$Min = 5,

    [string]$Saida
)

$ErrorActionPreference = 'Stop'

function Write-Saida {
    param([string]$Texto)
    if ($Saida) {
        $dir = Split-Path -Parent $Saida
        if ($dir -and -not (Test-Path $dir)) { [System.IO.Directory]::CreateDirectory($dir) | Out-Null }
        [System.IO.File]::WriteAllText($Saida, $Texto, (New-Object System.Text.UTF8Encoding($true)))
        Write-Output "gravado: $Saida ($($Texto.Length) chars)"
    }
    else {
        Write-Output $Texto
    }
}

function Get-Strings {
    param([byte[]]$Bytes, [int]$MinLen)

    $sb = New-Object System.Text.StringBuilder
    $ascii = New-Object System.Collections.Generic.List[string]
    $utf16 = New-Object System.Collections.Generic.List[string]

    $cur = New-Object System.Text.StringBuilder
    foreach ($b in $Bytes) {
        if ($b -ge 0x20 -and $b -le 0x7E) {
            [void]$cur.Append([char]$b)
        }
        else {
            if ($cur.Length -ge $MinLen) { $ascii.Add($cur.ToString()) }
            [void]$cur.Clear()
        }
    }
    if ($cur.Length -ge $MinLen) { $ascii.Add($cur.ToString()) }

    for ($i = 0; $i -lt $Bytes.Length - 1; $i += 2) {
        $lo = $Bytes[$i]; $hi = $Bytes[$i + 1]
        if ($hi -eq 0 -and $lo -ge 0x20 -and $lo -le 0x7E) {
            [void]$cur.Append([char]$lo)
        }
        else {
            if ($cur.Length -ge $MinLen) { $utf16.Add($cur.ToString()) }
            [void]$cur.Clear()
            $i++
        }
    }
    if ($cur.Length -ge $MinLen) { $utf16.Add($cur.ToString()) }

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('=== ASCII ===')
    foreach ($s in ($ascii | Select-Object -Unique)) { $lines.Add($s) }
    $lines.Add('')
    $lines.Add('=== UTF16LE ===')
    foreach ($s in ($utf16 | Select-Object -Unique)) { $lines.Add($s) }
    return ($lines -join "`r`n")
}

function Get-PeInfo {
    param([string]$Path)

    $lines = New-Object System.Collections.Generic.List[string]
    $bytes = [System.IO.File]::ReadAllBytes($Path)

    $lines.Add("arquivo: $Path")
    $lines.Add("bytes:   $($bytes.Length)")

    $fv = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($Path)
    $lines.Add("versao:  $($fv.FileVersion)  (produto $($fv.ProductVersion))")
    $lines.Add("empresa: $($fv.CompanyName)")
    $lines.Add("titulo:  $($fv.FileDescription)")

    $sig = Get-AuthenticodeSignature -FilePath $Path
    $lines.Add("assinatura: $($sig.Status)")
    if ($sig.SignerCertificate) {
        $lines.Add("  assinante: $($sig.SignerCertificate.Subject)")
        $lines.Add("  emissor:   $($sig.SignerCertificate.Issuer)")
        $lines.Add("  valido ate: $($sig.SignerCertificate.NotAfter)")
    }

    if ($bytes.Length -lt 0x40) { return ($lines -join "`r`n") }
    $peOff = [BitConverter]::ToInt32($bytes, 0x3C)
    if ([Text.Encoding]::ASCII.GetString($bytes, $peOff, 4) -ne 'PE' + [char]0 + [char]0) {
        $lines.Add('PE: invalido')
        return ($lines -join "`r`n")
    }

    $machine = [BitConverter]::ToUInt16($bytes, $peOff + 4)
    $ts = [BitConverter]::ToUInt32($bytes, $peOff + 8)
    $optOff = $peOff + 24
    $magic = [BitConverter]::ToUInt16($bytes, $optOff)
    $is64 = ($magic -eq 0x20B)
    $lines.Add("machine:  0x{0:X4}" -f $machine)
    $lines.Add("bits:     $(if ($is64) { 64 } else { 32 })")
    $lines.Add("magic:    0x{0:X4}" -f $magic)
    $lines.Add("timestamp: $([DateTimeOffset]::FromUnixTimeSeconds($ts).UtcDateTime.ToString('u'))")
    if ($ts -gt 4000000000) { $lines.Add('  (timestamp anomalo - empacotado ou alterado)') }

    $subsystem = [BitConverter]::ToUInt16($bytes, $optOff + 68)
    $subs = switch ($subsystem) { 2 { 'Windows GUI' } 3 { 'Windows CUI' } default { "$subsystem" } }
    $lines.Add("subsistema: $subs")

    # diretorios
    $numDirs = [BitConverter]::ToUInt16($bytes, $peOff + 6)
    $optSize = [BitConverter]::ToUInt16($bytes, $peOff + 20)
    $dirOff = $optOff + $optSize
    $sections = @()
    for ($i = 0; $i -lt $numDirs; $i++) {
        $o = $dirOff + $i * 40
        $name = [Text.Encoding]::ASCII.GetString($bytes, $o, 8).Trim([char]0)
        $sections += $name
    }
    $lines.Add("secoes:   $($sections -join ', ')")

    # imports
    $imports = @()
    try {
        $numSec = 0
        for ($i = 0; $i -lt $numDirs; $i++) {
            $o = $dirOff + $i * 40
            $vSize = [BitConverter]::ToUInt32($bytes, $o + 8)
            $vAddr = [BitConverter]::ToUInt32($bytes, $o + 12)
            $rawSize = [BitConverter]::ToUInt32($bytes, $o + 16)
            $rawPtr = [BitConverter]::ToUInt32($bytes, $o + 20)
            if ($vSize -gt 0) { $numSec++ }
        }
        $ddOff = if ($is64) { $optOff + 112 } else { $optOff + 96 }
        $importRva = [BitConverter]::ToUInt32($bytes, $ddOff + 8)

        function Convert-RvaToOffset {
            param([uint32]$Rva)
            for ($i = 0; $i -lt $numDirs; $i++) {
                $o = $dirOff + $i * 40
                $vAddr = [BitConverter]::ToUInt32($bytes, $o + 12)
                $vSize = [BitConverter]::ToUInt32($bytes, $o + 8)
                $rawPtr = [BitConverter]::ToUInt32($bytes, $o + 20)
                $rawSize = [BitConverter]::ToUInt32($bytes, $o + 16)
                if ($Rva -ge $vAddr -and $Rva -lt ($vAddr + $vSize)) {
                    return ($rawPtr + ($Rva - $vAddr))
                }
            }
            return 0
        }

        if ($importRva -ne 0) {
            $off = Convert-RvaToOffset $importRva
            while ($off -gt 0 -and $off -lt $bytes.Length - 20) {
                $nameRva = [BitConverter]::ToUInt32($bytes, $off + 12)
                $firstThunk = [BitConverter]::ToUInt32($bytes, $off + 16)
                if ($nameRva -eq 0 -and $firstThunk -eq 0) { break }
                $nOff = Convert-RvaToOffset $nameRva
                if ($nOff -gt 0) {
                    $end = $nOff
                    while ($end -lt $bytes.Length -and $bytes[$end] -ne 0) { $end++ }
                    $dll = [Text.Encoding]::ASCII.GetString($bytes, $nOff, $end - $nOff)
                    $imports += $dll
                }
                $off += 20
            }
        }
    }
    catch {
        $lines.Add("imports: erro ao ler - $($_.Exception.Message)")
    }
    if ($imports.Count -gt 0) {
        $lines.Add('')
        $lines.Add('=== DLLs importadas ===')
        foreach ($d in ($imports | Select-Object -Unique)) { $lines.Add($d) }
    }

    # strings indicativas de runtime
    $all = [Text.Encoding]::ASCII.GetString($bytes)
    $hints = @()
    if ($all -match 'mscoree\.dll') { $hints += 'gerenciado .NET (mscoree.dll)' }
    if ($all -match 'VCRUNTIME|MSVCP') { $hints += 'runtime C++ (MSVC)' }
    if ($all -match 'Qt5Core|Qt6Core') { $hints += 'Qt' }
    if ($all -match 'Delphi|Embarcadero') { $hints += 'Delphi/Embarcadero' }
    # marcadores de empacotador so valem para executaveis: em uma managed DLL
    # o nome do empacotador aparece no metadata do projeto e nao prova nada.
    if ([IO.Path]::GetExtension($Path) -eq '.exe') {
        if ($all -match 'Inno Setup') { $hints += 'empacotado com Inno Setup' }
        if ($all -match 'Nullsoft') { $hints += 'empacotado com NSIS' }
        if ($all -match '7-Zip') { $hints += 'empacotado com 7-Zip' }
        if ($all -match 'WiX|Burn') { $hints += 'possivel instalador WiX/MSI' }
    }
    if ($hints.Count -gt 0) {
        $lines.Add('')
        $lines.Add('=== marcadores ===')
        foreach ($h in $hints) { $lines.Add($h) }
    }

    return ($lines -join "`r`n")
}

switch ($Modo) {
    'strings' {
        $b = [System.IO.File]::ReadAllBytes($Arquivo)
        Write-Saida (Get-Strings -Bytes $b -MinLen $Min)
    }
    'resx' {
        [xml]$x = [System.IO.File]::ReadAllText($Arquivo)
        $lines = New-Object System.Collections.Generic.List[string]
        foreach ($d in $x.root.data) {
            $v = $d.value
            if ($v -is [System.Xml.XmlElement]) { $v = $v.InnerText }
            $lines.Add(('{0}`t{1}' -f $d.name, ($v -replace "`r?`n", ' | ')))
        }
        Write-Saida ($lines -join "`r`n")
    }
    'pe' {
        Write-Saida (Get-PeInfo -Path $Arquivo)
    }
}
