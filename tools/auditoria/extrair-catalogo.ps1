# Extrai o catalogo de funcionalidades dos 11 concorrentes para um dado unico.
#
# Uso: powershell -NoProfile -ExecutionPolicy Bypass -File tools\auditoria\extrair-catalogo.ps1
#
# Cada documento canonico (docs\auditoria\<CONCORRENTE>.md) tem tabelas de
# funcionalidades com o estado no Tranal. Este script junta todas em
# docs\auditoria\catalogo-concorrentes.json, que e a fonte da prova de
# cobertura por concorrente.
#
# A leitura e por cabecalho: cada tabela declara onde esta a coluna
# "Tranal" e a coluna de evidencia, porque os documentos nao usam a mesma
# quantidade de colunas em todas as tabelas.
$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = (Resolve-Path (Join-Path $scriptDir '..\..')).Path
$docs = Join-Path $repo 'docs\auditoria'
$saida = Join-Path $docs 'catalogo-concorrentes.json'

# O nome do arquivo (sem extensao) e o nome do concorrente.
#
# Fora do catalogo ficam os documentos que NAO sao o catalogo de um
# concorrente: as listas de lacunas binarias (BINARIO-*), a matriz, os
# relatorios de reconciliacao (RECON-*) e a prova de paridade — esta ultima
# e GERADA a partir do catalogo, e entra-la como fonte seria circular.
$concorrentes = Get-ChildItem $docs -Filter '*.md' |
    Where-Object { $_.Name -notmatch '^(BINARIO|README|MATRIZ|RECON|PARIDADE)' } |
    ForEach-Object { $_.BaseName }

function Dividir-Celulas {
    param([string]$Linha)

    $partes = New-Object System.Collections.Generic.List[string]
    $atual = New-Object System.Text.StringBuilder
    $dentro = $false
    $tamanhoCerca = 0
    $dentroAspas = $false

    # Uma contagem IMPAR de crases ou de aspas indica um escape defeituoso
    # no documento (uma crase dentro de um texto de codigo, por exemplo).
    # Com o escape quebrado, o escopo nao fecha e a coluna seguinte seria
    # engolida. Nesse caso o escopo daquela marca e desativado: o pipe
    # volta a separar coluna, que e o comportamento correto para uma
    # evidencia que nao tem pipe dentro.
    $crasesImpares = (([regex]::Matches($Linha, '`')).Count % 2) -ne 0
    $aspasImpares = (([regex]::Matches($Linha, '"')).Count % 2) -ne 0

    for ($i = 0; $i -lt $Linha.Length; $i++) {
        $c = $Linha[$i]

        # O pipe so separa coluna FORA de crase e FORA de aspas duplas.
        # As evidencias trazem trechos como 'literal "PowerShell script|*.ps1"'
        # e 'strings L80711 | ...': sem isso, a coluna da evidencia seria
        # partida ao meio e o estado da funcionalidade seria lido do lugar
        # errado.
        if ($c -eq [char]34 -and -not $aspasImpares) { $dentroAspas = -not $dentroAspas; [void]$atual.Append($c); continue }

        if ($c -eq [char]96 -and -not $crasesImpares) {
            # A cerca de crase tem um tamanho: um texto que CONTEM crase
            # (o tipo generico do .NET, por exemplo) e escrito com duas
            # crases nas pontas. Contar a crase uma a uma desbalancearia a
            # linha e a coluna seguinte seria engolida.
            $tamanho = 0
            while ($i -lt $Linha.Length -and $Linha[$i] -eq [char]96) { $tamanho++; $i++ }
            $i--

            if (-not $dentro) {
                $dentro = $true
                $tamanhoCerca = $tamanho
            }
            elseif ($tamanho -eq $tamanhoCerca) {
                $dentro = $false
                $tamanhoCerca = 0
            }

            [void]$atual.Append(('`' * $tamanho))
            continue
        }

        if ($c -eq '|' -and -not $dentro -and -not $dentroAspas) { $partes.Add($atual.ToString()); [void]$atual.Clear(); continue }
        [void]$atual.Append($c)
    }

    $partes.Add($atual.ToString())
    return $partes
}

function Classificar {
    param([string]$Celula)

    $t = ($Celula -replace '\*', '').Trim()

    if ($t -match '^(OK)\b') { return 'OK' }
    if ($t -match '^(PARCIAL)\b') { return 'PARCIAL' }
    if ($t -match '^(AUSENTE)\b') { return 'AUSENTE' }
    if ($t -match '^(FORA)\b') { return 'FORA' }
    if ($t -match '^(feita)\b') { return 'OK' }

    return 'REVISAR'
}

