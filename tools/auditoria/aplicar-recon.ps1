param(
    [Parameter(Mandatory = $true)]
    [string]$Recon,

    [Parameter(Mandatory = $true)]
    [string]$Catalogo,

    [Parameter(Mandatory = $true)]
    [string]$Secao
)

$ErrorActionPreference = 'Stop'

# Aplica a reconciliacao medida no catalogo canonico do concorrente.
#
# O relatorio de reconciliacao (RECON-*.md) e a fonte da verdade: ele foi
# conferido contra o codigo, linha a linha. O catalogo (docs/auditoria/<C>.md)
# tem a coluna "Tranal" com o estado medido — e e essa coluna que a prova de
# paridade le. Sem aplicar a reconciliacao, a prova mede o estado preliminar.
#
# O CASAMENTO e por CONTEUDO, e nao por numero: o numero do relatorio e o da
# lista de lacunas (BINARIO-<C>.md), que tem a sua propria numeracao. Casar
# por numero colocaria o estado de uma funcionalidade no lugar de outra —
# que e medir errado com aparencia de medir certo.
#
# A regra e uma so: o estado vem do relatorio; o texto vem do relatorio; o
# resto da linha do catalogo (numero, funcionalidade, evidencia do binario)
# nao e tocado.

# Os caminhos sao resolvidos a partir da raiz do repositorio: o .NET
# resolve caminho relativo pelo diretorio do PROCESSO, e nao pelo diretorio
# em que o script foi chamado — e sao coisas diferentes quando o script roda
# de dentro de outro script.
$raiz = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))

if (-not [System.IO.Path]::IsPathRooted($Recon)) { $Recon = Join-Path $raiz $Recon }
if (-not [System.IO.Path]::IsPathRooted($Catalogo)) { $Catalogo = Join-Path $raiz $Catalogo }

$mapaEstado = @{
    'JA EXISTE'               = 'OK'
    'NAO EXISTE'              = 'AUSENTE'
    'FORA POR DECISAO'        = 'FORA POR DECISAO'
    'PARCIAL'                 = 'PARCIAL'
}

# ------------------------------------------------------------------
# 1. Ler o relatorio
# ------------------------------------------------------------------

if (-not (Test-Path $Recon)) { throw "Relatorio nao encontrado: $Recon" }
if (-not (Test-Path $Catalogo)) { throw "Catalogo nao encontrado: $Catalogo" }

$linhasRelatorio = [System.IO.File]::ReadAllLines($Recon, [System.Text.Encoding]::UTF8)

# As linhas reconciliadas da secao pedida.
$reconLines = @()
$dentroDaSecao = $false

foreach ($linha in $linhasRelatorio) {
    # A secao do relatorio diz de qual concorrente a tabela e
    # ("## 1. HiBit Uninstaller").
    if ($linha -match '^##\s*\d+\.\s*(.+?)\s*$') {
        $dentroDaSecao = $matches[1].Trim() -match [regex]::Escape($Secao)
        continue
    }

    if (-not $dentroDaSecao) { continue }
    if ($linha -notmatch '^\|\s*(\d+)\s*\|') { continue }

    # O numero e guardado na hora: um "-match" depois sobrescreve $matches.
    $numero = [int]$matches[1]

    $celulas = $linha -split '\|'
    if ($celulas.Count -lt 5) { continue }

    $funcionalidade = $celulas[2].Trim()
    $estado = $celulas[3].Trim()
    $evidencia = $celulas[4].Trim()

    if (-not $mapaEstado.ContainsKey($estado)) { continue }

    # "JA EXISTE" com nota de parte nao coberta e parcial, nao inteiro.
    $rotulo = $mapaEstado[$estado]
    if ($rotulo -eq 'OK' -and $evidencia -match '\*\*Nota\*\*') { $rotulo = 'PARCIAL' }

    $reconLines += @{
        Numero         = $numero
        Funcionalidade = $funcionalidade
        Rotulo         = $rotulo
        Evidencia      = $evidencia
    }
}


if ($reconLines.Count -eq 0) {
    Write-Output "Nenhuma linha reconciliada encontrada em $Recon (secao '$Secao')"
    exit 0
}

# ------------------------------------------------------------------
# 2. Localizar a secao funcional do catalogo
# ------------------------------------------------------------------

$linhasCatalogo = [System.IO.File]::ReadAllLines($Catalogo, [System.Text.Encoding]::UTF8)

# A secao "O que o Tranal NAO faz hoje" tem numeracao propria, comecando
# de novo. As linhas do relatorio se referem a tabela de funcionalidades.
$limite = $linhasCatalogo.Count
for ($i = 0; $i -lt $linhasCatalogo.Count; $i++) {
    if ($linhasCatalogo[$i] -match '^##\s*\d+\.\s*O que o Tranal') {
        $limite = $i
        break
    }
}

# ------------------------------------------------------------------
# 3. Casar por conteudo
# ------------------------------------------------------------------

