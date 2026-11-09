# Bulk Crap Uninstaller 6.3.0 — auditoria

> Concorrente **open source** (Apache 2.0, `Klocman/Bulk-Crap-Uninstaller`).
> Escolido primeiro de propósito: por ser o único dos dez com o código
> público, ele é a **referência de implementação** — o lugar onde se vê
> *como* cada funcionalidade é feita, não apenas que ela existe.

Data da coleta: 05/11/2026 · Versão auditada: **6.3.0** (build `572f16347d8fd461e8725e88a5704478137dbe88`)

---

## 1. Identificação

| Campo | Valor | Fonte |
|---|---|---|
| Produto | Bulk Crap Uninstaller | `BCU_manual.html` |
| Editor | Klocman (Klocman Software) | metadata do assembly |
| Licença | Apache 2.0, open source | `Licence.txt`, `NOTICE` |
| Código | https://github.com/Klocman/Bulk-Crap-Uninstaller | `NOTICE` |
| Versão | 6.3.0 | `FileVersionInfo` |
| Formato | .NET 8 (`net8.0-windows10.0.18362.0`), WinForms | `BCUninstaller.runtimeconfig.json` |
| Assinatura | **não assinado** | `Get-AuthenticodeSignature` → `NotSigned` |
| Requisitos | Windows Vista+, ~300 MB de RAM, 50 MB de disco | manual §1.1 |
| Privilégio | **exige administrador para iniciar e funcionar** | manual §1.1 |

### Artefatos coletados

| Arquivo | Bytes | SHA-256 |
|---|---|---|
| `BCUninstaller_6.3.0_net8.0-windows10.0.18362.0.zip` | 12.362.694 | `4bfa602517b0cd4e81519ae12f4fdb087625aa93f2fed5c99f5c049117ce5c17` |
| `BCUninstaller_6.3.0_setup.exe` | 9.430.340 | `d89c81f4dd029b4862fc6261262a4a9851a6948827af3e55d54e9a8c53b13d88` |
| `Localisation_Pack_6.3.0.zip` | 2.755.822 | `8ebcf7adcc0a24dcfb24b33713c660a75dbdfaad2fb7982df6c98518b18b97a9` |
| `app\BCUninstaller.dll` | 1.722.368 | `22935786006e35eaf8799d0cc6ce68a5df311ba28a2c6f7c2a99c36335d6c0ec` |
| `app\UninstallTools.dll` (motor) | 686.592 | `3c82ca47f91d92eeadc34620a35d4e26d7acaa91338314bb2bba00eef4a0cf79` |
| `app\BCU-console.dll` | 332.800 | `c52d7a308a10979d6ae0203ca3715b5f729afe6de528d121bcab405cf4f4958b` |
| `app\UniversalUninstaller.dll` | 203.776 | `d1d3811231760986e9a878a31a2dce37d5211f7d4336c295d16f461771b5985f` |
| `app\UninstallerAutomatizer.dll` | 222.208 | `ee94fe66bb5583e2d9c975b7c2996ed5d4cb6804199b58a2cf745bbf6bae32c5` |

### A descoberta que muda a auditoria

O **Localisation Pack 6.3.0** não é só um pacote de idiomas: ele traz a
**estrutura de pastas do código-fonte**, com um `.resx` por tela. Isso
transformou a auditoria — os nomes dos arquivos são o **inventário de telas**
do produto, e os `.resx` em pt-BR são a **spec funcional traduzida**.

```
BulkCrapUninstaller/source/
  BulkCrapUninstaller/
    Controls/        AdvancedClipboardCopy, FileTargeter, ListLegend,
                     RelatedUninstallerAdder, UninstallConfirmation,
                     Settings/{AdvancedFilters, CacheSettings,
                              PropertiesSidebar, UninstallationSettings}
    Forms/Helpers/   AdvancedClipboardCopyWindow, DebugWindow, FeedbackBox,
                     FeedbackWindow, ListLegendWindow, NewsPopup,
                     PropertiesWindow, RatingPopup, TargetWindow
    Forms/Windows/   AboutBox, JunkRemoveWindow, MainWindow, SettingsWindow,
                     UninstallProgressWindow
    Forms/Wizards/   BeginUninstallTaskWizard, FirstStartBox
  UninstallTools/    Controls/{FilterEditor, UninstallListEditor},
                     Dialogs/StartupManagerWindow
  UniversalUninstaller/  TargetList, UninstallSelection
  UninstallerAutomatizer/ Forms/MainWindow
  KlocTools/         Controls/*, Forms/* (ProcessWaiter, WindowTargeterDialog)
```

**45 arquivos `.resx` em pt-BR, 1.066 chaves** — para comparação, o catálogo
do Revo Pro tem 1.136 strings em 42 seções. São ordens de grandeza
comparáveis, o que faz do BCU um bom espelho do Revo.

Extraídos para `workspace/auditoria/bcu/evidencia/resx-ptBR-*.txt`.

---

## 2. Superfície observável

Toda linha aponta para um arquivo de evidência **do binário**. A coluna
"Evidência no binário" cita o identificador real extraído do dump (nome de
tipo/método .NET, nome de formulário, ou literal exato de string) e indica o
arquivo de dump de onde veio. Quando o recurso **não** pôde ser confirmado no
dump, a linha diz isso honestamente em vez de inventar.

> **Nota de proveniência.** O dump analisado é o `BCUninstaller.dll` (a
> interface). As chaves `Progress_*`, `UninstallerType_*`, `Junk_*_GroupName` e
> `Confidence_*` citadas na versão anterior deste documento vêm do
> **Localisation Pack** (`.resx` traduzido) e **não** aparecem no dump do DLL
> (`bcu.strings.txt`: 1 ocorrência de `UninstallerType`, 0 de `Confidence_*`).
> Por isso, cada linha abaixo dá evidência **do próprio DLL** quando existe, e
> marca `nao confirmado no binario` quando só o `.resx` provava a existência.

