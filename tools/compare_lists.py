#!/usr/bin/env python3
"""Compara a listagem do Tranal com a do RevoCmd (paridade de resultados).

Normaliza acentos e simbolos para comparar nomes com codificacoes diferentes
(RevoCmd escreve na codepage do console, o Tranal em UTF-8).
"""
import re
import subprocess
import sys
import unicodedata

TRANAL = sys.argv[1] if len(sys.argv) > 1 else r"tranal.exe"
REVO = r"E:\Program Files\VS Revo Group\Revo Uninstaller Pro\RevoCmd.exe"


def norm(text):
    text = unicodedata.normalize("NFKD", text)
    text = "".join(c for c in text if not unicodedata.combining(c))
    text = text.replace("\u2122", "").replace("\u00ae", "")
    text = re.sub(r"\s+", " ", text).strip().lower()
    return text


def run(cmd):
    out = subprocess.run(cmd, capture_output=True)
    for enc in ("utf-8", "cp850", "cp1252", "latin-1"):
        try:
            return out.stdout.decode(enc)
        except UnicodeDecodeError:
            continue
    return out.stdout.decode("latin-1", "replace")


def names(text):
    """Cada bloco e 'Nome - Versao'; o nome pode conter ' - ' (nomes MSI)."""
    result = []
    for line in text.splitlines():
        line = line.strip()
        if not line:
            continue
        result.append(norm(line))
    return result


def split_entry(line):
    """'Nome - Versao' -> (nome, versao). O nome pode conter ' - ' (nomes MSI)."""
    if " - " in line:
        nome, versao = line.rsplit(" - ", 1)
        return nome.strip(), versao.strip()
    return line.strip(), ""


def main():
    t_text = run([TRANAL, "/m", "*"])
    r_text = run([REVO, "/m", "*"])

    t_lines = [l for l in (x.strip() for x in t_text.splitlines()) if l]
    r_lines = [l for l in (x.strip() for x in r_text.splitlines()) if l]

    t_pairs = [split_entry(l) for l in t_lines]
    r_pairs = [split_entry(l) for l in r_lines]

    print(f"Tranal: {len(t_pairs)} entradas   Revo: {len(r_pairs)} entradas")

    t_names = [norm(n) for n, _ in t_pairs]
    r_names = [norm(n) for n, _ in r_pairs]

    only_t = sorted(set(t_names) - set(r_names))
    only_r = sorted(set(r_names) - set(t_names))

    print(f"\nnomes unicos: Tranal={len(set(t_names))} Revo={len(set(r_names))}")
    print(f"apenas no Tranal: {len(only_t)}")
    for n in only_t:
        print(f"   + {n}")
    print(f"apenas no Revo: {len(only_r)}")
    for n in only_r:
        print(f"   - {n}")

    def ver(pair):
        return norm(pair[1])

    t_ver = {}
    for n, v in t_pairs:
        t_ver.setdefault(norm(n), set()).add(ver((n, v)))
    r_ver = {}
    for n, v in r_pairs:
        r_ver.setdefault(norm(n), set()).add(ver((n, v)))
    diff_ver = [n for n in set(t_ver) & set(r_ver) if t_ver[n] != r_ver[n]]
    print(f"\nversoes divergentes: {len(diff_ver)}")
    for n in diff_ver[:10]:
        print(f"   ~ {n}: tranal={sorted(t_ver[n])} revo={sorted(r_ver[n])}")


if __name__ == "__main__":
    main()