$saidas = @()

foreach ($concorrente in $concorrentes) {
    $arquivo = Join-Path $docs "$concorrente.md"
    if (-not (Test-Path -LiteralPath $arquivo)) { continue }

    $linhas = Get-Content -LiteralPath $arquivo -Encoding UTF8
    $itens = @()

    $i = 0
    $indiceTabela = 0
    while ($i -lt $linhas.Count) {
        $linha = $linhas[$i]

        if ($linha -match '^\|' -and $linha -match 'Funcionalidade') {
            # Cabecalho: guarda as posicoes das colunas uteis.
            $cabecalho = $linha.Trim('|').Split('|') | ForEach-Object { $_.Trim() }
            $colFunc = -1
            $colEvid = -1
            $colTranal = -1

            for ($c = 0; $c -lt $cabecalho.Count; $c++) {
                $h = $cabecalho[$c]
                if ($h -match 'Funcionalidade') { $colFunc = $c }
                elseif ($h -match 'Evid') { $colEvid = $c }
                elseif ($h -match 'Tranal') { $colTranal = $c }
            }

            $indiceTabela++

            $i++
            while ($i -lt $linhas.Count -and $linhas[$i] -match '^\|') {
                $corpo = $linhas[$i].Trim('|')

                # Quebra por | que nao esteja dentro de crase: as evidencias
                # trazem trechos com | (por exemplo "strings L80711 | ...").
                $partes = @(Dividir-Celulas $corpo | ForEach-Object { $_.Trim() })

                if ($partes.Count -gt 2 -and $partes[0] -match '^\d+$') {
                    $func = if ($colFunc -ge 0 -and $colFunc -lt $partes.Count) { $partes[$colFunc] } else { $partes[1] }
                    $evid = if ($colEvid -ge 0 -and $colEvid -lt $partes.Count) { $partes[$colEvid] } else { '' }
                    $tran = if ($colTranal -ge 0 -and $colTranal -lt $partes.Count) { $partes[$colTranal] } else { '' }

                    $itens += [pscustomobject]@{
                        numero = [int]$partes[0]
                        # O ID unico da linha: "<tabela>#<numero>". O numero
                        # sozinho nao identifica a linha, porque alguns
                        # catalogos (IOBIT, HIBIT) tem varias tabelas de
                        # funcionalidade com a mesma numeracao. E o mesmo ID
                        # que o aplicador (aplicar-recon.ps1) grava no mapa
                        # de reconciliacao.
                        id = "T$indiceTabela#$([int]$partes[0])"
                        funcionalidade = $func
                        evidencia = $evid
                        estado = Classificar $tran
                        estadoTexto = $tran
                    }
                }

                $i++
            }
            continue
        }

        $i++
    }

    if ($itens.Count -gt 0) {
        $saidas += [pscustomobject]@{
            concorrente = $concorrente
            documento = "docs/auditoria/$concorrente.md"
            total = $itens.Count
            funcionalidades = $itens
        }
    }
}

$doc = [pscustomobject]@{
    origem = 'docs\auditoria\<CONCORRENTE>.md (catalogo canonico de cada concorrente)'
    geradoPor = 'tools\auditoria\extrair-catalogo.ps1'
    concorrentes = $saidas
}

$json = $doc | ConvertTo-Json -Depth 8
[System.IO.File]::WriteAllText($saida, $json, (New-Object System.Text.UTF8Encoding($false)))

$total = ($saidas | Measure-Object -Property total -Sum).Sum
Write-Output ("Catalogo extraido: {0} concorrentes, {1} funcionalidades" -f $saidas.Count, $total)

foreach ($c in $saidas) {
    $ok = @($c.funcionalidades | Where-Object { $_.estado -eq 'OK' }).Count
    $par = @($c.funcionalidades | Where-Object { $_.estado -eq 'PARCIAL' }).Count
    $aus = @($c.funcionalidades | Where-Object { $_.estado -eq 'AUSENTE' }).Count
    $rev = @($c.funcionalidades | Where-Object { $_.estado -eq 'REVISAR' }).Count
    Write-Output ("  {0,-18} total {1,4}  OK {2,4}  PARCIAL {3,4}  AUSENTE {4,4}  REVISAR {5,3}" -f $c.concorrente, $c.total, $ok, $par, $aus, $rev)
}