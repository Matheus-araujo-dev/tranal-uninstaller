# Gera a prova de cobertura por concorrente.
#
# Uso: powershell -NoProfile -ExecutionPolicy Bypass -File tools\auditoria\provar-paridade.ps1
#
# Junta tres fontes:
#   1. docs\auditoria\catalogo-concorrentes.json  (o catalogo canonico de cada
#      concorrente, extraido por extrair-catalogo.ps1);
#   2. docs\auditoria\RECON-*.md                  (a apuracao linha a linha
#      contra o codigo, feita pelos verificadores);
#   3. docs\auditoria\paridade-revo.json          (o Revo Pro, que tem
#      catalogo proprio na secao 7.1 do REVO_SPEC).
#
# O resultado e docs\auditoria\PARIDADE-11-CONCORRENTES.md: uma tabela por
# concorrente com o total de funcionalidades, quantas o Tranal tem, quantas
# faltam e quantas sao limite declarado.
$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = (Resolve-Path (Join-Path $scriptDir '..\..')).Path
$docs = Join-Path $repo 'docs\auditoria'

$catalogoPath = Join-Path $docs 'catalogo-concorrentes.json'
$revoPath = Join-Path $docs 'paridade-revo.json'
$saida = Join-Path $docs 'PARIDADE-11-CONCORRENTES.md'

if (-not (Test-Path -LiteralPath $catalogoPath)) {
    Write-Error "Catalogo nao encontrado. Rode antes: tools\auditoria\extrair-catalogo.ps1"
    exit 2
}

# ------------------------------------------------------------------
# 1. Catalogo canonico
# ------------------------------------------------------------------
$catalogo = Get-Content -LiteralPath $catalogoPath -Raw -Encoding UTF8 | ConvertFrom-Json

foreach ($c in $catalogo.concorrentes) {
    foreach ($f in $c.funcionalidades) {
        if (-not $f.PSObject.Properties['verificado']) { $f | Add-Member -NotePropertyName 'verificado' -NotePropertyValue '' }
        if (-not $f.PSObject.Properties['evidenciaCodigo']) { $f | Add-Member -NotePropertyName 'evidenciaCodigo' -NotePropertyValue '' }
    }
}

# ------------------------------------------------------------------
# 2. Apuracao (RECON-*.md)
# ------------------------------------------------------------------
$mapear = @{
    'JÁ EXISTE' = 'OK'
    'JA EXISTE' = 'OK'
    'NÃO EXISTE' = 'AUSENTE'
    'NAO EXISTE' = 'AUSENTE'
    'FORA POR DECISÃO' = 'FORA'
    'FORA POR DECISAO' = 'FORA'
    'NÃO CONFIRMADO NO BINÁRIO' = 'NAO CONFIRMADO'
    'NAO CONFIRMADO NO BINARIO' = 'NAO CONFIRMADO'
}

