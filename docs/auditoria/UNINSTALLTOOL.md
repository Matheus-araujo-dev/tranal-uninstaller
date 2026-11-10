# Uninstall Tool 3.8.2 (CrystalIDEA) — auditoria

> O concorrente **mais fundo por dentro e mais pobre por fora**. É o único
> da série com **driver de kernel** (minifiltro de sistema de arquivos +
> callback de registro + callback de processos), o único com **serviço de
> rastreamento versionado** e o único com **extensão de shell empacotada em
> MSIX** para o Windows 11. Em contrapartida, é o **mais magro** em
> manutenção: o menu *Ferramentas* apenas **chama ferramentas do Windows**.
> Compensa a falta de ferramental com **precisão de rastreamento** — ele
> acompanha o instalador **enquanto ele roda**.

Data da coleta: 05/11/2026 · Versão auditada: **3.8.2 build 5762** (edição
**Free/trial**, 19/08/2026)

---

## 1. Identificação

| Campo | Valor | Fonte |
|---|---|---|
| Produto | Uninstall Tool (CrystalIDEA) — "All Uninstall & Tools" é o nome comercial antigo | `pe-UninstallTool_x64.txt` |
| Editor | **CrystalIDEA Software / CrystalBit Solutions** (Bélgica) | assinatura Authenticode |
| Site oficial | `https://crystalidea.com/uninstall-tool` — respondeu **200** | medido |
| Domínio histórico | `uninstalltool.com` — **HTTP** responde 200 e **redireciona** para `crystalidea.com`; **HTTPS dá timeout** e `www` também | medido |
| Wayback | **nenhum snapshot** arquivado de `uninstalltool.com` | medido |
| Canal real de download | **GitHub Releases** (`github.com/crystalidea/uninstall-tool/releases`) — o `uninstalltool.com` é só um redirecionador | manifesto winget |
| Versão | 3.8.2 build 5762, 19/08/2026 | `pe-UninstallTool_x64.txt` |
| Formato do programa | **PE nativo Win32, não .NET** — x64 (`0x8664`) e x86 (`0x014C`), GUI; UI Codejock **XTP** | `pe-*.txt` |
| Formato do instalador | **Inno Setup 6.6.0** (o `innoextract` 1.9 local não lê essa versão) | `strings-setup.txt` |
| Assinante do app | `CN=CrystalBit Solutions` — Sectigo Public Code Signing CA R36, **válido até 13/04/2029** | `pe-UninstallTool_x64.txt` |
| Assinante do helper | CrystalBit Solutions, **Certum — certificado expirado em 23/01/2020** | `pe-UninstallToolHelper.txt` |
| Proteção do código | **EXECryptor** | `strings-UninstallTool_x64.txt:11340` |
| Compilação | 2026-08-19 14:03:02Z | `pe-UninstallTool_x64.txt` |
| Portátil | launcher 32 bits + helper + `UninstallTool_x64.dat` (o `.dat` é um **PE nativo**, byte a byte igual ao `.exe`) + `RemoveService.cmd` + **45 idiomas** XML | `app/portable/` |
| Preço | Standard US$ 24,95/PC · Portable US$ 39,95 · **trial de 30 dias** | `manual-buy.txt` |
| Requisitos | Windows 11/10/8.1/8/7, 64/32 bits nativo, **exige administrador** | `manual-support.txt` |

### SHA-256 dos artefatos

| Arquivo | Bytes | SHA-256 |
|---|---|---|
| `uninstalltool_portable_3.8.2.zip` | 6.272.708 | `BF12F8E56988A260DCA9266905EC7AEA260D5ED531EB51169FACB3CDB6662BC8` |
| `uninstalltool_setup_3.8.2.exe` | 7.213.944 | `43CDF31C528327F9F355A003E60D5D4123994BB66C4DD0190410FA19EA05F98D` |
| `uninstalltool_setup.exe` (3.8.0) | 6.312.424 | `4EB66812DB6904811399199BFEFA09C6DD81BF5B35AEBA0F97FB5EDE24884345` (confere com o winget) |
| `UninstallTool_x64.exe` | 6.449.800 | `A47186969CB61616D2C939DA02ED43EC5456A08C20AEEFF3173F7D6B330D3F45` |
| `UninstallTool_x86.exe` | 5.337.736 | `86C8BB2D9D707379EF5EB3BCD0EA658223E18FEE699AC6161B41B729C2DE1552` |
| `UninstallToolPortable.exe` | 527.496 | `25B54BFD9734AA606FBEA756FF25A13CF3B03E573E63A8923D44F71B378A77B6` |
| `UninstallToolHelper.exe` | 474.720 | `BA8D7B5662F85AA901FD6BCF86FC5989013577B18C81A91BFFC1211FEC31D6C8` |

### O achado de nomenclatura que quebra automação

> A documentação oficial da linha de comando escreve **`UninstallTool.exe
> /install …`** — mas **esse executável não existe no pacote 3.8.2**. O
> binário real é `UninstallToolPortable.exe` (é o que o `RemoveService.cmd`
> chama) e, na variante instalada, `UninstallTool_x64.exe` / `UninstallTool_x86.exe`.
> **Qualquer script que siga a documentação ao pé da letra falha.**

---

## 2. Superfície observável

**67 funcionalidades** medidas — curadoria a partir do **binário**. A numeração original (1–67) foi preservada; as linhas novas (68+) continuam a numeração.