### 2.1 Descoberta e classificação de programas

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 1 | Enumeração pelo **registro** (`HKLM`/`HKCU`, 32 e 64 bits) | tipo `BulkCrapUninstaller.Functions.ApplicationList` + namespace `Microsoft.Win32.Registry` (`bcu.identificadores.txt:35,49`); `Progress_Registry` **não** encontrado no dump do DLL | OK — `src/Tranal.Uninstaller/Core/ProgramScanner.cs` |
| 2 | Enumeração de **produtos MSI instalados** por GUID | `uninstallUsingMsiExecContextMenuStripItem` (`bcu.strings.txt:5411`), literal "Uninstall using &MsiExec..." (`:8196`), "Product code" (`bcu.inventario.md:285`) | OK — `src/Tranal.Uninstaller/Core/Discovery/MsiProductCatalog.cs` |
| 3 | **Varredura de drives** atrás de aplicativos não registrados | literal "Scan removable drives" (`bcu.inventario.md:337`), "Scanning specified directory..." (`:338`) | OK — `src/Tranal.Uninstaller/Core/Discovery/DriveScanner.cs` |
| 4 | Detecção de **Windows Store Apps** | `viewWindowsStoreAppsToolStripMenuItem` (`bcu.strings.txt:5501`), `get_ScanStoreApps` (`:6577`), literal "Show Windows Store apps" (`bcu.inventario.md:365`) | OK — `src/Tranal.Uninstaller/Core/Modules/WindowsApps.cs` |
| 5 | Detecção de **Windows Features** (via WMI) | `viewWindowsFeaturesToolStripMenuItem` (`bcu.strings.txt:5486`), `checkBoxScanWinFeatures` (`:6317`), `get_ScanWinFeatures` (`:6315`) | OK — `src/Tranal.Uninstaller/Core/Discovery/WindowsFeatures.cs` |
| 6 | Detecção de **Windows Updates** (via WUA) | literal "Detection of Windows Updates uses WUA, which is slow…" (`bcu.inventario.md:44`) | OK — `src/Tranal.Uninstaller/Core/Discovery/WindowsFeatures.cs` |
| 7 | Detecção de **aplicativos Steam** | `checkBoxScanSteam` (`bcu.strings.txt:5399`), `get_ScanSteam` (`:5397`), `doNotKillSteam` (`:5396`) | OK — `src/Tranal.Uninstaller/Core/Discovery/PackageManagerCatalog.cs` |
| 8 | Detecção de **pacotes Chocolatey** e **Scoop** | `checkBoxScoop` (`bcu.strings.txt:5880`), `get_ScanScoop` (`:5878`), `groupBoxAppStores` (`:6314`); Chocolatey: **nao confirmado no binario** (só no `.resx`) | OK — `src/Tranal.Uninstaller/Core/Discovery/PackageManagerCatalog.cs` |
| 9 | Detecção de **apps Oculus** | **nao confirmado no binario** — a chave `UninstallerType_Oculus` só existe no `.resx` do Localisation Pack | **OK** - `src/Tranal.Uninstaller/Core/Discovery/OculusCatalog.cs:32` (`OculusCatalog.All`, 19 aplicativos Oculus/Meta) e `:73` (`IsOculusApp`); consumidor na CLI em `src/Tranal.Cli/ParityCommands.cs:47` (`/oculus`, registrado em `:29`). Binario do concorrente: nao confirmado no binario (a chave `UninstallerType_Oculus` existe so no `.resx` do Localisation Pack) |
| 10 | Detecção de **scripts PowerShell** de desinstalação | literal "PowerShell script|*.ps1" (`bcu.inventario.md:280`) | OK — `src/Tranal.Uninstaller/Core/UninstallerKind.cs` (tipo PowerShell) |
| 11 | **Classificação por tipo de desinstalador**: Msiexec, Inno Setup, NSIS, InstallShield, SDBInst, Steam, Store App, Windows Feature, Windows Update, Chocolatey, Oculus, PowerShell, Simple Delete, Desconhecido | `UninstallerType` (1 ocorrência em `bcu.strings.txt`); os 14 `UninstallerType_*` vivem no motor `UninstallTools.dll`, fora deste dump | **PARCIAL** - `src/Tranal.Uninstaller/Core/UninstallerKind.cs:22` (enum) e `:167` (`Classify`): 13 valores — Unknown, MsiExec, InnoSetup, Nsis, InstallShield, SdbInst, Steam, StoreApp, Chocolatey, Oculus, PowerShell, SimpleDelete; coluna na lista em `src/Tranal.Uninstaller/Core/ProgramColumns.cs:271`. **Nota**: faltam **Windows Feature** e **Windows Update** como tipo proprio do desinstalador (existem como catalogos separados em `src/Tranal.Uninstaller/Core/Discovery/WindowsFeatures.cs:129` e `src/Tranal.Uninstaller/Core/Management/WindowsUpdateManager.cs:25`, nunca como `UninstallerKind`). Binario do concorrente: `bcu.strings.txt` (`UninstallerType`; os 14 `UninstallerType_*` vivem no motor `UninstallTools.dll`, fora do dump) |
| 12 | Marcação de **componente do sistema** | literal "System Component" (`bcu.inventario.md:380`), "System components" (`:381`) | OK — `src/Tranal.Uninstaller/Core/ProgramColumns.cs` |
| 13 | Marcação de **protegido** (tag `NoRemove` do editor) | literal "Protected uninstallers" / "Protected apps" (`bcu.inventario.md:292-293`), "Protected items are marked with the 'NoRemove' tag" (`:371`) | **PARCIAL** - `src/Tranal.Uninstaller/Core/ProgramScanner.cs:148` (`IsProtected = GetInt(sub, "NoRemove") == 1`) e `src/Tranal.Uninstaller/Core/Management/ProtectedUninstallTag.cs:34` (`IsProtected`); aviso no assistente em `src/Tranal.Uninstaller/Core/UninstallAdvisories.cs:198` (`ProtectedAdvisory`), exibido em `src/Tranal.Uninstaller/Ui/UninstallWizardWindow.xaml.cs:493`. **Nota**: nao ha marcacao nem filtro de protegidos na lista — a coluna "Protegido" (`src/Tranal.Uninstaller/Core/ProgramColumns.cs:277`) mostra a lista de exclusoes do Tranal (`ProgramColumns.cs:413`), nao a tag `NoRemove`. Binario do concorrente: `bcu.inventario.md:292-293` / `:371` |
| 14 | Marcação de **atualização** de outro aplicativo | `checkBoxShowUpdates` (`bcu.strings.txt:6342`), literal "Show updates" (`bcu.inventario.md:363`) | **PARCIAL** - `src/Tranal.Uninstaller/Core/Discovery/UpdateMarker.cs:68` (`Detect`) e `:131` (`IsUpdateOf`); exposto pela CLI em `src/Tranal.Cli/ParityCommands.cs:74` (`/updates`, flag em `:30`). **Nota**: a deteccao existe, mas **nao ha marcacao/coluna de atualizacao na lista principal** — o sinal do registro `IsUpgrade` lido em `src/Tranal.Uninstaller/Core/ProgramScanner.cs:144` nao aparece em nenhuma coluna. Binario do concorrente: `bcu.strings.txt:6342` (`checkBoxShowUpdates`) |
| 15 | Marcação de **não registrado (órfão)** | `checkBoxOrphans` (`bcu.strings.txt:6529`), literal "Orphaned apps" (`bcu.inventario.md:264`) | **PARCIAL** - `src/Tranal.Uninstaller/Core/Discovery/DriveScanner.cs:82` (`OrphanUninstaller`) e `:226` (varredura); tela em `src/Tranal.Uninstaller/Ui/Discovery/DiscoveryWindow.xaml.cs:170` (categoria `OrphanUninstallers`). **Nota**: os executaveis orfaos aparecem na tela de Descoberta, **nao como marcacao na lista principal**. Binario do concorrente: `bcu.strings.txt:6529` (`checkBoxOrphans`) |
| 16 | Marcação de **aplicativo inválido / desinstalador corrompido** | `checkBoxInvalidTest` (`bcu.strings.txt:6893`), literal "Use grey color to identify uninstallers that are corrupted or missing." (`bcu.inventario.md:146`) | **PARCIAL** - `src/Tranal.Uninstaller/Core/UninstallerHealth.cs:55` (`Evaluate`: Ok/Missing/Broken), leitura em `src/Tranal.Uninstaller/Core/ProgramScanner.cs:153`, aviso em `src/Tranal.Uninstaller/Core/UninstallAdvisories.cs:214` (`UninstallerHealthAdvisory`), exibido em `src/Tranal.Uninstaller/Ui/UninstallWizardWindow.xaml.cs:493`. **Nota**: o desinstalador ausente/corrompido e avisado antes da desinstalacao, mas **nao ha coluna nem filtro que marque a entrada na lista** (a coluna "Protegido" da lista e a lista de exclusoes). Binario do concorrente: `bcu.strings.txt:6893` (`checkBoxInvalidTest`) |
| 17 | Pastas de instalação **personalizadas** declaradas pelo usuário + detecção automática | `labelProgramFolders` (`bcu.strings.txt:6595`), `checkBoxAutoInstallFolderDetect` (`:6745`) | **PARCIAL** - `src/Tranal.Uninstaller/Core/Discovery/UserInstallFolders.cs:47` (`Roots`: `%LOCALAPPDATA%\Programs`, `%APPDATA%`, `%LOCALAPPDATA%`) e `:87` (`IsUserInstallPath`); CLI em `src/Tranal.Cli/ParityCommands.cs:97` (`/userapps`, flag em `:31`). **Nota**: a deteccao automatica das pastas do usuario existe, mas **nao ha pastas extra declaradas pelo usuario** (nenhuma preferencia de pasta adicional e lida). Binario do concorrente: `bcu.strings.txt:6595` (`labelProgramFolders`) / `:6745` (`checkBoxAutoInstallFolderDetect`) |
| 18 | **Filtros rápidos**: Só básicas / Só componentes do sistema / Tudo / Só auto-inicialização / Só navegadores | literais "Only &basic applications", "Only system components", "Only started automatically", "Only web browsers" (`bcu.inventario.md:241-244`) | OK — `src/Tranal.Uninstaller/Core/Appearance/ProgramFilters.cs` |

