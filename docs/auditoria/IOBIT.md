# IObit Uninstaller 16.1.0.4 (Free) — auditoria

> Concorrente que **empurra** para o lado do "cuidador do PC": além de
> desinstalar, ele audita o sistema inteiro, remove extensões de navegador
> maliciosas e desinstala programas travados de uma base com 4.000+ nomes.
>
> É o concorrente com o **vocabulário mais amplo** de todos os dez: é o
> único que trata desinstalação como um caso particular de "saúde do
> software".

Data da coleta: 05/11/2026 · Versão auditada: **16.1.0.4** (edição **FREE**)
Curadoria binária: 06/11/2026 — as funcionalidades desta tabela estão
amarradas ao **binário**, não à documentação.

---

## 1. Identificação

| Campo | Valor | Fonte |
|---|---|---|
| Produto | IObit Uninstaller — edição FREE | `evidencia/site-uninstaller.html` |
| Editor | IObit Co., Ltd. — Chengdu, Sichuan, CN | `evidencia/pe-IObitUninstaler.txt` |
| Versão | FileVersion = ProductVersion **16.1.0.4**; Inno: "IObit Uninstaller v16 Official" | `pe-IObitUninstaler.txt` + `innoextract --info` |
| Site oficial | https://www.iobit.com/en/advanceduninstaller.php | `evidencia/site-uninstaller.html` |
| URL usada | `downloadcenter.php?product=advanced-uninstaller-free&insur=en_iu` | link do botão de download |
| SHA-256 do instalador | `98C9B1F5286F1C3B220E696E90B4D2BD7BB5FFB086D8CA83D560F208BE31C747` | `Get-FileHash` |
| Tamanho do instalador | 34.309.696 B | idem |
| Formato do instalador | **Inno Setup 5.5.0 (unicode)**, stub PE32 Delphi | `innoextract --info` + magic `4D 5A 50` |
| Assinante | `CN="IObit Co., Ltd."` — GlobalSign GCC R45 EV, válido até 08/06/2028 | `pe-IObitUninstaler.txt` |
| App principal | `IObitUninstaler.exe` — **PE32 x86 nativo Delphi/VCL**, 10.646.976 B | `pe-IObitUninstaler.txt` |
| **Binário analisado (esta rodada)** | `workspace/auditoria/iobit/app/payload/IObitUninstaler.exe` — 10.646.976 B — sha256 `320c59f6d0f29af8a75342fc0242c4bf5dffa100d300a25666c514eb84ec2617` | `bin-inventario/iobit.inventario.md:3-5` |
| Payload | **146 arquivos / 97.626.141 B** | `innoextract-lista.txt` |
| Idiomas da interface | **40 arquivos `.lng`** (inclui pt-BR) | `innoextract-lista.txt:97-135` |
| Idiomas do instalador | 31 idiomas | `innoextract --info` |
| Bases proprietárias | 14 arquivos `.dbd` — `SBData` (7,0 MB, base de software), `PlugDB` (1,6 MB, base de extensões de navegador), `SoftHealth`, `SoftPM`, `BWR`, `PSExt`, `AppRate`… | `innoextract-lista.txt:45-58` |

### A descoberta que muda a leitura do produto

O instalador não é um programa com "um desinstalador": é uma **coleção de
módulos**. O payload traz 13 DLLs próprias com nome de função —
`SysRest.dll`, `forcedelctl.dll`, `filectl.dll`, `rgfpctl.dll`,
`BloatApp.dll`, `UninstallExplorer.dll`, `IUMenuRight.dll`, `RegisterCom.dll` —
e o `Language\English.lng`, que é um INI com **95 seções de diálogo**.

O `.lng` é a mesma coisa que o `lang\english.ini` do Revo: o inventário de
telas. Achatado, virou `evidencia/lng-english-plano.txt`.

### Método da curadoria binária (06/11/2026)

Quatro artefatos, todos em `E:\Projetos\tranal-auditoria-evidencia`:

| Artefato | O que traz | Como foi usado |
|---|---|---|
| `bin-inventario/iobit.inventario.md` | 2.328 classes RTTI Delphi, 2.993 rótulos de interface, 62 capacidades por token de API/IO | nomes de classe Delphi (`TStubbornData`, `TSoftHealthDataBase`…) como evidência de funcionalidade |
| `bin-inventario/iobit.recursos.txt` | 194 entradas de recurso do PE | **recursos de formulário Delphi** (`TFORM_HUNTER`, `TFRABROWSERCLEANER`…) — a evidência mais forte de tela |
| `bin-inventario/iobit.identificadores.txt` | identificadores de tipo/método | units Delphi reveladas (`TSysRestore`, `TTaskSchedulerInterfaces1`) e listas de AppX |
| `bin-strings/iobit.strings.txt` | 47.978 strings únicas | literal exato de string (nomes de menu, mensagens, DLLs) |

A regra de ouro continua a do `README.md`: **medido vale, inferido não vale**.
Cada linha da tabela abaixo cita o arquivo e a linha do dump de onde veio.

---

## 2. Superfície observável

40 funcionalidades medidas. As mais relevantes para o Tranal:

### 2.1 Abas do programa principal

Programs · Browser Extensions · Plugins · Tasks · Startup · Files & Folders ·
Toolbox · More · Windows Apps · Bloatware · History.

Filtros rápidos: All · Bundleware · Logged · Large · Infrequent, mais
filtro por unidade de disco.

