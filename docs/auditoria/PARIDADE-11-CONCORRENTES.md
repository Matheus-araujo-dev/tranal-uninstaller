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
| ASHAMPOO | 76 | 49 | 21 | 2 | 4 | 0 | 69.7% |
| BCU | 97 | 72 | 23 | 0 | 2 | 0 | 76.3% |
| GEEK | 44 | 30 | 14 | 0 | 0 | 0 | 68.2% |
| HIBIT | 66 | 42 | 20 | 0 | 4 | 0 | 69.7% |
| IOBIT | 40 | 26 | 8 | 2 | 4 | 0 | 75% |
| SOFTORGANIZER | 77 | 56 | 18 | 2 | 1 | 0 | 74% |
| TOTALUNINSTALL | 94 | 61 | 29 | 0 | 4 | 0 | 69.1% |
| UNINSTALLTOOL | 94 | 60 | 27 | 2 | 5 | 0 | 69.1% |
| UNINSTALR | 51 | 36 | 12 | 0 | 3 | 0 | 76.5% |
| WISE | 86 | 53 | 30 | 0 | 3 | 0 | 65.1% |
| REVO PRO 5.5.4 | 70 | 68 | 0 | 0 | 2 | 0 | 100% |
| **TOTAL** | **795** | **553** | **202** | **8** | **32** | **0** | **73.6%** |

Apuracoes linha a linha aplicadas: 506.

## O que ainda falta (estado AUSENTE)

### ASHAMPOO — 2

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 73 | Instalador protegido por senha (criptografia ChaCha20 do setup) | Instalador protegido por senha (criptografia ChaCha20 do setup)/grep "Password" em `installer/tranal.iss` -> 0 hits; o Inno Setup do Tranal nao define `Password=` nem `Encryption=` |
| 75 | Manifesto que redireciona DLLs de sistema (`comctl32`, `mpr`, `netapi32`, `netutils`, `textshaping`, `version`) | Manifesto que redireciona DLLs de sistema (`comctl32`, `mpr`, `netapi32`, `netutils`, `textshaping`, `version`)/grep "comctl32" e "loadFrom" em `installer/tranal.iss` -> 0 hits |

### IOBIT — 2

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 7 | **Stubborn Program Remover** com base de **4.000+ programas** travados | **Stubborn Program Remover** com base de **4.000+ programas** travados/grep "Stubborn" em src/ -> 0 hits o Tranal tem desinstalacao forcada por alvo (`Core/Automation/ForcedRemovalOptions.cs`) e remocao manual assistida (`Core/Automation/ManualRemoval.cs`) — mas nao ha base de conhecimento |
| 34 | Integração com a **Central de Ações do Windows** (Action Center) | Integração com a **Central de Ações do Windows** (Action Center)/grep "ActionCenter" em src/ -> 0 hits |

### SOFTORGANIZER — 2

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 29 | Aviso para melhorar a qualidade do rastreio | Aviso para melhorar a qualidade do rastreio/grep "TraceQuality" em src/ -> 0 hits |
| 61 | **Descoberta de dispositivos Bluetooth LE** | Descoberta de dispositivos Bluetooth LE/grep "Bluetooth" em src/ -> 0 hits |

### UNINSTALLTOOL — 2

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 46 | Extensão de shell **empacotada em MSIX** para o Windows 11 | Extensão de shell **empacotada em MSIX** para o Windows 11/o instalador (`installer/tranal.iss`) cria grupo e area de trabalho; nao ha empacotamento MSIX da extensao de shell. |
| 85 | **Painéis ancoráveis** ("push pin") com pré-visualização | **Painéis ancoráveis** ("push pin") com pré-visualização/grep "PushPin" em src/ -> 0 hits |
