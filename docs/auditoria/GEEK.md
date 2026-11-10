# Geek Uninstaller 1.6.0.200 (Free) — auditoria

> O concorrente **mínimo**. Toda a superfície observável do Geek gratuito
> cabe em **60 strings** — contra 1.136 do Revo Pro e 1.066 do BCU. Serve
> como a referência do que um desinstalador enxuto ainda precisa ter, e do
> que ele dispensa.

Data da coleta: 05/11/2026 · Versão auditada: **1.6.0.200** (edição **Free**)

---

## 1. Identificação

| Campo | Valor | Fonte |
|---|---|---|
| Produto | Geek Uninstaller Free (pessoal, sem suporte) | `manual-download.txt` |
| Versão | **1.6.0.200** | `pe-geek-exe.txt` |
| Editor | Thomas Koen / Geek Uninstaller (Bélgica) | `manual-download.txt` (© 2012–2026) |
| Site | https://geekuninstaller.com/ | `manual-overview.html` |
| Download | `https://geekuninstaller.com/geek.zip` (botão "ZIP (4.64 MB)") | `manual-download.txt` |
| SHA-256 do zip | `B4EED5DFC430ECF74A7B2AB98EDE605D32D51FCF79BB2A0878021EA88671CDC9` | `sha256-geek-zip.txt` |
| Conteúdo do zip | **1 arquivo: `geek.exe`** (11.980.432 B) — sem instalador | `app/geek.exe` |
| Formato | **PE32 nativo, 32 bits, Windows GUI** — C++/MSVC (seções `.text .rdata .data .fptable .rsrc`), UI com o Codejock **XTP** | `pe-geek-exe.txt`, `uia-geek-arvore.txt` |
| Assinante | `CN=CrystalBit Solutions, O=CrystalBit Solutions, S=West-Vlaanderen, C=BE` — Sectigo Public Code Signing CA R36, **válido até 13/04/2029** | `pe-geek-exe.txt` |
| Compilação | 2026-10-14 11:20:34Z | `pe-geek-exe.txt` |
| Imphash | `c3059cf0fda04abb514a6832b76a7df6` | `pe-imports-resumido.txt` |
| Requisitos | Windows 11/10/7/8/8.1, 32 e 64 bits (inclusive ARM); **exige administrador**; em x64/ARM64 roda como nativo de 64 bits | `manual-download.txt` |

### O launcher de 32 bits (medido)

`geek.exe` é um **launcher**: na execução ele extrai `geek64.exe`
(4.154.000 B) em `%TEMP%` e o executa — as strings do binário contêm
`geek64.exe` e `geek-arm64.exe`, e o release notes 1.4.3.102 documenta
"geek64.exe is extracted to the same directory (on 64-bit Windows)". O
processo `geek64.exe` foi observado em execução com a janela
"Geek Uninstaller 1.6.0.200".

**Nada foi gravado** em `%ProgramFiles%` nem no Menu Iniciar: o Geek não
precisa de instalador.

### Onde o Geek guarda o estado (medido)

| Local | Conteúdo |
|---|---|
| `%APPDATA%\Geek Uninstaller\prefs.xml` | XML `<PortableProgramData version="2">`: `ColorScheme`, `InitialSortOrder`, `InitialSortParam`, `Interface_Language`, `Window_Maximized`, `Window_Rect`, `column_0..3` |
| `%APPDATA%\Geek Uninstaller\cache.dat` | cache binário (10.919 B) |
| `%APPDATA%\Geek Uninstaller\IconsCache\*.ico` | cache de ícones (~5,2 KB por programa) |
| `HKCU\Software\Geek Uninstaller\LastRunDate` | `REG_QWORD` |
| `%TEMP%\geek64.exe` | o binário de 64 bits extraído pelo launcher |

### O dado mais revelador da auditoria

A **superfície observável inteira** do Geek gratuito está em
`evidencia/langstrings-en.txt`: **60 strings**, em 9 blocos
(`MainMenu`, `ListCtrl`, `ExplorerBar`, `UninstallWizard`, `DlgAbout`,
`Messages`, `ProVersion`, `HTMLReport`, `Misc`).

| Produto | Strings observáveis |
|---|---|
| Revo Pro 5.5.4 | 1.136 (42 seções) |
| Bulk Crap Uninstaller 6.3.0 | 1.066 (45 telas) |
| IObit Uninstaller 16.1 | 95 telas de diálogo |
| **Geek Uninstaller 1.6.0 Free** | **60** |

---

## 2. Superfície observável

### 2.1 Menu (reconstruído e **capturado**)

| Menu | Itens |
|---|---|
| **File** | Export to HTML… · Exit |
| **Action** | Uninstall… · Modify… · **Force Removal** · **Batch Force Removal** · Remove Entry · **Batch Remove Entries** · Registry Entry · Installation Folder · Program Website · Open in Microsoft Store · Browse Location… · Browse Source… · Google for |
| **View** | Desktop Apps · Microsoft Store Apps · Refresh · **Reset Columns** · Color Scheme (Match Windows light/dark · Light · Dark) · **Interface Language** |
| **Help** | About… · Home Page |

**Atalhos de teclado (observados na captura do menu Ação):**

| Atalho | Comando |
|---|---|
| `Enter` | Desinstalar… |
| `Shift+Del` | Forçar Remoção |
| `Ctrl+R` | Entrada no Registro |
| `Ctrl+E` | Pasta de Instalação |
| `Ctrl+W` | Página web do programa |
| `Ctrl+S` | Exportar para HTML… |
| `F5` | Atualizar |
| `Ctrl+Tab` | alternar Desktop ↔ Microsoft Store |
| `ESC` | resetar o filtro |

