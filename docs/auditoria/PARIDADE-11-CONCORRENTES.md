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
| ASHAMPOO | 76 | 42 | 21 | 9 | 4 | 0 | 60.5% |
| BCU | 97 | 64 | 23 | 8 | 2 | 0 | 68% |
| GEEK | 44 | 29 | 13 | 2 | 0 | 0 | 65.9% |
| HIBIT | 66 | 38 | 20 | 4 | 4 | 0 | 63.6% |
| IOBIT | 40 | 18 | 8 | 10 | 4 | 0 | 55% |
| SOFTORGANIZER | 77 | 47 | 18 | 11 | 1 | 0 | 62.3% |
| TOTALUNINSTALL | 94 | 55 | 28 | 7 | 4 | 0 | 62.8% |
| UNINSTALLTOOL | 94 | 44 | 27 | 18 | 5 | 0 | 52.1% |
| UNINSTALR | 51 | 34 | 12 | 2 | 3 | 0 | 72.5% |
| WISE | 86 | 46 | 30 | 7 | 3 | 0 | 57% |
| REVO PRO 5.5.4 | 70 | 68 | 0 | 0 | 2 | 0 | 100% |
| **TOTAL** | **795** | **485** | **200** | **78** | **32** | **0** | **65%** |

Apuracoes linha a linha aplicadas: 506.

## O que ainda falta (estado AUSENTE)

### ASHAMPOO — 9

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 63 | Categoria automática expandida + reset para "Unsorted" | Categoria automática expandida + reset para "Unsorted"/grep "Unsorted" em src/ -> 0 hits o agrupamento por coluna existe (`Core/ProgramColumns.cs`), mas nao ha a categoria "Unsorted" nem o reset para ela |
| 3 | Ícone na bandeja do sistema para o monitor de instalação, com acionamento manual | Ícone na bandeja do sistema para o monitor de instalação, com acionamento manual/grep "NotifyIcon" em src/ -> 0 hits o monitor de instalacao existe (`Core/Modules/InstallMonitor.cs`), mas a bandeja nao |
| 12 | Importar logs do UnInstaller 4 | Importar logs do UnInstaller 4/grep "Uninstaller4" em src/ -> 0 hits o importador valida o formato e recusa qualquer outro (`Core/Modules/TraceStore.cs`) |
| 61 | Instal guard: definir como o programa identifica setups e quais propriedades de arquivo usar | Instal guard: definir como o programa identifica setups e quais propriedades de arquivo usar/grep "InstallerGuard" em src/ -> 0 hits o Tranal identifica o instalador pelo tipo de desinstalador (`Core/UninstallerKind.cs`) e pelo log do instalador (`Core/MsiInstallLog.cs`), mas nao ha o painel de configuracao do "instal guard". |
| 44 | File Associator (tipos de arquivo sem programa associado, editar/undo) | File Associator (tipos de arquivo sem programa associado, editar/undo)/grep "FileAssoc" em src/ -> 0 hits nao ha painel de associacoes de arquivo |
| 48 | Boot Center: resumo de tempo de boot, autorun e tarefas agendadas (enable/disable/delete) | Boot Center: resumo de tempo de boot, autorun e tarefas agendadas (enable/disable/delete)/grep "BootTime" em src/ -> 0 hits o Tranal gerencia o autorun (`Core/Tools/AutorunManager.cs`) e as tarefas agendadas (`Core/Management/ScheduledTaskManager.cs`), mas nao ha o resumo de tempo de boot nem a tela que junta os tres |
| 54 | **Hibernation**: colocar um programa para dormir (desabilita autostart e serviços) e acordá-lo depois | **Hibernation**: colocar um programa para dormir (desabilita autostart e serviços) e acordá-lo depois/grep "Hibernat" em src/ -> 0 hits o Tranal desabilita o autorun e os servicos de um programa (`Core/Management/AppPermissionManager.cs`), mas nao existe a operacao de suspender e acordar. |
| 73 | Instalador protegido por senha (criptografia ChaCha20 do setup) | Instalador protegido por senha (criptografia ChaCha20 do setup)/grep "Password" em `installer/tranal.iss` -> 0 hits; o Inno Setup do Tranal nao define `Password=` nem `Encryption=` |
| 75 | Manifesto que redireciona DLLs de sistema (`comctl32`, `mpr`, `netapi32`, `netutils`, `textshaping`, `version`) | Manifesto que redireciona DLLs de sistema (`comctl32`, `mpr`, `netapi32`, `netutils`, `textshaping`, `version`)/grep "comctl32" e "loadFrom" em `installer/tranal.iss` -> 0 hits |