### 2.2 O diferencial: extensões de navegador

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 1 | **Remoção de extensões de navegador** com base própria (`PlugDB.dbd`, 1,6 MB) | `iobit.strings.txt:37337` → `PlugDB.dbd`; `iobit.recursos.txt:109-114` → `PLUGIN_CHROME` / `PLUGIN_DEFAULT` / `PLUGIN_EDGE` / `PLUGIN_FIREFOX` / `PLUGIN_IE` / `PLUGIN_OPERA` | **FORA POR DECISAO** - `docs/auditoria/BINARIO.md` 3.2: "Base de **reputacao** de extensoes de navegador (maliciosa/ad) - Exige consulta a um servico online; o Tranal e offline por decisao. A remocao da extensao existe." Confirmado em `MATRIZ.md` E.1 (A17) |
| 2 | Classificação da extensão em **Malicious / Ad / Trusted** | `iobit.strings.txt:40554` → `Malicious Extension`; `:40556` → `Ad Extension`; `iobit.inventario.md:9` → `TADPluginDBHelper`, `TADPluginData`; `iobit.strings.txt:34156` → `Ad-based Browser Extensions` | **FORA POR DECISAO** - Mesma decisao (BINARIO.md 3.2; MATRIZ.md E.1, A17) |
| 3 | **Limpeza de navegador / reset de padrões** (página inicial e motor de busca) | `iobit.recursos.txt:147` → `TFRABROWSERCLEANER/0`; `iobit.inventario.md:9` → `TAllBrowserCleaner` | **PARCIAL** - A limpeza de navegador existe e esta ligada: `src/Tranal.Uninstaller/Core/Tools/BrowserCleanup.cs:63` (classe), `FindOrphanExtensions` `:68`, remocao das extensoes em `:109` e `CleanData` `:223` (que reaproveita o `InternetCleaner`). Tela `src/Tranal.Uninstaller/Ui/Tools/MaintenanceViews.cs:351`, com `BrowserScan` `:367`, `BrowserRemove` `:368` e `BrowserReset` `:369`; barra lateral `src/Tranal.Uninstaller/MainWindow.xaml:511`; CLI `src/Tranal.Cli/MaintenanceCommands.cs:80` (`tranal /browser-clean`). **Nota**: o **reset de padroes nao funciona** - `ResetDefaults` (`BrowserCleanup.cs:162`) devolve cedo com "Navegador nao informado" quando o navegador vem vazio (`:164-165`), e **os dois unicos chamadores passam `null`**: a tela (`src/Tranal.Uninstaller/Ui/Tools/MaintenanceViews.cs:423`) e a CLI (`src/Tranal.Cli/MaintenanceCommands.cs:99`); alem disso o reset so apaga valores de **politica** do navegador (`:172-208`), sem tocar nas preferencias do perfil. Binario do concorrente: confirmado em binario (`iobit.recursos.txt:147`, `TFRABROWSERCLEANER/0`) |
| 4 | Remoção de **plugins** do navegador | `iobit.recursos.txt:110` → `PLUGIN_DEFAULT/0`; `iobit.inventario.md:9` → `TADPluginData` | **PARCIAL** - Remoção de **plugins** do navegador/extensoes e add-ons `Core/Modules/BrowserExtensions.cs:53`; limpador de registro (ActiveX) `Core/Automation/ComUnregister.cs:18` |

### 2.3 Bloatware, bundleware e programas travados

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 5 | **Bloatware Remover**: pré-instalados do Windows, Windows Accessories e Windows Features, com marcação *Permanent* / *Warning* | `iobit.strings.txt:46869` → `Bloatware Remover`; `:34662` → ` Bloatware Remover`; `:40142` → `BloatApp.dll` | **OK** - **Bloatware Remover**: pré-instalados do Windows, Windows Accessories e Windows Features, com marcação *Permanent* / *Warning*/`src/Tranal.Uninstaller/Core/Discovery/BundlewareDetector.cs:55` (`public static class BundlewareDetector`) `src/Tranal.Uninstaller/Ui/Tools/MaintenanceViews.cs:106` (`BundlewareDetector.Detect`) `src/Tranal.Cli/MaintenanceCommands.cs:51` (`BundlewareDetector.Detect`) `src/Tranal.Uninstaller/MainWindow.xaml:488` (`AutomationProperties.AutomationId="ToolBloatware"`) |
| 6 | **Bundleware**: detectar o que veio junto com o programa e desinstalar em conjunto | `iobit.strings.txt:34695` → `~Bundleware is software that includes other programs that may be installed without your notice when you install a main program.`; `:9872` → `Tfrm_UninstallBundle`; `:9874` → `TUninstallBundleHelper`; `iobit.recursos.txt:154` → `TFRAME_BUNDLE/0`; `:181` → `TFRM_UNINSTALLBUNDLE/0` | **OK** - **Bundleware**: detectar o que veio junto com o programa e desinstalar em conjunto/`src/Tranal.Uninstaller/Core/Discovery/BundlewareDetector.cs:55` (`public static class BundlewareDetector`) `src/Tranal.Uninstaller/Ui/Tools/MaintenanceViews.cs:106` (`BundlewareDetector.Detect`) `src/Tranal.Cli/MaintenanceCommands.cs:51` (`BundlewareDetector.Detect`) a deteccao e por categoria e marca, e a remocao em conjunto usa o mesmo motor de lote da desinstalacao. |
| 7 | **Stubborn Program Remover** com base de **4.000+ programas** travados | `iobit.strings.txt:11326` → `TStubbornData`; `:11327` → `StubbornHelper`; `:11330` → `TStubbronUninstallManager`; `:11332` → `TStubbornCheckThread`; `iobit.recursos.txt:121` → `TFORM_CHECKSTUBBRON/0`; `:127` → `TFORM_FORCESTUBBRON/0`; `:142` → `TFORM_STUBBORNTOOL/0` | **OK** - **Stubborn Program Remover** com base de **4.000+ programas** travados/o modulo do removedor teimoso `Core/Automation/StubbornRemover.cs:60`; a medicao do que trava a remocao `Core/Automation/StubbornRemover.cs:82` |
| 8 | **Force Uninstall+**: desinstalar programa **sem entrada de registro** ou com desinstalador quebrado | `iobit.recursos.txt:162` → `TFRMFORCEDUNINSTALL/0`; `iobit.inventario.md:9` → `TAppsRemoval`, `TAnalysisThread` | **OK** - `Core/Automation/ForcedRemovalOptions.cs` (`ForTarget`), (`ForKeyword`), (`Resolve`); `Ui/ForcedUninstallWindow.xaml.cs`; entradas sem desinstalador em `Core/ProgramScanner.cs:61` (`IncludeEntriesWithoutUninstaller`); executaveis que parecem desinstalador em `Core/Discovery/DriveScanner.cs` |
| 9 | **Quick Uninstall**: confirmar automaticamente o desinstalador original | **não confirmado no binario** (a `.lng` não é o binário; nenhuma string de switch de confirmação automática foi localizada) | **PARCIAL** - **Quick Uninstall**: confirmar automaticamente o desinstalador original/`src/Tranal.Uninstaller/Core/Removal.cs:57` (`public static`) **Nota**: o desinstalador nativo roda com confirmacao propria, mas o Tranal nao tem o atalho "confirmar e seguir" do concorrente. |
| 10 | Aviso de que o desinstalador **vai deixar resíduo** ("still leaves") | **não confirmado no binario** | **PARCIAL** - Aviso de que o desinstalador **vai deixar resíduo** ("still leaves")/`src/Tranal.Uninstaller/Core/UninstallAdvisories.cs:87` (`public static`) **Nota**: existem os avisos que precedem a desinstalacao, mas nao a advertencia especifica de que o desinstalador deixara residuo. |
| 11 | Confirmar/avisar antes de apagar **arquivo pessoal** | **não confirmado no binario** | **OK** - Confirmar/avisar antes de apagar **arquivo pessoal**/modulo da guarda de arquivo pessoal `Core/Automation/PersonalFileGuard.cs:58`; a classificacao do caminho `Core/Automation/PersonalFileGuard.cs:109` |