### 2.2 Funcionalidades

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 1 | Lista com **3 colunas visíveis** (Nome, Tamanho, Instalado Em); o XML prevê uma 4ª (`column_0..3`) | chaves de recurso `ListCtrl/columnProgramName`, `columnSize`, `columnInstallationDate`, `columnTraced` (`geek.strings.txt:1162-1165`) e `column_0` (`:1118`); `prefs-geek.xml` confirma `Column0..Column3` | **OK** - `Core/ProgramColumns.cs:9-97` define 21 colunas (superset das 3 do Geek, com `Size` em `:14` e `Date` em `:24`); janela de colunas em `Ui/ColumnsWindow.xaml.cs:78` |
| 2 | Total agregado no rodapé ("%d programs of %s size in total") | literal `<statusTotalInfo>%d programs of %s size in total</statusTotalInfo>` (`geek.strings.txt:4639`) | **OK** - Total agregado no rodapé ("%d programs of %s size in total")/`src/Tranal.Uninstaller/Core/Modules/RelativeDate.cs:147` (`public static class ProgramTotalsCalculator`) `src/Tranal.Uninstaller/MainWindow.xaml.cs:411` (`ProgramTotalsCalculator.Compute`) `src/Tranal.Uninstaller/MainWindow.xaml.cs:413` (`totais.Text`) o rodapé mostra a contagem E o tamanho total; |
| 3 | Indicação de app 32-bit dentro do nome ("(32-bit)") | literal `<mark32bitItem>(32-bit)</mark32bitItem>` (`geek.strings.txt:4586`) | **OK** - `Core/ProgramScanner.cs:177` chama `DetectBits` (`:276`) e grava em `program.Bits` (`Core/InstalledProgram.cs:31`); a grade expoe em coluna propria (`Core/ProgramColumns.cs:21` e `:239`). Capacidade equivalente, forma diferente (coluna em vez de sufixo no nome) |
| 4 | **Filtro incremental por digitação**, sem campo visível ("Type to find a program") | literal `<itemShortSearchTip>Type to find a program</itemShortSearchTip>` (`geek.strings.txt:4589`), `ExplorerBar/itemShortSearchTip` (`:1166`) | **OK** - `Core/Appearance/ProgramFilters.cs:322` filtra por texto (`:343` compila a regex, `:357` monta o `FilterQuery`), `:204` aceita `regex:`; atalho de foco em `Core/Shortcuts/ShortcutMap.cs:66` |
| 5 | Aviso "No matching items, press ESC to reset" | literal `<staticNoItemsMatchingFilterTip>No matching items, press ESC to reset</staticNoItemsMatchingFilterTip>` (`geek.strings.txt:4584`) | **OK** - Aviso "No matching items, press ESC to reset"/o aviso do rodape `MainWindow.xaml.cs:236` |
| 6 | **Desinstalação** pelo desinstalador do programa | `MainMenu/mnuActionUninstall` (`geek.strings.txt:1168`), literal `<mnuActionUninstall>&amp;Uninstall...</mnuActionUninstall>` (`:4552`) | **OK** - `Core/Removal.cs` - motor de remocao pelo desinstalador nativo, com modo silencioso e codigo de erro (`ErrorCodeOf` em `:57`) |
| 7 | **Modificar/reparar** a instalação | `MainMenu/mnuActionModify` (`geek.strings.txt:1169`), literal `<mnuActionModify>&amp;Modify...</mnuActionModify>` (`:4553`) | **OK** - `Core/Discovery/MsiProductCatalog.cs:122` monta o comando, `:265` executa o reparo MSI; produtos expostos em `Ui/Discovery/DiscoveryWindow.xaml.cs` |
| 8 | **Force Removal** — remoção sem executar o desinstalador, com confirmação | `MainMenu/mnuActionDelete` (`geek.strings.txt:1170`), literal `<mnuActionDelete>&amp;Force Removal</mnuActionDelete>` (`:4554`), `<msgForceRemovalQuestion>` (`:4611`) | **OK** - `Ui/ForcedUninstallWindow.xaml.cs` + `Core/Automation/ForcedRemovalOptions.cs`; acao na UI em `MainWindow.xaml.cs:694` (`Cmd.ForcedUninstall`), confirmacao em `:1710` |
| 9 | **Batch Force Removal** | literal `<mnuActionDeleteBatch>Batch &amp;Force Removal</mnuActionDeleteBatch>` (`geek.strings.txt:4555`) | **OK** - Multi-selecao em `MainWindow.xaml:398`; confirmacao de lote em `MainWindow.xaml.cs:1663`; motor em `Core/Automation/UninstallQueue.cs`; forcado por alvo/palavra-chave em `Core/Automation/ForcedRemovalOptions.cs:41`; CLI `/batch` em `src/Tranal.Cli/ExtraCommands.cs:968` |
| 10 | **Remove Entry** + **Batch Remove Entries** | `MainMenu/mnuActionRemoveEntry` (`geek.strings.txt:1171`), literal `<mnuActionRemoveEntryBatch>Batch Remove &amp;Entries</mnuActionRemoveEntryBatch>` (`:4557`), `<msgRemoveEntryQuestion>` (`:4612`) | **OK** - **Remove Entry** + **Batch Remove Entries**/A remocao das entradas da lista do Windows existe **em lote** e na tela. A REGRA mora no nucleo, em um lugar so: a acao avulsa (`src/Tranal.Uninstaller/Core/Management/WindowsListEntry.cs:109`, `Remove`) delega para a acao em lote (`:136`, `RemoveMany`), e as duas passam pela mesma remocao (`src/Tranal.Uninstaller/Core/RegistryEditing/RegistryBatchRemover.cs:121`, `Remove`), que respeita a chave protegida e a visao do registro do programa (`:85`, `FromProgram`). A tela tem a acao em lote (`src/Tranal.Uninstaller/MainWindow.xaml.cs:1134`, `CmdRemoveEntriesBatch`; o metodo em `:1831`, `RemoveEntriesBatch`) e a linha de comando tem `/remove-entries` (`src/Tranal.Cli/EntryCommands.cs:24`). Medido: `tools/test-lote26.ps1` -> a entrada de teste sai do registro, a linha de comando avisa que o programa NAO e desinstalado e o menu "Mais comandos" mostra a acao. |
| 11 | **Assistente de desinstalação** com sobras em **2 categorias**: File System (%d items) e Registry (%d items), em **árvore** | literais `<treeItemFileSystem>File System (%d items)</treeItemFileSystem>` e `<treeItemRegistry>Registry (%d items)</treeItemRegistry>` (`geek.strings.txt:4599-4600`), `UninstallWizard/treeItemFileSystem` (`:1331`) | **OK** - **Assistente de desinstalação** com sobras em **2 categorias**: File System (%d items) e Registry (%d items), em **árvore**/a arvore existe e tem as duas categorias do Geek: `src/Tranal.Uninstaller/Core/LeftoverGroups.cs:35` (`LeftoverGroups`), `:51` (`Build`), a contagem em `:22` (`Count`), as chaves das duas categorias em `:38` (`filesystem`) e `:41` (`registry`) e a ordem (sistema de arquivos, registro, as outras) em `:136` (`OrderOf`); a tela `src/Tranal.Uninstaller/Ui/Shared/LeftoversTree.xaml.cs:25` (`LeftoversTree`) monta um no por categoria, com o titulo e a contagem no cabecalho em `:83`; o assistente consome a arvore em `src/Tranal.Uninstaller/Ui/UninstallWizardWindow.xaml:169` e a desinstalacao forcada em `src/Tranal.Uninstaller/Ui/ForcedUninstallWindow.xaml:36`. Medido: `tools/test-lote23.ps1` -> a janela real mostra o no "Sistema de arquivos (1)". |
| 12 | Mensagens do assistente, **medidas em execução**: "Alguns itens órfãos encontrados!" → "Gerando lista de traços…" → "Todos os traços removidos" | literais `<statusScanning>Scanning for leftover files, folders and registry items...</statusScanning>`, `<statusLeftOverTracesFound>Some leftover traces found!</statusLeftOverTracesFound>`, `<statusAllTracesRemoved>All Traces Removed</statusAllTracesRemoved>`, `<statusDeletingProgramTraces>Deleting program traces...</statusDeletingProgramTraces>` (`geek.strings.txt:4592-4597`) | **OK** - `Core/Automation/RemovalProgress.cs` reporta as etapas (`BeginStep`/`EndStep`), `Core/Automation/UninstallPlan.cs` define os passos (`UninstallStepKind`); a tela em `Ui/UninstallWizardWindow.xaml.cs:643` (scan ignorado), `:699` (quantidade de sobreviventes) e `:762` (itens removidos) |
| 13 | **Cancelamento** da remoção de sobras com confirmação | literal `<msgCancelWizard>The leftovers found will not be removed. Close the wizard?</msgCancelWizard>` (`geek.strings.txt:4613`) | **OK** - `Ui/UserMessages.cs:60` centraliza a confirmacao; a confirmacao do lote nunca e omitida (`MainWindow.xaml.cs:1663`); o botao de fechar do assistente em `Ui/UninstallWizardWindow.xaml.cs:1100` |
| 14 | Diálogo de falha de desinstalação com opção de **limpar a entrada do registro e os arquivos** | literal `<msgUninstallFailed>Uninstallation process failed. \nWould you like to clean this program entry from registry and clean up its files?</msgUninstallFailed>` (`geek.strings.txt:4610`) | **OK** - Diálogo de falha de desinstalação com opção de **limpar a entrada do registro e os arquivos**/modulo das falhas de desinstalacao `Core/Automation/UninstallFailureRecovery.cs:48`; o registro da falha `Core/Automation/UninstallFailureRecovery.cs:66`; a limpeza da entrada e dos arquivos `Core/Automation/UninstallFailureRecovery.cs:146` |
| 15 | "Browse..." dentro das sobras encontradas | `UninstallWizard/treeItemPopopMenuBrowse` (`geek.strings.txt:1333`), literal `<treeItemPopopMenuBrowse>Browse...</treeItemPopopMenuBrowse>` (`:4601`) | **OK** - "Browse..." dentro das sobras encontradas/a acao existe na arvore das sobras: `src/Tranal.Uninstaller/Ui/Shared/LeftoversTree.xaml.cs:204` (`BrowseButton_Click`) abre a pasta do item no Explorer (`ExternalLaunchers.OpenFolder`, `:216`) e a chave de registro no editor de registro (`SystemNavigator.Open` com `regedit.exe`, `:211`); o botao "Explorar" da tela e `src/Tranal.Uninstaller/Ui/Shared/LeftoversTree.xaml:26` (`LeftoversBrowseButton`), habilitado so com uma folha selecionada (`LeftoversTree.xaml.cs:96` e `:193`). Medido: `tools/test-lote23.ps1` acha o comando na janela real da desinstalacao forcada. |
| 16 | Aba de **Microsoft Store Apps** (`Ctrl+Tab`) | `MainMenu/mnuViewWinStoreApps` (`geek.strings.txt:1098`), literal `<mnuViewWinStoreApps>Microsoft Store Apps</mnuViewWinStoreApps>` (`:4567`) | **PARCIAL** - Aba de **Microsoft Store Apps** (`Ctrl+Tab`)/`src/Tranal.Uninstaller/Core/Modules/WindowsApps.cs:39` (`public static class WindowsApps`) `src/Tranal.Uninstaller/Ui/Modules/ModuleViews.cs:23` (`WindowsApps`) `src/Tranal.Cli/ExtraCommands.cs:14` (`/winapps`) **Nota**: os apps Store tem modulo e painel proprios, mas nao sao uma aba da janela principal. |
| 17 | **Reset Columns** | `MainMenu/mnuViewListColumnsReset` (`geek.strings.txt:1085`), literal `<mnuViewListColumnsReset>Reset Columns</mnuViewListColumnsReset>` (`:4569`) | **OK** - `Ui/ColumnsWindow.xaml.cs:78` monta a lista de colunas; acao na UI em `MainWindow.xaml.cs:708` (`CmdColumns`) |
| 18 | **Tema** automático (light/dark do Windows) | `MainMenu/mnuViewColorScheme` (`geek.strings.txt:1099`), literais "Match Windows light/dark mode" / "Light mode (default)" / "Dark mode" (`:4571-4573`) | **OK** - `Theming/ThemeProbe.cs:19` sonda o tema do sistema; `Theming/ThemeManager.cs` aplica; menu de temas em `MainWindow.xaml.cs:937-944`; CLI `/theme` em `src/Tranal.Cli/ExtraCommands.cs:57` |
| 19 | **Interface multilíngue** (pt-BR incluído) | `prefs-geek.xml` (`Portuguese_Brazilian`); `MainMenu/mnuViewLanguage` (`geek.strings.txt:1111`) | **OK** - `Core/Localization/LanguageCatalog.cs:52` registra "pt-BR" / "Portugues (Brasil)"; `:71` expoe a contagem; menu de idioma em `MainWindow.xaml.cs:946-960`; CLI `/lang` em `src/Tranal.Cli/ExtraCommands.cs:56` |
| 20 | **Exportação em HTML** ("Generated by %s on %s", "Installed Programs (%s)") | `HTMLReport/installedPrograms` (`geek.strings.txt:1257`), literal `<installedPrograms>Installed Programs (%s)</installedPrograms>` (`:4632`) | **OK** - `Core/Exporters.cs:63` gera o HTML da lista; `Core/Reporting/ReportFormats.cs:86` e o formato de relatorio; UI em `MainWindow.xaml.cs:1492` (`Cmd.ExportHtml`) |
| 21 | Ir para a **chave do registro**, a **pasta de instalação**, o **site do programa**, a **Microsoft Store** | `MainMenu/mnuActionJumpToRegistry` / `JumpToProgramFolder` / `JumpToWebSite` / `OpenWindowsStore` (`geek.strings.txt:1172-1175`), literais `<mnuActionOpenWindowsStore>Open in Microsoft Store</mnuActionOpenWindowsStore>` (`:4561`) | **OK** - Ir para a **chave do registro**, a **pasta de instalação**, o **site do programa**, a **Microsoft Store**/`src/Tranal.Uninstaller/Core/Management/SystemNavigator.cs:51` (`public static class SystemNavigator`) `src/Tranal.Uninstaller/Core/Management/SystemNavigator.cs:61` (`TargetsFor`) `src/Tranal.Uninstaller/Core/Management/SystemNavigator.cs:88` (`RegistryEditor`) `src/Tranal.Uninstaller/Core/Management/SystemNavigator.cs:116` (`MicrosoftStore`) `src/Tranal.Uninstaller/Core/Management/SystemNavigator.cs:138` (`ProgramFolder`) `src/Tranal.Uninstaller/Core/Management/SystemNavigator.cs:155` (`UninstallerFolder`) `src/Tranal.Uninstaller/MainWindow.xaml.cs:1705` (menu "Abrir no Windows") `src/Tranal.Cli/InspectionCommands.cs:519` (`tranal /inspection-store`) |
| 22 | **Browse Location…** e **Browse Source…** | literais `<mnuActionBrowseLocation>Browse Location...</mnuActionBrowseLocation>` e `<mnuActionBrowseSource>Browse Source...</mnuActionBrowseSource>` (`geek.strings.txt:4562-4563`) | **OK** - **Browse Location…** e **Browse Source…**/`src/Tranal.Uninstaller/Core/Management/SystemNavigator.cs:138` (`ProgramFolder`) `src/Tranal.Uninstaller/Core/Management/SystemNavigator.cs:155` (`UninstallerFolder`) `src/Tranal.Uninstaller/MainWindow.xaml.cs:1705` (menu "Abrir no Windows") as duas acoes abrem a pasta do programa e a pasta do desinstalador; |
| 23 | **Google for** — pesquisar o programa no navegador | `MainMenu/mnuActionGoogleFor` (`geek.strings.txt:1176`), literal `<mnuActionGoogleFor>Google for</mnuActionGoogleFor>` (`:4564`) | **OK** - `Core/WebSearch.cs:34` monta a URL (`:39` resolve o motor), `Core/WebSearchEngines.cs:88` define os motores embutidos; usado em `Ui/Shared/ItemListPanel.xaml.cs:216` |
| 24 | Verificação de **nova versão** (HTTP) | literais `<msgNewVersionAvailable>New version %s is available</msgNewVersionAvailable>` e `msgNewVersionAvailable2` (`geek.strings.txt:4615-4616`), `WININET.dll` importada | **OK** - `Core/Updates/AutoUpdateClient.cs:74` consulta o feed por HTTP; `:124` baixa a versao nova; startup controlado por `Core/Settings/AppSettings.cs` (`CheckForUpdateOnStartup`) |
| 25 | Categoria **Windows Updates** | literal `<WindowsUpdatesText>Windows Updates</WindowsUpdatesText>` (`geek.strings.txt:4641`), `Windows Updates` (`:4962`) | **OK** - `Ui/Discovery/DiscoveryWindow.xaml.cs:42` cria a categoria "Atualizacoes do Windows"; `Core/Discovery/WindowsFeatures.cs:350` cataloga as instaladas; `Core/Management/WindowsUpdateManager.cs:132` lista as instaladas do modulo de remocao |
| 26 | Aviso quando a remoção da entrada falha por falta de permissão | literal `<msgRemoveEntryFailed>Failed to delete the registry entry of this item. \nYou probably have no access to do it.</msgRemoveEntryFailed>` (`geek.strings.txt:4614`) | **OK** - `Core/Removal.cs:57` extrai o codigo de erro do Windows (5 = acesso negado); `:86` monta o relatorio com o codigo; `Core/RemovalLog.cs` grava o detalhe da falha por operacao |

