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
| BCU | 97 | 68 | 23 | 4 | 2 | 0 | 72.2% |
| GEEK | 44 | 30 | 14 | 0 | 0 | 0 | 68.2% |
| HIBIT | 66 | 40 | 20 | 2 | 4 | 0 | 66.7% |
| IOBIT | 40 | 22 | 8 | 6 | 4 | 0 | 65% |
| SOFTORGANIZER | 77 | 51 | 18 | 7 | 1 | 0 | 67.5% |
| TOTALUNINSTALL | 94 | 60 | 29 | 1 | 4 | 0 | 68.1% |
| UNINSTALLTOOL | 94 | 57 | 27 | 5 | 5 | 0 | 66% |
| UNINSTALR | 51 | 34 | 12 | 2 | 3 | 0 | 72.5% |
| WISE | 86 | 52 | 30 | 1 | 3 | 0 | 64% |
| REVO PRO 5.5.4 | 70 | 68 | 0 | 0 | 2 | 0 | 100% |
| **TOTAL** | **795** | **530** | **202** | **31** | **32** | **0** | **70.7%** |

Apuracoes linha a linha aplicadas: 506.

## O que ainda falta (estado AUSENTE)

### ASHAMPOO — 3

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 12 | Importar logs do UnInstaller 4 | Importar logs do UnInstaller 4/grep "Uninstaller4" em src/ -> 0 hits o importador valida o formato e recusa qualquer outro (`Core/Modules/TraceStore.cs`) |
| 73 | Instalador protegido por senha (criptografia ChaCha20 do setup) | Instalador protegido por senha (criptografia ChaCha20 do setup)/grep "Password" em `installer/tranal.iss` -> 0 hits; o Inno Setup do Tranal nao define `Password=` nem `Encryption=` |
| 75 | Manifesto que redireciona DLLs de sistema (`comctl32`, `mpr`, `netapi32`, `netutils`, `textshaping`, `version`) | Manifesto que redireciona DLLs de sistema (`comctl32`, `mpr`, `netapi32`, `netutils`, `textshaping`, `version`)/grep "comctl32" e "loadFrom" em `installer/tranal.iss` -> 0 hits |

### BCU — 4

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 30 | **Monitorar desinstaladores silenciosos** atrás de popups (daemon de automatização) | `grep "MonitorSilent/PopupWatch/WatchPopup/DaemonMonitor" em src/` -> **0 hits**. O mais proximo e deteccao de desinstalacao externa (`src/Tranal.Uninstaller/Core/Discovery/ExternalUninstallWatcher.cs:50`), que le o registro depois do fato; nao ha daemon nem vigia de popup. Binario do concorrente: `bcu.strings.txt:5751` (`checkBoxAutoDaemon`) |
| 52 | Instalador gerado automaticamente para **app não registrado** ("Simple Delete") | `grep "UninstallerKind.SimpleDelete" em src/` -> 3 hits, todos de classificacao (`src/Tranal.Uninstaller/Core/UninstallerKind.cs:74`, `:215`) e de legenda (`src/Tranal.Uninstaller/Core/Appearance/ColorLegend.cs:178`): **nada gera instalador** para app nao registrado. `docs/PARIDADE.md` §8 ("Fora de escopo") nao registra decisao sobre este item. Binario do concorrente: `UninstallerType_SimpleDelete` — nao confirmado no binario (so `.resx`) |
| 83 | **Relatório de erro automático** (crash report) com envio pela web | `grep "CrashReport/NBug/EnviarRelatorio/SubmitCrash" em src/` -> **0 hits**. O Tranal grava log local (`src/Tranal.Uninstaller/FatalLog.cs:13`, escrita em `:32`) e coleta dumps do Windows (`src/Tranal.Uninstaller/Core/Modules/CrashDumpCollector.cs:116`, painel em `src/Tranal.Uninstaller/Ui/Tools/MaintenanceViews.cs:301`), mas **nao gera relatorio de erro automatico nem envia pela web**. Binario do concorrente: `bcu.strings.txt:5611` (`NBug.Core.Submission`) |
| 90 | **Limpar caches** da aplicação | `grep "ClearCache/LimparCache" em src/` -> **0 hits**; ha limpeza de cache de navegador (`src/Tranal.Uninstaller/Core/Tools/JunkCleaner.cs:239`, categoria `browser-cache`), mas **nenhuma acao de limpar o cache do programa que esta sendo desinstalado**. Binario do concorrente: `bcu.strings.txt:7412` ("Clear caches") |