### BCU — 8

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 30 | **Monitorar desinstaladores silenciosos** atrás de popups (daemon de automatização) | `grep "MonitorSilent/PopupWatch/WatchPopup/DaemonMonitor" em src/` -> **0 hits**. O mais proximo e deteccao de desinstalacao externa (`src/Tranal.Uninstaller/Core/Discovery/ExternalUninstallWatcher.cs:50`), que le o registro depois do fato; nao ha daemon nem vigia de popup. Binario do concorrente: `bcu.strings.txt:5751` (`checkBoxAutoDaemon`) |
| 52 | Instalador gerado automaticamente para **app não registrado** ("Simple Delete") | `grep "UninstallerKind.SimpleDelete" em src/` -> 3 hits, todos de classificacao (`src/Tranal.Uninstaller/Core/UninstallerKind.cs:74`, `:215`) e de legenda (`src/Tranal.Uninstaller/Core/Appearance/ColorLegend.cs:178`): **nada gera instalador** para app nao registrado. `docs/PARIDADE.md` §8 ("Fora de escopo") nao registra decisao sobre este item. Binario do concorrente: `UninstallerType_SimpleDelete` — nao confirmado no binario (so `.resx`) |
| 81 | **Hotkeys globais** do sistema, registrados pelo processo | `grep "RegisterHotKey/Hotkey/GlobalHotkey" em src/` -> **0 hits**. Binario do concorrente: `bcu.strings.txt:6715` (`GlobalHotkeys`) |
| 82 | **Colocar o PC para dormir** quando a tarefa termina, com contagem regressiva | `grep "SetSuspendState/PutToSleep/Suspend/Hibernat" em src/` -> **0 hits** (o unico `Sleep` e `Thread.Sleep`). O que ha e o contrario: `src/Tranal.Uninstaller/Core/Automation/ShutdownGuard.cs:56` impede o desligamento durante a tarefa. Binario do concorrente: `bcu.strings.txt:5854` (`checkBoxFinishSleep`) |
| 83 | **Relatório de erro automático** (crash report) com envio pela web | `grep "CrashReport/NBug/EnviarRelatorio/SubmitCrash" em src/` -> **0 hits**. O Tranal grava log local (`src/Tranal.Uninstaller/FatalLog.cs:13`, escrita em `:32`) e coleta dumps do Windows (`src/Tranal.Uninstaller/Core/Modules/CrashDumpCollector.cs:116`, painel em `src/Tranal.Uninstaller/Ui/Tools/MaintenanceViews.cs:301`), mas **nao gera relatorio de erro automatico nem envia pela web**. Binario do concorrente: `bcu.strings.txt:5611` (`NBug.Core.Submission`) |
| 89 | **Adicionar os selecionados como filtro** (a seleção vira critério) | `grep "AddSelectedAsFilter/SelecionadosComoFiltro" em src/` -> **0 hits**; o filtro da lista e construido por campos fixos (`src/Tranal.Uninstaller/Core/Appearance/ProgramFilters.cs:147`) e nao pela selecao. Binario do concorrente: `bcu.strings.txt:6620` (`toolStripButtonAddSelectedAsFilters`) |
| 90 | **Limpar caches** da aplicação | `grep "ClearCache/LimparCache" em src/` -> **0 hits**; ha limpeza de cache de navegador (`src/Tranal.Uninstaller/Core/Tools/JunkCleaner.cs:239`, categoria `browser-cache`), mas **nenhuma acao de limpar o cache do programa que esta sendo desinstalado**. Binario do concorrente: `bcu.strings.txt:7412` ("Clear caches") |
| 93 | **Compressão Brotli** em backups/arquivos gerados | `grep "Brotli" em src/` -> **0 hits**; os backups sao JSON + arquivos (`src/Tranal.Uninstaller/Core/BackupManager.cs:282` `Save`). Binario do concorrente: `bcu.strings.txt:5103` (`System.IO.Compression.Brotli`) |