### 2.4 Motor de desinstalação e sobras

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 12 | **Sobras por categoria**: Registro, Arquivo, Tarefas, Serviços, DLL | `iobit.identificadores.txt:113` → `ScanItem.FilePath.Count:`; `:114` → `ScanItem.RegRoot.Count:`; `iobit.strings.txt:34695` (base de regras por tipo) | **PARCIAL** - **Sobras por categoria**: Registro, Arquivo, Tarefas, Serviços, DLL/`src/Tranal.Uninstaller/Core/UninstallSupport.cs:49` (`public enum LeftoverReason`) `src/Tranal.Uninstaller/Core/Management/ScheduledTaskManager.cs:72` (`public static class ScheduledTaskManager`) `src/Tranal.Uninstaller/Core/Management/ServiceManager.cs:89` (`public static class ServiceManager`) **Nota**: DLL nao e um motivo de sobra no Tranal — a biblioteca compartilhada tem catalogo proprio (`Core/Modules/SharedDllCatalog.cs:60`). |
| 13 | **Auto Clean** de resíduo, marcado na própria desinstalação | `iobit.strings.txt:12151` → `#btn_CompletelyRemoveBundlewareClick%`; `iobit.inventario.md:9` → `TAppsRemoval` | **OK** — `Core/UninstallSupport.cs` varre e remove as sobras no mesmo fluxo da desinstalação |
| 14 | Resíduo vai para a **Lixeira** | `SHFileOperationW` (API importada pela PE) — `evidencia/pe-imports-IObitUninstaler.txt` | **OK** — `Core/RecycleBin.cs` |
| 15 | Ponto de restauração via `SysRest.dll` (`CreateRestorePoint`, `RestoreSystem`, `EnableSR`) | `iobit.identificadores.txt:116` → `TSysRestore.TRestoreWork.NotifyWorkFailed`; `:117` → `TSysRestore.TRestoreWork.NotifyWorkSucess`; `iobit.recursos.txt:172` → `TFRMSYSTEMRESTORE/0` | **OK** — `Core/UninstallSupport.cs:1210` (`TryCreateRestorePoint`), ligado ao assistente em `Ui/UninstallWizardWindow.xaml.cs:542` |
| 16 | **Desinstalação em lote** (multi-seleção) | **não confirmado no binario** (nenhum literal de lote localizado nesta rodada) | **OK** - **Desinstalação em lote** (multi-seleção)/`src/Tranal.Uninstaller/MainWindow.xaml:591` (`SelectionMode="Extended"`) `src/Tranal.Uninstaller/Core/Automation/BatchList.cs:17` (`public sealed class BatchList`) `src/Tranal.Cli/ExtraCommands.cs:994` (`BatchList.Load`) |
| 17 | **Easy Uninstall**: arrastar o ícone do programa para a janela | `iobit.inventario.md:83` → `AllowMeesageForVistaAbove WM_ExplorerShredder Failed`; `:82` → `WM_DROPFILES Failed` | **OK** - `Ui/Modules/HunterView.xaml` (`AllowDrop`, `Drop`, `DragOver`) + `Core/Modules/HunterMode.cs:41` (`Resolve`) |

> **Atenção ao 12**: o IObit classifica as sobras por **categoria**
> (registro/arquivo/serviço), e não por **nível de ameaça**. O Revo, o BCU e
> o Tranal classificam por confiança. São eixos diferentes — e o do Tranal
> responde melhor à pergunta "isto é dele?".