**Legenda das evidências** — arquivos medidos em `E:\Projetos\tranal-auditoria-evidencia\`:

| Abreviação | Arquivo |
|---|---|
| `strings.txt` | `bin-strings/uninstalltool.strings.txt` — dump bruto (inclui o XML `LanguageStrings` embarcado) |
| `imports.txt` | `bin-inventario/uninstalltool.imports.txt` — imports da PE (822 funções) |
| `inventario.md` | `bin-inventario/uninstalltool.inventario.md` — classes RTTI (126) + rótulos (1372) |
| `identificadores.txt` | `bin-inventario/uninstalltool.identificadores.txt` — identificadores de tipo |

Coluna **Tranal**: `OK`, `PARCIAL` (diz o que falta) ou `AUSENTE`.

### 2.1 Desinstalação

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 1 | Lista com colunas Nome / Tamanho / Instalado em / **Rastreado** / Status / Origem / Empresa | `strings.txt:14099` → `<columnTraced>Traced</columnTraced>`; `:14105` → `<columnCompany>Company</columnCompany>`; `:14117` → `<mark32bitItem>(32-bit)</mark32bitItem>` | OK — `Core/ProgramColumns.cs` |
| 2 | 4 categorias na barra secundária: Aplicativos, Sistema, Oculto, Microsoft Store Apps | `strings.txt:14132` → `<btnApps>Apps</btnApps>`; `:14138` → `<btnStoreApps>Microsoft Store Apps</btnStoreApps>`; `:4928` → `SecondaryToolbar/btnApps` | OK — `MainWindow.xaml.cs`, `Ui/Modules/ModuleViews.cs` |
| 3 | Marcação `(32-bit)` em programas de 32 bits em Windows 64 | `strings.txt:14117` → `<mark32bitItem>(32-bit)</mark32bitItem>` | **OK** - Marcação `(32-bit)` em programas de 32 bits em Windows 64/`src/Tranal.Uninstaller/Core/ProgramMarks.cs:182` (`WithBits`), `:187` (o sufixo vem do catalogo de idiomas), `:103` (a marca so existe em Windows de 64 bits), `:115` (marca `ThirtyTwoBits`), `:51` (`Is32Bits`); o nome exibido em `src/Tranal.Uninstaller/Ui/ProgramRow.cs:63` (`Label`) e a identidade crua continua em `:41` (`DisplayName`); o binding da lista em `src/Tranal.Uninstaller/MainWindow.xaml:27`; a maquina do executavel e lida no disco em `src/Tranal.Uninstaller/Core/ProgramScanner.cs:197` (`DetectBits`, `:296`); `src/Tranal.Tests/Lote28FeatureTests.cs`; `tools/test-lote28.ps1`. Medido: o programa de 32 bits aparece na lista como Nome (32-bit), como no concorrente. |
| 4 | Ordenação por qualquer coluna, com largura e posição **memorizadas** | nao confirmado no binario | **PARCIAL** - Ordenação por qualquer coluna, com largura e posição **memorizadas**/`src/Tranal.Uninstaller/MainWindow.xaml.cs:72` (`ProgramsList`) **Nota**: a ordenacao por qualquer coluna existe; a largura e a posicao nao sao memorizadas — so as colunas escondidas vao para `AppSettings`. |
| 5 | Colunas ocultáveis por clique direito no cabeçalho | nao confirmado no binario | **OK** - O menu do cabecalho monta a lista de colunas e o clique esconde a coluna; a escolha fica gravada em `AppSettings.HiddenColumns` e e lida ao montar a grade (`MainWindow.xaml.cs:387`). |
| 6 | Atualizações de software como **sub-itens em árvore** | `strings.txt:17833` → `Display software updates:`; `:14227` → `<checkDisplayUpdates>Display software updates</checkDisplayUpdates>` | **PARCIAL** - Atualizações de software como **sub-itens em árvore**/`src/Tranal.Uninstaller/Core/Updates/UpdateService.cs:29` (`public static class UpdateService`) `src/Tranal.Uninstaller/Ui/Tools/UpdateViews.cs:22` (`UpdateService`) **Nota**: a verificacao de atualizacao dos programas existe e tem tela e comando, mas nao ha arvore de atualizacoes como sub-item. |
| 7 | Cálculo de tamanho **incluindo pastas de dados** em AppData/Documentos | nao confirmado no binario | **PARCIAL** - Cálculo de tamanho **incluindo pastas de dados** em AppData/Documentos/`src/Tranal.Uninstaller/Core/Discovery/UserInstallFolders.cs:35` (`public static class UserInstallFolders`) **Nota**: o tamanho medido e o da pasta de instalacao; as pastas de dados sao apenas apontadas como aviso e nao somadas ao tamanho. |
| 8 | Data de instalação por item, com destaque para "recentemente instalado" | `strings.txt:14234` → `<checkHighlighRecent>Highlight recently installed programs</checkHighlighRecent>` | **PARCIAL** - Data de instalação por item, com destaque para "recentemente instalado"/`src/Tranal.Uninstaller/Core/Modules/RelativeDate.cs:20` (`public static class RelativeDate`) **Nota**: a data em tempo relativo existe; o realce dos recem-instalados nao. |
| 9 | **Uninstall** — executa o desinstalador padrão | `strings.txt:4952` → `MainMenu/mnuActionUninstall`; `:14049` → `<mnuActionUninstall>&amp;Uninstall...</mnuActionUninstall>` | OK — `Core/Removal.cs` |
| 10 | **Modify** — tela de modificação de componentes | `strings.txt:4953` → `MainMenu/mnuActionModify`; `:14050` → `<mnuActionModify>&amp;Modify...</mnuActionModify>` | **OK** - **Modify** — tela de modificação de componentes/`src/Tranal.Uninstaller/Core/Discovery/MsiProductCatalog.cs:122` (`TryBuildRepairCommand`), `:265` (`Repair`), `src/Tranal.Uninstaller/MainWindow.xaml.cs:1683` (`CmdModifyProgram`), `:2340` (`ModifyProgram`). A acao "Modificar" existe na tela: e o item do menu de contexto (`Cmd.ModifyProgram`), que reaproveita o instalador do programa e, para um produto MSI, roda o reparo do Windows Installer (`msiexec /fa`). A regra mora no nucleo, em um lugar so (`Core/Discovery/MsiProductCatalog.cs`). |
| 11 | **Force Removal** — apaga arquivos e registro **sem rodar o desinstalador** | `strings.txt:5020` → `Messages/msgForceRemovalDescription` | OK — `Core/Modules/HunterMode.cs`, `Ui/ForcedUninstallWindow.xaml.cs` |
| 12 | **Batch Force Removal** | `strings.txt:14052` → `<mnuActionDeleteBatch>Batch &amp;Force Removal</mnuActionDeleteBatch>` | OK — `Core/Automation/BatchList.cs` |
| 13 | **Remove Entry** — tira da lista sem apagar sobras | `strings.txt:4955` → `MainMenu/mnuActionRemoveEntry`; `:14053` → `<mnuActionRemoveEntry>Remove &amp;Entry</mnuActionRemoveEntry>` | OK — `Core/RegistryEditing/RegistryEditor.cs` (MATRIZ D14) |
| 14 | **Batch Remove Entries** | `strings.txt:14054` → `<mnuActionRemoveEntryBatch>Batch Remove &amp;Entries</mnuActionRemoveEntryBatch>` | OK — `Core/Automation/BatchList.cs` |
| 15 | Lote com **teto de itens** e aviso explícito | `strings.txt:14151` → `<itemBatchLimitationWarning>You selected %u items but maximum number of items in a batch operation is limited to %u</itemBatchLimitationWarning>` | **OK** - Lote com **teto de itens** e aviso explícito/preferencia do teto `Core/Settings/AppSettings.cs:320`; a regra do teto `Ui/UninstallWizardWindow.xaml.cs:172` |
| 16 | Desinstalação de **aplicativos Microsoft Store / UWP**, inclusive em lote | `identificadores.txt:9` → `Windows.Management.Deployment.PackageManager`; `strings.txt:14138` → `<btnStoreApps>Microsoft Store Apps</btnStoreApps>` | OK — `Core/Modules/WindowsApps.cs` |
| 17 | Remoção "ultra fast and thorough" de programas **rastreados** | `strings.txt:5019` → `Messages/msgForceRemovalDescriptionTraced` | OK — `Core/Modules/TracedPrograms.cs` |
| 18 | Detecção de **itens relacionados** e **dependentes** | nao confirmado no binario | **PARCIAL** - Detecção de **itens relacionados** e **dependentes**/`src/Tranal.Uninstaller/Core/UninstallAdvisories.cs:87` (`public static class UninstallAdvisories`) `src/Tranal.Cli/RegistryCommands.cs:258` (`SharedReferenceCounter`) **Nota**: relacionados e pasta compartilhada sao avisados; dependentes nao sao detectados. |
| 19 | **Cancelamento forçado** de um desinstalador travado, com aviso de riscos | nao confirmado no binario | **PARCIAL** - **Cancelamento forçado** de um desinstalador travado, com aviso de riscos/`src/Tranal.Uninstaller/Ui/ForcedUninstallWindow.xaml.cs:17` (`public partial class ForcedUninstallWindow`) **Nota**: o processo que trava e encerrado, mas a tela de desinstalacao forcada nao avisa dos riscos antes de remover. |
| 20 | Remoção de **objetos COM** registrados | `imports.txt:26` → `ole32.dll / CoGetClassObject` | **PARCIAL** - Remoção de **objetos COM** registrados/`src/Tranal.Uninstaller/Core/Automation/ComUnregister.cs:28` (`public static class ComUnregister`) **Nota**: o desregistro de COM existe como ferramenta separada, mas nao esta ligado ao fluxo de remocao. |
| 21 | Remoção de **arquivos somente-leitura** | `imports.txt:18` → `SHELL32.dll / SHFileOperationW` | **PARCIAL** - Remoção de **arquivos somente-leitura**/`src/Tranal.Uninstaller/Core/BackupManager.cs:129` (`public static class BackupManager`) `src/Tranal.Uninstaller/Core/Tools/UnrecoverableDelete.cs:46` (`public static class UnrecoverableDelete`) **Nota**: o backup e a destruicao definitiva limpam o atributo antes de apagar; o caminho principal de remocao usa `File.Delete` direto. |

### 2.2 Uninstall Wizard (varredura de sobras)

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 22 | Wizard **embutido**: roda o desinstalador e depois varre o sistema | `strings.txt:5225` → `UninstallWizard/staticScanStatusFound` | **OK** - O assistente tem 5 etapas e, depois do desinstalador nativo, varre o sistema por sobras (`UninstallSupport.cs:1482`, `RemoveLeftovers`) e as lista para remocao. |
| 23 | Varredura separada de **File System** e **Registry**, em árvore | `strings.txt:5285` → `UninstallWizard/treeItemFileSystem`; `:5286` → `UninstallWizard/treeItemRegistry` | OK — `Core/UninstallSupport.cs` |
| 24 | Resumo quantitativo: "%d files (%s) and %d registry items are still left" | `strings.txt:5226` → `UninstallWizard/staticOverallInfo` | OK — `Core/RemovalLog.cs` |
| 25 | Revisão item a item antes de apagar, com "Browse…" | `strings.txt:5225` → `UninstallWizard/staticScanStatusFound` | OK — `Ui/UninstallWizardWindow.xaml.cs` |
| 26 | Remoção de arquivos em uso **agendada para o próximo boot** | `imports.txt:6` → `KERNEL32.dll / MoveFileExA` | OK — `Core/DeferredDelete.cs` (`MOVEFILE_DELAY_UNTIL_REBOOT`) |
| 27 | **Detecção e terminação** de processos do app ainda em execução | `inventario.md:9` → `TerminateProcess`, `TerminateThread` | OK — `Core/ProcessTerminator.cs` |
| 28 | **Log detalhado de remoção por item**, com código de erro | `strings.txt:5245` → `UninstallWizard/staticHyperlinkRemovalLog` | **OK** - Cada item removido vira uma linha do log com codigo de erro (`ErrorCode`, `RemovalLog.cs:55`) e o arquivo e gravado no fim do assistente (`src/Tranal.Uninstaller/Ui/UninstallWizardWindow.xaml.cs:741`, `RemovalLog.Save`). |
| 29 | **Exportação do resultado da varredura para XML** | `strings.txt:5222` → `UninstallWizard/staticHyperlinkExport` | **PARCIAL** - **Exportação do resultado da varredura para XML**/`src/Tranal.Uninstaller/Core/Reporting/LeftoverReportExporter.cs:21` (`public static class LeftoverReportExporter`) **Nota**: o exportador de sobras existe em XML, mas nao tem consumidor de producao — a varredura do assistente nao tem acao de exportar. |
| 30 | Log de desinstalação **salvo em arquivo** | nao confirmado no binario | **OK** - O log por item e gravado em arquivo no fim da desinstalacao, em `Core/RemovalLog.cs:13` (estrutura) e `Core/RemovalLog.cs:111` (CSV). |
| 31 | Preferência: sempre rodar / nunca rodar / perguntar | `strings.txt:4726` → `DlgPreferences/comboItemWizardOption1` | **OK** - Preferência: sempre rodar / nunca rodar / perguntar/preferencia `Core/Settings/AppSettings.cs:368`; a regra dos modos `Core/Settings/AppSettings.cs:366` |
| 32 | O Wizard **sempre roda** para programas rastreados | nao confirmado no binario | **PARCIAL** - O Wizard **sempre roda** para programas rastreados/preferencia `Core/Settings/AppSettings.cs:368` |

### 2.3 Install Tracker (rastreamento por driver)

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 33 | **Instala e rastreia** um `.exe`/`.msi` disparando o instalador e observando o sistema | `inventario.md:159` → `CisUtMonitor: DriverEntry`; `:161` → `FltRegisterFilter FAILED!` | **OK** - O modulo "Programas Monitorados" tem o comando "Instalar", que dispara o instalador (`UseShellExecute`, `Core/Modules/TracedPrograms.cs:172`) e guarda o antes e o depois. |
| 34 | Rastreamento **em tempo real, por evento**, de arquivos/pastas criados e renomeados e chaves criadas/alteradas/excluídas | `inventario.md:160` → `CmRegisterCallback FAILED!`; `:162` → `FltStartFiltering FAILED!` | **PARCIAL** - Rastreamento **em tempo real, por evento**, de arquivos/pastas criados e renomeados e chaves criadas/alteradas/excluídas/`src/Tranal.Uninstaller/Core/Tracing/WriteWatcher.cs:35` (`public sealed class WriteWatcher`) `src/Tranal.Uninstaller/Core/Modules/SystemSnapshot.cs:43` (`public sealed class SystemSnapshot`) **Nota**: o disco e rastreado em tempo real, mas o registro e capturado por snapshot, nao por notificacao. |
| 35 | Rastreamento de **processos criados** | `inventario.md:165` → `PsSetCreateProcessNotifyRoutine FAILED!` | **PARCIAL** - Rastreamento de **processos criados**/`src/Tranal.Uninstaller/Core/Modules/ProcessInspector.cs:74` (`public static class ProcessInspector`) **Nota**: existe a politica que decide quais processos entram no rastreio, mas nao ha callback de criacao — os processos sao observados por consulta. |
| 36 | Log com **eventos enumerados** e barra de progresso na bandeja | nao confirmado no binario | **PARCIAL** - Log com **eventos enumerados** e barra de progresso na bandeja/`src/Tranal.Uninstaller/Core/Reporting/TraceLogExporter.cs:37` (`public static class TraceLogExporter`) **Nota**: o log e enumerado e exportavel em Text, Csv e Html; nao ha barra de progresso nem bandeja. |
| 37 | **Salvar log** de rastreamento em arquivo | `strings.txt:5043` → `InstallTracker/btnSaveLog` | OK — `Core/Modules/TraceStore.cs`, `Core/Reporting/TraceLogExporter.cs` |
| 38 | **Reverter** a instalação rastreada | `strings.txt:5157` → `InstallTracker/btnNoAndRevert` | **PARCIAL** - **Reverter** a instalação rastreada/`src/Tranal.Uninstaller/Core/Recovery/ProgramBackup.cs:212` (`Register`), `:292` (`Undo`), `src/Tranal.Uninstaller/Core/Recovery/UndoChangesCenter.cs:81` (`Scan`), `src/Tranal.Uninstaller/Core/UninstallSupport.cs:1620` (`BackupLeftovers`) **Nota**: o motor de undo e redo tem consumidor de producao desde o lote 29 (a remocao entra no centro de mudancas quando o backup e gravado), mas o que se reverte e o conteudo removido, e nao o log de instalacao rastreado. |
| 39 | **Anexar** dados rastreados a um item existente | nao confirmado no binario | **OK** - **Anexar** dados rastreados a um item existente/o modulo das ligacoes entre programa e log `Core/Modules/TraceAttachment.cs:40`; o anexo do log ao programa `Core/Modules/TraceAttachment.cs:78` |
| 40 | Detecção de **múltiplas entradas** de instalação | nao confirmado no binario | **OK** - Detecção de **múltiplas entradas** de instalação/detector de entradas duplicadas `Core/Discovery/DuplicateInstallDetector.cs:97` |
| 41 | **Entrada customizada** quando o instalador não deixou registro | `strings.txt:14356` → `<checkCreateCustomEntry>I'd like to create a custom item in the list</checkCreateCustomEntry>` | **PARCIAL** - **Entrada customizada** quando o instalador não deixou registro/`src/Tranal.Uninstaller/Core/Automation/AdvancedUninstallReview.cs:89` (`public static class AdvancedUninstallReview`) **Nota**: a revisao do que o instalador nao registrou existe e e oferecida no assistente, mas o item criado e um registro de remocao assistida — nao uma entrada na lista principal. |
| 42 | Ver dados rastreados como **XML** e **desfazer o rastreamento** | `strings.txt:4957` → `MainMenu/mnuTracedDataAsXML`; `:14056` → `<mnuTracedDataAsXML>View as XML...</mnuTracedDataAsXML>` | **PARCIAL** - Ver dados rastreados como **XML** e **desfazer o rastreamento**/`src/Tranal.Uninstaller/Core/Modules/LogOperations.cs:165` (`public static class LogOperations`) `src/Tranal.Uninstaller/Core/Reporting/TraceLogExporter.cs:37` (`public static class TraceLogExporter`) **Nota**: editar, renomear, mesclar, ignorar e remover entradas do log existem; "ver como XML" nao — o exportador so tem Text, Csv e Html. |
| 43 | Coluna **"Rastreado"** na lista, com ícone próprio | `strings.txt:4926` → `ListCtrl/columnTraced` | OK — `Core/ProgramColumns.cs` |
| 44 | Menu de contexto do Explorer: **"Install and Trace"** para `.exe`/`.msi` | `strings.txt:14213` → `<checkShellExtension>Add 'Install and Trace' command for setup files (exe/msi) to the Windows Explorer context menu</checkShellExtension>` | **OK** - Menu de contexto do Explorer: **"Install and Trace"** para `.exe`/`.msi`/o modulo das entradas do Explorer `Core/Topmost/ExplorerContextMenu.cs:327`; a entrada Install and Trace `Core/Topmost/ExplorerContextMenu.cs:350` |
| 45 | Menu de contexto dos **atalhos**: "Uninstall with Uninstall Tool" | nao confirmado no binario | **PARCIAL** - Menu de contexto dos **atalhos**: "Uninstall with Uninstall Tool"/`src/Tranal.Uninstaller/Core/Topmost/ExplorerContextMenu.cs:28` (`public static class ExplorerContextMenu`) `src/Tranal.Uninstaller/Core/Modules/HunterMode.cs:38` (`public static class HunterMode`) **Nota**: o verb vale para os atalhos e o Tranal resolve atalhos, mas nao ha um verb especifico nem o rotulo dedicado do concorrente. |
| 46 | Extensão de shell **empacotada em MSIX** para o Windows 11 | nao confirmado no binario (o pacote `.msix` é arquivo do instalador, não recurso do PE) | **AUSENTE** - Extensão de shell **empacotada em MSIX** para o Windows 11/o instalador (`installer/tranal.iss`) cria grupo e area de trabalho; nao ha empacotamento MSIX da extensao de shell. |
| 47 | Serviço de rastreamento **versionado**, com prompt de atualização | nao confirmado no binario | **OK** - Serviço de rastreamento **versionado**, com prompt de atualização/o modulo do servico de rastreio `Core/Modules/TraceService.cs:39`; a versao do servico `Core/Modules/TraceService.cs:61` |
| 48 | Setup instala o serviço e o **driver de kernel** | `inventario.md:274` → `driver file doesn't seem to exist in the app folder` | **FORA POR DECISAO** - Decisao registrada: "Driver de filtro (`revoflt.sys`) - Fora de escopo por decisao registrada (driver de kernel)". |