### GEEK — 2

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 14 | Diálogo de falha de desinstalação com opção de **limpar a entrada do registro e os arquivos** | Diálogo de falha de desinstalação com opção de **limpar a entrada do registro e os arquivos**/grep "msgUninstallFailed" em src/ -> 0 hits existe o relatorio de remocao com codigo de erro (`Core/Removal.cs`) e a desinstalacao forcada como caminho separado (`Ui/ForcedUninstallWindow.xaml.cs`), mas nao o dialogo que pergunta pela limpeza. |
| 42 | **Enumeração por SID do usuário** dos pacotes MSIX | **Enumeração por SID do usuário** dos pacotes MSIX/`src/Tranal.Uninstaller/Core/Modules/WindowsApps.cs:39` (`public static class WindowsApps`) a listagem e so do usuario atual: `Get-AppxPackage` sem `-AllUsers` e sem `-PackageUserInformation`. |

### HIBIT — 4

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 17 | **Menu de contexto do Monitor de Instalação**: `Msi.Package\shell\HiBitMonitor` e `exefile\shell\HiBitMonitor` | `grep "HiBitMonitor/Msi.Package" em src/` -> **0 hits**. `src/Tranal.Uninstaller/Core/Topmost/ExplorerContextMenu.cs:37` registra **apenas** a entrada do Tranal (`Software\Classes\*\shell\TranalUninstaller`, `:40` o comando); nao ha segunda entrada para o monitor de instalacao. Binario do concorrente: `hibit.recursos.txt:172` (`TINSTALLMONITORFORM/0`) |
| 62 | Botão **Doar** (adicionado na 4.0.10) | `grep "Doar/Donate/Doacao" em src/` -> **0 hits**. Funcao sem equivalente; irrelevante para paridade funcional. Binario do concorrente: nao confirmado no binario |
| 91 | **Criar item de inicialização** do zero | `grep "AutorunManager.Add" em src/` -> **1 hit**, a definicao em `src/Tranal.Uninstaller/Core/Tools/AutorunManager.cs:189` (`Add`), **sem consumidor** em `Ui/` nem na CLI — o painel so expoe Refresh/Enable/Disable/Remove (`src/Tranal.Uninstaller/Ui/Tools/ToolViews.cs:145-148`) e a CLI so lista (`src/Tranal.Cli/ExtraCommands.cs:200`). Binario do concorrente: `hibit.recursos.txt:145` (`TADDSTARTUPFORM/0`) |
| 100 | **Banco local embutido** (`LOCALDB`) | `grep "LOCALDB/Sqlite/Microsoft.Data.Sqlite" em src/` -> **0 hits**. O estado e JSON: `src/Tranal.Uninstaller/Core/Settings/ProgramCache.cs:154` (`ProgramCacheStore`), `:188` (salva), `:252` (carrega); o log de instalacao vive em `src/Tranal.Uninstaller/Core/Modules/TraceStore.cs:68`. **Nao ha banco local**. Binario do concorrente: `hibit.recursos.txt:127` (`LOCALDB/1033`) |