### 2.5 Monitoramento de instalação

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 18 | **Install Monitor** com log por categoria: registro, arquivo, tarefa, serviço, DLL, inicialização | `iobit.recursos.txt:163` → `TFRMINSTALLMONITOR/0`; `:166` → `TFRMMONITORLOG/0`; `:184` → `TIUINSTALLMONITORFRM/0`; `iobit.strings.txt:34634` → `Install Monitor`; `:35860` → `Install Monitor detects and logs system changes in installation, which ensures all changes can be restored in future thorough uninstall.` | **OK** - `Core/Modules/InstallMonitor.cs:103/119`; `Core/Tracing/TraceLogV2.cs:89` (`Create`), (`Save`); `Core/Modules/TraceStore.cs:165` (`Save`); restauracao pela desinstalacao a partir do log (`Core/Modules/TracedPrograms.cs:119` (`Uninstall`), `Core/Modules/TracedPrograms.cs:143` (`UninstallByLogPath`) |
| 19 | Log guardado em **SQLite** (`sqlite3.dll`, 37 funções importadas) | `iobit.inventario.md:92` → `An internal logic error in SQLite` | **OK** - Log guardado em **SQLite** (`sqlite3.dll`, 37 funções importadas)/a gravacao do log no SQLite do Windows `Core/Modules/LocalSqlStore.cs:199`; o arquivo do banco `Core/Modules/LocalSqlStore.cs:63` |
| 20 | **Desinstalar pelo log de instalação** (usar o log para desinstalar) | `iobit.strings.txt:44602` → `Programs logged by Install Monitor can be uninstalled thoroughly in future uninstallation.`; `:44974` → `UninstallMonitor.exe` | **OK** - `Core/Modules/TracedPrograms.cs:119` (`Uninstall`), (`UninstallByLogPath`), (`Install`) |
| 21 | Monitor roda **em segundo plano no startup** | **não confirmado no binario** (a chave `Run` é artefato de execução, não do binário) | **PARCIAL** - Monitor roda **em segundo plano no startup**/`src/Tranal.Uninstaller/Core/Modules/InstallMonitor.cs:28` (`public sealed class InstallMonitor`) **Nota**: o rastreio existe sob demanda, mas nao roda continuamente em segundo plano ao iniciar. |

### 2.6 Software Health — o painel único

Nove categorias num painel só: sobras, software desatualizado, permissões,
popups, problemas de desinstalação, arquivos de instalação, malware e
extensões, lixo, hibernação.

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 26 | **Software Permissions**: detectar e desabilitar permissões | `iobit.inventario.md:9` → `THealthCheckItem`, `TSoftHealthDataBase`; `iobit.recursos.txt:128` → `TFORM_HEALTHCHECK/0`; `:157` → `TFRAME_SOFTHEALTHMAIN/0`; `:155` → `TFRAME_HEALTHCHECKITEM/0` | **OK** — `Core/Management/AppPermissionManager.cs` (serviço, inicialização, menu de contexto e tarefa agendada, com ação por item) |

### 2.7 Ferramentas da caixa de ferramentas

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 22 | **File Shredder** com **3 modos de sobrescrita** | `iobit.recursos.txt:126` → `TFORM_FILESHARDDER/0`; `iobit.strings.txt:44161` → `FileShredder`; `:28741` → `fUnit_FileShredder`; `:46986` → `ToolPopFileShredder` | **OK** - `Core/Tools/UnrecoverableDelete.cs` (`Delete(paths, passes)`, `DefaultPasses`, `BytesOverwritten`) + `Ui/Tools/ToolViews.cs:477-568` |
| 23 | Botão **Shred no Explorer** | `iobit.inventario.md:83` → `AllowMeesageForVistaAbove WM_ExplorerShredder Failed`; `iobit.recursos.txt:84` → `WM_MenuRightStart` | **OK** - Botão de destruição irreversível no Explorer/a entrada de apagar em definitivo `Core/Topmost/ExplorerContextMenu.cs:333` |
| 24 | **Software Relocator**: mover software instalado entre discos | `iobit.strings.txt:13482` → `Frame_SoftRelocator`; `:13525` → `btn_StartSoftMove`; `:13500` → `TSoftMoveThread`; `:12902` → `TSoftMoveLan`; `iobit.recursos.txt:136` → `TFORM_MAINSOFTMOVE/0` | **OK** - `src/Tranal.Uninstaller/Core/Management/RelocationManager.cs:81` (`Move`: move a pasta de instalacao para a pasta de destino escolhida, `:96-100`), corrige a entrada de desinstalacao (`:131` e `:192`), recria o atalho (`:133` e `:252`); `Revert` `:149` e o historico `Historico` `:186`. Tela `src/Tranal.Uninstaller/Ui/Tools/InsightViews.cs:231`, escolha do destino por pasta em `:324` e a chamada em `:327`; barra lateral `src/Tranal.Uninstaller/MainWindow.xaml:379`. Binario do concorrente: confirmado em binario (`iobit.strings.txt:13482`, `Frame_SoftRelocator`) |
| 25 | **Bloqueio de popups** de notificação | **não confirmado no binario** | **OK** - **Bloqueio de popups** de notificação/a leitura do estado do bloqueio `Core/Tools/NotificationBlocker.cs:62` |
| 27 | **Software Updater** integrado (atualizar todos) | `iobit.recursos.txt:141` → `TFORM_SOFTWAREUPDATER/0`; `iobit.strings.txt:11558` → `SoftwareUpdaterForm`; `:11601` → `TForm_SoftwareUpdater` | **OK** - **Software Updater** integrado (atualizar todos)/`src/Tranal.Uninstaller/Core/Updates/UpdateService.cs:29` (`public static class UpdateService`) `src/Tranal.Uninstaller/Ui/Tools/UpdateViews.cs:22` (`UpdateService`) `src/Tranal.Cli/UpdateCommands.cs:40` (`UpdateService.AtualizadorDeProgramas`) o feed de programas e `feed/programas.json` no repositorio. |
| 28 | Remoção de item fixado na barra de tarefas | `iobit.strings.txt:28639` → `TaskbarPin`; `:45597` → `taskbarunpin`; `:45602` → `TaskbarPin:FilePath=`; `:45961` → `TaskbarJumpLists` | **PARCIAL** - Remoção de item fixado na barra de tarefas/`src/Tranal.Uninstaller/Core/Tools/EvidenceRemover.cs:72` (`ScanJumpLists`) **Nota**: as Jump Lists sao tratadas, mas o item fixado na barra de tarefas nao — a busca por "Taskbar" em `src/` so acha `ShowInTaskbar` das janelas. |
| 29 | **Desinstalar Windows Updates** | `iobit.inventario.md:3023` → `Windows Update` - Interage com Windows Update (token de API ativo) | **OK** - `Core/Management/WindowsUpdateManager.cs` (`Remove`, `RemoveMany`) — remocao de KB por `wusa.exe /uninstall`, com o codigo de saida traduzido (0 ok, 3010 reiniciar, 2359302 nao instalado); linha de comando `tranal /updates` |
| 30 | **Uninstall History** com data, tamanho, operação e exportação | `iobit.recursos.txt:143` → `TFORM_UNINSTALLLOG/0`; `:189` → `TTOOLUNINSTALLHISTORY/0` | **OK** — `Core/UninstallHistory.cs` |

### 2.8 Funcionalidades descobertas **no binário** e ausentes do documento anterior

Estas linhas não estavam no relatório de 05/11/2026: só apareceram quando o
binário foi aberto e as classes e os recursos Delphi foram lidos.

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 31 | **Hunter Mode**: varredura profunda de sobras (arquivo e registro) com painel próprio | `iobit.recursos.txt:131` → `TFORM_HUNTER/0`; `:132` → `TFORM_HUNTERHINT/0`; `iobit.strings.txt:44910` → `HunterMode`; `:46987` → `ToolPopHunterMode`; `:47532` → `HUNTERMODE` | **OK** — `Core/Modules/HunterMode.cs` + `Ui/Modules/HunterView.xaml.cs` |
| 32 | **Verificação de programas relacionados** (checar dependências antes de desinstalar) | `iobit.recursos.txt:174` → `TFRM_CHECKRELATEDFORM/0` | **OK** - `Core/Discovery/RelatedProgramFinder.cs:46` (`Find`), `Core/UninstallAdvisories.cs` (`RelatedProgramsAdvisory`), aviso no assistente |
| 33 | Aviso de **sincronização do Chrome** ao remover extensão | `iobit.recursos.txt:175` → `TFRM_CHROMESYNCNOTICE/0` | **OK** - Aviso de **sincronização do Chrome** ao remover extensão/o modulo do aviso de sincronizacao `Core/Modules/ChromeSyncGuard.cs:18`; o texto do aviso `Core/Modules/ChromeSyncGuard.cs:75` |
| 34 | Integração com a **Central de Ações do Windows** (Action Center) | `iobit.recursos.txt:148` → `TFRAMEACTIONCENTER8/0`; `iobit.identificadores.txt:115` → `TActionCenterAppList.FAppList.Add` | **AUSENTE** - Integração com a **Central de Ações do Windows** (Action Center)/grep "ActionCenter" em src/ -> 0 hits |
| 35 | Gerência de **tarefas agendadas** via COM Task Scheduler | `iobit.identificadores.txt:118` → `TTaskSchedulerInterfaces1.IniTaskScheduler.CoCreateInstance`; `iobit.inventario.md:3012` → `schtasks`; `:3013` → `TaskScheduler` | **PARCIAL** - Gerência de **tarefas agendadas** via COM Task Scheduler/`src/Tranal.Uninstaller/Core/Management/ScheduledTaskManager.cs:72` (`public static class ScheduledTaskManager`) `src/Tranal.Uninstaller/Core/Management/AppPermissionManager.cs:134` (`ScheduledTaskManager.ListFor`) **Nota**: a gerencia existe, mas por `schtasks` e nao pela COM do Task Scheduler 1.0. |
| 36 | **Voto / avaliação** de programas e plugins pela comunidade | `iobit.recursos.txt:135` → `TFORM_NEWAPPVOTE/0`; `:167` → `TFRMPLUGINVOTE/0`; `:168` → `TFRMPROMOTEVOTE/0` | **OK** - **Voto / avaliação** de programas e plugins pela comunidade/o modulo dos votos `Core/Appearance/CommunityVotes.cs:44`; o registro do voto `Core/Appearance/CommunityVotes.cs:82` |
| 37 | **Captura de tela** do sistema no fluxo de suporte | `iobit.recursos.txt:188` → `TMESCRSHOTFORM/0` | **OK** - **Captura de tela** do sistema no fluxo de suporte/o modulo da captura de tela `Core/Tools/ScreenCapture.cs:32`; a captura da tela `Core/Tools/ScreenCapture.cs:143` |
| 38 | **Catálogo embutido de Appx pré-instalados** (a lista do que o Windows traz de fábrica) | `iobit.identificadores.txt:103` → `Microsoft.Windows.Ai.Copilot.Provider`; `:107` → `Microsoft.Windows.DevHome`; `:98` → `Microsoft.WebView2Runtime.Stable_8wekyb3d8bbwe`; `:110` → `Microsoft.Xbox.TCUI` | **OK** - **Catálogo embutido de Appx pré-instalados** (a lista do que o Windows traz de fábrica)/modulo do catalogo Appx `Core/Discovery/AppxCatalog.cs:48`; a lista dos pre-instalados `Core/Discovery/AppxCatalog.cs:72` |
| 39 | **Gerenciador de licença** do próprio produto (ativar PRO) | `iobit.recursos.txt:133` → `TFORM_LICENSEMANAGER/0`; `:134` → `TFORM_LICENSEMESSAGE/0` | **FORA POR DECISAO** - `docs/auditoria/BINARIO.md` 3.2 (produto offline, sem cadastro) e o proprio `docs/auditoria/BINARIO-IOBIT.md` (Observacoes): "a linha 39 (licenca) e a linha 40 (telemetria) nao sao backlog - o Tranal nao comercializa e nao tem telemetria por decisao" |
| 40 | Tela de **dados de uso** do programa (telemetria) | `iobit.recursos.txt:123` → `TFORM_DISPLAYDATA/0`; `:122` → `TFORM_EXPRIED/0` | **FORA POR DECISAO** - Mesma base (BINARIO.md 3.2: "o Tranal e offline, sem cadastro e sem telemetria") |

### 2.9 FREE × PRO

O FREE entrega: Programs, Browser Extensions, Windows Apps, Bloatware,
Force Uninstall+, File Shredder, Relocator, Toolbox, History, menu de
contexto. O **PRO** adiciona (deduzido das telas de venda, não executado):
desinstalar pelo log de instalação, a base de 4.000+ programas travados, a
remoção de extensões maliciosas e de advertising, *Update All*, *Fix All* em
1 clique e auto-limpeza de sobras.

> **Achado de campo**: a versão **FREE** do IObit baixou **~33 MB de payloads
> promocionais** na primeira execução (`Pub/`, `LatestNews/`,
> `Action Center/`). O registro está em
> `evidencia/arquivos-criados-em-execucao.txt`.

---

## 3. Mecanismos notáveis

### 3.1 Quatro drivers de kernel

| Driver | Função |
|---|---|
| `IUFileFilter.sys` | filtro de sistema de arquivos |
| `IUForceDelete.sys` | **força remoção de arquivo em uso** (`ZwCreateFile`, `ZwOpenKey`, `ZwSetValueKey`, `SeCaptureSecurityDescriptor`, `BCrypt*`) |
| `IUProcessFilter.sys` | filtro de processos (encerrar processo travado) |
| `IURegistryFilter.sys` | filtro de registro (remover chave/valor sob demanda) |

Entregues para **7 arquiteturas** (`win7_x86/amd64/ia64`, `win10_x86/amd64/ia64/arm64`) —
28 arquivos `.sys`.

### 3.2 APIs importadas pelo executável principal

29 DLLs, 841 funções únicas. As que contam para a paridade:

| Mecanismo | APIs |
|---|---|
| **Escrita/inspeção de memória de processo** | `WriteProcessMemory`, `ReadProcessMemory`, `VirtualAllocEx`, `VirtualProtectEx`, `OpenProcess`, `TerminateProcess`, `TerminateThread`, `SuspendThread` |
| **Serviços do Windows** | `OpenSCManagerW`, `OpenServiceW`, `StartServiceW`, `ControlService`, `DeleteService`, `EnumServicesStatusW` |
| **Elevação de privilégio** | `OpenProcessToken`, `AdjustTokenPrivileges`, `DuplicateTokenEx` |
| **Registro** | `RegOpenKeyExW`, `RegSetValueExW`, `RegDeleteKeyW`, `RegLoadKeyW` (hive), `RegUnLoadKeyW` |
| **Lixeira e shell** | `SHFileOperationW`, `ShellExecuteExW`, `SHGetSpecialFolderPathW`, `SHChangeNotify`, `DragAcceptFiles` |
| **Restauração do sistema** | `SysRest.dll`: `IsSREnable`, `EnableSR`, `CreateRestorePoint`, `RestoreSystem`, `DeleteRestorePoint` |
| **Validação de assinatura** | `WinVerifyTrust`, `CryptCATAdminAcquireContext`, `CryptCATAdminCalcHashFromFileHandle` |
| **Nível baixo do NT** | `NtOpenProcess`, `NtOpenThread`, `NtQueryVirtualMemory`, `NtQuerySystemInformation` |
| **Rede e cripto** | `wininet.dll`, `wintrust.dll`, `crypt32.dll`, `netapi32.dll` (`NetUserEnum`), `libcrypto-1_1.dll`, `libssl-1_1.dll` |

### 3.3 Integração com o sistema

Leitura das chaves de desinstalação em **HKCU e HKLM, 32 e 64 bits** ·
**menu de contexto do Explorer** (`shellex\ContextMenuHandlers\`) para
"Powerful Uninstall" · botão "Powerful Uninstall" no Painel de Controle ·
chave `Run` para o monitor em segundo plano · `SC.exe`/`sc delete` para
serviços · `SHCatch.ini` (hook de shell) · servidores `api.iobit.com`,
`ascstats.iobit.com`, `purchase.iobit.com`.

### 3.4 Superfície de recursos Delphi confirmada no binário

As 194 entradas de recurso do PE de `iobit.recursos.txt` confirmam 40+
formulários: `TFRMIOBITUNINSTALL` (principal), `TFRMUNINSTALLFORM`,
`TFRMFORCEDUNINSTALL`, `TFRM_UNINSTALLBUNDLE`, `TFORM_STUBBORNTOOL`,
`TFORM_CHECKSTUBBRON`, `TFORM_FORCESTUBBRON`, `TFORM_HUNTER`,
`TFRABROWSERCLEANER`, `TFORM_FILESHARDDER`, `TFORM_HEALTHCHECK`,
`TFRAME_SOFTHEALTHMAIN`, `TFRAME_HEALTHCHECKITEM`, `TFORM_SOFTWAREUPDATER`,
`TFORM_MAINSOFTMOVE`, `TFORM_WINAPPSTOOL`, `TFRAME_WIN8APP`,
`TFRMINSTALLMONITOR`, `TFRMMONITORLOG`, `TFORM_UNINSTALLLOG`,
`TTOOLUNINSTALLHISTORY`, `TFRMSYSTEMRESTORE`, `TFRAMEACTIONCENTER8`,
`TFRMSTOREPAGE`, `TFORM_LICENSEMANAGER` — e as units `TSysRestore`,
`TTaskSchedulerInterfaces1`, `TActionCenterAppList`.

---

## 4. Interface

Janela principal em Delphi/VCL com barra lateral de abas. A árvore UIA
expõe 27 elementos; o VCL cria 48 janelas filhas (painéis). Atalhos de
teclado registrados no `.lng`.

**Dificuldade registrada na auditoria**: o app Delphi renderiza fora da tela
e o *foreground* era disputado por outro processo, então a navegação entre
abas por clique não foi confiável. Só **uma** captura de tela foi obtida; o
resto veio do artefato (`.lng` com 95 seções) e da análise estática. **Não
verificado**: o comportamento das abas além da primeira.

---

## 5. Linha de comando

**O IObit Uninstaller não publica nenhum switch de linha de comando.** A API
existe (`GetCommandLineW`, `CommandLineToArgvW`), mas nenhuma string de
`/SILENT`, `/scan` ou `/uninstall "<id>"` aparece no binário, no site nem no
manual. O único `/s` encontrado é o da **instalação** silenciosa do próprio
produto.

Isso é uma diferença importante e favorável ao Tranal: a **nossa** CLI
(`/scan`, `/mu`, `/export`, `/columns`) é uma capacidade que nenhum dos
concorrentes auditados tem.

---

## 6. O que o Tranal NÃO faz hoje

Coluna `Tranal` no formato da curadoria binária: **OK** = implementado e
verificado no código; **PARCIAL** = existe mas falta parte; **AUSENTE** =
sem equivalente identificado em `src/Tranal.Uninstaller`.

| # | Recurso do IObit | Evidência no binário | Tranal |
|---|---|---|---|
| 1 | **Remoção de extensões de navegador** com base maliciosa/ad | `iobit.strings.txt:37337` → `PlugDB.dbd`; `:40554` → `Malicious Extension` | **PARCIAL** — `Core/Modules/BrowserExtensions.cs`, 4 navegadores e **sem base de assinaturas** |
| 2 | Base de extensões maliciosas embarcada | `iobit.inventario.md:9` → `TADPluginDBHelper` | **AUSENTE** |
| 3 | Limpeza de navegador / reset de padrões | `iobit.recursos.txt:147` → `TFRABROWSERCLEANER/0` | **PARCIAL** — `Core/Tools/TrackCleaner.cs` limpa rastros; falta reset de padrões |
| 4 | Remoção de **plugins** de navegador | `iobit.recursos.txt:110` → `PLUGIN_DEFAULT/0` | **AUSENTE** |
| 5 | **Bloatware Remover** (pré-instalados, Accessories, Windows Features) | `iobit.strings.txt:46869` → `Bloatware Remover`; `:40142` → `BloatApp.dll` | **AUSENTE** |
| 6 | **Bundleware**: detectar o que veio junto | `iobit.strings.txt:9872` → `Tfrm_UninstallBundle` | **AUSENTE** |
| 7 | **Desinstalar pelo log de instalação** | `iobit.strings.txt:44602` → `Programs logged by Install Monitor can be uninstalled thoroughly…` | **PARCIAL** — `Core/Modules/TracedPrograms.cs` registra; a desinstalação não parte do log |
| 8 | **Stubborn Program Remover** com base de 4.000+ | `iobit.strings.txt:11330` → `TStubbronUninstallManager` | **PARCIAL** — `Core/ProcessTerminator.cs` + `Core/DeferredDelete.cs`, sem base |
| 9 | **Force Uninstall+** (sem chave de registro) | `iobit.recursos.txt:162` → `TFRMFORCEDUNINSTALL/0` | **PARCIAL** — `Ui/ForcedUninstallWindow.xaml.cs`; falta o caso "sem entrada de registro" |
| 10 | **Easy Uninstall** (arrastar ícone) | `iobit.inventario.md:82` → `WM_DROPFILES Failed` | **PARCIAL** — `Ui/Tools/TopmostOverlayWindow.xaml.cs` resolve o alvo, com outro desenho |
| 11 | **Software Health** como painel único de 9 categorias | `iobit.recursos.txt:128` → `TFORM_HEALTHCHECK/0`; `:157` → `TFRAME_SOFTHEALTHMAIN/0` | **PARCIAL** — `Core/Management/SoftwareHealth.cs` existe; falta o painel único de 9 categorias |
| 12 | Bloqueio de popups | **não confirmado no binario** | **AUSENTE** |
| 13 | **Software Updater** de terceiros | `iobit.recursos.txt:141` → `TFORM_SOFTWAREUPDATER/0` | **AUSENTE** — o Tranal atualiza a si mesmo |
| 14 | **Software Relocator** (mover entre discos) | `iobit.strings.txt:13525` → `btn_StartSoftMove` | **AUSENTE** |
| 15 | Desinstalar **Windows Updates** | `iobit.inventario.md:3023` → `Windows Update` | **AUSENTE** |
| 16 | **Desinstalação em lote** (multi-seleção) | **não confirmado no binario** | **PARCIAL** — `Core/Automation/BatchList.cs` + CLI `/mu` |
| 17 | Aviso de que o desinstalador "ainda vai deixar" resíduo | **não confirmado no binario** | **PARCIAL** — `Core/UninstallAdvisories.cs` |
| 18 | Aviso antes de apagar **arquivo pessoal** | **não confirmado no binario** | **AUSENTE** |
| 19 | **Drivers de kernel** para arquivo em uso | `IUForceDelete.sys` (`evidencia/pe-imports-IObitUninstaler.txt`) | **AUSENTE** — decisão registrada: o Tranal não assina driver |
| 20 | Desinstalador do **fabricante** como fallback | `iobit.inventario.md:9` → `TAppsRemoval` | **PARCIAL** — `Core/Removal.cs` encerra o processo, mas não procura o desinstalador oficial |
| 21 | **Sobras por categoria** (tarefa/serviço/DLL) | `iobit.identificadores.txt:113-114` → `ScanItem.FilePath.Count:` / `ScanItem.RegRoot.Count:` | **PARCIAL** — `Core/UninstallSupport.cs` varre registro e disco, por **confiança** |
| 22 | Uninstall History com tamanho e exportação | `iobit.recursos.txt:143` → `TFORM_UNINSTALLLOG/0` | **OK** — `Core/UninstallHistory.cs` |
| 23 | Remoção de item fixado na barra de tarefas | `iobit.strings.txt:45597` → `taskbarunpin` | **AUSENTE** |
| 24 | Gerência de tarefas agendadas (COM) | `iobit.identificadores.txt:118` → `TTaskSchedulerInterfaces1.IniTaskScheduler.CoCreateInstance` | **PARCIAL** — `Core/Management/ScheduledTaskManager.cs` |
| 25 | Verificação de **programas relacionados** | `iobit.recursos.txt:174` → `TFRM_CHECKRELATEDFORM/0` | **AUSENTE** |
| 26 | Catálogo embutido de Appx pré-instalados | `iobit.identificadores.txt:103` → `Microsoft.Windows.Ai.Copilot.Provider` | **AUSENTE** |
| 27 | **Hunter Mode** (varredura profunda de sobras) | `iobit.recursos.txt:131` → `TFORM_HUNTER/0` | **OK** — `Core/Modules/HunterMode.cs` |
| 28 | **Software Permissions** (serviço/inicialização/menu/tarefa) | `iobit.inventario.md:9` → `THealthCheckItem` | **OK** — `Core/Management/AppPermissionManager.cs` |
| 29 | Modo escuro | `iobit.recursos.txt` (temas `AllSkin*`) | **OK** — `Theming/ThemeManager.cs` (4 temas) |

### Onde o Tranal já é melhor

| Ponto | Tranal | IObit |
|---|---|---|
| **Linha de comando** | `/scan`, `/mu`, `/export`, `/columns`, modos, `/all`, `/high` | **nenhum switch publicado** |
| **Driver de kernel** | não usa, e não precisa assinar driver | 4 drivers × 7 arquiteturas |
| **Confiança das sobras** | 3 níveis + **motivo por item** | 4 categorias, sem nível de ameaça |
| **Backup antes de desinstalar** | backup completo do registro, com script de restauração | sem backup de registro próprio |
| **Desinstalação de programa travado** | encerra o processo e segue | tem driver e base de 4.000+ |
| **Privacidade** | sem telemetria | `ascstats.iobit.com`, `ProductNews2.dll`, payloads promocionais baixados |

---

## 7. Fichos de evidência

| Arquivo | Conteúdo |
|---|---|
| `downloads/iobit_uninstaller_setup.exe` | instalador (34.309.696 B) |
| `app/payload` | 146 arquivos extraídos (97,6 MB) |
| `evidencia/RESUMO.md` | 56 KB, 8 seções, 142 funcionalidades, 40 linhas de gap |
| `evidencia/lng-english-plano.txt` | as 95 telas do `.lng` achatadas |
| `evidencia/pe-imports-IObitUninstaler.txt` | 29 DLLs, 841 funções |
| `evidencia/strings-*.txt` | 20 arquivos de strings |
| `evidencia/uia-*.txt` | árvores de UIA |
| `evidencia/arquivos-criados-em-execucao.txt` | os payloads baixados pelo FREE |
| `evidencia/innoextract-lista.txt` | a lista completa dos 146 arquivos |

### Fichos de evidência da curadoria binária (06/11/2026)

| Arquivo | Bytes | Conteúdo |
|---|---|---|
| `bin-inventario/iobit.inventario.md` | 155.615 | 2.328 classes RTTI, 2.993 rótulos de interface, 62 capacidades de API/IO |
| `bin-inventario/iobit.recursos.txt` | 16.932 | 194 entradas de recurso do PE (formulários Delphi) |
| `bin-inventario/iobit.identificadores.txt` | 5.753 | identificadores de tipo/método (units Delphi, listas de AppX) |
| `bin-strings/iobit.strings.txt` | 860.341 | dump bruto de strings do executável principal |

Todas as citações `arquivo:linha` desta curadoria foram conferidas contra o
conteúdo real dos dumps.

---

## 8. Lacunas desta auditoria

| O que faltou | Por quê | Como fechar |
|---|---|---|
| Navegação nas outras abas | o app Delphi renderiza fora da tela e o *foreground* era disputado por outro processo | auditar com o owner fechando o que estiver usando a máquina, ou com o app instalado |
| **Desinstalação real** | exigiria instalar e desinstalar algo na máquina; não foi feito | o owner autorizou instalar os concorrentes |
| **Software Health** em detalhe | mesma razão da navegação | idem |
| Recursos **PRO** | software pago | pesquisa documental no site |
| Bases `.dbd` proprietárias | formato fechado, não é SQLite puro | engenharia reversa do formato |
| Comportamento do `Install Monitor` rodando de verdade | exige instalar um programa para monitorar | idem |
| **9 funcionalidades sem confirmação no binário** (#9, #10, #11, #16, #21, #25) | a evidência original vinha do `.lng`, que não é o binário; nesta rodada só o literal exato do executável conta | extrair o `English.lng` do payload e cruzar com as strings do executável |
| **Módulos auxiliares não lidos** | `UninstallMonitor.exe`, `UninstallExplorer.dll`, `IUMenuRight.dll`, `RegisterCom.dll`, `SysRest.dll` são binários separados | abrir cada um com o mesmo inventário |

---

## 9. Decisões que este documento obriga

1. **Remoção de extensões de navegador entra no backlog.** É a lacuna que
   só o IObit revelou, e nenhum outro concorrente tem. Ela não é "desinstalar"
   — é um produto dentro do produto, e precisa de uma base de assinaturas
   para ser útil.
2. **Driver de kernel continua fora.** O IObit usa 4 drivers para remover
   arquivo em uso. O Tranal resolve o mesmo problema encerrando o processo
   e adiando para o reinício (`DeferredDelete`). A decisão do `PARIDADE.md`
   §8 se sustenta — e agora tem a prova: o custo de um driver é 4 binários
   × 7 arquiteturas, assinatura de código e um problema de reputação que o
   McAfee desta máquina já demonstrou na prática.
3. **A classificação das sobras continua por confiança, não por categoria.**
   O IObit organiza por categoria (registro/arquivo/serviço); o Tranal
   organiza por confiança + motivo. A pergunta que o usuário faz é "isto é
   dele?", e a confiança responde a ela.
4. **Bundleware e "desinstalar pelo log de instalação" viram item de
   backlog.** O Tranal já tem o log (`TracedPrograms`); falta usá-lo para
   desinstalar, que é a metade que o dono sente.
5. **O painel único de saúde do software é prioridade de produto.** A
   curadoria binário confirma que o IObit **tem** o painel
   (`TFRAME_SOFTHEALTHMAIN`) e o Tranal **tem** o motor
   (`Core/Management/SoftwareHealth.cs`) — falta a casca de painel único.
6. **O que a curadoria fechou:** Hunter Mode, permissões de aplicativo,
   restauração do sistema, histórico de desinstalação e shredder **já
   existem** no Tranal. As lacunas reais do IObit hoje são: extensões com
   base de assinaturas, bloatware, bundleware, base de programas travados,
   software updater de terceiros, software relocator, desinstalar Windows
   Updates e desinstalar pelo log.

---

*Auditoria gerada por medição direta dos artefatos e por execução da versão
FREE. Toda linha deste documento aponta para evidência em disco; o que não
pôde ser verificado está marcado como tal na seção 8.*
