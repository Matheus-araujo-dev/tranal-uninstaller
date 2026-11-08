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
# 2. A reconciliacao (docs\auditoria\reconciliacao.json)
# ------------------------------------------------------------------

# O estado reconciliado vem do MAPA, e nao do RECON lido aqui.
#
# O mapa e gravado por tools\auditoria\aplicar-recon.ps1: cada entrada e o
# par (linha do RECON -> linha do catalogo) com o estado medido. Ler o RECON
# e casar por numero seria medir errado com aparencia de medir certo: o
# numero do RECON e o da lista de lacunas (BINARIO-<C>.md), que tem a sua
# propria numeracao. Medido: o RECON #3 do UNINSTALR (MuiCache) nao e a
# linha 3 do catalogo.
$reconPath = Join-Path $docs 'reconciliacao.json'
$recon = $null
if (Test-Path -LiteralPath $reconPath) {
    $recon = Get-Content -LiteralPath $reconPath -Raw -Encoding UTF8 | ConvertFrom-Json
}

# O rotulo do estado ja vem pronto do mapa: nao ha traducao a fazer aqui.
$mapear = @{
    'OK'               = 'OK'
    'PARCIAL'          = 'PARCIAL'
    'AUSENTE'          = 'AUSENTE'
    'FORA POR DECISAO' = 'FORA'
}

$apuracoes = 0
if ($recon -and $recon.mapa) {
    foreach ($prop in $recon.mapa.PSObject.Properties) {
        $chaveCatalogo = $prop.Name.ToUpperInvariant()

        $alvo = $null
        foreach ($c in $catalogo.concorrentes) {
            if (($c.concorrente -replace '\s', '').ToUpperInvariant() -eq $chaveCatalogo) {
                $alvo = $c
                break
            }
        }
        if (-not $alvo) { continue }

        foreach ($p in @($prop.Value)) {
            $rotulo = $p.estado
            if (-not $mapear.ContainsKey($rotulo)) { continue }

            # O casamento e pelo ID unico da linha do catalogo ("T2#14"),
            # e nao pelo numero: alguns catalogos tem varias tabelas de
            # funcionalidade com a mesma numeracao.
            $idAlvo = $p.catalogoId
            if (-not $idAlvo) { continue }

            foreach ($f in $alvo.funcionalidades) {
                if ($f.id -eq $idAlvo) {
                    $f.estado = $mapear[$rotulo]
                    $f.evidenciaCodigo = $p.evidencia
                    $apuracoes++
                    break
                }
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
$linhas.Add('Estado: `OK` o Tranal tem · `PARCIAL` tem parte · `AUSENTE` falta ·')
$linhas.Add('`FORA` limite declarado · `NAO CONFIRMADO` o dump do concorrente nao permite confirmar.')
$linhas.Add('')
$linhas.Add('A **cobertura** conta `OK` e `FORA` (o que o Tranal tem, ou o que foi')
$linhas.Add('declarado fora por decisao). `PARCIAL` aparece na coluna propria: a')
$linhas.Add('funcionalidade existe, mas nao inteira — por isso ela NAO entra na')
$linhas.Add('cobertura, e por isso o total medido e sempre maior que a soma das')
$linhas.Add('colunas.')
$linhas.Add('')
$linhas.Add('| Concorrente | Funcionalidades medidas | OK | Parcial | Ausente | Fora | Nao confirmado | Cobertura |')
$linhas.Add('|---|---:|---:|---:|---:|---:|---:|---:|')

$resumo = @()
$grandTotal = 0
$grandOk = 0
$grandPar = 0
$grandAus = 0
$grandFora = 0
$grandNc = 0

foreach ($c in $catalogo.concorrentes) {
    $total = @($c.funcionalidades).Count
    $ok = @($c.funcionalidades | Where-Object { $_.estado -eq 'OK' }).Count
    $par = @($c.funcionalidades | Where-Object { $_.estado -eq 'PARCIAL' }).Count
    $aus = @($c.funcionalidades | Where-Object { $_.estado -eq 'AUSENTE' }).Count
    $fora = @($c.funcionalidades | Where-Object { $_.estado -eq 'FORA' }).Count
    $nc = @($c.funcionalidades | Where-Object { $_.estado -eq 'NAO CONFIRMADO' }).Count

    $cobertura = if ($total -gt 0) { [math]::Round(100.0 * ($ok + $fora) / $total, 1) } else { 0 }

    $linhas.Add("| $($c.concorrente) | $total | $ok | $par | $aus | $fora | $nc | $cobertura% |")

    $resumo += [pscustomobject]@{
        Concorrente = $c.concorrente
        Total = $total
        OK = $ok
        Parcial = $par
        Ausente = $aus
        Fora = $fora
        NaoConfirmado = $nc
        Cobertura = $cobertura
    }

    $grandTotal += $total
    $grandOk += $ok
    $grandPar += $par
    $grandAus += $aus
    $grandFora += $fora
    $grandNc += $nc
}

if ($revo) {
    $total = @($revo.linhas).Count
    $ok = @($revo.linhas | Where-Object { $_.status -eq 'OK' }).Count
    $par = @($revo.linhas | Where-Object { $_.status -eq 'PARCIAL' }).Count
    $aus = @($revo.linhas | Where-Object { $_.status -eq 'AUSENTE' }).Count
    $fora = @($revo.linhas | Where-Object { $_.status -eq 'FORA' }).Count
    $nc = @($revo.linhas | Where-Object { $_.status -eq 'NAO CONFIRMADO' }).Count
    $cobertura = if ($total -gt 0) { [math]::Round(100.0 * ($ok + $fora) / $total, 1) } else { 0 }

    $linhas.Add("| REVO PRO 5.5.4 | $total | $ok | $par | $aus | $fora | $nc | $cobertura% |")

    $resumo += [pscustomobject]@{
        Concorrente = 'REVO PRO 5.5.4'
        Total = $total
        OK = $ok
        Parcial = $par
        Ausente = $aus
        Fora = $fora
        NaoConfirmado = $nc
        Cobertura = $cobertura
    }

    $grandTotal += $total
    $grandOk += $ok
    $grandPar += $par
    $grandAus += $aus
    $grandFora += $fora
    $grandNc += $nc
}

$coberturaGeral = if ($grandTotal -gt 0) { [math]::Round(100.0 * ($grandOk + $grandFora) / $grandTotal, 1) } else { 0 }
$linhas.Add("| **TOTAL** | **$grandTotal** | **$grandOk** | **$grandPar** | **$grandAus** | **$grandFora** | **$grandNc** | **$coberturaGeral%** |")
$linhas.Add('')
$linhas.Add("Apuracoes linha a linha aplicadas: $apuracoes.")
$linhas.Add('')

# ------------------------------------------------------------------
# 5. O que ainda falta, por concorrente
# ------------------------------------------------------------------
$linhas.Add('## O que ainda falta (estado AUSENTE)')
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
