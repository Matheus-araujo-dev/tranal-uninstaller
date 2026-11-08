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
| ASHAMPOO | 76 | 30 | 19 | 24 | 3 | 0 | 43.4% |
| BCU | 97 | 47 | 24 | 26 | 0 | 0 | 48.5% |
| GEEK | 44 | 27 | 10 | 7 | 0 | 0 | 61.4% |
| HIBIT | 66 | 29 | 22 | 11 | 4 | 0 | 50% |
| IOBIT | 40 | 13 | 7 | 16 | 4 | 0 | 42.5% |
| SOFTORGANIZER | 77 | 39 | 19 | 18 | 1 | 0 | 51.9% |
| TOTALUNINSTALL | 94 | 52 | 26 | 12 | 4 | 0 | 59.6% |
| UNINSTALLTOOL | 94 | 41 | 24 | 24 | 5 | 0 | 48.9% |
| UNINSTALR | 51 | 22 | 8 | 18 | 3 | 0 | 49% |
| WISE | 86 | 39 | 30 | 14 | 3 | 0 | 48.8% |
| REVO PRO 5.5.4 | 70 | 68 | 0 | 0 | 2 | 0 | 100% |
| **TOTAL** | **795** | **407** | **189** | **170** | **29** | **0** | **54.8%** |

Apuracoes linha a linha aplicadas: 432.

## O que ainda falta (estado AUSENTE)

### ASHAMPOO — 24

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 22 | Remover a entrada da lista do Windows de programas instalados, sem desinstalar | `grep RemoveProgramEntry\/HideProgram\/RemoveEntryFromList\/DeleteUninstallKey` sobre `src/` -> **0 hits**; a lista de exclusao **oculta da lista do Tranal** (`ExcludeList.cs:107`), nao remove a entrada `Uninstall` do Windows |
| 62 | Comparação de tamanho declarado vs. real do programa, sincronizável com o Windows | Existe estimativa e medicao isoladas; **nao ha** tela de comparacao nem sincronizacao |
| 63 | Categoria automática expandida + reset para "Unsorted" | Agrupamento por coluna existe; **nao ha** categoria "Unsorted" nem reset - `grep Unsorted/SemCategoria` -> 0 hits |
| 64 | País do editor com bandeira e nome | `grep Country/RegionInfo/flag` -> 0 hits |
| 3 | Ícone na bandeja do sistema para o monitor de instalação, com acionamento manual | `grep NotifyIcon\/TrayIcon\/SystemIcons` sobre `src/Tranal.Uninstaller` -> **0 hits**; o monitor existe (`Core/Modules/InstallMonitor.cs:103` `Start`), a bandeja nao |
| 12 | Importar logs do UnInstaller 4 | `grep Ashampoo\/Uninstaller4\/ThirdParty` -> **0 hits**; o importador valida o formato em `TraceStore.cs:626-628` e recusa qualquer outro |
| 13 | Abrir a pasta de logs no Explorer | `grep OpenLogsFolder\/LogDirectory\/LogsFolder\/OpenFolder` -> existe a **propriedade** do caminho (`RemovalLog.cs:168`), mas **nenhum comando que abra a pasta**; `grep explorer.exe` sobre `src/` -> 1 hit, e `Ui/Shared/ItemListPanel.xaml.cs:180` (`/select,` de um item), nao a pasta de logs |
| 15 | Análise forense em lote (processar todas as aplicações compatíveis de uma vez) | `grep Analyze` mostra so a assinatura por `InstalledProgram`; `SoftwareHealth.Analyze` (`:119`) varre todos, mas e health, nao forense |
| 61 | Instal guard: definir como o programa identifica setups e quais propriedades de arquivo usar | `grep InstallerGuard/SetupGuard` -> 0 hits |
| 41 | Drive Cleaner (arquivos desnecessários, com seleção por categoria e relatório) | A varredura do lixo nao tem filtro de tamanho |
| 42 | Internet Cleaner (cookies, histórico, exceções e "cookies seguros" / whitelist) | Nao ha whitelist de cookies; os 6 grupos sao fixos |
| 44 | File Associator (tipos de arquivo sem programa associado, editar/undo) | `grep FileAssoc/DefaultIcon/ProgId` -> 0 hits; nao ha painel de associacoes de arquivo |
| 48 | Boot Center: resumo de tempo de boot, autorun e tarefas agendadas (enable/disable/delete) | `grep BootTime\/BootTimeMs\/LastBootUpTime\/BootCenter` sobre `src/Tranal.Uninstaller` -> **0 hits** |
| 50 | **Unlocker**: desbloquear arquivos em uso, reiniciar o Explorer, desregistrar DLL, encerrar processo | Encerrar processo e adiar remocao para o reboot existem; **reinicio do Explorer e `DllUnregister` nao**: `grep regsvr32/DllUnregister/Explorer.*restart` -> 0 hits |
| 51 | **Crash Analyzer**: relatórios de falha do Windows Event Log, agrupados por programa, com export e busca online | O Tranal **limpa** Event Logs (6 grupos em `PrivacyTraceCleaner.cs:12-31`); nao agrupa crash por programa |
| 53 | **Software Updater**: atualizar programas instalados, em lote, com lista negra de atualizações | O feed do Tranal atualiza **so o proprio Tranal**; nao ha atualizador de terceiros |
| 54 | **Hibernation**: colocar um programa para dormir (desabilita autostart e serviços) e acordá-lo depois | `grep Hibernat/Suspend` -> 0 hits no `src/` (so `Thread.Sleep` em `Ui/Appearance/ProgramClipboard.cs:66`) |
| 55 | **Impact**: programas com maior impacto na performance (CPU e memória) | So ha ordenacao e mapa de **tamanho**; nenhuma medicao de CPU/memoria por programa |
| 56 | **Move**: mover um programa para outro local, recriando o atalho no lugar original | `grep Relocat/Move.*install` -> 0 hits; nenhuma operacao de mover/reverter a pasta de instalacao |
| 57 | **Revert relocation**: devolver o programa movido à posição original | `grep Relocat/Move.*install` -> 0 hits; nenhuma operacao de mover/reverter a pasta de instalacao |
| 30 | Remoção de Windows-Apps / system apps, com remoção parcial e restauração dos apps padrão | `grep RestoreDefault\/DefaultApps\/ProvisionedApp\/ResetApp\/DefaultApp` sobre `src/` -> **0 hits**; `WindowsApps.cs` expoe apenas `List` (`:55`) e `Uninstall` (`:96`) |
| 73 | Instalador protegido por senha (criptografia ChaCha20 do setup) | `grep Password` em `installer/tranal.iss` -> **0 hits**; o Inno Setup do Tranal nao define `Password=` nem `Encryption=` |
| 75 | Manifesto que redireciona DLLs de sistema (`comctl32`, `mpr`, `netapi32`, `netutils`, `textshaping`, `version`) | `grep manifest\/comctl32\/loadFrom` em `installer/tranal.iss` -> **0 hits** |
| 76 | Compilador Delphi (Embarcadero) do instalador e detecção de sistema | `ashampoo-instalador.inventario.md` → `Embarcadero Delphi for Win32 compiler version 36.0 (29.0.55362.2017)`; `ashampoo-instalador.identificadores.txt:22` → `Winapi.Windows.PkgHelper` |