### 2.2 Desinstalação

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 19 | Desinstalar com **desinstalador nativo em primeiro plano** ("loud") | `checkBoxConcurrentOneLoud` (`bcu.strings.txt:4291`), literal "Selected uninstallers will now be executed one at a time." (`bcu.inventario.md:348`) | OK — `src/Tranal.Uninstaller/Core/Removal.cs` |
| 20 | **Desinstalação silenciosa**, com gerador de desinstalador silencioso ausente | `get_QuietUninstallPossible` (`bcu.strings.txt:4506`), `checkBoxGenerate` (`:4797`), literal "Quiet uninstall command" (`bcu.inventario.md:306`) | OK — `src/Tranal.Uninstaller/Core/QuietUninstall.cs` |
| 21 | **Gerar silencioso por automação de interface** (UI Automation) | `checkBoxGenerate` (`bcu.strings.txt:4797`), `checkBoxGenerateStuck` (`:5257`); a técnica de UI Automation vem do `.resx` — **nao confirmado no binario** | **FORA POR DECISAO** - Decisao registrada: `docs/auditoria/BINARIO.md:132` — "**Gerar** um desinstalador silencioso para programa orfao / Exige automacao de interface de desinstaladores de terceiros. O Tranal deriva o comando e oferece a remocao manual assistida". O que existe hoje: `src/Tranal.Uninstaller/Core/QuietUninstall.cs:71` (`Build`) deriva o comando por familia, sem dirigir a interface. Binario do concorrente: `bcu.strings.txt:4797` (`checkBoxGenerate`); a tecnica de UI Automation vem do `.resx` — nao confirmado no binario |
| 22 | **Desinstalar via MsiExec** pelo GUID do produto | `uninstallUsingMsiExecContextMenuStripItem` (`bcu.strings.txt:5411`), literal "Selected uninstaller doesn't have a valid Guid for use with MsiExec." (`:9147`) | OK — `src/Tranal.Uninstaller/Core/Discovery/MsiProductCatalog.cs` |
| 23 | **Desinstalar manualmente**: pula o desinstalador e devolve a lista de arquivos/chaves para apagar à mão | `manualUninstallToolStripMenuItem` (`bcu.strings.txt:5461`), literal "Manual uninstall" (`bcu.inventario.md:209`) | OK — `src/Tranal.Uninstaller/Core/Automation/ManualRemoval.cs` |
| 24 | **Desinstalar por janela, diretório ou arquivo** (mira de arrastar e soltar sobre a janela) | `toolStripButtonTarget` (`bcu.strings.txt:6752`), tipo `BulkCrapUninstaller.Forms.TargetWindow.resources` (`bcu.identificadores.txt:32`) | OK — `src/Tranal.Uninstaller/Core/Topmost/TopmostTargetResolver.cs` + `src/Tranal.Uninstaller/Core/UninstallTargetArgument.cs` |
| 25 | **Desinstalar de um diretório** (achando o desinstalador dentro dele) | `get_FileTargeter_SelectDirectoryWithAppsToRemove` (`bcu.strings.txt:4888`), literal "Search for applications in..." (`bcu.inventario.md:339`) | OK — `src/Tranal.Uninstaller/Core/UninstallTargetArgument.cs` |
| 26 | **Desinstalação simultânea** com prevenção de colisão | `checkBoxConcurrent` (`bcu.strings.txt:6837`), `checkBoxConcurrentOneLoud` (`:4291`), literal "Disable collision prevention when running" (`bcu.inventario.md:15`) | **PARCIAL** - `src/Tranal.Uninstaller/Core/Automation/UninstallQueue.cs:71` (`Gate`, `SemaphoreSlim` de processo inteiro) e `:168` (`DetectCollisions`); a fila e consumida pela CLI em `src/Tranal.Cli/ExtraCommands.cs:1052` e a colisao e reportada em `:1068`. **Nota**: a tela nao usa a fila (`src/Tranal.Uninstaller/Ui/UninstallWizardWindow.xaml.cs:164` so chama `SmartOrdering.Order`), e a guarda de processo cruzado nunca e marcada — `grep "MarkRunning" em src/` -> 1 hit so em teste (`src/Tranal.Tests/SearchGuardsAndPlatformTests.cs:151`), logo `src/Tranal.Uninstaller/Core/Automation/UninstallGuards.cs:133` (`CheckRunning`) nunca encontra a chave e a segunda desinstalacao nao e recusada. Binario do concorrente: `bcu.strings.txt:6837` (`checkBoxConcurrent`) |
| 27 | **Ordenação inteligente**: loud primeiro, para poder sair antes | `checkBoxBatchSortQuiet` (`bcu.strings.txt:6758`), literal "Intelligent uninstaller sorting" (`bcu.inventario.md:180`) | **OK** - `src/Tranal.Uninstaller/Core/Appearance/SmartOrdering.cs:25` (`Order`) e `:43` (`IsLoud`); consumido pela tela em `src/Tranal.Uninstaller/Ui/UninstallWizardWindow.xaml.cs:164` e pela CLI em `src/Tranal.Cli/ExtraCommands.cs:1052` (via `UninstallQueue.cs:94`). Binario do concorrente: `bcu.strings.txt:6758` (`checkBoxBatchSortQuiet`) |
| 28 | **Simulação** (não desinstala nada) | `checkBoxSimulate` (`bcu.strings.txt:4792`), literal "Simulate uninstallation" (`bcu.inventario.md:366`) | OK — `src/Tranal.Uninstaller/Core/Automation/CliToolSwitch.cs` (`/scan`, dry-run real) |
| 29 | **Impedir desligamento/reinício** durante a tarefa | `checkBoxShutdown` (`bcu.strings.txt:5789`), `PreventSleepOrShutdown` (`:5784`), `AllowSleepOrShutdown` (`:5785`) | **OK** - `src/Tranal.Uninstaller/Core/Automation/ShutdownGuard.cs:56` (`Acquire`, `SetThreadExecutionState`); ligado na remocao em `src/Tranal.Uninstaller/Core/UninstallSupport.cs:1363` (`using var guard = ShutdownGuard.Acquire()`). Binario do concorrente: `bcu.strings.txt:5789` (`checkBoxShutdown`) |
| 30 | **Monitorar desinstaladores silenciosos** atrás de popups (daemon de automatização) | `checkBoxAutoDaemon` (`bcu.strings.txt:5751`), literal "Monitor \"silent\" uninstallers for popups or other stalls" (`bcu.inventario.md:134`) | **AUSENTE** - `grep "MonitorSilent/PopupWatch/WatchPopup/DaemonMonitor" em src/` -> **0 hits**. O mais proximo e deteccao de desinstalacao externa (`src/Tranal.Uninstaller/Core/Discovery/ExternalUninstallWatcher.cs:50`), que le o registro depois do fato; nao ha daemon nem vigia de popup. Binario do concorrente: `bcu.strings.txt:5751` (`checkBoxAutoDaemon`) |
| 31 | **Finalizar automaticamente** desinstaladores silenciosos travados | `checkBoxAutoKillQuiet` (`bcu.strings.txt:6754`), `checkBoxGenerateStuck` (`:5257`) | OK — `src/Tranal.Uninstaller/Core/ProcessTerminator.cs` |
| 32 | **Tentar novamente** os silenciosos que falharam | `checkBoxRetryQuiet` (`bcu.strings.txt:6759`), literal "Do you want to try running quiet uninstallers that failed as loud?" (`bcu.inventario.md:46`) | **OK** - preferencia de repeticao `Core/Settings/AppSettings.cs:326`; a regra da repeticao `Core/Automation/UninstallQueue.cs:124` |
| 33 | **Pular** o desinstalador em execução, **Encerrá-lo**, **Rodar agora** | literais "Skip currently running task" (`bcu.inventario.md:367`), "Run uninstaller now" (`:327`), `terminateToolStripMenuItem` (`bcu.inventario.md:9`) | OK — `src/Tranal.Uninstaller/Core/Automation/RemovalProgress.cs` + `src/Tranal.Uninstaller/Ui/Automation/AutomationWindow.xaml.cs` |
| 34 | **Abortar a tarefa inteira**, sem matar o desinstalador em execução | literal "Stop current uninstall task" (`bcu.inventario.md:377`), `get_MessageBoxes_TaskStopConfirmation_Title` (`bcu.strings.txt:4544`) | **PARCIAL** - `src/Tranal.Uninstaller/Core/Automation/RemovalProgress.cs:243` (`RequestStop` -> `Issue(ProgressCommand.Stop)`), botao em `src/Tranal.Uninstaller/Ui/Automation/AutomationWindow.xaml.cs:106` (janela aberta pelo assistente em `src/Tranal.Uninstaller/Ui/UninstallWizardWindow.xaml.cs:806`); o plano para no `src/Tranal.Uninstaller/Core/Automation/UninstallPlan.cs:432` (`if (progress?.StopRequested == true) break;`). **Nota**: o encerramento so e verificado **entre as etapas** — o desinstalador nativo em execucao nao e interrompido, e nao ha o "Stop current uninstall task" imediato do BCU. Binario do concorrente: `bcu.inventario.md:377` |
| 35 | **Comandos antes e depois** da desinstalação | campos `textBoxPreUninstall` / `textBoxPostUninstall` (`bcu.inventario.md:9`) | OK — `src/Tranal.Uninstaller/Core/Automation/UninstallPlan.cs` (`PreCommand`/`PostCommand`) |
| 36 | **Desinstalação silenciosa de quem não aceita silent**: oferecer rodar em primeiro plano ou remover da tarefa | literal "Following items are missing quiet uninstallers:" (`bcu.inventario.md:136`) | **PARCIAL** - `src/Tranal.Uninstaller/Core/Automation/UninstallQueue.cs:140` (`UninstallQueueOutcome.RemovedFromTask`, "nao desinstalou em silencio: saiu da tarefa"); a CLI usa a fila em `src/Tranal.Cli/ExtraCommands.cs:1052` e reporta a saida da tarefa em `:1070`. **Nota**: existe **remover da tarefa**, mas **nao ha a opcao de rodar em primeiro plano** para quem nao desinstala em silencio. Binario do concorrente: `bcu.inventario.md:136` |
| 37 | **Ponto de restauração do sistema** antes de desinstalar | `checkBoxRestorePoint` (`bcu.strings.txt:6846`), literal "Create a new restore point" (`bcu.inventario.md:80`), "Start restore point creation" (`bcu.strings.txt:12997`) | OK — `src/Tranal.Uninstaller/Ui/BackupManagerWindow.xaml.cs` |
| 38 | **Backup do registro** antes de remover sobras | `radioButtonBackupAsk` (`bcu.strings.txt:5299`), `radioButtonBackupAuto` (`:5844`), `radioButtonBackupNever` (`:6115`), literal "Create registry backup" (`bcu.inventario.md:82`) | OK — `src/Tranal.Uninstaller/Core/FullRegistryBackup.cs` (com script de restauração) |
| 39 | **Assistente de desinstalação em 6 etapas**: Relacionado → Confirmação → Aplicativos em execução → Configuração → … → Final | tipo `BulkCrapUninstaller.Forms.BeginUninstallTaskWizard.resources` (`bcu.identificadores.txt:18`), literal "Begin new uninstallation task" (`bcu.inventario.md:47`) | **PARCIAL** - `src/Tranal.Uninstaller/Ui/UninstallWizardWindow.xaml.cs:72-80` (`StepNames`: 5 etapas) e `:312` (`ShowStep`, casos 0 a 4). **Nota**: sao **5 etapas, nao 6** — nao ha etapa de **Relacionado** nem de **Aplicativos em execucao** (o painel de processos em uso e a ferramenta separada `src/Tranal.Uninstaller/Ui/Tools/CleanupViews.cs:108`). Binario do concorrente: `bcu.identificadores.txt:18` (tipo `BeginUninstallTaskWizard.resources`) |
| 40 | Lista de **aplicativos relacionados** que talvez queiram desinstalar junto | tipo `BulkCrapUninstaller.Forms.RelatedUninstallerAdder.resources` (`bcu.identificadores.txt:30`), literal "Related uninstaller name" (`bcu.inventario.md:314`) | **OK** - `src/Tranal.Uninstaller/Core/Discovery/RelatedProgramFinder.cs:53` (`Find`, 4 motivos de relacao) e `:100` (`ReasonFor`); aviso em `src/Tranal.Uninstaller/Core/UninstallAdvisories.cs:244` (`RelatedProgramsAdvisory`), exibido em `src/Tranal.Uninstaller/Ui/UninstallWizardWindow.xaml.cs:493`. Binario do concorrente: `bcu.identificadores.txt:30` (tipo `RelatedUninstallerAdder.resources`) |

