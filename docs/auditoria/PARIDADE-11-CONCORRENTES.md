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
| ASHAMPOO | 76 | 46 | 21 | 5 | 4 | 0 | 65.8% |
| BCU | 97 | 67 | 23 | 5 | 2 | 0 | 71.1% |
| GEEK | 44 | 30 | 14 | 0 | 0 | 0 | 68.2% |
| HIBIT | 66 | 39 | 20 | 3 | 4 | 0 | 65.2% |
| IOBIT | 40 | 20 | 8 | 8 | 4 | 0 | 60% |
| SOFTORGANIZER | 77 | 48 | 18 | 10 | 1 | 0 | 63.6% |
| TOTALUNINSTALL | 94 | 58 | 29 | 3 | 4 | 0 | 66% |
| UNINSTALLTOOL | 94 | 47 | 27 | 15 | 5 | 0 | 55.3% |
| UNINSTALR | 51 | 34 | 12 | 2 | 3 | 0 | 72.5% |
| WISE | 86 | 48 | 30 | 5 | 3 | 0 | 59.3% |
| REVO PRO 5.5.4 | 70 | 68 | 0 | 0 | 2 | 0 | 100% |
| **TOTAL** | **795** | **505** | **202** | **56** | **32** | **0** | **67.5%** |

Apuracoes linha a linha aplicadas: 506.

## O que ainda falta (estado AUSENTE)

### ASHAMPOO — 5

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 63 | Categoria automática expandida + reset para "Unsorted" | Categoria automática expandida + reset para "Unsorted"/grep "Unsorted" em src/ -> 0 hits o agrupamento por coluna existe (`Core/ProgramColumns.cs`), mas nao ha a categoria "Unsorted" nem o reset para ela |
| 3 | Ícone na bandeja do sistema para o monitor de instalação, com acionamento manual | Ícone na bandeja do sistema para o monitor de instalação, com acionamento manual/grep "NotifyIcon" em src/ -> 0 hits o monitor de instalacao existe (`Core/Modules/InstallMonitor.cs`), mas a bandeja nao |
| 12 | Importar logs do UnInstaller 4 | Importar logs do UnInstaller 4/grep "Uninstaller4" em src/ -> 0 hits o importador valida o formato e recusa qualquer outro (`Core/Modules/TraceStore.cs`) |
| 73 | Instalador protegido por senha (criptografia ChaCha20 do setup) | Instalador protegido por senha (criptografia ChaCha20 do setup)/grep "Password" em `installer/tranal.iss` -> 0 hits; o Inno Setup do Tranal nao define `Password=` nem `Encryption=` |
| 75 | Manifesto que redireciona DLLs de sistema (`comctl32`, `mpr`, `netapi32`, `netutils`, `textshaping`, `version`) | Manifesto que redireciona DLLs de sistema (`comctl32`, `mpr`, `netapi32`, `netutils`, `textshaping`, `version`)/grep "comctl32" e "loadFrom" em `installer/tranal.iss` -> 0 hits |

### BCU — 5

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 30 | **Monitorar desinstaladores silenciosos** atrás de popups (daemon de automatização) | `grep "MonitorSilent/PopupWatch/WatchPopup/DaemonMonitor" em src/` -> **0 hits**. O mais proximo e deteccao de desinstalacao externa (`src/Tranal.Uninstaller/Core/Discovery/ExternalUninstallWatcher.cs:50`), que le o registro depois do fato; nao ha daemon nem vigia de popup. Binario do concorrente: `bcu.strings.txt:5751` (`checkBoxAutoDaemon`) |
| 52 | Instalador gerado automaticamente para **app não registrado** ("Simple Delete") | `grep "UninstallerKind.SimpleDelete" em src/` -> 3 hits, todos de classificacao (`src/Tranal.Uninstaller/Core/UninstallerKind.cs:74`, `:215`) e de legenda (`src/Tranal.Uninstaller/Core/Appearance/ColorLegend.cs:178`): **nada gera instalador** para app nao registrado. `docs/PARIDADE.md` §8 ("Fora de escopo") nao registra decisao sobre este item. Binario do concorrente: `UninstallerType_SimpleDelete` — nao confirmado no binario (so `.resx`) |
| 81 | **Hotkeys globais** do sistema, registrados pelo processo | `grep "RegisterHotKey/Hotkey/GlobalHotkey" em src/` -> **0 hits**. Binario do concorrente: `bcu.strings.txt:6715` (`GlobalHotkeys`) |
| 83 | **Relatório de erro automático** (crash report) com envio pela web | `grep "CrashReport/NBug/EnviarRelatorio/SubmitCrash" em src/` -> **0 hits**. O Tranal grava log local (`src/Tranal.Uninstaller/FatalLog.cs:13`, escrita em `:32`) e coleta dumps do Windows (`src/Tranal.Uninstaller/Core/Modules/CrashDumpCollector.cs:116`, painel em `src/Tranal.Uninstaller/Ui/Tools/MaintenanceViews.cs:301`), mas **nao gera relatorio de erro automatico nem envia pela web**. Binario do concorrente: `bcu.strings.txt:5611` (`NBug.Core.Submission`) |
| 90 | **Limpar caches** da aplicação | `grep "ClearCache/LimparCache" em src/` -> **0 hits**; ha limpeza de cache de navegador (`src/Tranal.Uninstaller/Core/Tools/JunkCleaner.cs:239`, categoria `browser-cache`), mas **nenhuma acao de limpar o cache do programa que esta sendo desinstalado**. Binario do concorrente: `bcu.strings.txt:7412` ("Clear caches") |