### BCU — 26

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 13 | Marcação de **protegido** (tag `NoRemove` do editor) | literal "Protected uninstallers" / "Protected apps" (`bcu.inventario.md:292-293`), "Protected items are marked with the 'NoRemove' tag" (`:371`) |
| 16 | Marcação de **aplicativo inválido / desinstalador corrompido** | `checkBoxInvalidTest` (`bcu.strings.txt:6893`), literal "Use grey color to identify uninstallers that are corrupted or missing." (`bcu.inventario.md:146`) |
| 26 | **Desinstalação simultânea** com prevenção de colisão | `checkBoxConcurrent` (`bcu.strings.txt:6837`), `checkBoxConcurrentOneLoud` (`:4291`), literal "Disable collision prevention when running" (`bcu.inventario.md:15`) |
| 27 | **Ordenação inteligente**: loud primeiro, para poder sair antes | `checkBoxBatchSortQuiet` (`bcu.strings.txt:6758`), literal "Intelligent uninstaller sorting" (`bcu.inventario.md:180`) |
| 29 | **Impedir desligamento/reinício** durante a tarefa | `checkBoxShutdown` (`bcu.strings.txt:5789`), `PreventSleepOrShutdown` (`:5784`), `AllowSleepOrShutdown` (`:5785`) |
| 30 | **Monitorar desinstaladores silenciosos** atrás de popups (daemon de automatização) | `checkBoxAutoDaemon` (`bcu.strings.txt:5751`), literal "Monitor \"silent\" uninstallers for popups or other stalls" (`bcu.inventario.md:134`) |
| 32 | **Tentar novamente** os silenciosos que falharam | `checkBoxRetryQuiet` (`bcu.strings.txt:6759`), literal "Do you want to try running quiet uninstallers that failed as loud?" (`bcu.inventario.md:46`) |
| 36 | **Desinstalação silenciosa de quem não aceita silent**: oferecer rodar em primeiro plano ou remover da tarefa | literal "Following items are missing quiet uninstallers:" (`bcu.inventario.md:136`) |
| 40 | Lista de **aplicativos relacionados** que talvez queiram desinstalar junto | tipo `BulkCrapUninstaller.Forms.RelatedUninstallerAdder.resources` (`bcu.identificadores.txt:30`), literal "Related uninstaller name" (`bcu.inventario.md:314`) |
| 45 | Confiança de **inicialização**: entrada `RunOnce`, entrada compatível com o app | chaves `Confidence_Startup_*` — **nao confirmado no binario** (só `.resx`) |
| 52 | Instalador gerado automaticamente para **app não registrado** ("Simple Delete") | `UninstallerType_SimpleDelete` — **nao confirmado no binario** (só `.resx`) |
| 67 | **Abrir a chave no Regedit** | `openKeyInRegeditToolStripMenuItem` (`bcu.strings.txt:5505`) |
| 75 | **Gerenciador de desinstaladores relacionados** (`RelatedUninstallerAdder`) | tipo `BulkCrapUninstaller.Forms.RelatedUninstallerAdder.resources` (`bcu.identificadores.txt:30`) |
| 79 | Estatísticas anônimas de uso (opt-in) | `checkBoxSendStats` (`bcu.strings.txt:6651`), `UsageTracker` (`:5982`), `UsageTrackerSendData` (`:3865`), `hBulkCrapUninstaller.Functions.Tracking.UsageTracker` (`:7443`) |
| 81 | **Hotkeys globais** do sistema, registrados pelo processo | `GlobalHotkeys` (`bcu.strings.txt:6715`), `SetupHotkeys` (`:131`), `GetHotkeyList` (`:6918`), `HotkeyEntry` (`:7134`), `QKlocman.Subsystems.GlobalHotkeys, KlocTools` (`:8049`) |
| 82 | **Colocar o PC para dormir** quando a tarefa termina, com contagem regressiva | `checkBoxFinishSleep` (`bcu.strings.txt:5854`), `PutToSleep` (`:5855`), `sleepTimer` (`:6021`), `SleepControls` (`:6508`), `get_UninstallProgressWindow_StatusPuttingToSleepInSeconds` (`:6199`), literal "Put PC to sleep when done" (`bcu.inventario.md:298`) |
| 83 | **Relatório de erro automático** (crash report) com envio pela web | `NBug.Core.Submission` (`bcu.strings.txt:5611`), `NBugConfigurator` (`:6172`), `NBugDatabaseSenderWrapper` (`:6037`), `SetupNBug` (`:5052`), literal "Failed to send crash report: " (`:13083`) |
| 85 | **Desabilitar a proteção** de um item marcado como protegido | literais "Disable protection" (`bcu.strings.txt:7529`), "Modify protected item" (`:9131`), "Disable protection of uninstallers marked as \"protected\"" (`:8782`) |
| 86 | **Modificar a instalação** (reparo / alterar instalação) | literal "Modify application" (`bcu.strings.txt:9070`) |
| 88 | **Auto-ajustar a largura de todas as colunas** | literal "Auto-resize all columns" (`bcu.strings.txt:8114`) |
| 89 | **Adicionar os selecionados como filtro** (a seleção vira critério) | `toolStripButtonAddSelectedAsFilters` (`bcu.strings.txt:6620`), `toolStripButtonAddSelectedAsFilters_Click` (`:5235`) |
| 90 | **Limpar caches** da aplicação | literal "Clear caches" (`bcu.strings.txt:7412`), "Failed to delete cache: " (`bcu.strings.txt:13043`) |
| 91 | **Abrir a Limpeza de Disco do Windows** | literal "Start Disk Cleanup" (`bcu.strings.txt:8279`) |
| 92 | **Saída serializada em JSON** (além de XML/CSV) | `System.Text.Json` (`bcu.strings.txt:5754`), `JsonSerializer` (`:6119`), `JsonSerializerOptions` (`:6565`), `objToJsonserialize` (`:4909`) |
| 93 | **Compressão Brotli** em backups/arquivos gerados | `System.IO.Compression.Brotli` (`bcu.strings.txt:5103`), `BrotliStream` (`:5394`), `BrotliCompress` (`:6650`) |
| 95 | Filtro que **esconde o que foi publicado pela Microsoft** | literais "Hide published by Microsoft" (`bcu.inventario.md:160`), "Published by Microsoft" (`:296`), "Filter out everything that was published by Microsoft. It's that simple." (`:159`) |