### 2.4 Startup Manager

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 49 | Listagem com colunas **Status** e **Origem** | nao confirmado no binario | OK — `Core/Tools/AutorunManager.cs` |
| 50 | Habilitar / **desabilitar** item de inicialização | `strings.txt:14170` → `<itemDisableRunning>Disable running on startup</itemDisableRunning>`; `:4794` → `ExplorerBar/itemDisableRunning` | OK — `Core/Tools/AutorunManager.cs` (`SetEnabled`) |
| 51 | **Remover** item de inicialização do registro/Start Menu | `strings.txt:14172` → `<itemDeleteStartupItem>Delete from the list</itemDeleteStartupItem>`; `:4795` → `ExplorerBar/itemDeleteStartupItem` | OK — `Core/Tools/AutorunManager.cs` (`Remove`) |
| 52 | **Adicionar** item de inicialização (caminho, argumentos, "executar com privilégios máximos") | `strings.txt:4773` → `DlgAddNewStartupProgram/DlgAddNewStartupProgramCaption`; `:4776` → `labelArguments`; `:4777` → `checkHighestPriviledges` | OK — `Core/Tools/AutorunManager.cs` (`Add`) |
| 53 | **Filtro por categoria** de origem: Registry / Start Menu / Tasks / UWP | nao confirmado no binario | **PARCIAL** - **Filtro por categoria** de origem: Registry / Start Menu / Tasks / UWP/`src/Tranal.Uninstaller/Core/Appearance/ProgramFilters.cs:147` (`ProgramFilterSet`) **Nota**: a origem aparece na coluna Source, mas nao ha filtro por origem nem as categorias Start Menu e Tasks. |
| 54 | **Busca/filtro** da lista de startup | nao confirmado no binario | **OK** - A varredura cobre Run, RunOnce, RunServices, pastas de inicializacao, servicos e tarefas agendadas (`AutorunManager.cs:110-120`), e os itens entram na lista principal, onde valem a busca e o filtro por coluna. |
| 55 | Itens de inicialização de **aplicativos UWP / Store** | nao confirmado no binario | **OK** - Itens de inicialização de **aplicativos UWP / Store**/modulo da inicializacao da Store `Core/Discovery/StoreStartupEntries.cs:52`; a listagem das StartupTask `Core/Discovery/StoreStartupEntries.cs:63` |
| 56 | Tarefas do **Task Scheduler** agendadas para logon, com "navegar até a origem" | nao confirmado no binario | OK — `Core/Management/ScheduledTaskManager.cs` |
| 57 | Tarefas criadas pela Microsoft **ocultadas** por segurança | nao confirmado no binario | **OK** - Tarefas criadas pela Microsoft **ocultadas** por segurança/modulo de tarefas ocultas `Core/Management/ScheduledTaskVisibility.cs:60`; a listagem das tarefas ocultas `Core/Management/ScheduledTaskVisibility.cs:111` |
| 58 | Alternância rápida **Uninstaller ⇄ Startup Manager** (Ctrl+Tab) | nao confirmado no binario | **OK** - A troca de tela existe e e o que liga o painel de programas ao gerenciador de inicializacao. |

