# Funcoes compartilhadas pela cadeia de auditoria.
#
# A regra de "o que e um cabecalho de tabela de funcionalidade" mora AQUI, e
# nao em cada script: e a MESMA interpretacao para o extrator e para o
# aplicador. Se os dois discordarem, o ID "T{n>#<numero>" gravado no mapa de
# reconciliacao nao bate com o ID do catalogo extraido, e a prova de paridade
# deixa de casar as linhas.
#
# Uso: . (Join-Path $PSScriptRoot 'auditoria-comum.ps1')
#
# A regra ESTRITA: uma linha e cabecalho de tabela de funcionalidade quando e
# uma linha de tabela E tem uma CELULA cujo texto, aparado, e EXATAMENTE
# "Funcionalidade".
#
# A regra antiga era LAXA ("a linha contem a palavra Funcionalidade") e
# contava como cabecalho qualquer linha de DADOS que mencionasse a palavra -
# por exemplo a linha do inventario binario do HIBIT:
#   | `bin-inventario/hibit.inventario.md` | 4.675 classes RTTI Delphi, ... |
# Isso deslocava o indice da tabela e, com ele, o ID "T<n>#<numero>" de todas
# as linhas seguintes. Medido: HIBIT 8 -> 5 tabelas, IOBIT 10 -> 7, WISE
# 9 -> 7, TOTALUNINSTALL 10 -> 7, UNINSTALLTOOL 7 -> 6, ASHAMPOO 9 -> 7,
# SOFTORGANIZER 6 -> 5. As linhas de dados capturadas sao as MESMAS nas duas
# regras: o que muda e so o indice da tabela (e, com ele, o ID).

# Divide uma linha de tabela em celulas.
#
# O pipe so separa coluna FORA de crase e FORA de aspas duplas: as evidencias
# trazem trechos como 'literal "PowerShell script|*.ps1"' e 'strings L80711 |
# ...'. Sem isso, a coluna da evidencia seria partida ao meio e o estado da
# funcionalidade seria lido do lugar errado.
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

# Verdadeiro quando a linha e o cabecalho de uma tabela de FUNCIONALIDADE.
#
# Regra ESTRITA: a linha e de tabela E tem uma celula cujo texto, aparado, e
# EXATAMENTE "Funcionalidade".
function Eh-CabecalhoFuncionalidade {
    param([string]$Linha)

    if ($Linha -notmatch '^\|') { return $false }

    $celulas = @(Dividir-Celulas $Linha | ForEach-Object { $_.Trim() })
    return (@($celulas | Where-Object { $_ -eq 'Funcionalidade' }).Count -gt 0)
}