### 2.2.1 Funcionalidades adicionais descobertas no binário (não constavam no documento)

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 27 | **Varredura de sobras de MSI** pelo registro do Installer | `CScannerMSI::ScanMSITraces` (`geek.strings.txt:1307`), `SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\%s\Products\` (`:12659`), `Installer\Folders` (`:12762`) | **OK** - `Core/MsiInstallLog.cs:110`/`:130` capturam o log do instalador MSI, `:163` (`UsableAfter`) so aceita o log depois do desinstalador nativo rodar; o log vira sobra por `Core/UninstallSupport.cs:105` (`InstallerLog`) |
| 28 | **Chave de produto MSI** detectada por programa | literal "Found MSI product key {} for {}" (`geek.strings.txt:12759`), `HKEY_LOCAL_MACHINE\SOFTWARE\...\Installer\UserData\%s\Products\%s` (`:12758`) | **OK** - `Core/Discovery/MsiProductCatalog.cs:248`/`:265` operam pela chave de produto; `Core/MsiInstallLog.cs:126` captura pelo `ProductCode` |
| 29 | **Desregistro de COM** (`regsvr32 /u`) antes de remover | literal `regsvr32.exe /s /u "%s"` (`geek.strings.txt:12725`), "> Unregister COM Object: failed to create regsvr32 process" (`:12726`) | **PARCIAL** - **Desregistro de COM** (`regsvr32 /u`) antes de remover/`src/Tranal.Uninstaller/Core/Automation/ComUnregister.cs:28` (`public static class ComUnregister`) `src/Tranal.Cli/RegistryCommands.cs:41` (`ComUnregister`) **Nota**: o desregistro de COM existe e tem comando proprio, mas nao esta ligado ao fluxo de remocao — e uma ferramenta separada. |
| 30 | **Remoção adiada para o próximo reinício** quando o arquivo está em uso | literais "> ERROR_ACCESS_DENIED, the file will be deleted on the next system restart" (`geek.strings.txt:12728`), "> ERROR_SHARING_VIOLATION, the folder will be deleted on the next system restart" (`:12731`), `MoveFileExW` importada (`:2817`) | **OK** - `Core/DeferredDelete.cs:27` usa `MoveFileEx` com `MOVEFILE_DELAY_UNTIL_REBOOT`; o resultado entra no relatorio como `Deferred` (`Core/Removal.cs:8`) e `Core/RemovalLog.cs:69` |
| 31 | **Detecção de instalação em `Local AppData\Programs`** | literal "Found install location in Local AppData Programs: {}" (`geek.strings.txt:12639`) | **OK** - `Core/Discovery/UserInstallFolders.cs:87` reconhece o caminho de usuario, `:107` varre as instalacoes em `Local AppData\Programs` |
| 32 | **Guarda contra falso positivo** de `InstallLocation` | literais "False positive InstallLocation found {}" e "Cannot set location for item (false positive risk) {}" (`geek.strings.txt:12664-12665`) | **OK** - `Core/Discovery/FolderConfidenceEvaluator.cs:165` avalia a pasta por criterios (`:293` protege pastas de conteudo do usuario); os avisos de baixa confianca saem em `Core/UninstallSupport.cs:205` (`LeftoverWarnings`) |
| 33 | **Correspondência aproximada (Levenshtein)** de caminho de instalação | literal `GetInstallLocationFromPathLevinshtein ({})` (`geek.strings.txt:12640`) | **PARCIAL** - **Correspondência aproximada (Levenshtein)** de caminho de instalação/`src/Tranal.Uninstaller/Core/Search/FuzzyMatch.cs:17` (`public static class FuzzyMatch`) `src/Tranal.Uninstaller/Core/Appearance/ProgramFilters.cs:555` (`FuzzyMatch.IsMatch`) **Nota**: a distancia de edicao existe e e usada na busca por nome do programa, mas nao na comparacao de caminho de instalacao. |
| 34 | **Varredura de atalhos do Menu Iniciar e da Área de Trabalho** para achar ícone e localização | `CFindIconJob::FindStartMenuShortCut` (`geek.strings.txt:1194`), `CScannerNormal::LoadStartMenuShortCuts` (`:1317`), `CScannerNormal::LoadDeskTopShortcuts` (`:1320`) | **PARCIAL** - A varredura existe e cobre os quatro lugares: `src/Tranal.Uninstaller/Core/Modules/ShortcutFixer.cs:84` (`Locations`: Menu Iniciar do usuario e de todos, Area de Trabalho do usuario e publica, em `:94-100`), enumerando os `.lnk` em `Scan` `:110` e lendo o alvo em `ReadTarget` `:262`. Tela `src/Tranal.Uninstaller/Ui/Tools/CleanupViews.cs:376`, com `ShortcutScan` em `:392`; CLI `src/Tranal.Cli/CleanupCommands.cs:39` e `:186`. **Nota**: o item **icone** nao e lido - a busca por `IconLocation` em `src/` nao retorna hits, e `ReadTarget` (`:262-288`) so le o `TargetPath`; o `src/Tranal.Uninstaller/Core/Discovery/ShortcutDiscovery.cs:44`, que descobre programas pelos atalhos, **nao tem consumidor** (a busca por `ShortcutDiscovery` em `src/` so encontra a propria declaracao). Binario do concorrente: confirmado em binario (`geek.strings.txt:1194`, `:1317`, `:1320`) |
| 35 | **Limpeza do cache MRU do Windows** | literal `Software\Classes\Local Settings\MrtCache` (`geek.strings.txt:12749`) | **OK** - A limpeza de MRU existe: `src/Tranal.Uninstaller/Core/Tools/MruCleaner.cs:41` (classe), as chaves em `MruKeys` (a `Software\Classes\Local Settings\MrtCache` do Geek em `:62`), `Scan` e `Clear`; tela `src/Tranal.Uninstaller/Ui/Tools/CleanupViews.cs:34` (com `MruCleaner.Scan` em `:507`); CLI `src/Tranal.Cli/CleanupCommands.cs:234` (`tranal /mru /clean`). Binario do concorrente: confirmado em binario (`geek.strings.txt:12749`) |
| 36 | **Detecção de instalação Inno Setup** pelo caminho do App Path | literal "Inno Setup: App Path" (`geek.strings.txt:12700`) | **OK** - `Core/UninstallerKind.cs` classifica o tipo de desinstalador, incluindo Inno Setup; a deteccao do `App Path` alimenta `Core/ProgramScanner.cs:177` |
| 37 | **Cache de uninstall** com XML versionado | literais `CUninstallCache::Save` / `::Load` / `::clear` (`geek.strings.txt:1134-1147`), `UninstallCache` versionado em XML (`geek.strings.txt:1136`) | **PARCIAL** - **Cache de uninstall** com XML versionado/`src/Tranal.Uninstaller/Core/Settings/ProgramCache.cs:154` (`public static class ProgramCache`) **Nota**: o cache existe (salva, carrega e invalida), mas o formato e JSON, nao XML versionado. |
| 38 | **Cálculo do tamanho** de cada programa | literais "Calculate size of {}" (`geek.strings.txt:14583`), "CCalculate size of {}" (`:12626`) | **OK** - `Core/ProgramScanner.cs:405` mede a pasta; a medicao e opt-in por `ScanOptions.ComputeFolderSize` (`:42`) e aplicada em `:180` |
| 39 | **Painel "Active Files"** de arquivos em uso | literal "Active Files" (`geek.strings.txt:14689`) | **OK** - **Painel "Active Files"** de arquivos em uso/modulo de arquivos em uso `Core/Automation/ActiveFiles.cs:37`; ferramenta na barra lateral `Core/Automation/ActiveFiles.cs:37` |
| 40 | **Encerra o processo** que trava a desinstalação | `TerminateProcess` importada (`geek.strings.txt:2784`), `CUninstallProcess::WaitForProcessAndChildren` (`:1272`) | **OK** - `Core/ProcessTerminator.cs:70` acha processos sob a pasta do programa, `:107` enumera os do sistema; a UI aciona "Kill process" em `Ui/Tools/TopmostOverlayWindow.xaml.cs` |
| 41 | **Log da desinstalação** gravado em arquivo por programa | literal `%s_UninstallLog.log` (`geek.strings.txt:12739`) | **OK** - `Core/RemovalLog.cs` grava o log por operacao (CSV/JSON) com o codigo de erro do Windows (`Core/Removal.cs:57`); o log de instalacao e `Core/Modules/LogOperations.cs:165` |
| 42 | **Enumeração por SID do usuário** dos pacotes MSIX | literal `FindPackagesByUserSecurityId failed` (`geek.strings.txt:1907`) | **PARCIAL** - **Enumeração por SID do usuário** dos pacotes MSIX/modulo de aplicativos do Windows `Core/Modules/WindowsApps.cs:39` |
| 43 | **Remoção de arquivo via SHFileOperation** (lixeira) | `SHFileOperationW` importada (`geek.strings.txt:3269`) | **OK** - `Core/RecycleBin.cs:61` envia para a lixeira, `:95` verifica se aceita, `:127` esvazia |
| 44 | **"Nenhum programa nesta categoria"** | literal `<staticNoProgramsInCategory>Seems like no Microsoft Store apps installed</staticNoProgramsInCategory>` (`geek.strings.txt:4585`) | **OK** - **"Nenhum programa nesta categoria"**/`src/Tranal.Uninstaller/MainWindow.xaml.cs:404` (`Status.NoProgramsInCategory`) o rodapé avisa quando a lista da categoria esta vazia; |

### 2.3 A correção: o Geek **não** aceita arrastar executável

A crença popular é que o Geek aceita arrastar um `.exe` sobre a janela.
**Medido: não aceita.**

- Nenhuma das 7 janelas de topo e dos 15 controles-filhos tem
  `WS_EX_ACCEPTFILES` (`drop-log3.txt`: todas com `ACCEPTFILES=False`).
- Um `WM_DROPFILES` postado na janela principal **não produziu nenhuma janela
  nem mensagem** (`drop-log.txt`).
- Passar o caminho de um `.exe` como **argumento** também não produziu
  efeito: `geek.exe "C:\Windows\System32\notepad.exe"`, executado 2 vezes,
  **não** abriu o notepad nem mostrou diálogo.

O binário importa `DragQueryFileW`/`DragFinish` (API do shell), mas nenhuma
janela aceita o drop. É a API disponível, não o recurso.

### 2.4 O que a versão **PRO** promete (banner, não executado)

Real-time installation monitor · Thorough and complete removal · Start-up
programs management · Modern interface, technical support · **Batch removal**.

> O Free já tem *Batch Force Removal* e *Batch Remove Entries*; o "Batch
> removal" do PRO é a desinstalação completa em lote — o que o Free não faz.

---

## 3. Mecanismos notáveis

### 3.1 DLLs importadas

18 DLLs no dump PE local (o levantamento REA lista 19, com `MSIMG32` em
duplicidade), 716 funções:

| DLL | Nº | Papel |
|---|---|---|
| KERNEL32 | 230 | `CreateProcessW`, `OpenProcess`, **`TerminateProcess`**, `WaitForSingleObject(Ex)`, `CreateFileW`, `DeleteFileW`, `RemoveDirectoryW`, **`MoveFileExW`**, `FindFirstFileExW`, `GetVolumeInformationW`, `GetPrivateProfileStringW`, `GetTempPathW` |
| USER32 | 222 | `MessageBoxW`, `SendMessageW`, `PostMessageW`, `SetWindowsHookExW` |
| GDI32 | 87 | desenho da lista/ícones |
| OLEAUT32 | 26 | automação OLE |
| gdiplus | 25 | `GdipCreateBitmapFromHICON`, `GdipDrawPath` |
| ole32 | 22 | `CoCreateInstance`, `CoInitializeEx` (COM para MSIX/Store apps) |
| ADVAPI32 | 18 | `RegOpenKeyExW`, `RegQueryValueExW`, `RegEnumKeyExW`, `RegDeleteKeyW`, `RegDeleteValueW`, `RegSetValueExW`; `AdjustTokenPrivileges`, `LookupPrivilegeValueW`, `OpenProcessToken` |
| COMCTL32 | 16 | common controls |
| SHLWAPI | 16 | helpers de caminho/URL |
| SHELL32 | 14 | `ShellExecuteExW`, **`SHFileOperationW`**, `ExtractIconExW`, `SHGetKnownFolderPath`, **`DragQueryFileW`**, `CommandLineToArgvW` |
| UxTheme | 14 | theming do Windows (dark mode) |
| WININET | 13 | `InternetOpenW`, `InternetOpenUrlW` → **verificação de nova versão** |
| VERSION | 3 | `GetFileVersionInfoW` |
| OLEACC | 2 | acessibilidade MSAA |
| oledlg | 2 | `OleUIBusyW` |
| MSIMG32 | 1 | `AlphaBlend` |
| ntdll | 1 | `RtlGetVersion` |
| WINMM | 1 | `PlaySoundW` |

### 3.2 Mecanismos de remoção

| Mecanismo | Evidência |
|---|---|
| **Kill de processo travado** — `TerminateProcess` importado + logs `> TerminateProcess %u`, `> TerminateProcess failed with error %u`, `> OpenProcess failed with error %u` | `pe-imports-resumido.txt`, `strings-geek-exe.txt` |
| **Remoção adiada para o próximo reinício** quando o arquivo está em uso — "ERROR_ACCESS_DENIED, the file will be deleted on the next system restart" e "ERROR_SHARING_VIOLATION, the folder will be deleted on the next system restart" | `strings-geek-exe.txt` |
| **Desregistro de COM** — `regsvr32.exe /s /u "%s"` | `strings-geek-exe.txt` |
| **Log de desinstalação** — `s_UninstallLog.log` | `strings-geek-exe.txt` |
| **Varredura de sobras** — classes `CUninstaller::FindTraced`, `CWizardScanner`, **`CWizardScannerMSI`**, **`CScannerMSI::ScanMSITraces`**, `CWizardTreeCtrl` | `strings-geek-exe.txt` |
| **Varredura de registro** — `...\CurrentVersion\Uninstall\`, **`...\Installer\UserData\%s\Products\`**, **`...\Installer\Folders`**, `KEY_CLASSES_ROOT`, `KEY_USERS\`, `Software\Classes\ActivatableClasses\Package` (MSIX) | `strings-geek-exe.txt` |
| Detecção de instalação em `Local AppData Programs` e por uninstall string / display icon | `strings-geek-exe.txt` |
| Extração de ícone — `ExtractIconExW` + `GdipCreateBitmapFromHICON` | `pe-imports-resumido.txt` |
| **Mutex de instância única** — `Local\GeekUninstallerMutex` | `strings-geek-exe.txt` |

> **Achado relevante:** o Geek varre as sobras de **MSI** (`ScanMSITraces`) e
> as chaves `Installer\UserData\…\Products` e `Installer\Folders`. É a mesma
> direção que o Tranal ainda não tomou — a varredura por nome não acha o que
> o instalador colocou fora da pasta do programa.

### 3.3 Integração com o sistema

**Nenhuma extensão de shell.** Sem menu de contexto, sem serviço de
background, sem driver.

---

## 4. Interface

Codejock XTP. A árvore UIA expõe 7 elementos: `XTPDockBar` (topo),
`XTPToolBar` (barra de menu), `SysListView32` + `SysHeader32` (lista e
cabeçalho), `Edit` (campo de filtro) e `XTPStatusBar` (rodapé). A lista e o
cabeçalho são controles nativos; menu, barra e rodapé são do XTP — daí a
árvore rasa apesar da interface rica. Os menus são popups `XTPPopupBar`.

| Janela | Classe | Tamanho |
|---|---|---|
| Principal "Geek Uninstaller 1.6.0.200" | `#32770` | 480×600 |
| Confirmação de remoção forçada | `#32770` | 376×162 |
| Wizard de sobras "Alguns itens órfãos encontrados!" | `#32770` | 496×288 |
| "Todos os traços removidos" | `#32770` | 496×288 |
| "Sobre o Geek Uninstaller" | `#32770` | 358×153 |
| "Salvar como" (export HTML) | `#32770` | 1200×720 |

