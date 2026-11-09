"""Gera o relatorio comparativo dos onze concorrentes.

O relatorio nao e escrito a mao: ele e lido do dado medido
(`docs/auditoria/reconciliacao.json` e `docs/auditoria/catalogo-concorrentes.json`),
que a cadeia aplicar -> extrair -> provar produz. Assim o relatorio nunca
fica desatualizado em relacao a medicao: ele e a mesma coisa, em outra forma.

O estado vem do MAPA DE RECONCILIACAO (o mesmo que a prova de paridade
consome) e o texto da funcionalidade vem do catalogo. O que esta nos dois
tem de concordar — e por isso que o relatorio avisa quando discordam.

Uso:
    python tools/auditoria/gerar-relatorio.py
"""

import json
import os
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.abspath(os.path.join(AQUI, '..', '..'))
AUDITORIA = os.path.join(RAIZ, 'docs', 'auditoria')

ORDEM = [
    'ASHAMPOO', 'BCU', 'GEEK', 'HIBIT', 'IOBIT', 'SOFTORGANIZER',
    'TOTALUNINSTALL', 'UNINSTALLTOOL', 'UNINSTALR', 'WISE',
]

NOME = {
    'ASHAMPOO': 'Ashampoo UnInstaller 27',
    'BCU': 'Bulk Cookie Uninstaller',
    'GEEK': 'Geek Uninstaller',
    'HIBIT': 'HiBit Uninstaller',
    'IOBIT': 'IObit Uninstaller',
    'SOFTORGANIZER': 'Soft Organizer',
    'TOTALUNINSTALL': 'Total Uninstall',
    'UNINSTALLTOOL': 'Uninstall Tool',
    'UNINSTALR': 'Uninstalr',
    'WISE': 'Wise Program Uninstaller',
}


def carregar(nome):
    with open(os.path.join(AUDITORIA, nome), encoding='utf-8') as fh:
        return json.load(fh)


def nota_de(estado_texto):
    """A nota do estado, sem o prefixo do rotulo."""
    texto = estado_texto or ''
    for rotulo in ('FORA POR DECISAO', 'AUSENTE', 'PARCIAL', 'OK'):
        if texto.startswith(rotulo):
            return texto[len(rotulo):].lstrip(' -')
    return texto


