# Prova de cobertura por concorrente

Gerado por `tools\auditoria\provar-paridade.ps1`. Cada linha diz o que o
concorrente tem e o que o Tranal tem, **conferido no codigo**.

Estado: `OK` o Tranal tem · `PARCIAL` tem parte · `AUSENTE` falta ·
`FORA` limite declarado · `NAO CONFIRMADO` o dump do concorrente nao permite confirmar.

A **cobertura** conta `OK` e `FORA` (o que o Tranal tem, ou o que foi
declarado fora por decisao). `PARCIAL` aparece na coluna propria: a
funcionalidade existe, mas nao inteira — por isso ela NAO entra na
cobertura, e por isso o total medido e sempre maior que a soma das
colunas.

| Concorrente | Funcionalidades medidas | OK | Parcial | Ausente | Fora | Nao confirmado | Cobertura |
|---|---:|---:|---:|---:|---:|---:|---:|
| ASHAMPOO | 76 | 25 | 24 | 25 | 2 | 0 | 35.5% |
| BCU | 97 | 47 | 24 | 26 | 0 | 0 | 48.5% |
| GEEK | 44 | 28 | 3 | 13 | 0 | 0 | 63.6% |
| HIBIT | 66 | 23 | 15 | 24 | 4 | 0 | 40.9% |
| IOBIT | 40 | 12 | 7 | 17 | 4 | 0 | 40% |
| SOFTORGANIZER | 77 | 32 | 19 | 26 | 0 | 0 | 41.6% |
| TOTALUNINSTALL | 94 | 45 | 27 | 22 | 0 | 0 | 47.9% |
| UNINSTALLTOOL | 94 | 33 | 29 | 32 | 0 | 0 | 35.1% |
| UNINSTALR | 51 | 21 | 7 | 20 | 3 | 0 | 47.1% |
| WISE | 86 | 28 | 32 | 26 | 0 | 0 | 32.6% |
| REVO PRO 5.5.4 | 70 | 68 | 0 | 0 | 2 | 0 | 100% |
| **TOTAL** | **795** | **362** | **187** | **231** | **15** | **0** | **47.4%** |

Apuracoes linha a linha aplicadas: 173.

## O que ainda falta (estado AUSENTE)