### 2.5 Interface e integração

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 59 | **Smart Search** — filtragem incremental enquanto se digita | `strings.txt:4787` → `ExplorerBar/itemSearch`; `:14153` → `<itemSearch>Search</itemSearch>` | OK — `Core/Appearance/ProgramFilters.cs` |
| 60 | **Atalhos de teclado**: F1, F5, F9, Ctrl+Tab, Ctrl+S, Enter, Shift+Del, Ctrl+G, Ctrl+R, Ctrl+E, Ctrl+W, Esc | nao confirmado no binario | **PARCIAL** - **Atalhos de teclado**: F1, F5, F9, Ctrl+Tab, Ctrl+S, Enter, Shift+Del, Ctrl+G, Ctrl+R, Ctrl+E, Ctrl+W, Esc/`src/Tranal.Uninstaller/Core/Shortcuts/ShortcutMap.cs:59` (`public static class ShortcutMap`) **Nota**: existem 10 atalhos; faltam F9, Ctrl+Tab, Ctrl+S, Ctrl+G, Ctrl+R e Ctrl+W. |
| 61 | **Exportar a lista para HTML** | `strings.txt:4646` → `MainMenu/mnuFileExport`; `:14046` → `<mnuFileExport>&amp;Export to HTML...</mnuFileExport>` | OK — `Core/Exporters.cs` |
| 62 | Painel lateral com **Desinstalar / Forçar a Remoção**, caminho, versão, autor, data e site | nao confirmado no binario | OK — `Ui/Modules/ModuleViews.cs` |
| 63 | **Preferências** em 4 seções: Geral, Interface, Lista de Aplicativos, Uninstall Wizard | nao confirmado no binario | OK — `Core/Settings/AppSettings.cs`, `Ui/OptionsWindow.xaml.cs` |
| 64 | **Modo escuro** (automático / claro / escuro) | `strings.txt:4631` → `DlgPreferences/colorSchemeDark` | OK — `Theming/ThemeManager.cs` |
| 65 | **45 idiomas** via XML | `inventario.md:38` → `LanguageStrings version="3.8.2" LangID="1033" Author="CrystalIDEA Software Team"`; `identificadores.txt:13` → `/LanguageStrings` | OK — `Core/Localization/LanguageCatalog.cs` |
| 66 | **Portable Mode** — corre de um pendrive, chave em `license.dat` | `strings.txt:14459` → `<msgRegPortableLicenseLink>Download the portable version</msgRegPortableLicenseLink>`; `:14461` → `<msgRegNotForPortableText>...run the portable version.</msgRegNotForPortableText>` | OK — `Core/PortableMode.cs` |
| 67 | Relatório de **crash** com ficheiro de dump | nao confirmado no binario | **PARCIAL** - Relatório de **crash** com ficheiro de dump/`src/Tranal.Uninstaller/Core/Modules/CrashDumpCollector.cs:87` (`public static class CrashDumpCollector`) `src/Tranal.Uninstaller/Ui/Tools/MaintenanceViews.cs:301` (`CrashDumpCollector`) **Nota**: os dumps de falha do Windows sao coletados, agrupados por programa e mostrados; nao ha o relatorio de crash do proprio Tranal com arquivo de dump. |