def main():
    catalogo = carregar('catalogo-concorrentes.json')
    reconciliacao = carregar('reconciliacao.json')

    por_catalogo = {c['concorrente']: c for c in catalogo['concorrentes']}
    mapa = reconciliacao.get('mapa', {})

    ordem = [n for n in ORDEM if n in por_catalogo]

    total_geral = ok_geral = parcial_geral = ausente_geral = fora_geral = 0
    divergencias = 0

    corpo = []
    panorama = []

    for nome in ordem:
        c = por_catalogo[nome]
        linhas = c['funcionalidades']

        # O estado final vem do mapa de reconciliacao (a mesma fonte da
        # prova de paridade). Onde o mapa nao tem a linha, o estado do
        # catalogo e o que vale.
        por_id = {l['id']: l for l in linhas}
        estados = {}

        for entrada in mapa.get(nome, []):
            id_catalogo = entrada.get('catalogoId')
            if id_catalogo in por_id:
                estados[id_catalogo] = entrada.get('estado', 'AUSENTE')

        ok = parcial = ausente = fora = 0
        lacunas = []

        for l in linhas:
            id_catalogo = l['id']
            estado = estados.get(id_catalogo, l.get('estado', 'AUSENTE'))

            if estado != l.get('estado', estado):
                divergencias += 1

            if estado == 'OK':
                ok += 1
            elif estado == 'PARCIAL':
                parcial += 1
                lacunas.append((id_catalogo, l['funcionalidade'], estado, nota_de(l.get('estadoTexto', ''))))
            elif estado == 'FORA POR DECISAO':
                fora += 1
            else:
                ausente += 1
                lacunas.append((id_catalogo, l['funcionalidade'], estado, nota_de(l.get('estadoTexto', ''))))

        total = len(linhas)
        cobertura = (ok + fora) / total * 100 if total else 0.0

        total_geral += total
        ok_geral += ok
        parcial_geral += parcial
        ausente_geral += ausente
        fora_geral += fora

        panorama.append('| %s | %d | %d | %d | %d | %d | %.1f%% |' % (
            NOME.get(nome, nome), total, ok, parcial, ausente, fora, cobertura))

        corpo.append('## %s' % NOME.get(nome, nome))
        corpo.append('')
        corpo.append('| Medido | |')
        corpo.append('|---|---|')
        corpo.append('| Funcionalidades medidas | %d |' % total)
        corpo.append('| **OK** | **%d** |' % ok)
        corpo.append('| PARCIAL | %d |' % parcial)
        corpo.append('| AUSENTE | %d |' % ausente)
        corpo.append('| FORA POR DECISAO | %d |' % fora)
        corpo.append('| **Cobertura** | **%.1f%%** |' % cobertura)
        corpo.append('')

        if lacunas:
            corpo.append('### O que ainda falta')
            corpo.append('')
            corpo.append('| # | Funcionalidade | Estado | O que falta |')
            corpo.append('|---|---|---|---|')
            for id_catalogo, funcionalidade, estado, nota in lacunas:
                corpo.append('| %s | %s | %s | %s |' % (
                    id_catalogo, funcionalidade, estado, nota or '—'))
            corpo.append('')

    cobertura_geral = (ok_geral + fora_geral) / total_geral * 100 if total_geral else 0.0

    cabecalho = []
    cabecalho.append('# Relatorio dos onze concorrentes')
    cabecalho.append('')
    cabecalho.append('**Gerado por** `tools/auditoria/gerar-relatorio.py`, a partir de '
                     '`docs/auditoria/reconciliacao.json` e `docs/auditoria/catalogo-concorrentes.json`. '
                     'O relatorio e a leitura da medicao: nao ha numero escrito a mao aqui.')
    cabecalho.append('')
    cabecalho.append('Cobertura conta **OK** e **FORA POR DECISAO** (o que foi decidido, com '
                     'motivo registrado, nao ser feito). **PARCIAL** tem coluna propria: e o que '
                     'existe em parte, com a nota do que falta.')
    cabecalho.append('')
    cabecalho.append('A prova completa, que inclui tambem as 70 funcionalidades medidas do '
                     '**Revo Pro 5.5.4** (a referencia), esta em '
                     '[`PARIDADE-11-CONCORRENTES.md`](PARIDADE-11-CONCORRENTES.md).')
    cabecalho.append('')
    cabecalho.append('## Panorama')
    cabecalho.append('')
    cabecalho.append('| Concorrente | Medido | OK | PARCIAL | AUSENTE | FORA | Cobertura |')
    cabecalho.append('|---|---|---|---|---|---|---|')
    cabecalho.extend(panorama)
    cabecalho.append('| **TOTAL** | **%d** | **%d** | **%d** | **%d** | **%d** | **%.1f%%** |' % (
        total_geral, ok_geral, parcial_geral, ausente_geral, fora_geral, cobertura_geral))
    cabecalho.append('')

    if divergencias:
        cabecalho.append('> **Atencao:** %d linha(s) tem estado diferente entre o mapa de '
                         'reconciliacao e o catalogo. A prova de paridade usa o mapa; este '
                         'relatorio tambem.' % divergencias)
        cabecalho.append('')

    destino = os.path.join(AUDITORIA, 'RELATORIO-11-CONCORRENTES.md')
    with open(destino, 'w', encoding='utf-8', newline='\n') as fh:
        fh.write('\n'.join(cabecalho + corpo))
        fh.write('\n')

    print('Relatorio gravado: %s' % destino)
    print('  Medido: %d | OK: %d | PARCIAL: %d | AUSENTE: %d | FORA: %d | Cobertura: %.1f%%'
          % (total_geral, ok_geral, parcial_geral, ausente_geral, fora_geral, cobertura_geral))
    return 0


if __name__ == '__main__':
    sys.exit(main())