### HIBIT — 2

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 62 | Botão **Doar** (adicionado na 4.0.10) | `grep "Doar/Donate/Doacao" em src/` -> **0 hits**. Funcao sem equivalente; irrelevante para paridade funcional. Binario do concorrente: nao confirmado no binario |
| 100 | **Banco local embutido** (`LOCALDB`) | `grep "LOCALDB/Sqlite/Microsoft.Data.Sqlite" em src/` -> **0 hits**. O estado e JSON: `src/Tranal.Uninstaller/Core/Settings/ProgramCache.cs:154` (`ProgramCacheStore`), `:188` (salva), `:252` (carrega); o log de instalacao vive em `src/Tranal.Uninstaller/Core/Modules/TraceStore.cs:68`. **Nao ha banco local**. Binario do concorrente: `hibit.recursos.txt:127` (`LOCALDB/1033`) |

### IOBIT — 6

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 7 | **Stubborn Program Remover** com base de **4.000+ programas** travados | **Stubborn Program Remover** com base de **4.000+ programas** travados/grep "Stubborn" em src/ -> 0 hits o Tranal tem desinstalacao forcada por alvo (`Core/Automation/ForcedRemovalOptions.cs`) e remocao manual assistida (`Core/Automation/ManualRemoval.cs`) — mas nao ha base de conhecimento |
| 19 | Log guardado em **SQLite** (`sqlite3.dll`, 37 funções importadas) | Log guardado em **SQLite** (`sqlite3.dll`, 37 funções importadas)/grep "sqlite3" em src/ -> 0 hits o log e JSON (`Core/Tracing/TraceLogV2.cs`); a limpeza do navegador apaga os arquivos `.sqlite` do Firefox por nome, sem motor SQLite embarcado (`Core/Tools/InternetCleaner.cs:440`) |
| 33 | Aviso de **sincronização do Chrome** ao remover extensão | Aviso de **sincronização do Chrome** ao remover extensão/`src/Tranal.Uninstaller/Core/Modules/BrowserExtensions.cs:53` (`public static`) a remocao de extensao acontece sem aviso de sincronizacao; o aviso de navegador em execucao existe, mas e outro assunto (`Core/Tools/OperationGuards.cs:68`) |
| 34 | Integração com a **Central de Ações do Windows** (Action Center) | Integração com a **Central de Ações do Windows** (Action Center)/grep "ActionCenter" em src/ -> 0 hits |
| 36 | **Voto / avaliação** de programas e plugins pela comunidade | **Voto / avaliação** de programas e plugins pela comunidade/a avaliacao local por estrelas existe (`Core/Appearance/ProgramRating.cs`), mas nao ha coleta de votos da comunidade |
| 37 | **Captura de tela** do sistema no fluxo de suporte | **Captura de tela** do sistema no fluxo de suporte/grep "screenshot" em src/ -> 0 hits o pedido de suporte monta texto e URL com os dados do programa (`Core/Appearance/SupportRequest.cs`) |

