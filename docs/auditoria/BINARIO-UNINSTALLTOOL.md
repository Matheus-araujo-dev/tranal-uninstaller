# Lacunas - Uninstall Tool 3.8.2

Binario analisado: `C:\Users\Matheus\.minimax\sessions\mvs_1a1a8292487b443c92dc9d3eac74f170\workspace\auditoria\uninstalltool\app\bin\UninstallTool_x64.exe`  sha256: `a47186969cb61616d2c939da02ed43ec5456a08c20aeeff3173f7d6b330d3f45` (6.449.800 bytes, versao 3.8.2.5762, CrystalIDEA Software)

Metodo: extracao **estatica** do PE, sem execucao. (1) Tabela completa de imports por DLL (822 funcoes) (`bin-inventario/uninstalltool.imports.txt`); (2) classes RTTI (126) e 1.372 rotulos de interface (`bin-inventario/uninstalltool.inventario.md`); (3) dump bruto de strings com 18.114 literais, que inclui o XML `LanguageStrings` embarcado com todas as chaves de UI (`bin-strings/uninstalltool.strings.txt`); (4) identificadores de tipo (`bin-inventario/uninstalltool.identificadores.txt`). Cada literal citado foi reconfirmado por `grep` no dump, com o numero da linha.

## Funcionalidades presentes no binario e ausentes no Tranal