$apuracoes = 0
foreach ($arquivo in (Get-ChildItem $docs -Filter 'RECON-*.md' -ErrorAction SilentlyContinue)) {
    $competentes = @()
    $secaoAtual = $null

    foreach ($linha in (Get-Content -LiteralPath $arquivo.FullName -Encoding UTF8)) {
        # A secao do relatorio diz de qual concorrente a tabela e
        # ("## 1. HiBit Uninstaller"). E de onde vem o nome quando a
        # tabela nao tem a coluna do concorrente.
        if ($linha -match '^##\s*\d+\.\s*(.+?)\s*$') { $secaoAtual = $Matches[1].Trim(); continue }

        if ($linha -notmatch '^\|') { continue }
        if ($linha -match '^\|\s*-') { continue }
        if ($linha -match '^\|\s*#') { continue }

        $celulas = $linha.Trim('|').Split('|')
        if ($celulas.Count -lt 4) { continue }

        # A coluna do competidor existe quando o SEGUNDO campo e o numero.
        # O primeiro campo e o nome do competidor; sem ele, o primeiro e o
        # numero da funcionalidade.
        $temColunaConcorrente = ($celulas.Count -ge 5) -and ($celulas[1].Trim() -match '^\d+$')

        if ($temColunaConcorrente) {
            if ($celulas.Count -lt 5) { continue }
            $numero = [int]$celulas[1].Trim()
            $estadoTexto = $celulas[3].Trim()
            $evidencia = $celulas[4].Trim()
            $nome = $celulas[0].Trim()
        }
        else {
            # Sem coluna do competidor: o primeiro campo e o numero. Uma
            # linha que nao comeca com numero e cabecalho (ou outra tabela).
            if ($celulas[0].Trim() -notmatch '^\d+$') { continue }
            $numero = [int]$celulas[0].Trim()
            $estadoTexto = $celulas[2].Trim()
            $evidencia = $celulas[3].Trim()
            $nome = $secaoAtual
        }

        if (-not $nome) { continue }
        if ($nome -match '^(Funcionalidade|Estado|Evidencia|Evidência)') { continue }

        $chave = $null
        foreach ($k in $mapear.Keys) {
            if ($estadoTexto -like "$k*") { $chave = $k; break }
        }
        if (-not $chave) { continue }

        $alvo = $null
        # O titulo da secao e o nome do produto ("HiBit Uninstaller"), e o
        # catalogo usa a chave ("HIBIT"). A comparacao ignora espacos e
        # caixa: e a mesma chave dos dois lados.
        $chaveNormalizada = ($nome -replace '\s', '').ToUpperInvariant()

        foreach ($c in $catalogo.concorrentes) {
            $chaveCatalogo = ($c.concorrente -replace '\s', '').ToUpperInvariant()
            if ($chaveCatalogo.Contains($chaveNormalizada) -or $chaveNormalizada.Contains($chaveCatalogo)) {
                $alvo = $c
                break
            }
        }
        if (-not $alvo) { continue }

        foreach ($f in $alvo.funcionalidades) {
            if ([int]$f.numero -eq $numero) {
                # O estado reconciliado passa a ser o estado do catalogo: e
                # dele que sai a contagem de cobertura.
                $f.estado = $mapear[$chave]
                $f.evidenciaCodigo = $evidencia
                $apuracoes++
                break
            }
        }
    }
}

# ------------------------------------------------------------------
# 3. O Revo Pro (catalogo proprio: a secao 7.1)
# ------------------------------------------------------------------
$revo = $null
if (Test-Path -LiteralPath $revoPath) {
    $revo = Get-Content -LiteralPath $revoPath -Raw -Encoding UTF8 | ConvertFrom-Json
}

# ------------------------------------------------------------------
# 4. Tabela final
# ------------------------------------------------------------------
$linhas = New-Object System.Collections.Generic.List[string]
$linhas.Add('# Prova de cobertura por concorrente')
$linhas.Add('')
$linhas.Add('Gerado por `tools\auditoria\provar-paridade.ps1`. Cada linha diz o que o')
$linhas.Add('concorrente tem e o que o Tranal tem, **conferido no codigo**.')
$linhas.Add('')
$linhas.Add('Estado: `OK` o Tranal tem · `AUSENTE` falta · `FORA` limite declarado ·')
$linhas.Add('`NAO CONFIRMADO` o dump do concorrente nao permite afirmar.')
$linhas.Add('')
$linhas.Add('| Concorrente | Funcionalidades medidas | OK | Ausente | Fora | Nao confirmado | Cobertura |')
$linhas.Add('|---|---:|---:|---:|---:|---:|---:|')

$resumo = @()
$grandTotal = 0
$grandOk = 0
$grandAus = 0
$grandFora = 0
$grandNc = 0