###  — 25

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 62 | Comparação de tamanho declarado vs. real do programa, sincronizável com o Windows | Existe estimativa e medicao isoladas; **nao ha** tela de comparacao nem sincronizacao |
| 63 | Categoria automática expandida + reset para "Unsorted" | Agrupamento por coluna existe; **nao ha** categoria "Unsorted" nem reset - `grep Unsorted |
| 64 | País do editor com bandeira e nome | `grep Country |
| 66 | Desinstalar o próprio produto pelo Windows "Apps & Features" | nao confirmado no binario — doc.: `manual-pro27.txt:513-521` |
| 12 | Importar logs do UnInstaller 4 | nao confirmado no binario — doc.: `manual-free.txt:177-179` |
| 15 | Análise forense em lote (processar todas as aplicações compatíveis de uma vez) | `grep Analyze` mostra so a assinatura por `InstalledProgram`; `SoftwareHealth.Analyze` (`:119`) varre todos, mas e health, nao forense |
| 61 | Instal guard: definir como o programa identifica setups e quais propriedades de arquivo usar | `grep InstallerGuard |
| 36 | Wizard de snapshots por caso de uso, com nomes automáticos | nao confirmado no binario — doc.: `manual-pro27.txt:287-288` |
| 37 | Snapshot: seleção de drives incluídos e pastas excluídas | nao confirmado no binario — doc.: `manual-pro27.txt:492-493` |
| 41 | Drive Cleaner (arquivos desnecessários, com seleção por categoria e relatório) | A varredura do lixo nao tem filtro de tamanho |
| 42 | Internet Cleaner (cookies, histórico, exceções e "cookies seguros" / whitelist) | Nao ha whitelist de cookies; os 6 grupos sao fixos |
| 44 | File Associator (tipos de arquivo sem programa associado, editar/undo) | `grep FileAssoc |
| 50 | **Unlocker**: desbloquear arquivos em uso, reiniciar o Explorer, desregistrar DLL, encerrar processo | Encerrar processo e adiar remocao para o reboot existem; **reinicio do Explorer e `DllUnregister` nao**: `grep regsvr32 |
| 51 | **Crash Analyzer**: relatórios de falha do Windows Event Log, agrupados por programa, com export e busca online | O Tranal **limpa** Event Logs (6 grupos em `PrivacyTraceCleaner.cs:12-31`); nao agrupa crash por programa |
| 53 | **Software Updater**: atualizar programas instalados, em lote, com lista negra de atualizações | O feed do Tranal atualiza **so o proprio Tranal**; nao ha atualizador de terceiros |
| 54 | **Hibernation**: colocar um programa para dormir (desabilita autostart e serviços) e acordá-lo depois | `grep Hibernat |
| 55 | **Impact**: programas com maior impacto na performance (CPU e memória) | So ha ordenacao e mapa de **tamanho**; nenhuma medicao de CPU/memoria por programa |
| 56 | **Move**: mover um programa para outro local, recriando o atalho no lugar original | `grep Relocat |
| 57 | **Revert relocation**: devolver o programa movido à posição original | `grep Relocat |
| 65 | Instalação via "Ashampoo App" (`C:\Program Files\Ashampoo\Ashampoo App`, produtos em `products\`) | nao confirmado no binario — doc.: `manual-troubleshooting.txt:13-15` |
| 71 | Enumeração de dispositivos/drivers no instalador (SetupDi) | `ashampoo-instalador.inventario.md:409-410` → `**SetupDi** - Enumera dispositivos/Drivers` |
| 73 | Instalador protegido por senha (criptografia ChaCha20 do setup) | `ashampoo-instalador.inventario.md:9` → classe RTTI `TChaCha20Context`; `ashampoo-instalador.strings.txt` → `TSetupEncryptionKey`, `The password you specified is not correct. Please try again.` |
| 74 | Fechamento automático de aplicações que travam a instalação (e reinício delas) | `ashampoo-instalador.inventario.md` §"Rotulos de interface relevantes" (`Instructs Setup to attempt closing applications using files that need to be updated, or prevents it from doing so.`, `Instructs Setup to force close when closing applications, or prevents it from doing so.`, `Instructs Setup to attempt restarting applications, or prevents it from doing so.`) |
| 75 | Manifesto que redireciona DLLs de sistema (`comctl32`, `mpr`, `netapi32`, `netutils`, `textshaping`, `version`) | `ashampoo-instalador.strings.txt:66-71` → `file name="comctl32.dll" loadFrom="%SystemRoot%\system32\" /` e equivalentes |
| 76 | Compilador Delphi (Embarcadero) do instalador e detecção de sistema | `ashampoo-instalador.inventario.md` → `Embarcadero Delphi for Win32 compiler version 36.0 (29.0.55362.2017)`; `ashampoo-instalador.identificadores.txt:22` → `Winapi.Windows.PkgHelper` |

###  — 26

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

###  — 13

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 2 | Total agregado no rodapé ("%d programs of %s size in total") | O rodape mostra a **contagem** (`MainWindow.xaml.cs`), nao o tamanho total. O total so aparece no resumo do treemap (`Ui/Appearance/AppearanceView.xaml.cs`) |
| 5 | Aviso "No matching items, press ESC to reset" | Nao ha estado/aviso de lista vazia por filtro (grep "NoResults"/"Sem resultados" sem resultado). O filtro tem validacao de regex (`Core/Appearance/ProgramFilters.cs`) e exclusoes (`ProgramFilters.AddExclusion`), mas nenhum aviso de "nada corresponde" |
| 10 | **Remove Entry** + **Batch Remove Entries** | O editor de registro e unitario: `Core/RegistryEditing/RegistryEditor.cs:125/174`. Nao ha acao em lote so de chaves |
| 14 | Diálogo de falha de desinstalação com opção de **limpar a entrada do registro e os arquivos** | Ha o relatorio de remocao com codigo de erro (`Core/RemovalLog.cs`, `Core/Removal.cs`) e a desinstalacao forcada como caminho separado (`Ui/ForcedUninstallWindow.xaml.cs`), mas nao o dialogo "a desinstalacao falhou, quer limpar a entrada e os arquivos?" |
| 15 | "Browse..." dentro das sobras encontradas | A lista de sobras e `LeftoverItem` (`Core/UninstallSupport.cs`) em lista simples, sem acao de navegar para o item |
| 16 | Aba de **Microsoft Store Apps** (`Ctrl+Tab`) | Grep "ms-windows-store" sem resultado. Ha o modulo de apps Store (`Core/Modules/WindowsApps.cs`) para listar/remover, mas nao o comando "abrir na Store" |
| 22 | **Browse Location…** e **Browse Source…** | A **origem** do item existe (`Core/ProgramColumns.cs:112` (`ProgramSource`) e `Core/ProgramColumns.cs:304` (`SourceOf`), mas nao ha localizacao do instalador-fonte nem acao de browse |
| 29 | **Desregistro de COM** (`regsvr32 /u`) antes de remover | Nenhum uso de `regsvr32` no codigo (grep sem resultado em `src/`) |
| 33 | **Correspondência aproximada (Levenshtein)** de caminho de instalação | Grep "Levensh" sem resultado. O que existe: variantes por palavra inteira (`Core/UninstallSupport.cs:552` (`NameVariants`), `Core/UninstallSupport.cs:292` (`ContainsWord`) e `LeftoverReason.SimilarNameMatch` |
| 34 | **Varredura de atalhos do Menu Iniciar e da Área de Trabalho** para achar ícone e localização | O Menu Iniciar so aparece como entrada obsoleta (`Core/Tools/RegistryOptimizer.cs`, `ScanStartMenu`) e atalho so como sobra (`Core/UninstallSupport.cs`). Nao ha varredura de atalhos para descobrir icone/local do programa |
| 35 | **Limpeza do cache MRU do Windows** | Grep "MrtCache" sem resultado em `src/`. O MRU existente e o de Office (`Core/Tools/TrackCleaner.cs:55-58`), nao essa chave |
| 39 | **Painel "Active Files"** de arquivos em uso | Nao ha painel dedicado. A **deteccao** existe (o item em uso vira "adiado para o proximo reinicio": `Core/Removal.cs`, `Core/RemovalLog.cs`), mas nao a tela listando arquivos em uso |
| 42 | **Enumeração por SID do usuário** dos pacotes MSIX | `Core/Modules/WindowsApps.cs` usa `Get-AppxPackage` (usuario atual), sem `-AllUsers` e sem SID |

###  — 24

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 4 | **Desinstalação forçada** ("Remoção Forçada") — reagendada para o próximo logon via `RunOnce` | `Core/DeferredDelete.cs` reagenda **arquivo** com `MoveFileEx(DELAY_UNTIL_REBOOT)`, nao a operacao; `Core/Automation/UninstallSession.cs:130` grava sessao, mas `UninstallSession.Pending` so e consumido em `Tranal.Tests`; nao ha escrita em `RunOnce` (grep so acha leitura em `Core/Discovery/StartupEntries.cs` e `Core/Tools/AutorunManager.cs`) |
| 8 | **Relatórios HTML por ferramenta**: `OpReport`, `ScanLog`, `ProgramCompReport`, `WindowsStoreReport`, `ServicesReport`, `DriversReport`, `StartupReport`, `SchedulerReport`, `ContextMenuReport`, `UninstallerReport` | Exporta HTML da **lista** (`Core/Exporters.cs:63`) e do **log de instalacao** (`Core/Reporting/TraceLogExporter.cs`); `Core/Reporting/ReportFormats.cs` trata lista/registro. Nao ha relatorio dedicado por ferramenta |
| 9 | **Ocultar entrada de desinstalação** ("Hide original uninstallation entry") | **não confirmado no binario** |
| 17 | **Menu de contexto do Monitor de Instalação**: `Msi.Package\shell\HiBitMonitor` e `exefile\shell\HiBitMonitor` | `Core/Topmost/ExplorerContextMenu.cs` registra apenas o menu de desinstalacao do Tranal; nao ha entrada do monitor |
| 18 | **Gerenciador do Menu de Contexto** — lista, cria e remove menus do shell, com "Restaurar" e "Excluir tudo" | `Core/Management/ContextMenuManager.cs:70/137/148` faz List/ListFor/Remove de handlers; `Core/Topmost/ExplorerContextMenu.cs:189/156` instala/remove o menu **do proprio Tranal**. Nao ha criacao de menu arbitrario, nem "Restaurar"/"Excluir tudo" |
| 21 | Desinstalar **atualizações do Windows**: `cmd.exe /c wusa.exe /uninstall /` + `KBID=` | `Core/Discovery/WindowsFeatures.cs:350` (`ListInstalled`) e (`ListAvailable`) so catalogam; nao existe comando de remocao de KB |
| 60 | Painel de informações do sistema no banner (CPU, GPU, RAM disponível, SO, tempo de atividade) | `hibit.inventario.md:13431` -> (sem token equivalente — ausência **não** prova) |
| 62 | Botão **Doar** (adicionado na 4.0.10) | **não confirmado no binario** |
| 63 | Data de instalação com **tempo relativo** ("Hoje", "Ontem", "1 mês atrás", "2 meses antes", "1 ano atrás") | **não confirmado no binario** (captura de tela, não string do binário) |
| 64 | Coluna **Tipo** com `32bit` / `64bit` por programa | **não confirmado no binario** |
| 65 | Contadores na barra de status: total de programas **e tamanho total ocupado** ("Total: 79 Unidade", "Tamanho: 610 GB") | A barra de status existe (`MainWindow.xaml:409-411`) mas mostra **contagem**, nao tamanho: `MainWindow.xaml.cs` |
| 68 | Gerenciador de **Serviços** | `Core/Management/ServiceManager.cs:113/148/199/209/217/242` - List/ListFor/Disable/Enable/Stop/Delete. Nenhum `Create` |
| 69 | Gerenciador de **Processos** (painel) | `Core/ProcessTerminator.cs:61/107/156` encerra processos e `Ui/Tools/TopmostOverlayWindow.xaml.cs` tem "Kill process"; nao existe listagem/painel de processos do sistema |
| 72 | Gerenciador de **Componentes de Programas** (DLLs compartilhadas) | `Core/ProgramScanner.cs:64` so le chave de desinstalacao, catalogo MSI e Store (`Core/ProgramColumns.cs`); nao ha enumerador de DLL compartilhada entre programas |
| 73 | Gerenciador de **Extensões de Navegador** | `Core/Modules/BrowserExtensions.cs:76-80` cobre apenas chrome, edge, opera (+ firefox em ); nao ha Brave, Vivaldi, Yandex |
| 77 | **Limpador de Registro** (15+ categorias: ActiveX, FileTypes, SharedDLLs, IFEO, FirewallRules, MUICache) | `Core/Tools/RegistryOptimizer.cs` trata so entradas obsoletas do Menu Iniciar; `Core/Tools/PrivacyTraceCleaner.cs` tem 6 grupos de rastros; nao ha MUICache no codigo (grep "MuiCache" sem resultado) nem ActiveX/FileTypes/SharedDLLs/IFEO |
| 80 | **Fixador de Atalhos** (atalhos corrompidos) | `LeftoverReason.ObsoleteShortcut` (`Core/UninstallSupport.cs`) so **apaga** atalho obsoleto; nao ha verificacao nem reparo do alvo do atalho |
| 82 | **Localizador de Arquivos e Registros** | `Core/Tools/EvidenceRemover.cs` so apaga rastros; nao ha busca por nome de arquivo ou de chave |
| 87 | **Propriedades de processo** (painel dedicado) | Nao existe nem listagem de processos do sistema (ver linha 69) |
| 88 | **Afinidade de processador** do programa | Nenhum uso de `GetProcessAffinityMask`/`SetProcessAffinityMask` no codigo |
| 92 | **Criar serviço** do Windows | `Core/Management/ServiceManager.cs` nao tem `Create` (so List/Disable/Enable/Stop/Delete) |
| 93 | **Criar tarefa agendada** | `Core/Management/ScheduledTaskManager.cs` nao tem `Create` (so List/Disable/Enable/Delete) |
| 98 | **Verificação de integridade de arquivo** e limpeza pontual de arquivo | Nao ha verificacao de integridade por hash. A limpeza pontual existe (`Core/Tools/JunkCleaner.cs`, `Core/Tools/EvidenceRemover.cs`, `Core/Tools/UnrecoverableDelete.cs`) |
| 100 | **Banco local embutido** (`LOCALDB`) | O estado e JSON: `Core/Settings/ProgramCache.cs` (`ProgramCacheStore`), `Core/Modules/TraceStore.cs`, manifesto em `Core/BackupManager.cs`. Nao ha banco local |

###  — 17

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
| 29 | **Desinstalar Windows Updates** | `Core/Discovery/WindowsFeatures.cs:350/357` so catalogam; nao ha remocao de KB |
| 33 | Aviso de **sincronização do Chrome** ao remover extensão | `Core/Modules/BrowserExtensions.cs` (`Remove`) remove direto, sem aviso de sincronizacao |
| 34 | Integração com a **Central de Ações do Windows** (Action Center) | Nao ha integracao; o mais perto e `Core/Management/SoftwareHealth.cs` (`HealthCategory.Notifications`), que e aviso interno |
| 36 | **Voto / avaliação** de programas e plugins pela comunidade | Nao ha coleta de votos; ha avaliacao local por estrelas (`Core/Appearance/ProgramRating.cs`, `Core/ProgramCustomization.cs`) |
| 37 | **Captura de tela** do sistema no fluxo de suporte | `Core/Appearance/SupportRequest.cs:44/91` monta texto e URL com os dados do programa; grep "screenshot"/"captura"/"Image" sem resultado - nao ha captura de tela |
| 38 | **Catálogo embutido de Appx pré-instalados** (a lista do que o Windows traz de fábrica) | Igual a linha 5: nao ha essa lista no codigo |

###  — 26

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 9 | **Removal tools from the developers** (link para o desinstalador oficial) | `recursos.txt:107` → `TAPPLICATIONSREMOVALTOOLSFORM/0` |
| 10 | Filtro **"Large Applications"** | nao confirmado no binario |
| 15 | Notificações internas (sino) e dicas | `recursos.txt:127` → `TNOTIFICATIONSDATAMODULE/0` |
| 22 | **Undo Changes Center** | `recursos.txt:134` → `TUNDOINGCHANGESCENTERFORM/0` |
| 23 | Reverter uninstallação cronologicamente, renomear ou restaurar | `recursos.txt:134` → `TUNDOINGCHANGESCENTERFORM/0` |
| 24 | Descarte de mudanças antigas do histórico | nao confirmado no binario |
| 25 | Aviso "use o Undo Changes Center para cancelar" | nao confirmado no binario |
| 29 | Aviso para melhorar a qualidade do rastreio | nao confirmado no binario |
| 42 | Rastreamento de **aplicativos adicionais** instalados junto | nao confirmado no binario |
| 46 | Detecção de **desinstalação externa** (app removido por fora) | `recursos.txt:102` → `TAPPLICATIONSEXTERNALDELETEMODULE/0` |
| 50 | "Rarely used apps" | nao confirmado no binario |
| 52 | Instalação silenciosa de programas | nao confirmado no binario |
| 54 | **Centro de dicas "Did You Know"** com dicas contextuais (arrastar para desinstalar, desinstalação externa, atualização, multilíngue) | `recursos.txt:112` → `TDIDYOUKNOWCENTERFORM/0`; `:101` → `TAPPLICATIONSDIDYOUKNOWTIPSDATA/0` |
| 57 | **Carga e descarregamento de chaves** e substituição de chave | `imports.txt:8` → `ADVAPI32.dll / RegLoadKeyW`, `RegUnLoadKeyW`, `RegReplaceKeyW` |
| 59 | **Verificação de assinatura digital** de executáveis | `imports.txt:56` → `CRYPT32.dll / CryptQueryObject`, `CryptMsgGetParam` |
| 60 | **Enumeração de impressoras** e fila de impressão (para desinstalar driver de impressora) | `imports.txt:18` → `WINSPOOL.DRV / EnumPrintersW`, `GetDefaultPrinterW`, `OpenPrinterW` |
| 61 | **Descoberta de dispositivos Bluetooth LE** | `identificadores.txt:58` → `Windows.Devices.Bluetooth`; `strings.txt:15394` → `TWinRTBluetoothLEAdapter` |
| 63 | Informações de **usuário e estação de rede** | `imports.txt:12` → `NETAPI32.dll / NetUserGetInfo`, `NetWkstaGetInfo` |
| 64 | Consulta da **Lixeira** para reportar itens removidos | `imports.txt:30` → `SHELL32.dll / SHQueryRecycleBinW` |
| 67 | **Tarefas de segundo plano** executadas pelo app | `recursos.txt:110` → `TBACKGROUNDTASKSMODULE/0` |
| 68 | **Itens compartilhados e dependentes** entre programas | `recursos.txt:130` → `TSHAREDANDDEPENDENTDATAMODULE/0` |
| 69 | **Startup gear** (gerenciador de inicialização próprio) | `recursos.txt:132` → `TSTARTUPGEARDATAMODULE/0` |
| 70 | Formulário de **notações do log** de rastreamento | `recursos.txt:105` → `TAPPLICATIONSLOGNOTATIONSFORM/0` |
| 71 | Formulário do **resultado da reversão do log** | `recursos.txt:108` → `TAPPLICATIONSREVERTLOGRESULTFORM/0` |
| 74 | Telas de **licenciamento** (chave, assinatura, expiração da assinatura) | `recursos.txt:118` → `TLICENSINGKEYENTERINGFORM/0`; `:121` → `TLICENSINGSUBSCRIPTIONEXPIREDFORM/0` |
| 77 | **Notificações internas agendadas** (avisos de dica) | `recursos.txt:127` → `TNOTIFICATIONSDATAMODULE/0` |

###  — 22

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 14 | Nome do snapshot pré e pós-instalação customizável ("Allow custom snapshot name") | `inventario.md:28` → `Allow custom snapshot name` |
| 15 | Pasta de snapshots com limite de tamanho e contagem ("Limits for Snapshots folder") | `inventario.md:326` → `Limits for Snapshots folder` |
| 18 | **Perfis** separados: Scanning, Analysis, Uninstall, Comparing, Restore/Redo, Cleaner, Auto-run view | `inventario.md:9` → `tabAnalysisProfile`, `tabCompareProfile`, `tabScanProfile`, `tabUninstPrf`, `tabRedoProfiles` |
| 20 | Varredura de **Alternate Data Streams** (ADS) | `inventario.md:176` → `Edit program Delete alternate data stream: %s` |
| 21 | **Visões de mudança** configuráveis ("Change views") | `inventario.md:81` → `Change views`; `inventario.md:9` → `tabChangeView`, `tabChangeViews` |
| 50 | **Agente autônomo** de baixo consumo para notificar instalações em andamento | `strings.txt:15827` → `AAuto-start with Windows to continue unfinished monitoring process` |
| 60 | Validação **online de licença** | `imports.txt:14` → `wininet.dll / InternetCloseHandle`; `inventario.md:325` → `Licensed to "%s"` |
| 30 | Restaurar **só o log de instalação**, sem o programa | `strings.txt:16320` → `Create installation log`; `recursos.txt:149` → `TDLGTUNRESTORE` |
| 32 | **Undo/Redo** de uma restauração | `inventario.md:9` → `tabRedo`, `tabRedoAdvanced`, `tabRedoProfiles` |
| 37 | Itens compartilhados: contadores de referência em registro e diálogo "Remove shared file" | `strings.txt:51011` → `Decrement registry shared file counter`; `strings.txt:51014` → `Shared file counter found too small in registry`; `recursos.txt:143` → `TDLGRMVSHARED` |
| 65 | **Instalar programa novo** com monitoramento do instalador | `inventario.md:9` → `tbtnInstallNew`; `strings.txt:16814` → `Install new...` |
| 66 | **Instalar atualização** de programa já instalado, monitorando o update | `inventario.md:9` → `tbtnInstallUpdate`; `strings.txt:16817` → `Install update...`; `inventario.md:344` → `Monitor program update "%s"` |
| 67 | **Mesclar logs** de instalação (e apagar o log do update depois) | `recursos.txt:139` → `TDLGMERGELOGS`; `strings.txt:17030` → `Merge logs...`; `inventario.md:152` → `Delete update log after merge` |
| 72 | **Visor hexadecimal** e abertura de arquivo em editor hex/texto | `strings.txt:17284` → `Hex view`; `strings.txt:17285` → `Open file in hex editor`; `strings.txt:15646` → `Hex viewer` |
| 73 | **Soma MD5 dos arquivos novos** criados pela instalação | `strings.txt:15993` → `Compute MD5 for new files`; `strings.txt:16071` → `New file MD5 sum` |
| 76 | **Incluir/excluir por produto e por publicador** | `strings.txt:16193` → `Include publishers`; `strings.txt:16201` → `Exclude publishers`; `inventario.md:263` → `Include products`; `inventario.md:189` → `Exclude products` |
| 78 | **Classificação dos itens detectados**: existentes no sistema / alterados / ausentes | `strings.txt:16039` → `Existing items in system`; `strings.txt:16048` → `Items altered in system`; `strings.txt:16044` → `Items missing from system` |
| 79 | **Customização dos botões da barra** e legendas dos botões | `strings.txt:16594` → `Add or Remove Buttons`; `strings.txt:16995` → `Button caption`; `strings.txt:16996` → `(Show or hide the toolbar button captions` |
| 83 | **Dependências entre pacotes MSI** ("Depends on packages") | `strings.txt:50864` → `Depends on packages&Uninstall the selected application(s)?%Error uninstalling %d application(s).` |
| 84 | **Verificação de DCOM** do produto | `strings.txt:51107` → `DCOM not installed` |
| 88 | **Retomar o monitoramento inacabado** no arranque do Windows | `strings.txt:15827` → `AAuto-start with Windows to continue unfinished monitoring process` |
| 94 | **Licença por família / múltiplos computadores / avaliação / manutenção expirada** | `strings.txt:50980` → `Family License`; `strings.txt:50982` → `Multiple Computers License`; `strings.txt:50913` → `Family license is not valid.4The maintenance period for your license has expired.` |

###  — 32

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 10 | **Modify** — tela de modificação de componentes | `strings.txt:4953` → `MainMenu/mnuActionModify`; `:14050` → `<mnuActionModify>&amp;Modify...</mnuActionModify>` |
| 15 | Lote com **teto de itens** e aviso explícito | `strings.txt:14151` → `<itemBatchLimitationWarning>You selected %u items but maximum number of items in a batch operation is limited to %u</itemBatchLimitationWarning>` |
| 18 | Detecção de **itens relacionados** e **dependentes** | nao confirmado no binario |
| 19 | **Cancelamento forçado** de um desinstalador travado, com aviso de riscos | nao confirmado no binario |
| 28 | **Log detalhado de remoção por item**, com código de erro | `strings.txt:5245` → `UninstallWizard/staticHyperlinkRemovalLog` |
| 38 | **Reverter** a instalação rastreada | `strings.txt:5157` → `InstallTracker/btnNoAndRevert` |
| 39 | **Anexar** dados rastreados a um item existente | nao confirmado no binario |
| 40 | Detecção de **múltiplas entradas** de instalação | nao confirmado no binario |
| 41 | **Entrada customizada** quando o instalador não deixou registro | `strings.txt:14356` → `<checkCreateCustomEntry>I'd like to create a custom item in the list</checkCreateCustomEntry>` |
| 44 | Menu de contexto do Explorer: **"Install and Trace"** para `.exe`/`.msi` | `strings.txt:14213` → `<checkShellExtension>Add 'Install and Trace' command for setup files (exe/msi) to the Windows Explorer context menu</checkShellExtension>` |
| 45 | Menu de contexto dos **atalhos**: "Uninstall with Uninstall Tool" | nao confirmado no binario |
| 46 | Extensão de shell **empacotada em MSIX** para o Windows 11 | nao confirmado no binario (o pacote `.msix` é arquivo do instalador, não recurso do PE) |
| 47 | Serviço de rastreamento **versionado**, com prompt de atualização | nao confirmado no binario |
| 48 | Setup instala o serviço e o **driver de kernel** | `inventario.md:274` → `driver file doesn't seem to exist in the app folder` |
| 57 | Tarefas criadas pela Microsoft **ocultadas** por segurança | nao confirmado no binario |
| 67 | Relatório de **crash** com ficheiro de dump | nao confirmado no binario |
| 68 | **Contornar o UAC** criando uma tarefa elevada do Task Scheduler executada a cada abertura do app | `strings.txt:14207` → `<checkSkipUAC>Use Windows Task Scheduler to bypass User Account Control confirmation dialog (when necessary)</checkSkipUAC>`; `:14208` → `<checkSkipUACHelpText>...creates a Windows Task Scheduler elevated task which is executed every time you'd like to run the app.</checkSkipUACHelpText>`; `:15548` → `control.exe schedtasks` |
| 69 | **Ícone do Uninstall Tool no Painel de Controle** | `strings.txt:14212` → `<checkControlPanelIcon>Show Uninstall Tool icon in Control Panel</checkControlPanelIcon>`; `:17818` → `Control Panel icon` |
| 70 | **Popup de aplicações recém-instaladas** | `strings.txt:14233` → `<checkDisplayPopup>Display pop-up window with recently installed applications</checkDisplayPopup>`; `:17828` → `Display popup window with recently installed applications` |
| 72 | **Programas recentes na barra secundária** | `strings.txt:14235` → `<checkRecentOnSecondaryToolbar>Display recent programs in the secondary toolbar</checkRecentOnSecondaryToolbar>` |
| 76 | **Minimizar na bandeja durante a instalação** | `strings.txt:14295` → `<checkMinimize>Minimize Uninstall Tool to system tray during installation</checkMinimize>` |
| 77 | **Exibir o ícone na bandeja do sistema** | `inventario.md:271` → `Display Uninstall Tool system tray icon` |
| 78 | **Iniciar automaticamente minimizado na bandeja** | `inventario.md:56` → `Automatically start Uninstall Tool minimized in system tray on Windows startup` |
| 79 | **Navegar até a pasta do programa com o Total Commander** | `strings.txt:14214` → `<checkTotalCommander>Navigate to program directory using Total Commander (if installed)</checkTotalCommander>` |
| 80 | **Criar item personalizado na lista** | `strings.txt:14356` → `<checkCreateCustomEntry>I'd like to create a custom item in the list</checkCreateCustomEntry>` |
| 82 | **Lista compacta de aplicativos** | `strings.txt:14219` → `<checkSmallIcons>Compact list with apps</checkSmallIcons>` |
| 83 | **Chave de licença com lista negra** e chave separada para a versão portátil | `strings.txt:4592` → `activation: black listed key`; `:14460` → `<msgRegNotForPortableTitle>This key cannot activate the portable version</msgRegNotForPortableTitle>`; `:5692` → `checking license key {}...` |
| 84 | **Limpar o cache** da aplicação | `inventario.md:168` → `Clear cache` |
| 90 | **Detecção de falso positivo** ao fixar a localização de um item | `inventario.md:115` → `Cannot set location for item (false positive risk) {}` |
| 91 | **Coleta de identificação da máquina** para a licença | `inventario.md:227` → `cpu name is {}`; `:242` → `Data folder is {}, ({})` |
| 92 | **Validação de cadeia de certificados** embarcada (CRYPTO/SSL) | `imports.txt:48` → `CRYPT32.dll / CertVerifyCertificateChainPolicy`, `CertGetCertificateChain` |
| 94 | **Install Tracker com driver de kernel**: minifiltro de sistema de arquivos + callback de registro + callback de processos | `inventario.md:159` → `CisUtMonitor: DriverEntry`; `:161` → `FltRegisterFilter FAILED!`; `:160` → `CmRegisterCallback FAILED!`; `:165` → `PsSetCreateProcessNotifyRoutine FAILED!` |

###  — 20

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 10 | **Exclusão** de arquivos/pastas/unidades, configurável | `Uninstaller.options-ignore-paths`, `Uninstaller.options-ignore-paths-tip`, `-ignore-paths-format` (`strings-uninstalr-own.txt:9767-9771`) |
| 14 | **16 tipos de app** com detecção dedicada: normal, MS Store, portable, sobras, Chocolatey, Ninite, PortableApps.com, Steam, EA App, Epic, Riot, GOG Galaxy, Wargaming.net, Battle.net, itch.io, Big Fish | Sem lastro no codigo |
| 15 | Detecção de sobras via **MuiCache / TypeLib / AppX ModelID** | Grep "ModelId"/"ModelID" sem resultado. `Core/Modules/WindowsApps.cs` gerencia pacotes MSIX (listar/remover), mas nao varre sobras por ModelID |
| 16 | **País de origem** do app, com bandeira | Grep "country"/"Country" sem resultado em `src/` |
| 21 | **Reiniciar automaticamente** para finalizar (avisa que pode reiniciar 1 ou 2 vezes) | `Core/Automation/ShutdownGuard.cs:18` faz o **contrario** (impede desligamento durante a tarefa); nao ha reinicio automatico |
| 23 | **Gerar `.bat`** para desinstalação em lote + `PreStartupDelete.bat` para o próximo arranque | `Core/Reporting/BatchScriptWriter.cs:35` gera script de **desinstalacao em lote** sob demanda; nao ha script de limpeza agendado para o proximo arranque |
| 24 | **Retomar a desinstalação após reinício** via `RunOnce` (`Uninstalr_Unsetup_`, `Uninstalr_Rerun_`) | Nada grava `RunOnce` (grep so acha leitura em `Core/Discovery/StartupEntries.cs` e `Core/Tools/AutorunManager.cs`); `Core/DeferredDelete.cs` reagenda so **arquivo**; `Core/Automation/UninstallSession.cs` grava a sessao mas `UninstallSession.Pending` so e consumido em `Tranal.Tests` - nenhum consumidor em Core/Ui/Cli retoma |
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

###  — 26

| # | Funcionalidade | Evidencia no codigo |
|---|---|---|
| 7 | "Modificar" — reaproveita o instalador do programa | `wise.recursos.txt:144` -> `TFRMBEFOREUNISTALL/0` |
| 9 | Aviso para não usar a Forçada antes da Segura | `wise.identificadores.txt:70` -> `FrmMain.BtnForce.Caption` |
| 12 | Extensão de navegador, com aviso de **sincronização** | `wise.recursos.txt:151` -> `TFRMPLUGDELCOMP/0` |
| 14 | Recusa de desinstalar a si mesmo | `wise.recursos.txt:165` -> `TFRMWPUMAIN/0` |
| 16 | Aviso de desinstalação anterior em andamento | `wise.recursos.txt:144` -> `TFRMBEFOREUNISTALL/0` |
| 32 | **Desinstalação Personalizada** — varre por palavra-chave e deixa explorar a pasta | `wise.recursos.txt:162` -> `TFRMUNLESSDEL/0` |
| 33 | Botão "Explorar" para apagar o que for encontrado | `wise.recursos.txt:162` -> `TFRMUNLESSDEL/0` |
| 35 | Detecção da desinstalação feita por **outro** desinstalador | `wise.recursos.txt:144` -> `TFRMBEFOREUNISTALL/0` |
| 41 | "Block pop-up notifications and unwanted ads" | `wise.strings.txt:62862` -> `"defaultname": "Popup notifications"` |
| 45 | Diálogo de **notificações** com remetente e tipo | `wise.recursos.txt:149` -> `TFRMNOTIFYDIALOG/0` |
| 46 | Registro de "última análise" | `wise.recursos.txt:146` -> `TFRMCLEANPAGE/0` |
| 50 | Bloqueio de notificações pop-up (regra `PopNotify`) | `wise.strings.txt:62861` -> `"tranname": "PopNotify",`; `:62885` -> `"type": "PopNotify",` |
| 52 | **Base embutida de regras**: **595 perfis** nomeados e **2.379 caminhos** | `wise.strings.txt:62853-62892` (bloco JSON de regras: `tranname`, `defaultname`, `type`, `key`, `path0`…); `wise.inventario.md:1196` -> `Rule` - Motor de regras |
| 53 | Tipos de regra: **1.513 File**, **671 RegKey**, **166 RegValue**, **22 Reg**, **16 Ini**, **11 PopNotify** | `wise.strings.txt:62885` -> `"type": "PopNotify",`; `:62856` -> `"key": "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Comodo Dragon"` |
| 54 | Perfis de conhecidos: Epic Games Launcher, Discord, Slack, AnyDesk, Xmind, AVG PC TuneUp, IObit Driver Booster, WeChat Cache, Bilibili Offline Cache, Razer Software, DiskDrill Software, 360 Total Security, Adobe Acrobat, Ableton Live, ACDSee… | `wise.strings.txt:62853-62892` (bloco JSON de perfis nomeados) |
| 55 | Tabela de caminhos de registro por app, com marcação `(Reg)`: **48 entradas, 14 com `(Reg)`** | `wise.strings.txt:62854` -> `"key": "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Vivaldi"`; `:62858` -> `...Uninstall\\SogouExplorer"` |
| 60 | Datas **relativas** no histórico | `wise.recursos.txt:163` -> `TFRMUNLOG/0` |
| 66 | Ativação de licença PRO online, com validade e renovação | `wise.recursos.txt:156` -> `TFRMREG/0`; `:157` -> `TFRMRENEW/0` |
| 68 | "Traduzir pro…" — convida a traduzir | `wise.recursos.txt:147` -> `TFRMFEEDBACK/0` |
| 69 | Programa de melhoria da experiência do usuário (opt-in) | `wise.recursos.txt:147` -> `TFRMFEEDBACK/0` |
| 73 | Tela **"My View"** (visão do usuário) | `wise.recursos.txt:148` -> `TFRMMYVIEW/0` |
| 74 | Diálogo **"Catch"** (captura do estado do sistema antes da ação) | `wise.recursos.txt:145` -> `TFRMCATCH/0` |
| 77 | **Renovação de licença** | `wise.recursos.txt:157` -> `TFRMRENEW/0` |
| 79 | **Desinstalar componente de plugin** | `wise.recursos.txt:151` -> `TFRMPLUGDELCOMP/0` |
| 82 | Acesso ao **gerenciador de credenciais** do Windows | `wise.identificadores.txt:228` -> `Winapi.Security.Credentials` |
| 83 | Detecção de **geolocalização** | `wise.identifiers.txt:227` -> `Winapi.Devices.Geolocation` |