### HIBIT — 3

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 17 | **Menu de contexto do Monitor de Instalação**: `Msi.Package\shell\HiBitMonitor` e `exefile\shell\HiBitMonitor` | `grep "HiBitMonitor/Msi.Package" em src/` -> **0 hits**. `src/Tranal.Uninstaller/Core/Topmost/ExplorerContextMenu.cs:37` registra **apenas** a entrada do Tranal (`Software\Classes\*\shell\TranalUninstaller`, `:40` o comando); nao ha segunda entrada para o monitor de instalacao. Binario do concorrente: `hibit.recursos.txt:172` (`TINSTALLMONITORFORM/0`) |
| 62 | Botão **Doar** (adicionado na 4.0.10) | `grep "Doar/Donate/Doacao" em src/` -> **0 hits**. Funcao sem equivalente; irrelevante para paridade funcional. Binario do concorrente: nao confirmado no binario |
| 100 | **Banco local embutido** (`LOCALDB`) | `grep "LOCALDB/Sqlite/Microsoft.Data.Sqlite" em src/` -> **0 hits**. O estado e JSON: `src/Tranal.Uninstaller/Core/Settings/ProgramCache.cs:154` (`ProgramCacheStore`), `:188` (salva), `:252` (carrega); o log de instalacao vive em `src/Tranal.Uninstaller/Core/Modules/TraceStore.cs:68`. **Nao ha banco local**. Binario do concorrente: `hibit.recursos.txt:127` (`LOCALDB/1033`) |

### IOBIT — 8

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 7 | **Stubborn Program Remover** com base de **4.000+ programas** travados | **Stubborn Program Remover** com base de **4.000+ programas** travados/grep "Stubborn" em src/ -> 0 hits o Tranal tem desinstalacao forcada por alvo (`Core/Automation/ForcedRemovalOptions.cs`) e remocao manual assistida (`Core/Automation/ManualRemoval.cs`) — mas nao ha base de conhecimento |
| 19 | Log guardado em **SQLite** (`sqlite3.dll`, 37 funções importadas) | Log guardado em **SQLite** (`sqlite3.dll`, 37 funções importadas)/grep "sqlite3" em src/ -> 0 hits o log e JSON (`Core/Tracing/TraceLogV2.cs`); a limpeza do navegador apaga os arquivos `.sqlite` do Firefox por nome, sem motor SQLite embarcado (`Core/Tools/InternetCleaner.cs:440`) |
| 23 | Botão **Shred no Explorer** | Botão de destruição irreversível no Explorer/menu de contexto do Explorer `Core/Management/ContextMenuManager.cs:46` |
| 25 | **Bloqueio de popups** de notificação | **Bloqueio de popups** de notificação/grep "ActionCenter" em src/ -> 0 hits o Software Health tem a categoria de notificacoes (`Core/Management/SoftwareHealth.cs`) como estado do programa, nao como bloqueio |
| 33 | Aviso de **sincronização do Chrome** ao remover extensão | Aviso de **sincronização do Chrome** ao remover extensão/`src/Tranal.Uninstaller/Core/Modules/BrowserExtensions.cs:53` (`public static`) a remocao de extensao acontece sem aviso de sincronizacao; o aviso de navegador em execucao existe, mas e outro assunto (`Core/Tools/OperationGuards.cs:68`) |
| 34 | Integração com a **Central de Ações do Windows** (Action Center) | Integração com a **Central de Ações do Windows** (Action Center)/grep "ActionCenter" em src/ -> 0 hits |
| 36 | **Voto / avaliação** de programas e plugins pela comunidade | **Voto / avaliação** de programas e plugins pela comunidade/a avaliacao local por estrelas existe (`Core/Appearance/ProgramRating.cs`), mas nao ha coleta de votos da comunidade |
| 37 | **Captura de tela** do sistema no fluxo de suporte | **Captura de tela** do sistema no fluxo de suporte/grep "screenshot" em src/ -> 0 hits o pedido de suporte monta texto e URL com os dados do programa (`Core/Appearance/SupportRequest.cs`) |