### 2.3 Sobras (junk) e confiança

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 41 | Varredura de sobras **após** a desinstalação, com motivo em texto | literal "Do you want to look for leftovers from performed uninstallation(s)?" (`bcu.inventario.md:62`), tipo `BulkCrapUninstaller.Forms.JunkRemoveWindow.resources` (`bcu.identificadores.txt:24`) | OK — `src/Tranal.Uninstaller/Core/UninstallSupport.cs` |
| 42 | **5 níveis de confiança**: Muito bom / Bom / Duvidoso / Mau / Desconhecido | tipo `UninstallTools.Junk.Confidence` (motor, `bcu.identificadores.txt:115`); os rótulos `Confidence_VeryGood…` são do `.resx` — **nao confirmado no binario** | **PARCIAL** - `src/Tranal.Uninstaller/Core/UninstallSupport.cs:19` (`LeftoverConfidence`: High/Medium/Low) e `src/Tranal.Uninstaller/Core/Discovery/FolderConfidenceEvaluator.cs:12` (`FolderConfidenceLevel`: High/Medium/Low). **Nota**: sao **3 niveis, nao 5**, e o motivo do julgamento acompanha a confianca (`src/Tranal.Uninstaller/Core/UninstallSupport.cs:142`). Binario do concorrente: `bcu.identificadores.txt:115` (tipo `UninstallTools.Junk.Confidence`); os rotulos `Confidence_VeryGood...` vem do `.resx` — nao confirmados no binario |
| 43 | **Justificativa textual por item** — a confiança vem acompanhada do *porquê* | as chaves `ConfidencePart_*` **não** aparecem no dump do DLL (só no `.resx`) — **nao confirmado no binario** | OK — `src/Tranal.Uninstaller/Core/Localization/LeftoverLabels.cs` (11 motivos) |
| 44 | Confiança de **pasta**: vazia, executáveis presentes, arquivos presentes, muitos arquivos, nome reconhecido, sem subdiretórios, editor reconhecido | chaves `Confidence_PF_*` — **nao confirmado no binario** (só `.resx`) | **PARCIAL** - `src/Tranal.Uninstaller/Core/Discovery/FolderConfidenceEvaluator.cs:99-117` (7 criterios: pasta-vazia, executaveis, dados-do-usuario, editor-reconhecido, nome-do-programa, pasta-de-programas, pasta-do-usuario) e `:165` (`Evaluate`); o veredito entra na sobra em `src/Tranal.Uninstaller/Core/UninstallSupport.cs:1128`. **Nota**: faltam os criterios "**muitos arquivos**" e "**sem subdiretorios**" — a inspecao le so o primeiro nivel (`FolderConfidenceEvaluator.cs:323`). Binario do concorrente: chaves `Confidence_PF_*` — nao confirmado no binario (so `.resx`) |
| 45 | Confiança de **inicialização**: entrada `RunOnce`, entrada compatível com o app | chaves `Confidence_Startup_*` — **nao confirmado no binario** (só `.resx`) | **OK** - `src/Tranal.Uninstaller/Core/Discovery/StartupEntries.cs:43` (`Scan`, le `Run` e `RunOnce` em `:30-35`) e `:104` (`BelongsTo`, dois sinais: executavel dentro da pasta e comando que cita o nome); o sinal entra na confianca da sobra em `src/Tranal.Uninstaller/Core/UninstallSupport.cs:663` (motivo `LeftoverReason.StartupEntry`, `src/Tranal.Uninstaller/Core/UninstallSupport.cs:115`). Binario do concorrente: chaves `Confidence_Startup_*` — nao confirmado no binario (so `.resx`) |
| 46 | Aviso explícito ao mexer em item de **baixa confiança** ("pode ser muito perigoso") | `checkBoxHideLowConfidence` (`bcu.strings.txt:4315`), literal "Junk \| Potentially dangerous" (`bcu.inventario.md:192`) | **PARCIAL** - `src/Tranal.Uninstaller/Ui/UninstallWizardWindow.xaml.cs:510` (`ShowLeftoverAdvisory`) e `:515` (`UninstallAdvisories.UserDataAdvisory`), avisos na etapa de sobras (`src/Tranal.Uninstaller/Ui/UninstallWizardWindow.xaml:139`). **Nota**: o aviso existe para **dados do usuario** e para os casos do `UninstallAdvisories` (`src/Tranal.Uninstaller/Core/UninstallAdvisories.cs:9-57`), mas **nao ha aviso ligado ao nivel de confianca** ("pode ser muito perigoso"). Binario do concorrente: `bcu.strings.txt:4315` (`checkBoxHideLowConfidence`) |
| 47 | Backup antes de remover sobras + **lixeira** para arquivos | literal "Files and directories will be moved to the recycle bin, you can restore them from there." (`bcu.inventario.md:131`), "BCU Backup " (`bcu.strings.txt:13038`) | OK — `src/Tranal.Uninstaller/Core/RecycleBin.cs` + `src/Tranal.Uninstaller/Core/Recovery/LeftoverBackup.cs` |
| 48 | Exportar a lista de sobras para arquivo texto | literal "Export leftover list" (`bcu.inventario.md:119`), "Export failed" (`:118`) | OK — `src/Tranal.Uninstaller/Core/Reporting/LeftoverReportExporter.cs` |
| 49 | **Grupos de sobras** por categoria, com scanners próprios | as chaves `Junk_*_GroupName` — **nao confirmado no binario**; o painel é confirmado pelo literal "Junk/Leftover removal" (`bcu.inventario.md:193`) e pelo tipo `BulkCrapUninstaller.Forms.JunkRemoveWindow.resources` | **PARCIAL** - `src/Tranal.Uninstaller/Core/UninstallSupport.cs:49-123` (14 motivos de sobra, com justificativa por item) e `src/Tranal.Uninstaller/Core/Discovery/FolderConfidenceEvaluator.cs:96` (criterios de pasta); as ferramentas com scanner proprio tem scanner proprio: `src/Tranal.Uninstaller/Core/Tools/JunkCleaner.cs:55`, `src/Tranal.Uninstaller/Core/Tools/RegistryCleaner.cs:256`, `src/Tranal.Uninstaller/Core/Tools/TrackCleaner.cs:81`, `src/Tranal.Uninstaller/Core/Tools/PrivacyTraceCleaner.cs:181`. **Nota**: a lista de sobras do assistente e uma `ListView` sem agrupamento por categoria (`src/Tranal.Uninstaller/Ui/UninstallWizardWindow.xaml:164`) e **nao ha os 17 grupos** do BCU. Binario do concorrente: painel confirmado em `bcu.inventario.md:193`; as chaves `Junk_*_GroupName` nao estao confirmadas no binario |
| 50 | **Limpeza de pastas "Arquivos de Programas"** (vazias e parcialmente desinstaladas) | `cleanUpProgramFilesToolStripMenuItem` (`bcu.strings.txt:5484`) | OK — `src/Tranal.Uninstaller/Core/Discovery/EmptyFolderCleaner.cs` |
| 51 | Filtro para **esconder sobras de baixa confiança** | `checkBoxHideLowConfidence` (`bcu.strings.txt:4315`) | OK — `src/Tranal.Uninstaller/Core/Appearance/ProgramFilters.cs` |
| 52 | Instalador gerado automaticamente para **app não registrado** ("Simple Delete") | `UninstallerType_SimpleDelete` — **nao confirmado no binario** (só `.resx`) | **AUSENTE** - `grep "UninstallerKind.SimpleDelete" em src/` -> 3 hits, todos de classificacao (`src/Tranal.Uninstaller/Core/UninstallerKind.cs:74`, `:215`) e de legenda (`src/Tranal.Uninstaller/Core/Appearance/ColorLegend.cs:178`): **nada gera instalador** para app nao registrado. `docs/PARIDADE.md` §8 ("Fora de escopo") nao registra decisao sobre este item. Binario do concorrente: `UninstallerType_SimpleDelete` — nao confirmado no binario (so `.resx`) |