### GEEK — 7

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 5 | Aviso "No matching items, press ESC to reset" | `grep "NoResults\/Sem resultados\/NoItemsMatching" src/` -> **0 hits**. A barra de status (`MainWindow.xaml.cs:176-178`) mostra a contagem mas **nao tem estado de "nada corresponde"**. O filtro tem validacao de regex (`Core/Appearance/ProgramFilters.cs:357-361`) e exclusoes, mas nenhum aviso |
| 14 | Diálogo de falha de desinstalação com opção de **limpar a entrada do registro e os arquivos** | `grep "msgUninstallFailed\/Falha na desinstalacao\/Limpar entrada" src/` -> **0 hits** de dialogo com a pergunta "a desinstalacao falhou, quer limpar a entrada e os arquivos?". Ha o **relatorio** de remocao com codigo de erro (`Core/Removal.cs:57` `ErrorCodeOf`, `Core/Removal.cs:86` `RemovalReport`) e a desinstalacao forcada como caminho separado (`Ui/ForcedUninstallWindow.xaml.cs`), mas **nao o dialogo** |
| 15 | "Browse..." dentro das sobras encontradas | `grep "Browse" src/` -> **0 hits** na UI de sobras. O menu de contexto da lista (`Ui/UninstallWizardWindow.xaml.cs:1015-1020`) so tem "Adicionar a lista de exclusao"; **nao ha acao de navegar** para o item |
| 33 | **Correspondência aproximada (Levenshtein)** de caminho de instalação | `grep "Levensh\/Damerau\/EditDistance" src/` -> **0 hits**. O que existe e variacao por **palavra inteira**: `Core/UninstallSupport.cs:292` (`ContainsWord`) e o motivo `SimilarNameMatch` (`:66`); nao ha distancia de edicao |
| 35 | **Limpeza do cache MRU do Windows** | `grep "MrtCache" src/` -> **0 hits**. O MRU existente e o do Office (`Core/Tools/TrackCleaner.cs:42`, `Scan` por modulo em `:81`) e o historico do Windows (`Core/Tools/HistoryCleaner.cs:55`), **nao** a chave `MrtCache` |
| 39 | **Painel "Active Files"** de arquivos em uso | `grep "ActiveFiles\/FileInUse\/InUse\/LockedFile" src/` -> **0 hits**. A **deteccao** existe (o item em uso vira "adiado para o proximo reinicio": `Core/DeferredDelete.cs:27`, `Core/Removal.cs:8`), mas **nao ha painel** listando arquivos em uso |
| 42 | **Enumeração por SID do usuário** dos pacotes MSIX | `Core/Modules/WindowsApps.cs:43` executa `Get-AppxPackage` seguido de `Select-Object ...` **sem** `-AllUsers` e **sem** `-PackageUserInformation`/SID; `grep "Sid\/SecurityIdentifier\/AllUsers" src/` -> **0 hits** no modulo. A listagem e **so do usuario atual** |