foreach ($c in $catalogo.concorrentes) {
    $total = @($c.funcionalidades).Count
    $ok = @($c.funcionalidades | Where-Object { $_.estado -eq 'OK' }).Count
    $aus = @($c.funcionalidades | Where-Object { $_.estado -eq 'AUSENTE' }).Count
    $fora = @($c.funcionalidades | Where-Object { $_.estado -eq 'FORA' }).Count
    $nc = @($c.funcionalidades | Where-Object { $_.estado -eq 'NAO CONFIRMADO' }).Count

    $cobertura = if ($total -gt 0) { [math]::Round(100.0 * ($ok + $fora) / $total, 1) } else { 0 }

    $linhas.Add("| $($c.concorrente) | $total | $ok | $aus | $fora | $nc | $cobertura% |")

    $resumo += [pscustomobject]@{
        Concorrente = $c.concorrente
        Total = $total
        OK = $ok
        Ausente = $aus
        Fora = $fora
        NaoConfirmado = $nc
        Cobertura = $cobertura
    }

    $grandTotal += $total
    $grandOk += $ok
    $grandAus += $aus
    $grandFora += $fora
    $grandNc += $nc
}

if ($revo) {
    $total = @($revo.linhas).Count
    $ok = @($revo.linhas | Where-Object { $_.status -eq 'OK' }).Count
    $aus = @($revo.linhas | Where-Object { $_.status -eq 'AUSENTE' }).Count
    $fora = @($revo.linhas | Where-Object { $_.status -eq 'FORA' }).Count
    $nc = @($revo.linhas | Where-Object { $_.status -eq 'PARCIAL' }).Count
    $cobertura = if ($total -gt 0) { [math]::Round(100.0 * ($ok + $fora) / $total, 1) } else { 0 }

    $linhas.Add("| REVO PRO 5.5.4 | $total | $ok | $aus | $fora | $nc | $cobertura% |")

    $resumo += [pscustomobject]@{
        Concorrente = 'REVO PRO 5.5.4'
        Total = $total
        OK = $ok
        Ausente = $aus
        Fora = $fora
        NaoConfirmado = $nc
        Cobertura = $cobertura
    }

    $grandTotal += $total
    $grandOk += $ok
    $grandAus += $aus
    $grandFora += $fora
    $grandNc += $nc
}

$coberturaGeral = if ($grandTotal -gt 0) { [math]::Round(100.0 * ($grandOk + $grandFora) / $grandTotal, 1) } else { 0 }
$linhas.Add("| **TOTAL** | **$grandTotal** | **$grandOk** | **$grandAus** | **$grandFora** | **$grandNc** | **$coberturaGeral%** |")
$linhas.Add('')
$linhas.Add("Apuracoes linha a linha aplicadas: $apuracoes.")
$linhas.Add('')

# ------------------------------------------------------------------
# 5. O que ainda falta, por concorrente
# ------------------------------------------------------------------
$linhas.Add('## O que ainda falta (estado `AUSENTE`)')
$linhas.Add('')

foreach ($c in $catalogo.concorrentes) {
    $faltando = @($c.funcionalidades | Where-Object { $_.estado -eq 'AUSENTE' })
    if ($faltando.Count -eq 0) { continue }

    $linhas.Add("### $($c.concorrente) — $($faltando.Count)")
    $linhas.Add('')
    $linhas.Add('| # | Funcionalidade | Verificado |')
    $linhas.Add('|---|---|---|')
    foreach ($f in $faltando) {
        $v = if ($f.verificado) { $f.verificado } else { 'nao apurado' }
        $linhas.Add("| $($f.numero) | $($f.funcionalidade) | $v |")
    }
    $linhas.Add('')
}

[System.IO.File]::WriteAllText($saida, ($linhas -join "`r`n"), (New-Object System.Text.UTF8Encoding($false)))

Write-Output ("Prova gerada: {0}" -f $saida)
Write-Output ("Total: {0} funcionalidades  OK {1}  AUSENTE {2}  FORA {3}  nao confirmado {4}  cobertura {5}%" -f `
    $grandTotal, $grandOk, $grandAus, $grandFora, $grandNc, $coberturaGeral)

$resumo | Sort-Object Cobertura | ForEach-Object {
    Write-Output ("  {0,-18} {1,4} measuradas | cobertura {2,5}% | faltam {3}" -f $_.Concorrente, $_.Total, $_.Cobertura, $_.Ausente)
}
