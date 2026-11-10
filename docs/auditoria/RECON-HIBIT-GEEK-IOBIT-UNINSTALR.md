# Reconciliacao de lacunas contra o codigo - HiBit, Geek, IObit e Uninstalr

**Metodo: lido no codigo, nao na memoria.** Cada linha de lacuna dos quatro
documentos `docs/auditoria/BINARIO-<CONCORRENTE>.md` foi conferida contra
`src/Tranal.Uninstaller/Core/**`, `src/Tranal.Uninstaller/Ui/**` e
`src/Tranal.Cli/**` da copia `waves2/w1` (HEAD b36b20d). Onde a alegacao nao
tinha evidencia no proprio dump do concorrente, a linha entrou como
**NAO CONFIRMADO NO BINARIO** - a ausencia de literal no exe do outro nao
prova a ausencia do recurso, so prova que a alegacao continua sem lastro.


Criterio de classificacao (aplicado igual em todas as 154 linhas):

- **JA EXISTE** - o Tranal entrega a capacidade. A coluna "Evidencia" traz
  `arquivo::simbolo`. Quando a linha do concorrente tem duas partes, a nota
  diz exatamente qual parte nao e coberta.
- **NAO EXISTE** - falta a capacidade. A coluna "Evidencia" diz o que falta
  e o que existe de vizinho.
- **FORA POR DECISAO** - limite declarado do projeto. So vale com a decisao
  registrada citada (BINARIO.md 3.2, MATRIZ.md E.1 ou PARIDADE.md).
- **NAO CONFIRMADO NO BINARIO** - a alegacao do concorrente nao tem evidencia
  no dump de strings/imports dele; nao da para afirmar nem negar.

Total apurado: **154 linhas** (HiBit 56, Geek 30, IObit 34, Uninstalr 34).

---

## 1. HiBit Uninstaller