| # | Funcionalidade | Evidencia no binario | Categoria | Impacto (alto/medio/baixo) |
|---|---|---|---|---|
| 3 | Marcacao `(32-bit)` na propria linha da lista (PARCIAL) | `strings.txt:14117` → `<mark32bitItem>(32-bit)</mark32bitItem>` | interface | baixo |
| 4 | Ordenacao por qualquer coluna com largura e posicao **memorizadas** (PARCIAL) | nao confirmado no binario | interface | baixo |
| 5 | Colunas **ocutaveis** por clique direito no cabecalho (PARCIAL) | nao confirmado no binario | interface | baixo |
| 6 | Atualizacoes de software como **sub-itens em arvore** (PARCIAL) | `strings.txt:17833` → `Display software updates:`; `:14227` → `<checkDisplayUpdates>Display software updates</checkDisplayUpdates>` | interface | medio |
| 7 | Tamanho **incluindo pastas de dados** do usuario (PARCIAL) | nao confirmado no binario | varredura | medio |
| 8 | **Realce** dos programas recem-instalados (PARCIAL) | `strings.txt:14234` → `<checkHighlighRecent>Highlight recently installed programs</checkHighlighRecent>` | interface | baixo |
| 10 | Acao **Modify** (alterar componentes do instalador) (AUSENTE) | `strings.txt:4953` → `MainMenu/mnuActionModify`; `:14050` → `<mnuActionModify>&amp;Modify...</mnuActionModify>` | integracao | alto |
| 15 | **Teto de itens por lote**, com aviso explicito (AUSENTE) | `strings.txt:14151` → `<itemBatchLimitationWarning>You selected %u items but maximum number of items in a batch operation is limited to %u</itemBatchLimitationWarning>` | interface | baixo |
| 18 | Deteccao de **itens relacionados e dependentes** (AUSENTE) | nao confirmado no binario | integracao | alto |
| 19 | **Cancelamento forcado** de desinstalador travado, com aviso de riscos (AUSENTE) | nao confirmado no binario | remocao | alto |
| 21 | Remocao de **arquivos somente-leitura** (PARCIAL) | `imports.txt:18` → `SHELL32.dll / SHFileOperationW` | remocao | medio |
| 22 | **Wizard embutido** que varre o sistema depois do desinstalador (PARCIAL) | `strings.txt:5225` → `UninstallWizard/staticScanStatusFound`; `:5226` → `UninstallWizard/staticOverallInfo` | automacao | alto |
| 28 | **Log detalhado por item**, com codigo de erro (AUSENTE) | `strings.txt:5245` → `UninstallWizard/staticHyperlinkRemovalLog` | relatorio | alto |
| 29 | **Exportar a varredura** do wizard para XML (PARCIAL) | `strings.txt:5222` → `UninstallWizard/staticHyperlinkExport` | relatorio | medio |
| 30 | Log de desinstalacao **salvo em arquivo** (PARCIAL) | nao confirmado no binario | relatorio | medio |
| 31 | Preferencia **sempre / nunca / perguntar** para o wizard (PARCIAL) | `strings.txt:4726` → `DlgPreferences/comboItemWizardOption1` | interface | baixo |
| 32 | Wizard **sempre roda** para programas rastreados (PARCIAL) | nao confirmado no binario | automacao | medio |
| 33 | **Instalar e rastrear** um `.exe`/`.msi` disparando o instalador (PARCIAL) | `inventario.md:159` → `CisUtMonitor: DriverEntry`; `:161` → `FltRegisterFilter FAILED!` | integracao | alto |
| 34 | Rastreamento **em tempo real, por evento**, do registro e do disco (PARCIAL) | `inventario.md:160` → `CmRegisterCallback FAILED!`; `:162` → `FltStartFiltering FAILED!` | integracao | alto |
| 35 | Rastreamento de **processos criados** (PARCIAL) | `inventario.md:165` → `PsSetCreateProcessNotifyRoutine FAILED!` | integracao | medio |
| 36 | Log com **eventos enumerados** e barra de progresso na bandeja (PARCIAL) | nao confirmado no binario | relatorio | medio |
| 38 | **Reverter** a instalacao rastreada (AUSENTE) | `strings.txt:5157` → `InstallTracker/btnNoAndRevert` | recuperacao | alto |
| 39 | **Anexar** dados rastreados a um item ja existente (AUSENTE) | nao confirmado no binario | organizacao | medio |
| 40 | Deteccao de **multiplas entradas** de instalacao (AUSENTE) | nao confirmado no binario | organizacao | medio |
| 41 | **Entrada customizada** quando o instalador nao deixou registro (AUSENTE) | `strings.txt:14356` → `<checkCreateCustomEntry>I'd like to create a custom item in the list</checkCreateCustomEntry>` | interface | medio |
| 42 | Ver dados rastreados como **XML** e **desfazer o rastreamento** (PARCIAL) | `strings.txt:4957` → `MainMenu/mnuTracedDataAsXML`; `:14056` → `<mnuTracedDataAsXML>View as XML...</mnuTracedDataAsXML>` | relatorio | medio |
| 44 | Menu de contexto do Explorer: **"Install and Trace"** (AUSENTE) | `strings.txt:14213` → `<checkShellExtension>Add 'Install and Trace' command for setup files (exe/msi) to the Windows Explorer context menu</checkShellExtension>` | integracao | alto |
| 45 | Menu de contexto dos **atalhos** ("Uninstall with Uninstall Tool") (AUSENTE) | nao confirmado no binario | integracao | medio |
| 46 | Extensao de shell **empacotada em MSIX** (AUSENTE) | nao confirmado no binario (o `.msix` e arquivo do instalador, nao recurso do PE) | integracao | medio |
| 47 | **Servico de rastreamento versionado**, com prompt de atualizacao (AUSENTE) | nao confirmado no binario | integracao | medio |
| 48 | O setup instala **servico e driver de kernel** (AUSENTE) | `inventario.md:274` → `driver file doesn't seem to exist in the app folder` | integracao | alto |
| 53 | **Filtro por categoria** de origem (Registry / Start Menu / Tasks / UWP) (PARCIAL) | nao confirmado no binario | interface | baixo |
| 54 | **Busca/filtro** da lista de inicializacao (PARCIAL) | nao confirmado no binario | interface | baixo |
| 55 | Itens de inicializacao de **aplicativos UWP / Store** (PARCIAL) | nao confirmado no binario | interface | medio |
| 57 | Tarefas criadas pela Microsoft **ocultadas** por seguranca (AUSENTE) | nao confirmado no binario | interface | baixo |
| 58 | Alternancia rapida **Uninstaller <-> Startup Manager** (PARCIAL) | nao confirmado no binario | interface | baixo |
| 60 | **Atalhos de teclado** completos (F1, F5, F9, Ctrl+Tab, Ctrl+S, Shift+Del, Ctrl+G, Ctrl+R, Ctrl+E, Ctrl+W) (PARCIAL) | nao confirmado no binario | interface | baixo |
| 67 | Relatorio de **crash** com ficheiro de dump (AUSENTE) | nao confirmado no binario | interface | baixo |
| 68 | **Contornar o UAC** com tarefa elevada do Task Scheduler executada a cada abertura (AUSENTE) | `strings.txt:14207` → `<checkSkipUAC>Use Windows Task Scheduler to bypass User Account Control confirmation dialog (when necessary)</checkSkipUAC>`; `:14208` → `<checkSkipUACHelpText>...creates a Windows Task Scheduler elevated task which is executed every time you'd like to run the app.</checkSkipUACHelpText>`; `:15548` → `control.exe schedtasks` | automacao | alto |
| 69 | **Icone no Painel de Controle** (AUSENTE) | `strings.txt:14212` → `<checkControlPanelIcon>Show Uninstall Tool icon in Control Panel</checkControlPanelIcon>`; `:17818` → `Control Panel icon` | integracao | baixo |
| 70 | **Popup de aplicacoes recem-instaladas** (AUSENTE) | `strings.txt:14233` → `<checkDisplayPopup>Display pop-up window with recently installed applications</checkDisplayPopup>`; `:17828` → `Display popup window with recently installed applications` | interface | medio |
| 71 | **Realce** dos programas recem-instalados (PARCIAL) | `strings.txt:14234` → `<checkHighlighRecent>Highlight recently installed programs</checkHighlighRecent>` | interface | baixo |
| 72 | **Programas recentes** na barra secundaria (AUSENTE) | `strings.txt:14235` → `<checkRecentOnSecondaryToolbar>Display recent programs in the secondary toolbar</checkRecentOnSecondaryToolbar>` | interface | baixo |
| 73 | **Exibir atualizacoes de software** na lista (PARCIAL) | `strings.txt:14227` → `<checkDisplayUpdates>Display software updates</checkDisplayUpdates>`; `:17833` → `Display software updates:` | interface | medio |
| 74 | **Verificar atualizacoes ao iniciar**, com canal **beta** (PARCIAL) | `strings.txt:14209` → `<checkCheckForUpdates>Check for updates on program startup</checkCheckForUpdates>`; `:14210` → `<checkCheckForBetaVersions>Check for beta versions as well</checkCheckForBetaVersions>` | integracao | baixo |
| 75 | **Personalizar apariencia, barras e menus**, com legendas nos botoes e barra de status editavel (PARCIAL) | `strings.txt:18030` → `CCustomize application appearance, toolbars, and menus`; `:17811` → `Display toolbar buttons captions`; `:14217` → `<checkToolBarCaptions>`; `inventario.md:237` → `Customize Status Bar4(%s) was pressed...` | interface | medio |
| 76 | **Minimizar na bandeja** durante a instalacao (AUSENTE) | `strings.txt:14295` → `<checkMinimize>Minimize Uninstall Tool to system tray during installation</checkMinimize>` | interface | baixo |
| 77 | **Icone na bandeja do sistema** (AUSENTE) | `inventario.md:271` → `Display Uninstall Tool system tray icon` | interface | baixo |
| 78 | **Iniciar automaticamente minimizado na bandeja** (AUSENTE) | `inventario.md:56` → `Automatically start Uninstall Tool minimized in system tray on Windows startup` | automacao | medio |
| 79 | **Navegar ate a pasta do programa com o Total Commander** (AUSENTE) | `strings.txt:14214` → `<checkTotalCommander>Navigate to program directory using Total Commander (if installed)</checkTotalCommander>` | integracao | baixo |
| 80 | **Criar item personalizado** na lista (AUSENTE) | `strings.txt:14356` → `<checkCreateCustomEntry>I'd like to create a custom item in the list</checkCreateCustomEntry>` | interface | medio |
| 82 | **Lista compacta** de aplicativos (AUSENTE) | `strings.txt:14219` → `<checkSmallIcons>Compact list with apps</checkSmallIcons>` | interface | baixo |
| 83 | **Chave de licenca com lista negra** e chave separada para a versao portatil (AUSENTE) | `strings.txt:4592` → `activation: black listed key`; `:14460` → `<msgRegNotForPortableTitle>This key cannot activate the portable version</msgRegNotForPortableTitle>`; `:5692` → `checking license key {}...` | integracao | baixo |
| 84 | **Limpar o cache** da aplicacao (AUSENTE) | `inventario.md:168` → `Clear cache` | interface | baixo |
| 85 | **Paineis ancoraveis** ("push pin") com pre-visualizacao (PARCIAL) | `inventario.md:169` → `Click push pin to keep open`; `:201` → `Close Preview`; `:203` → `Close Window` | interface | baixo |
| 86 | **Execucao de processo 32 bits** (`CreateX86Process`) (PARCIAL) | `inventario.md:230` → `CreateX86Process OK` | integracao | baixo |
| 90 | Deteccao de **falso positivo** ao fixar a localizacao de um item (AUSENTE) | `inventario.md:115` → `Cannot set location for item (false positive risk) {}` | interface | baixo |
| 91 | **Coleta de identificacao da maquina** para a licenca (AUSENTE) | `inventario.md:227` → `cpu name is {}`; `:242` → `Data folder is {}, ({})` | integracao | baixo |
| 92 | **Validacao de cadeia de certificados** embarcada (CRYPTO/SSL) (AUSENTE) | `imports.txt:48` → `CRYPT32.dll / CertVerifyCertificateChainPolicy`, `CertGetCertificateChain` | integracao | baixo |
| 94 | **Install Tracker com driver de kernel**: minifiltro de sistema de arquivos + callback de registro + callback de processos (AUSENTE) | `inventario.md:159` → `CisUtMonitor: DriverEntry`; `:161` → `FltRegisterFilter FAILED!`; `:160` → `CmRegisterCallback FAILED!`; `:165` → `PsSetCreateProcessNotifyRoutine FAILED!` | integracao | alto |

**Total: 60 lacunas** — 32 `AUSENTE` e 28 `PARCIAL`, das 94 funcionalidades mapeadas na tabela de `UNINSTALLTOOL.md`.

### Observacoes de impacto

- **Alto**: 10, 18, 19, 22, 28, 33, 34, 38, 44, 48, 68, 94 — a maior diferenca de produto esta no **Install Tracker por driver de kernel** (94) e na capacidade de **reverter** a instalacao rastreada (38); o Tranal cobre o rastreio por snapshot, nao por driver.
- **Baixo**: 3, 4, 5, 15, 31, 53, 54, 57, 60, 67, 69, 71, 72, 74, 77, 79, 82, 83, 84, 85, 86, 90, 91, 92 — customizacao de interface, integracoes opcionais e o bloco de licenca (83, 91, 92), que nao tem impacto porque o Tranal e free e sem licenca.