### HIBIT — 11

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 4 | **Desinstalação forçada** ("Remoção Forçada") — reagendada para o próximo logon via `RunOnce` | `grep "RunOnce" src/` -> **so leitura**: `Core/Discovery/StartupEntries.cs:31-34` e `Core/Tools/AutorunManager.cs:84`; `Core/Tools/RegistryCleaner.cs:903` le a chave. **Nenhuma escrita em `RunOnce`**. `Core/DeferredDelete.cs:27` reagenda **arquivo** com `MoveFileEx(DELAY_UNTIL_REBOOT)`, nao a operacao. `Core/Automation/UninstallResume.cs:95`/`:136` grava e le sessao, mas nao agenda no shell |
| 9 | **Ocultar entrada de desinstalação** ("Hide original uninstallation entry") | `grep "HideUninstall\/HideEntry\/HideFromList\/Ocultar" src/` -> **0 hits** em codigo de producao. O unico hit de "Ocultar" e `Ui/UserMessages.cs:9`, que e a preferencia de esconder *mensagens* ao usuario, nao entrada de desinstalacao. `Core/InstalledProgram.cs:33` tem `IsSystemComponent`, que e outra coisa (componente do sistema) e nao esconde a entrada |
| 17 | **Menu de contexto do Monitor de Instalação**: `Msi.Package\shell\HiBitMonitor` e `exefile\shell\HiBitMonitor` | `grep "Msi.Package\/HiBitMonitor" src/` -> **0 hits**. `Core/Topmost/ExplorerContextMenu.cs` registra **apenas** o menu do Tranal (`ShellKeyRelative` unico, `:202-216`); nao ha segunda entrada para o monitor |
| 60 | Painel de informações do sistema no banner (CPU, GPU, RAM disponível, SO, tempo de atividade) | `grep "SystemInfoPanel" src/Tranal.Uninstaller/` -> **2 hits, ambos dentro do proprio modulo novo e de `Core/Modules/ProgramImpactAnalyzer.cs:36`**: **nenhuma referencia em `Ui/`** (`Ui/**` -> 0 hits). O modulo esta **nao versionado** e **nao compila** (ver ressalva de concorrencia): ha a coleta (`:47` SO, `:54` CPU, `:58` RAM, `:60` disco, `:65` GPU, `:69` uptime) mas **nao ha painel no banner** |
| 62 | Botão **Doar** (adicionado na 4.0.10) | `grep "Doar\/Donate\/Doacao" src/` -> **0 hits**. Funcao sem equivalente; irrelevante para paridade funcional |
| 63 | Data de instalação com **tempo relativo** ("Hoje", "Ontem", "1 mês atrás", "2 meses antes", "1 ano atrás") | A coluna **mostra a data absoluta** (`Core/ProgramColumns.cs:240`, `Core/InstalledProgram.cs:25`). `grep "RelativeDate" src/` -> **0 hits em codigo de producao**: o arquivo `Core/Modules/RelativeDate.cs:20` existe **nao versionado** e so e consumido por `src/Tranal.Tests/InsightsAndRelocationTests.cs:30-84`; **nao ha fio na coluna** |
| 80 | **Fixador de Atalhos** (atalhos corrompidos) | `grep "ShortcutFix\/CorruptShortcut\/BrokenShortcut\/RepararAtalho" src/` -> **0 hits**. `Core/Tools/RegistryOptimizer.cs:72` **apaga** atalho obsoleto do Menu Iniciar (`:159` `ObsoleteStartMenu`, `:172` `ObsoleteShortcut`); `LeftoverReason.ObsoleteShortcut` (`Core/UninstallSupport.cs:88`) tambem so **apaga**. **Nao ha verificacao nem reparo do alvo do atalho** |
| 82 | **Localizador de Arquivos e Registros** | `grep "FileFinder\/RegistryFinder\/FindByName" src/` -> **0 hits**. `Core/Tools/EvidenceRemover.cs` **apaga** rastros (`:212` documentos recentes); nao ha busca por nome de arquivo ou de chave |
| 88 | **Afinidade de processador** do programa | `grep "GetProcessAffinityMask\/SetProcessAffinityMask" src/` -> **0 hits**. Nada no codigo le ou escreve a mascara de afinidade |
| 98 | **Verificação de integridade de arquivo** e limpeza pontual de arquivo | `grep "IntegrityCheck\/VerificarIntegridade\/FileHash" src/` -> **0 hits**; o unico SHA-256 do codigo e interno (`Core/Management/SoftwareHealth.cs:431`) e serve ao detector de duplicados, nao a verificacao de integridade. A **limpeza pontual existe** (`Core/Tools/JunkCleaner.cs:91`, `Core/Tools/EvidenceRemover.cs`, `Core/Tools/UnrecoverableDelete.cs:118`), mas a **verificacao de integridade** nao |
| 100 | **Banco local embutido** (`LOCALDB`) | `grep "LOCALDB\/Sqlite\/Microsoft.Data.Sqlite" src/` -> **0 hits**. O estado e JSON: `Core/Settings/ProgramCache.cs:154` (`ProgramCacheStore`), `:188` salva, `:252` carrega, `:306` invalida; `Core/Modules/TraceStore.cs` para o log de instalacao. **Nao ha banco local** |