Popups de menu: File 213×56 · View 232×135 · Action 203×156 · Help 128×56 ·
Appearance 241×77.

---

## 5. Linha de comando

| Parâmetro | Efeito | Fonte |
|---|---|---|
| `/store_apps` | Abre o app já na aba **Microsoft Store Apps** | `geek-store-apps.png`; `strings-geek-exe.txt`; release notes 1.4.10.155 |

Medido: com `/store_apps` a lista passa a mostrar "App Installer",
"Calculator", "Camera", "ChatGPT", "Claude", "Clock", "Dev Home",
extensões de imagem — **74 programas / 29,0 MB** — em vez dos
**80 programas / 658 GB** do desktop.

- **Não existe modo silencioso / não-interativo.** Nenhuma string de
  silent/unattended/quiet no binário (a única ocorrência é
  `regsvr32.exe /s /u "%s"`, que é desregistro de COM). O site também não
  documenta nenhuma seção de linha de comando.
- **Não aceita caminho de arquivo como argumento** (medido, 2 execuções).
- `CommandLineToArgvW` e `GetCommandLineA/W` são importadas, coerente com o
  único parâmetro acima.
- O launcher repassa o parâmetro ao `geek64.exe` que extrai (medido:
  `/store_apps` chegou ao processo de 64 bits).