### 2.6 Linhas novas: funcionalidades presentes no binário e ainda não no documento

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 68 | **Contornar o UAC** criando uma tarefa elevada do Task Scheduler executada a cada abertura do app | `strings.txt:14207` → `<checkSkipUAC>Use Windows Task Scheduler to bypass User Account Control confirmation dialog (when necessary)</checkSkipUAC>`; `:14208` → `<checkSkipUACHelpText>...creates a Windows Task Scheduler elevated task which is executed every time you'd like to run the app.</checkSkipUACHelpText>`; `:15548` → `control.exe schedtasks` | **FORA POR DECISAO** - Decisao registrada: "Pular o prompt do UAC - decisao: manter o caminho de UAC do Windows". |
| 69 | **Ícone do Uninstall Tool no Painel de Controle** | `strings.txt:14212` → `<checkControlPanelIcon>Show Uninstall Tool icon in Control Panel</checkControlPanelIcon>`; `:17818` → `Control Panel icon` | **OK** - **Ícone do Uninstall Tool no Painel de Controle**/o modulo do icone no painel de controle `Core/Management/ControlPanelIcon.cs:17`; a criacao da entrada `Core/Management/ControlPanelIcon.cs:50` |
| 70 | **Popup de aplicações recém-instaladas** | `strings.txt:14233` → `<checkDisplayPopup>Display pop-up window with recently installed applications</checkDisplayPopup>`; `:17828` → `Display popup window with recently installed applications` | **OK** - **Popup de aplicações recém-instaladas**/o rastreio dos programas novos `Core/Discovery/NewInstallsTracker.cs:47`; a deteccao do que apareceu `Core/Discovery/NewInstallsTracker.cs:129` |
| 71 | **Realce dos programas recém-instalados** | `strings.txt:14234` → `<checkHighlighRecent>Highlight recently installed programs</checkHighlighRecent>` | **OK** - **Realce dos programas recém-instalados**/a marca de programa novo `Core/Discovery/NewInstallsTracker.cs:161` |
| 72 | **Programas recentes na barra secundária** | `strings.txt:14235` → `<checkRecentOnSecondaryToolbar>Display recent programs in the secondary toolbar</checkRecentOnSecondaryToolbar>` | **OK** - **Programas recentes na barra secundária**/a listagem dos programas recentes `Core/Discovery/NewInstallsTracker.cs:47` |
| 73 | **Exibir atualizações de software** na lista | `strings.txt:14227` → `<checkDisplayUpdates>Display software updates</checkDisplayUpdates>`; `:17833` → `Display software updates:` | **PARCIAL** - **Exibir atualizações de software** na lista/`src/Tranal.Uninstaller/Core/Updates/UpdateService.cs:29` (`public static class UpdateService`) `src/Tranal.Uninstaller/Ui/Tools/UpdateViews.cs:22` (`UpdateService`) **Nota**: as atualizacoes tem tela e comando proprios, mas nao ha coluna nem item de atualizacao na lista principal. |
| 74 | **Verificar atualizações ao iniciar**, com canal **beta** | `strings.txt:14209` → `<checkCheckForUpdates>Check for updates on program startup</checkCheckForUpdates>`; `:14210` → `<checkCheckForBetaVersions>Check for beta versions as well</checkCheckForBetaVersions>` | **PARCIAL** - **Verificar atualizações ao iniciar**, com canal **beta**/`src/Tranal.Uninstaller/Core/Updates/UpdateService.cs:29` (`public static class UpdateService`) **Nota**: a verificacao ao iniciar existe e pode atualizar em segundo plano, mas nao ha canal beta. |
| 75 | **Personalizar aparência, barras de ferramentas e menus**, com legendas nos botões e barra de status editável | `strings.txt:18030` → `CCustomize application appearance, toolbars, and menus`; `:17811` → `Display toolbar buttons captions`; `:14217` → `<checkToolBarCaptions>...`; `inventario.md:237` → `Customize Status Bar4(%s) was pressed...` | **PARCIAL** - **Personalizar aparência, barras de ferramentas e menus**, com legendas nos botões e barra de status editável/`src/Tranal.Uninstaller/Theming/ThemeManager.cs:41` (`public static class ThemeManager`) `src/Tranal.Uninstaller/Core/Settings/AppSettings.cs:315` (`public static class AppSettings`) **Nota**: a visibilidade das barras e os quatro temas existem; faltam as legendas nos botoes e a barra de status editavel. |
| 76 | **Minimizar na bandeja durante a instalação** | `strings.txt:14295` → `<checkMinimize>Minimize Uninstall Tool to system tray during installation</checkMinimize>` | **OK** - **Minimizar na bandeja durante a instalação**/a regra de minimizar durante a desinstalacao `Core/Management/SystemTray.cs:43`; a minimizacao na janela principal `MainWindow.xaml.cs:166` |
| 77 | **Exibir o ícone na bandeja do sistema** | `inventario.md:271` → `Display Uninstall Tool system tray icon` | **OK** - **Exibir o ícone na bandeja do sistema**/o icone na bandeja `Ui/Tools/TrayIconManager.cs:76`; o registro do icone `Ui/Tools/TrayIconManager.cs:87` |
| 78 | **Iniciar automaticamente minimizado na bandeja** | `inventario.md:56` → `Automatically start Uninstall Tool minimized in system tray on Windows startup` | **OK** - **Iniciar automaticamente minimizado na bandeja**/a regra de iniciar minimizado `Core/Management/SystemTray.cs:36`; a montagem da bandeja no arranque `MainWindow.xaml.cs:112` |
| 79 | **Navegar até a pasta do programa com o Total Commander** | `strings.txt:14214` → `<checkTotalCommander>Navigate to program directory using Total Commander (if installed)</checkTotalCommander>` | **OK** - **Navegar até a pasta do programa com o Total Commander**/o modulo das ferramentas externas `Core/Tools/ExternalLaunchers.cs:33`; a abertura no Total Commander `Core/Tools/ExternalLaunchers.cs:91` |
| 80 | **Criar item personalizado na lista** | `strings.txt:14356` → `<checkCreateCustomEntry>I'd like to create a custom item in the list</checkCreateCustomEntry>` | **PARCIAL** - **Criar item personalizado na lista**/`src/Tranal.Uninstaller/Core/Automation/AdvancedUninstallReview.cs:89` (`public static class AdvancedUninstallReview`) **Nota**: existe a criacao assistida de item para quando o instalador nao deixou registro, mas ela nao entra na lista de programas. |
| 81 | **Adiar a exclusão dos dados de sobras** ("save and delete the leftover data later") | `strings.txt:14480` → `<checkDeleteDataLater>Save and delete the leftover data later</checkDeleteDataLater>` | OK — `Core/DeferredDelete.cs` |
| 82 | **Lista compacta de aplicativos** | `strings.txt:14219` → `<checkSmallIcons>Compact list with apps</checkSmallIcons>` | **OK** - **Lista compacta de aplicativos**/o modulo do formato da lista `Core/Appearance/ProgramListLayout.cs:15`; as colunas da lista compacta `Core/Appearance/ProgramListLayout.cs:21` |
| 83 | **Chave de licença com lista negra** e chave separada para a versão portátil | `strings.txt:4592` → `activation: black listed key`; `:14460` → `<msgRegNotForPortableTitle>This key cannot activate the portable version</msgRegNotForPortableTitle>`; `:5692` → `checking license key {}...` | **FORA POR DECISAO** - Fora de escopo por decisao: o Tranal e reimplementacao independente, sem licenca, sem cadastro e sem telemetria. |
| 84 | **Limpar o cache** da aplicação | `inventario.md:168` → `Clear cache` | **PARCIAL** - **Limpar o cache** da aplicação/`src/Tranal.Uninstaller/Core/Settings/ProgramCache.cs:154` (`public static class ProgramCache`) **Nota**: o cache e invalidado e reconstruido na varredura seguinte; nao ha "limpar cache" como acao sobre o cache do aplicativo. |
| 85 | **Painéis ancoráveis** ("push pin") com pré-visualização | `inventario.md:169` → `Click push pin to keep open`; `:201` → `Close Preview`; `:203` → `Close Window` | **OK** - **Painéis ancoráveis** ("push pin") com pré-visualização/o modulo do painel ancorado `src/Tranal.Uninstaller/Core/Appearance/DockedPanel.cs:37` (`Toggle`); a pre-visualizacao `src/Tranal.Uninstaller/Core/Appearance/PanelPreview.cs:47` (`Publish`) e `src/Tranal.Uninstaller/Core/Appearance/PanelPreview.cs:65` (`Forget`); a janela `src/Tranal.Uninstaller/MainWindow.xaml:845` (`DockPanelBorder`) e `src/Tranal.Uninstaller/MainWindow.xaml.cs:998` (`RefreshDock`); a linha de comando `src/Tranal.Cli/ExtraCommands.cs:90` (`/painel`); os testes `src/Tranal.Tests/Lote17FeatureTests.cs` e o teste de ponta a ponta `tools/test-painel-ancorado.ps1`. Binario do concorrente: `Click push pin to keep open`, `Close Preview`, `Close Window`. |
| 86 | **Execução de processo 32 bits** (`CreateX86Process`) | `inventario.md:230` → `CreateX86Process OK` | **OK** - **Execução de processo 32 bits** (`CreateX86Process`)/o modulo do processo de 32 bits `Core/Tools/Wow64Process.cs:21`; a execucao em 32 bits `Core/Tools/Wow64Process.cs:62` |
| 87 | **Ciclo de vida de serviço do Windows** gerenciado pelo próprio app | `imports.txt:16` → `ADVAPI32.dll / CreateServiceW`, `DeleteService`, `ControlService`; `inventario.md:216` → `ControlService failed with error {}`; `:229` → `CreateService ({}) failed with error {}`; `:254` → `DeleteService ({}) failed with error {}` | OK — `Core/Management/ServiceManager.cs` |
| 88 | **Log da exclusão de chaves de registro** | `inventario.md:253` → `Deleted regkey '{}'` | OK — `Core/RemovalLog.cs` |
| 89 | **Persistir em XML os itens de inicialização desativados**, por arquitetura | `inventario.md:113` → `Cannot load DisabledStartupItems.xml for current architecture.`; `:265` → `Disabled item loaded: {}, X64: {}` | OK — `Core/Tools/AutorunManager.cs` |
| 90 | **Detecção de falso positivo** ao fixar a localização de um item | `inventario.md:115` → `Cannot set location for item (false positive risk) {}` | **OK** - Sete criterios de confianca de pasta (vazia, executaveis, dados do usuario, fabricante conhecido, nome, pasta de programas, pasta do usuario) avaliam a localizacao antes de a Localizacao ser fixada, e o consumidor esta no fluxo de sobra (`UninstallSupport.cs:1128`). |
| 91 | **Coleta de identificação da máquina** para a licença | `inventario.md:227` → `cpu name is {}`; `:242` → `Data folder is {}, ({})` | **FORA POR DECISAO** - Mesma decisao da linha 83: sem licenca, sem cadastro e sem telemetria. |
| 92 | **Validação de cadeia de certificados** embarcada (CRYPTO/SSL) | `imports.txt:48` → `CRYPT32.dll / CertVerifyCertificateChainPolicy`, `CertGetCertificateChain` | **OK** - **Validação de cadeia de certificados** embarcada (CRYPTO/SSL)/a cadeia de subjects do certificado `Core/Discovery/UninstallerCertificate.cs:64`; a verificacao do certificado `Core/Discovery/UninstallerCertificate.cs:114` |
| 93 | **Preferências persistidas em XML** com chaves `check*` | `strings.txt:14201` → `<checkHighestPriviledges>Run task with the highest privileges</checkHighestPriviledges>`; `:14381` → `<checkDontAskAgain>...` | OK — `Core/Settings/AppSettings.cs` |
| 94 | **Install Tracker com driver de kernel**: minifiltro de sistema de arquivos + callback de registro + callback de processos | `inventario.md:159` → `CisUtMonitor: DriverEntry`; `:161` → `FltRegisterFilter FAILED!`; `:160` → `CmRegisterCallback FAILED!`; `:165` → `PsSetCreateProcessNotifyRoutine FAILED!` | **FORA POR DECISAO** - Driver de kernel fora do plano do Tranal, por decisao registrada. O que o Tranal faz e rastrear por snapshot (`Core/Modules/InstallMonitor.cs:31`), sem driver. |