### IOBIT — 16

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 3 | **Limpeza de navegador / reset de padrões** (página inicial e motor de busca) | O modulo so lista e remove extensoes (`Core/Modules/BrowserExtensions.cs:86` (`List`), `Core/Modules/BrowserExtensions.cs:113` (`Remove`)); a limpeza de rastros (`Core/Tools/TrackCleaner.cs`) nao mexe em pagina inicial nem motor de busca |
| 4 | Remoção de **plugins** do navegador | Nao ha catalogo nem remocao de plugins NPAPI/ActiveX; so extensoes Chromium/Firefox (`Core/Modules/BrowserExtensions.cs`) |
| 5 | **Bloatware Remover**: pré-instalados do Windows, Windows Accessories e Windows Features, com marcação *Permanent* / *Warning* | `iobit.strings.txt:46869` → `Bloatware Remover`; `:34662` → ` Bloatware Remover`; `:40142` → `BloatApp.dll` |
| 6 | **Bundleware**: detectar o que veio junto com o programa e desinstalar em conjunto | Grep "bundle" sem resultado em `src/` |
| 7 | **Stubborn Program Remover** com base de **4.000+ programas** travados | Grep "Stubborn" sem resultado. O Tranal tem desinstalacao forcada por alvo/palavra-chave (`Core/Automation/ForcedRemovalOptions.cs:41/50`) e remocao manual assistida (`Core/Automation/ManualRemoval.cs`) - nao e base de conhecimento |
| 11 | Confirmar/avisar antes de apagar **arquivo pessoal** | **não confirmado no binario** |
| 19 | Log guardado em **SQLite** (`sqlite3.dll`, 37 funções importadas) | O log e JSON (`Core/Tracing/TraceLogV2.cs` `Save`); nao ha SQLite no codigo |
| 23 | Botão **Shred no Explorer** | O menu de contexto do Explorer do Tranal e o de desinstalar (`Core/Topmost/ExplorerContextMenu.cs`); nao ha entrada de destruicao |
| 24 | **Software Relocator**: mover software instalado entre discos | Grep "Relocat" sem resultado. O Tranal varre drives atras de apps nao registrados (`Core/Discovery/DriveScanner.cs`), nao os move |
| 25 | **Bloqueio de popups** de notificação | **não confirmado no binario** |
| 27 | **Software Updater** integrado (atualizar todos) | `Core/Updates/AutoUpdateClient.cs:74` (`CheckAsync(feedUrl)`) e o atualizador **do proprio Tranal**, alimentado por um feed; nao ha atualizador de programas de terceiros |
| 33 | Aviso de **sincronização do Chrome** ao remover extensão | `Core/Modules/BrowserExtensions.cs` (`Remove`) remove direto, sem aviso de sincronizacao |
| 34 | Integração com a **Central de Ações do Windows** (Action Center) | Nao ha integracao; o mais perto e `Core/Management/SoftwareHealth.cs` (`HealthCategory.Notifications`), que e aviso interno |
| 36 | **Voto / avaliação** de programas e plugins pela comunidade | Nao ha coleta de votos; ha avaliacao local por estrelas (`Core/Appearance/ProgramRating.cs`, `Core/ProgramCustomization.cs`) |
| 37 | **Captura de tela** do sistema no fluxo de suporte | `Core/Appearance/SupportRequest.cs:44/91` monta texto e URL com os dados do programa; grep "screenshot"/"captura"/"Image" sem resultado - nao ha captura de tela |
| 38 | **Catálogo embutido de Appx pré-instalados** (a lista do que o Windows traz de fábrica) | Igual a linha 5: nao ha essa lista no codigo |

### SOFTORGANIZER — 18

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 9 | **Removal tools from the developers** (link para o desinstalador oficial) | A coluna mostra o caminho do desinstalador (`ProgramColumns.cs:283`, `UninstallerLocationOf`), nao um link para a ferramenta de remocao do desenvolvedor. Grep sem resultado em `src/`. |
| 11 | Verificação de novas versões com "Update to X" | Nao ha consulta de versao de programas instalados. Grep sem resultado em `src/`: a unica atualizacao do codigo e a do proprio Tranal (`Core/Updates/AutoUpdateClient.cs:56`). |
| 15 | Notificações internas (sino) e dicas | Grep por sino, notificacao interna e dicas: sem resultado em `src/`. A categoria "Notificacoes" do Software Health (`src/Tranal.Uninstaller/Core/Management/SoftwareHealth.cs:289`) trata do estado dos programas, nao de avisos internos do aplicativo. |
| 25 | Aviso "use o Undo Changes Center para cancelar" | Nao ha aviso que remeta o usuario ao centro de reversao. Grep sem resultado em `src/`. |
| 29 | Aviso para melhorar a qualidade do rastreio | Grep por aviso de qualidade de rastreio: sem resultado em `src/`. |
| 40 | Rastreamento de **DLLs em pastas do sistema** | As raizes varridas sao Program Files, Program Files (x86), CommonApplicationData, LocalApplicationData e ApplicationData (`src/Tranal.Uninstaller/Core/Modules/SystemSnapshot.cs:106`); `C:\Windows` nao entra. Grep por rastreamento de DLL em pasta de sistema sem resultado em `src/`. |
| 41 | **Monitor em processo separado** (app de rastreamento) | Nao ha processo separado de rastreamento: a solucao tem apenas `src/Tranal.Uninstaller/Tranal.Uninstaller.csproj`, `src/Tranal.Cli/Tranal.Cli.csproj` e `src/Tranal.Tests/Tranal.Tests.csproj`. |
| 44 | Chave de configuração "Windows context menu commands" | Nao ha chave de configuracao para os comandos do menu de contexto do Explorer: grep sem resultado em `src/`. |
| 52 | Instalação silenciosa de programas | Nao ha instalacao silenciosa: grep por "MsiInstallProduct" e por instalacao silenciosa sem resultado em `src/`. So existem desinstalacao e reparo MSI (`src/Tranal.Uninstaller/Core/Discovery/MsiProductCatalog.cs:242`). |
| 53 | Instalação de novas versões | Nao ha instalacao de versao nova: grep sem resultado em `src/`. |
| 54 | **Centro de dicas "Did You Know"** com dicas contextuais (arrastar para desinstalar, desinstalação externa, atualização, multilíngue) | Grep por centro de dicas: sem resultado em `src/`. |
| 57 | **Carga e descarregamento de chaves** e substituição de chave | Grep por "RegLoadKey", "RegUnLoadKey" e "RegReplaceKey": sem resultado em `src/`. |
| 60 | **Enumeração de impressoras** e fila de impressão (para desinstalar driver de impressora) | Grep por "EnumPrinters" e por fila de impressao: sem resultado em `src/`. |
| 61 | **Descoberta de dispositivos Bluetooth LE** | Grep por "Bluetooth" e por WinRT de dispositivo: sem resultado em `src/`. |
| 63 | Informações de **usuário e estação de rede** | Grep por "NetUserGetInfo" e por "NetWkstaGetInfo": sem resultado em `src/`. |
| 64 | Consulta da **Lixeira** para reportar itens removidos | A lixeira e esvaziada (`SHEmptyRecycleBin`), nao consultada: grep por "SHQueryRecycleBin" sem resultado em `src/`. |
| 76 | **Impersonação de token** e verificação de privilégios | Grep por "ImpersonateLoggedOnUser", "SetThreadToken", "PrivilegeCheck" e "RevertToSelf": sem resultado em `src/`. |
| 77 | **Notificações internas agendadas** (avisos de dica) | Grep por notificacao interna agendada: sem resultado em `src/`. |