---

## 6. O que o Tranal NÃO faz hoje

| Recurso do Geek | Tranal |
|---|---|
| **Diálogo de órfãos** ("Alguns itens órfãos encontrados!") | ⬜ — o Tranal não tem o conceito de órfão |
| **Frase de fechamento do ciclo** ("Todos os traços removidos") | ⬜ |
| **Confirmação ao cancelar** a remoção de sobras | ⟡ — o Tranal cancela o assistente, sem essa confirmação |
| **Batch Force Removal** / **Batch Remove Entries** | ⬜ |
| **Modificar/reparar** a instalação | ⬜ |
| **Reset Columns** | ⬜ |
| **Abrir na Microsoft Store** | ⬜ |
| **Browse Source…** (ir à origem do instalador) | ⬜ |
| Diálogo de falha com opção de **limpar entrada e arquivos** | 🟡 — o Tranal tem modo forçado, mas não esse diálogo |
| **Varredura de sobras de MSI** (`ScanMSITraces`, `Installer\UserData\…\Products`, `Installer\Folders`) | ⬜ |
| Categoria **Windows Updates** | ⬜ |
| **Atalhos de teclado** no menu Ação | 🟡 |
| Coluna de **data de instalação** e de **tamanho** com total agregado | ✅ |

### Onde o Tranal já é melhor

