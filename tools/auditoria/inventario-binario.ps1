<#
  Mineracao do dumps de strings em um inventario estruturado por concorrente.
  Entrada: <StringsDir>\<id>.strings.txt (saida do extrair-strings.ps1)
  Saida:   <Destino>\<id>.inventario.md e <Destino>\<id>.candidatos.json

  Cada linha do inventario aponta para a evidencia no binario de origem.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$StringsDir,
    [Parameter(Mandatory = $true)][string]$Destino,
    [string]$TranalSrc = 'E:\Projetos\tranal-uninstaller\src'
)

$ErrorActionPreference = 'Stop'
$utf8 = New-Object System.Text.UTF8Encoding($false)
if (-not (Test-Path $Destino)) { [System.IO.Directory]::CreateDirectory($Destino) | Out-Null }

# --- dicionario de capacidades: token no binario -> capacidade do produto ---
$capacidades = [ordered]@{
    'reg-delete'       = 'Remove chaves de registro'
    'RegDeleteKey'     = 'Remove chaves de registro'
    'RegDeleteValue'   = 'Remove valores de registro'
    'RegSetValue'      = 'Escreve no registro'
    'CreateService'    = 'Gerencia servicos do Windows'
    'OpenService'      = 'Gerencia servicos do Windows'
    'DeleteService'    = 'Remove servicos'
    'schtasks'         = 'Agenda tarefas'
    'TaskScheduler'    = 'Agenda tarefas'
    'SetupDi'          = 'Enumera dispositivos/Drivers'
    'SetupAPI'         = 'Enumera dispositivos/Drivers'
    'Win32_Process'    = 'Interage com processos (WMI)'
    'Win32_Service'    = 'Interage com servicos (WMI)'
    'Root\CIMV2'       = 'Usa WMI'
    'SRSetRestorePoint'= 'Cria ponto de restauracao do sistema'
    'vssadmin'         = 'Manipula copias de sombra'
    'WinInet'          = 'Acesso HTTP/FTP'
    'WinHttp'          = 'Acesso HTTP/FTP'
    'URLDownloadToFile'= 'Baixa arquivos'
    'ShellExecute'     = 'Executa programas'
    'CreateProcess'    = 'Executa processos'
    'UnregisterDll'    = 'Chama unregister de DLL (registro de componentes)'
    'MsiEnumProducts'  = 'Consulta produtos MSI'
    'Prefetch'         = 'Analisa Prefetch'
    'Windows Update'   = 'Interage com Windows Update'
    'wuauserv'         = 'Interage com Windows Update'
    'Uninstall'        = 'Executa desinstaladores'
    'msiexec'          = 'Desinstalacao via MSI'
    'rundll32'         = 'Desinstalacao via rundll32'
    'Password'         = 'Gerencia senhas'
    'Credential'       = 'Gerencia credenciais'
    'Browser'          = 'Integra-se a navegadores'
    'Extension'        = 'Gerencia extensoes de navegador'
    'Addons'           = 'Gerencia addons de navegador'
    'Firewall'         = 'Gerencia firewall'
    'Defender'         = 'Integra-se ao Defender'
    'Product Key'      = 'Gerencia chaves de produto/licenca'
    'License'          = 'Gerencia licencas'
    'Serial'           = 'Gerencia numeros de serie'
    'Undo'             = 'Oferece desfazer'
    'Backup'           = 'Faz backup'
    'Restore'          = 'Restaura backups'
    'Snapshot'         = 'Cria snapshots/ponto de restauracao'
    'Log'              = 'Mantem registro/log'
    'History'          = 'Mantem historico'
    'Report'           = 'Gera relatorios'
    'Export'           = 'Exporta dados'
    'Import'           = 'Importa dados'
    'Print'            = 'Imprime'
    'Script'           = 'Executa scripts/automacao'
    'Schedule'         = 'Agenda execucao'
    'Monitor'          = 'Monitora em segundo plano'
    'Watch'            = 'Monitora arquivos/pastas'
    'Filter'           = 'Filtra a lista'
    'Group'            = 'Agrupa itens'
    'Category'         = 'Categoriza'
    'Portable'         = 'Modo portatil'
    'Ignore'           = 'Ignora itens'
    'Exclude'          = 'Exclui itens'
    'Protect'          = 'Proteger itens'
    'Include'          = 'Inclui itens'
    'Rule'             = 'Motor de regras'
    'Compare'          = 'Compara versoes/estado'
    'Cluster'          = 'Agrupa por proximidade'
    'Duplicate'        = 'Detecta duplicados'
    'Size'             = 'Mede tamanho em disco'
    'Shred'            = 'Apaga de forma segura'
    'Privacy'          = 'Recursos de privacidade'
    'Virtual'          = 'Ambiente virtual'
    'Sandbox'          = 'Execucao em area restrita'
    'Wallpaper'        = 'Gerencia papel de parede'
    'Font'             = 'Gerencia fontes'
    'Startup'          = 'Gerencia itens de inicializacao'
    'ContextMenu'      = 'Adiciona ao menu de contexto'
    'Shell'            = 'Integra ao shell do Windows'
}

# palavras-chave para filtrar rotulos de interface realmente descrevem funcao
$kw = @('uninstall','remov','delet','clean','scan','search','find','backup','restore','snapshot','history','log','report','export','import','print','script','schedul','monitor','watch','filter','group','categor','portable','ignore','exclud','protect','includ','rule','compar','cluster','duplicat','size','shred','privacy','virtual','sandbox','wallpaper','font','startup','context','shell','install','program','application','registry','service','task','driver','startup','browser','plugin','addon','extension','password','license','key','firewall','defender','update','trace','lock','kill','process','folder','file','shortcut','component','leftover','orphan','junk','temp','cache','user','system','disk','space','drive','usage','analyz','statistic','compar','review','plan','preview','command','line','silent','batch','auto','wizard','ste','p','feature','setting','option','configur','prefer','theme','appear','column','sort','view','panel','sidebar','window','dialog','tab','menu','toolbar','shortcut','star','favorite','tag','note','comment','rating','usage','quota','limit','lock','crypt','encrypt','wipe','cleaner','declutter','optimiz','boost','speed','up','tweak','service','disable','enable','stop','pause','resume','restart','reboot','shut','boot')