### 2.7 Lacunas confirmadas no binário

A lista completa, com evidência e juizo de impacto por linha, está em **[BINARIO-UNINSTALLTOOL.md](BINARIO-UNINSTALLTOOL.md)**.

- **32 linhas `AUSENTE`** — 10, 15, 18, 19, 28, 38, 39, 40, 41, 44, 45, 46, 47, 48, 57, 67, 68, 69, 70, 72, 76, 77, 78, 79, 80, 82, 83, 84, 90, 91, 92, 94
- **28 linhas `PARCIAL`** — 3, 4, 5, 6, 7, 8, 21, 22, 29, 30, 31, 32, 33, 34, 35, 36, 42, 53, 54, 55, 58, 60, 71, 73, 74, 75, 85, 86
- **94 linhas** na tabela de funcionalidades (67 originais + 27 novas)

Correção em relação à §6 deste documento: a **desinstalação em lote** e a **remoção de entradas em lote** **existem** no Tranal — `Core/Automation/BatchList.cs` e `Core/Reporting/BatchScriptWriter.cs`, com o verbo `/batch` na CLI. As marcas `⬜` da §6 estão desatualizadas.

---

## 3. Mecanismos notáveis

### 3.1 O driver de kernel — minifiltro `CisUtMonitor.sys`

O binário principal **embute o driver**. Strings medidas:

| linha | string | leitura |
|---|---|---|
| 14946 | `CisUtMonitor: DriverEntry` | ponto de entrada |
| 14949 | `UtMonCreateRootDeviceObject: FltRegisterFilter FAILED!` | **minifiltro de sistema de arquivos** |
| 14950 | `… FltStartFiltering FAILED!` | arranque do minifiltro |
| 14951 | `… CmRegisterCallback FAILED!` | **callback de registro** |
| 14952 | `… PsSetCreateProcessNotifyRoutine FAILED!` | **callback de processos** |
| 14958 | `G:\Projects\uninstall-tool\Driver\build\CisUtMonitor.pdb` | caminho PDB |
| 15661 | "CisUtMonitor driver is a part of Uninstall Tool Install Tracker module. It's responsible for tracking of newly created files and Windows Registry items during installation of a traced app." | **descrição oficial do driver** |
| 15664 | `SYSTEM\CurrentControlSet\Services\CisUtMonitor` | chave de serviço |
| 11574 | `DeviceIoControl` | canal app ↔ driver |

**Estado observado em execução:** no modo **portátil**, `CisUtMonitor`,
`CisUtMonFlt` e `UninstallToolService` estão **ausentes** e nenhum driver CIS
é carregado — o driver e o serviço **só existem na variante instalada**.

### 3.2 Serviço de rastreamento `CisUtMonitor`

`OpenServiceW` / `StartServiceW` / `CreateServiceW` para o ciclo de vida;
`msgTrackingServiceUpdate` — *"The installed 'CisUtMonitor' service version (%u)
is older than current (%u)"* — é **controle de versão do serviço**. Removível
por `/uninstall_service`, que é literalmente o que o `RemoveService.cmd` da
versão portátil executa.

### 3.3 Extensão de shell

| alvo | mecanismo |
|---|---|
| Windows 10/7 | **DLL de shell** `utshellext.dll` |
| Windows 11 | **MSIX** — `UTShellExt2.msix`, pacote `UninstallToolWindows11ContextMenu`, switch interno `/msix_register` |

### 3.4 Integração com o Windows

`appwiz.cpl,,2` (Abrir/Remover Programas) · `Remove-AppxPackage` (Store) ·
`regsvr32 /s /u` (desregistrar COM) · `CLSID_TaskScheduler` (a tarefa elevada
que contorna o UAC) · `CreateProcessWithTokenW` · `CreateToolhelp32Snapshot` ·
`CreateFileTransactedW` · WMI para identificação de hardware
(`Win32_VideoController`, `Win32_ComputerSystem`, `Win32_BaseBoard`).

### 3.5 Ausências comprovadas por leitura de strings

- **Sem `CreateRestorePoint`** → não cria ponto de restauração antes de
  desinstalar.
- **Sem `FilterConnect`** → o driver não expõe canal de usuário por
  FilterConnect (usa `DeviceIoControl`).
- **Sem** limpador de lixo, otimizador de registro genérico, backup de
  registro, histórico de desinstalação → nenhuma string, chave de UI nem item
  de menu correspondente no dicionário de **427 chaves**.

### 3.6 Comparação de mecanismos com os concorrentes já auditados

| mecanismo | **Uninstall Tool** | IObit | Wise | HiBit |
|---|---|---|---|---|
| driver de kernel | **SIM** — `CisUtMonitor.sys` | não documentado | não documentado | **sem** driver |
| serviço de fundo | **SIM** — versionado, instalável à parte | `UninstallMonitor.exe` é **processo**, não serviço | `UnMonitor.exe` é **processo**, não serviço | **sem** |
| extensão de shell | **SIM** — DLL (Win10) + **MSIX** (Win11) | `IUMenuRight.dll` | não medido | chave de registro |
| log de instalação | eventos de kernel + **XML** | **SQLite** | processo `UnMonitor.exe` | eventos WMI |
| ponto de restauração | **não** | **SIM** | **SIM** | **SIM** |
| log detalhado de remoção por item | **SIM** | não medido | não medido | não medido |

**Conclusão:** o Uninstall Tool é o **único** da série com rastreamento por
**evento de kernel**; ninguém mais tem driver + serviço versionado. Em
contrapartida, **é o único que não cria ponto de restauração** — IObit, Wise e
HiBit criam.

---

## 4. Interface

### 4.1 A janela principal observada