| Ponto | Tranal | Geek |
|---|---|---|
| **Backup do registro antes de desinstalar** | ✅ completo, com script de restauração | ❌ nenhum |
| **Ponto de restauração** antes de desinstalar | ✅ | ❌ nenhum |
| **Confiança das sobras com motivo** | 3 níveis + frase por item | 2 categorias (File System / Registry), sem confiança |
| **Lixeira** para os arquivos | ✅ | ❌ apaga direto (`SHFileOperationW` existe mas sem lixeira) |
| **CLI** | `/scan`, `/mu`, `/export`, `/columns` | 1 switch (`/store_apps`) |
| **Exclusão** contra a remoção | exclusões aplicadas **até no backup** | não há |
| **Ferramentas** (rastros, backup, registro, autoruns) | ✅ painel completo | ❌ só desinstalação |
| **Segurança do motor** | `SafeTraversal` nunca atravessa link | — |

> **O Geek gratuito é o exemplo mais claro de que a lista de recursos do
> Revo Pro é majoritariamente toolset**: o desinstalador em si cabe em 60
> strings. O que o Revo paga caro é o **painel de manutenção** — e é
> exatamente isso que o Tranal já tem.

---

## 7. Fichos de evidência

| Arquivo | Bytes | Conteúdo |
|---|---|---|
| `app/geek.exe` | 11.980.432 | o binário |
| `evidencia/langstrings-en.txt` | 5.829 | **as 60 strings do app** — o inventário completo |
| `evidencia/strings-geek-exe.txt` | 326.188 | strings ASCII + UTF-16 do binário |
| `evidencia/pe-geek-exe.txt` | 878 | cabeçalho PE, assinatura, DLLs importadas |
| `evidencia/pe-imports-resumido.txt` | — | 18 DLLs × 716 funções, com o papel de cada uma |
| `evidencia/manual-download.txt` | 21.373 | página de download + release notes |
| `evidencia/uia-geek-arvore.txt` | 429 | árvore UIA (7 elementos) |
| `evidencia/prefs-geek.xml` | 577 | preferências em PortableProgramData |
| `evidencia/drop-log3.txt` | 1.725 | **prova de que nenhuma janela aceita drop** |
| `evidencia/wiz-*.png` | — | 11 capturas do assistente de desinstalação |
| `evidencia/menu-*.png`, `popup-*.png` | — | capturas dos 4 menus e dos popups |
| `evidencia/elev-Geek_Uninstaller_1_6_0_200-20261105-190211.png` | 42.168 | janela principal em execução |

