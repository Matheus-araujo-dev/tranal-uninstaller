# -*- coding: utf-8 -*-
"""Reancora as citacoes dos RECON que o codigo deixou para tras.

MEDIDO (lote 33): quando o codigo anda, a citacao `arquivo.cs:linha` do RECON
fica apontando para a linha errada. Uma linha "OK" com evidencia errada e
pior que uma linha "AUSENTE" honesta — por isso a citacao e conferida pela
suite (`src/Tranal.Tests/ReconEvidenceTests.cs`), e este script e o que
devolve a citacao para o codigo.

A REGRA e o reancorar, nunca o reescrever: a citacao vai para a ocorrencia
mais proxima do simbolo, que e o que preserva a intencao original da medicao.
O texto da evidencia nao e tocado — so o numero da linha.

Uso:
    python tools/auditoria/reancorar-citacoes.py --Simula
    python tools/auditoria/reancorar-citacoes.py
"""
import argparse
import io
import json
import os
import pathlib
import re
import sys

RAIZ = pathlib.Path(__file__).resolve().parent.parent.parent
DOCS = RAIZ / "docs" / "auditoria"
SRC = RAIZ / "src"

# A mesma regra do teste: o arquivo e a linha entre crases.
PADRAO = re.compile(r"`(?P<arq>[^`]*?\.cs):(?P<lin>\d+)`")


def localizar(arq):
    """O arquivo citado: relativo a raiz, ao projeto grafico ou pelo nome."""
    separado = arq.replace("/", os.sep)
    candidato = RAIZ / separado
    if candidato.is_file():
        return candidato

    candidato = SRC / "Tranal.Uninstaller" / separado
    if candidato.is_file():
        return candidato

    nome = os.path.basename(arq.replace("\\", "/"))
    achados = list(SRC.rglob(nome))
    if len(achados) == 1:
        return achados[0]

    return None


def ler_simbolo(resto):
    achado = re.match(r"^\s*\(`(?P<sim>[^`]+?)`\)", resto)
    return achado.group("sim").strip() if achado else ""


def identificador_final(simbolo):
    texto = simbolo.strip().strip("`")
    abre = texto.find("(")
    if abre >= 0:
        texto = texto[:abre]
    partes = [p for p in re.split(r"[.\s\t]+", texto) if p]
    return partes[-1] if partes else ""


def classe_de(simbolo):
    texto = simbolo.strip().strip("`")
    abre = texto.find("(")
    if abre >= 0:
        texto = texto[:abre]
    partes = [p for p in re.split(r"[.\s\t]+", texto) if p]
    return partes[0] if len(partes) >= 2 else ""


def candidatos_do_simbolo(linhas, simbolo):
    """As linhas onde o identificador final do simbolo aparece."""
    ident = identificador_final(simbolo)
    if not ident:
        return []

    achados = [i for i, linha in enumerate(linhas) if ident in linha]
    if not achados:
        return []

    classe = classe_de(simbolo)
    if classe:
        # A string "Classe.Metodo" nunca aparece literal no codigo: a classe
        # tem que existir no arquivo, e o metodo e procurado sem o prefixo.
        if not any(classe in linha for linha in linhas):
            return []

    return achados


def linhas_cheias(linhas):
    return [i for i, linha in enumerate(linhas) if linha.strip()]


def reancorar_um(arq, numero, simbolo, linhas):
    """A linha certa para a citacao, ou None quando nao da para saber."""
    if simbolo:
        achados = candidatos_do_simbolo(linhas, simbolo)
        if not achados:
            return None
        return min(achados, key=lambda i: abs(i + 1 - numero)) + 1

    cheias = linhas_cheias(linhas)
    if not cheias:
        return None
    return min(cheias, key=lambda i: abs(i + 1 - numero)) + 1


def processar(caminho, simula):
    # newline="" para o fim de linha do arquivo ser preservado como esta.
    texto = io.open(caminho, encoding="utf-8", newline="").read()
    mudancas = []

    # De tras para frente: as substituicoes nao mexem nos indices ja lidos.
    achados = list(PADRAO.finditer(texto))
    for achado in reversed(achados):
        arq = achado.group("arq")
        numero = int(achado.group("lin"))

        fonte = localizar(arq)
        if fonte is None:
            continue

        linhas = io.open(fonte, encoding="utf-8-sig", newline="").read().split("\n")
        if numero < 1 or numero > len(linhas):
            continue

        resto = texto[achado.end():]
        simbolo = ler_simbolo(resto)
        if simbolo and simbolo.startswith("/"):
            continue
        if simbolo and ("tranal " in simbolo or " /" in simbolo):
            continue

        # Ja esta certa?
        if simbolo:
            ident = identificador_final(simbolo)
            if ident and ident in linhas[numero - 1]:
                continue
        elif linhas[numero - 1].strip():
            continue

        certo = reancorar_um(arq, numero, simbolo, linhas)
        if certo is None or certo == numero:
            continue

        inicio = achado.start("lin")
        fim = achado.end("lin")
        texto = texto[:inicio] + str(certo) + texto[fim:]
        mudancas.append((arq, numero, certo, simbolo))

    mudancas.reverse()

    if mudancas and not simula:
        io.open(caminho, "w", encoding="utf-8", newline="").write(texto)

    return mudancas


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--Simula", action="store_true")
    args = ap.parse_args()

    total = 0
    for caminho in sorted(DOCS.glob("RECON-*.md")):
        mudancas = processar(caminho, args.Simula)
        if not mudancas:
            continue
        print("%s: %d citacao(oes) reancorada(s)" % (caminho.name, len(mudancas)))
        for arq, antes, depois, simbolo in mudancas:
            print("    %s:%d -> %d   %s" % (arq, antes, depois, simbolo))
        total += len(mudancas)

    print()
    print("total: %d citacao(oes)" % total)
    if args.Simula:
        print("(simulacao: nada foi gravado)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