### IOBIT — 10

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 7 | **Stubborn Program Remover** com base de **4.000+ programas** travados | **Stubborn Program Remover** com base de **4.000+ programas** travados/grep "Stubborn" em src/ -> 0 hits o Tranal tem desinstalacao forcada por alvo (`Core/Automation/ForcedRemovalOptions.cs`) e remocao manual assistida (`Core/Automation/ManualRemoval.cs`) — mas nao ha base de conhecimento |
| 11 | Confirmar/avisar antes de apagar **arquivo pessoal** | Confirmar/avisar antes de apagar **arquivo pessoal**/grep "PersonalFile" em src/ -> 0 hits a remocao segue a regra de protecao por lista de exclusoes e por confianca da sobra (`Core/UninstallSupport.cs`), sem uma categoria de "arquivo pessoal" |
| 19 | Log guardado em **SQLite** (`sqlite3.dll`, 37 funções importadas) | Log guardado em **SQLite** (`sqlite3.dll`, 37 funções importadas)/grep "sqlite3" em src/ -> 0 hits o log e JSON (`Core/Tracing/TraceLogV2.cs`); a limpeza do navegador apaga os arquivos `.sqlite` do Firefox por nome, sem motor SQLite embarcado (`Core/Tools/InternetCleaner.cs:440`) |
| 23 | Botão **Shred no Explorer** | Botão de destruição irreversível no Explorer/menu de contexto do Explorer `Core/Management/ContextMenuManager.cs:46` |
| 25 | **Bloqueio de popups** de notificação | **Bloqueio de popups** de notificação/grep "ActionCenter" em src/ -> 0 hits o Software Health tem a categoria de notificacoes (`Core/Management/SoftwareHealth.cs`) como estado do programa, nao como bloqueio |
| 33 | Aviso de **sincronização do Chrome** ao remover extensão | Aviso de **sincronização do Chrome** ao remover extensão/`src/Tranal.Uninstaller/Core/Modules/BrowserExtensions.cs:53` (`public static`) a remocao de extensao acontece sem aviso de sincronizacao; o aviso de navegador em execucao existe, mas e outro assunto (`Core/Tools/OperationGuards.cs:68`) |
| 34 | Integração com a **Central de Ações do Windows** (Action Center) | Integração com a **Central de Ações do Windows** (Action Center)/grep "ActionCenter" em src/ -> 0 hits |
| 36 | **Voto / avaliação** de programas e plugins pela comunidade | **Voto / avaliação** de programas e plugins pela comunidade/a avaliacao local por estrelas existe (`Core/Appearance/ProgramRating.cs`), mas nao ha coleta de votos da comunidade |
| 37 | **Captura de tela** do sistema no fluxo de suporte | **Captura de tela** do sistema no fluxo de suporte/grep "screenshot" em src/ -> 0 hits o pedido de suporte monta texto e URL com os dados do programa (`Core/Appearance/SupportRequest.cs`) |
| 38 | **Catálogo embutido de Appx pré-instalados** (a lista do que o Windows traz de fábrica) | **Catálogo embutido de Appx pré-instalados** (a lista do que o Windows traz de fábrica)/o Tranal lista os pacotes MSIX instalados (`Core/Modules/WindowsApps.cs`) e detecta o bloatware por marcadores de pasta (`Core/Discovery/BundlewareDetector.cs`), mas nao tem a lista de fabrica do Windows |

### SOFTORGANIZER — 11

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 15 | Notificações internas (sino) e dicas | Notificações internas (sino) e dicas/grep "BellIcon" em src/ -> 0 hits a categoria "Notificacoes" do Software Health (`Core/Management/SoftwareHealth.cs`) trata do estado dos programas, nao de avisos internos do aplicativo |
| 29 | Aviso para melhorar a qualidade do rastreio | Aviso para melhorar a qualidade do rastreio/grep "TraceQuality" em src/ -> 0 hits |
| 40 | Rastreamento de **DLLs em pastas do sistema** | Rastreamento de **DLLs em pastas do sistema**/`src/Tranal.Uninstaller/Core/Modules/SystemSnapshot.cs:43` (`public sealed class SystemSnapshot`) **Nota**: as raizes varridas sao Program Files, Program Files (x86), CommonApplicationData, LocalApplicationData e ApplicationData; `C:\Windows` nao entra, e DLL nao e um motivo de sobra. |
| 41 | **Monitor em processo separado** (app de rastreamento) | **Monitor em processo separado** (app de rastreamento)/a solucao tem apenas `Tranal.Uninstaller.csproj` (WPF), `Tranal.Cli.csproj` e `Tranal.Tests.csproj` — nao ha processo separado de rastreamento |
| 53 | Instalação de novas versões | Instalação de novas versões/grep "MsiInstallProduct" em src/ -> 0 hits |
| 54 | **Centro de dicas "Did You Know"** com dicas contextuais (arrastar para desinstalar, desinstalação externa, atualização, multilíngue) | **Centro de dicas "Did You Know"** com dicas contextuais/grep "DidYouKnow" em src/ -> 0 hits |
| 57 | **Carga e descarregamento de chaves** e substituição de chave | **Carga e descarregamento de chaves** e substituição de chave/grep "RegLoadKey" em src/ -> 0 hits |
| 61 | **Descoberta de dispositivos Bluetooth LE** | Descoberta de dispositivos Bluetooth LE/grep "Bluetooth" em src/ -> 0 hits |
| 63 | Informações de **usuário e estação de rede** | Informações de **usuário e estação de rede**/grep "NetUserGetInfo" em src/ -> 0 hits |
| 76 | **Impersonação de token** e verificação de privilégios | **Impersonação de token** e verificação de privilégios/grep "ImpersonateLoggedOnUser" em src/ -> 0 hits |
| 77 | **Notificações internas agendadas** (avisos de dica) | **Notificações internas agendadas** (avisos de dica)/grep "ScheduledNotification" em src/ -> 0 hits |

