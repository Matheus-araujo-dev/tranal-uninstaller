# Prova de cobertura por concorrente

Gerado por `tools\auditoria\provar-paridade.ps1`. Cada linha diz o que o
o concorrente tem e o que o Tranal tem, **conferido no codigo**.

Estado: `OK` o Tranal tem · `PARCIAL` tem parte · `AUSENTE` falta ·
`FORA` limite declarado · `NAO CONFIRMADO` o dump do concorrente nao permite confirmar.

A **cobertura** conta `OK` e `FORA` (o que o Tranal tem, ou o que foi
declarado fora por decisao). `PARCIAL` aparece na coluna propria: a
funcionalidade existe, mas nao inteira — por isso ela NAO entra na
cobertura, e por isso o total medido e sempre maior que a soma das
colunas.

| Concorrente | Funcionalidades medidas | OK | Parcial | Ausente | Fora | Nao confirmado | Cobertura |
|---|---:|---:|---:|---:|---:|---:|---:|
| ASHAMPOO | 76 | 54 | 18 | 0 | 4 | 0 | 76.3% |
| BCU | 97 | 82 | 13 | 0 | 2 | 0 | 86.6% |
| GEEK | 44 | 39 | 5 | 0 | 0 | 0 | 88.6% |
| HIBIT | 66 | 45 | 17 | 0 | 4 | 0 | 74.2% |
| IOBIT | 40 | 30 | 6 | 0 | 4 | 0 | 85% |
| SOFTORGANIZER | 77 | 60 | 16 | 0 | 1 | 0 | 79.2% |
| TOTALUNINSTALL | 94 | 68 | 22 | 0 | 4 | 0 | 76.6% |
| UNINSTALLTOOL | 94 | 63 | 25 | 1 | 5 | 0 | 72.3% |
| UNINSTALR | 51 | 36 | 12 | 0 | 3 | 0 | 76.5% |
| WISE | 86 | 56 | 27 | 0 | 3 | 0 | 68.6% |
| REVO PRO 5.5.4 | 70 | 68 | 0 | 0 | 2 | 0 | 100% |
| **TOTAL** | **795** | **601** | **161** | **1** | **32** | **0** | **79.6%** |

Apuracoes linha a linha aplicadas: 506.

## O que ainda falta (estado AUSENTE)

### UNINSTALLTOOL — 1

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 46 | Extensão de shell **empacotada em MSIX** para o Windows 11 | Extensão de shell **empacotada em MSIX** para o Windows 11/o instalador (`installer/tranal.iss`) cria grupo e area de trabalho; nao ha empacotamento MSIX da extensao de shell. |
