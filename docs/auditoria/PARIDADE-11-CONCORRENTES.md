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
| ASHAMPOO | 76 | 51 | 21 | 0 | 4 | 0 | 72.4% |
| BCU | 97 | 72 | 23 | 0 | 2 | 0 | 76.3% |
| GEEK | 44 | 30 | 14 | 0 | 0 | 0 | 68.2% |
| HIBIT | 66 | 42 | 20 | 0 | 4 | 0 | 69.7% |
| IOBIT | 40 | 27 | 8 | 1 | 4 | 0 | 77.5% |
| SOFTORGANIZER | 77 | 58 | 18 | 0 | 1 | 0 | 76.6% |
| TOTALUNINSTALL | 94 | 61 | 29 | 0 | 4 | 0 | 69.1% |
| UNINSTALLTOOL | 94 | 61 | 27 | 1 | 5 | 0 | 70.2% |
| UNINSTALR | 51 | 36 | 12 | 0 | 3 | 0 | 76.5% |
| WISE | 86 | 53 | 30 | 0 | 3 | 0 | 65.1% |
| REVO PRO 5.5.4 | 70 | 68 | 0 | 0 | 2 | 0 | 100% |
| **TOTAL** | **795** | **559** | **202** | **2** | **32** | **0** | **74.3%** |

Apuracoes linha a linha aplicadas: 506.

## O que ainda falta (estado AUSENTE)

### IOBIT — 1

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 34 | Integração com a **Central de Ações do Windows** (Action Center) | Integração com a **Central de Ações do Windows** (Action Center)/grep "ActionCenter" em src/ -> 0 hits; MEDIDO em 09/11/2026: o balao da bandeja com `SetCurrentProcessExplicitAppUserModelID` (retorno 0x00000000) e `Shell_NotifyIcon` (ADD, SETVERSION e MODIFY com NIF_INFO, todos True) **nao entra** na Central de Acoes — a contagem do banco real `%LOCALAPPDATA%\Microsoft\Windows\Notifications\wpndatabase.db` ficou em 27 antes e 27 depois, com o mesmo `Order` maximo (1336). O detalhe esta em `docs/auditoria/EVIDENCIA-CENTRAL-ACoes.md`. |

### UNINSTALLTOOL — 1

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 46 | Extensão de shell **empacotada em MSIX** para o Windows 11 | Extensão de shell **empacotada em MSIX** para o Windows 11/o instalador (`installer/tranal.iss`) cria grupo e area de trabalho; nao ha empacotamento MSIX da extensao de shell. |