### 2.4 Interface, filtro e automação

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 53 | **Filtro avançado por condições**: método de comparação × propriedade alvo, com negação | literal "Based on pre-defined rules" (`bcu.strings.txt:8428`), "Advanced filtering" (`bcu.inventario.md:16`); `FilterComparisonMethod_*` é do motor `UninstallTools.dll`, fora deste dump | **PARCIAL** - `src/Tranal.Uninstaller/Core/Appearance/ProgramFilters.cs:147` (`ProgramFilterSet`: tipo, tamanho, unidade, editora, comentario, grupo, uso raro), `:212` (prefixo `regex:`) e `:221` (`ExcludedTerms`, a negacao); consumo na lista em `src/Tranal.Uninstaller/MainWindow.xaml.cs:292`. **Nota**: o filtro e por **campos fixos**, nao por condicao "**metodo de comparacao × propriedade alvo**" — o `src/Tranal.Uninstaller/Core/Rules/RuleEngine.cs:43` e motor de regra de **remocao**, nao de filtro de lista. Binario do concorrente: `bcu.strings.txt:8428` ("Based on pre-defined rules") |
| 54 | Filtros de tipo **Incluir** e **Excluir**, incluir roda antes, excluir sobrepõe | literal "Include all" (`bcu.inventario.md:172`), "All files\|*.*" (`:18`) | OK — `src/Tranal.Uninstaller/Core/ExcludeList.cs` |
| 55 | Listas de desinstalação salvas em **`.bcul`**, usáveis pela linha de comando | literal "Save an Uninstall List...B" (`bcu.inventario.md:332`), "Open an Uninstall List..." (`:250`) | OK — `src/Tranal.Uninstaller/Core/Automation/BatchList.cs` + `/batch` |
| 56 | **Treemap** do tamanho estimado de cada aplicativo | `showTreemapToolStripMenuItem` (`bcu.strings.txt:5473`), `JSimpleTreeMap.TreeMap, SimpleTreeMap` (`:8082`) | OK — `src/Tranal.Uninstaller/Core/Appearance/TreemapModel.cs` |
| 57 | **Legenda de cores** das marcações | `showColorLegendToolStripMenuItem` (`bcu.strings.txt:5440`), tipo `BulkCrapUninstaller.Controls.ListLegend.resources` (`bcu.identificadores.txt:11`) | OK — `src/Tranal.Uninstaller/Core/Appearance/ColorLegend.cs` |
| 58 | **Classificação do usuário** (0 a 4 estrelas, sincronizada com servidor) | `BulkCrapUninstaller.Functions.Ratings` (`bcu.strings.txt:6373`), `checkBoxRatings` (`:6383`), `FetchRatings` (`:6378`), `UploadRatings` (`:6374`) | **PARCIAL** - `src/Tranal.Uninstaller/Core/Appearance/ProgramRating.cs:13` (0 a 5 estrelas, `Max = 5` em `:19`), gravada em `src/Tranal.Uninstaller/Core/ProgramCustomization.cs:463`; editor na tela em `src/Tranal.Uninstaller/Ui/Appearance/AppearanceView.xaml.cs:307`; coluna na lista em `src/Tranal.Uninstaller/Core/ProgramColumns.cs:279`. **Nota**: a classificacao e **local** (0 a 5 estrelas, nao 0 a 4) e **nao ha sincronizacao com servidor** — nenhuma chamada de rede no modulo. Binario do concorrente: `bcu.strings.txt:6373` (`BulkCrapUninstaller.Functions.Ratings`) |
| 59 | **Verificação de certificado** do desinstalador (verde = verificado, azul = não) | `checkBoxCertTest` (`bcu.strings.txt:6894`), `get_PropertiesWindow_Table_ErrorNoCertificate` (`:4761`) | OK — `src/Tranal.Uninstaller/Core/Discovery/UninstallerCertificate.cs` |
| 60 | **Cópia avançada para a área de transferência** com padrão de formato composto | tipo `BulkCrapUninstaller.Forms.AdvancedClipboardCopyWindow.resources` (`bcu.strings.txt:6233`), `OpenAdvancedClipboardCopy` (`:7093`) | OK — `src/Tranal.Uninstaller/Core/Appearance/ProgramClipboard.cs` |
| 61 | **Copiar**: informações completas, nome do programa, GUID, caminho do registro, sequência de desinstalação | `fullInformationCopyContextMenuStripItem` (`bcu.strings.txt:5429`) | OK — `src/Tranal.Uninstaller/Core/Appearance/ProgramTextFormatter.cs` |
| 62 | **Pesquisar on-line** o programa (Google, AlternativeTo, FossHub, SourceForge, GitHub, FileHippo) | `lookUpOnlineToolStripMenuItem` (`bcu.strings.txt:5450`), literal "Search on line" (`:9140`) | OK — `src/Tranal.Uninstaller/Core/WebSearch.cs` |
| 63 | **Gerenciador de inicialização** próprio, com entrada/saída de itens de Run/RunOnce/Service/Task/BrowserHelper | `StartupManagerWindow` (`bcu.strings.txt:7003`) | OK — `src/Tranal.Uninstaller/Core/Tools/AutorunManager.cs` + `src/Tranal.Uninstaller/Core/Management/AppPermissionManager.cs` |
| 64 | **Renomear entrada** da lista | `renameContextMenuStripItem` (`bcu.strings.txt:5414`), `get_MainWindow_Rename_Title` (`:4539`) | **OK** - `src/Tranal.Uninstaller/Ui/RenameWindow.xaml.cs:18` (dialogo), acionado em `src/Tranal.Uninstaller/MainWindow.xaml.cs:1358`; o nome novo e o que a lista mostra em `src/Tranal.Uninstaller/Core/ProgramColumns.cs:260` (`custom.DisplayName`). Binario do concorrente: `bcu.strings.txt:5414` (`renameContextMenuStripItem`) |
| 65 | **Excluir a entrada do registro** (some da lista, sem desinstalar) | `deleteRegistryEntryContextMenuStripItem` (`bcu.strings.txt:5430`) | OK — `src/Tranal.Uninstaller/Core/RegistryEditing/RegistryEditor.cs` |
| 66 | **Assumir a propriedade** de um item | `TakeOwnership` (`bcu.strings.txt:5859`), `takeOwnershipToolStripMenuItem` (`:5474`) | **OK** - modulo de propriedade `Core/Management/FileOwnership.cs:60`; comando de linha de comando `src/Tranal.Cli/OwnershipCommands.cs:17` |
| 67 | **Abrir a chave no Regedit** | `openKeyInRegeditToolStripMenuItem` (`bcu.strings.txt:5505`) | **OK** - `src/Tranal.Uninstaller/Core/Management/SystemNavigator.cs:58` (`RegistryEditor`: `regedit.exe "<chave>"`), aberto pelo menu da lista em `src/Tranal.Uninstaller/MainWindow.xaml.cs:1238`. Binario do concorrente: `bcu.strings.txt:5505` (`openKeyInRegeditToolStripMenuItem`) |
| 68 | **Abrir "Programas e Recursos"** e **"Recuperação do Sistema"** | `openProgramsAndFeaturesToolStripMenuItem` (`bcu.strings.txt:5485`), `openSystemRestoreToolStripMenuItem` (`:5451`) | **OK** - `src/Tranal.Uninstaller/Core/Management/SystemNavigator.cs:69` (`AppsAndFeatures` -> `appwiz.cpl`) e `:73` (`SystemRestore` -> `SystemPropertiesProtection.exe`), ambos no menu da lista (`src/Tranal.Uninstaller/MainWindow.xaml.cs:1238`). Binario do concorrente: `bcu.strings.txt:5485` (`openProgramsAndFeaturesToolStripMenuItem`) |
| 69 | **Exportar para script de desinstalação em lote** | `exportToABatchUninstallScriptToolStripMenuItem` (`bcu.strings.txt:5509`), filtro "Batch file\|*.bat" (`bcu.inventario.md:37`) | OK — `src/Tranal.Uninstaller/Core/Reporting/BatchScriptWriter.cs` |
| 70 | **Criar script PowerShell para remover Store Apps** | `exportStoreAppsToPowerShellRemoveScriptToolStripMenuItem` (`bcu.strings.txt:5508`) | **OK** - `src/Tranal.Uninstaller/Core/Reporting/StoreAppScriptWriter.cs:36` (`Build`) e `:110` (`Write`); tela em `src/Tranal.Uninstaller/Ui/Modules/ModuleViews.cs:187` e CLI em `src/Tranal.Cli/ParityCommands.cs:162` (`/storeapp-script`, flag em `:33`). Binario do concorrente: `bcu.strings.txt:5508` (`exportStoreAppsToPowerShellRemoveScriptToolStripMenuItem`) |
| 71 | Exportar dados em **XML** com cabeçalho fixo (editora, versão, data, tamanho, registro, tipo, cadeia de desinstalação, cadeia silenciosa, comentários) | `get_StrExportHeader` (`bcu.strings.txt:5935`), literal "Publisher \| Version \| Date \| Size \| Registry \| Uninstaller type \| Uninstall string \| Quiet uninstall string \| Comments" (`bcu.inventario.md:297`) | **PARCIAL** - `src/Tranal.Uninstaller/Core/Exporters.cs:157` (`ToXml`: elemento `TranalUninstaller` com atributo `exported` e um `Entry` por programa, com os elementos das colunas escolhidas); exporta pela tela (`src/Tranal.Uninstaller/MainWindow.xaml.cs:1629`) e pela CLI (`src/Tranal.Cli/ExtraCommands.cs:258`). **Nota**: **nao ha cabecalho fixo** com os 9 campos do BCU — o XML sai com as colunas que o usuario escolheu, e `Tamanho` e `Desinstalador silencioso` ficam de fora do conjunto padrao (`src/Tranal.Uninstaller/Core/Exporters.cs:26`). Binario do concorrente: `bcu.strings.txt:5935` (`get_StrExportHeader`) |
| 72 | Painel lateral de configurações (filtros e realces) | tipo `BulkCrapUninstaller.Controls.PropertiesSidebar.resources` (`bcu.identifiers.txt:12`) | OK — `src/Tranal.Uninstaller/Ui/Appearance/SettingsSidebar.xaml.cs` |
| 73 | **Agrupamento** de itens pela coluna de ordenação | `checkBoxViewGroups` (`bcu.strings.txt:6588`), literal "Grouping of most of the columns uses smart filtering." (`:7490`) | **OK** - `src/Tranal.Uninstaller/Core/Appearance/GroupingModel.cs:80` (`Group`, agrupa pelo valor da coluna escolhida) e `:120` (ordem por tamanho do grupo); consumido na tela em `src/Tranal.Uninstaller/Ui/Appearance/AppearanceView.xaml.cs:197`. Binario do concorrente: `bcu.strings.txt:6588` (`checkBoxViewGroups`) |
| 74 | **Modo para daltônicos**, escalonamento por DPI, tema do sistema | `checkBoxColorblind` (`bcu.strings.txt:4261`), `checkBoxDpiaware` (`:4703`) | **PARCIAL** - `src/Tranal.Uninstaller/Core/Appearance/ColorBlindPalette.cs:157` (`Adapt`) e `:90` (`IsAdjusted`), aplicado em `src/Tranal.Uninstaller/Theming/ThemeManager.cs:159`; escolha do modo na tela em `src/Tranal.Uninstaller/Ui/OptionsWindow.xaml.cs:203` e preferencia em `src/Tranal.Uninstaller/Core/Settings/AppSettings.cs:33`; tema do Windows em `src/Tranal.Uninstaller/Theming/ThemeProbe.cs:11` (chamado em `src/Tranal.Uninstaller/App.xaml.cs:65`). **Nota**: modo daltônico e tema do sistema existem, mas **nao ha escalonamento por DPI** — `grep "Dpi/PerMonitor/GetDpi" em src/` -> **0 hits**. Binario do concorrente: `bcu.strings.txt:4261` (`checkBoxColorblind`) / `:4703` (`checkBoxDpiaware`) |
| 75 | **Gerenciador de desinstaladores relacionados** (`RelatedUninstallerAdder`) | tipo `BulkCrapUninstaller.Forms.RelatedUninstallerAdder.resources` (`bcu.identificadores.txt:30`) | **PARCIAL** - `src/Tranal.Uninstaller/Core/Discovery/RelatedProgramFinder.cs:53` (`Find`) e o aviso `src/Tranal.Uninstaller/Core/UninstallAdvisories.cs:244`, exibido em `src/Tranal.Uninstaller/Ui/UninstallWizardWindow.xaml.cs:493`. **Nota**: a correlacao e a lista existem, mas **nao ha gerenciador** para adicionar/remover desinstaladores relacionados da tarefa — o BCU tem a tela `RelatedUninstallerAdder`; aqui so ha o aviso. Binario do concorrente: `bcu.identificadores.txt:30` (tipo `RelatedUninstallerAdder.resources`) |
| 76 | Atalhos de teclado e cópia de informações da entrada | `fullInformationCopyContextMenuStripItem` (`bcu.strings.txt:5429`), `GlobalHotkeys` (`:6715`), `GetHotkeyList` (`:6918`) | **PARCIAL** - `src/Tranal.Uninstaller/Ui/Appearance/ProgramClipboard.cs:39` (`Copy`), acionado em `src/Tranal.Uninstaller/MainWindow.xaml.cs:833`; atalhos de teclado da janela em `src/Tranal.Uninstaller/MainWindow.xaml.cs:92` (`PreviewKeyDown`) e `:1676` (tratador). **Nota**: **nao ha hotkey global** registrado pelo processo — `grep "RegisterHotKey/Hotkey/GlobalHotkey" em src/` -> **0 hits**. Binario do concorrente: `bcu.strings.txt:5429` (`fullInformationCopyContextMenuStripItem`) |
| 77 | Ver **mudanças** (tweaks) detectadas pelo monitor | `checkBoxTweaks` (`bcu.strings.txt:6434`), literal "Show tweaks" (`bcu.inventario.md:360`) | OK — `src/Tranal.Uninstaller/Core/Modules/InstallMonitor.cs` + `src/Tranal.Uninstaller/Core/Tracing/` |
| 78 | **Atualização automática** (baixa e instala após reiniciar) | literais "Check for updates" (`bcu.inventario.md:66`), "BCU has been updated to v{0}!" (`:39`) | OK — `src/Tranal.Uninstaller/Core/Updates/AutoUpdateClient.cs` |
| 79 | Estatísticas anônimas de uso (opt-in) | `checkBoxSendStats` (`bcu.strings.txt:6651`), `UsageTracker` (`:5982`), `UsageTrackerSendData` (`:3865`), `hBulkCrapUninstaller.Functions.Tracking.UsageTracker` (`:7443`) | **FORA POR DECISAO** - Decisao registrada: `docs/auditoria/BINARIO.md:128` — "Base de logs **online** do Revo / Decisao de produto: o Tranal e offline, sem cadastro e sem telemetria"; reforcada em `docs/auditoria/MATRIZ.md:529` ("Privacidade (sem cadastro, sem telemetria...)"). `grep "telemetr/UsageTracker" em src/` -> **0 hits**. Binario do concorrente: `bcu.strings.txt:6651` (`checkBoxSendStats`) |
| 80 | Linha de comando **antes/depois** e "executar aplicativos externos" (desabilitável) | `checkBoxEnableExternal` (`bcu.strings.txt:5310`), `groupBoxExternal` (`:5311`) | **PARCIAL** - `src/Tranal.Uninstaller/Core/Automation/UninstallPlan.cs:170` (`ResolveOptions`) e `:205` (`Build`); tela de edicao em `src/Tranal.Uninstaller/Ui/UninstallCommandsWindow.xaml.cs:18` (`PreCommand`/`PostCommand`); gravacao por programa em `src/Tranal.Uninstaller/Core/ProgramCustomization.cs:72` (`PreCommand`), `:79` (`PostCommand`) e `:297` (`SetCommands`). **Nota**: os comandos antes/depois existem e sao executados, mas **nao ha a opcao "executar aplicativos externos"** desabilitavel — `grep "External" em src/Tranal.Uninstaller/Core/Settings/AppSettings.cs` -> **0 hits**. Binario do concorrente: `bcu.strings.txt:5310` (`checkBoxEnableExternal`) |