### TOTALUNINSTALL — 12

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 61 | Filtros de tipo de arquivo na operação de exclusão (.ico, .dll, .htm/.html, .lnk/.exe) | `grep extension\/Extension\/Wildcard\/wildcard` sobre `src/Tranal.Uninstaller/Core/ExcludeList.cs` -> **0 hits**; a lista exclui por **caminho**, nao por tipo/curinga de arquivo |
| 15 | Pasta de snapshots com limite de tamanho e contagem ("Limits for Snapshots folder") | `grep MaxSnapshots\/SnapshotLimit\/PurgeSnapshot\/MaxSize` sobre `src/Tranal.Uninstaller` -> **0 hits**; a pasta e `SnapshotLibrary.SnapshotDirectory` (`:86`) sem qualquer limite ou purga |
| 18 | **Perfis** separados: Scanning, Analysis, Uninstall, Comparing, Restore/Redo, Cleaner, Auto-run view | `grep Profile\/ScanProfile\/UninstallProfile\/CompareProfile` sobre `src/Tranal.Uninstaller` -> **0 hits** para perfis de operacao; ha apenas `ProgramFilterSet` (`Core/Appearance/ProgramFilters.cs:147`), que e filtro de **lista**, nao perfil de operacao |
| 21 | **Visões de mudança** configuráveis ("Change views") | `grep ChangeView\/ChangeViews` sobre `src/Tranal.Uninstaller` -> **0 hits**; o que existe e modo de exibicao da **lista** (`MainWindow.xaml.cs:701-703` `Details`/`List`/`Icons`), nao visao de mudancas |
| 23 | Opção de **não usar o log de mudanças detectadas** ("Don't use detected changes log") | `grep UseChangeLog\/NoChangeLog\/ChangeLog.*Option\/DisableTrace` sobre `src/` -> **0 hits** |
| 45 | Classificação por extensão de arquivo ("File extensions with version information") | `grep FileVersion\/FileDescription\/ProductVersion` sobre `src/Tranal.Uninstaller/Core/UninstallSupport.cs` -> **0 hits**; a versao de arquivo so e lida para processos (`ProcessInspector.cs:153`) e autorun (`Core/Tools/AutorunManager.cs:995`), **nao** na classificacao de sobras |
| 50 | **Agente autônomo** de baixo consumo para notificar instalações em andamento | `grep AutoStart\/Unfinished\/Resume` sobre `src/Tranal.Uninstaller` -> **0 hits** para agente/daemon; `AutoStart` so aparece no gerenciador de inicializacao de **terceiros** (`Core/Topmost/TopmostTargetResolver.cs:289` `HasAutoStart`, `Ui/Tools/TopmostOverlayWindow.xaml.cs:264` `StopAutoStartButton_Click`) e `Resume` so em `Core/Automation/UninstallResume.cs:80` (retomada de desinstalacao); **nenhum agente autonomo** |
| 72 | **Visor hexadecimal** e abertura de arquivo em editor hex/texto | `grep HexView\/HexEditor\/MD5\/Md5\/Hexadecimal` sobre `src/Tranal.Uninstaller` -> **0 hits** |
| 73 | **Soma MD5 dos arquivos novos** criados pela instalação | `grep MD5\/Md5` sobre `src/` -> **0 hits**; `Core/Tracing/SnapshotComparer.cs:331` `Mudou` compara **tamanho e data**, nao hash |
| 83 | **Dependências entre pacotes MSI** ("Depends on packages") | `grep Dependency\/DependsOn\/DependencyCheck` sobre `src/Tranal.Uninstaller/Core/Discovery/MsiProductCatalog.cs` -> **0 hits**; o catalogo le o produto, mas nao resolve dependencia entre pacotes |
| 84 | **Verificação de DCOM** do produto | `grep DCOM` sobre `src/` -> **0 hits**; o que existe e o inverso - desregistrar COM do programa desinstalado (`Core/Automation/ComUnregister.cs:43` `Unregister`, `:118` `RegisteredDlls`), nao verificar DCOM do proprio produto |
| 88 | **Retomar o monitoramento inacabado** no arranque do Windows | `grep Unfinished` -> **0 hits**; `AutoStart` so existe para **terceiros** (`Core/Topmost/TopmostTargetResolver.cs:289`) e `Resume` so em `Core/Automation/UninstallResume.cs:80` (desinstalacao, nao monitoramento); `InstallMonitor.Start` (`Core/Modules/InstallMonitor.cs:103`) e sessao em memoria, morre com o processo |

