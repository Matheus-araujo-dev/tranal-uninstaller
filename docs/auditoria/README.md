# Auditoria de concorrentes — Tranal Uninstaller

Esta pasta guarda a **engenharia reversa** de cada software concorrente do
Revo Uninstaller Pro, feita para que o Tranal fique **igual ou melhor em
tudo**.

O objetivo do dono é explícito: *"não quero perder em nada pro revo,
precisamos ser igual ou melhor que o revo em TUDO"*. Para isso é preciso
saber, de cada concorrente, o que ele faz que o Tranal não faz — e o que o
Tranal já faz melhor.

## Regra do método

> **Medido vale. Inferido não vale.**

Cada afirmação neste diretório nasce de uma evidência que existe em disco:

| Tipo de evidência | Prova | Onde |
|---|---|---|
| String de interface (`.resx`, `.ini`, recurso do PE) | existe um item de tela | `evidencia/*.strings.txt`, `*.resx.txt` |
| Manual oficial | o recurso existe e é documentado | `evidencia/manual-*.txt`, com a seção citada |
| Árvore UIA / captura | o comportamento foi observado | `evidencia/*.uia.txt`, `*.png` |
| Cabeçalho PE / imports | o mecanismo (ex.: `SHFileOperation` = Lixeira) | `evidencia/pe-*.txt` |
| Documentação oficial | recurso de versão paga | trecho citado + URL |

O que **não** vale: "o Revo faz isso, então o concorrente também faz" ou
qualquer afirmação sem fonte anexa. Quando não foi possível verificar, o
documento diz **"não verificado"** e explica o motivo.

## Software auditados

| Software | Doc | Estado |
|---|---|---|
| Bulk Crap Uninstaller 6.3.0 | [BCU.md](BCU.md) | ✅ auditado |
| IObit Uninstaller 16.1.0.4 | [IOBIT.md](IOBIT.md) | ✅ auditado |
| HiBit Uninstaller 4.0.10 | [HIBIT.md](HIBIT.md) | ✅ auditado |
| Uninstalr 3.2 | [UNINSTALR.md](UNINSTALR.md) | ✅ auditado |
| Geek Uninstaller 1.6.0.200 | [GEEK.md](GEEK.md) | ✅ auditado |
| HiBit Uninstaller | — | em curso |
| Geek Uninstaller | — | em curso |
| Uninstalr | — | em curso |
| IObit Uninstaller | — | em curso |
| Total Uninstall | — | em curso |
| Ashampoo UnInstaller | — | pendente |
| Uninstall Tool | — | pendente |
| Soft Organizer | — | pendente |
| Wise Program Uninstaller | — | pendente |

A matriz consolidada ("Revo + 10 concorrentes → o que falta para ser igual
ou melhor em TUDO") fica em [MATRIZ.md](MATRIZ.md), e é atualizada a cada
auditoria concluída.

## Ferramentas

A coleta é feita por `tools/auditoria/coleta.ps1`, com três modos:

```powershell
# strings ASCII/UTF-16 de um binário nativo (Delphi, C++) — o que revela
# menus, legendas de formulário, mensagens e URLs
.\tools\auditoria\coleta.ps1 -Modo strings -Arquivo <bin> -Min 5 -Saida <txt>

# pares nome/valor de um .resx (localização de programa .NET)
.\tools\auditoria\coleta.ps1 -Modo resx -Arquivo <arquivo.resx> -Saida <txt>

# cabeçalho PE, assinatura, versão e DLLs importadas
.\tools\auditoria\coleta.ps1 -Modo pe -Arquivo <bin>
```

| Ferramenta | Onde | Uso |
|---|---|---|
| `innoextract` 1.9 | `workspace\tools\innoextract\` | extrai instaladores Inno Setup (até 6.0.5) |
| `tar` (bsdtar 3.8.9) | no PATH | extrai zip/tar |
| REA MCP | `inspect_pe_imports`, `inspect_managed_artifact`, … | metadata .NET e PE |

Não há 7-Zip CLI, Ghidra nem IDA nesta máquina. Para instaladores NSIS ou
formatos fora do alcance do `innoextract`, a via é a versão portátil do
software ou a leitura da pasta de instalação depois de instalar.

## Como este diretório é mantido

- Um software = um documento, no mesmo formato do `docs/PARIDADE.md`.
- Ao implementar algo que a auditoria apontou, a linha muda **no documento do
  software**, em `docs/PARIDADE.md` e em `NOVIDADES.md`.
- O inventário bruto de strings **não** é versionado: é material de análise.
  O que fica no repositório é o fato derivado.