### TOTALUNINSTALL — 7

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 15 | Pasta de snapshots com limite de tamanho e contagem ("Limits for Snapshots folder") | Pasta de snapshots com limite de tamanho e contagem ("Limits for Snapshots folder")/`src/Tranal.Uninstaller/Core/Tracing/SnapshotLibrary.cs:60` (`public static class SnapshotLibrary`) a pasta e `SnapshotLibrary.SnapshotDirectory`, sem limite nem purga. |
| 18 | **Perfis** separados: Scanning, Analysis, Uninstall, Comparing, Restore/Redo, Cleaner, Auto-run view | **Perfis** separados: Scanning, Analysis, Uninstall, Comparing, Restore/Redo, Cleaner, Auto-run view/grep "ScanProfile" em src/ -> 0 hits existe o filtro de lista (`ProgramFilterSet`), mas nao ha perfis de operacao. |
| 21 | **Visões de mudança** configuráveis ("Change views") | **Visões de mudança** configuráveis ("Change views")/grep "ChangeView" em src/ -> 0 hits o que existe e o modo de exibicao da lista (Details/List/Icons), nao a visao de mudancas. |
| 23 | Opção de **não usar o log de mudanças detectadas** ("Don't use detected changes log") | Opção de **não usar o log de mudanças detectadas** ("Don't use detected changes log")/grep "UseChangeLog" em src/ -> 0 hits |
| 50 | **Agente autônomo** de baixo consumo para notificar instalações em andamento | **Agente autônomo** de baixo consumo para notificar instalações em andamento/grep "Unfinished" em src/ -> 0 hits o rastreio roda no processo do aplicativo (`Core/Modules/InstallMonitor.cs`) e morre com ele; nao ha agente nem daemon. |
| 83 | **Dependências entre pacotes MSI** ("Depends on packages") | **Dependências entre pacotes MSI** ("Depends on packages")/`src/Tranal.Uninstaller/Core/Discovery/MsiProductCatalog.cs:74` (`public static class MsiProductCatalog`) o catalogo le o produto MSI, mas nao resolve dependencia entre pacotes. |
| 84 | **Verificação de DCOM** do produto | **Verificação de DCOM** do produto/grep "DCOM" em src/ -> 0 hits o que existe e o inverso: desregistrar COM do programa desinstalado (`Core/Automation/ComUnregister.cs`). |