### SOFTORGANIZER — 10

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 15 | Notificações internas (sino) e dicas | Notificações internas (sino) e dicas/grep "BellIcon" em src/ -> 0 hits a categoria "Notificacoes" do Software Health (`Core/Management/SoftwareHealth.cs`) trata do estado dos programas, nao de avisos internos do aplicativo |
| 29 | Aviso para melhorar a qualidade do rastreio | Aviso para melhorar a qualidade do rastreio/grep "TraceQuality" em src/ -> 0 hits |
| 41 | **Monitor em processo separado** (app de rastreamento) | **Monitor em processo separado** (app de rastreamento)/a solucao tem apenas `Tranal.Uninstaller.csproj` (WPF), `Tranal.Cli.csproj` e `Tranal.Tests.csproj` — nao ha processo separado de rastreamento |
| 53 | Instalação de novas versões | Instalação de novas versões/grep "MsiInstallProduct" em src/ -> 0 hits |
| 54 | **Centro de dicas "Did You Know"** com dicas contextuais (arrastar para desinstalar, desinstalação externa, atualização, multilíngue) | **Centro de dicas "Did You Know"** com dicas contextuais/grep "DidYouKnow" em src/ -> 0 hits |
| 57 | **Carga e descarregamento de chaves** e substituição de chave | **Carga e descarregamento de chaves** e substituição de chave/grep "RegLoadKey" em src/ -> 0 hits |
| 61 | **Descoberta de dispositivos Bluetooth LE** | Descoberta de dispositivos Bluetooth LE/grep "Bluetooth" em src/ -> 0 hits |
| 63 | Informações de **usuário e estação de rede** | Informações de **usuário e estação de rede**/grep "NetUserGetInfo" em src/ -> 0 hits |
| 76 | **Impersonação de token** e verificação de privilégios | **Impersonação de token** e verificação de privilégios/grep "ImpersonateLoggedOnUser" em src/ -> 0 hits |
| 77 | **Notificações internas agendadas** (avisos de dica) | **Notificações internas agendadas** (avisos de dica)/grep "ScheduledNotification" em src/ -> 0 hits |

### TOTALUNINSTALL — 3

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 18 | **Perfis** separados: Scanning, Analysis, Uninstall, Comparing, Restore/Redo, Cleaner, Auto-run view | **Perfis** separados: Scanning, Analysis, Uninstall, Comparing, Restore/Redo, Cleaner, Auto-run view/grep "ScanProfile" em src/ -> 0 hits existe o filtro de lista (`ProgramFilterSet`), mas nao ha perfis de operacao. |
| 21 | **Visões de mudança** configuráveis ("Change views") | **Visões de mudança** configuráveis ("Change views")/grep "ChangeView" em src/ -> 0 hits o que existe e o modo de exibicao da lista (Details/List/Icons), nao a visao de mudancas. |
| 50 | **Agente autônomo** de baixo consumo para notificar instalações em andamento | **Agente autônomo** de baixo consumo para notificar instalações em andamento/grep "Unfinished" em src/ -> 0 hits o rastreio roda no processo do aplicativo (`Core/Modules/InstallMonitor.cs`) e morre com ele; nao ha agente nem daemon. |