### UNINSTALLTOOL — 24

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 6 | Atualizações de software como **sub-itens em árvore** | Nao ha arvore de atualizacoes de programas no Tranal. A unica verificacao de atualizacao do codigo e a do proprio aplicativo (`src/Tranal.Uninstaller/Core/Updates/AutoUpdateClient.cs:56`). |
| 15 | Lote com **teto de itens** e aviso explícito | Grep por "BatchLimit"/"MaxItems"/limite de lote: sem resultado em `src/`. A fila de desinstalacao (`Core/Automation/UninstallQueue.cs:66`) aceita qualquer quantidade. |
| 31 | Preferência: sempre rodar / nunca rodar / perguntar | As preferencias do aplicativo estao em `src/Tranal.Uninstaller/Core/Settings/AppSettings.cs:14-140` e nenhuma delas trata o assistente. Grep por "wizard" em `Core/Settings/` sem resultado. |
| 32 | O Wizard **sempre roda** para programas rastreados | O assistente so e aberto por acao do usuario (`src/Tranal.Uninstaller/MainWindow.xaml.cs:1669` e `MainWindow.xaml.cs:1694`); nao ha execucao automatica para programas rastreados. |
| 39 | **Anexar** dados rastreados a um item existente | Nao ha acao de anexar um log a um item da lista. O que existe e a fusao de logs do mesmo programa (`src/Tranal.Uninstaller/Core/Tracing/TraceLogV2.cs:116`, `Merge`), que e outro caso. |
| 40 | Detecção de **múltiplas entradas** de instalação | Grep por entradas multiplas do mesmo programa: sem resultado em `src/`. O contador de referencias compartilhadas (`Core/Discovery/SharedReferenceCounter.cs:52`) trata de pasta compartilhada entre programas, nao de entrada duplicada. |
| 44 | Menu de contexto do Explorer: **"Install and Trace"** para `.exe`/`.msi` | O verb registrado e so de desinstalacao (`--uninstall-target`). Nao ha verb de "instalar e rastrear": grep por "InstallAndTrace"/"Install and Trace" sem resultado em `src/`. |
| 46 | Extensão de shell **empacotada em MSIX** para o Windows 11 | Nao ha empacotamento MSIX da extensao de shell. O instalador (`installer/tranal.iss:118`) cria apenas grupo e area de trabalho. |
| 47 | Serviço de rastreamento **versionado**, com prompt de atualização | Nao ha servico de rastreamento: o rastreio roda no processo do aplicativo (`src/Tranal.Uninstaller/Core/Tracing/WriteWatcher.cs:35`). Nao ha prompt de atualizacao de versao do servico. |
| 55 | Itens de inicialização de **aplicativos UWP / Store** | Grep por "StartupTask" e por leitura de item de inicializacao de Store: sem resultado em `src/`. `Core/Modules/WindowsApps.cs:39` le MSIX instalado, nao item de inicializacao. |
| 57 | Tarefas criadas pela Microsoft **ocultadas** por segurança | `List` traz todas as tarefas agendadas; nao ha filtro nem ocultacao das tarefas da Microsoft. |
| 69 | **Ícone do Uninstall Tool no Painel de Controle** | O instalador cria apenas o grupo e o atalho na area de trabalho; nao ha entrada no Painel de Controle. |
| 70 | **Popup de aplicações recém-instaladas** | Grep por popup de aplicacoes recentes: sem resultado em `src/`. |
| 71 | **Realce dos programas recém-instalados** | Mesma medicao da linha 8: o realce existente e por filtro e cor (`MainWindow.xaml.cs:204`), nao por data de instalacao. |
| 72 | **Programas recentes na barra secundária** | Grep por programas recentes na barra: sem resultado. `AppSettings.RecentPrograms` (`src/Tranal.Uninstaller/Core/Settings/AppSettings.cs:95`) e a lista do menu de acesso rapido, nao a barra secundaria. |
| 73 | **Exibir atualizações de software** na lista | Nao ha coluna nem item de atualizacao de programa na lista: a unica atualizacao do codigo e a do proprio Tranal (`Core/Updates/AutoUpdateClient.cs:56`). |
| 76 | **Minimizar na bandeja durante a instalação** | Grep por "NotifyIcon" e por bandeja do sistema: sem resultado em `src/`. |
| 77 | **Exibir o ícone na bandeja do sistema** | Grep por "NotifyIcon" e por bandeja do sistema: sem resultado em `src/`. |
| 78 | **Iniciar automaticamente minimizado na bandeja** | Grep por bandeja do sistema e por inicio automatico minimizado: sem resultado em `src/`. |
| 79 | **Navegar até a pasta do programa com o Total Commander** | Grep por "Total Commander"/"TotalCommander": sem resultado em `src/`. |
| 82 | **Lista compacta de aplicativos** | Grep por lista compacta e por "SmallIcons": sem resultado em `src/`. A lista usa sempre o mesmo conjunto de colunas. |
| 85 | **Painéis ancoráveis** ("push pin") com pré-visualização | Grep por painel ancoravel e por pre-visualizacao: sem resultado em `src/`. |
| 86 | **Execução de processo 32 bits** (`CreateX86Process`) | Grep por "CreateX86Process": sem resultado em `src/`. O Tranal so le o `WOW6432Node` do registro (`src/Tranal.Uninstaller/Core/Modules/SystemSnapshot.cs:529`). |
| 92 | **Validação de cadeia de certificados** embarcada (CRYPTO/SSL) | O que existe e a validacao da assinatura do desinstalador (`WinVerifyTrust` com `X509Chain`), que e outro objeto. Grep por "CertVerifyCertificateChainPolicy" e por "CertGetCertificateChain": sem resultado em `src/`. |