### 2.4.1 Funcionalidades adicionais descobertas no binário (não constavam no documento)

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 81 | **Hotkeys globais** do sistema, registrados pelo processo | `GlobalHotkeys` (`bcu.strings.txt:6715`), `SetupHotkeys` (`:131`), `GetHotkeyList` (`:6918`), `HotkeyEntry` (`:7134`), `QKlocman.Subsystems.GlobalHotkeys, KlocTools` (`:8049`) | **AUSENTE** - `grep "RegisterHotKey/Hotkey/GlobalHotkey" em src/` -> **0 hits**. Binario do concorrente: `bcu.strings.txt:6715` (`GlobalHotkeys`) |
| 82 | **Colocar o PC para dormir** quando a tarefa termina, com contagem regressiva | `checkBoxFinishSleep` (`bcu.strings.txt:5854`), `PutToSleep` (`:5855`), `sleepTimer` (`:6021`), `SleepControls` (`:6508`), `get_UninstallProgressWindow_StatusPuttingToSleepInSeconds` (`:6199`), literal "Put PC to sleep when done" (`bcu.inventario.md:298`) | **OK** - modulo das acoes de energia `Core/Tools/PowerPlanner.cs:41`; o agendamento com a contagem `Core/Tools/PowerPlanner.cs:102`; o cancelamento `Core/Tools/PowerPlanner.cs:106` |
| 83 | **Relatório de erro automático** (crash report) com envio pela web | `NBug.Core.Submission` (`bcu.strings.txt:5611`), `NBugConfigurator` (`:6172`), `NBugDatabaseSenderWrapper` (`:6037`), `SetupNBug` (`:5052`), literal "Failed to send crash report: " (`:13083`) | **AUSENTE** - `grep "CrashReport/NBug/EnviarRelatorio/SubmitCrash" em src/` -> **0 hits**. O Tranal grava log local (`src/Tranal.Uninstaller/FatalLog.cs:13`, escrita em `:32`) e coleta dumps do Windows (`src/Tranal.Uninstaller/Core/Modules/CrashDumpCollector.cs:116`, painel em `src/Tranal.Uninstaller/Ui/Tools/MaintenanceViews.cs:301`), mas **nao gera relatorio de erro automatico nem envia pela web**. Binario do concorrente: `bcu.strings.txt:5611` (`NBug.Core.Submission`) |
| 84 | **Nota personalizada por entrada** da lista | `CustomNoteDialog` (`bcu.strings.txt:5035`), tipo `BulkCrapUninstaller.Forms.CustomNoteDialog.resources` (`:6213`), literal "Custom Note for " (`:7616`) | OK — `src/Tranal.Uninstaller/Ui/CommentWindow.xaml.cs` + `src/Tranal.Uninstaller/Core/ProgramCustomization.cs` |
| 85 | **Desabilitar a proteção** de um item marcado como protegido | literais "Disable protection" (`bcu.strings.txt:7529`), "Modify protected item" (`:9131`), "Disable protection of uninstallers marked as \"protected\"" (`:8782`) | **OK** - `src/Tranal.Uninstaller/Core/Management/ProtectedUninstallTag.cs:85` (`Clear`, apaga o valor `NoRemove`); exposto pela CLI em `src/Tranal.Cli/RegistryCommands.cs:353` (`/noremove /disable`, flag em `:43`). Binario do concorrente: `bcu.strings.txt:7529` ("Disable protection") |
| 86 | **Modificar a instalação** (reparo / alterar instalação) | literal "Modify application" (`bcu.strings.txt:9070`) | **PARCIAL** - `src/Tranal.Uninstaller/Core/Discovery/MsiProductCatalog.cs:265` (`Repair`, `msiexec /fa`) e `:122` (`TryBuildRepairCommand`). **Nota**: o reparo existe no nucleo, mas **nao tem consumidor** — `grep "MsiProductCatalog.Repair" em src/` -> 1 hit (a definicao) e `TryBuildRepairCommand` -> 3 hits, todos em `src/Tranal.Tests/QueueAndStartupTests.cs:231`; a tela de Descoberta so lista os produtos MSI (`src/Tranal.Uninstaller/Ui/Discovery/DiscoveryWindow.xaml.cs:115`) sem botao de reparo. Binario do concorrente: `bcu.strings.txt:9070` ("Modify application") |
| 87 | **Desabilitar a inicialização automática** do programa | literal "Disable autostart" (`bcu.strings.txt:8138`) | **OK** - `src/Tranal.Uninstaller/Core/Management/AppPermissionManager.cs:207` (desabilita a entrada de inicializacao do programa escolhido) e `:157` (`For`, correlaciona servico/inicializacao/menu/tarefa do programa); painel por programa em `src/Tranal.Uninstaller/Ui/Management/ManagementView.xaml.cs:243` (botao Desabilitar) e `src/Tranal.Uninstaller/Ui/Tools/TopmostOverlayWindow.xaml.cs:284`. Binario do concorrente: `bcu.strings.txt:8138` ("Disable autostart") |
| 88 | **Auto-ajustar a largura de todas as colunas** | literal "Auto-resize all columns" (`bcu.strings.txt:8114`) | **OK** - a conta da largura `Core/ProgramColumns.cs:269`; o comando de auto-ajuste `MainWindow.xaml.cs:576` |
| 89 | **Adicionar os selecionados como filtro** (a seleção vira critério) | `toolStripButtonAddSelectedAsFilters` (`bcu.strings.txt:6620`), `toolStripButtonAddSelectedAsFilters_Click` (`:5235`) | **OK** - a selecao transformada em filtro `Core/Appearance/ProgramFilters.cs:233` |
| 90 | **Limpar caches** da aplicação | literal "Clear caches" (`bcu.strings.txt:7412`), "Failed to delete cache: " (`bcu.strings.txt:13043`) | **AUSENTE** - `grep "ClearCache/LimparCache" em src/` -> **0 hits**; ha limpeza de cache de navegador (`src/Tranal.Uninstaller/Core/Tools/JunkCleaner.cs:239`, categoria `browser-cache`), mas **nenhuma acao de limpar o cache do programa que esta sendo desinstalado**. Binario do concorrente: `bcu.strings.txt:7412` ("Clear caches") |
| 91 | **Abrir a Limpeza de Disco do Windows** | literal "Start Disk Cleanup" (`bcu.strings.txt:8279`) | **OK** - `src/Tranal.Uninstaller/Core/Tools/SystemActions.cs:30` (`AbrirLimpezaDeDisco`, `cleanmgr.exe`); botao na tela em `src/Tranal.Uninstaller/Ui/Tools/ToolViews.cs:249` e CLI em `src/Tranal.Cli/InspectionCommands.cs:303` (o catalogo tambem lista `cleanmgr.exe` em `src/Tranal.Uninstaller/Core/Tools/WindowsToolsCatalog.cs:43`). Binario do concorrente: `bcu.strings.txt:8279` ("Start Disk Cleanup") |
| 92 | **Saída serializada em JSON** (além de XML/CSV) | `System.Text.Json` (`bcu.strings.txt:5754`), `JsonSerializer` (`:6119`), `JsonSerializerOptions` (`:6565`), `objToJsonserialize` (`:4909`) | **OK** - `src/Tranal.Uninstaller/Core/Exporters.cs:126` (`ToJson`); tela em `src/Tranal.Uninstaller/MainWindow.xaml.cs:1630` e CLI em `src/Tranal.Cli/ExtraCommands.cs:259` (`/json`). Binario do concorrente: `bcu.strings.txt:5754` (`System.Text.Json`) |
| 93 | **Compressão Brotli** em backups/arquivos gerados | `System.IO.Compression.Brotli` (`bcu.strings.txt:5103`), `BrotliStream` (`:5394`), `BrotliCompress` (`:6650`) | **OK** - a compressao Brotli do backup `Core/Recovery/LeftoverBackup.cs:209`; a preferencia da compressao `Core/Settings/AppSettings.cs:401` |
| 94 | **Configurações portáteis** (settings gravados junto do executável) | `PortableSettingsProvider` (`bcu.strings.txt:5939`), assembly `PortableSettingsProvider, Version=1.2.1.0` (`:7191`), `labelPortable` (`:4490`), `get_StrIsPortable` (`:4491`) | OK — `src/Tranal.Uninstaller/Core/PortableMode.cs` + `src/Tranal.Uninstaller/Core/Settings/AppSettings.cs` (`portableDataFolder`) |
| 95 | Filtro que **esconde o que foi publicado pela Microsoft** | literais "Hide published by Microsoft" (`bcu.inventario.md:160`), "Published by Microsoft" (`:296`), "Filter out everything that was published by Microsoft. It's that simple." (`:159`) | **PARCIAL** - `src/Tranal.Uninstaller/Core/Appearance/ProgramFilters.cs:189` (`PublisherFilter`, filtro por editora, consumida em `:640`). **Nota**: existe o filtro **generico por editora** (digitar "Microsoft" filtra), mas **nao ha o filtro dedicado** "esconder o publicado pela Microsoft" — nenhum `Microsoft` fixo no codigo do filtro. Binario do concorrente: `bcu.inventario.md:160` ("Hide published by Microsoft") |
| 96 | Filtro por **regras pré-definidas** | literal "Based on pre-defined rules" (`bcu.strings.txt:8428`) | OK — `src/Tranal.Uninstaller/Core/Rules/RuleEngine.cs` + `src/Tranal.Uninstaller/Core/Rules/BuiltinRules.cs` |
| 97 | Remoção de um item **pelo MsiExec a partir do menu de contexto** | `uninstallUsingMsiExecContextMenuStripItem` (`bcu.strings.txt:5411`), literal "&Uninstall using MsiExec" (`:8196`) | OK — `src/Tranal.Uninstaller/Core/Discovery/MsiProductCatalog.cs` monta `msiexec /x {GUID}` |