| elemento | observado |
|---|---|
| título | `Uninstall Tool 3.8.2 [VERSÃO NÃO REGISTRADA]` |
| menu | `Arquivo` · `Ação` · `Exibir` · `Ferramentas` · `Ajuda` |
| barra principal | `Desinstalador` · `Iniciar` (Startup Manager) · `Atualizar` · `Preferências` · `Ajuda` |
| painel esquerdo | item selecionado "AMD Install Manager"; `Desinstalar` e `Forçar a Remoção`; caminho `C:\Program Files\AMD\AMDInstallManager\`; Versão, Autor, Data de Instalação; link do site; campo `Procurar` |
| lista central | colunas `Nome do Programa` \| `Tamanho` \| `Instalado em` \| `Rastreado`; itens com marca `(32-bit)` |
| barra secundária | `Aplicativos` · `Sistema` · `Oculto` · `Microsoft Store Apps` · `Instalar e Rastrear` |
| barra de estado | **`79 programas com um total de 658 GB`** |

### 4.2 O menu completo (49 chaves `MainMenu/*`)

- **Arquivo**: Export to HTML… · Exit
- **Ação**: Uninstall… · Modify… · Force Removal · **Batch Force Removal** ·
  Remove Entry · **Batch Remove Entries** · *Traced Data* (View as XML… · Undo
  Tracing · Append traced data) · Registry Entry · Installation Folder ·
  Program Website · Open in Microsoft Store · Browse Location… · Browse
  Source… · Google for
- **Exibir**: Main Toolbar · Secondary Toolbar · Refresh · Reset Columns ·
  Interface Language
- **Ferramentas**: *System Tools* (Add/Remove Programs… · Windows Components… ·
  System Restore · Security Center · System Properties · System Information ·
  Disk Defragmenter · Services · Task Scheduler) · Preferences…
- **Ajuda**: Online help… · Home Page · Support · Purchase… · Enter Key… ·
  Check for updates… · About…

**O menu Ferramentas é a prova da fase do produto**: ele só **chama
ferramentas do Windows**. Não há limpador, nem otimizador, nem backup.

### 4.3 Limitação de medição

`uia-principal.txt` devolve **11 elementos, todos `Pane`** — a app é nativa
(Codejock XTP) e `SetForegroundWindow`/`SetWindowPos` falham com
`ERROR_ACCESS_DENIED` nesta máquina; `PrintWindow` falha em forms Codejock.
**Nenhuma funcionalidade foi provada por árvore UIA** — a §4 apoia-se nas 9
capturas de tela e no dicionário de UI.

---

## 5. Linha de comando

### 5.1 Switches da APLICAÇÃO (publicados pelo fabricante)

| switch | efeito |
|---|---|
| `/install [setupfile]` | instala e **rastreia** um `.exe` ou `.msi` |
| `/install_log [logfile]` | caminho do ficheiro de log do rastreamento |
| `/uninstall_service` | **remove o serviço de rastreamento** (uso interno) |
| `/startup` | abre a app com o **Startup Manager** já ativo |
| `/store_apps` | abre a app com as **aplicações da Store** já ativas |

Switch **interno, não publicado**: `/msix_register` (registro da extensão de
shell do Windows 11).

### 5.2 Switches do INSTALADOR — não confundir com os da app

O setup é **Inno Setup 6.6.0**, logo aceita a família padrão
(`/SILENT`, `/VERYSILENT`, `/SUPPRESSMSGBOXES`, `/DIR=`), confirmada por
strings do próprio instalador e pela página oficial. **Switches próprios do
instalador: NÃO VERIFICADOS** — o `innoextract` 1.9 local não lê a 6.6.0
("Unexpected setup loader revision: 2", exit 2) e o setup não foi executado.

### 5.3 O resumo que interessa

**5 switches publicados na aplicação, e nenhum para desinstalar em lote,
exportar, gerar snapshot ou agendar.** O Uninstall Tool **não tem CLI pública
de automação** além desses cinco — distinção importante face ao Tranal, cuja
CLI tem dezenas de verbos.

---

## 6. O que o Tranal NÃO faz hoje

Grep real executado em `src\` (78 ficheiros de produção, sem `obj`/`bin`/testes);
evidência bruta em `evidencia/tranal-grep.txt`. "Não" significa **zero
ocorrências** do padrão em todo o `src/`.

| Funcionalidade do Uninstall Tool | Tranal | Onde no Tranal |
|---|---|---|
| Rastreamento por **driver de kernel** | ⬜ | — — o Tranal faz **polling de snapshot de 30 s** (`Core/Modules/InstallMonitor.cs:31,48`); grep `minifilter|FltRegister|FilterConnect|kernel driver` = 0 hits |
| **Serviço de sistema** versionado e instalável à parte | ⬜ | — — não há infra de serviço no `src/` |
| Extensão de shell **COM/MSIX** | 🟡 | `Core/Topmost/ExplorerContextMenu.cs:28,37,40` — é só chave de registro; grep `IShellExtInit|DllGetClassObject|COMServer` = 0 hits |
| Menu de contexto "Install and Trace" para `.exe`/`.msi` | ⟡ | `Ui/Modules/ModuleViews.cs:83` — a Tracing via UI existe, a integração com o Explorer não foi localizada |
| **Remoção forçada** sem rodar o desinstalador | ✅ | `Core/UninstallSupport.cs`, `Core/Modules/HunterMode.cs`, `Ui/ForcedUninstallWindow.xaml.cs` |
| Remoção de arquivos em uso **agendada para o próximo boot** | ✅ | `Core/DeferredDelete.cs:12-47` (`MoveFileEx` + `MOVEFILE_DELAY_UNTIL_REBOOT`) |
| **Detecção e terminação de processos** | ✅ | `Core/ProcessTerminator.cs:61` |
| **Desinstalação em lote** | ⬜ | — — grep `Batch|batchUninstall|MultiSelect` só acha `Multiselect` em duas janelas |
| **Remoção de entradas em lote** | ⬜ | — idem |
| **Log detalhado de remoção por item**, com código de erro | ⬜ | `Core/MsiInstallLog.cs` é *leitor*; `FatalLog.cs` só registra falha de arranque |
| **Exportar a varredura de sobras para XML** | ⬜ | `Core/Exporters.cs` exporta a **lista** |
| **Reverter** a instalação rastreada | ⬜ | — — grep `Revert` = 1 hit, só comentário |
| Rastreamento de **renomeação** de arquivos/pastas | ⬜ | — |
| Rastreamento de **processos criados** | ⬜ | `Core/Modules/SystemSnapshot.cs` não apanha processos |
| **Snapshot do sistema antes/depois** | ✅ | `Core/Modules/SystemSnapshot.cs:43` (`Capture()`/`Diff()`) — o Uninstall Tool **não tem** |
| **Filtro por data/tamanho** | ✅ **superior** | `MainWindow.xaml.cs:114,130,142`; o Uninstall Tool só ordena |
| **Backup / restauração** | ✅ **superior** | `Core/BackupManager.cs`, `Core/FullRegistryBackup.cs` — o Uninstall Tool **não tem** backup |
| **Agendamento** | 🟡 | `Core/Tools/AutorunManager.cs` **gere** tarefas; o Uninstall Tool **cria** uma tarefa elevada para contornar o UAC |
| Detecção de **itens relacionados/dependentes** | ⬜ | — |
| **Cancelamento forçado** de desinstalador travado | ⟡ | `Core/ProcessTerminator.cs` termina processos, não o desinstalador |
| **Desinstalador incorporado** (motor próprio) | 🟡 | `Core/UninstallSupport.cs` + `Core/Modules/HunterMode.cs` — o Uninstall Tool **não tem** motor próprio |
| Desinstalar apps **Microsoft Store** | ✅ | `Core/Modules/WindowsApps.cs` (ambos usam `Remove-AppxPackage`) |
| **Startup Manager** | 🟡 | `Core/Tools/AutorunManager.cs` — o Uninstall Tool ganha no **filtro por categoria** e nos itens UWP |
| Remoção de sobras (arquivo + registro) em árvore | ✅ | `Core/UninstallSupport.cs`, `Core/Modules/HunterMode.cs` |
| Limpeza de **extensões de navegador** | ✅ **Tranal sim / Uninstall Tool não** | `Core/Modules/BrowserExtensions.cs:53` |
| **Limpador de lixo** | ✅ **Tranal sim / Uninstall Tool não** | `Core/Tools/JunkCleaner.cs:38` |
| **Otimizador de registro** | ✅ **Tranal sim / Uninstall Tool não** | `Core/Tools/RegistryOptimizer.cs:55,72,82` |
| **Ponto de restauração** antes de desinstalar | ✅ **só o Tranal** | Uninstall Tool: sem `CreateRestorePoint`. Tranal: `Core/UninstallSupport.cs:1170` (`TryCreateRestorePoint`, WMI `SystemRestore`, `RestorePointType` 12), acionado no assistente (`Ui/UninstallWizardWindow.xaml.cs:396`) e na CLI (`src/Tranal.Cli/Program.cs:348`), ligado por padrão (`Core/Settings/AppSettings.cs:48`) |
| Idiomas | 🟡 | `Core/Localization/LanguageCatalog.cs:16` — o Uninstall Tool tem 45 XML |
| **CLI pública de automação** | ✅ **superior** | `src/Tranal.Cli/` — o Uninstall Tool tem 5 switches, nenhum de automação |

### Onde o Tranal já é melhor

O Tranal **cobre** os dois terços funcionais do Uninstall Tool e o **supera** em
manutenção (lixo, registro, extensões de navegador, backup), em filtragem, em
ponto de restauração, em snapshot e em CLI. As **5 lacunas estruturais** do
Tranal face a este concorrente são: ausência de **driver de kernel** para
rastreamento fino; ausência de **serviço de fundo versionado**; ausência de
**extensão de shell COM/MSIX**; ausência de **desinstalação/remoção em
lote**; e ausência de **log de remoção por item** e de **reverter instalação**.

---

## 7. Fichos de evidência

| Arquivo | Bytes | Conteúdo |
|---|---|---|
| `evidencia/manual-release-notes.txt` | 37.525 | release notes 1.0-beta → 3.8.2 (**pista mais rica**) |
| `evidencia/ui-strings-english.txt` | 35.097 | dicionário de UI: **427 chaves** em 27 seções |
| `evidencia/strings-UninstallTool_x64.txt` | 522.233 | strings do app 64 bits (imports e mensagens de log) |
| `evidencia/strings-setup.txt` | 200.382 | strings do instalador Inno (novo nesta auditoria) |
| `evidencia/ui-English.xml` | 42.364 | idioma inglês original |
| `evidencia/manual-command-line.txt` | 2.112 | switches da app e do instalador |
| `evidencia/manual-preferences.txt` | 4.611 | 4 seções de preferências |
| `evidencia/manual-keyboard-shortcuts.txt` | 2.588 | atalhos |
| `evidencia/manual-programs-list.txt` | 2.210 | lista multi-coluna |
| `evidencia/manual-smart-search.txt` | 1.540 | Smart Search |
| `evidencia/manual-uninstall-wizard.txt` | 2.765 | Uninstall Wizard |
| `evidencia/manual-buy.txt` / `-download` / `-support` | 2.415 / 1.366 / 2.021 | preços, download, suporte |
| `evidencia/site-…crystalidea…txt` (+`_faq`) | 4.134 (+2.140) | página inicial e FAQ |
| `evidencia/pe-*.txt` (4) | ~800-1.000 cada | cabeçalhos PE, assinatura, DLLs importadas |
| `evidencia/sysstate-antes.txt` / `-depois-rodando.txt` / `-final.txt` | 587 / 674 / 587 | serviços, drivers e processos antes/depois |
| `evidencia/uia-principal.txt` | 555 | árvore UIA (limitada — ver §4.3) |
| `evidencia/tranal-grep.txt` | 14.834 | evidência bruta dos greps no `src/` do Tranal |
| `evidencia/02-janela-principal.png` … `06-printwindow.png` | 30 KB – 1,4 MB | 9 capturas de tela |

---

## 8. Lacunas desta auditoria

1. **O instalador não pôde ser extraído** — `innoextract` 1.9 não lê Inno Setup
   6.6.0 ("Unexpected setup loader revision: 2", exit 2); as opções e switches
   próprios do instalador **não foram enumerados** e o setup **não foi
   executado**.
2. **A variante Professional (paga) não foi exercitada** — a execução foi em
   modo trial/não registrado ("[VERSÃO NÃO REGISTRADA]"). As strings indicam
   que **em modo não registrado o Uninstall Wizard não guarda os dados de
   sobras** (`Trial/msgNotAvailableInTrial`), então o comportamento completo do
   Wizard e do Install Tracker **não foi medido**.
3. **O driver e o serviço não foram observados em runtime** — o modo portátil
   não os instala; o comportamento do minifiltro **instalado** (o mecanismo
   mais diferencial do produto) é **inferido** das strings do driver embutido.
   Medi-lo exigiria instalar o setup 3.8.2 e observar `fltmc filters`.
4. **Domínio** — `uninstalltool.com` responde **por HTTP** (redireciona para
   `crystalidea.com`); por **HTTPS dá timeout**, e o Wayback **não tem
   snapshot nenhum**. Todo o conteúdo oficial veio de `crystalidea.com` e do
   **GitHub Releases**, que é o canal de download real.
5. **Captura de janela indisponível** — `ERROR_ACCESS_DENIED` em todas as
   janelas e `PrintWindow` falha em Codejock; a árvore UIA tem 11 elementos
   inúteis. **Nenhuma funcionalidade foi provada por captura de janela**.
6. **Filtro interativo por data/tamanho no Uninstall Tool: não verificado** —
   não existe string de UI para isso (427 chaves conferidas); só há ordenação.
   A existência de filtro escondido não pôde ser descartada.
7. **"Snapshot antes/depois" do sistema no Uninstall Tool: não verificado** —
   a string isolada `Snapshot` (`:16712`) não tem contexto suficiente.
8. **Agendamento de desinstalações: não verificado** — a única automação de
   planeamento observada é a tarefa elevada para contornar o UAC.
9. **Importação de listas: não verificado** — exporta XML do scan e dos
   traces, mas não há string de UI de "importar".
10. **Número de idiomas** — 45 ficheiros `languages/*.xml` contados, mas a
    lista efetivamente disponível na UI não foi conferida item a item.
11. **Vínculo de licença por hardware** — as consultas WMI sugerem
    identificação de máquina para a licença, mas não se verificou como o
    resultado é persistido nem como afeta a versão portátil.
12. **Não foram auditados** as telas de Preferências, Startup Manager e menu
    Ferramentas em UIA (mesma limitação do ponto 5), nem o comportamento real
    do Uninstall Wizard contra um app real (exigiria desinstalar software do
    owner).

---

## 9. Decisões que este documento obriga

1. **O driver de kernel entra na decisão, agora com a contraprova.** Duas
   auditorias o usam (IObit com 4 drivers × 7 arquiteturas; Uninstall Tool com
   um minifiltro) e **três não usam** (BCU, HiBit, Wise). O Uninstall Tool
   mostra que o **minifiltro** é a via *barata* do driver: um `.sys` só,
   instalado pelo setup e removível por script, com o app falando por
   `DeviceIoControl`. **Continua fora do plano do Tranal** — o ganho (rastrear
   evento a evento) não paga o custo (instalação de driver, assinatura,
   AVC, e um segundo modo de falhar) num produto que hoje entrega 87,7% de
   cobertura de silencioso sem nada disso. Registrar a contraprova é o que
   torna a decisão defensável.
2. **Desinstalação em lote é a lacuna mais barata desta matriz.** O Uninstall
   Tool tem Batch Force Removal e Batch Remove Entries; o Tranal tem as
   duas: a desinstalação em lote com prevenção de colisão
   (`Core/Automation/UninstallQueue.cs`, que a tela consome) e a remoção em lote
   das entradas da lista do Windows (`Core/Management/WindowsListEntry.cs`,
   `RemoveMany`, com ação na tela e na linha de comando).
3. **Log de remoção por item, com código de erro, é o que a auditoria do
   Geek/MSI também pede.** O `Core/MsiInstallLog.cs` é *leitor*; falta o
   *escritor*. Um log por operação, com o código de erro e a explicação,
   transformaria a remoção em algo auditável — e é o que o dono vai perguntar
   quando algo der errado.
4. **Exportar a varredura de sobras (não só a lista) é o formato certo.**
   O `Core/Exporters.cs` exporta a lista de programas; o Uninstall Tool
   exporta o **resultado da varredura**. São coisas diferentes, e a segunda é
   a que serve para auditoria.
5. **Reverter a instalação rastreada é a feature que fecha o ciclo.** O Tranal
   tem `Core/Modules/InstallMonitor.cs` e `Core/Modules/TracedPrograms.cs`
   (rastreio) e `Core/BackupManager.cs` (backup), mas **não** tem o
   "desfazer": é a mesma peça que o Wise chama de "Recuperar" no histórico.
6. **A tarefa elevada que contorna o UAC é uma faca de dois gumes.** O
   Uninstall Tool cria uma tarefa agendada para rodar elevado; o Tranal tem o
   caminho de UAC do Windows. **Manter o caminho do Windows** — a tarefa
   elevada é o que faz o Avast e o McAfee marcarem o produto.
7. **A divergência de nomenclatura (`UninstallTool.exe` que não existe) é
   uma lição para a nossa própria documentação.** Qualquer comando nosso que
   apareça no manual tem de ser **testado** contra o binário real, não
   escrito de memória.