### UNINSTALR — 18

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 10 | **Exclusão** de arquivos/pastas/unidades, configurável | `Uninstaller.options-ignore-paths`, `Uninstaller.options-ignore-paths-tip`, `-ignore-paths-format` (`strings-uninstalr-own.txt:9767-9771`) |
| 14 | **16 tipos de app** com detecção dedicada: normal, MS Store, portable, sobras, Chocolatey, Ninite, PortableApps.com, Steam, EA App, Epic, Riot, GOG Galaxy, Wargaming.net, Battle.net, itch.io, Big Fish | Sem lastro no codigo |
| 16 | **País de origem** do app, com bandeira | Grep "country"/"Country" sem resultado em `src/` |
| 21 | **Reiniciar automaticamente** para finalizar (avisa que pode reiniciar 1 ou 2 vezes) | `Core/Automation/ShutdownGuard.cs:18` faz o **contrario** (impede desligamento durante a tarefa); nao ha reinicio automatico |
| 23 | **Gerar `.bat`** para desinstalação em lote + `PreStartupDelete.bat` para o próximo arranque | `Core/Reporting/BatchScriptWriter.cs:35` gera script de **desinstalacao em lote** sob demanda; nao ha script de limpeza agendado para o proximo arranque |
| 26 | **Deep Analysis** sob demanda, com aviso de que pode levar minutos | Nao ha "deep analysis". O que existe e outro: revisao de sobras do instalador (`Core/Automation/AdvancedUninstallReview.cs:56`) e modo forcado (`Core/Automation/ForcedRemovalOptions.cs`) |
| 27 | Tooltip por app com Publisher, Tipo, Versão, Data, **País**, "Automatically starts with Windows", pasta, chave de registro, uninstall key, comando completo, executável principal, atalho principal | `Uninstaller.options-show-more-info`, `-show-apptype`, `-show-apppaths`, `-show-scanpath` (`strings-uninstalr-own.txt:9813-9819`), `GLOB_DetectDataFromManifest1/2`, `GLOB_DetectPubFromFile` (`:3900-3902`) |
| 30 | Opções de **acessibilidade** (leitor de tela, moldura padrão, botões padrão) | Nao ha essas opcoes. WPF ja entrega acessibilidade de sistema; nao ha registro de decisao sobre isso nos docs de decisao |
| 35 | **Correspondência difusa (fuzzy)** de elementos e de termos de busca | Grep "Fuzzy"/"Levensh" sem resultado. O filtro e regex exato + termos de exclusao (`Core/Appearance/ProgramFilters.cs:215/228`) |
| 36 | **Dados do publicador extraídos do manifesto do executável** | Grep "manifest"/"Manifest" sem resultado em `src/`. A editora vem do registro (`Core/ProgramColumns.cs` `Company`) e dos pacotes (`Core/Modules/WindowsApps.cs`) |
| 37 | **Seleção em massa por país** (selecionar/desmarcar por país) | Depende da linha 6: nao ha pais de origem no codigo |
| 40 | **Detecção de Epic Games** e **Big Fish** como plataforma | "Epic Games" so aparece como nome de pasta conhecido em `Core/Discovery/FolderConfidenceEvaluator.cs` - isso e heuristica de confianca, nao deteccao de plataforma |
| 41 | **Detecção de NuGet** como plataforma | Sem lastro no codigo |
| 42 | **PortableApps.com** como plataforma detectada | Sem lastro no codigo |
| 43 | **Atalhos** como fonte de descoberta de apps | `Core/ProgramScanner.cs` varre so chave de desinstalacao, catalogo MSI e Store (`Core/ProgramColumns.cs` `ProgramSource`). Atalhos so entram como sobra (`Core/UninstallSupport.cs`) |
| 47 | **Opções "speedhacks"** (desativar verificações lentas) | Sem lastro no codigo (nao ha flag equivalente a "desligar verificacoes lentas") |
| 49 | **Fechar o próprio aplicativo** ao terminar | Sem opcao de auto-fechar no codigo |
| 51 | **Modo de exibição do sistema** (system/always/never) | Nao ha preferencia nesses 3 estados. O mais perto e incluir/excluir componentes do sistema (`Core/ProgramScanner.cs`, `IncludeSystemComponents`) |

### WISE — 14

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 9 | Aviso para não usar a Forçada antes da Segura | A tela de desinstalacao forcada (`src/Tranal.Uninstaller/Ui/ForcedUninstallWindow.xaml.cs:69`) vai direto para a varredura; nao ha aviso que oriente a ordem segura, forcada. |
| 13 | Aviso quando o navegador está em execução | Grep por deteccao de navegador em execucao: sem resultado em `src/`. |
| 14 | Recusa de desinstalar a si mesmo | Nao ha guarda que recuse desinstalar o proprio Tranal: grep por auto-protecao de remocao sem resultado em `src/`. |
| 16 | Aviso de desinstalação anterior em andamento | Nao ha deteccao de desinstalacao em andamento: grep por operacao em curso sem resultado em `src/`. |
| 29 | **Botão "Recuperar" no histórico** | Nao ha tela de historico nem botao "Recuperar": `UninstallHistory.Read` so e chamado pelos testes (`src/Tranal.Tests/RecoveryTests.cs:264`). |
| 39 | "Stop" para interromper a varredura | Nao ha cancelamento da varredura: grep por "CancellationToken" e por "Cancel" em `src/Tranal.Uninstaller/Core/Management/SoftwareHealth.cs` sem resultado. |
| 45 | Diálogo de **notificações** com remetente e tipo | Nao ha tela de notificacoes com remetente e tipo. A categoria "Notifications" do Software Health (`SoftwareHealth.cs:289`) trata do estado do programa (sem comando de desinstalacao, pasta compartilhada, servico, tarefa), nao de notificacoes do sistema. |
| 46 | Registro de "última análise" | Nao ha registro da ultima analise: grep por "LastScan" e por "LastAnalysis" sem resultado em `src/Tranal.Uninstaller/`. O unico "LastRun" e o do backup diario de registro (`src/Tranal.Uninstaller/Core/Recovery/DailyRegistryBackup.cs:59`). |
| 49 | Leitura do arquivo de preferências do navegador | O Tranal le o `manifest.json` das extensoes, dentro da pasta do perfil (`BrowserExtensions.cs:50`); nao le o arquivo `Preferences` do perfil. Grep por "Preferences" sem resultado em `src/Tranal.Uninstaller/Core/Modules/BrowserExtensions.cs`. |
| 50 | Bloqueio de notificações pop-up (regra `PopNotify`) | Nao ha o tipo de regra "PopNotify": grep por "PopNotify" sem resultado em `src/`. Os tipos de regra existentes sao as categorias Include, Exclude, Protect e Cleanup (`src/Tranal.Uninstaller/Core/Rules/RemovalRule.cs:15`). |
| 55 | Tabela de caminhos de registro por app, com marcação `(Reg)`: **48 entradas, 14 com `(Reg)`** | Nao ha tabela de chaves de registro por aplicativo: grep por tabela de caminhos por app sem resultado em `src/`. |
| 60 | Datas **relativas** no histórico | Nao ha tela de historico nem formatacao de data relativa: o historico guarda o carimbo de tempo bruto (`src/Tranal.Uninstaller/Core/UninstallHistory.cs:98`). |
| 82 | Acesso ao **gerenciador de credenciais** do Windows | Grep por "CredRead" e por "CredEnumerate": sem resultado em `src/`. |
| 83 | Detecção de **geolocalização** | Grep por "Geolocation" e por WinRT de geolocalizacao: sem resultado em `src/`. |