### 2.5 Linha de comando

Extraída do manual oficial (seções 9.1–9.6) e das strings do
`BCU-console.dll`.

| Comando | Parâmetros | Efeito |
|---|---|---|
| `BCUninstaller.exe` | `[drive:][path]filename` | abre a lista de desinstalação indicada |
| `BCUninstaller.exe` | `/sm` (`/startupmanager`) | abre **só** o Gerenciador de Inicialização, sem enumerar aplicativos — parte muito mais rápido |
| `BCU-console.exe` | `uninstall <arquivo.bcul> [/Q] [/U] [/V] [/J=<nível>]` | desinstala conforme a lista |
| `BCU-console.exe` | `export <arquivo.xml> [/Q] [/U] [/V]` | exporta os dados em XML |
| | `/Q` | usa desinstaladores silenciosos sempre que possível |
| | `/U` | modo não assistido (sem confirmação) — o manual avisa em caixa alta |
| | `/J=<Level>` | limpa sobras após desinstalar; padrão `VeryGood`; níveis `VeryGood, Good, Questionable, Bad, Unknown` |
| | `/V` | log detalhado |
| `BCU-console.exe` | `/N` | **execução de simulação** (dry run) — visível nas strings do binário |
| `BCU-console.exe` | `/F=<Formato>` | formato de saída `json` ou `xml` (padrão texto puro) — visível nas strings do binário |
| `StoreAppHelper.exe` | `/query` · `/uninstall <FullName>` | Store Apps do usuário atual |
| `SteamHelper.exe` | `u[ninstall] [/s[ilent]] <AppID>` · `i[nfo] <AppID>` · `l[ist]` · `steam` | Steam |
| `OculusHelper.exe` | `/query` · `/uninstall <CanonicalName>` | Oculus |
| `UninstallerAutomatizer.exe` | `<UninstallerType> [/K] <UninstallCommand>` | automatiza desinstaladores NSIS travados; `/K` mata o processo se a automação falhar |

Códigos de retorno: `0` sucesso · `1`/`10022` argumentos inválidos ·
`1223` cancelado pelo usuário · `1627`/`59` erro inesperado.

---

## 3. Mecanismos notáveis

### 3.1 A classificação do desinstalador é o eixo do produto

O BCU não trata "desinstalar" como uma operação só: ele **classifica** o
desinstalador (`UninstallerType_*`) e escolhe a estratégia por tipo —
MsiExec recebe o GUID, NSIS e Inno Setup recebem tratamento de silencioso,
Store App vai por outro caminho, Windows Feature e Windows Update têm
caminho próprio, Chocolatey é desinstalado pelo próprio Chocolatey
(`ChocolateyFactory_UninstallInChocolateyJunkName`).

O Tranal hoje **não classifica** o desinstalador: ele roda a cadeia de
desinstalação como veio do registro e varre sobras pelo nome. Essa é a
maior lacuna estrutural que a auditoria do BCU revela.

### 3.2 O modelo de confiança é a parte mais valiosa do BCU

O Revo resolve isso com **negrito**: só os itens em negrito são removidos.
O BCU resolve com **cinco níveis + justificativa textual por item**:

```
ConfidencePart_ProductNamePerfectMatch   →  "Nome do produto combinou perfeitamente"
ConfidencePart_CompanyNameDidNotMatch     →  "O nome da empresa não coincidiu"
ConfidencePart_DirectoryStillUsed         →  "O diretório é usado por outros aplicativos instalados"
ConfidencePart_AllSubdirsMatched         →  "O diretório é, ou estará vazio após a remoção de lixo"
```

A regra do Tranal é mais simples e mais segura (só confiança **alta** vem
marcada; média e baixa vêm desmarcadas), mas **não mostra o porquê**. O
usuário do Tranal vê "não marcado" e não sabe se o motor errou ou se foi
cuidado.

### 3.3 A atribuição de sobras é por **padrão do desinstalador**, não só por nome

O BCU não procura "pasta com o nome do programa": ele lê a cadeia de
desinstalação e procura itens **mencionados explicitamente** nela
(`ConfidencePart_ExplicitConnection`). É por isso que ele acerta sobras
cujo nome não tem nada a ver com o programa (o executável do jogo em
`AppData\Local\Temp`, o driver em `Windows\System32\drivers`).

### 3.4 Código aberto: a referência de implementação

Por ser Apache 2.0 com código público, o BCU permite responder à pergunta
que nenhum outro concorrente responde: **como** se faz a varredura de
sobras sem quebrar o sistema, **como** se classifica o desinstalador, **como**
se geram desinstaladores silenciosos, **como** se trata desinstalador
travado. É a fonte para as decisões de arquitetura — não para copiar
código (que tem licença própria e obligations de atribuição).

---

## 4. Interface

A janela principal é WinForms com:

- **lista de aplicativos** (`ObjectListView`) com 18 colunas:
  Nome, Editor, Classificação do usuário, Versão, Data da instalação, Tamanho,
  Inicialização, 64bit, Comando de desinstalação, Sobre o URL, Fonte da
  instalação, Local da instalação, Tipo de desinstalador, Componente do
  sistema, Protegido, Chave do registro, Código do produto, Comando de
  desinstalação silenciosa;
- **painel lateral** de configurações (filtros e realces);
- **treemap** acima da barra de status;
- **legenda de cores** no canto inferior direito;
- **barra de status** com contagens (`{0} desinstaladores no total, {1}`).

Menus: Arquivo · Visualizar · Desinstalar · Ferramentas · Configurações ·
Ajuda.

---

## 5. O que o Tranal NÃO faz hoje

Comparação com o estado **medido** do Tranal (`docs/PARIDADE.md` e o código
em `src/Tranal.Uninstaller/Core/`).

### 5.1 Lacunas estruturais (mudam a arquitetura)