$palavrasComum = @('de', 'do', 'da', 'e', 'a', 'o', 'para', 'por', 'com', 'em', 'na', 'no', 'que', 'se', 'ao', 'os', 'as', 'um', 'uma', 'the', 'of')

function Palavras([string]$texto) {
    if ($null -eq $texto) { return @() }

    $limpo = $texto.ToLowerInvariant()
    $limpo = $limpo -replace '\*', ''
    $limpo = $limpo -replace '[^\p{L}\p{N}]+', ' '

    $palavras = @()
    foreach ($p in ($limpo -split '\s+')) {
        if ($p.Length -lt 3) { continue }
        if ($palavrasComum -contains $p) { continue }
        $palavras += $p
    }

    return $palavras
}

# A frequencia de cada palavra no catalogo: uma palavra que aparece em
# poucas linhas diz muito mais do que uma que aparece em trinta.
#
# E o que separa "Deteccao de sobras por TypeLib" (que e sobre TypeLib)
# de "Remocao de programas SEM desinstalador" (que so tem a palavra
# "sobras" em comum): casar por palavra comum seria medir errado.
$frequencia = @{}
for ($i = 0; $i -lt $limite; $i++) {
    if ($linhasCatalogo[$i] -notmatch '^\|\s*\d+\s*\|') { continue }

    $celulas = $linhasCatalogo[$i] -split '\|'
    if ($celulas.Count -lt 3) { continue }

    foreach ($p in (Palavras $celulas[2])) {
        if ($frequencia.ContainsKey($p)) { $frequencia[$p]++ } else { $frequencia[$p] = 1 }
    }
}

function Peso([string]$palavra) {
    if ($frequencia.ContainsKey($palavra)) { return 1.0 / $frequencia[$palavra] }
    return 1.0
}

function Semelhanca([string]$a, [string]$b) {
    $pa = Palavras $a
    $pb = Palavras $b

    if ($pa.Count -eq 0 -or $pb.Count -eq 0) { return 0.0 }

    $total = 0.0
    foreach ($p in ($pa | Select-Object -Unique)) { $total += Peso $p }

    if ($total -le 0) { return 0.0 }

    $comum = 0.0
    foreach ($p in ($pa | Select-Object -Unique)) {
        if ($pb -contains $p) { $comum += Peso $p }
    }

    return $comum / $total
}

# As linhas do catalogo que podem ser alvo.
$alvos = @()
for ($i = 0; $i -lt $limite; $i++) {
    if ($linhasCatalogo[$i] -notmatch '^\|\s*(\d+)\s*\|') { continue }

    $numero = [int]$matches[1]

    $celulas = $linhasCatalogo[$i] -split '\|'
    if ($celulas.Count -lt 5) { continue }

    $alvos += @{
        Indice         = $i
        Numero         = $numero
        Funcionalidade = $celulas[2].Trim()
        Usado          = $false
    }
}

# Cada linha reconciliada busca a linha do catalogo mais parecida. O
# primeiro casamento que passa do limite vence, e a linha do catalogo
# nao pode ser usada duas vezes.
$limiteSemelhanca = 0.20
$aplicadas = 0
$divergentes = @()

foreach ($linhaRec in $reconLines) {
    $melhor = $null
    $melhorNota = 0.0

    foreach ($alvo in $alvos) {
        if ($alvo.Usado) { continue }

        $nota = Semelhanca $linhaRec.Funcionalidade $alvo.Funcionalidade
        if ($nota -gt $melhorNota) {
            $melhorNota = $nota
            $melhor = $alvo
        }
    }

    if ($melhor -eq $null -or $melhorNota -lt $limiteSemelhanca) {
        $divergentes += "#$($linhaRec.Numero) '$($linhaRec.Funcionalidade)' (nenhuma linha do catalogo com semelhanca acima de $limiteSemelhanca; melhor = $([math]::Round($melhorNota, 2)))"
        continue
    }

    $celulas = $linhasCatalogo[$melhor.Indice] -split '\|'

    # Um pipe dentro do texto quebraria a tabela: a barra vira barra.
    $texto = ($linhaRec.Evidencia -replace '\|', '/')

    $celulas[$celulas.Count - 2] = " **$($linhaRec.Rotulo)** - $texto "
    $linhasCatalogo[$melhor.Indice] = ($celulas -join '|')

    $melhor.Usado = $true
    $aplicadas++
}

[System.IO.File]::WriteAllLines($Catalogo, $linhasCatalogo, (New-Object System.Text.UTF8Encoding($false)))

Write-Output "$([System.IO.Path]::GetFileName($Catalogo)): $aplicadas linha(s) atualizada(s) de $($reconLines.Count) reconciliada(s)."
if ($divergentes.Count -gt 0) {
    Write-Output "  $($divergentes.Count) linha(s) sem correspondencia no catalogo (nao aplicadas):"
    foreach ($d in $divergentes) { Write-Output "      $d" }
}
