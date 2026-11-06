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

## A auditoria binária (método atual)

A auditoria deixou de depender de documentação e de execução manual: as
funcionalidades passaram a ser extraídas **do artefato entregue de cada
concorrente**. O método, os números medidos e a **decisão sobre o que entra no
Tranal** estão em [BINARIO.md](BINARIO.md).

| Extrator | O que entrega |
|---|---|
| REA `inspect_pe_imports` | as DLLs e funções que o binário importa |
| REA `inspect_pe_resources` | a árvore de recursos da PE (formulários Delphi, manifesto) |
| `tools/auditoria/extrair-strings.ps1` | todo literal ASCII e UTF-16 do binário |
| `tools/auditoria/DotnetMap/Program.cs` | o mapa tipo → método → literal dos assemblies .NET |
| Tabela de idioma do próprio produto (`lang\*.ini`) | o inventário de telas e comandos (é o caso do Revo Pro) |

Cada concorrente tem, além do documento canônico, um arquivo
`BINARIO-<COMPETIDOR>.md` com a lista de funcionalidades presentes no binário
e ausentes no Tranal — **com a evidência apontada**. Essas listas são
matéria-prima da decisão, não a decisão: a decisão está em `BINARIO.md` §3.

## Software auditados

| Software | Doc | Estado |
|---|---|---|
| Bulk Crap Uninstaller 6.3.0 | [BCU.md](BCU.md) | ✅ auditado |
| IObit Uninstaller 16.1.0.4 | [IOBIT.md](IOBIT.md) | ✅ auditado |
| HiBit Uninstaller 4.0.10 | [HIBIT.md](HIBIT.md) | ✅ auditado |
| Uninstalr 3.2 | [UNINSTALR.md](UNINSTALR.md) | ✅ auditado |
| Geek Uninstaller 1.6.0.200 | [GEEK.md](GEEK.md) | ✅ auditado |
| Wise Program Uninstaller 3.2.7.271 | [WISE.md](WISE.md) | ✅ auditado |
| Total Uninstall 7.6.2 Professional | [TOTALUNINSTALL.md](TOTALUNINSTALL.md) | ✅ auditado |
| Ashampoo UnInstaller 27.0.1 | [ASHAMPOO.md](ASHAMPOO.md) | ✅ auditado |
| Uninstall Tool 6.6.0 | [UNINSTALLTOOL.md](UNINSTALLTOOL.md) | ✅ auditado |
| Soft Organizer 10.50 | [SOFTORGANIZER.md](SOFTORGANIZER.md) | ✅ auditado |

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
| REA MCP | `inspect_pe_imports`, `inspect_pe_resources`, `inspect_managed_artifact`, `inspect_managed_members` | metadata PE e .NET |
| `tools/auditoria/extrair-strings.ps1` | no repositório | dump de strings de alta velocidade (o `coleta.ps1` faz o mesmo, byte a byte) |
| `tools/auditoria/DotnetMap/Program.cs` | no repositório | mapa tipo → método → literal de um assembly .NET |

Não há 7-Zip CLI, Ghidra nem IDA nesta máquina. Para instaladores NSIS ou
formatos fora do alcance do `innoextract`, a via é a versão portátil do
software ou a leitura da pasta de instalação depois de instalar. O motor
Ghidra da REA **não** foi usado nesta rodada: os literais, os recursos e as
importações já entregam a funcionalidade, e o limite está registrado em
`BINARIO.md` §5.

## Como este diretório é mantido

- Um software = um documento, no mesmo formato do `docs/PARIDADE.md`.
- Ao implementar algo que a auditoria apontou, a linha muda **no documento do
  software**, em `docs/PARIDADE.md` e em `NOVIDADES.md`.
- O inventário bruto de strings **não** é versionado: é material de análise.
  O que fica no repositório é o fato derivado.
