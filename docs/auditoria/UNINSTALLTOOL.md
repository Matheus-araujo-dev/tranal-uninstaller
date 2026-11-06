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

**67 funcionalidades** medidas — 56 **Observado** (medidas nesta máquina ou
extraídas do binário/serviços em execução) e 11 **Documentado** (manual,
release notes e site oficial).

### 2.1 Desinstalação (21)

| # | Funcionalidade | Evidência | Status |
|---|---|---|---|
| 1 | Lista com colunas Nome / Tamanho / Instalado em / **Rastreado** / Status / Origem / Empresa | `ui-strings-english.txt` (`ListCtrl/columnProgramName…columnCompany`) | Observado |
| 2 | 4 categorias na barra secundária: Aplicativos, Sistema, Oculto, Microsoft Store Apps | `SecondaryToolbar/btnApps…btnStoreApps` | Observado |
| 3 | Marcação `(32-bit)` em programas de 32 bits em Windows 64 | `ListCtrl/mark32bitItem` | Observado |
| 4 | Ordenação por qualquer coluna, com largura e posição **memorizadas** | `manual-programs-list.txt` | Documentado |
| 5 | Colunas ocultáveis por clique direito no cabeçalho | `manual-programs-list.txt` | Documentado |
| 6 | Atualizações de software como **sub-itens em árvore** | `DlgPreferences/staticTreeLikeUpdates` | Documentado |
| 7 | Cálculo de tamanho **incluindo pastas de dados** em AppData/Documentos | release notes v3.5.2 | Documentado |
| 8 | Data de instalação por item, com destaque para "recentemente instalado" | `Misc/statusRecentItems` | Observado |
| 9 | **Uninstall** — executa o desinstalador padrão | `MainMenu/mnuActionUninstall` | Observado |
| 10 | **Modify** — tela de modificação de componentes | `MainMenu/mnuActionModify` | Observado |
| 11 | **Force Removal** — apaga arquivos e registro **sem rodar o desinstalador** | `Messages/msgForceRemovalDescription` | Observado |
| 12 | **Batch Force Removal** | `MainMenu/mnuActionDeleteBatch` | Observado |
| 13 | **Remove Entry** — tira da lista sem apagar sobras | `MainMenu/mnuActionRemoveEntry` | Observado |
| 14 | **Batch Remove Entries** | `MainMenu/mnuActionRemoveEntryBatch` | Observado |
| 15 | Lote com **teto de itens** e aviso explícito | `ExplorerBar/itemBatchLimitationWarning` | Observado |
| 16 | Desinstalação de **aplicativos Microsoft Store / UWP**, inclusive em lote | `Get-AppxPackage -name %s \| Remove-AppxPackage` | Observado |
| 17 | Remoção "ultra fast and thorough" de programas **rastreados** | `Messages/msgForceRemovalDescriptionTraced` | Observado |
| 18 | Detecção de **itens relacionados** e **dependentes** | `RelatedItemsDialog/*`, `DependentItemsDialog/*` | Observado |
| 19 | **Cancelamento forçado** de um desinstalador travado, com aviso de riscos | `Messages/msgForceCancelWaiting` | Observado |
| 20 | Remoção de **objetos COM** registrados (`regsvr32.exe /s /u`) | `strings:15801` | Observado |
| 21 | Remoção de **arquivos somente-leitura** | release notes v2.5 | Documentado |

### 2.2 Uninstall Wizard™ — a varredura de sobras (11)