| # | Funcionalidade (o que o concorrente tem) | Estado no Tranal | Evidencia no codigo |
|---|---|---|---|
| 1 | Colunas completas: falta Tipo (32/64) e local de instalacao | JA EXISTE | `Core/ProgramColumns.cs:21` (`ProgramColumn.Type`, "32 ou 64 bits"); local em `Core/Appearance/ProgramTextFormatter.cs` e `Core/Appearance/ProgramFilters.cs` (`HighlightScope.InstallLocation`). **Nota**: o local de instalacao nao e coluna da grade, aparece em detalhes/copia/filtro |
| 3 | Desinstalacao em lote com selecao multipla na grade | NAO CONFIRMADO NO BINARIO | O proprio documento diz "nao confirmado no binario". (No Tranal ja existe lote: `src/Tranal.Uninstaller/MainWindow.xaml:798` (`SelectionMode="Extended"`) + `src/Tranal.Uninstaller/Core/Automation/UninstallQueue.cs` + `src/Tranal.Uninstaller/MainWindow.xaml.cs:1134`) |
| 4 | Desinstalacao forcada reagendada para o proximo logon via `RunOnce` | NAO EXISTE | `Core/DeferredDelete.cs` reagenda **arquivo** com `MoveFileEx(DELAY_UNTIL_REBOOT)`, nao a operacao; `Core/Automation/UninstallSession.cs:130` grava sessao, mas `UninstallSession.Pending` so e consumido em `Tranal.Tests`; nao ha escrita em `RunOnce` (grep so acha leitura em `Core/Discovery/StartupEntries.cs` e `Core/Tools/AutorunManager.cs`) |
| 5 | Varredura de sobras em arvore por categoria (registro/arquivo/tarefa/servico) | JA EXISTE | `Core/UninstallSupport.cs:523` (`LeftoverScanner`) + 15 motivos em `LeftoverReason` (`Core/UninstallSupport.cs:49-117`), incluindo atalho obsoleto (`LeftoverReason.ObsoleteShortcut`), Menu Iniciar obsoleto (`LeftoverReason.ObsoleteStartMenu`) e entrada de inicializacao (`LeftoverReason.StartupEntry`). **Nota**: servico e tarefa tem gerenciador proprio (`Core/Management/ServiceManager.cs:113`, `Core/Management/ScheduledTaskManager.cs:79`) e nao entram na arvore de sobras |
| 6 | Backup proprio `.dbh` com restauracao | JA EXISTE | `Core/BackupManager.cs:165/193/225/282/601`, `Core/FullRegistryBackup.cs`, `Core/Recovery/ProgramBackup.cs`, `Core/Recovery/LeftoverBackup.cs`, `Ui/BackupManagerWindow.xaml.cs`. Formato e JSON + arquivos, nao `.dbh` |
| 8 | Relatorios HTML por ferramenta (servicos, drivers, tarefas, inicializacao, menu de contexto) | NAO EXISTE | Exporta HTML da **lista** (`Core/Exporters.cs:63`) e do **log de instalacao** (`Core/Reporting/TraceLogExporter.cs`); `Core/Reporting/ReportFormats.cs` trata lista/registro. Nao ha relatorio dedicado por ferramenta |
| 9 | Ocultar entrada de desinstalacao | NAO CONFIRMADO NO BINARIO | Alegacao sem lastro no dump do proprio HiBit |
| 10 | Apagar a entrada do registro do programa como acao avulsa | JA EXISTE | `src/Tranal.Uninstaller/Core/Management/WindowsListEntry.cs:136` (`RemoveMany`), `:109` (`Remove`), `src/Tranal.Uninstaller/Core/RegistryEditing/RegistryBatchRemover.cs:85` (`FromProgram`), `:121` (`Remove`), `src/Tranal.Uninstaller/MainWindow.xaml.cs:1134` (`CmdRemoveEntriesBatch`), `:1831` (`RemoveEntriesBatch`), `src/Tranal.Cli/EntryCommands.cs:24` (`/remove-entries`). A remocao das entradas da lista do Windows existe **em lote** e na tela. A REGRA mora no nucleo, em um lugar so: a acao avulsa (`:109`) delega para a acao em lote (`:136`), e as duas passam pela mesma remocao (`RegistryBatchRemover`, `:121`), que respeita a chave protegida e a visao do registro do programa (`:85`). **Nao desinstala nada**: o programa continua instalado, e a confirmacao da tela e o aviso da linha de comando dizem isso. Medido: `tools/test-lote26.ps1` -> a entrada de teste sai do registro, a linha de comando avisa e o menu "Mais comandos" mostra a acao. |
| 11 | Editar informacoes do programa | JA EXISTE | `Core/ProgramCustomization.cs:246/263/306/317/460` (nome, comentario, icone, grupo, estrelas) + `Ui/RenameWindow.xaml.cs`, `Ui/CommentWindow.xaml.cs`, `Ui/IconPickerWindow.xaml.cs` |
| 13 | Copiar dados do programa para a area de transferencia | JA EXISTE | `Core/Appearance/ProgramTextFormatter.cs:47` (`Format`, `ClipboardFormat`) + `Ui/Appearance/ProgramClipboard.cs` |
| 15 | Modificar/reparar instalacao MSI (`ModifyPath`) | JA EXISTE | `Core/Discovery/MsiProductCatalog.cs:122` (`TryBuildRepairCommand`) e (`Repair`) |
| 17 | Menu de contexto do Monitor de Instalacao (entrada separada no shell) | NAO EXISTE | `Core/Topmost/ExplorerContextMenu.cs` registra apenas o menu de desinstalacao do Tranal; nao ha entrada do monitor |
| 18 | Gerenciador do Menu de Contexto - cria e remove menus do shell, com "Restaurar" e "Excluir tudo" | JA EXISTE | `Core/Management/ContextMenuOverview.cs:94` (`List`), `:377` (`Restore`), `:269` (`RemoveAll`), `:245` (`Create`), `:420` (`Remove`); `Core/Management/ContextMenuManager.cs:123` (`List`), `:212` (`ListFor`), `:314` (`Remove`); `Core/Topmost/CustomShellMenu.cs:81` (`Create`); a tela `Ui/Tools/ContextMenuViews.cs:21` e a CLI `src/Tranal.Cli/ExtraCommands.cs:1494`. Medido no aplicativo de verdade: 41 entradas (4 do Tranal e 37 do shell) |
| 20 | Monitoramento de instalacao com eventos por tipo | JA EXISTE | `Core/Modules/InstallMonitor.cs:103` (`Start`) e (`StopAndSave`); `Core/Tracing/WriteWatcher.cs` (observacao de escrita, sem driver); `Core/Tracing/TraceEntry.cs` (`TraceChangeKind`) |
| 21 | Desinstalar atualizacoes do Windows (`wusa`/KB) | NAO EXISTE | `Core/Discovery/WindowsFeatures.cs:350` (`ListInstalled`) e (`ListAvailable`) so catalogam; nao existe comando de remocao de KB |
| 59 | Arrastar e soltar um programa para desinstalar | JA EXISTE | `Ui/Modules/HunterView.xaml` (`AllowDrop`) + `Core/Modules/HunterMode.cs:41` (`Resolve`) |
| 60 | Painel de informacoes do sistema no banner | NAO CONFIRMADO NO BINARIO | Alegacao sem lastro no dump do proprio HiBit |
| 61 | Seletor de tema na barra superior, com amostras de cor | JA EXISTE | `Core/Appearance/ThemePalette.cs:94-124` (`ThemeSkinCatalog.All`), seletor em `Theming/ThemeManager.cs` e `Ui/Appearance/AppearanceView.xaml.cs`. **Nota**: 4 temas (Dark/Slate/Contrast/Light), nao 7 |
| 62 | Botao Doar | NAO CONFIRMADO NO BINARIO | Alegacao sem lastro no dump do proprio HiBit |
| 63 | Data de instalacao em tempo relativo ("Hoje", "Ontem") | NAO CONFIRMADO NO BINARIO | Alegacao sem lastro no dump do proprio HiBit |
| 64 | Coluna Tipo (32/64) por programa | NAO CONFIRMADO NO BINARIO | Alegacao sem lastro no dump do proprio HiBit (o recurso existe no Tranal: `ProgramColumn.Type`) |
| 65 | Contador de tamanho total dos programas na barra de status | NAO EXISTE | A barra de status existe (`MainWindow.xaml:409-411`) mas mostra **contagem**, nao tamanho: `MainWindow.xaml.cs` |
| 66 | Painel de detalhes do programa (local, entrada de registro, website) | JA EXISTE | `Ui/Tools/TopmostOverlayWindow.xaml.cs:485-486` (editora, local), `Core/ProgramColumns.cs` (`HomePage`) e (`RegistryKey`), `Core/Appearance/ProgramTextFormatter.cs` |
| 67 | Gerenciador de Inicializacao que cria e edita itens de autostart | JA EXISTE | `Core/Tools/AutorunManager.cs:105` (`Scan`), (`SetEnabled`), (`Remove`), (`Add`); painel em `Ui/Tools/ToolViews.cs:112-115`. **Nota**: o painel expoe Refresh/Enable/Disable/Remove, nao um botao "criar" |
| 68 | Gerenciador de Servicos que **cria** servico | NAO EXISTE | `Core/Management/ServiceManager.cs:113/148/199/209/217/242` - List/ListFor/Disable/Enable/Stop/Delete. Nenhum `Create` |
| 69 | Gerenciador de Processos (painel) | NAO EXISTE | `Core/ProcessTerminator.cs:61/107/156` encerra processos e `Ui/Tools/TopmostOverlayWindow.xaml.cs` tem "Kill process"; nao existe listagem/painel de processos do sistema |
| 70 | Gerenciador de Tarefas Agendadas | JA EXISTE | `Core/Management/ScheduledTaskManager.cs:79/114/125/137/149` + `Ui/Management/ManagementView.xaml.cs` |
| 71 | Gerenciador de Menu de Contexto (janela propria) | JA EXISTE | `Core/Management/ContextMenuOverview.cs:94` (`List`); a tela `Ui/Tools/ContextMenuViews.cs:21` (janela propria, com "Restaurar", "Desativar", "Remover", "Criar entrada" e "Apagar tudo") e a CLI `src/Tranal.Cli/ExtraCommands.cs:1494` |
| 72 | Gerenciador de Componentes de Programas (DLLs compartilhadas) | NAO EXISTE | `Core/ProgramScanner.cs:64` so le chave de desinstalacao, catalogo MSI e Store (`Core/ProgramColumns.cs`); nao ha enumerador de DLL compartilhada entre programas |
| 73 | Gerenciador de Extensoes de Navegador para mais navegadores | NAO EXISTE | `Core/Modules/BrowserExtensions.cs:76-80` cobre apenas chrome, edge, opera (+ firefox em ); nao ha Brave, Vivaldi, Yandex |
| 74 | Gerenciador de Conexoes (rede) | FORA POR DECISAO | `docs/auditoria/BINARIO.md` 3.2: "Analisador de disco, gerenciador de conexoes, de fontes, papel de parede, destruidor de arquivos - Nao e dominio de desinstalacao" |
| 75 | Backups & Reports (painel proprio de backup e de relatorio) | JA EXISTE | `Ui/BackupManagerWindow.xaml.cs` e `Ui/Reporting/ReportingWindow.xaml.cs` |
| 76 | Restaurador do Sistema (criar/excluir pontos com um clique) | JA EXISTE | `Core/BackupManager.cs` (`Create`) e `Core/UninstallSupport.cs:1391` (`TryCreateRestorePoint`); painel em `Ui/BackupManagerWindow.xaml.cs`. **Nota**: cria ponto; nao exclui pontos de restauracao do Windows |
| 77 | Limpador de Registro com 15+ categorias (ActiveX, FileTypes, SharedDLLs, IFEO, FirewallRules, MUICache) | NAO EXISTE | `Core/Tools/RegistryOptimizer.cs` trata so entradas obsoletas do Menu Iniciar; `Core/Tools/PrivacyTraceCleaner.cs` tem 6 grupos de rastros; nao ha MUICache no codigo (grep "MuiCache" sem resultado) nem ActiveX/FileTypes/SharedDLLs/IFEO |
| 78 | Limpador de Arquivos de Lixo com 17 categorias | JA EXISTE | `Core/Tools/JunkCleaner.cs:60-81` - 8 varreduras/categorias: temp do usuario, temp do Windows, cache do Windows Update, caches dos navegadores, logs antigos, prefetch, miniaturas, lixeira. **Nota**: 8, nao 17 |
| 80 | Fixador de Atalhos (atalhos corrompidos) | NAO EXISTE | `LeftoverReason.ObsoleteShortcut` (`Core/UninstallSupport.cs`) so **apaga** atalho obsoleto; nao ha verificacao nem reparo do alvo do atalho |
| 81 | Destruidor de Arquivos com 5 algoritmos de hash e relatorio de hash | JA EXISTE | `Core/Tools/UnrecoverableDelete.cs:118` (`Delete(paths, passes)`, `DefaultPasses`) + `Ui/Tools/ToolViews.cs:477-568`. **Nota**: sobrescreve e apaga; nao tem MD5/SHA-1/SHA-256/SHA-512/CRC32 nem relatorio de hash |
| 82 | Localizador de Arquivos e Registros | NAO EXISTE | `Core/Tools/EvidenceRemover.cs` so apaga rastros; nao ha busca por nome de arquivo ou de chave |
| 83 | Localizador de Arquivos Duplicados | JA EXISTE | `Core/Management/SoftwareHealth.cs:184` (`DuplicateFindings`) + `HealthCategory.RedundantFiles`, opt-in via `SoftwareHealth.scanDuplicates`. **Nota**: nao e localizador autonomo, e uma categoria do painel Software Health |
| 84 | Analisador de disco (por arquivo, nao por programa) | FORA POR DECISAO | `docs/auditoria/BINARIO.md` 3.2 (analisador de disco nao e dominio de desinstalacao) |
| 86 | Gerenciador de Atualizacoes do Windows (painel proprio) | JA EXISTE | `Ui/Discovery/DiscoveryWindow.xaml.cs:42` (categoria "Atualizacoes do Windows") + (`WindowsUpdateCatalog.ListInstalled`). **Nota**: vive dentro do Discovery, nao e ferramenta separada |
| 87 | Propriedades de processo (painel dedicado) | NAO EXISTE | Nao existe nem listagem de processos do sistema (ver linha 69) |
| 88 | Afinidade de processador do programa | NAO EXISTE | Nenhum uso de `GetProcessAffinityMask`/`SetProcessAffinityMask` no codigo |
| 89 | Scanner VirusTotal (verificacao de arquivo na nuvem) | FORA POR DECISAO | `docs/auditoria/BINARIO.md` 3.2: "Base de logs **online** do Revo - Decisao de produto: o Tranal e offline, sem cadastro e sem telemetria". **Ver Limites**: a decisao nao nomeia o VirusTotal |
| 90 | Integracao com o Windows Defender (cache, limpeza, varredura agendada) | FORA POR DECISAO | Mesma decisao offline (BINARIO.md 3.2); o Tranal apenas protege o Defender da desinstalacao (`Core/Rules/BuiltinRules.cs`). **Ver Limites** |
| 91 | Criar item de inicializacao do zero | JA EXISTE | `Core/Tools/AutorunManager.cs` (`Add`). **Nota**: o painel da ferramenta nao expoe o botao "criar" (`Ui/Tools/ToolViews.cs:112-115`) |
| 92 | Criar servico do Windows | NAO EXISTE | `Core/Management/ServiceManager.cs` nao tem `Create` (so List/Disable/Enable/Stop/Delete) |
| 93 | Criar tarefa agendada | NAO EXISTE | `Core/Management/ScheduledTaskManager.cs` nao tem `Create` (so List/Disable/Enable/Delete) |
| 94 | Criar menu de contexto no Explorer | JA EXISTE | `Core/Topmost/ExplorerContextMenu.cs:189` (`Apply`), (`BuildRegistryScript`), (`RemoveRegistryScript`) |
| 95 | Limpeza personalizada (regras proprias por ferramenta) | JA EXISTE | `Core/Rules/RuleEngine.cs:207/222/178`, `Core/Rules/RemovalRule.cs`, `Core/Tools/CustomToolRegistry.cs` |
| 96 | Listas de exclusao / ignorar por ferramenta | JA EXISTE | `Core/ExcludeList.cs:128/141/154/166/249` + `Ui/ExcludeListWindow.xaml.cs` + `Core/Rules/RuleEngine.cs:274` (`IsPathProtected`). **Nota**: lista global, nao uma por ferramenta |
| 97 | Restaurar o registro a partir do backup | JA EXISTE | `Core/BackupManager.cs` (`Restore`), `Core/Recovery/ProgramBackup.cs`, `Core/Recovery/RecoverOperation.cs` |
| 98 | Verificacao de integridade de arquivo e limpeza pontual de arquivo | NAO EXISTE | Nao ha verificacao de integridade por hash. A limpeza pontual existe (`Core/Tools/JunkCleaner.cs`, `Core/Tools/EvidenceRemover.cs`, `Core/Tools/UnrecoverableDelete.cs`) |
| 99 | Sete temas VCL embutidos no binario | JA EXISTE | `Core/Appearance/ThemePalette.cs:94-124` - 4 temas no catalogo. **Nota**: 4, nao 7 |
| 100 | Banco local embutido (`LOCALDB`) | NAO EXISTE | O estado e JSON: `Core/Settings/ProgramCache.cs` (`ProgramCacheStore`), `Core/Modules/TraceStore.cs`, manifesto em `Core/BackupManager.cs`. Nao ha banco local |
| 101 | Salvar log de ferramenta em arquivo | JA EXISTE | `Core/Modules/LogOperations.cs:165`, `Core/RemovalLog.cs`, `Core/Reporting/TraceLogExporter.cs:203` (`Write`), `Core/Tools/WindowsToolsCatalog.cs:289` (`ToolLog`) |