### UNINSTALLTOOL — 15

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 39 | **Anexar** dados rastreados a um item existente | **Anexar** dados rastreados a um item existente/`src/Tranal.Uninstaller/Core/Tracing/TraceLogV2.cs:42` (`public sealed class TraceLogV2`) a fusao de logs do mesmo programa existe, mas nao ha acao de anexar um log a um item da lista. |
| 44 | Menu de contexto do Explorer: **"Install and Trace"** para `.exe`/`.msi` | Menu de contexto do Explorer: **"Install and Trace"** para `.exe`/`.msi`/gerenciador do menu de contexto do Tranal `Core/Management/AppPermissionManager.cs:120` |
| 46 | Extensão de shell **empacotada em MSIX** para o Windows 11 | Extensão de shell **empacotada em MSIX** para o Windows 11/o instalador (`installer/tranal.iss`) cria grupo e area de trabalho; nao ha empacotamento MSIX da extensao de shell. |
| 47 | Serviço de rastreamento **versionado**, com prompt de atualização | Serviço de rastreamento **versionado**, com prompt de atualização/`src/Tranal.Uninstaller/Core/Tracing/WriteWatcher.cs:35` (`public sealed class WriteWatcher`) o rastreio roda no processo do aplicativo; nao ha servico nem prompt de atualizacao de versao. |
| 69 | **Ícone do Uninstall Tool no Painel de Controle** | **Ícone do Uninstall Tool no Painel de Controle**/o instalador cria grupo e atalho na area de trabalho; nao ha entrada no Painel de Controle. |
| 70 | **Popup de aplicações recém-instaladas** | **Popup de aplicações recém-instaladas**/grep "RecentAppsPopup" em src/ -> 0 hits |
| 71 | **Realce dos programas recém-instalados** | **Realce dos programas recém-instalados**/`src/Tranal.Uninstaller/MainWindow.xaml.cs:221` (`Status.Installations`) o realce existente e por filtro e cor, nao por data de instalacao. |
| 72 | **Programas recentes na barra secundária** | **Programas recentes na barra secundária**/`src/Tranal.Uninstaller/Core/Settings/AppSettings.cs:315` (`public static class AppSettings`) `AppSettings.RecentPrograms` e a lista do menu de acesso rapido, nao a barra secundaria. |
| 76 | **Minimizar na bandeja durante a instalação** | **Minimizar na bandeja durante a instalação**/grep "NotifyIcon" em src/ -> 0 hits |
| 77 | **Exibir o ícone na bandeja do sistema** | **Exibir o ícone na bandeja do sistema**/grep "NotifyIcon" em src/ -> 0 hits |
| 78 | **Iniciar automaticamente minimizado na bandeja** | **Iniciar automaticamente minimizado na bandeja**/grep "NotifyIcon" em src/ -> 0 hits |
| 79 | **Navegar até a pasta do programa com o Total Commander** | **Navegar até a pasta do programa com o Total Commander**/grep "TotalCommander" em src/ -> 0 hits |
| 82 | **Lista compacta de aplicativos** | **Lista compacta de aplicativos**/grep "SmallIcons" em src/ -> 0 hits a lista usa sempre o mesmo conjunto de colunas. |
| 85 | **Painéis ancoráveis** ("push pin") com pré-visualização | **Painéis ancoráveis** ("push pin") com pré-visualização/grep "PushPin" em src/ -> 0 hits |
| 86 | **Execução de processo 32 bits** (`CreateX86Process`) | **Execução de processo 32 bits** (`CreateX86Process`)/grep "CreateX86Process" em src/ -> 0 hits o Tranal so le o `WOW6432Node` do registro (`Core/Modules/SystemSnapshot.cs`). |

### UNINSTALR — 2

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 30 | Opções de **acessibilidade** (leitor de tela, moldura padrão, botões padrão) | Opções de **acessibilidade** (leitor de tela, moldura padrão, botões padrão)/grep "Accessibility" em src/ -> 0 hits o WPF entrega a acessibilidade de sistema; nao ha as opcoes do concorrente. |
| 47 | **Opções "speedhacks"** (desativar verificações lentas) | Opções "speedhacks" (desativar verificações lentas)/grep "SpeedHack" em src/ -> 0 hits nao ha a opcao de desligar as verificacoes lentas. |

### WISE — 5

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 45 | Diálogo de **notificações** com remetente e tipo | Diálogo de **notificações** com remetente e tipo/`src/Tranal.Uninstaller/Core/Management/SoftwareHealth.cs:105` (`public static class SoftwareHealth`) a categoria de notificacoes do Software Health trata do estado do programa, nao de notificacoes do sistema. |
| 50 | Bloqueio de notificações pop-up (regra `PopNotify`) | Bloqueio de notificações pop-up (regra `PopNotify`)/grep "PopNotify" em src/ -> 0 hits os tipos de regra existentes sao as categorias Include, Exclude, Protect e Cleanup (`Core/Rules/RemovalRule.cs`). |
| 55 | Tabela de caminhos de registro por app, com marcação `(Reg)`: **48 entradas, 14 com `(Reg)`** | Tabela de caminhos de registro por app, com marcação `(Reg)`/grep "TabelaDeRegistros" em src/ -> 0 hits nao ha tabela de chaves de registro por aplicativo. |
| 82 | Acesso ao **gerenciador de credenciais** do Windows | Acesso ao **gerenciador de credenciais** do Windows/grep "CredentialManager" em src/ -> 0 hits a busca por "CredRead" e "CredEnumerate" em `src/` nao retorna hits. |
| 83 | Detecção de **geolocalização** | Detecção de **geolocalização**/grep "Geolocation" em src/ -> 0 hits |
