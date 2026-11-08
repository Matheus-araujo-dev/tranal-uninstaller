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

# As funcoes compartilhadas moram em UM lugar so: a regra de cabecalho de
# tabela de funcionalidade precisa ser a MESMA no extrator e no aplicador.
# Se os dois discordarem, o ID "T{n>#<numero>" do catalogo nao bate com o do
# mapa de reconciliacao, e a prova de paridade deixa de casar as linhas.
. (Join-Path $scriptDir 'auditoria-comum.ps1')


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

        # Cabecalho de tabela de funcionalidade: uma CELULA com o texto
        # exatamente "Funcionalidade" (regra estrita - ver
        # tools\auditoria\auditoria-comum.ps1).
        if (Eh-CabecalhoFuncionalidade $linha) {
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