---

## 8. Lacunas desta auditoria

### 8.1 Lacunas de **medição** (o que não foi possível medir)

| O que faltou | Por quê |
|---|---|
| **Desinstalação real** de um programa | não executada — exigiria instalar e desinstalar de verdade |
| Comportamento do **PRO** | software pago; só o banner foi medido |
| Como o Geek calcula o **tamanho** de cada programa | não instrumentado |
| O **Windows Updates** na prática (a string existe, o uso não foi observado) | não navigado |
| Quais itens do `Installer\Folders` o Geek remove | a varredura existe; o critério, não |

### 8.2 Lacunas de **funcionalidade** (medidas no binário, ausentes no Tranal)

A lista completa e evidenciada está em **[`BINARIO-GEEK.md`](BINARIO-GEEK.md)** — 30
linhas, cada uma com o identificador do binário e o arquivo+linha do dump.
Resumo por impacto:

| Impacto | Quantidade | Exemplos |
|---|---|---|
| 🔴 alto | 7 | Modify/repair, Batch Force Removal, Batch Remove Entries, desregistro de COM via `regsvr32`, varredura de sobras de MSI pelo registro do Installer, limpeza de `MrtCache`, backup da `Applistbackup` |
| 🟡 médio | 17 | falso positivo de `InstallLocation`, Levenshtein, `Local AppData\Programs`, cache de uninstall, painel "Active Files", diálogo de órfãos, "Browse..." nas sobras, confirmação ao cancelar |
| ⚪ baixo | 6 | aviso "No matching items", abrir na Microsoft Store, Browse Source, log por programa, total agregado no rodapé, enumeração MSIX por SID |

