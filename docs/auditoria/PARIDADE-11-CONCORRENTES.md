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
| ASHAMPOO | 76 | 48 | 21 | 3 | 4 | 0 | 68.4% |
| BCU | 97 | 71 | 23 | 1 | 2 | 0 | 75.3% |
| GEEK | 44 | 30 | 14 | 0 | 0 | 0 | 68.2% |
| HIBIT | 66 | 40 | 20 | 2 | 4 | 0 | 66.7% |
| IOBIT | 40 | 24 | 8 | 4 | 4 | 0 | 70% |
| SOFTORGANIZER | 77 | 55 | 18 | 3 | 1 | 0 | 72.7% |
| TOTALUNINSTALL | 94 | 61 | 29 | 0 | 4 | 0 | 69.1% |
| UNINSTALLTOOL | 94 | 58 | 27 | 4 | 5 | 0 | 67% |
| UNINSTALR | 51 | 34 | 12 | 2 | 3 | 0 | 72.5% |
| WISE | 86 | 52 | 30 | 1 | 3 | 0 | 64% |
| REVO PRO 5.5.4 | 70 | 68 | 0 | 0 | 2 | 0 | 100% |
| **TOTAL** | **795** | **541** | **202** | **20** | **32** | **0** | **72.1%** |

Apuracoes linha a linha aplicadas: 506.

## O que ainda falta (estado AUSENTE)

### ASHAMPOO — 3

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 12 | Importar logs do UnInstaller 4 | Importar logs do UnInstaller 4/grep "Uninstaller4" em src/ -> 0 hits o importador valida o formato e recusa qualquer outro (`Core/Modules/TraceStore.cs`) |
| 73 | Instalador protegido por senha (criptografia ChaCha20 do setup) | Instalador protegido por senha (criptografia ChaCha20 do setup)/grep "Password" em `installer/tranal.iss` -> 0 hits; o Inno Setup do Tranal nao define `Password=` nem `Encryption=` |
| 75 | Manifesto que redireciona DLLs de sistema (`comctl32`, `mpr`, `netapi32`, `netutils`, `textshaping`, `version`) | Manifesto que redireciona DLLs de sistema (`comctl32`, `mpr`, `netapi32`, `netutils`, `textshaping`, `version`)/grep "comctl32" e "loadFrom" em `installer/tranal.iss` -> 0 hits |

### BCU — 1

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 30 | **Monitorar desinstaladores silenciosos** atrás de popups (daemon de automatização) | `grep "MonitorSilent/PopupWatch/WatchPopup/DaemonMonitor" em src/` -> **0 hits**. O mais proximo e deteccao de desinstalacao externa (`src/Tranal.Uninstaller/Core/Discovery/ExternalUninstallWatcher.cs:50`), que le o registro depois do fato; nao ha daemon nem vigia de popup. Binario do concorrente: `bcu.strings.txt:5751` (`checkBoxAutoDaemon`) |

### HIBIT — 2

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 62 | Botão **Doar** (adicionado na 4.0.10) | `grep "Doar/Donate/Doacao" em src/` -> **0 hits**. Funcao sem equivalente; irrelevante para paridade funcional. Binario do concorrente: nao confirmado no binario |
| 100 | **Banco local embutido** (`LOCALDB`) | `grep "LOCALDB/Sqlite/Microsoft.Data.Sqlite" em src/` -> **0 hits**. O estado e JSON: `src/Tranal.Uninstaller/Core/Settings/ProgramCache.cs:154` (`ProgramCacheStore`), `:188` (salva), `:252` (carrega); o log de instalacao vive em `src/Tranal.Uninstaller/Core/Modules/TraceStore.cs:68`. **Nao ha banco local**. Binario do concorrente: `hibit.recursos.txt:127` (`LOCALDB/1033`) |