### SOFTORGANIZER — 7

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 29 | Aviso para melhorar a qualidade do rastreio | Aviso para melhorar a qualidade do rastreio/grep "TraceQuality" em src/ -> 0 hits |
| 41 | **Monitor em processo separado** (app de rastreamento) | **Monitor em processo separado** (app de rastreamento)/a solucao tem apenas `Tranal.Uninstaller.csproj` (WPF), `Tranal.Cli.csproj` e `Tranal.Tests.csproj` — nao ha processo separado de rastreamento |
| 53 | Instalação de novas versões | Instalação de novas versões/grep "MsiInstallProduct" em src/ -> 0 hits |
| 57 | **Carga e descarregamento de chaves** e substituição de chave | **Carga e descarregamento de chaves** e substituição de chave/grep "RegLoadKey" em src/ -> 0 hits |
| 61 | **Descoberta de dispositivos Bluetooth LE** | Descoberta de dispositivos Bluetooth LE/grep "Bluetooth" em src/ -> 0 hits |
| 63 | Informações de **usuário e estação de rede** | Informações de **usuário e estação de rede**/grep "NetUserGetInfo" em src/ -> 0 hits |
| 76 | **Impersonação de token** e verificação de privilégios | **Impersonação de token** e verificação de privilégios/grep "ImpersonateLoggedOnUser" em src/ -> 0 hits |

### TOTALUNINSTALL — 1

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 50 | **Agente autônomo** de baixo consumo para notificar instalações em andamento | **Agente autônomo** de baixo consumo para notificar instalações em andamento/grep "Unfinished" em src/ -> 0 hits o rastreio roda no processo do aplicativo (`Core/Modules/InstallMonitor.cs`) e morre com ele; nao ha agente nem daemon. |

### UNINSTALLTOOL — 5

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 39 | **Anexar** dados rastreados a um item existente | **Anexar** dados rastreados a um item existente/`src/Tranal.Uninstaller/Core/Tracing/TraceLogV2.cs:42` (`public sealed class TraceLogV2`) a fusao de logs do mesmo programa existe, mas nao ha acao de anexar um log a um item da lista. |
| 46 | Extensão de shell **empacotada em MSIX** para o Windows 11 | Extensão de shell **empacotada em MSIX** para o Windows 11/o instalador (`installer/tranal.iss`) cria grupo e area de trabalho; nao ha empacotamento MSIX da extensao de shell. |
| 47 | Serviço de rastreamento **versionado**, com prompt de atualização | Serviço de rastreamento **versionado**, com prompt de atualização/`src/Tranal.Uninstaller/Core/Tracing/WriteWatcher.cs:35` (`public sealed class WriteWatcher`) o rastreio roda no processo do aplicativo; nao ha servico nem prompt de atualizacao de versao. |
| 85 | **Painéis ancoráveis** ("push pin") com pré-visualização | **Painéis ancoráveis** ("push pin") com pré-visualização/grep "PushPin" em src/ -> 0 hits |
| 86 | **Execução de processo 32 bits** (`CreateX86Process`) | **Execução de processo 32 bits** (`CreateX86Process`)/grep "CreateX86Process" em src/ -> 0 hits o Tranal so le o `WOW6432Node` do registro (`Core/Modules/SystemSnapshot.cs`). |

### UNINSTALR — 2

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 30 | Opções de **acessibilidade** (leitor de tela, moldura padrão, botões padrão) | Opções de **acessibilidade** (leitor de tela, moldura padrão, botões padrão)/grep "Accessibility" em src/ -> 0 hits o WPF entrega a acessibilidade de sistema; nao ha as opcoes do concorrente. |
| 47 | **Opções "speedhacks"** (desativar verificações lentas) | Opções "speedhacks" (desativar verificações lentas)/grep "SpeedHack" em src/ -> 0 hits nao ha a opcao de desligar as verificacoes lentas. |

### WISE — 1

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 45 | Diálogo de **notificações** com remetente e tipo | Diálogo de **notificações** com remetente e tipo/`src/Tranal.Uninstaller/Core/Management/SoftwareHealth.cs:105` (`public static class SoftwareHealth`) a categoria de notificacoes do Software Health trata do estado do programa, nao de notificacoes do sistema. |