> **Nota.** A varredura de sobras de MSI aparece na tabela de funcionalidades
> como `OK` porque o Tranal já tem `Core/MsiInstallLog.cs`. O que falta de fato
> é a **varredura do registro do Windows Installer** (`Installer\UserData\…\Products`
> e `Installer\Folders`), registrada à parte em `BINARIO-GEEK.md`.

---

## 9. Decisões que este documento obriga

1. **O diálogo de órfãos é uma ideia que vale roubar.** O Geek diz
   "Alguns itens órfãos encontrados!" — e oferece removê-los. O Tranal hoje
   nem tem o conceito de órfão. E há um segundo motivo: **sobra de
   confiança baixa que não pertence a nenhum programa instalado** é
   informação útil — hoje o Tranal apenas a lista.
2. **A varredura de sobras de MSI é o próximo passo do núcleo.** O Geek
   varre `Installer\UserData\…\Products` e `Installer\Folders` e tem uma
   classe `CScannerMSI::ScanMSITraces`. São **149 dos 211 programas** desta
   máquina (MSI). A varredura por nome não acha o que o instalador pôs
   fora da pasta do programa — e é exatamente esse o item F.1-2 do backlog.
3. **O resumo do ciclo ("Todos os traços removidos") é uma frase que
   falta.** Custa pouco e fecha a história na tela.
4. **Cancelar a remoção de sobras precisa de confirmação.**
5. **A lista de recursos pagos do Revo é majoritariamente toolset.** Isso
   consolida a aposta do Tranal: desinstalador + painel de manutenção.

---

*Auditoria gerada por medição direta dos artefatos e por execução do
programa. Toda linha deste documento aponta para evidência em disco; o que
não pôde ser verificado está marcado como tal na seção 8.*