### UNINSTALLTOOL — 18

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 39 | **Anexar** dados rastreados a um item existente | **Anexar** dados rastreados a um item existente/`src/Tranal.Uninstaller/Core/Tracing/TraceLogV2.cs:42` (`public sealed class TraceLogV2`) a fusao de logs do mesmo programa existe, mas nao ha acao de anexar um log a um item da lista. |
| 44 | Menu de contexto do Explorer: **"Install and Trace"** para `.exe`/`.msi` | Menu de contexto do Explorer: **"Install and Trace"** para `.exe`/`.msi`/gerenciador do menu de contexto do Tranal `Core/Management/AppPermissionManager.cs:120` |
| 46 | Extensão de shell **empacotada em MSIX** para o Windows 11 | Extensão de shell **empacotada em MSIX** para o Windows 11/o instalador (`installer/tranal.iss`) cria grupo e area de trabalho; nao ha empacotamento MSIX da extensao de shell. |
| 47 | Serviço de rastreamento **versionado**, com prompt de atualização | Serviço de rastreamento **versionado**, com prompt de atualização/`src/Tranal.Uninstaller/Core/Tracing/WriteWatcher.cs:35` (`public sealed class WriteWatcher`) o rastreio roda no processo do aplicativo; nao ha servico nem prompt de atualizacao de versao. |
| 55 | Itens de inicialização de **aplicativos UWP / Store** | Itens de inicialização de **aplicativos UWP / Store**/grep "StartupTask" em src/ -> 0 hits `Core/Modules/WindowsApps.cs` le o MSIX instalado, nao o item de inicializacao do Store. |
| 57 | Tarefas criadas pela Microsoft **ocultadas** por segurança | Tarefas criadas pela Microsoft **ocultadas** por segurança/`src/Tranal.Uninstaller/Core/Management/ScheduledTaskManager.cs:72` (`public static class ScheduledTaskManager`) a listagem traz todas as tarefas agendadas; nao ha filtro nem ocultacao das tarefas da Microsoft. |
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
| 92 | **Validação de cadeia de certificados** embarcada (CRYPTO/SSL) | **Validação de cadeia de certificados** embarcada (CRYPTO/SSL)/grep "CertGetCertificateChain" em src/ -> 0 hits o que existe e a validacao da assinatura do desinstalador (`WinVerifyTrust` com `X509Chain`), que e outro objeto. |

### UNINSTALR — 2

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 30 | Opções de **acessibilidade** (leitor de tela, moldura padrão, botões padrão) | Opções de **acessibilidade** (leitor de tela, moldura padrão, botões padrão)/grep "Accessibility" em src/ -> 0 hits o WPF entrega a acessibilidade de sistema; nao ha as opcoes do concorrente. |
| 47 | **Opções "speedhacks"** (desativar verificações lentas) | Opções "speedhacks" (desativar verificações lentas)/grep "SpeedHack" em src/ -> 0 hits nao ha a opcao de desligar as verificacoes lentas. |

### WISE — 7

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 39 | "Stop" para interromper a varredura | "Stop" para interromper a varredura/grep "CancelarVarredura" em src/ -> 0 hits as varreduras rodam em `Task.Run` sem cancelamento — a busca por "CancellationToken" em `src/` so encontra o plano de desinstalacao e o atualizador. |
| 45 | Diálogo de **notificações** com remetente e tipo | Diálogo de **notificações** com remetente e tipo/`src/Tranal.Uninstaller/Core/Management/SoftwareHealth.cs:105` (`public static class SoftwareHealth`) a categoria de notificacoes do Software Health trata do estado do programa, nao de notificacoes do sistema. |
| 46 | Registro de "última análise" | Registro de "última análise"/grep "UltimaAnalise" em src/ -> 0 hits nao ha registro da ultima analise — a busca por "LastScan" em `src/` nao retorna hits. |
| 50 | Bloqueio de notificações pop-up (regra `PopNotify`) | Bloqueio de notificações pop-up (regra `PopNotify`)/grep "PopNotify" em src/ -> 0 hits os tipos de regra existentes sao as categorias Include, Exclude, Protect e Cleanup (`Core/Rules/RemovalRule.cs`). |
| 55 | Tabela de caminhos de registro por app, com marcação `(Reg)`: **48 entradas, 14 com `(Reg)`** | Tabela de caminhos de registro por app, com marcação `(Reg)`/grep "TabelaDeRegistros" em src/ -> 0 hits nao ha tabela de chaves de registro por aplicativo. |
| 82 | Acesso ao **gerenciador de credenciais** do Windows | Acesso ao **gerenciador de credenciais** do Windows/grep "CredentialManager" em src/ -> 0 hits a busca por "CredRead" e "CredEnumerate" em `src/` nao retorna hits. |
| 83 | Detecção de **geolocalização** | Detecção de **geolocalização**/grep "Geolocation" em src/ -> 0 hits |