| # | Funcionalidade | Evidência | Status |
|---|---|---|---|
| 22 | Wizard **embutido**: roda o desinstalador e depois varre o sistema | `UninstallWizard/*` | Observado |
| 23 | Varredura separada de **File System** e **Registry**, em árvore | `UninstallWizard/treeItemFileSystem`, `treeItemRegistry` | Observado |
| 24 | Resumo quantitativo: "%d files (%s) and %d registry items are still left" | `UninstallWizard/staticOverallInfo` | Observado |
| 25 | Revisão item a item antes de apagar, com "Browse…" | `UninstallWizard/staticScanStatusFound` | Observado |
| 26 | Remoção de arquivos em uso **agendada para o próximo boot** | `DelayedFilesRemovalDialog/*` | Observado |
| 27 | **Detecção e terminação** de processos do app ainda em execução | `RunningProcessesDialog/*` | Observado |
| 28 | **Log detalhado de remoção por item**, com código de erro | `UninstallWizard/staticHyperlinkRemovalLog` | Observado |
| 29 | **Exportação do resultado da varredura para XML** | `UninstallWizard/staticHyperlinkExport`; release notes v3.4 | Observado |
| 30 | Log de desinstalação **salvo em arquivo** logo após a desinstalação (novo na 3.8.2) | release notes v3.8.2 (#187) | Documentado |
| 31 | Preferência: sempre rodar / nunca rodar / perguntar | `DlgPreferences/comboItemWizardOption1..3` | Observado |
| 32 | O Wizard **sempre roda** para programas rastreados | `DlgPreferences/staticWizardDesctiption` | Observado |

### 2.3 Install Tracker — o rastreamento por driver (16)

| # | Funcionalidade | Evidência | Status |
|---|---|---|---|
| 33 | **Instala e rastreia** um `.exe`/`.msi` disparando o instalador e observando o sistema | `InstallTracker/*` | Observado |
| 34 | Rastreamento **em tempo real, por evento**, de arquivos/pastas criados, renomeados e chaves criadas/alteradas/excluídas | `strings:14946-14953`; `logEventCreateFile`, `logEventRenameFile`, `logEventDeleteKey` | Observado |
| 35 | Rastreamento de **processos criados** (`PsSetCreateProcessNotifyRoutine`) | `strings:14952`, `:14986` | Observado |
| 36 | Log com **eventos enumerados** e barra de progresso na bandeja | `InstallTracker/trayIconMenuProgress` | Observado |
| 37 | **Salvar log** de rastreamento em arquivo | `InstallTracker/btnSaveLog` | Observado |
| 38 | **Reverter** a instalação rastreada | `InstallTracker/btnNoAndRevert`; release notes v3.2 | Observado |
| 39 | **Anexar** dados rastreados a um item existente | `InstallTracker/TitleAppend` | Observado |
| 40 | Detecção de **múltiplas entradas** de instalação | `InstallTrackerMultipleEntries/*` | Observado |
| 41 | **Entrada customizada** quando o instalador não deixou registro | `InstallTrackerCustomEntryDialog/*` | Observado |
| 42 | Ver dados rastreados como **XML** e **desfazer o rastreamento** (Undo Tracing) | `mnuTracedDataAsXML`, `mnuTracedDataUndoTracing` | Observado |
| 43 | Coluna **"Rastreado"** na lista, com ícone próprio | `ListCtrl/columnTraced` | Observado |
| 44 | Menu de contexto do Explorer: **"Install and Trace"** para `.exe`/`.msi` | `ShellExtension/InstallWithInstallTracker*` | Observado |
| 45 | Menu de contexto dos **atalhos**: "Uninstall with Uninstall Tool" | `ShellExtension/uninstallWithUninstallTool*` | Observado |
| 46 | Extensão de shell **empacotada em MSIX** para o Windows 11 | `UTShellExt2.msix`, `Add-AppxPackage` | Observado |
| 47 | Serviço de rastreamento **versionado**, com prompt de atualização | `Messages/msgTrackingServiceUpdate` | Observado |
| 48 | Setup instala o serviço e o **driver de kernel**; `RemoveService.cmd` remove | `Setup/install_service`, `Setup/installing_driver` | Observado |

### 2.4 Startup Manager (10)

| # | Funcionalidade | Evidência | Status |
|---|---|---|---|
| 49 | Listagem com colunas **Status** e **Origem** | `ListCtrl/columnStatus`, `columnSource` | Observado |
| 50 | Habilitar / **desabilitar** item de inicialização | `ExplorerBar/itemDisableRunning` | Observado |
| 51 | **Remover** item de inicialização do registro/Start Menu | `ExplorerBar/itemDeleteStartupItem` | Observado |
| 52 | **Adicionar** item de inicialização (caminho, argumentos, "executar com privilégios máximos") | `DlgAddNewStartupProgram/*` | Observado |
| 53 | **Filtro por categoria** de origem: Registry / Start Menu / Tasks / UWP | release notes v3.8.0 | Documentado |
| 54 | **Busca/filtro** da lista de startup | release notes v3.8.0 | Documentado |
| 55 | Itens de inicialização de **aplicativos UWP / Store** | release notes v3.6.0 (#78) | Documentado |
| 56 | Tarefas do **Task Scheduler** agendadas para logon, com "navegar até a origem" | `ExplorerBar/itemSourceWindowsTask` | Observado |
| 57 | Tarefas criadas pela Microsoft **ocultadas** por segurança | release notes v3.8.1 (#178) | Documentado |
| 58 | Alternância rápida **Uninstaller ⇄ Startup Manager** (Ctrl+Tab) | `manual-keyboard-shortcuts.txt` | Observado |

### 2.5 Interface e integração (9)

| # | Funcionalidade | Evidência | Status |
|---|---|---|---|
| 59 | **Smart Search™** — filtragem incremental enquanto se digita | `ExplorerBar/itemSearch` | Observado |
| 60 | **Atalhos de teclado**: F1, F5, F9, Ctrl+Tab, Ctrl+S, Enter, Shift+Del, Ctrl+G, Ctrl+R, Ctrl+E, Ctrl+W, Esc | `manual-keyboard-shortcuts.txt` | Documentado |
| 61 | **Exportar a lista para HTML** | `MainMenu/mnuFileExport`, `HTMLReport/*` | Observado |
| 62 | Painel lateral com **Desinstalar / Forçar a Remoção**, caminho, versão, autor, data e site | `ExplorerBar/*` | Observado |
| 63 | **Preferências** em 4 seções: Geral, Interface, Lista de Aplicativos, Uninstall Wizard | `DlgPreferences/category*` | Observado |
| 64 | **Modo escuro** (automático / claro / escuro) | `DlgPreferences/colorSchemeDark` | Observado |
| 65 | **45 idiomas** via XML | `app/portable/languages/` | Observado |
| 66 | **Portable Mode** — corre de um pendrive, chave em `license.dat` | `Messages/msgRegSuccessPortableSaved` | Observado |
| 67 | Relatório de **crash** com ficheiro de dump | `UnhandledExceptionHandler/*` | Observado |

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
   Tool tem Batch Force Removal e Batch Remove Entries; o Tranal **não tem
   desinstalação em lote** (grep confirma). O `Core/Modules/TracedPrograms.cs`
   e o `Core/UninstallSupport.cs` já operam sobre listas — é carga de trabalho
   de interface, não de motor.
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