### IOBIT — 4

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 7 | **Stubborn Program Remover** com base de **4.000+ programas** travados | **Stubborn Program Remover** com base de **4.000+ programas** travados/grep "Stubborn" em src/ -> 0 hits o Tranal tem desinstalacao forcada por alvo (`Core/Automation/ForcedRemovalOptions.cs`) e remocao manual assistida (`Core/Automation/ManualRemoval.cs`) — mas nao ha base de conhecimento |
| 19 | Log guardado em **SQLite** (`sqlite3.dll`, 37 funções importadas) | Log guardado em **SQLite** (`sqlite3.dll`, 37 funções importadas)/grep "sqlite3" em src/ -> 0 hits o log e JSON (`Core/Tracing/TraceLogV2.cs`); a limpeza do navegador apaga os arquivos `.sqlite` do Firefox por nome, sem motor SQLite embarcado (`Core/Tools/InternetCleaner.cs:440`) |
| 34 | Integração com a **Central de Ações do Windows** (Action Center) | Integração com a **Central de Ações do Windows** (Action Center)/grep "ActionCenter" em src/ -> 0 hits |
| 36 | **Voto / avaliação** de programas e plugins pela comunidade | **Voto / avaliação** de programas e plugins pela comunidade/a avaliacao local por estrelas existe (`Core/Appearance/ProgramRating.cs`), mas nao ha coleta de votos da comunidade |

### SOFTORGANIZER — 3

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 29 | Aviso para melhorar a qualidade do rastreio | Aviso para melhorar a qualidade do rastreio/grep "TraceQuality" em src/ -> 0 hits |
| 53 | Instalação de novas versões | Instalação de novas versões/grep "MsiInstallProduct" em src/ -> 0 hits |
| 61 | **Descoberta de dispositivos Bluetooth LE** | Descoberta de dispositivos Bluetooth LE/grep "Bluetooth" em src/ -> 0 hits |

### UNINSTALLTOOL — 4

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 39 | **Anexar** dados rastreados a um item existente | **Anexar** dados rastreados a um item existente/`src/Tranal.Uninstaller/Core/Tracing/TraceLogV2.cs:42` (`public sealed class TraceLogV2`) a fusao de logs do mesmo programa existe, mas nao ha acao de anexar um log a um item da lista. |
| 46 | Extensão de shell **empacotada em MSIX** para o Windows 11 | Extensão de shell **empacotada em MSIX** para o Windows 11/o instalador (`installer/tranal.iss`) cria grupo e area de trabalho; nao ha empacotamento MSIX da extensao de shell. |
| 47 | Serviço de rastreamento **versionado**, com prompt de atualização | Serviço de rastreamento **versionado**, com prompt de atualização/`src/Tranal.Uninstaller/Core/Tracing/WriteWatcher.cs:35` (`public sealed class WriteWatcher`) o rastreio roda no processo do aplicativo; nao ha servico nem prompt de atualizacao de versao. |
| 85 | **Painéis ancoráveis** ("push pin") com pré-visualização | **Painéis ancoráveis** ("push pin") com pré-visualização/grep "PushPin" em src/ -> 0 hits |

### UNINSTALR — 2

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 30 | Opções de **acessibilidade** (leitor de tela, moldura padrão, botões padrão) | Opções de **acessibilidade** (leitor de tela, moldura padrão, botões padrão)/grep "Accessibility" em src/ -> 0 hits o WPF entrega a acessibilidade de sistema; nao ha as opcoes do concorrente. |
| 47 | **Opções "speedhacks"** (desativar verificações lentas) | Opções "speedhacks" (desativar verificações lentas)/grep "SpeedHack" em src/ -> 0 hits nao ha a opcao de desligar as verificacoes lentas. |

### WISE — 1

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 45 | Diálogo de **notificações** com remetente e tipo | Diálogo de **notificações** com remetente e tipo/`src/Tranal.Uninstaller/Core/Management/SoftwareHealth.cs:105` (`public static class SoftwareHealth`) a categoria de notificacoes do Software Health trata do estado do programa, nao de notificacoes do sistema. |
