# Extrai a fila acionavel das listas de lacuna (BINARIO-<C>.md).
#
# Uso: powershell -NoProfile -ExecutionPolicy Bypass -File tools\auditoria\extrair-fila-implementacao.ps1
#
# Cada lista de lacuna tem: numero da linha no catalogo canonico,
# funcionalidade, evidencia no binario, CATEGORIA e IMPACTO. E dai que sai a
# ordem de implementacao: primeiro o que o usuario de um desinstalador
# notaria faltando (impacto alto), depois o resto.
$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = (Resolve-Path (Join-Path $scriptDir '..\..')).Path
$docs = Join-Path $repo 'docs\auditoria'
$saida = Join-Path $docs 'fila-implementacao.json'

$pesoImpacto = @{
    'alto' = 0
    'media' = 1
    'medio' = 1
    'baixo' = 2
}

$itens = @()

foreach ($arquivo in (Get-ChildItem $docs -Filter 'BINARIO-*.md')) {
    $concorrente = $arquivo.BaseName -replace '^BINARIO-', ''

    $linhas = Get-Content -LiteralPath $arquivo.FullName -Encoding UTF8
    $cabecalho = $null

    foreach ($linha in $linhas) {
        if ($linha -notmatch '^\|') { continue }

        $celulas = $linha.Trim('|').Split('|') | ForEach-Object { $_.Trim() }

        if ($celulas[0] -eq '#' -or $celulas[0] -match '^#') {
            if ($celulas -contains 'Impacto (alto/medio/baixo)') {
                $cabecalho = $celulas
            }
            continue
        }

        if ($cabecalho -eq $null) { continue }
        if ($celulas.Count -lt $cabecalho.Count) { continue }
        if ($celulas[0] -notmatch '^\d+$') { continue }

        $indice = @{}
        for ($i = 0; $i -lt $cabecalho.Count; $i++) { $indice[$cabecalho[$i]] = $i }

        $func = $celulas[$indice['Funcionalidade']]
        $evid = $celulas[$indice['Evidencia no binario']]
        $cat = $celulas[$indice['Categoria']]
        $imp = $celulas[$indice['Impacto (alto/medio/baixo)']]

        $peso = if ($pesoImpacto.ContainsKey($imp.ToLower())) { $pesoImpacto[$imp.ToLower()] } else { 3 }

        $itens += [pscustomobject]@{
            concorrente = $concorrente
            numero = [int]$celulas[0]
            funcionalidade = $func
            evidencia = $evid
            categoria = $cat
            impacto = $imp
            peso = $peso
            confirmadoNoBinario = ($evid -notmatch 'nao confirmado')
        }
    }
}

$ordenados = $itens | Sort-Object @{ Expression = 'peso' }, concorrente, numero

$doc = [pscustomobject]@{
    origem = 'docs\auditoria\BINARIO-<CONCORRENTE>.md'
    geradoPor = 'tools\auditoria\extrair-fila-implementacao.ps1'
    total = $ordenados.Count
    itens = @($ordenados)
}

$json = $doc | ConvertTo-Json -Depth 6
[System.IO.File]::WriteAllText($saida, $json, (New-Object System.Text.UTF8Encoding($false)))

Write-Output ("Fila extraida: {0} itens" -f $ordenados.Count)

foreach ($p in 0..3) {
    $grupo = @($ordenados | Where-Object { $_.peso -eq $p })
    if ($grupo.Count -eq 0) { continue }
    $rotulo = @('alto', 'medio', 'baixo', 'nao classificado')[$p]
    Write-Output ("  impacto {0,-15} {1,4} itens" -f $rotulo, $grupo.Count)
}

Write-Output ''
Write-Output 'Impacto alto, por categoria:'
$ordenados | Where-Object { $_.peso -eq 0 } | Group-Object categoria | Sort-Object Count -Descending |
    ForEach-Object { Write-Output ("  {0,-16} {1}" -f $_.Name, $_.Count) }
