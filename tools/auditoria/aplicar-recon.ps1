param(
    [Parameter(Mandatory = $true)]
    [string]$Recon,

    [Parameter(Mandatory = $true)]
    [string]$Catalogo,

    [Parameter(Mandatory = $true)]
    [string]$Secao,

    # As ancoras explicitas: "RECON #N" do relatorio aponta para a linha "N"
    # do catalogo. E o desempate medido: quando o texto nao casa (o relatorio
    # e o catalogo escrevem a mesma funcionalidade com palavras diferentes),
    # a ancora e a unica forma de casar sem inventar.
    [string]$Ancoras = 'docs\auditoria\ancoras-recon.csv'
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
if (-not [System.IO.Path]::IsPathRooted($Ancoras)) { $Ancoras = Join-Path $raiz $Ancoras }

# As funcoes compartilhadas moram em UM lugar so: a regra de cabecalho de
# tabela de funcionalidade precisa ser a MESMA no extrator e no aplicador.
# Se os dois discordarem, o ID "T{n>#<numero>" do catalogo nao bate com o do
# mapa de reconciliacao, e a prova de paridade deixa de casar as linhas.
. (Join-Path $raiz 'tools\auditoria\auditoria-comum.ps1')

$mapaEstado = @{
    'JA EXISTE'               = 'OK'
    'NAO EXISTE'              = 'AUSENTE'
    'FORA POR DECISAO'        = 'FORA POR DECISAO'
    'PARCIAL'                 = 'PARCIAL'
    'OK'                      = 'OK'
    'AUSENTE'                 = 'AUSENTE'
    'FORA'                    = 'FORA POR DECISAO'
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

    # A coluna "#" do relatorio pode trazer o numero da linha do catalogo
    # (relatorios antigos) ou o ID unico "T{tabela}#{numero}" (relatorios
    # novos, como o RECON-CATALOGO-HIBIT-GEEK.md). As duas formas sao
    # aceitas: com o ID, o casamento passa a ser exato.
    if ($linha -notmatch '^\|\s*(\d+|T\d+#\d+)\s*\|') { continue }

    # O numero e guardado na hora: um "-match" depois sobrescreve $matches.
    $celulaNumero = $matches[1].Trim()

    # As celulas saem pelo MESMO divisor do catalogo (o pipe dentro de crase
    # ou de aspas nao separa coluna). Sem isso, a evidencia de um relatorio
    # com pipe escapado era cortada na primeira barra: o texto chegava
    # truncado ao catalogo e a linha Markdown perdia uma coluna.
    $celulas = @(Dividir-Celulas $linha | ForEach-Object { $_.Trim() })
    if ($celulas.Count -lt 5) { continue }

    # A ultima celula util e a ultima antes da barra final (ou a ultima de
    # todas, quando a linha nao termina em barra).
    $ultima = $celulas.Count - 1
    if ($celulas[$ultima] -eq '') { $ultima-- }

    # O estado e reconhecido pelo TEXTO, e nao pela posicao: os relatorios
    # tem tres formatos — "| # | Funcionalidade | Estado | Evidencia |",
    # "| # | Funcionalidade | O que existe | Estado | Evidencia |" e
    # "| # | Estado | Funcionalidade | Evidencia |". O que decide e a celula
    # cujo texto e um estado conhecido.
    #
    # A busca comeca na coluna 2 porque o estado pode estar logo depois do
    # numero: MEDIDO, o RECON-D-2026-11-08.md escreve o estado nessa posicao,
    # e a busca que comecava na coluna 3 ignorava o arquivo inteiro.
    $indiceEstado = -1
    foreach ($i in 1..$ultima) {
        if ($mapaEstado.ContainsKey($celulas[$i])) { $indiceEstado = $i; break }
    }
    if ($indiceEstado -lt 0) { continue }

    $funcionalidade = $celulas[2]
    $estado = $celulas[$indiceEstado]

    # A evidencia e a ultima coluna: as celulas restantes sao remontadas com
    # a barra, para nao perder texto quando o relatorio traz pipe dentro da
    # evidencia (o texto truncado chegava ao catalogo pela metade).
    $evidencia = ''
    if (($indiceEstado + 1) -le $ultima) {
        $evidencia = (($celulas[($indiceEstado + 1)..$ultima]) -join '|').Trim()
    }

    if (-not $mapaEstado.ContainsKey($estado)) { continue }

    # "JA EXISTE" com nota de parte nao coberta e parcial, nao inteiro.
    $rotulo = $mapaEstado[$estado]
    if ($rotulo -eq 'OK' -and $evidencia -match '\*\*Nota\*\*') { $rotulo = 'PARCIAL' }

    # O relatorio pode trazer o ID do catalogo na coluna "#" (no formato
    # "T2#14"). Quando traz, o casamento e exato e nao precisa de similaridade
    # nem de ancora: e o proprio relatorio apontando a linha.
    $idCatalogo = ''
    $numero = 0
    if ($celulaNumero -match '^T\d+#\d+$') {
        $idCatalogo = $celulaNumero
        $numero = [int](($celulaNumero -split '#')[1])
    }
    else {
        $numero = [int]$celulaNumero
    }

    $reconLines += @{
        Numero         = $numero
        IdCatalogo     = $idCatalogo
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
# 3. As ancoras explicitas
# ------------------------------------------------------------------

# O casamento por conteudo resolve a maioria das linhas, mas nao todas: o
# relatorio e o catalogo as vezes descrevem a mesma funcionalidade com
# palavras diferentes ("Gerenciador proprio de tarefas agendadas" contra
# "Boot Center: ... autorun e tarefas agendadas"). Nesses casos, a ancora
# manual e o que impede que o estado caia na linha errada.
#
# Formato do arquivo: uma linha por ancora, separada por ponto e virgula:
#   <competidor>;<numero do RECON>;<numero do catalogo>;<motivo>
# O numero do RECON e o da tabela do relatorio; o do catalogo e o da coluna
# "#" do catalogo canonico.
$mapaAncoras = @{}

if (Test-Path $Ancoras) {
    $linhasAncora = [System.IO.File]::ReadAllLines($Ancoras, [System.Text.Encoding]::UTF8)
    foreach ($linhaAncora in $linhasAncora) {
        $t = $linhaAncora.Trim()
        if ($t.Length -eq 0) { continue }
        if ($t.StartsWith('#')) { continue }
        if ($t.StartsWith('//')) { continue }
        if ($t.StartsWith(';')) { continue }

        $partes = $t -split ';'
        if ($partes.Count -lt 3) { continue }

        $chave = "$($partes[0].Trim())|$($partes[1].Trim())"
        $mapaAncoras[$chave] = @{
            Catalogo = $partes[2].Trim()
            Motivo   = if ($partes.Count -ge 4) { $partes[3].Trim() } else { '' }
        }
    }
}

# ------------------------------------------------------------------
# 4. Casar por conteudo
# ------------------------------------------------------------------

$palavrasComum = @(
    'de', 'do', 'da', 'e', 'a', 'o', 'para', 'por', 'com', 'em', 'na', 'no', 'que', 'se',
    'ao', 'os', 'as', 'um', 'uma', 'the', 'of', 'nao', 'sem', 'so', 'dos', 'das', 'mais',
    'menos', 'cada', 'pelo', 'pela', 'seu', 'sua', 'isso', 'isto', 'como', 'onde',
    'quando', 'todo', 'toda', 'todos', 'todas', 'outro', 'outra', 'ser', 'ter', 'ver',
    'dar', 'sao', 'foi', 'sim', 'na', 'nas', 'nos', 'das', 'pelo', 'esta', 'este',
    'e/ou', 'etc', 'via', 'ainda', 'apos', 'entre', 'sobre', 'ate', 'desde'
)

# Sufixos cortados para chegar na raiz da palavra. A raiz e o que permite
# casar "listaveis" com "listar" e "comparacao" com "comparar": sem isso, o
# casamento por conteudo falhava em funcionalidades que o relatorio e o
# catalogo descrevem com palavras diferentes — e a reconciliacao ficava sem
# aplicar mesmo quando a capacidade existia.
#
# A ordem importa: o sufixo mais longo e testado primeiro, e UM sufixo e
# cortado por palavra (a raiz nao vira radical de radical).
$sufixos = @(
    'mente', 'aveis', 'iveis', 'eiras', 'eiro', 'eiros', 'adores', 'adoras',
    'ador', 'edor', 'idor', 'acao', 'acoes', 'ados', 'adas', 'ado', 'ada',
    'ido', 'ida', 'ico', 'ica', 'oes', 'ao', 'ar', 'er', 'ir', 'im', 's'
)

function SemAcento([string]$texto) {
    if ($null -eq $texto) { return '' }

    $normalizado = $texto.Normalize([System.Text.NormalizationForm]::FormD)
    $sb = New-Object System.Text.StringBuilder
    foreach ($c in $normalizado.ToCharArray()) {
        $categoria = [System.Globalization.CharUnicodeInfo]::GetUnicodeCategory($c)
        if ($categoria -ne [System.Globalization.UnicodeCategory]::NonSpacingMark) {
            [void]$sb.Append($c)
        }
    }

    return $sb.ToString().Normalize([System.Text.NormalizationForm]::FormC)
}

function Raiz([string]$palavra) {
    $p = $palavra
    foreach ($s in $sufixos) {
        # Depois do corte ainda precisam sobrar 4 letras: e o que impede que
        # "os" vire "o" e que palavras curtas virem ruido.
        if ($p.Length -ge ($s.Length + 4) -and $p.EndsWith($s, [System.StringComparison]::Ordinal)) {
            $p = $p.Substring(0, $p.Length - $s.Length)
            break
        }
    }

    return $p
}

function Palavras([string]$texto) {
    if ($null -eq $texto) { return @() }

    $limpo = SemAcento $texto
    $limpo = $limpo.ToLowerInvariant()
    $limpo = $limpo -replace '\*', ''
    $limpo = $limpo -replace '[^\p{L}\p{N}]+', ' '

    $palavras = @()
    foreach ($p in ($limpo -split '\s+')) {
        if ($p.Length -lt 3) { continue }
        if ($palavrasComum -contains $p) { continue }
        $palavras += (Raiz $p)
    }

    return $palavras
}

# A frequencia de cada palavra no catalogo: uma palavra que aparece em
# poucas linhas diz muito mais do que uma que aparece em trinta.
#
# E o que separa "Deteccao de sobras por TypeLib" (que e sobre TypeLib)
# de "Remocao de programas SEM desinstalador" (que so tem a palavra
# "sobras" em comum): casar por palavra comum seria medir errado.
#
# A frequencia e calculada sobre os MESMOS alvos do casamento (as linhas de
# funcionalidade), e calculada DEPOIS deles: a lista de alvos e a unica
# fonte, e nao a varredura solta das linhas numeradas.
$frequencia = @{}

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
#
# So entram as linhas das tabelas de FUNCIONALIDADE — as mesmas que o
# extrator (extrair-catalogo.ps1) le. Sem esse filtro, uma linha de outra
# tabela (por exemplo a tabela de switches do HIBIT, que tambem tem a
# coluna "#") entrava como alvo com o mesmo numero de uma funcionalidade
# real, e o estado medido caia na linha errada.
#
# Cada alvo recebe um ID unico: "<tabela>#<numero>". O numero sozinho nao
# identifica a linha, porque alguns catalogos (IOBIT, HIBIT) tem varias
# tabelas de funcionalidade com a mesma numeracao. E o ID que a ancora
# aponta — assim a ancora nunca depende de adivinhar qual tabela e.
$alvos = @()
$dentroTabela = $false
$indiceTabela = 0
for ($i = 0; $i -lt $limite; $i++) {
    $linha = $linhasCatalogo[$i]

    if ($linha -match '^\|') {
        # Cabecalho de tabela de funcionalidade: uma CELULA com o texto
        # exatamente "Funcionalidade" (regra estrita - a mesma do extrator,
        # em tools\auditoria\auditoria-comum.ps1). A regra antiga ("a linha
        # contem a palavra Funcionalidade") contava linha de DADOS de outra
        # tabela e deslocava o ID "T{n>#<numero>" de todas as linhas.
        if (Eh-CabecalhoFuncionalidade $linha) {
            if (-not $dentroTabela) { $indiceTabela++ }
            $dentroTabela = $true
            continue
        }
        if ($linha -match '^\|\s*-') { continue }
    }
    else {
        $dentroTabela = $false
        continue
    }

    if (-not $dentroTabela) { continue }
    if ($linha -notmatch '^\|\s*(\d+)\s*\|') { continue }

    $numero = [int]$matches[1]

    $celulas = @(Dividir-Celulas $linha | ForEach-Object { $_.Trim() })
    if ($celulas.Count -lt 5) { continue }

    $alvos += @{
        Indice         = $i
        Numero         = $numero
        Id             = "T$indiceTabela#$numero"
        Tabela         = $indiceTabela
        Funcionalidade = $celulas[2]
        Usado          = $false
    }
}

# A frequencia das palavras, agora sobre os alvos ja conhecidos.
foreach ($alvo in $alvos) {
    foreach ($p in (Palavras $alvo.Funcionalidade)) {
        if ($frequencia.ContainsKey($p)) { $frequencia[$p]++ } else { $frequencia[$p] = 1 }
    }
}

# Cada linha reconciliada busca a linha do catalogo mais parecida. O
# primeiro casamento que passa do limite vence, e a linha do catalogo
# nao pode ser usada duas vezes.
#
# A ancora explicita tem prioridade sobre o texto: quando ela existe, o
# estado medido cai na linha que a ancora aponta, e nao na linha "parecida".
# O limite de semelhanca e 0.45 — abaixo disso o casamento por texto erra:
# medido, RECON #6 do Ashampoo ("gerenciador proprio de tarefas agendadas")
# caia na linha do "Autorun / StartUp Tuner" com 0.25, e todas as linhas
# casadas por conteudo com nota boa (>= 0.47) estavam corretas.
$limiteSemelhanca = 0.45
$aplicadas = 0
$divergentes = @()
$pares = @()

foreach ($linhaRec in $reconLines) {
    # A ancora explicita tem prioridade sobre o texto: quando ela existe, o
    # estado medido cai na linha que a ancora aponta, e nao na linha
    # "parecida". E o desempate medido: ha funcionalidades que o relatorio
    # e o catalogo escrevem com palavras diferentes.
    #
    # A ancora pode apontar para VARIAS linhas do catalogo (separadas por
    # virgula): e o caso da funcionalidade que o catalogo registra em duas
    # linhas e o relatorio reconcilia em uma so.
    $alvosAncora = @()
    $chaveAncora = "$Secao|$($linhaRec.Numero)"

    # O caminho do ID e TERMINAL, e o caminho da ancora tambem: os dois
    # apontam a linha exata. MEDIDO: antes, o ID era apenas COLETADO e o
    # estado caia no caminho de similaridade - o caminho do ID exato nunca
    # aplicava nada. Com a regra lax, o ID do relatorio e o do catalogo
    # divergiam, e a similaridade era o unico caminho que restava.
    $via = ''
    $motivo = ''

    if ($linhaRec.IdCatalogo) {
        foreach ($cand in @($alvos | Where-Object { $_.Id -eq $linhaRec.IdCatalogo })) {
            $alvosAncora += $cand
        }

        # Se o ID nao existe no catalogo, relatorio e catalogo estao fora de
        # passo: isso e uma linha sem correspondencia, e nao um caso para
        # "casar por similaridade" (o estado cairia em outra funcionalidade).
        if ($alvosAncora.Count -eq 0) {
            $divergentes += "#$($linhaRec.Numero) '$($linhaRec.Funcionalidade)' (ID do catalogo '$($linhaRec.IdCatalogo)' nao existe em $([System.IO.Path]::GetFileName($Catalogo)))"
            continue
        }

        $via = 'id'
    }
    elseif ($mapaAncoras.ContainsKey($chaveAncora)) {
        $motivo = $mapaAncoras[$chaveAncora].Motivo

        foreach ($n in ($mapaAncoras[$chaveAncora].Catalogo -split ',')) {
            $referencia = $n.Trim()

            # A ancora pode apontar pelo ID unico ("T2#14") ou pelo numero
            # ("14"). O ID e o preciso: o numero sozinho nao diz em qual
            # tabela a linha esta, e alguns catalogos repetem a numeracao.
            $candidatos = @($alvos | Where-Object { $_.Id -eq $referencia })
            if ($candidatos.Count -eq 0) {
                # A ancora pode apontar pelo ID unico ("T2#14") ou pelo numero
                # ("14"). Um ID que nao e numero nao vira numero:
                # [int]"T1#1" lancaria excecao e derrubaria a execucao.
                $numeroAlvo = 0
                if ([int]::TryParse($referencia, [ref]$numeroAlvo)) {
                    $candidatos = @($alvos | Where-Object { $_.Numero -eq $numeroAlvo })
                }
            }
            if ($candidatos.Count -eq 0) { continue }

            $escolhido = $candidatos[0]
            $melhorNotaAncora = -1.0
            foreach ($cand in $candidatos) {
                $nota = Semelhanca $linhaRec.Funcionalidade $cand.Funcionalidade
                if ($nota -gt $melhorNotaAncora) {
                    $melhorNotaAncora = $nota
                    $escolhido = $cand
                }
            }

            $alvosAncora += $escolhido
        }

        if ($alvosAncora.Count -gt 0) { $via = 'ancora' }
    }

    # A linha apontada pelo ID ou pela ancora recebe o estado medido.
    if ($alvosAncora.Count -gt 0) {
        foreach ($destino in $alvosAncora) {
            # A linha do catalogo e dividida pelo MESMO divisor do
            # relatorio: o pipe dentro de crase nao separa coluna. Sem
            # isso, a barra escapada de uma evidencia virava separador e a
            # linha do catalogo perdia a estrutura - a coluna Tranal recebia
            # o texto errado.
            $celulas = @(Dividir-Celulas $linhasCatalogo[$destino.Indice])

            # Um pipe dentro do texto quebraria a tabela: a barra vira barra.
            $texto = ($linhaRec.Evidencia -replace '\\\|', '|' -replace '\|', '/')

            $celulas[$celulas.Count - 2] = " **$($linhaRec.Rotulo)** - $texto "
            $linhasCatalogo[$destino.Indice] = ($celulas -join '|')

            $destino.Usado = $true
            $aplicadas++

            $pares += [pscustomobject]@{
                Rec      = $linhaRec.Numero
                Catalogo = $destino.Numero
                CatalogoId = $destino.Id
                Nota     = $via
                ViaAncora = ($via -eq 'ancora')
                Motivo   = $motivo
                FuncRec  = $linhaRec.Funcionalidade
                FuncCat  = $destino.Funcionalidade
                Estado   = $linhaRec.Rotulo
                Evidencia = $linhaRec.Evidencia
            }
        }

        continue
    }

    # Sem ancora: o melhor casamento por conteudo, entre as linhas do
    # catalogo que ainda nao receberam estado.
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

    # A linha do catalogo e dividida pelo MESMO divisor do relatorio (ver
    # a nota da escrita por ancora).
    $celulas = @(Dividir-Celulas $linhasCatalogo[$melhor.Indice])

    $texto = ($linhaRec.Evidencia -replace '\\\|', '|' -replace '\|', '/')

    $celulas[$celulas.Count - 2] = " **$($linhaRec.Rotulo)** - $texto "
    $linhasCatalogo[$melhor.Indice] = ($celulas -join '|')

    $melhor.Usado = $true
    $aplicadas++

    $pares += [pscustomobject]@{
        Rec      = $linhaRec.Numero
        Catalogo = $melhor.Numero
        CatalogoId = $melhor.Id
        Nota     = [math]::Round($melhorNota, 2)
        ViaAncora = $false
        Motivo   = ''
        FuncRec  = $linhaRec.Funcionalidade
        FuncCat  = $melhor.Funcionalidade
        Estado   = $linhaRec.Rotulo
        Evidencia = $linhaRec.Evidencia
    }
}

[System.IO.File]::WriteAllLines($Catalogo, $linhasCatalogo, (New-Object System.Text.UTF8Encoding($false)))

# O mapa de reconciliacao: o par (linha do RECON -> linha do catalogo), com o
# estado medido. A prova de paridade CONSOME este mapa, em vez de casar o
# RECON com o catalogo por numero.
#
# O motivo: o numero do RECON e o da lista de lacunas (BINARIO-<C>.md), que
# tem numeracao propria. Casar por numero colocaria o estado de uma
# funcionalidade no lugar de outra — que e medir errado com aparencia de
# medir certo. Medido: o RECON #3 do UNINSTALR (MuiCache) nao e a linha 3 do
# catalogo.
$chaveCatalogo = [System.IO.Path]::GetFileNameWithoutExtension($Catalogo).ToUpperInvariant()
$mapaPath = Join-Path ([System.IO.Path]::GetDirectoryName($Catalogo)) 'reconciliacao.json'

$mapa = @{}
if (Test-Path -LiteralPath $mapaPath) {
    $docExistente = Get-Content -LiteralPath $mapaPath -Raw -Encoding UTF8 | ConvertFrom-Json

    # O que se le e o conteudo de "mapa", e nao o documento inteiro: ler as
    # propriedades do documento inteiro aninharia o mapa dentro de si mesmo a
    # cada execucao.
    $conteudo = if ($docExistente.PSObject.Properties['mapa']) { $docExistente.mapa } else { $docExistente }

    foreach ($c in $conteudo.PSObject.Properties) {
        $mapa[$c.Name] = $c.Value
    }
}

# O mapa do competidor e ACUMULADO entre execucoes, e nao substituido: uma
# reconciliacao nova nao pode apagar as linhas medidas por outra. A chave e a
# LINHA do catalogo ("T{tabela}#{numero}"): quando a reconciliacao nova mede a
# mesma linha, o estado novo substitui o antigo (a medicao mais recente
# vence) e as demais linhas continuam no mapa.
#
# As entradas de uma execucao anterior so valem se o ID ainda existe no
# catalogo: e assim que um ID do esquema antigo (laxo) deixa de sobreviver a
# troca de regra.
$idsValidos = @($alvos | ForEach-Object { $_.Id })
$anteriores = @()
if ($mapa.ContainsKey($chaveCatalogo)) {
    $anteriores = @($mapa[$chaveCatalogo] | Where-Object { $idsValidos -contains $_.catalogoId })
}
$idsMedidos = @($pares | ForEach-Object { $_.CatalogoId })
$mantidos = @($anteriores | Where-Object { $idsMedidos -notcontains $_.catalogoId })

$mapa[$chaveCatalogo] = @($mantidos + @($pares | ForEach-Object {
    [pscustomobject]@{
        recon     = $_.Rec
        catalogo  = $_.Catalogo
        catalogoId = $_.CatalogoId
        estado    = $_.Estado
        evidencia = $_.Evidencia
        via       = $_.Nota
        motivo    = $_.Motivo
    }
}))

$docMapa = [pscustomobject]@{
    geradoPor = 'tools\auditoria\aplicar-recon.ps1'
    mapa      = $mapa
}
[System.IO.File]::WriteAllText(
    $mapaPath,
    ($docMapa | ConvertTo-Json -Depth 6),
    (New-Object System.Text.UTF8Encoding($false)))

Write-Output "$([System.IO.Path]::GetFileName($Catalogo)): $aplicadas linha(s) atualizada(s) de $($reconLines.Count) reconciliada(s)."
Write-Output "  Mapa de reconciliacao atualizado: $([System.IO.Path]::GetFileName($mapaPath))"
if ($divergentes.Count -gt 0) {
    Write-Output "  $($divergentes.Count) linha(s) sem correspondencia no catalogo (nao aplicadas):"
    foreach ($d in $divergentes) { Write-Output "      $d" }
}

# O par Reconciliado -> Catalogo fica visivel: e o que permite conferir, linha
# a linha, que o estado medido caiu na funcionalidade certa. Sem isso, "26
# linhas aplicadas" seria um numero sem lastro.
if ($pares.Count -gt 0) {
    Write-Output "  Pares aplicados (reconciliado -> catalogo, semelhanca):"
    foreach ($p in $pares) {
        Write-Output ("      RECON #{0} -> CAT #{1} ({2})" -f $p.Rec, $p.Catalogo, $p.Nota)
        if ($p.ViaAncora -and $p.Motivo.Length -gt 0) {
            Write-Output ("          ancora: {0}" -f $p.Motivo)
        }
        Write-Output ("          RECON: {0}" -f $p.FuncRec)
        Write-Output ("          CAT  : {0}" -f $p.FuncCat)
    }
}
