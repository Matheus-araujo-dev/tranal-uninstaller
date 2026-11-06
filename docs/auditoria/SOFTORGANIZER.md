# Soft Organizer 10.50 — auditoria

> Concorrente do Tranal Uninstaller com **duas tecnologias que o site chama de
> Full Uninstall™**: rastreamento de instalação (snapshot de registro antes/depois
> + rastreio de arquivos em tempo real) e varredura profunda de sobras quando a
> instalação **não** foi rastreada. É o concorrente mais próximo do conceito de
> "registrou o que sobrou" que o owner descreveu — e, ao mesmo tempo, o mais
> **fechado**: quase toda a interface é HTML renderizado por Sciter, a lista de
> aplicações não é exposta por UIA e não há CLI documentada.
>
> Ponto que decide a comparação: o Soft Organizer **não usa driver de kernel**,
> e mesmo assim faz rastreamento de disco em tempo real. Isso reforça, com um
> concorrente a mais, a decisão do Tranal de ficar **fora** do driver.

Data da coleta: 05/11/2026 (22:43–22:55) · Versão auditada: **10.50** (edição
**Free**; a PRO existe, é licenciada e é a que traz "leftovers de programas já
desinstalados", "instalação silenciosa" e "instalação de novas versões")

---

## 1. Identificação

| Campo | Valor | Fonte |
|---|---|---|
| Produto | **Soft Organizer 10.50** (Free) | `evidencia/pe-SoftOrganizer.txt` (`produto 10.50`) |
| Editor / empresa | **ChemTable Software** (`CompanyName=Chemtable Software`); copyright "© Software Solutions 365 LLC" | `evidencia/pe-SoftOrganizer.txt`, `pagina-soft-organizer.htm` (rodapé) |
| Site oficial | **https://www.chemtable.com/soft-organizer.htm** | página do produto lida em 05/11/2026 22:41 |
| URL do download usado | **https://files.chemtable.com/so/soft-organizer-setup.exe** (botão "DOWNLOAD / installation pack" da página oficial) | `pagina-soft-organizer.htm` |
| Tamanho do instalador | **17.938.744 bytes** | `downloads/soft-organizer-setup.exe` |
| SHA-256 do instalador | **`7f8d7661c83437617d892b6825cd2c8ebe9c3711b67acc60f76d69e10e71f688`** | `downloads/soft-organizer-setup.exe` |
| Formato do instalador | **Inno Setup 6.4.3** | `evidencia/install-10.50.log`: "Setup version: Inno Setup version 6.4.3" |
| Formato do programa | **PE32+ x64 nativo, Delphi/Embarcadero (C++ Builder) — NÃO é .NET** (magic `0x020B`, machine `0x8664`, 64 bits, subsistema Windows GUI, timestamp `2026-06-16 15:11:59Z`, 29.574.040 bytes) | `evidencia/pe-SoftOrganizer.txt` |
| Assinante | `CN=Software Solutions 365 LLC, O=Software Solutions 365 LLC, L=Pervomaisky, S=Sverdlovsk Oblast, C=RU` — emissor `GlobalSign GCC R45 CodeSigning CA 2020`, **válido até 13/02/2027 11:14:29** | `evidencia/pe-SoftOrganizer.txt` |
| Interface | **Sciter (HTML/CSS)** — `sciter.dll` (8.890.880 B) renderiza a interface dentro de um form Delphi | `evidencia/pe-SoftOrganizer.txt` (arquivo), `evidencia/uia-softorganizer.txt` (`TSciterHostWindow`) |
| **Driver de kernel** | **nenhum** — nenhum `.sys` instalado (log do instalador), nenhum `.sys` no diretório; a única ocorrência de `.sys` nas strings é `System.SysUtils` | `evidencia/install-10.50.log`, `evidencia/strings-key.txt` (grupo `K_DRIVER_SYS`) |
| Chave ARP (desinstalar) | `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Soft Organizer_is1` + `HKLM\SOFTWARE\Wow6432Node\...\Uninstall\Full Uninstall_is1` | `evidencia/install-log-key.txt` |
| Idioma da instalação | **pt-BR** (atalhos "Desinstalar o Soft Organizer", "Manual do usuário") | `evidencia/install-10.50.log` |
| winget | `ChemTableSoftware.SoftOrganizer` — versões 10.33 / 10.31 / 10.10 | `winget show` |
| URL winget (10.33) | `https://files.chemtable.com/older/SO/SO1033.exe`, SHA-256 `6d564adfd11ded315b6d9e253edcbe5240b5ae2972aa37cda773b141af5b5b0b` (confere com o manifesto) | `downloads/SO1033.exe` |

### O domínio: `codeapps.com` NÃO é o editor

O briefing atribuía o produto a **CodeApps**. Medido: `https://codeapps.com/`
responde com a página de venda do domínio no Namegarage
(`<title>codeapps.com is for sale — Namegarage</title>`, preço de compra
US$ 49.250, "Registered 2008-05-12"). O domínio está **à venda**, é de um
investidor, e não tem nenhuma relação com o produto.

O editor real é a **ChemTable Software** (companhia russa; endereço de
assinatura em Pervomaisky, Sverdlovsk Oblast). Também é do grupo ChemTable o
produto irmão **Reg Organizer**, que compartilha a mesma tecnologia *Full
Uninstall™*.

`softorganizer.com` é **site homônimo morto** — não é o site do produto. O
produto vive em `chemtable.com`.

### O que happenedo com cada caminho testado

| Caminho | Resultado |
|---|---|
| `softorganizer.com` | inacessível (já registrado por auditorias anteriores) |
| `www.softorganizer.com` | **não é o site do produto** — o produto vive em `chemtable.com` |
| `codeapps.com` | **domínio à venda**, sem relação com o produto |
| `winget search softorganizer` | ✅ **funcionou** → `ChemTableSoftware.SoftOrganizer` (10.33) |
| `winget show` | ✅ forneceu a URL do manifesto (`files.chemtable.com/older/SO/SO1033.exe`) |
| `choco search` | ❌ chocolatey **não instalado** nesta máquina |
| `http://archive.org/wayback/available?url=softorganizer.com` | ✅ respondeu (snapshot 2018-03-07 de um site homônimo antigo) |
| `https://www.chemtable.com/soft-organizer.htm` | ✅ **site oficial real**, acessível |

O `winget` foi a via que destravou a auditoria: o manifesto entrega a URL de
download **e** o SHA-256 esperado.

### Instalação medida (e desinstalação)

O instalador 10.50 foi instalado **silenciosamente numa pasta do workspace**
(`/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /DIR=…`) — nada foi gravado em
`Program Files`. Log completo em `evidencia/install-10.50.log` (14.196 bytes);
trechos relevantes em `evidencia/install-log-key.txt`.

Ao final da auditoria o produto foi **desinstalado** (`unins000.exe
/VERYSILENT`, exit code 0): os 21 arquivos instalados sumiram e as duas chaves
ARP acima foram removidas. A máquina do owner ficou como estava. Os binários
foram preservados em `app/so1050-10.50/` para conferência posterior.

### Discrepância medida entre o instalador e o payload (10.33 vs 9.55)

O `innoextract` 1.9 **não consegue** parsear o Inno Setup 6.4.3 dos dois
instaladores (`Stream error while parsing setup headers!`). Do instalador
**10.33** (winget) ele ainda assim extraiu o payload — e o app extraído
reporta `ProductVersion=9.55`, com `WhatsNew.txt`... 9.55 ("What's New in
the Latest Version — Soft Organizer 9.55") e `Documentation.chm` de 19.804 bytes.
Ou seja: **o instalador 10.33 do winget embala o app 9.55** (ou o payload está
corrompido/desalinhado) — medido, não explicado. Ver §8.

---

## 2. Superfície observável

Inventário de **funcionalidades** (não de telas) — **53 itens** na numeração original, curados a partir do **binário**. As linhas novas (54+) continuam a numeração.

**Legenda das evidências** — arquivos medidos em `E:\Projetos\tranal-auditoria-evidencia\`:

| Abreviação | Arquivo |
|---|---|
| `recursos.txt` | `bin-inventario/softorganizer.recursos.txt` — recursos Delphi do PE (32 formulários `T*`) |
| `imports.txt` | `bin-inventario/softorganizer.imports.txt` — imports da PE (904 funções) |
| `inventario.md` | `bin-inventario/softorganizer.inventario.md` — classes RTTI (4499) + rótulos |
| `identificadores.txt` | `bin-inventario/softorganizer.identificadores.txt` — tipos WinRT embarcados |
| `strings.txt` | `bin-strings/softorganizer.strings.txt` — dump bruto de strings |

Coluna **Tranal**: `OK`, `PARCIAL` (diz o que falta) ou `AUSENTE`.

### 2.1 Lista, busca e desinstalação (1–16)

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 1 | Lista de aplicativos instalados | `recursos.txt:103` → `TAPPLICATIONSFRAME/0` | OK — `Core/ProgramScanner.cs`, `Ui/Modules/ModuleViews.cs` |
| 2 | Painel lateral com **INSTALL WITH TRACING**, REMOVE, PROPERTIES | nao confirmado no binario | PARCIAL — `Ui/Modules/ModuleViews.cs` mostra ações, mas não a ação "INSTALL WITH TRACING" |
| 3 | Busca rápida por nome | nao confirmado no binario | OK — `Core/Appearance/ProgramFilters.cs` |
| 4 | Ordenação e agrupamento da lista | nao confirmado no binario | PARCIAL — `Core/ProgramColumns.cs` ordena; o agrupamento é só por `GroupingModel.cs` |
| 5 | Desinstalar vários aplicativos de uma vez | nao confirmado no binario | OK — `Core/Automation/BatchList.cs` |
| 6 | Executa primeiro o **desinstalador nativo**, depois limpa as sobras | nao confirmado no binario | OK — `Core/Removal.cs` + `Core/UninstallSupport.cs` |
| 7 | **Modo forçado** de desinstalação | nao confirmado no binario | OK — `Core/Modules/HunterMode.cs` |
| 8 | "Delete from the List Without Uninstalling" | nao confirmado no binario | OK — `Core/RegistryEditing/RegistryEditor.cs` |
| 9 | **Removal tools from the developers** (link para o desinstalador oficial) | `recursos.txt:107` → `TAPPLICATIONSREMOVALTOOLSFORM/0` | AUSENTE |
| 10 | Filtro **"Large Applications"** | nao confirmado no binario | AUSENTE |
| 11 | Verificação de novas versões com "Update to X" | `recursos.txt:136` → `TUPDATETHISAPPFORM/0` | PARCIAL — `Core/Updates/AutoUpdateClient.cs` atualiza o próprio SO, não os programas listados |
| 12 | Atualização automática do próprio Soft Organizer | `recursos.txt:131` → `TSILENTUPDATESCHECKDATAMODULE/0` | OK — `Core/Updates/AutoUpdateClient.cs` |
| 13 | Avaliação/rating dos programas | nao confirmado no binario | OK — `Core/Appearance/ProgramRating.cs` |
| 14 | Relatório final da desinstalação | nao confirmado no binario | OK — `Core/RemovalLog.cs` |
| 15 | Notificações internas (sino) e dicas | `recursos.txt:127` → `TNOTIFICATIONSDATAMODULE/0` | AUSENTE |
| 16 | Painel de configurações em árvore | `recursos.txt:129` → `TSETTINGSFORM/0` | OK — `Ui/OptionsWindow.xaml.cs` |

### 2.2 Scanning e removal (17–31)

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 17 | Varredura de sobras com pré-visualização e seleção | `recursos.txt:106` → `TAPPLICATIONSLOGVIEWFORM/0` | OK — `Core/UninstallSupport.cs` |
| 18 | "REMOVED APPLICATIONS LEFTOVERS" e botão **REMOVE LEFTOVERS** | `recursos.txt:100` → `TAPPLICATIONSDELETEDFORM/0` | OK — `Core/Tools/TrackCleaner.cs` |
| 19 | Mensagens de estado da remoção de sobras | nao confirmado no binario | OK — `Core/RemovalLog.cs` |
| 20 | Backup automático antes da remoção | nao confirmado no binario | PARCIAL — `Core/BackupManager.cs` faz backup, mas não é automático por padrão |
| 21 | Diretório de backups configurável | nao confirmado no binario | OK — `Core/BackupManager.cs` |
| 22 | **Undo Changes Center** | `recursos.txt:134` → `TUNDOINGCHANGESCENTERFORM/0` | AUSENTE |
| 23 | Reverter uninstallação cronologicamente, renomear ou restaurar | `recursos.txt:134` → `TUNDOINGCHANGESCENTERFORM/0` | AUSENTE |
| 24 | Descarte de mudanças antigas do histórico | nao confirmado no binario | AUSENTE |
| 25 | Aviso "use o Undo Changes Center para cancelar" | nao confirmado no binario | AUSENTE |
| 26 | Trace manager: excluir itens e editar valores antes de aplicar | nao confirmado no binario | PARCIAL — `Core/Tracing/*` grava o log; não há edição de valores do log |
| 27 | "View all traces" | nao confirmado no binario | OK — `Ui/TracingWindow.xaml.cs` |
| 28 | Link "Search" na página de tracing | nao confirmado no binario | OK — `Core/Appearance/ProgramFilters.cs` |
| 29 | Aviso para melhorar a qualidade do rastreio | nao confirmado no binario | AUSENTE |
| 30 | **Ignore List** global de itens a não remover | nao confirmado no binario | PARCIAL — `Core/ExcludeList.cs` é global, mas sem a semântica de "não remover na limpeza" |
| 31 | Limpeza automática de leftovers após a remoção | nao confirmado no binario | OK — `Core/Tools/TrackCleaner.cs` |

### 2.3 Tracing / Uninstall monitor (32–47)

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 32 | **INSTALL WITH TRACING** — instala o programa e registra o que ele mudou | nao confirmado no binario | OK — `Core/Modules/InstallMonitor.cs`, `Core/Modules/TracedPrograms.cs` |
| 33 | Relatório do que mudou, **salvo em log** | `recursos.txt:104` → `TAPPLICATIONSLOGCREATEFORM/0` | OK — `Core/Modules/TraceStore.cs`, `Core/Reporting/TraceLogExporter.cs` |
| 34 | Etiqueta "Traced" na lista | nao confirmado no binario | OK — `Core/ProgramColumns.cs` |
| 35 | **Snapshot do registro** antes/depois da instalação | `imports.txt:8` → `ADVAPI32.dll / RegNotifyChangeKeyValue`, `RegSaveKeyExW` | OK — `Core/Modules/SystemSnapshot.cs` |
| 36 | Rastreamento do **disco em tempo real** | `imports.txt:10` → `KERNEL32.dll / FindFirstChangeNotificationW`, `FindNextChangeNotification` | PARCIAL — `Core/Modules/SystemSnapshot.cs` faz polling; não há notificação do SO |
| 37 | Varredura profunda quando o app não foi rastreado | nao confirmado no binario | OK — `Core/Tracing/ForensicAnalyzer.cs` |
| 38 | Módulo interno de rastreamento de registro | `imports.txt:8` → `ADVAPI32.dll / RegNotifyChangeKeyValue` | OK — `Core/Tracing/*` |
| 39 | Log de mudanças em arquivo por app (`TracedApp.changes`) | nao confirmado no binario | PARCIAL — `Core/Modules/TraceStore.cs` grava log por app, mas sem o formato de snapshot de registro |
| 40 | Rastreamento de **DLLs em pastas do sistema** | nao confirmado no binario | PARCIAL — `Core/UninstallSupport.cs` detecta DLLs em System32 no scan de sobras |
| 41 | **Monitor em processo separado** (app de rastreamento) | nao confirmado no binario | PARCIAL — `Core/Modules/InstallMonitor.cs` roda no processo principal |
| 42 | Rastreamento de **aplicativos adicionais** instalados junto | nao confirmado no binario | AUSENTE |
| 43 | Extensão de shell COM para o menu de contexto | nao confirmado no PE (a DLL da extensão é arquivo do payload) | PARCIAL — `Core/Topmost/ExplorerContextMenu.cs` |
| 44 | Chave de configuração "Windows context menu commands" | nao confirmado no binario | PARCIAL — `Core/Settings/AppSettings.cs` |
| 45 | **Arrastar ícone** para o atalho | nao confirmado no binario | OK — `Core/Modules/HunterMode.cs` |
| 46 | Detecção de **desinstalação externa** (app removido por fora) | `recursos.txt:102` → `TAPPLICATIONSEXTERNALDELETEMODULE/0` | AUSENTE |
| 47 | Remoção de **Universal Windows Apps** | `imports.txt:63` → `API-MS-WIN-CORE-WINRT-L1-1-0.DLL` | OK — `Core/Modules/WindowsApps.cs` |

### 2.4 Integração e organização (48–53)

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 48 | Export da lista para HTML | nao confirmado no binario | OK — `Core/Exporters.cs` |
| 49 | Atalhos instalados, com destino, alvo e working directory | nao confirmado no binario | OK — `Core/Management/ShortcutManager.cs` |
| 50 | "Rarely used apps" | nao confirmado no binario | AUSENTE |
| 51 | Leftovers de programas **já desinstalados** (lista de apps deletadas) | `recursos.txt:100` → `TAPPLICATIONSDELETEDFORM/0` | PARCIAL — `Core/Tools/TrackCleaner.cs` limpa rastros; falta a lista de apps já desinstalados |
| 52 | Instalação silenciosa de programas | nao confirmado no binario | AUSENTE |
| 53 | Instalação de novas versões | nao confirmado no binario | PARCIAL |

### 2.5 Linhas novas: funcionalidades presentes no binário e ainda não no documento

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 54 | **Centro de dicas "Did You Know"** com dicas contextuais (arrastar para desinstalar, desinstalação externa, atualização, multilíngue) | `recursos.txt:112` → `TDIDYOUKNOWCENTERFORM/0`; `:101` → `TAPPLICATIONSDIDYOUKNOWTIPSDATA/0` | AUSENTE |
| 55 | Aviso de **atualização do próprio app** ("Update This App") | `recursos.txt:136` → `TUPDATETHISAPPFORM/0` | PARCIAL — `Core/Updates/AutoUpdateClient.cs` atualiza, mas não há o painel "Update This App" |
| 56 | **Backup e restauração de chaves de registro inteiras** | `imports.txt:8` → `ADVAPI32.dll / RegSaveKeyExW`, `RegRestoreKeyW` | PARCIAL — `Core/FullRegistryBackup.cs` exporta/importa hive `.reg`; o SO salva e restaura a chave selecionada |
| 57 | **Carga e descarregamento de chaves** e substituição de chave | `imports.txt:8` → `ADVAPI32.dll / RegLoadKeyW`, `RegUnLoadKeyW`, `RegReplaceKeyW` | AUSENTE |
| 58 | **Reiniciar o gerenciador** (Restart Manager) para liberar arquivos em uso | `imports.txt:76` → `Rstrtmgr.dll / RmStartSession`, `RmGetList`, `RmRegisterResources`, `RmRestart`, `RmShutdown`, `RmEndSession` | PARCIAL — `Core/ProcessTerminator.cs` termina processos e `Core/DeferredDelete.cs` adia o arquivo; falta o Restart Manager |
| 59 | **Verificação de assinatura digital** de executáveis | `imports.txt:56` → `CRYPT32.dll / CryptQueryObject`, `CryptMsgGetParam` | AUSENTE |
| 60 | **Enumeração de impressoras** e fila de impressão (para desinstalar driver de impressora) | `imports.txt:18` → `WINSPOOL.DRV / EnumPrintersW`, `GetDefaultPrinterW`, `OpenPrinterW` | AUSENTE |
| 61 | **Descoberta de dispositivos Bluetooth LE** | `identificadores.txt:58` → `Windows.Devices.Bluetooth`; `strings.txt:15394` → `TWinRTBluetoothLEAdapter` | AUSENTE |
| 62 | Monitoramento de processos **por sessão** | `imports.txt:50` → `WTSAPI32.dll / WTSEnumerateProcessesW` | OK — `Core/ProcessTerminator.cs` |
| 63 | Informações de **usuário e estação de rede** | `imports.txt:12` → `NETAPI32.dll / NetUserGetInfo`, `NetWkstaGetInfo` | AUSENTE |
| 64 | Consulta da **Lixeira** para reportar itens removidos | `imports.txt:30` → `SHELL32.dll / SHQueryRecycleBinW` | AUSENTE |
| 65 | **Configuração completa de MSI** (instalar, modificar, reparar) | `imports.txt:74` → `msi.dll / MsiConfigureProductW`, `MsiSetExternalUIW`, `MsiSetInternalUI`, `MsiEnumProductsExW`, `MsiQueryProductStateW` | PARCIAL — `Core/MsiInstallLog.cs` e `Core/Discovery/MsiProductCatalog.cs` leem o MSI; falta configurar/reparar |
| 66 | Download de arquivos pela web | `imports.txt:42` → `URLMON.DLL / URLDownloadToFileW` | OK — `Core/Updates/AutoUpdateClient.cs` |
| 67 | **Tarefas de segundo plano** executadas pelo app | `recursos.txt:110` → `TBACKGROUNDTASKSMODULE/0` | AUSENTE |
| 68 | **Itens compartilhados e dependentes** entre programas | `recursos.txt:130` → `TSHAREDANDDEPENDENTDATAMODULE/0` | AUSENTE |
| 69 | **Startup gear** (gerenciador de inicialização próprio) | `recursos.txt:132` → `TSTARTUPGEARDATAMODULE/0` | AUSENTE |
| 70 | Formulário de **notações do log** de rastreamento | `recursos.txt:105` → `TAPPLICATIONSLOGNOTATIONSFORM/0` | AUSENTE |
| 71 | Formulário do **resultado da reversão do log** | `recursos.txt:108` → `TAPPLICATIONSREVERTLOGRESULTFORM/0` | AUSENTE |
| 72 | Painel de **progresso próprio** | `recursos.txt:128` → `TPROGRESSFRAME/0` | OK — `Core/Automation/RemovalProgress.cs` |
| 73 | Troca de **idioma em runtime** (multilíngue) | `recursos.txt:125` → `TMULTILANGUAGESUPPORTFORM/0` | OK — `Core/Localization/LanguageCatalog.cs` |
| 74 | Telas de **licenciamento** (chave, assinatura, expiração da assinatura) | `recursos.txt:118` → `TLICENSINGKEYENTERINGFORM/0`; `:121` → `TLICENSINGSUBSCRIPTIONEXPIREDFORM/0` | AUSENTE — o Tranal não tem licença (sem impacto funcional) |
| 75 | Tema **escuro "Charcoal Dark Slate"** embarcado | `recursos.txt:6` → `VCLSTYLE/CHARCOALDARKSLATE` | OK — `Theming/ThemeManager.cs` |
| 76 | **Impersonação de token** e verificação de privilégios | `imports.txt:8` → `ADVAPI32.dll / ImpersonateLoggedOnUser`, `SetThreadToken`, `PrivilegeCheck`, `RevertToSelf`, `AdjustTokenPrivileges` | PARCIAL — `Core/ProcessTerminator.cs` termina processos com privilégio; falta impersonação de token |
| 77 | **Notificações internas agendadas** (avisos de dica) | `recursos.txt:127` → `TNOTIFICATIONSDATAMODULE/0` | AUSENTE |

### 2.6 Lacunas confirmadas no binário

A lista completa, com evidência e juizo de impacto por linha, está em **[BINARIO-SOFTORGANIZER.md](BINARIO-SOFTORGANIZER.md)**.

- **26 linhas `AUSENTE`** — 9, 10, 15, 22, 23, 24, 25, 29, 42, 46, 50, 52, 54, 57, 59, 60, 61, 63, 64, 67, 68, 69, 70, 71, 74, 77
- **19 linhas `PARCIAL`** — 2, 4, 11, 20, 26, 30, 36, 39, 40, 41, 43, 44, 51, 53, 55, 56, 58, 65, 76
- **77 linhas** na tabela de funcionalidades (53 originais + 24 novas)

---

## 3. Mecanismos notáveis

### 3.1 Imports do PE (`evidencia/pe-SoftOrganizer.txt`)

`SETUPAPI`, `ADVAPI32`, `KERNEL32`, `NETAPI32`, `RPCRT4`, `VERSION`,
`WINSPOOL.DRV`, `COMCTL32`, `COMDLG32`, `DWMAPI`, `GDI32`, `MSIMG32`,
`SHELL32.DLL`, `USER32`, `WINMM`, `IMM32`, `OLE32`, `OLEAUT32`, `Urlmon`,
`Wininet`, `Gdiplus`, `UXTHEME`, `WTSAPI32`, `Windowscodecs`, `Shell32`,
`Crypt32`, `WS2_32`, `BTHPROPS.CPL`, `BLUETOOTHAPIS`,
`API-MS-WIN-CORE-WINRT-L1-1-0.DLL`, `API-MS-WIN-CORE-WINRT-STRING-L1-1-0.DLL`,
`Shlwapi`, `Winhttp`, `Shcore`, **`Msi`**, `Rstrtmgr`.

Leitura relevante:

| Mecanismo | Evidência | Marcado |
|---|---|---|
| **Windows Installer (MSI)** — o SO conversa com o instalador do Windows | import `Msi`; strings `msiexec` (L64898), `MsiExec` (L64899) | Observado |
| **WMI** — enumeração de processos/instalações | strings `ROOT\CIMV2` (L122857), `TWMIProcessCompletionWaiter` (L68823) | Observado |
| **Shell extension COM** | `Shell\ShellContextMenu_4.dll` (261.752 B) registrado por `regsvr32 /s` (64-bit) no instalador; strings `HKLM\SOFTWARE\Classes\CLSID`, `\InprocServer32`, `CLSIDFromString` | Observado (log + strings) |
| **Extensão MSIX** (Explorer modernizado do Win11) | `Shell\ShellContextMenuInstallerSO.msix` (12.797 B) | Observado (arquivo) |
| **Apps Universais (WinRT)** | `WinRTApps.dll` (514.216 B) + import `API-MS-WIN-CORE-WINRT-*` | Observado |
| **Rede** (update, download de removal tools) | `Winhttp`, `Wininet`, `Urlmon`, `Crypt32` | Observado |
| **Bluetooth** (instalação/apresentação de apps) | `BTHPROPS.CPL`, `BLUETOOTHAPIS` | Observado |
| **UI em HTML/CSS (Sciter)** | `sciter.dll` (8.890.880 B na 10.50) | Observado |
| **Registro em tempo real** | `RegistryTracing.TRegSnapShot` / `TRegSnapShotAsync` / `TRegNotify` | Observado (strings) |
| **Driver de kernel** | **nenhum** — nenhum `.sys` instalado (log do instalador), nenhum `.sys` no diretório; a única ocorrência de `.sys` nas strings é `System.SysUtils` | **Não existe (medido)** |
| `MoveFileEx` / `SHFileOperation` | **não aparecem nos imports** — o SO apaga arquivos por rotas próprias; a semântica de "lixeira" **não verificada** | Não verificado |
| Snapshot de disco | não há API de snapshot (VSS); o rastreio de disco é **journaling em user-mode** (`TApplicationsLogCreateForm@ProcessFileChangesTracingPause`, `FinalizeTracing`) | Observado (strings) |

### 3.2 Componentes instalados (10.50, `app/so1050-10.50/`)

| Arquivo | Bytes | Papel |
|---|---|---|
| `SoftOrganizer.exe` | 29.574.040 | o programa (Delphi/C++ Builder x64) |
| `sciter.dll` | 8.890.880 | motor de render HTML/CSS da UI |
| `WinRTApps.dll` | 514.216 | remoção de apps da Store |
| `Shell\ShellContextMenu_4.dll` | 261.752 | extensão COM de contexto do Explorer (64-bit, a registrada) |
| `ShellContextMenu_4.dll` | 247.808 | mesma extensão, cópia na raiz |
| `Shell\ShellContextMenuInstaller.exe` | 232.056 | instalador da extensão de shell |
| `Shell\ShellContextMenuInstallerSO.msix` | 12.797 | pacote MSIX da extensão |
| `CloseApplication.dll` | 1.079.744 | fecha aplicativos antes de desinstalar (importado pelo setup) |
| `Notifications.dll` | 181.416 | notificações do Windows |
| `Launcher.exe` | 609.056 | AutoRun/launcher |
| `Documentation\English\Documentation.chm` | 20.600 | manual oficial (17 capítulos) |
| `Documentation\Russian\Documentation.chm` | 21.987 | manual em russo |
| `Languages\Russian.sib` | 108.155 | catálogo de idioma (russo) |
| `AppUninstIgnore.bkp.xml` | 18.857 | **backup do arquivo de ignore do app** |
| `License.txt` | 3.217 | licença |
| `Readme.txt` | 1.577 | readme do produto |
| `WhatsNew.txt` | 677 | novidades (ainda marcado como 9.55 no payload 10.33) |
| `links.xml` | 253 | links do shell extension |

### 3.3 O que o instalador toca no sistema (medido no log)

- `HKLM` (modo administrativo, 64-bit) — `install-10.50.log`.
- Registra **uma** DLL COM: `Shell\ShellContextMenu_4.dll` via `regsvr32 /s`.
- **Não instala serviço, não instala driver, não cria tarefa agendada** — nada
  disso aparece no log (busca por `service|Task|driver|\.sys` no log: zero
  ocorrências).
- Cria atalhos em Menu Iniciar (público) e na Área de Trabalho pública.
- Cria as chaves ARP `Soft Organizer_is1` e `Full Uninstall_is1`.

### 3.4 Os mecanismos notáveis

São os 11 mecanismos que realmente distinguem o produto. Cada um tem uma
consequência para o Tranal, tratada na §6 e na §9.

| # | Mecanismo | Como funciona (medido/documentado) | Evidência | Estado |
|---|---|---|---|---|
| 1 | **Tracing de instalação com log salvo** | o SO monitora o instalador e **grava em arquivo** o que mudou (arquivos + registro), producing um log reaproveitável por app | `manual-chm-10.50.txt` ("creates a report on what changes were made to the system during the installation and saves it"); string `$n:RO\TracedApp.changes` (L65940) | Documentado + observado (string) |
| 2 | **Undo Changes Center** | janela dedicada (`TUndoingChangesCenterForm`) que concentra tudo o que o programa alterou, para desfazer | strings L69787, L69726, L65867 | Observado (string) + documentado |
| 3 | **Detecção de desinstalação externa** | o SO fica em background, detecta que você desinstalou o app por fora (ex.: "Apps & Features") e **oferece limpar as sobras** | strings L80711 "CDetecting the application external uninstall to clean the leftovers", L80668; manual `uninstallaps_external.html` | Observado (string) + documentado |
| 4 | **Ignore List global retroativa** | entradas ignoradas passam a valer para **todos** os apps rastreados, presentes e futuros — e a lista é uma configuração do produto, não do app | strings `IgnoreList1/2`, "Add to the Ignore List", ":Ignore List. Do not remove these items during the cleanup:" (L90282); manual `uninstallaps_ignorelist.html` (capítulo novo na 10.50) | Observado (string) + documentado |
| 5 | **Varredura profunda para apps NÃO rastreados** | se a instalação não foi rastreada, o SO faz um **deeper scan** do sistema assim que o desinstalador padrão termina | `pagina-full-uninstall.htm` | Documentado |
| 6 | **"Large Applications"** | filtro dedicado que isola os programas grandes, ordenados por tamanho decrescente — um atalho para o caso "este app ocupa GB" | strings L81566 / L123666 "Large Applications"; manual | Observado (string) + documentado |
| 7 | **Painel de updates** ("INSTALLED APPLICATIONS UPDATES") | painel que verifica novas versões dos programas instalados e oferece "Update to X" | `pagina-soft-organizer.htm`; string L81525 "Update to Version %s" | Observado (string) + documentado |
| 8 | **Rating de programa** | avaliação calculada por base de usuários que efetivamente removeram o app — sinal de qualidade de desinstalação real | strings L7179 `TRatingTranslationThread`, L68867 "rating"; `pagina-soft-organizer.htm` ("Built-in program rating") | Observado (string) + documentado |
| 9 | **Removal tools do fabricante** | baixa e executa a ferramenta oficial de remoção do próprio fabricante do app (o antivírus que veio junto, por exemplo) | manual `uninstallaps_removal_tools.html` | Documentado |
| 10 | **Exportação HTML** | a lista de instalados sai como relatório HTML navegável | `pagina-soft-organizer.htm` ("Export the list of installed programs to HTML") | Documentado |
| 11 | **Undo Changes Center — reverter mudança a mudança, com histórico cronológico e renomear** | o centro lista as mudanças **uma a uma em ordem cronológica**, permite **reverter** cada uma isoladamente, **renomear** a entrada pelo menu de contexto e **descartar** automaticamente as antigas | manual `extrafeatures_rezervnie_kopii.html`; strings `TUndoChangeLogThread` (L65867), L81421 ("To cancel the leftovers removal, use the Undo Changes Center.") | Observado (string) + documentado |

Leitura de conjunto: **1 + 2 + 11** formam o ciclo que o owner descreveu —
*desinstalar → excluir sobras → voltar atrás*. O Soft Organizer é o único
concorrente auditado até agora que fecha os três passos **no mesmo produto**.

---

## 4. Interface

### 4.1 Observado em execução (10.50)

Arvore UIA da janela principal (`evidencia/uia-softorganizer.txt`):

```
WINDOW: 'Version 10.50'  class='TMainForm'  type=ControlType.Window  rect=823,403,914,585
  [Pane] class='TApplicationsFrame'     <- quadro da lista de aplicativos
  [Pane] class='TSciterHostWindow'  x3  <- conteúdo renderizado por Sciter
  [Pane] class='TSplitView'
  [Pane] class='TStackPanel'      x3
  [Pane] class='TButton' name='Upgrade to Pro'
  [Pane] class='TButtonedEdit'          <- caixa de busca
  [Pane] class='TStatusBar'
  [Pane] class='TPanel'          x8
  descendants = 21
```

Achado estrutural importante: **a UI é Sciter (HTML) hospedada em Delphi**.
O Automation Tree expõe apenas os **21 *host panes***; **a lista de aplicativos,
as colunas e os menus não são expostos por UIA** (nenhum `List`/`DataGrid`/
`Button` além do "Upgrade to Pro"). Captura de tela também não foi possível
nesta máquina (ACCESS_DENIED já registrado em auditorias anteriores) — daí a
importância das strings e do manual.

### 4.2 Documentado (manual `Documentation.chm`, 17 capítulos)

Comandos da janela principal: `TOOLS` → `Application Uninstaller`; painel
esquerdo `INSTALL WITH TRACING`; botão `UNDO CHANGES` (canto inferior direito)
e submenu `...` do menu principal; botão `Settings...` (ícone "sandwich" com
três linhas horizontais no topo direito); `View all traces` (link na linha do
app); botão `Uninstall Application` dentro da lista; `... Removal Tool` no
lugar dos "..." quando há ferramenta do fabricante; `Large Applications`;
`INSTALLED APPLICATIONS UPDATES`.

Diálogos/classes de janela conhecidos (da coleta anterior da 9.55 na mesma
pasta, `evidencia/uia-so-555-dialogo.txt`): `TUpdateThisAppForm` (título
"%s Update"), `TPUtilWindow` (janela utilitária), `TApplicationsFrame`,
`TSciterHostWindow`, e um diálogo padrão `#32770` com o texto *"You are running
the 32-bit version of Soft Organizer under the 64-bit system. Please reinstall
the program from the official site."* — prova de que a build de 32 bits é
bloqueada em Windows 64 bits.

### 4.3 O que **não** foi verificado na interface

- **Colunas da lista**: **não verificadas**. O UIA não expõe a lista (a UI é
  Sciter) e o manual não detalha as colunas.
- **Atalhos de teclado**: **não verificados**. Nenhum capítulo do manual os
  documenta; a única pista é o manual mencionar a tecla `Delete` no trace
  manager (§2, item 26).
- **Captura de tela**: **impossível** nesta máquina (`SetWindowPos`/`PrintWindow`
  com ACCESS_DENIED). A interface foi reconstruída por UIA + strings + manual.
- **`UninstallMonitor.exe`**: o processo foi **observado rodando** junto com o
  `SoftOrganizer.exe`, mas **o tempo real no disco não foi verificado** —
  não há driver de kernel instalado, e o mecanismo de captura de escrita em
  disco não foi dissecado (ver §8, lacuna 7).

---

## 5. Linha de comando

**Não existe CLI documentada** do Soft Organizer: nenhum capítulo do CHM, nenhuma
seção do site oficial e nenhuma página do fstab descrevem opções de linha de
comando para scan/uninstall/export. O que existe são **literais no binário**.

| Literal | Ocorrência (strings) | Interpretação | Estado |
|---|---|---|---|
| `-UpdateCheck` | L69975 | verificação de update do próprio SO | Observado (string); **parser não verificado em execução** |
| `-UpdatesURI` | L69867 | fonte de atualização | Observado (string); não verificado |
| `-UpdatesSource` | L69973 | fonte de atualização | Observado (string); não verificado |
| `-SilentUpdatesCheck` | L69433 | update silencioso (task de background `TSilentUpdatesCheck@TBackgroundTask`) | Observado (string); não verificado |
| `-ForceForeground` | L70077 | forçar janela em primeiro plano | Observado (string); não verificado |
| `-background` | L73377 | iniciar em background | Observado (string); não verificado |
| `-Minimize` | L66300 | iniciar minimizado | Observado (string); não verificado |
| `-InstallApp` | L70313 | instalar um aplicativo | Observado (string); não verificado |
| `-TestingMode` | L69476 | modo de teste | Observado (string); não verificado |
| `-TestOldVersion` | L69456 | testar versão antiga | Observado (string); não verificado |
| `/Silent`, `/VerySilent` | L69892, L69893 | estilo **Inno Setup** — pertain ao stub de setup/uninstall embutido, **não** ao app | Observado (string); não é switch do produto |
| `/S` e `/SILENT` | L122421 | **enviados a instaladores de terceiros** no modo "Run Installer in Silent Mode (If Supported)" | Documentado (manual) + observado (string) |

Sinais de que existe um parser: classe `TCommandLineParams` (L5056, L5067,
L5077, L5085, L66361) e import de `GetCommandLineW` (L71248).

**Marcado como não verificado:** **nenhum** dos literais foi executado. A
captura de janela é bloqueada nesta máquina e cada execução abriria a UI. Não
posso afirmar quais desses literais são realmente consumidos pelo parser do
produto — apenas que estão presentes no binário. Tudo nesta seção é
**literal no binário**, não **switch comprovado**.

---

## 6. O que o Tranal NÃO faz hoje

Comparação feita por **grep real em `E:\Projetos\tranal-uninstaller\src\`**.
Cada linha "Onde no Tranal" aponta para `arquivo:linha` já visto no grep.
Legenda: **✅ tem** · **🟡 parcial** · **⬜ não tem** · **⟡ não verificado**.

| Recurso do Soft Organizer | Tranal | Onde no Tranal (arquivo) |
|---|---|---|
| Lista de aplicativos instalados | ✅ | `Core/ProgramScanner.cs:153` (`InstallDateValue`), `Core/InstalledProgram.cs:25` |
| Busca rápida na lista | ✅ | `MainWindow.xaml.cs:128` (`MatchesSearch`), acionada em `FilterRow` (`:125`) |
| **Desinstalação em lote / multi-seleção** | 🟡 | o caminho de código existe (`MainWindow.xaml.cs:1215-1222` pergunta "Deseja desinstalar N programas?"), mas a lista principal é `SelectionMode="Single"` (`MainWindow.xaml:309`) — multi-seleção só no painel genérico (`Ui/Shared/ItemListPanel.xaml:39`, `Extended`); a CLI `/mu` aceita **um** programa por vez (`Tranal.Cli/Program.cs:302,334`) |
| Desinstalação pelo desinstalador nativo | ✅ | `Core/QuietUninstall.cs:26`, `Core/UninstallSupport.cs` |
| **Classificação do desinstalador** (família) | ✅ | `Core/UninstallerKind.cs:22` — o SO **não tem** |
| **Desinstalação silenciosa** por família | ✅ | `Core/QuietUninstall.cs:26` — o SO só tem na **PRO** (e em outro sentido: `-InstallApp` + `/S` para o instalador de terceiros) |
| **Modo de desinstalação forçada** | ✅ | `Core/UninstallMode.cs` — o SO documenta "Forced uninstall mode" |
| Remover entrada da lista sem desinstalar | ✅ | `Core/ProgramCustomization.cs` — equivalente do "Delete from the List Without Uninstalling" |
| Varredura de sobras pós-desinstalação | ✅ | `Core/UninstallSupport.cs:389` (`LeftoverScanner`) |
| **Nível de confiança High/Medium/Low** por item | ✅ | `Core/LeftoverScanOptions.cs`, `Core/UninstallSupport.cs:46` (`enum LeftoverReason`) — o SO **não exibe** |
| Filtro `/high` (só confiança alta) | ✅ | `Tranal.Cli/Program.cs` (`/all|/high`) — o SO **não tem** |
| **Backup do registro antes de desinstalar** | ✅ | `Core/FullRegistryBackup.cs:42` (`FullRegistryBackup`) — o SO só faz backup das sobras |
| Backup/restauração de itens, com seleção | ✅ | `Core/BackupManager.cs` (`BackupItem`, `RestorePoint`) |
| **Ponto de restauração do Windows** antes de desinstalar | ✅ | `Tranal.Cli/Program.cs:348` (`TryCreateRestorePoint`) — o SO **não tem** |
| **Undo Changes Center** (reverter mudança a mudança, com histórico cronológico e renomear) | ⬜ | grep `Undo|Undoing|UndoChanges` no `src\` devolve **só** `Core/RecycleBin.cs:24` (`FofAllowUndo`, flag do shell) — **não existe** centro de undo |
| **Monitor de instalação** (rastrear instalação) | 🟡 | `Core/Modules/InstallMonitor.cs:28` (`InstallMonitor`), `Core/Modules/TracedPrograms.cs:12` (`TracedPrograms`) — funciona, mas por **polling** de snapshot de 30 s (`InstallMonitor.cs:31`, `DefaultIntervalSeconds = 30`), **não** em tempo real |
| **Log de instalação salvo** por app | 🟡 | `Core/Modules/TraceStore.cs:68` (`TraceStore`) grava JSON (`Tranal.TraceLog.v1`); o SO grava `TracedApp.changes` por app |
| **Snapshot antes/depois** com diff | ✅ | `Core/Modules/SystemSnapshot.cs:180` (`SystemSnapshot.Diff`) |
| Rastreio de **arquivos em tempo real** durante a instalação | ⬜ | o Tranal só faz snapshot → diff (`SystemSnapshot.cs:180`); nenhum hook/journaling em tempo real |
| **Varredura profunda para apps NÃO rastreados** | 🟡 | `Core/UninstallSupport.cs:389` varra sempre, mas **sem a distinção** "instalação não rastreada → varredura mais profunda" |
| **Detecção de desinstalação externa** (app desinstalado por fora → oferecer limpar sobras) | ⬜ | grep `ExternalUninstall\|DetectExternal\|ExternalRemoval\|UninstallDetected` no `src\`: **0 ocorrências** |
| **Ignore List global retroativa** | 🟡 | `Core/ExcludeList.cs:19` (`ExcludeList`) — é global para o motor (`ContainsPath` recusa qualquer item protegido), mas **não é retroativa**: não reescreve nem limpa os logs de tracing já salvos (`Core/Modules/TraceStore.cs:68`) e não tem a semântica de "ignorar ao limpar sobras de todos os apps" |
| Extensão de shell / menu de contexto do Explorer | ✅ | `Core/Topmost/ExplorerContextMenu.cs` |
| **Remoção de Universal Windows Apps** | ✅ | `Core/Modules/WindowsApps.cs:39` (`WindowsApps`) |
| Autoruns / startups / tarefas agendadas | ✅ | `Core/Tools/AutorunManager.cs:406` (`CreateComObject("Schedule.Service")`, via COM) |
| Limpeza de lixo / otimizador | ✅ | `Core/Tools/JunkCleaner.cs:38` |
| **Atualização de programas instalados** ("Update to X", painel de updates) | ⬜ | `Core/Updates/AutoUpdateClient.cs` é auto-atualização **do próprio Tranal**; nenhum atualizador de terceiros |
| **Instalar programas** (com rastreamento ou silencioso) | ⬜ | a chave `"Cmd.InstallPrograms": "Instalar Programas"` existe em `Lang/pt-BR.json:92` e `Lang/en-US.json:92` **sem nenhum consumidor no código**; o Tranal só abre `appwiz.cpl` (`Core/Tools/WindowsToolsCatalog.cs:234`) |
| **Rating de programa** | ⬜ | grep `rating\|Rating` no `src\`: **0 ocorrências reais** (o único hit é `Is64BitOperatingSystem`) |
| **Removal tools do fabricante** (baixar e rodar a ferramenta oficial do app) | ⬜ | grep `RemovalTool\|removal tool\|fabricante\|manufacturer`: **0 ocorrências** |
| Exportar lista | ✅ | `Core/Exporters.cs`; `MainWindow.xaml.cs:1167-1173` |
| Exportar em **CSV/XML/TXT** | ✅ | `MainWindow.xaml.cs:1169-1172` (`ToCsv`/`ToXml`/`ToText`) — o SO **só exporta HTML** |
| Exportar em **HTML** | ✅ | `Core/Exporters.cs` (`ToHtml`), `MainWindow.xaml.cs:1170` — paridade direta com o SO |
| **Importar** lista de programas | ⬜ | não encontrado no Tranal nem no SO |
| Filtro por **tamanho** ("Large Applications") | ✅ | `MainWindow.xaml.cs:230` (`MatchesSize`: small/medium/large) |
| Filtro por **tipo** (32/64/MSI) e **unidade** | ✅ | `MainWindow.xaml.cs:222` (`MatchesType`), `:245` (`MatchesDrive`) — o SO **não tem** |
| Filtro por **data de instalação** na UI | 🟡 | **não existe como filtro da lista principal**: `FilterRow` (`MainWindow.xaml.cs:125`) só combina busca, tipo, tamanho e unidade; a data aparece no **agrupamento** (`Ui/ProgramRow.cs:71`) e na **CLI** (`Tranal.Cli/ExtraCommands.cs:323`) |
| Ordenação e agrupamento da lista | ✅ | `Ui/ProgramRow.cs:71` (`Group`) |
| **CLI roteirizável** (`/scan`, `/mu`, `/export`) | ✅ | `Tranal.Cli/Program.cs`, `Tranal.Cli/ExtraCommands.cs` — o SO **não tem** |
| Atribuição de sobras pela cadeia do Windows Installer | ✅ | `Core/MsiInstallLog.cs:33` (`MsiInstallLog`), `:63` (`MsiInstallLogReader`) — o SO só chama `msiexec` |
| Modo portátil | ✅ | `Core/PortableMode.cs:18` — o SO **não tem** (só instalador) |
| Agendamento de varreduras | ⬜ | o Tranal só agenda **remoção** para o próximo reinício (`Core/DeferredDelete.cs:27`, `Schedule`) |
| Relatório final de desinstalação (arquivos, chaves, valores) | ⟡ | o Tranal produz log de tracing e relatório de sobras, mas o equivalente **exato** do relatório do SO **não foi verificado** |
| Trace manager: excluir item da lista de sobras e **editar valores de registro** exibidos | ⟡ | `Core/UninstallSupport.cs:389` produz a lista de sobras; a edição de valor de registro exibido **não foi verificada** |
| "View all traces" (rastros do app selecionado) | ⟡ | o Tranal tem `TraceStore` por programa, mas a navegação "todos os rastros de um app" **não foi verificada** |
| Notificações internas com novidades (ícone de sino) | ⟡ | não verificado |
| Painel de configurações em árvore | ✅ | `Ui/OptionsWindow.xaml.cs:59-60,118-119` — o SO também tem (`extrafeatures_nastroiki.html`) |

### 6.1 Onde o Tranal já é melhor

| Ponto | Tranal | Soft Organizer |
|---|---|---|
| **CLI roteirizável** | `Tranal.Cli/Program.cs` (`/scan`, `/mu`, `/export`, `/high`, `/regbackup`) e `Tranal.Cli/ExtraCommands.cs` | **não tem CLI** — só literais no binário, nenhum executado (§5) |
| **Confiança e motivo por item de sobra** | `Core/UninstallSupport.cs:46` (`enum LeftoverReason`) + `Core/LeftoverScanOptions.cs` (High/Medium/Low) | mostra a lista de rastros **sem** nível de evidência |
| **Atribuição pela cadeia do Windows Installer** | `Core/MsiInstallLog.cs:33` — lê o pacote `.msi` em cache e sabe que o item **é** do programa | só chama `msiexec` (import `Msi`, strings `msiexec`/`MsiExec`) |
| **Classificação do desinstalador por família** | `Core/UninstallerKind.cs:22` + `Core/QuietUninstall.cs:26` (silencioso por família) | não tem classificação; o modo silencioso é "instale em modo silencioso, se suportado" |
| **Backup do registro antes de apagar** | `Core/FullRegistryBackup.cs:42` + `Core/BackupManager.cs` + ponto de restauração (`Tranal.Cli/Program.cs:348`) | só faz backup automático das **sobras** (`\Backups32`, `UccBackup`) |
| **Filtros de tipo e de unidade** | `MainWindow.xaml.cs:222` / `:245` — 32/64/MSI e unidade | não tem |
| **Exportação** | CSV, XML, TXT **e** HTML (`MainWindow.xaml.cs:1169-1172`) | **só HTML** |
| **Windows-Apps** | `Core/Modules/WindowsApps.cs:39` | `WinRTApps.dll` + import WinRT — paridade |
| **Tarefas agendadas** | `Core/Tools/AutorunManager.cs:406` — edita `Schedule.Service` **via COM** | não tem; só strings "schedul\|timer\|crontab" de uso interno |
| **Sem driver de kernel** | decisão do projeto: driver **fora** do plano | **também não usa driver** (`K_DRIVER_SYS`: 0 `.sys`) e mesmo assim faz rastreio de disco em tempo real — mais um concorrente de acordo |
| **Privacidade** | sem cadastro, sem telemetria, sem base online | traz rating online, busca de update de terceiros, download de removal tools — recursos de rede que o Tranal não tem e não quer |
| **Segurança do motor** | varredura segura de caminhos, exclusão diferida (`Core/DeferredDelete.cs:27`) e lixeira (`Core/RecycleBin.cs`) | `MoveFileEx`/`SHFileOperation` nem aparecem nos imports — a semântica de lixeira **não foi verificada** |

---

## 7. Fichos de evidência

Tamanhos **reais em bytes**, lidos do disco em 05/11/2026. Caminho base:
`auditoria/softorganizer/`.

### 7.1 Downloads

| Ficheiro | Bytes | SHA-256 / Conteúdo |
|---|---|---|
| `downloads/soft-organizer-setup.exe` | 17.938.744 | instalador oficial 10.50 — `7f8d7661c83437617d892b6825cd2c8ebe9c3711b67acc60f76d69e10e71f688` |
| `downloads/soft-organizer-setup-10.50.exe` | 17.938.744 | cópia do mesmo instalador |
| `downloads/soft-organizer-setup-atual-oficial.exe` | 17.938.744 | cópia do mesmo instalador |
| `downloads/SO1033.exe` | 17.856.288 | instalador 10.33 (winget) — `6d564adfd11ded315b6d9e253edcbe5240b5ae2972aa37cda773b141af5b5b0b`; embala o app **9.55** (§8) |
| `downloads/SO955.exe` | 17.133.744 | instalador 9.55 |
| `downloads/SO944.exe` | 20.397.704 | instalador 9.44 |
| `downloads/SO930.exe` | 20.247.784 | instalador 9.30 |
| `downloads/SO918.exe` | 19.024.824 | instalador 9.18 |

### 7.2 Binários preservados (`app/so1050-10.50/`)

| Ficheiro | Bytes | Conteúdo |
|---|---|---|
| `SoftOrganizer.exe` | 29.574.040 | o programa (Delphi/C++ Builder x64) |
| `sciter.dll` | 8.890.880 | motor de render HTML/CSS da UI |
| `CloseApplication.dll` | 1.079.744 | fecha aplicativos antes de desinstalar |
| `WinRTApps.dll` | 514.216 | remoção de Windows Apps |
| `Launcher.exe` | 609.056 | AutoRun/launcher |
| `Shell\ShellContextMenu_4.dll` | 261.752 | extensão COM registrada (64-bit) |
| `ShellContextMenu_4.dll` | 247.808 | mesma extensão, cópia na raiz |
| `Shell\ShellContextMenuInstaller.exe` | 232.056 | instalador da extensão de shell |
| `Notifications.dll` | 181.416 | notificações do Windows |
| `AppUninstIgnore.bkp.xml` | 18.857 | backup do arquivo de ignore do app |
| `Shell\ShellContextMenuInstallerSO.msix` | 12.797 | pacote MSIX da extensão |
| `Languages\Russian.sib` | 108.155 | catálogo de idioma (russo) |
| `Documentation\Russian\Documentation.chm` | 21.987 | manual em russo |
| `Documentation\English\Documentation.chm` | 20.600 | manual oficial (17 capítulos) |
| `License.txt` | 3.217 | licença |
| `Readme.txt` | 1.577 | readme do produto |
| `WhatsNew.txt` | 677 | novidades (marcado como 9.55 no payload 10.33) |
| `links.xml` | 253 | links do shell extension |

### 7.3 Evidência (`evidencia/`)

| Ficheiro | Bytes | Conteúdo |
|---|---|---|
| `RESUMO.md` | 39.584 | o relatório de coleta (8 seções §1–§8, 53 funcionalidades) |
| `install-10.50.log` | 14.196 | log completo da instalação silenciosa (Inno Setup 6.4.3) |
| `install-log-key.txt` | 4.336 | trechos do log (registro, shell, atalhos) |
| `manual-chm-10.50.txt` | 22.898 | **manual oficial 10.50** (CHM decompilado, 17 capítulos) |
| `manual-chm.txt` | 20.473 | manual oficial 9.55 (CHM do payload extraído) |
| `manual-indice-e-texto.txt` | 20.107 | índice do CHM 9.55 + texto |
| `pe-SoftOrganizer.txt` | 1.242 | imports PE, assinatura, seções, timestamp |
| `strings-key.txt` | 19.935 | strings agrupadas em 16 grupos temáticos (`A_COMMANDLINE` … `P_SHORTCUT`) |
| `strings-SoftOrganizer.txt` | 1.781.951 | strings do binário (mín. 5) |
| `uia-softorganizer.txt` | 1.152 | árvore UIA da janela principal 10.50 em execução (21 host panes) |
| `uia-so-555-dialogo.txt` | 5.292 | UIA do diálogo/monitor da 9.55 (rodada anterior) |
| `pagina-soft-organizer.htm` | 17.453 | página oficial do produto (lista Free/Pro) — rodada anterior |
| `pagina-full-uninstall.htm` | 11.643 | página oficial da tecnologia Full Uninstall™ — rodada anterior |
| `pagina-version-history.htm` | 50.760 | histórico de versões — rodada anterior |
| `pagina-downloads.htm` | 15.688 | página de downloads — rodada anterior |
| `pagina-previous-versions.htm` | 22.450 | versões anteriores — rodada anterior |
| `tranal-src-funcionalidades.txt` | 172.845 | mapa do `src\` do Tranal — rodada anterior |
| `tranal-src-verificacao-fina.txt` | 10.768 | verificação final no `src\` do Tranal — rodada anterior |
| `instalar-10.50.ps1` | 3.944 | script de instalação silenciosa — rodada anterior |
| `mapeia-tranal-src.ps1` | 3.170 | script de mapeamento do `src\` — rodada anterior |
| `doc-chm/` | 19 arquivos / 38.580 | CHM decompilado em HTML (9.55) |
| `doc-chm-1050/` | 20 arquivos / 42.251 | CHM decompilado em HTML (10.50) |
| `manual-chm/` | 19 arquivos / 38.580 | CHM decompilado em HTML (9.55, mesma geração que `doc-chm/`) |
| `_contaminacao-arquivo/` | 1 arquivo / 946.272 | **PNG do desktop do owner — em quarentena; não reproduzido, não citado, não copiado** (ver §8, lacuna 10) |

**Regra do projeto aplicada aqui:** só o **fato derivado** é versionado neste
repositório (este documento). O material bruto — binários, logs, CHMs
decompilados, strings — permanece fora do repositório, na pasta de auditoria do
workspace, e serve de fonte para reexecução.

---

## 8. Lacunas desta auditoria

1. **A atribuição "CodeApps" do briefing está errada.** `codeapps.com` é um
   domínio à venda (Namegarage). O editor é a **ChemTable Software**. Isso muda
   a comparação: a ChemTable também é a dona do **Reg Organizer**, o
   concorrente mais próximo do Tranal no mercado — a auditoria do Reg Organizer
   precisa levar isso em conta. `softorganizer.com` é site homônimo morto.
2. **Discrepância 10.33 vs 9.55 não explicada.** O instalador 10.33 do winget
   tem o recurso de versão "10.33" e o SHA confere com o manifesto, mas o
   payload extraído é o app **9.55**. Não há como decidir se é empacotamento
   errado do fornecedor ou artefato da extração — o `innoextract` 1.9 **não
   suporta Inno Setup 6.4.3** (`Stream error while parsing setup headers!`), e
   a extração só foi possível via instalação silenciosa controlada (em pasta do
   workspace, já revertida). O que medi **com o app real** foi a 10.50.
3. **CLI não verificada em execução.** Só existe "literal no binário"
   (`-UpdateCheck`, `-UpdatesURI`, `-ForceForeground`, `-InstallApp`, …).
   **Nenhum** switch foi executado: a UI não é capturável nesta máquina e cada
   execução abriria a interface. A lista da §5 é de literais, não de switches
   comprovados. **Não existe CLI documentada** do produto.
4. **Colunas da lista e atalhos de teclado não verificados.** A UI é Sciter
   (HTML) hospedada em Delphi; o UIA expõe só os **21 host panes** e **não
   expõe a lista**. O manual também não detalha as colunas nem documenta
   atalhos.
5. **PRO não testada.** "Leftovers de apps já desinstalados", "instalação
   silenciosa" e "instalação de novas versões" são **documentados** (site
   oficial), nunca executados — exigem licença.
6. **Nenhuma captura de tela do Soft Organizer.** `SetWindowPos`/`PrintWindow`
   falham com ACCESS_DENIED nesta máquina (restrição já documentada nas
   auditorias anteriores). A interface foi reconstruída por UIA + strings +
   manual.
7. **`UninstallMonitor.exe` observado rodando, mas tempo real não verificado.**
   O processo existe e roda junto com o app; **não há driver de kernel
   instalado**, então **como** ele captura escrita em disco em tempo real **não
   foi verificado** (provável hooking de API/FSCTL de usuário). O
   `UninstallMonitor.exe` em si **não está preservado** na pasta `app/` da
   auditoria — só a observação de execução ficou registrada.
8. **`MoveFileEx` / `SHFileOperation` ausentes dos imports.** O SO apaga
   arquivos por rotas próprias; a semântica de "lixeira" e de exclusão
   recuperável **não foi verificada**.
9. **Duas rodadas de coleta na mesma pasta.** Os arquivos das 19:52–21:48 são de
   uma rodada anterior (versão 9.55); os das 22:43+ são desta rodada (10.50).
   O RESUMO distingue as duas por marcação de data/versão — mas a mistura fica
   na mesma árvore de diretórios, o que é fonte de erro de leitura.
10. **Contaminação na pasta.** Existe uma captura de tela PNG
    (`so-555-execucao-01.png`) que **não** é do Soft Organizer: é uma foto do
    desktop do owner, com dados pessoais. Foi **movida para
    `evidencia/_contaminacao-arquivo/`** (histórico preservado, fora do
    conjunto de evidências) e **não** foi reproduzida, citada nem copiada neste
    relatório. Recomenda-se a remoção definitiva pelo owner.

---

## 9. Decisões que este documento obriga

1. **Desinstalação em lote precisa existir de verdade na UI.** O Soft Organizer
   promete "Remove multiple programs at once" (§2, item 5) e traz
   `MultipleCheckBoxesPanel` no binário. No Tranal o caminho de código já
   pergunta "Deseja desinstalar N programas?" (`MainWindow.xaml.cs:1215-1222`),
   **mas a lista principal é `SelectionMode="Single"`** (`MainWindow.xaml:309`) —
   então, na prática, nunca se chega ao N. **Decisão:** a multi-seleção da lista
   principal passa a ser requisito, e o alvo da desinstalação passa a ser
   definido pelas caixas de marcação (hoje elas são "apenas indicador visual",
   por decisão registrada no próprio código). A CLI `/mu` também precisa
   aceitar vários programas (`Tranal.Cli/Program.cs:302` aceita um).
2. **O Undo Changes Center é a peça que fecha o ciclo "desinstalar + excluir
   sobras + voltar atrás".** O Tranal tem `Core/Modules/TraceStore.cs:68`
   (rastreia o que foi instalado) e `Core/BackupManager.cs` (backup/restauração
   de itens), mas **não tem o "desfazer"**: o grep de `Undo` no `src\` devolve
   apenas `FofAllowUndo` (`Core/RecycleBin.cs:24`), que é flag do shell, não
   centro de undo. **Decisão:** construir o **Undo Changes Center** — lista
   cronológica das mudanças, reversão **uma a uma**, renomear entrada e
   descarte automático das antigas — sobre o `TraceStore`, que já é a fonte
   canônica do que mudou. **Não** criar um segundo registro de mudanças: a
   regra fica em um lugar só e o resto consome.
3. **Detecção de desinstalação externa: o Tranal não sabe se o programa foi
   desinstalado por fora.** O SO fica em background, detecta a remoção feita
   pelo "Apps & Features" e oferece limpar as sobras
   (`CDetecting the application external uninstall to clean the leftovers`,
   L80711). No Tranal, `ExternalUninstall|DetectExternal|ExternalRemoval|
   UninstallDetected` tem **0 ocorrências** no `src\`. **Decisão:** o Tranal
   passa a reconciliar a lista de instalados com o registro do Windows depois
   de cada ação — e quando um programa some de fora, o Tranal **oferece** a
   varredura de sobras (`Core/UninstallSupport.cs:389`), sem exigir que o
   usuário procure o programa na lista.
4. **Ignore List global e retroativa: o `Core/ExcludeList.cs:19` existe, mas
   não é global nem retroativo no sentido do Soft Organizer.** Hoje ele é uma
   lista de caminhos/chaves que o motor de remoção recusa (`ContainsPath`) — é
   global para o motor, mas **não alcança** os logs de tracing já gravados
   (`Core/Modules/TraceStore.cs:68`) e não tem a semântica de "ignorar na
   limpeza de sobras de todos os apps" (`IgnoreList`, capítulo novo na 10.50).
   **Decisão:** a lista de exclusão passa a ser **retroativa** — adicionar um
   item reescreve a varredura de sobras pendente e os logs de tracing — e a UI
   passa a expor a mesma ação do SO ("Add to the Ignore List").
5. **O Soft Organizer é SUPERIOR ao Tranal no tracing de instalação — e isso
   obriga uma mudança de método, não de recurso.** O SO grava **log em
   arquivo** do que o instalador mudou (registro **e** disco), com rastreio de
   disco em tempo real e rótulo "Traced" na lista. O Tranal faz **polling de
   snapshot de 30 s** (`Core/Modules/InstallMonitor.cs:31`,
   `DefaultIntervalSeconds = 30`), que pode perder escrita rápida entre duas
   capturas, e **não** tem rastreio em tempo real. **Decisão:** o Tranal passa a
   usar o mesmo caminho do SO — **tracing de usuário em tempo real**
   (observação de escrita, sem driver) — e a persistir o log por programa no
   formato que já existe (`TraceStore`), com rótulo de "rastreado" na lista.
   Isto **não** exige driver de kernel: ver decisão 8.
6. **Varredura profunda para apps não rastreados: o Tranal varre sempre, e
   isso é bom — o que falta é a distinção.** O SO faz "deeper scan" quando a
   instalação não foi rastreada (`pagina-full-uninstall.htm`); o Tranal
   varre em todos os casos (`Core/UninstallSupport.cs:389`) mas **sem marcar a
   diferença** entre "sobras de um app rastreado" e "sobras descobertas por
   varredura profunda". **Decisão:** a varredura passa a produzir **dois
   níveis de confiança** — o Tranal já tem `LeftoverReason`
   (`Core/UninstallSupport.cs:46`) e `LeftoverScanOptions` —, e a diferença
   precisa aparecer no relatório e na UI, não só no código.
7. **"Large Applications", painel de updates e rating são lacunas reais do
   Tranal — mas o ranking e o update de terceiros são recursos de rede que o
   Tranal não deve copiar.** O "Large Applications" é de baixo custo e já
   existe como filtro por tamanho (`MainWindow.xaml.cs:230`, `MatchesSize`) —
   **decisão: dar o rótulo e a ordenação por tamanho decrescente**. Já o
   painel "INSTALLED APPLICATIONS UPDATES" (update de programas de terceiros),
   o "Update to X" e o **rating de programa** dependem de serviço online: o
   SO traz `Winhttp`/`Wininet`/`Crypt32` para isso, e o Tranal hoje não tem
   `rating` nem atualizador de terceiros (`AutoUpdateClient` é o do próprio
   Tranal). **Decisão:** os três ficam **fora** do Tranal por enquanto — a
   atualização de terceiros é dependência de rede e de confiança no
   fabricante que o projeto não deve assumir; se entrarem, entram **depois**
   da varredura profunda, nunca antes.
8. **A decisão de driver de kernel continua FORA do plano do Tranal — e o Soft
   Organizer é mais um concorrente de acordo.** O SO **não usa driver**: nenhum
   `.sys` instalado, nenhum `.sys` no diretório, a única ocorrência de `.sys`
   nas strings é `System.SysUtils` (grupo `K_DRIVER_SYS`). E mesmo assim ele
   faz rastreio de disco em tempo real. **Decisão:** manter o driver fora; o
   caminho do SO prova que rastreamento de usuário em tempo real é viável sem
   driver — é esse o caminho que o Tranal deve seguir (decisão 5).
9. **Exportação: o Tranal já é superior e não precisa de ação.** O SO exporta
   **só HTML**; o Tranal exporta CSV, XML, TXT **e** HTML
   (`MainWindow.xaml.cs:1169-1172`, `Core/Exporters.cs`). **Decisão:** manter
   como está — nenhuma mudança, e nenhuma cópia do HTML do SO.
10. **A CLI do Tranal é o ativo que o SO não tem — e ela é a interface
    oficial para tudo o que for adicionado.** O SO **não tem CLI** documentada
    (§5). **Decisão:** todo mecanismo novo que entrar no Tranal (Undo Changes
    Center, detecção de desinstalação externa, Ignore List retroativa,
    desinstalação em lote) precisa de **um** caminho roteirizável na CLI, e
    esse caminho **consome o mesmo núcleo** que a UI — sem segunda
    implementação.
11. **A regra que atravessa todas as decisões acima: a regra fica em um lugar só
    e o resto consome.** O `Core/Modules/TraceStore.cs:68` é o registro
    canônico do que mudou; `Core/BackupManager.cs` é o mecanismo canônico de
    backup e restauração; `Core/ExcludeList.cs:19` é a lista canônica do que
    nunca se toca; `Core/UninstallSupport.cs:389` é o varredor canônico de
    sobras. Nenhuma funcionalidade nova pode criar um **segundo** registro, um
    **segundo** arquivo de exclusão ou uma **segunda** lista de sobras. Toda
    tela, todo comando de CLI e todo relatório **consome** esses quatro
    pontos — inclusive a UI do Undo Changes Center, que nasce da leitura do
    `TraceStore` e da restauração do `BackupManager`, e não de uma estrutura
    própria.

---

*Fonte da verdade: `auditoria/softorganizer/evidencia/RESUMO.md` (39.584 bytes,
8 seções, 53 funcionalidades). Método: `auditoria/METODO.md`.
Regra do documento: **medido vale, inferido não vale** — cada afirmação deste
arquivo aponta para evidência em `evidencia/` ou para um grep real em
`src/`.*
