"""Gera um relatorio de reconciliacao (RECON) a partir de uma definicao.

A evidencia de cada linha e montada aqui: o gerador procura o simbolo no
arquivo e escreve o `arquivo:linha` REAL. Ninguem escreve o numero da
linha de memoria — uma linha escrita a mao deixa de valer no dia em que o
arquivo cresce uma linha acima.

O que o gerador NAO faz: decidir o estado. O estado vem da lista de
definicao, que e a medicao; o gerador so escreve a evidencia no lugar
certo.

Uso:
    python tools/auditoria/gerar-recon.py <definicao.json>
"""

import io
import json
import os
import re
import sys

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))


def localizar(caminho, simbolo):
    """A linha real do simbolo no arquivo (1-based), ou None."""
    completo = os.path.join(REPO, caminho)
    if not os.path.exists(completo):
        return None

    with io.open(completo, encoding='utf-8', errors='replace') as fh:
        linhas = fh.read().split('\n')

    if not simbolo:
        return 1

    rx = re.compile(re.escape(simbolo))
    for i, linha in enumerate(linhas, 1):
        if rx.search(linha):
            return i

    return None


def montar_evidencia(item):
    """Monta a evidencia de uma linha do RECON.

    Cada parte pode ser:
      - {"text": "..."}                -> texto solto (grep negativo, decisao)
      - {"file": "caminho", "at": "x"} -> o gerador acha a linha do "x"
    """
    partes = []

    for parte in item:
        if 'text' in parte:
            partes.append(parte['text'])
            continue

        caminho = parte.get('file', '')
        simbolo = parte.get('at', '')
        linha = localizar(caminho, simbolo)

        if linha is None:
            partes.append('%s (simbolo nao encontrado)' % caminho)
            continue

        if simbolo:
            partes.append('`%s:%d` (`%s`)' % (caminho.replace('\\', '/'), linha, simbolo))
        else:
            partes.append('`%s:%d`' % (caminho.replace('\\', '/'), linha))

    return ' '.join(partes)


def gerar(definicao):
    saida = []
    saida.append('# Reconciliacao medida - %s' % definicao['titulo'])
    saida.append('')
    saida.append('**Marco medido:** HEAD `%s`, branch `main`.' % definicao.get('marco', ''))
    saida.append('')

    preambulo = definicao.get('preambulo', '')
    if preambulo:
        saida.append(preambulo)
        saida.append('')

    for i, secao in enumerate(definicao['secoes'], 1):
        saida.append('## %d. %s' % (i, secao['nome']))
        saida.append('')
        saida.append('| # | Funcionalidade | Estado no Tranal | Evidencia no codigo |')
        saida.append('|---|---|---|---|')

        for item in secao['linhas']:
            saida.append('| %s | %s | %s | %s |' % (
                item['id'],
                item['funcionalidade'],
                item['estado'],
                montar_evidencia(item.get('evidencia', [])),
            ))

        saida.append('')

    return '\n'.join(saida)


def main():
    if len(sys.argv) < 2:
        print('uso: gerar-recon.py <definicao.json>')
        return 1

    caminho = sys.argv[1]
    with io.open(caminho, encoding='utf-8') as fh:
        definicao = json.load(fh)

    destino = definicao.get('destino')
    if not destino:
        print('a definicao precisa do campo "destino"')
        return 1

    texto = gerar(definicao)

    with io.open(os.path.join(REPO, destino), 'w', encoding='utf-8', newline='\n') as fh:
        fh.write(texto)

    total = sum(len(s['linhas']) for s in definicao['secoes'])
    print('%s: %d linha(s) em %d secao(oes)' % (destino, total, len(definicao['secoes'])))
    return 0


if __name__ == '__main__':
    sys.exit(main())