| # | Recurso do BCU | Tranal hoje | Prioridade |
|---|---|---|---|
| 1 | ✅ **Classificar o desinstalador** (12 tipos implementados em `Core/UninstallerKind.cs`: MsiExec, Inno, NSIS, InstallShield, SDBInst, Steam, Store, Chocolatey, Oculus, PowerShell, Simple Delete e desconhecido; Windows Feature e Windows Update dependem de detectores que ainda não existem) | não classifica; roda a cadeia do registro e varre por nome | — feita |
| 2 | **Atribuir sobras pelo desinstalador** (item explicitamente citado na cadeia) e não só pelo nome | varre por nome e variantes | 🔴 alta |
| 3 | **Níveis de confiança com justificativa** (5 níveis + motivo por item) | 3 níveis, sem justificativa visível | 🔴 alta |
| 4 | **Desinstalação silenciosa com geração de silencioso** (inclusive por automação de interface) | modo `builtin` roda o desinstalador como veio | 🔴 alta |
| 5 | **Filtro avançado por condições** (Regex, 6 métodos de comparação, alvo = propriedade, negação, Incluir/Excluir) | filtros rápidos + popup de filtros | 🟡 média |
| 6 | **Colunas**: o BCU tem 18, o Tranal tem 14 | ✅ implementadas: Tipo de desinstalador e Código do produto. Faltam Desinstalador silencioso, Localização do desinstalador, Fonte da instalação, Componente do sistema, Protegido, Inicialização, Classificação do usuário | 🟡 média |
| 7 | **Detecção de fontes além do registro**: Store Apps, Windows Features, Windows Updates, Steam, Chocolatey, Scoop, Oculus, varredura de drives | só Windows Store Apps (`Core/Modules/WindowsApps.cs`, 122 pacotes MSIX) | 🟡 média |
| 8 | **Desinstalar via MsiExec** pelo GUID | não | 🟡 média |
| 9 | **Desinstalação simultânea** com prevenção de colisão | não | 🟡 média |
| 10 | **Desinstalar manualmente**: pular o desinstalador e listar arquivos/chaves para apagar à mão | não | 🟡 média |

### 5.2 Lacunas de recursos

| Recurso do BCU | Tranal hoje |
|---|---|
| Gerenciador de inicialização próprio com janela de desinstaladores | `Core/Tools/AutorunManager.cs` (346 itens) — gerencia, mas não integra o ciclo de desinstalação |
| Limpeza de pastas "Arquivos de Programas" (vazias e parcialmente desinstaladas) | não |
| Encontrar por janela (mira de arrastar e soltar) | `Core/Topmost/` tem overlay e janela flutuante — resolve o caso de uso, por outro caminho |
| Classificação do usuário (estrelas, sincronizada) | não |
| Verificação de certificado do desinstalador | não |
| Cópia avançada para a área de transferência (padrão composto) | tem exportação CSV/XML/HTML, não cópia com padrão |
| Exportar para script de desinstalação em lote | não |
| Criar script PowerShell para remover Store Apps | não |
| Treemap | não |
| Legenda de cores | não |
| Painel lateral de configurações | não |
| Agrupamento por coluna de ordenação | tem grupos do usuário, não agrupamento automático |
| Renomear entrada da lista | não |
| Excluir entrada do registro sem desinstalar | não |
| Assumir a propriedade | não |
| Abrir chave no Regedit / Programas e Recursos / Recuperação do Sistema | não |
| Comandos antes e depois da desinstalação | não |
| Pular / Encerrar / Rodar agora um desinstalador em execução | tem "Kill the Process and Continue" e encerramento forçado, mas não o painel de progresso com controle manual |
| Abortar a tarefa inteira sem matar o desinstalador em execução | não |
| Monitorar silencioso atrás de popups (daemon de automatização) | não |
| Simulação (dry run) | **tem e é melhor**: `/scan` é dry-run real na CLI |
| Detecção de app não registrado + varredura de drives | não (e o `PARIDADE.md` §8 registra por que foi deixado de fora) |
| Geração de desinstalador simples para app órfão | não (decisão conscious do §8 do `PARIDADE.md`) |
| Modo daltônico / DPI / tema do sistema | tema próprio, sem escalonamento por DPI |
| Estatísticas anônimas | não |
| Debug window | não |

### 5.3 Onde o Tranal já é **melhor**

| Ponto | Tranal | BCU |
|---|---|---|
| Simulação antes de agir | `/scan` real na CLI, com a lista de sobras que seria removida | `checkBoxSimulate` (equivalente, mas não roteirizável) |
| Segurança da remoção | backup completo do registro **antes** do desinstalador nativo, com script de restauração; exclusões aplicadas até no backup; item em uso adiado para o reinício | backup opcional (pergunta), backup só do registro, sem script de restauração |
| Atalhos/junctions | `Core/SafeTraversal.cs` — o motor nunca atravessa um link e nunca apaga o alvo | não verificado nesta auditoria |
| Confiança | mais restritiva (só alta vem marcada; média e baixa nunca) | 5 níveis, mas o usuário precisa saber o que significam |
| Lixeira | sobras de arquivo vão para a lixeira e podem ser restauradas | idem (`MessageBoxes_BackupRegistryQuestion_Details`) |
| Idiomas | pt-BR e en-US com **601 chaves** em paridade | 24 idiomas, 1.066 chaves pt-BR — **o BCU tem mais tradução** |

### 5.4 Onde o Tranal já é **peior**

| Ponto | Tranal | BCU |
|---|---|---|
| Cobertura de fontes | registro + Store Apps | registro + MSI + drives + Store + WinFeatures + WinUpdates + Steam + Chocolatey + Scoop + Oculus + PowerShell |
| Automação por linha de comando | `/scan`, `/mu`, `/export`, `/columns` — **sem** desinstalação em lote por arquivo de lista | `BCU-console uninstall lista.bcul /Q /U` — desinstalação em lote roteirizável é o forte do BCU |
| Profundidade da lista | 12 colunas | 18 colunas |
| Desinstalador travado | encerramento automático | painel de progresso com pular/encerrar/rodar agora |

---

## 6. Fichos de evidência

Todos em `workspace/auditoria/bcu/evidencia/` (material de análise, **não**
versionado — o que fica no repositório é o fato derivado, como manda a
regra do `PARIDADE.md`).

| Arquivo | Conteúdo |
|---|---|
| `resx-ptBR-*.txt` (45 arquivos) | 1.066 chaves pt-BR por tela — a spec funcional |
| `manual-texto.txt` | manual oficial (427 KB) convertido em texto, 324 linhas |
| `strings-BCU-console.txt` | strings do console — inclui `/N` (dry run) e `/F=<formato>`, que não estão no manual |
| `pe-*.txt` | cabeçalho PE, assinatura e imports dos assemblies |

Evidência de engenharia reversa metadata do `UninstallTools.dll` (motor do
produto): .NET 8 gerenciado, PE32 anycpu, TypeDef 368, MethodDef 2.374,
Field 4.345, AssemblyRef 40, resources 5, SHA-256
`3c82ca47f91d92eeadc34620a35d4e26d7acaa91338314bb2bba00eef4a0cf79`.

---

## 7. Lacunas desta auditoria

### 7.1 Lacunas de **medição** (o que não foi possível medir)

| O que faltou | Por quê | Como fechar |
|---|---|---|
| **Execução real do BCU 6.3** | a instalação exige administrador e a auditoria corre sem elevação; a janela elevada não pode ser automatizada (UIPI) | pedir ao dono para instalar e rodar, ou capturar a tela na mão dele |
| **Comportamento real do gerador de desinstalador silencioso** | só se observa com o programa rodando | idem |
| **Sincronização da classificação de usuário** | depende do servidor de estatísticas do BCU | observar com o app rodando |
| **Resultado do `/J=<nível>`** em cada nível de confiança | idem | idem |
| **Códigos de saída reais** do `BCU-console` | só documentados, não medidos | rodar `BCU-console.exe` num sandbox |

### 7.2 Lacunas de **funcionalidade** (medidas no binário, ausentes no Tranal)

A lista completa e evidenciada está em **[`BINARIO-BCU.md`](BINARIO-BCU.md)** — 42
linhas, cada uma com o identificador do binário e o arquivo+linha do dump.
Resumo por impacto:

| Impacto | Quantidade | Exemplos |
|---|---|---|
| 🔴 alto | 5 | desinstalação simultânea com prevenção de colisão, daemon de automação, geração de silencioso por UI Automation, marcação `NoRemove`, catálogo de relacionados, hotkeys globais |
| 🟡 médio | 20 | ordenação inteligente, retry de silencioso falhado, sono ao terminar, modify/repair, confiança de inicialização, filtro avançado da lista, cabeçalho XML fixo |
| ⚪ baixo | 17 | JSON, Brotli, crash report, limpar caches, abrir Limpeza de Disco, auto-resize de colunas, daltônico/DPI |

> **Proveniência das evidências.** As chaves `Progress_*`, `UninstallerType_*`,
> `Junk_*_GroupName` e `Confidence_*` vêm do **Localisation Pack** (`.resx`
> traduzido) e **não** existem no dump do `BCUninstaller.dll`. Só o que foi
> confirmado no binário entra em `BINARIO-BCU.md`.

---

## 8. Decisões que este documento obriga

1. ✅ **O Tranal passa a classificar o desinstalador.** É o eixo do BCU e o que
   habilita quase tudo o mais (silencioso, MsiExec, Store App, órfão). A
   coluna "Tipo de desinstalador" vira a primeira coluna nova do catálogo.
   **Implementado** em `Core/UninstallerKind.cs` — 12 tipos, medidos nos 211
   programas desta máquina.
2. **A confiança ganha justificativa.** O `LeftoverConfidence` do Tranal
   (3 níveis) continua, mas passa a produzir uma frase por item — o mesmo
   que o BCU faz com `ConfidencePart_*`. A lista de sobras mostra o *porquê*.
3. **A atribuição de sobras usa a cadeia de desinstalação**, não só o nome.
   É o que separa "achou a pasta do programa" de "achou a pasta **dele**".
4. **Filtro Regex e lista de desinstalação em arquivo** entram na backlog
   depois do núcleo: são o que torna o Tranal roteirizável como o BCU.
5. **O que o Tranal já faz melhor fica protegido.** Backup antes do
   desinstalador nativo, exclusões aplicadas até no backup, `SafeTraversal`
   e `/scan` são melhores que qualquer coisa do BCU e não podem ser regredidos em
   nome de paridade.

---

*Auditoria gerada por medição direta dos artefatos. Toda linha deste
documento aponta para evidência em disco; o que não pôde ser verificado
está marcado como tal na seção 7.*