$matriz = New-Object System.Collections.Generic.List[object]

foreach ($f in (Get-ChildItem $StringsDir -Filter '*.strings.txt' | Sort-Object Name)) {
    $id = $f.BaseName -replace '\.strings$',''
    $lines = [System.IO.File]::ReadAllLines($f.FullName, [System.Text.Encoding]::UTF8)
    $meta = @{}
    $body = New-Object System.Collections.Generic.List[string]
    foreach ($l in $lines) {
        if ($l.StartsWith('# ')) {
            $kv = $l.Substring(2).Split(':', 2)
            if ($kv.Count -eq 2) { $meta[$kv[0].Trim()] = $kv[1].Trim() }
        }
        elseif ($l.Trim().Length -gt 0) { $body.Add($l) }
    }

    # --- classes Delphi / RTTI ---
    $classes = $body | Where-Object { $_ -match '^T[A-Z][A-Za-z0-9_]{3,}$' -and $_.Length -le 60 } | Sort-Object -Unique

    # --- rotulos de interface (2+ palavras, com maiuscula inicial) ---
    $labels = $body | Where-Object {
        $s = $_
        $s.Length -ge 6 -and $s.Length -le 140 -and
        $s -match '^[A-Z\u00C0-\u00DA]' -and
        $s -match '[a-z\u00E0-\u00FA]' -and
        ($s -split '\s+').Count -ge 2 -and
        $s -notmatch '://' -and
        $s -notmatch '^[A-Za-z]:\\' -and
        $s -notmatch '^[A-Za-z0-9_]+\.[A-Za-z0-9_.]+$' -and
        $s -notmatch '^[^a-zA-Z]*$' -and
        $s -notmatch '^\W+$' -and
        $s -notmatch 'HKEY_|\\Registry\\|\\Software\\|\\Microsoft\\' -and
        $s -notmatch '^[A-Z0-9]{6,}$'
    } | Sort-Object -Unique

    # --- rotulos relevantes por palavra-chave ---
    $relevantes = $labels | Where-Object {
        $l = $_.ToLowerInvariant()
        ($kw | Where-Object { $l.Contains($_) }).Count -ge 1
    }

    # --- capacidades ---
    $capAtivas = @()
    $capInativas = @()
    $todas = ($body -join "`n")
    foreach ($k in $capacidades.Keys) {
        if ($todas.IndexOf($k, [StringComparison]::OrdinalIgnoreCase) -ge 0) { $capAtivas += $k } else { $capInativas += $k }
    }

    $json = [ordered]@{
        id              = $id
        arquivo         = $meta['arquivo']
        bytes           = $meta['bytes']
        sha256          = $meta['sha256']
        versao          = $meta['versao']
        empresa         = $meta['empresa']
        triagem         = $meta['triagem']
        strings_unicas  = [int]$meta['strings unicas']
        classes_rtti    = @($classes)
        rotulos_total   = @($labels).Count
        rotulos_relev   = @($relevantes)
        capacidades_ativas = @($capAtivas)
    }
    [System.IO.File]::WriteAllText((Join-Path $Destino "$id.candidatos.json"), ($json | ConvertTo-Json -Depth 5), $utf8)

    $md = New-Object System.Collections.Generic.List[string]
    $md.Add("# Inventario binario - $id")
    $md.Add("")
    $md.Add("- arquivo: ``$($meta['arquivo'])``")
    $md.Add("- sha256: ``$($meta['sha256'])``")
    $md.Add("- bytes: $($meta['bytes']) | strings unicas: $($meta['strings unicas']) | triagem: $($meta['triagem'])")
    $md.Add("- versao: $($meta['versao']) | empresa: $($meta['empresa'])")
    $md.Add("")
    $md.Add("## Classes RTTI (Delphi) detectadas: $($classes.Count)")
    if ($classes.Count) { $md.Add(($classes -join ', ')) }
    $md.Add("")
    $md.Add("## Rotulos de interface relevantes: $($relevantes.Count) de $($labels.Count)")
    foreach ($r in $relevantes) { $md.Add("- $r") }
    $md.Add("")
    $md.Add("## Capacidades por token de API/IO: $($capAtivas.Count) ativas de $($capacidades.Count)")
    foreach ($k in $capAtivas) { $md.Add("- **$k** - $($capacidades[$k])") }
    [System.IO.File]::WriteAllText((Join-Path $Destino "$id.inventario.md"), ($md -join "`r`n"), $utf8)

    $matriz.Add([pscustomobject]@{
        id = $id; classes = $classes.Count; rotulos = $relevantes.Count; caps = $capAtivas.Count; strings = [int]$meta['strings unicas']
    })
    Write-Output ("{0,-22} classes={1,5} rotulos={2,5} caps={3,4} strings={4,7}" -f $id, $classes.Count, $relevantes.Count, $capAtivas.Count, [int]$meta['strings unicas'])
}

[System.IO.File]::WriteAllText((Join-Path $Destino 'RESUMO.tsv'), (($matriz | ForEach-Object { "$($_.id)`t$($_.classes)`t$($_.rotulos)`t$($_.caps)`t$($_.strings)" }) -join "`r`n"), $utf8)
Write-Output ""
Write-Output "resumo: $(Join-Path $Destino 'RESUMO.tsv')"