---

## 2. Geek Uninstaller

| # | Funcionalidade (o que o concorrente tem) | Estado no Tranal | Evidencia no codigo |
|---|---|---|---|
| 1 | Modificar/reparar a instalacao (`Modify...`) | JA EXISTE | `Core/Discovery/MsiProductCatalog.cs:122` (`TryBuildRepairCommand`), (`Repair`); produtos MSI expostos em `Ui/Discovery/DiscoveryWindow.xaml.cs` |
| 2 | Batch Force Removal - remocao forca de varios programas de uma vez | JA EXISTE | Lote na interface: `MainWindow.xaml` (`SelectionMode="Extended"`) + `MainWindow.xaml.cs` (`Cmd.UninstallManyConfirm`) + `Core/Automation/UninstallQueue.cs`; forcado por alvo/palavra-chave: `Core/Automation/ForcedRemovalOptions.cs:41/50`, `Ui/ForcedUninstallWindow.xaml.cs`; CLI: `Tranal.Cli/ExtraCommands.cs` (`/batch`) |
| 3 | Batch Remove Entries - remocao em lote das entradas de registro | OK | A remocao em lote existe e tem acao na interface. A REGRA mora no nucleo (`src/Tranal.Uninstaller/Core/Management/WindowsListEntry.cs:136`, `RemoveMany`), o alvo sai da entrada do programa (`src/Tranal.Uninstaller/Core/RegistryEditing/RegistryBatchRemover.cs:85`, `FromProgram`) e a remocao em lote, com a protecao de chave protegida, esta em `:121` (`Remove`); a tela usa a acao em lote (`src/Tranal.Uninstaller/MainWindow.xaml.cs:1134`, `CmdRemoveEntriesBatch`) e a linha de comando tem `/remove-entries` (`src/Tranal.Cli/EntryCommands.cs:24`). Medido: `tools/test-lote26.ps1` -> a entrada de teste sai do registro, a linha de comando avisa que o programa NAO e desinstalado e o menu "Mais comandos" mostra a acao. |
| 4 | Desregistro de COM (`regsvr32 /u`) antes de remover | NAO EXISTE | Nenhum uso de `regsvr32` no codigo (grep sem resultado em `src/`) |
| 5 | Varredura de sobras de MSI pelo registro do Windows Installer | JA EXISTE | `Core/MsiInstallLog.cs:110` (`Capture`) e (`FindLeftovers`); `Core/Discovery/MsiProductCatalog.cs:177/205` |
| 6 | Desinstalar ate o registro de produtos MSI por chave de produto | JA EXISTE | `Core/Discovery/MsiProductCatalog.cs:248` (`Uninstall`), (`TryNormalizeProductCode`) |
| 7 | Varredura de sobras de MSI em traces de instalacao | JA EXISTE | `Core/MsiInstallLog.cs:110/130` (`Capture`), (`UsableAfter`); o log do instalador vira sobras por `LeftoverReason.InstallerLog` (`Core/UninstallSupport.cs`) |
| 8 | Limpeza do cache MRU do Windows (`Software\Classes\Local Settings\MrtCache`) | JA EXISTE | A chave esta na lista de MRU limpa pelo Tranal: `Core/Tools/MruCleaner.cs:62` (`Software\Classes\Local Settings\MrtCache`), com a tela em `Ui/Tools/CleanupViews.cs:34` e a CLI em `src/Tranal.Cli/CleanupCommands.cs:234` |
| 9 | Remocao adiada para o proximo reinicio quando o arquivo esta em uso | JA EXISTE | `Core/DeferredDelete.cs:27` (`Schedule`) e (`MoveFileEx` + `MOVEFILE_DELAY_UNTIL_REBOOT`); `Core/UninstallRemovalOptions.cs` (`DeferToNextRestart`); `Core/Removal.cs:8` (`RemovalStatus.Deferred`); `Core/RemovalLog.cs:69` (`DeferredToReboot`) |
| 10 | Guarda contra falso positivo de `InstallLocation` | JA EXISTE | `Core/Discovery/FolderConfidenceEvaluator.cs:165` (`Evaluate`, 7 criterios); avisos de baixa confianca em `Core/UninstallSupport.cs:199` (`LeftoverWarnings`) e (`LowConfidenceItems`) |
| 11 | Correspondencia aproximada (Levenshtein) do caminho de instalacao | NAO EXISTE | Grep "Levensh" sem resultado. O que existe: variantes por palavra inteira (`Core/UninstallSupport.cs:552` (`NameVariants`), `Core/UninstallSupport.cs:292` (`ContainsWord`) e `LeftoverReason.SimilarNameMatch`|
| 12 | Deteccao de instalacao em `Local AppData\Programs` | JA EXISTE | `Core/Discovery/UserInstallFolders.cs:87` (`IsUserInstallPath`) e (`Scan`); `Core/MsiInstallLog.cs` resolve "LocalAppDataProgramFilesFolder" |
| 13 | Varredura de atalhos do Menu Iniciar e da Area de Trabalho para achar icone e localizacao | NAO EXISTE | O Menu Iniciar so aparece como entrada obsoleta (`Core/Tools/RegistryOptimizer.cs`, `ScanStartMenu`) e atalho so como sobra (`Core/UninstallSupport.cs`). Nao ha varredura de atalhos para descobrir icone/local do programa |
| 14 | Cache de uninstall persistido em XML versionado | JA EXISTE | `Core/Settings/ProgramCache.cs:85` (`ProgramCache`), (`ProgramCacheStore`: Save/TryLoad/Invalidate) + `Core/Modules/TraceStore.cs`. **Nota**: e JSON, nao XML |
| 15 | Painel "Active Files" - arquivos em uso detectados | NAO EXISTE | Nao ha painel dedicado. A **deteccao** existe (o item em uso vira "adiado para o proximo reinicio": `Core/Removal.cs`, `Core/RemovalLog.cs`), mas nao a tela listando arquivos em uso |
| 16 | Dialogo de orfaos - "Alguns itens orfaos encontrados!" e oferta de remove-los | JA EXISTE | `Core/Discovery/DriveScanner.cs:82` (`OrphanUninstaller`) e (`DriveScanResult`), categoria em `Ui/Discovery/DiscoveryWindow.xaml.cs:44`; remocao assistida em `Core/Automation/ForcedRemovalOptions.cs` |
| 17 | Confirmacao ao cancelar a remocao de sobras | JA EXISTE | `Ui/UserMessages.cs:60` (`Confirm`) - a confirmacao do lote nunca e omitida (ver `MATRIZ.md` F.1 linha 27) |
| 18 | Dialogo de falha de desinstalacao com oferta de limpar a entrada do registro e os arquivos | NAO EXISTE | Ha o relatorio de remocao com codigo de erro (`Core/RemovalLog.cs`, `Core/Removal.cs`) e a desinstalacao forcada como caminho separado (`Ui/ForcedUninstallWindow.xaml.cs`), mas nao o dialogo "a desinstalacao falhou, quer limpar a entrada e os arquivos?" |
| 19 | "Browse..." dentro das sobras encontradas (navegar para o item) | JA EXISTE | A arvore das sobras (`Ui/Shared/LeftoversTree.xaml:26`) tem o botao "Explorar" (`Ui/Shared/LeftoversTree.xaml.cs:204`), que abre a pasta do item no Explorer (`:216`) ou a chave no editor de registro (`:211`). Medido: `tools/test-lote23.ps1`. |
| 20 | Aviso "No matching items, press ESC to reset" quando o filtro nao casa nada | NAO EXISTE | Nao ha estado/aviso de lista vazia por filtro (grep "NoResults"/"Sem resultados" sem resultado). O filtro tem validacao de regex (`Core/Appearance/ProgramFilters.cs`) e exclusoes (`ProgramFilters.AddExclusion`), mas nenhum aviso de "nada corresponde" |
| 21 | Abrir na Microsoft Store | JA EXISTE | O destino existe: `Core/Management/SystemNavigator.cs:116` (`MicrosoftStore`), ligado ao menu "Abrir no Windows" em `MainWindow.xaml.cs:1705` e a CLI em `src/Tranal.Cli/InspectionCommands.cs:519` (`tranal /inspection-store`) |
| 22 | Browse Source... - localizar o instalador-fonte do programa | JA EXISTE | A acao existe: `Core/Management/SystemNavigator.cs:155` (`UninstallerFolder`, a pasta do desinstalador) e `:138` (`ProgramFolder`, a pasta de instalacao), no menu "Abrir no Windows" em `MainWindow.xaml.cs:1705` |
| 23 | "Nenhum programa nesta categoria" (categoria da aba Microsoft Store vazia) | JA EXISTE | A string dedicada existe e e mostrada no rodape quando a lista esta vazia: `MainWindow.xaml.cs:404` (`Status.NoProgramsInCategory`) |
| 24 | Enumeracao MSIX por SID do usuario | NAO EXISTE | `Core/Modules/WindowsApps.cs` usa `Get-AppxPackage` (usuario atual), sem `-AllUsers` e sem SID |
| 25 | Log da desinstalacao gravado por programa | JA EXISTE | `Core/RemovalLog.cs` (`RemovalLog`, CSV/JSON por operacao) com codigo de erro do Windows (`Core/Removal.cs`) |
| 26 | Aviso quando a remocao da entrada falha por falta de permissao | JA EXISTE | `Core/Removal.cs` (`ErrorCodeOf`, 5 = acesso negado) + `Core/RemovalLog.cs`; o relatorio mostra a falha com codigo |
| 27 | Ciclo de mensagens do assistente ("Gerando lista de tracos..." -> "Todos os tracos removidos") | JA EXISTE | `Core/Automation/RemovalProgress.cs` (`BeginStep`), (`EndStep`); etapas em `Core/Automation/UninstallPlan.cs` (`UninstallStepKind`); tela `Ui/UninstallWizardWindow.xaml.cs` |
| 28 | Categoria Windows Updates como filtro de lista | JA EXISTE | `Ui/Discovery/DiscoveryWindow.xaml.cs:143` e (`WindowsUpdateCatalog.ListInstalled`) |
| 29 | Total agregado no rodape ("%d programs of %s size in total") | JA EXISTE | O rodape mostra a contagem E o tamanho total: `MainWindow.xaml.cs:411` (`ProgramTotalsCalculator.Compute`) e `:413` (`totais.Text`), com o total no nucleo em `Core/Modules/RelativeDate.cs:147` (`ProgramTotalsCalculator`) |
| 30 | Calculo do tamanho de cada programa no scanner | JA EXISTE | `Core/ProgramScanner.cs:405` (`MeasureFolder`) + `ScanOptions.ComputeFolderSize`  |

---

## 3. IObit Uninstaller

| # | Funcionalidade (o que o concorrente tem) | Estado no Tranal | Evidencia no codigo |
|---|---|---|---|
| 1 | Remocao de extensoes de navegador com base de assinaturas maliciosa/ad | FORA POR DECISAO | `docs/auditoria/BINARIO.md` 3.2: "Base de **reputacao** de extensoes de navegador (maliciosa/ad) - Exige consulta a um servico online; o Tranal e offline por decisao. A remocao da extensao existe." Confirmado em `MATRIZ.md` E.1 (A17) |
| 2 | Classificacao da extensao em Malicious / Ad / Trusted | FORA POR DECISAO | Mesma decisao (BINARIO.md 3.2; MATRIZ.md E.1, A17) |
| 3 | Limpeza de navegador com reset de pagina inicial e motor de busca | NAO EXISTE | O modulo so lista e remove extensoes (`Core/Modules/BrowserExtensions.cs:86` (`List`), `Core/Modules/BrowserExtensions.cs:113` (`Remove`)); a limpeza de rastros (`Core/Tools/TrackCleaner.cs`) nao mexe em pagina inicial nem motor de busca |
| 4 | Remocao de plugins de navegador (NPAPI/ActiveX) | NAO EXISTE | Nao ha catalogo nem remocao de plugins NPAPI/ActiveX; so extensoes Chromium/Firefox (`Core/Modules/BrowserExtensions.cs`) |
| 5 | Bloatware Remover com base propria e lista de Appx pre-instalados | NAO EXISTE | Grep "Bloat" sem resultado; nao ha lista de Appx pre-instalados. O que existe e catalogo de pacotes gerenciados (`Core/Discovery/PackageManagerCatalog.cs`) |
| 6 | Bundleware: detectar o que veio junto com o programa | NAO EXISTE | Grep "bundle" sem resultado em `src/` |
| 7 | Stubborn Program Remover com base de 4.000+ programas travados | NAO EXISTE | Grep "Stubborn" sem resultado. O Tranal tem desinstalacao forcada por alvo/palavra-chave (`Core/Automation/ForcedRemovalOptions.cs:41/50`) e remocao manual assistida (`Core/Automation/ManualRemoval.cs`) - nao e base de conhecimento |
| 8 | Force Uninstall+: desinstalar programa **sem entrada de registro** | JA EXISTE | `Core/Automation/ForcedRemovalOptions.cs` (`ForTarget`), (`ForKeyword`), (`Resolve`); `Ui/ForcedUninstallWindow.xaml.cs`; entradas sem desinstalador em `Core/ProgramScanner.cs:61` (`IncludeEntriesWithoutUninstaller`); executaveis que parecem desinstalador em `Core/Discovery/DriveScanner.cs` |
| 9 | Quick Uninstall: confirmar automaticamente o desinstalador original | NAO CONFIRMADO NO BINARIO | Alegacao sem lastro no dump do proprio IObit |
| 10 | Aviso de que o desinstalador vai deixar residuo | NAO CONFIRMADO NO BINARIO | Alegacao sem lastro no dump do proprio IObit |
| 11 | Confirmar/avisar antes de apagar arquivo pessoal | NAO CONFIRMADO NO BINARIO | Alegacao sem lastro no dump do proprio IObit |
| 12 | Sobras por categoria (Registro / Arquivo / Tarefas / Servicos / DLL) | JA EXISTE | `Core/UninstallSupport.cs:49` (`LeftoverReason`, 15 membros) e (`LeftoverScanner`); servico e tarefa tem gerenciador proprio (`Core/Management/ServiceManager.cs:113`, `Core/Management/ScheduledTaskManager.cs:79`). **Nota**: DLL nao e um motivo de sobra no Tranal |
| 16 | Desinstalacao em lote (multi-selecao) | NAO CONFIRMADO NO BINARIO | Alegacao sem lastro no dump do proprio IObit (o Tranal tem lote: `MainWindow.xaml`) |
| 17 | Easy Uninstall: arrastar o icone do programa para a janela | JA EXISTE | `Ui/Modules/HunterView.xaml` (`AllowDrop`, `Drop`, `DragOver`) + `Core/Modules/HunterMode.cs:41` (`Resolve`) |
| 18 | Install Monitor com log por categoria e **restauracao** do que foi gravado | JA EXISTE | `Core/Modules/InstallMonitor.cs:103/119`; `Core/Tracing/TraceLogV2.cs:89` (`Create`), (`Save`); `Core/Modules/TraceStore.cs:165` (`Save`); restauracao pela desinstalacao a partir do log (`Core/Modules/TracedPrograms.cs:119` (`Uninstall`), `Core/Modules/TracedPrograms.cs:143` (`UninstallByLogPath`) |
| 19 | Log de instalacao em SQLite embarcado | NAO EXISTE | O log e JSON (`Core/Tracing/TraceLogV2.cs` `Save`); nao ha SQLite no codigo |
| 20 | Desinstalar pelo log de instalacao | JA EXISTE | `Core/Modules/TracedPrograms.cs:119` (`Uninstall`), (`UninstallByLogPath`), (`Install`) |
| 21 | Monitor de instalacao em segundo plano no startup | NAO CONFIRMADO NO BINARIO | Alegacao sem lastro no dump do proprio IObit |
| 22 | File Shredder com 3 modos de sobrescrita | JA EXISTE | `Core/Tools/UnrecoverableDelete.cs` (`Delete(paths, passes)`, `DefaultPasses`, `BytesOverwritten`) + `Ui/Tools/ToolViews.cs:477-568` |
| 23 | Botao de destruicao no Explorer (handler de menu de contexto) | NAO EXISTE | O menu de contexto do Explorer do Tranal e o de desinstalar (`Core/Topmost/ExplorerContextMenu.cs`); nao ha entrada de destruicao |
| 24 | Software Relocator: mover software instalado entre discos | NAO EXISTE | Grep "Relocat" sem resultado. O Tranal varre drives atras de apps nao registrados (`Core/Discovery/DriveScanner.cs`), nao os move |
| 25 | Bloqueio de popups de notificacao | NAO CONFIRMADO NO BINARIO | Alegacao sem lastro no dump do proprio IObit |
| 27 | Software Updater de programas de terceiros (atualizar todos) | NAO EXISTE | `Core/Updates/AutoUpdateClient.cs:74` (`CheckAsync(feedUrl)`) e o atualizador **do proprio Tranal**, alimentado por um feed; nao ha atualizador de programas de terceiros |
| 28 | Remocao de item fixado na barra de tarefas e de Jump Lists | JA EXISTE | Jump Lists: `Core/Tools/EvidenceRemover.cs` e (`ScanJumpLists`). **Nota**: item fixado na barra de tarefas nao e tratado (grep "Taskbar" sem resultado) |
| 29 | Desinstalar Windows Updates | NAO EXISTE | `Core/Discovery/WindowsFeatures.cs:350/357` so catalogam; nao ha remocao de KB |
| 32 | Verificacao de programas relacionados antes de desinstalar | JA EXISTE | `Core/Discovery/RelatedProgramFinder.cs:46` (`Find`), `Core/UninstallAdvisories.cs` (`RelatedProgramsAdvisory`), aviso no assistente |
| 33 | Aviso de sincronizacao do Chrome ao remover extensao | NAO EXISTE | `Core/Modules/BrowserExtensions.cs` (`Remove`) remove direto, sem aviso de sincronizacao |
| 34 | Integracao com a Central de Acoes do Windows | NAO EXISTE | Nao ha integracao; o mais perto e `Core/Management/SoftwareHealth.cs` (`HealthCategory.Notifications`), que e aviso interno |
| 35 | Gerencia de tarefas agendadas | JA EXISTE | `Core/Management/ScheduledTaskManager.cs:79` (`List`), (`ListFor`), (`Delete`), (Disable/Enable). **Nota**: via `schtasks`, nao via COM Task Scheduler 1.0 |
| 36 | Voto / avaliacao de programas e plugins pela comunidade | NAO EXISTE | Nao ha coleta de votos; ha avaliacao local por estrelas (`Core/Appearance/ProgramRating.cs`, `Core/ProgramCustomization.cs`) |
| 37 | Captura de tela do sistema anexada ao fluxo de suporte | NAO EXISTE | `Core/Appearance/SupportRequest.cs:44/91` monta texto e URL com os dados do programa; grep "screenshot"/"captura"/"Image" sem resultado - nao ha captura de tela |
| 38 | Catalogo embutido de Appx pre-instalados do Windows | NAO EXISTE | Igual a linha 5: nao ha essa lista no codigo |
| 39 | Gerenciador de licenca do proprio produto (ativar PRO) | FORA POR DECISAO | `docs/auditoria/BINARIO.md` 3.2 (produto offline, sem cadastro) e o proprio `docs/auditoria/BINARIO-IOBIT.md` (Observacoes): "a linha 39 (licenca) e a linha 40 (telemetria) nao sao backlog - o Tranal nao comercializa e nao tem telemetria por decisao" |
| 40 | Tela de dados de uso do programa (telemetria) | FORA POR DECISAO | Mesma base (BINARIO.md 3.2: "o Tranal e offline, sem cadastro e sem telemetria") |

---

## 4. Uninstalr

| # | Funcionalidade (o que o concorrente tem) | Estado no Tranal | Evidencia no codigo |
|---|---|---|---|
| 1 | Motor de desinstalacao que dirige a interface de desinstaladores de terceiros | FORA POR DECISAO | `docs/auditoria/MATRIZ.md` F.1 linha 5: "**A parte de dirigir a interface do desinstalador de terceiros (o sentido do Uninstalr) nao foi feita** - exige automacao de interface"; `docs/auditoria/BINARIO.md` 3.2: "**Gerar** um desinstalador silencioso para programa orfao - Exige automacao de interface de desinstaladores de terceiros" |
| 2 | Retomar a desinstalacao depois do reinicio via `RunOnce` | NAO EXISTE | Nada grava `RunOnce` (grep so acha leitura em `Core/Discovery/StartupEntries.cs` e `Core/Tools/AutorunManager.cs`); `Core/DeferredDelete.cs` reagenda so **arquivo**; `Core/Automation/UninstallSession.cs` grava a sessao mas `UninstallSession.Pending` so e consumido em `Tranal.Tests` - nenhum consumidor em Core/Ui/Cli retoma |
| 3 | Deteccao de sobras por MuiCache | NAO EXISTE | Grep "MuiCache" sem resultado em `src/` |
| 4 | Deteccao de sobras por TypeLib | NAO EXISTE | Grep "TypeLib" sem resultado em `src/` |
| 5 | Deteccao de sobras por AppX ModelID (registro, chaves, arquivos e pastas) | NAO EXISTE | Grep "ModelId"/"ModelID" sem resultado. `Core/Modules/WindowsApps.cs` gerencia pacotes MSIX (listar/remover), mas nao varre sobras por ModelID |
| 6 | Pais de origem do app, com bandeira | NAO EXISTE | Grep "country"/"Country" sem resultado em `src/` |
| 7 | Selecionar/desmarcar em massa por pais | NAO EXISTE | Depende da linha 6: nao ha pais de origem no codigo |
| 8 | Deteccao de Chocolatey como plataforma | JA EXISTE | `Core/Discovery/PackageManagerCatalog.cs:248` (`DiscoverChocolatey`), (`Detect`), (`PackageManagerRoots`) |
| 9 | Deteccao de Steam como plataforma | JA EXISTE | `Core/Discovery/PackageManagerCatalog.cs:196` (`DiscoverSteam`), (`ReadSteamLibraryPaths`) |
| 10 | Deteccao de Epic Games como plataforma | NAO EXISTE | "Epic Games" so aparece como nome de pasta conhecido em `Core/Discovery/FolderConfidenceEvaluator.cs` - isso e heuristica de confianca, nao deteccao de plataforma |
| 11 | Deteccao de Big Fish como plataforma | NAO EXISTE | Sem lastro no codigo |
| 12 | Deteccao de NuGet como plataforma | NAO EXISTE | Sem lastro no codigo |
| 13 | Deteccao de PortableApps.com | NAO EXISTE | Sem lastro no codigo |
| 14 | Correspondencia difusa (fuzzy) de elementos e de termos de busca | NAO EXISTE | Grep "Fuzzy"/"Levensh" sem resultado. O filtro e regex exato + termos de exclusao (`Core/Appearance/ProgramFilters.cs:215/228`) |
| 15 | Backup da lista de instalados antes de alterar | JA EXISTE | `Core/FullRegistryBackup.cs`, `Core/UninstallRemovalOptions.cs` (`FullRegistryBackup`), `Core/Settings/ProgramCache.cs` (`ProgramCacheStore`) |
| 16 | Deteccao automatica de caminhos a ignorar (pastas de sistema, backup, `$Recycle.Bin`) | JA EXISTE | `Core/UninstallSupport.cs:397` (`SafetyRules.IsProtectedFolder`); `Core/Discovery/DriveScanner.cs` (`ExcludedFolderNames`, `$Recycle.Bin` em ); `Core/ExcludeList.cs:40/67` |
| 17 | Deep Analysis sob demanda, com modo "sempre profundo" | NAO EXISTE | Nao ha "deep analysis". O que existe e outro: revisao de sobras do instalador (`Core/Automation/AdvancedUninstallReview.cs:56`) e modo forcado (`Core/Automation/ForcedRemovalOptions.cs`) |
| 18 | Reiniciar o computador automaticamente para finalizar a desinstalacao | NAO EXISTE | `Core/Automation/ShutdownGuard.cs:18` faz o **contrario** (impede desligamento durante a tarefa); nao ha reinicio automatico |
| 19 | Remocao automatica de atalhos, pastas e proximos passos ao desinstalar | JA EXISTE | `Core/UninstallSupport.cs:523` (`LeftoverScanner`) apaga atalho obsoleto (`LeftoverReason.ObsoleteShortcut`, ) e Menu Iniciar obsoleto  junto com pastas e registro; `Core/UninstallRemovalOptions.cs` usa a Lixeira |
| 20 | `PreStartupDelete.bat` - script de limpeza que roda no proximo arranque | NAO EXISTE | `Core/Reporting/BatchScriptWriter.cs:35` gera script de **desinstalacao em lote** sob demanda; nao ha script de limpeza agendado para o proximo arranque |
| 21 | Execucao de comandos registrados (`.cmd` reaproveitavel) | JA EXISTE | `Core/ProgramCustomization.cs:291` (`SetCommands`), `Core/Automation/UninstallPlan.cs` (`UninstallPlanOptions.PreCommand`/`PostCommand`), tela `Ui/UninstallCommandsWindow.xaml.cs` |
| 22 | Opcoes de acessibilidade (leitor de tela, moldura padrao, botoes padrao) | NAO EXISTE | Nao ha essas opcoes. WPF ja entrega acessibilidade de sistema; nao ha registro de decisao sobre isso nos docs de decisao |
| 23 | Modo de exibicao do sistema (system / always / never) | NAO EXISTE | Nao ha preferencia nesses 3 estados. O mais perto e incluir/excluir componentes do sistema (`Core/ProgramScanner.cs`, `IncludeSystemComponents`) |
| 24 | Opcoes "speedhacks" para desligar verificacoes lentas | NAO EXISTE | Sem lastro no codigo (nao ha flag equivalente a "desligar verificacoes lentas") |
| 25 | Ignorar programas de usuario e ignorar componentes embutidos como eixos de filtro | JA EXISTE | `Core/ProgramScanner.cs:32` (`IncludeSystemComponents`), `Core/ExcludeList.cs:40/67`, `Core/Appearance/ProgramFilters.cs` (`AddExclusion`). **Nota**: nao ha eixo dedicado "ignorar programas de usuario" (o caminho oposto existe: `Core/Discovery/UserInstallFolders.cs`) |
| 26 | Corrigir o tamanho errado que o Windows mostra na lista | JA EXISTE | `Core/ProgramScanner.cs:42` (`ComputeFolderSize`) + (`MeasureFolder`) - mede a pasta de verdade em vez de confiar no registro |
| 27 | Fechar o proprio aplicativo ao terminar | NAO EXISTE | Sem opcao de auto-fechar no codigo |
| 28 | Salvar posicao da janela e filtros entre sessoes | JA EXISTE | Filtros e preferencias: `Core/Settings/AppSettings.cs:287` (`AppSettingsStore`, `hiddenColumns`, `recentPrograms`), `Core/Settings/ProgramCache.cs`, `Core/Appearance/PreferenceRetention.cs`. **Nota**: **posicao da janela nao e salva** - `AppSettings` nao tem campo de posicao/tamanho |
| 29 | Publisher extraido do manifesto do executavel | NAO EXISTE | Grep "manifest"/"Manifest" sem resultado em `src/`. A editora vem do registro (`Core/ProgramColumns.cs` `Company`) e dos pacotes (`Core/Modules/WindowsApps.cs`) |
| 30 | Atalhos como fonte de descoberta de aplicativos | NAO EXISTE | `Core/ProgramScanner.cs` varre so chave de desinstalacao, catalogo MSI e Store (`Core/ProgramColumns.cs` `ProgramSource`). Atalhos so entram como sobra (`Core/UninstallSupport.cs`) |
| 31 | Varredura apenas do disco do sistema (opcao) | JA EXISTE | `Core/Discovery/DriveScanner.cs:16` (`Roots`), (`Default`, as unidades fixas prontas) - quem escolhe as raizes escolhe o disco. **Nota**: e opt-in, nao e a opcao padrao |
| 32 | Telemetria opt-in com confirmacao explicita | FORA POR DECISAO | `docs/auditoria/BINARIO.md` 3.2: "Base de logs **online** do Revo - Decisao de produto: o Tranal e offline, sem cadastro e sem telemetria" |
| 33 | Licenca Pro embutida no app | FORA POR DECISAO | Mesma decisao de produto (BINARIO.md 3.2) e o registro em `docs/auditoria/BINARIO-IOBIT.md` (Observacoes): o Tranal nao comercializa |
| 34 | Formulario de correcao de dados de entrada e de adicionar app manualmente | JA EXISTE | Edicao: `Core/ProgramCustomization.cs:246/263/306/317`, `Ui/CommentWindow.xaml.cs`, `Ui/RenameWindow.xaml.cs`, `Ui/IconPickerWindow.xaml.cs`. **Nota**: nao ha "adicionar app manualmente" ao catalogo; o caminho equivalente e a desinstalacao forcada por alvo (`Core/Automation/ForcedRemovalOptions.cs`, `Ui/ForcedUninstallWindow.xaml.cs`) |

---

## 5. Resumo por concorrente

| Concorrente | JA EXISTE | NAO EXISTE | FORA POR DECISAO | NAO CONFIRMADO NO BINARIO | Total |
|---|---|---|---|---|---|
| HiBit | 27 | 19 | 4 | 6 | 56 |
| Geek | 17 | 13 | 0 | 0 | 30 |
| IObit | 9 | 15 | 4 | 6 | 34 |
| Uninstalr | 11 | 20 | 3 | 0 | 34 |
| **Total** | **64** | **67** | **11** | **12** | **154** |

## 6. Resumo geral

| Categoria | Linhas | % |
|---|---|---|
| JA EXISTE | 64 | 41,6% |
| NAO EXISTE | 67 | 43,5% |
| FORA POR DECISAO | 11 | 7,1% |
| NAO CONFIRMADO NO BINARIO | 12 | 7,8% |
| **Total** | **154** | **100%** |

Leitura: **41,6%** das alegacoes de lacuna ja estavam cobertas pelo codigo
quando a lista foi escrita - varias delas sao features grandes (monitor de
instalacao, backup e restauracao, gerenciador de tarefas, modo cacador,
destruidor de arquivos, remocao por log do instalador). Das 67 que faltam,
**11 sao limite declarado** (offline, sem driver, sem automacao de interface,
fora do dominio de desinstalacao) e **12 nem podem ser afirmadas** porque o
proprio dump do concorrente nao as comprova.

## 7. Top 10 do que realmente falta (so "NAO EXISTE")

Ordenado por impacto para quem usa um desinstalador - o que um usuario do
Revo notaria faltando.

| # | Falta | Quem pede | Onde mexer | Impacto |
|---|---|---|---|---|
| 1 | **Desinstalar atualizacoes do Windows (KB/wusa)** - o catalogo existe, a remocao nao | HiBit 21, IObit 29 | `Core/Discovery/WindowsFeatures.cs` (falta o comando de remocao) | alto - e o pedido mais comum de quem nao quer patch no Windows |
| 2 | **Retomar a desinstalacao depois do reinicio** - nada reagenda a operacao; so o arquivo em uso e adiado | HiBit 4, Uninstalr 2 | `Core/Automation/UninstallSession.cs` (falta o consumidor de `Pending` e o agendamento) | alto - hoje a sessao e gravada e nunca retomada |
| 3 | **Criar servico, criar tarefa agendada, criar item de inicializacao** pela interface | HiBit 67/68/70/91/92/93 | `Core/Management/ServiceManager.cs:113`, `Core/Management/ScheduledTaskManager.cs:79`, `Core/Tools/AutorunManager.cs` | alto - o Tranal gerencia o que existe, nao cria (o `Add` do Autorun nem aparece no painel) |
| 4 | **Desregistro de COM (`regsvr32 /u`) antes de remover** | Geek 4 | `Core/Automation/UninstallPlan.cs` (etapa nova) | alto - deixa servers COM registradas no sistema apos a remocao |
| 5 | **Varredura de sobras por MUI cache, TypeLib e AppX ModelID** | Uninstalr 3/4/5 | `Core/UninstallSupport.cs` (novos `LeftoverReason`) | alto - sao tres das sobras que mais sobram no Windows moderno |
| 6 | **Limpador de registro com as 15+ categorias** (ActiveX, FileTypes, SharedDLLs, IFEO, MUICache) | HiBit 77 | `Core/Tools/RegistryOptimizer.cs` | alto - o `RegistryOptimizer` hoje so limpa entrada obsoleta do Menu Iniciar |
| 7 | **Relatorio HTML por ferramenta** (servicos, drivers, tarefas, inicializacao, menu de contexto) | HiBit 8 | `Core/Reporting/` | medio - a exportacao HTML existe, mas so da lista e do log de instalacao |
| 8 | **Desinstalacao forcada em lote** e **remocao em lote so de entradas de registro** | Geek 2/3, HiBit 3/18 | `Core/Automation/UninstallQueue.cs`, `Core/Management/WindowsListEntry.cs`, `Core/RegistryEditing/RegistryBatchRemover.cs` | fechado - a desinstalacao forcada em lote e a remocao em lote das entradas da lista do Windows existem, com acao na tela (`CmdRemoveEntriesBatch`) e na linha de comando (`/remove-entries`) |
| 9 | **Navegadores alem de Chrome/Edge/Firefox/Opera** (Brave, Vivaldi, Yandex) e **plugins NPAPI/ActiveX** | HiBit 73, IObit 4 | `Core/Modules/BrowserExtensions.cs:76-80` | medio - Brave e Vivaldi sao os navegadores que mais acumulam extension |
| 10 | **Painel de processos** (listagem, propriedades, afinidade de CPU) | HiBit 69/87/88 | novo modulo + `Core/ProcessTerminator.cs` | medio - hoje so existe "encerrar processo", nao "ver processos" |

Mencoes honrosas (proximo do corte): `Bundleware` e `Bloatware Remover`
(IObit 5/6/38 - sem lastro no codigo e sem decisao registrada), `Software
Relocator` (IObit 24), `catalogo de Appx pre-instalados` (IObit 38),
`reparador de atalhos` (HiBit 80), `fuzzy/Levenshtein` (Geek 11, Uninstalr 14),
`pais de origem` (Uninstalr 6/7), `detector de arquivos duplicados` como
ferramenta propria (HiBit 83 - hoje e categoria do Software Health),
`dialogo de falha de desinstalacao` (Geek 18) e `painel de arquivos em uso`
(Geek 15).

## 8. Limites

1. **Nao ha build nem execucao nesta apuracao.** O veredito e de leitura de
   codigo (grep + leitura de trecho). Comandos de CLI, telas e nomes de
   simbolo foram conferidos no fonte, mas **nao** rodados. Um item marcado
   "JA EXISTE" pelo simbolo pode ter o caminho de UI quebrado - o que nao
   invalida a existencia da capacidade no nucleo, mas invalida a promessa de
   que ela esta visivel para o usuario.
2. **12 linhas sao "NAO CONFIRMADO NO BINARIO"** (HiBit 3/9/60/62/63/64,
   IObit 9/10/11/16/21/25). O documento de origem ja as marca como nao
   confirmadas; classifiquei assim **mesmo quando o Tranal tem o recurso**
  (ex.: o lote existe no Tranal, mas a alegacao do HiBit nao tem lastro no
   dump do HiBit). Se o dono preferir, elas podem ser reclassificadas por
   evidencia do Tranal - a lista esta no texto das tabelas.
3. **Duas "FORA POR DECISAO" sao interpretacao minha, nao citacao literal:**
   HiBit 89 (VirusTotal) e HiBit 90 (Windows Defender). A decisao registrada
  (BINARIO.md 3.2) fala em "logs online", "sem cadastro e sem telemetria" e
   em base de reputacao de extensoes; **nao nomeia** o VirusTotal nem o
   Defender. Classifiquei como fora por decisao porque as duas exigem servico
   online, que e exatamente o que a decisao nega. O dono pode querer
   rebaixar as duas para "NAO EXISTE" - o efeito no total seria: FORA 11 -> 9,
   NAO EXISTE 67 -> 69.
4. **Onze linhas "JA EXISTE" tem cobertura parcial** e isso esta escrito na
   coluna de evidencia (local de instalacao sem coluna, 4 temas em vez de 7,
   8 categorias de lixo em vez de 17, Jump Lists sem item fixado, relatorio
   HTML so da lista, filtros sem posicao de janela, etc.). A coluna "Estado"
   responde "a capacidade existe?"; a coluna "Evidencia" responde "ate onde?".
5. **Nao conferi a MATRIZ.md linha a linha** (so a secao E.1 e o item F.1 #5,
   que citei). O pedido era reconciliar os 4 concorrentes contra o codigo; a
   MATRIZ nao foi objeto.
6. **Nao toquei em `E:\Projetos\tranal-uninstaller`**, nao rodei git, nao
   alterei nenhum arquivo de projeto. Este relatorio e o unico arquivo
   criado, na copia `waves2/w1`.
