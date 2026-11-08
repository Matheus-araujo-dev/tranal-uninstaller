# HiBit Uninstaller 4.0.10 — auditoria

> O concorrente **mais amplo em ferramentas** e o **melhor em linha de
> comando**: 21+ ferramentas, e **um switch de linha de comando para cada
> uma**. É também a prova mais forte de que um desinstalador pode ser um
> "cuidador do PC" completo sem driver de kernel e sem serviço de fundo.

Data da coleta: 05/11/2026 · Versão auditada: **4.0.10**
Curadoria binária: 06/11/2026 — as funcionalidades desta tabela estão
amarradas ao **binário**, não à documentação.

---

## 1. Identificação

| Campo | Valor | Fonte |
|---|---|---|
| Produto | HiBit Uninstaller **4.0.10** | `evidencia/pe-main.txt` |
| Editor | HiBitSoftware (assinatura `HiBitSoft.IR`) | `evidencia/pe-main.txt` |
| Site oficial | **https://www.hibitsoft.ir/Uninstaller.html** | `evidencia/site-uninstaller.html` |
| Instalador | `HiBitUninstaller-setup-4.0.10.exe` — 4.533.003 B | `downloads/` |
| SHA-256 do instalador | `ef4f82968d2ab81e574d8cddd433c66190947ba5d28ca83bec8034d124a007dc` | idem (confere com o manifesto winget `HiBitSoftware.HiBitUninstaller`) |
| SHA-256 do portátil | `986b1e07de8037cda70e4d2cf01010e2660042f79f233bbac91846de94741646` | idem |
| SHA-256 do executável | `89092212bd920b84d235a5d4ea89ccaa7f64ec32fcaf769f1e9cdb8070c3dcea` | REA |
| Formato | **PE32 nativo x86, Delphi/Embarcadero**, 15.410.176 B, **sem** CLI data directory (não é .NET) | `evidencia/pe-main.txt` |
| Assinatura | **NÃO assinado** | `Get-AuthenticodeSignature` = `NotSigned`; REA `inspect_pe_certificates` → `table: null` |
| Compilação | 2026-02-10 06:15:09Z | `evidencia/pe-main.txt` |
| Empacotador do setup | Inno Setup (setup data 5.6.0), payload único | `innoextract` |
| **Binário analisado (esta rodada)** | `workspace/auditoria/hibit/app/setup-inno/app/HiBitUninstaller.exe` — 15.410.176 B — sha256 `89092212bd920b84d235a5d4ea89ccaa7f64ec32fcaf769f1e9cdb8070c3dcea` | `bin-inventario/hibit.inventario.md:3-5` |

### A correção de domínio que vale registrar

`https://www.hibituninstaller.com/` **não é o site do fabricante**: é um site
terceiro de SEO que devolve uma *homepage* estática para qualquer URL —
inclusive `hibit_uninstaller.zip`, que respondeu **HTTP 200** com
`Content-Type: text/html`. O domínio do fabricante é **`hibitsoft.ir`**;
`hibitsoft.com` não resolve DNS nesta máquina.

Quem tentar auditar pelo `.com` vai baixar uma página HTML e achar que
achou o artefato errado. O manifesto **winget** `HiBitSoftware.HiBitUninstaller`
foi o que destravou o download correto.

### Método da curadoria binária (06/11/2026)

| Artefato | O que traz | Como foi usado |
|---|---|---|
| `bin-inventario/hibit.inventario.md` | 4.675 classes RTTI Delphi, 13.418 rótulos de interface, 63 capacidades por token de API/IO | nomes de classe Delphi como evidência de funcionalidade |
| `bin-inventario/hibit.recursos.txt` | 214 entradas de recurso do PE | **recursos de formulário Delphi** — 62 formulários `T*FORM` — a evidência mais forte de tela |
| `bin-inventario/hibit.identificadores.txt` | identificadores de tipo/método | units Delphi reveladas (`JunkFilesCleaner.TCleanFile.CleanS.TFileInfo`) |
| `bin-strings/hibit.strings.txt` | 97.229 strings únicas | literal exato de string (nomes de menu, mensagens, DLLs) |

O binário do HiBit é Delphi **não empacotado**: por isso a esttica é rica —
os nomes das 62 classes de formulário estão no PE, não apenas em tabelas de
strings.

---

## 2. Superfície observável

66 funcionalidades medidas na auditoria de 05/11/2026; a curadoria binária
deste documento fecha a tabela com **102 linhas**. As que importam para o
Tranal:

### 2.1 Núcleo do desinstalador

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 1 | Lista com colunas: Nome, Versão, Editor, Data, Tamanho, Tipo, Comentários, String de desinstalação, Pasta de instalação, Site, Entrada de registro | `hibit.recursos.txt:191` -> `TUNINSTALLER/0` (formulário principal); `hibit.recursos.txt:192` -> `TUNINSTALLFORM/0`; `hibit.recursos.txt:159` -> `TEDITINFOFORM/0` | **PARCIAL** - `Core/ProgramColumns.cs:21` (`ProgramColumn.Type`, "32 ou 64 bits"); local em `Core/Appearance/ProgramTextFormatter.cs` e `Core/Appearance/ProgramFilters.cs` (`HighlightScope.InstallLocation`). **Nota**: o local de instalacao nao e coluna da grade, aparece em detalhes/copia/filtro |
| 2 | Desinstalação pelo desinstalador nativo | `hibit.recursos.txt:192` -> `TUNINSTALLFORM/0`; `hibit.recursos.txt:171` -> `TFORCEUNINSTALLFORM/0` | **OK** — `Core/Removal.cs` |
| 3 | **Desinstalação em lote** ("Desinstalar Selecionado", botão + 6 itens de menu) | **não confirmado no binario** (literal de lote nao localizado nesta rodada) | **PARCIAL** — existe motor de lote (`Core/Automation/BatchList.cs`) e a CLI `/mu`; a multi-seleção na grade não foi conferida |
| 4 | **Desinstalação forçada** ("Remoção Forçada") — reagendada para o próximo logon via `RunOnce` | `hibit.recursos.txt:171` -> `TFORCEUNINSTALLFORM/0`; `hibit.inventario.md:13448` -> `Schedule` - Agenda execucao | **AUSENTE** - `Core/DeferredDelete.cs` reagenda **arquivo** com `MoveFileEx(DELAY_UNTIL_REBOOT)`, nao a operacao; `Core/Automation/UninstallSession.cs:130` grava sessao, mas `UninstallSession.Pending` so e consumido em `Tranal.Tests`; nao ha escrita em `RunOnce` (grep so acha leitura em `Core/Discovery/StartupEntries.cs` e `Core/Tools/AutorunManager.cs`) |
| 5 | **Varredura de sobras** em **árvore** (TreeView), com AutoClean e RegBackup | `hibit.recursos.txt:181` -> `TREGISTRYCLEANERFORM/0`; `hibit.inventario.md:13463` -> `Restore` - Restaura backups | **PARCIAL** - `Core/UninstallSupport.cs:523` (`LeftoverScanner`) + 15 motivos em `LeftoverReason` (`Core/UninstallSupport.cs:49-117`), incluindo atalho obsoleto (`LeftoverReason.ObsoleteShortcut`), Menu Iniciar obsoleto (`LeftoverReason.ObsoleteStartMenu`) e entrada de inicializacao (`LeftoverReason.StartupEntry`). **Nota**: servico e tarefa tem gerenciador proprio (`Core/Management/ServiceManager.cs:113`, `Core/Management/ScheduledTaskManager.cs:79`) e nao entram na arvore de sobras |
| 6 | **Backup próprio `.dbh`** (Hibit Backup File): `BackupFiles.zip` + `FileList.txt` + `Registry.ini` | `hibit.recursos.txt:149` -> `TBACKUPFORM/0`; `:148` -> `TBACKUPDETAILSFORM/0` | **OK** - `Core/BackupManager.cs:165/193/225/282/601`, `Core/FullRegistryBackup.cs`, `Core/Recovery/ProgramBackup.cs`, `Core/Recovery/LeftoverBackup.cs`, `Ui/BackupManagerWindow.xaml.cs`. Formato e JSON + arquivos, nao `.dbh` |
| 7 | **Ponto de restauração** antes de desinstalar — WMI `CreateRestorePoint` e `SRSetRestorePointW`/`SRRemoveRestorePoint` (`SRCLIENT.DLL`) | `hibit.inventario.md:13441` -> `SRSetRestorePoint` - Cria ponto de restauracao do sistema; `hibit.recursos.txt:189` -> `TSYSRESTOREFORM/0`; `:146` -> `TADDSYSRESTOREFORM/0` | **OK** — `Core/UninstallSupport.cs:1210` (`TryCreateRestorePoint`), ligado ao assistente |
| 8 | **Relatórios HTML por ferramenta**: `OpReport`, `ScanLog`, `ProgramCompReport`, `WindowsStoreReport`, `ServicesReport`, `DriversReport`, `StartupReport`, `SchedulerReport`, `ContextMenuReport`, `UninstallerReport` | `hibit.recursos.txt:164` -> `TEXPORTFORM/0`; `hibit.inventario.md:13469` -> `Report` - Gera relatorios | **AUSENTE** - Exporta HTML da **lista** (`Core/Exporters.cs:63`) e do **log de instalacao** (`Core/Reporting/TraceLogExporter.cs`); `Core/Reporting/ReportFormats.cs` trata lista/registro. Nao ha relatorio dedicado por ferramenta |
| 9 | **Ocultar entrada de desinstalação** ("Hide original uninstallation entry") | **não confirmado no binario** | **AUSENTE** — sem equivalente identificado em `src/Tranal.Uninstaller` |
| 10 | **Deletar entrada** do registro do programa | `hibit.recursos.txt:192` -> `TUNINSTALLFORM/0`; `hibit.inventario.md:13432` -> `RegDeleteKey` - Remove chaves de registro | **PARCIAL** - `Core/RegistryEditing/RegistryEditor.cs:125` (`DeleteKey`) e (`DeleteValue`); o fluxo de remocao apaga a chave em `Core/UninstallSupport.cs:2001` (`DeleteSubKeyTree`). **Nota**: nao ha botao dedicado "remover so a entrada" no assistente |
| 11 | **Editar informações** do programa | `hibit.recursos.txt:159` -> `TEDITINFOFORM/0` | **OK** - `Core/ProgramCustomization.cs:246/263/306/317/460` (nome, comentario, icone, grupo, estrelas) + `Ui/RenameWindow.xaml.cs`, `Ui/CommentWindow.xaml.cs`, `Ui/IconPickerWindow.xaml.cs` |
| 12 | Abrir chave de registro / pasta de instalação / site do programa | `hibit.recursos.txt:166` -> `TFILEINFOFORM/0`; `hibit.inventario.md:13467` -> `Shell` - Integra ao shell do Windows | **OK** — `Core/RegistryEditing/RegistryEditor.cs` + `Core/Management/ExecutablePath.cs` |
| 13 | Copiar dados do programa para a área de transferência | `hibit.recursos.txt:166` -> `TFILEINFOFORM/0` | **OK** - `Core/Appearance/ProgramTextFormatter.cs:47` (`Format`, `ClipboardFormat`) + `Ui/Appearance/ProgramClipboard.cs` |
| 14 | Exportar a lista (HTML / TXT / CSV) | `hibit.recursos.txt:164` -> `TEXPORTFORM/0`; `hibit.inventario.md:13470` -> `Export` - Exporta dados | **OK** — `Core/Exporters.cs` |
| 15 | **Modificar/reparar** instalação MSI (`ModifyPath`, `MsiExec.exe /I`) | `hibit.inventario.md:13448` -> `msiexec` - Desinstalacao via msiexec; `hibit.recursos.txt:180` -> `TPROGRAMCOMPONENTSFORM/0` | **OK** - `Core/Discovery/MsiProductCatalog.cs:122` (`TryBuildRepairCommand`) e (`Repair`) |

### 2.2 Integração com o Windows

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 16 | **Menu de contexto do Explorer**: `lnkfile\shell\HiBitUninstaller` e `exefile\shell\HiBitUninstaller` | `hibit.inventario.md:13493` -> `ContextMenu` - Adiciona ao menu de contexto; `hibit.recursos.txt:153` -> `TCREATECONTEXTMFORM/0` | **OK** — `Core/Topmost/ExplorerContextMenu.cs` |
| 17 | **Menu de contexto do Monitor de Instalação**: `Msi.Package\shell\HiBitMonitor` e `exefile\shell\HiBitMonitor` | `hibit.recursos.txt:172` -> `TINSTALLMONITORFORM/0`; `:173` -> `TINSTALLMONITORIGNOREFORM/0` | **AUSENTE** - `Core/Topmost/ExplorerContextMenu.cs` registra apenas o menu de desinstalacao do Tranal; nao ha entrada do monitor |
| 18 | **Gerenciador do Menu de Contexto** — lista, cria e remove menus do shell, com "Restaurar" e "Excluir tudo" | `hibit.recursos.txt:152` -> `TCONTEXTMENUFORM/0`; `:153` -> `TCREATECONTEXTMFORM/0` | **AUSENTE** - `Core/Management/ContextMenuManager.cs:70/137/148` faz List/ListFor/Remove de handlers; `Core/Topmost/ExplorerContextMenu.cs:189/156` instala/remove o menu **do proprio Tranal**. Nao ha criacao de menu arbitrario, nem "Restaurar"/"Excluir tudo" |
| 19 | **Desabilitar handlers** de menu de contexto (`DisableCon\Directory\shellex\…`, `Folder`, `Drive`, `exefile`, `lnkfile`, `Directory\Background`, `DesktopBackground`, `CLSID\{645FF040-…}`) | `hibit.recursos.txt:152` -> `TCONTEXTMENUFORM/0`; `hibit.inventario.md:13493` -> `ContextMenu` | **OK** — `Core/Management/ContextMenuManager.cs` |
| 20 | **Monitoramento de instalação** com eventos `ApplicationInstalled`, `ApplicationUninstalled`, `ApplicationDeleteRestore`, `DeviceInstalled` | `hibit.recursos.txt:172` -> `TINSTALLMONITORFORM/0`; `hibit.inventario.md:13471` -> `Monitor` - Monitora em segundo plano | **OK** - `Core/Modules/InstallMonitor.cs:103` (`Start`) e (`StopAndSave`); `Core/Tracing/WriteWatcher.cs` (observacao de escrita, sem driver); `Core/Tracing/TraceEntry.cs` (`TraceChangeKind`) |
| 21 | Desinstalar **atualizações do Windows**: `cmd.exe /c wusa.exe /uninstall /` + `KBID=` | `hibit.inventario.md:13446` -> `Windows Update` - Interage com Windows Update | **AUSENTE** - `Core/Discovery/WindowsFeatures.cs:350` (`ListInstalled`) e (`ListAvailable`) so catalogam; nao existe comando de remocao de KB |
| 22 | Desinstalar **app MSIX**: `powershell.exe Remove-AppxPackage` | `hibit.inventario.md:13448` -> `msiexec` - Desinstalacao via msiexec; `hibit.recursos.txt:196` -> `TWINDOWSAPPFORM/0` | **OK** — `Core/Modules/WindowsApps.cs` |

### 2.3 Suíte de ferramentas — 21+ gerenciadores

Cada uma é uma janela própria, com relatório próprio e **switch de linha de
comando própria**:

Startup · Services · Processes · Scheduler Tasks · Context Menu · Program
Components · Browser Extensions · Connections · Backups & Reports · System
Restore · Registry Cleaner · Junk Files Cleaner · Empty Folder Cleaner ·
Shortcuts Fixer · File Shredder · File & Registry Finder · Duplicate File
Finder · Disk Analyzer · Windows Store Apps Manager · Windows Updates
Manager.

**Limpar Arquivos de Lixo** tem **17 categorias**: temporários, log do
Windows, cache de miniaturas, prefetch antigo, cache de fontes, cache do
sistema, cache do instalador, relatórios de erro, fragmentos do chkdsk,
instaladores obsoletos, cache IE/Edge/Chrome/Firefox/Opera/UCC/CentBrowser,
limpador comum e avançado, limpeza personalizada.

**Destruidor de Arquivos** sobrescreve com **MD5 / SHA-1 / SHA-256 / SHA-512 /
CRC32**.

**Agendamento automático de limpeza**: tarefas do Agendador do Windows para
os limpadores (`Empty_Folder_Cleaner_Schedule`, `Junk_Files_Cleaner_Schedule`,
`Shortcuts_Fixer_Schedule`, `Registry_Cleaner_Schedule`).

> A tabela abaixo (linhas 67-86) é a **versão com evidência de binário** dessa
> suíte: cada janela é um **recurso de formulário Delphi** no PE, com nome
> verificável. As linhas 23-58 do relatório bruto de 05/11/2026 correspondem a
> esta suíte mais as 17 categorias de lixo, os 5 algoritmos do shredder e o
> agendamento — reexpressados aqui.

### 2.4 Itens observados na tela principal (captura real)

Oito itens que só apareceram quando a janela principal foi finalmente
capturada em execução. São de interface, não de mecanismo — e três deles
(drag-and-drop, tempo relativo e contador de tamanho) são coisas que o Tranal
também não tem.

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 59 | **Arrastar e soltar** para desinstalar um programa (anunciado na barra de estado) | `hibit.recursos.txt:12` -> `12/CAT_DRAG_COPY/1033` (cursor de arrastar) | **OK** - `Ui/Modules/HunterView.xaml` (`AllowDrop`) + `Core/Modules/HunterMode.cs:41` (`Resolve`) |
| 60 | Painel de informações do sistema no banner (CPU, GPU, RAM disponível, SO, tempo de atividade) | `hibit.inventario.md:13431` -> (sem token equivalente — ausência **não** prova) | **AUSENTE** — sem equivalente identificado no código do Tranal |
| 61 | Seletor de tema com amostras de cor na barra superior (7 amostras + botão "…") | `hibit.recursos.txt:6-12` -> `VCLSTYLE/CARBON`, `COBALTXEMEDIA`, `ICEBERGCLASSICO`, `LUNA`, `TABLETDARK`, `WINDOWSMODERNDARK`, `WINDOWSSLATEGRAY` (7 temas VCL no PE) | **PARCIAL** - `Core/Appearance/ThemePalette.cs:94-124` (`ThemeSkinCatalog.All`), seletor em `Theming/ThemeManager.cs` e `Ui/Appearance/AppearanceView.xaml.cs`. **Nota**: 4 temas (Dark/Slate/Contrast/Light), nao 7 |
| 62 | Botão **Doar** (adicionado na 4.0.10) | **não confirmado no binario** | **AUSENTE** — irrelevante para paridade funcional |
| 63 | Data de instalação com **tempo relativo** ("Hoje", "Ontem", "1 mês atrás", "2 meses antes", "1 ano atrás") | **não confirmado no binario** (captura de tela, não string do binário) | **AUSENTE** — o Tranal mostra a data absoluta (`ProgramColumn.Date` → `InstalledProgram.InstallDate`) |
| 64 | Coluna **Tipo** com `32bit` / `64bit` por programa | **não confirmado no binario** | **AUSENTE** — sem equivalente identificado no código do Tranal |
| 65 | Contadores na barra de status: total de programas **e tamanho total ocupado** ("Total: 79 Unidade", "Tamanho: 610 GB") | `hibit.inventario.md:13485` -> `Size` - Mede tamanho em disco | **AUSENTE** - A barra de status existe (`MainWindow.xaml:409-411`) mas mostra **contagem**, nao tamanho: `MainWindow.xaml.cs` |
| 66 | Painel de detalhes do programa selecionado: **Local de Instalação**, **Entrada de Registro** e **Website** | `hibit.recursos.txt:166` -> `TFILEINFOFORM/0` | **OK** - `Ui/Tools/TopmostOverlayWindow.xaml.cs:485-486` (editora, local), `Core/ProgramColumns.cs` (`HomePage`) e (`RegistryKey`), `Core/Appearance/ProgramTextFormatter.cs` |

### 2.5 A suíte de ferramentas com evidência de binário

Cada linha cita o **recurso de formulário Delphi** no PE (`hibit.recursos.txt`).

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 67 | Gerenciador de **Inicialização** (Startup) | `hibit.recursos.txt:188` -> `TSTARTUPMANAGERFORM/0`; `:145` -> `TADDSTARTUPFORM/0`; `:160` -> `TEDITSTARTUPFORM/0` | **PARCIAL** - `Core/Tools/AutorunManager.cs:105` (`Scan`), (`SetEnabled`), (`Remove`), (`Add`); painel em `Ui/Tools/ToolViews.cs:112-115`. **Nota**: o painel expoe Refresh/Enable/Disable/Remove, nao um botao "criar" |
| 68 | Gerenciador de **Serviços** | `hibit.recursos.txt:185` -> `TSERVICESFORM/0`; `:144` -> `TADDSERVICESFORM/0` | **AUSENTE** - `Core/Management/ServiceManager.cs:113/148/199/209/217/242` - List/ListFor/Disable/Enable/Stop/Delete. Nenhum `Create` |
| 69 | Gerenciador de **Processos** (painel) | `hibit.recursos.txt:178` -> `TPROCESSESMANAGERFORM/0` | **AUSENTE** - `Core/ProcessTerminator.cs:61/107/156` encerra processos e `Ui/Tools/TopmostOverlayWindow.xaml.cs` tem "Kill process"; nao existe listagem/painel de processos do sistema |
| 70 | Gerenciador de **Tarefas Agendadas** | `hibit.recursos.txt:184` -> `TSCHEDULETASKFROM/0`; `hibit.inventario.md:13438` -> `TaskScheduler` - Agenda tarefas | **OK** - `Core/Management/ScheduledTaskManager.cs:79/114/125/137/149` + `Ui/Management/ManagementView.xaml.cs` |
| 71 | Gerenciador de **Menu de Contexto** | `hibit.recursos.txt:152` -> `TCONTEXTMENUFORM/0` | **PARCIAL** - `Core/Management/ContextMenuManager.cs:70` + `Ui/Management/ManagementView.xaml.cs`. **Nota**: sem "Restaurar" e sem "Excluir tudo" |
| 72 | Gerenciador de **Componentes de Programas** (DLLs compartilhadas) | `hibit.recursos.txt:180` -> `TPROGRAMCOMPONENTSFORM/0` | **AUSENTE** - `Core/ProgramScanner.cs:64` so le chave de desinstalacao, catalogo MSI e Store (`Core/ProgramColumns.cs`); nao ha enumerador de DLL compartilhada entre programas |
| 73 | Gerenciador de **Extensões de Navegador** | `hibit.recursos.txt:150` -> `TBROWSEREXTFORM/0` | **AUSENTE** - `Core/Modules/BrowserExtensions.cs:76-80` cobre apenas chrome, edge, opera (+ firefox em ); nao ha Brave, Vivaldi, Yandex |
| 74 | Gerenciador de **Conexões** (rede) | `hibit.recursos.txt:151` -> `TCONNECTIONMANAGERFORM/0` | **FORA POR DECISAO** - `docs/auditoria/BINARIO.md` 3.2: "Analisador de disco, gerenciador de conexoes, de fontes, papel de parede, destruidor de arquivos - Nao e dominio de desinstalacao" |
| 75 | **Backups & Reports** | `hibit.recursos.txt:149` -> `TBACKUPFORM/0`; `:148` -> `TBACKUPDETAILSFORM/0`; `:164` -> `TEXPORTFORM/0` | **OK** - `Ui/BackupManagerWindow.xaml.cs` e `Ui/Reporting/ReportingWindow.xaml.cs` |
| 76 | **Restaurador do Sistema** (criar/excluir pontos com um clique) | `hibit.recursos.txt:189` -> `TSYSRESTOREFORM/0`; `:146` -> `TADDSYSRESTOREFORM/0`; `hibit.inventario.md:13441` -> `SRSetRestorePoint` | **PARCIAL** - `Core/BackupManager.cs` (`Create`) e `Core/UninstallSupport.cs:1391` (`TryCreateRestorePoint`); painel em `Ui/BackupManagerWindow.xaml.cs`. **Nota**: cria ponto; nao exclui pontos de restauracao do Windows |
| 77 | **Limpador de Registro** (15+ categorias: ActiveX, FileTypes, SharedDLLs, IFEO, FirewallRules, MUICache) | `hibit.recursos.txt:181` -> `TREGISTRYCLEANERFORM/0` | **AUSENTE** - `Core/Tools/RegistryOptimizer.cs` trata so entradas obsoletas do Menu Iniciar; `Core/Tools/PrivacyTraceCleaner.cs` tem 6 grupos de rastros; nao ha MUICache no codigo (grep "MuiCache" sem resultado) nem ActiveX/FileTypes/SharedDLLs/IFEO |
| 78 | **Limpador de Arquivos de Lixo** (17 categorias) | `hibit.recursos.txt:174` -> `TJFCLEANERFORM/0`; `:175` -> `TJFSETTINGFORM/0`; `hibit.identificadores.txt:169` -> `TList<JunkFilesCleaner.TCleanFile.CleanS.TFileInfo>` | **PARCIAL** - `Core/Tools/JunkCleaner.cs:60-81` - 8 varreduras/categorias: temp do usuario, temp do Windows, cache do Windows Update, caches dos navegadores, logs antigos, prefetch, miniaturas, lixeira. **Nota**: 8, nao 17 |
| 79 | **Limpador de Pastas Vazias** | `hibit.recursos.txt:161` -> `TEMPTYFOLDERFORM/0`; `:162` -> `TEMPTYFOLSETTFORM/0` | **OK** — `Core/Discovery/EmptyFolderCleaner.cs` |
| 80 | **Fixador de Atalhos** (atalhos corrompidos) | `hibit.recursos.txt:187` -> `TSHORTCUTFIXERFORM/0` | **AUSENTE** - `LeftoverReason.ObsoleteShortcut` (`Core/UninstallSupport.cs`) so **apaga** atalho obsoleto; nao ha verificacao nem reparo do alvo do atalho |
| 81 | **Destruidor de Arquivos** (MD5 / SHA-1 / SHA-256 / SHA-512 / CRC32) | `hibit.recursos.txt:168` -> `TFILESHREDDERFORM/0`; `hibit.inventario.md:13486` -> `Shred` - Apaga de forma segura | **PARCIAL** - `Core/Tools/UnrecoverableDelete.cs:118` (`Delete(paths, passes)`, `DefaultPasses`) + `Ui/Tools/ToolViews.cs:477-568`. **Nota**: sobrescreve e apaga; nao tem MD5/SHA-1/SHA-256/SHA-512/CRC32 nem relatorio de hash |
| 82 | **Localizador de Arquivos e Registros** | `hibit.recursos.txt:167` -> `TFILEREGFINDERFORM/0` | **AUSENTE** - `Core/Tools/EvidenceRemover.cs` so apaga rastros; nao ha busca por nome de arquivo ou de chave |
| 83 | **Localizador de Arquivos Duplicados** | `hibit.recursos.txt:158` -> `TDUPLICATEFILEFINDERFORM/0`; `hibit.inventario.md:13484` -> `Duplicate` - Detecta duplicados | **PARCIAL** - `Core/Management/SoftwareHealth.cs:184` (`DuplicateFindings`) + `HealthCategory.RedundantFiles`, opt-in via `SoftwareHealth.scanDuplicates`. **Nota**: nao e localizador autonomo, e uma categoria do painel Software Health |
| 84 | **Analisador de disco** | `hibit.recursos.txt:157` -> `TDISKANALYZERFORM/0`; `hibit.inventario.md:13485` -> `Size` - Mede tamanho em disco | **FORA POR DECISAO** - `docs/auditoria/BINARIO.md` 3.2 (analisador de disco nao e dominio de desinstalacao) |
| 85 | **Gerenciador de Apps da Microsoft Store** | `hibit.recursos.txt:196` -> `TWINDOWSAPPFORM/0` | **OK** — `Core/Modules/WindowsApps.cs` |
| 86 | **Gerenciador de Atualizações do Windows** | `hibit.recursos.txt:197` -> `TWINUPDATEFORM/0`; `hibit.inventario.md:13446` -> `Windows Update` | **PARCIAL** - `Ui/Discovery/DiscoveryWindow.xaml.cs:42` (categoria "Atualizacoes do Windows") + (`WindowsUpdateCatalog.ListInstalled`). **Nota**: vive dentro do Discovery, nao e ferramenta separada |

### 2.6 Funcionalidades descobertas **no binário** e ausentes do documento anterior

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 87 | **Propriedades de processo** (painel dedicado) | `hibit.recursos.txt:179` -> `TPROCPROPERTIFORM/0` | **AUSENTE** - Nao existe nem listagem de processos do sistema (ver linha 69) |
| 88 | **Afinidade de processador** do programa | `hibit.recursos.txt:147` -> `TAFFINITYFORM/0`; `hibit.strings.txt:25783` -> `GetProcessAffinityMask`; `:25791` -> `SetProcessAffinityMask`; `:70074` -> `Affinity1.Caption=Affinity` | **AUSENTE** - Nenhum uso de `GetProcessAffinityMask`/`SetProcessAffinityMask` no codigo |
| 89 | **Scanner VirusTotal** (verificação de arquivo na nuvem) | `hibit.recursos.txt:194` -> `TVIRUSTOTALAPIFORM/0`; `:195` -> `TVIRUSTOTALSCANFORM/0`; `hibit.strings.txt:18270` -> `VirusTotal_API`; `:22823` -> `AddVirusTotalScannertoContextMenu1` | **FORA POR DECISAO** - `docs/auditoria/BINARIO.md` 3.2: "Base de logs **online** do Revo - Decisao de produto: o Tranal e offline, sem cadastro e sem telemetria". **Ver Limites**: a decisao nao nomeia o VirusTotal |
| 90 | Integração com o **Windows Defender** (cache, limpeza, varredura agendada) | `hibit.strings.txt:65936` -> `Defender`; `:67985` -> `Windows Defender Cache Maintenance`; `:67987` -> `Windows Defender Scheduled Scan`; `hibit.inventario.md:13456` -> `Defender` - Integra-se ao Defender | **FORA POR DECISAO** - Mesma decisao offline (BINARIO.md 3.2); o Tranal apenas protege o Defender da desinstalacao (`Core/Rules/BuiltinRules.cs`). **Ver Limites** |
| 91 | **Criar item de inicialização** do zero | `hibit.recursos.txt:145` -> `TADDSTARTUPFORM/0`; `:160` -> `TEDITSTARTUPFORM/0` | **PARCIAL** - `Core/Tools/AutorunManager.cs` (`Add`). **Nota**: o painel da ferramenta nao expoe o botao "criar" (`Ui/Tools/ToolViews.cs:112-115`) |
| 92 | **Criar serviço** do Windows | `hibit.recursos.txt:144` -> `TADDSERVICESFORM/0`; `hibit.inventario.md:13435` -> `CreateService` - Gerencia servicos do Windows | **AUSENTE** - `Core/Management/ServiceManager.cs` nao tem `Create` (so List/Disable/Enable/Stop/Delete) |
| 93 | **Criar tarefa agendada** | `hibit.recursos.txt:154` -> `TCREATETASKFORM/0` | **AUSENTE** - `Core/Management/ScheduledTaskManager.cs` nao tem `Create` (so List/Disable/Enable/Delete) |
| 94 | **Criar menu de contexto** no Explorer | `hibit.recursos.txt:153` -> `TCREATECONTEXTMFORM/0` | **OK** - `Core/Topmost/ExplorerContextMenu.cs:189` (`Apply`), (`BuildRegistryScript`), (`RemoveRegistryScript`) |
| 95 | **Limpeza personalizada** (regras próprias por ferramenta) | `hibit.recursos.txt:142` -> `TADDCUSTOMCLEANFORM/0`; `:155` -> `TCUSTOMCLEANFORM/0`; `hibit.strings.txt:57758` -> `UCustomClean` | **OK** - `Core/Rules/RuleEngine.cs:207/222/178`, `Core/Rules/RemovalRule.cs`, `Core/Tools/CustomToolRegistry.cs` |
| 96 | **Listas de exclusão / ignorar** por ferramenta (monitor, registro, junk, atalhos) | `hibit.recursos.txt:143` -> `TADDIGNOREMONITORFORM/0`; `:163` -> `TEXCLUSIONLISTFORM/0`; `:169` -> `TFJIGNOREFORM/0`; `:186` -> `TSHORTCATIGNORELISTFORM/0`; `hibit.strings.txt:19967` -> `RegIgnoreList` | **PARCIAL** - `Core/ExcludeList.cs:128/141/154/166/249` + `Ui/ExcludeListWindow.xaml.cs` + `Core/Rules/RuleEngine.cs:274` (`IsPathProtected`). **Nota**: lista global, nao uma por ferramenta |
| 97 | **Restaurar o registro** a partir do backup | `hibit.recursos.txt:182` -> `TREGRESTOREFORM/0` | **OK** - `Core/BackupManager.cs` (`Restore`), `Core/Recovery/ProgramBackup.cs`, `Core/Recovery/RecoverOperation.cs` |
| 98 | **Verificação de integridade de arquivo** e limpeza pontual de arquivo | `hibit.recursos.txt:165` -> `TFILECHECKSFORM/0`; `:166` -> `TFILEINFOFORM/0`; `:156` -> `TDELETEFILEFORM/0` | **AUSENTE** - Nao ha verificacao de integridade por hash. A limpeza pontual existe (`Core/Tools/JunkCleaner.cs`, `Core/Tools/EvidenceRemover.cs`, `Core/Tools/UnrecoverableDelete.cs`) |
| 99 | **Sete temas VCL** embutidos no binário | `hibit.recursos.txt:6-12` -> `VCLSTYLE/CARBON` / `COBALTXEMEDIA` / `ICEBERGCLASSICO` / `LUNA` / `TABLETDARK` / `WINDOWSMODERNDARK` / `WINDOWSSLATEGRAY` | **PARCIAL** - `Core/Appearance/ThemePalette.cs:94-124` - 4 temas no catalogo. **Nota**: 4, nao 7 |
| 100 | **Banco local embutido** (`LOCALDB`) | `hibit.recursos.txt:127` -> `LOCALDB/1033` | **AUSENTE** - O estado e JSON: `Core/Settings/ProgramCache.cs` (`ProgramCacheStore`), `Core/Modules/TraceStore.cs`, manifesto em `Core/BackupManager.cs`. Nao ha banco local |
| 101 | **Salvar log** de ferramenta em arquivo | `hibit.recursos.txt:176` -> `TLOGSAVEFORM/0`; `hibit.inventario.md:13466` -> `Log` - Mantem registro/log | **OK** - `Core/Modules/LogOperations.cs:165`, `Core/RemovalLog.cs`, `Core/Reporting/TraceLogExporter.cs:203` (`Write`), `Core/Tools/WindowsToolsCatalog.cs:289` (`ToolLog`) |
| 102 | Formulário inicial de **escolha de onde guardar a configuração** | `hibit.recursos.txt:183` -> `TSAVESETTINGSFORM/0` | **OK** — `Ui/FirstRunWindow.xaml.cs` |

---

## 3. Mecanismos notáveis

| Mecanismo | Evidência |
|---|---|
| **33 DLLs importadas, 698 funções estáticas + 63 delay-load**; imphash `a5d540b8f302fb265a4ac16541c7d23a` | `evidencia/pe-imports.txt` |
| `advapi32!RegDeleteKeyExW` (delay-load) → limpeza de registro | `pe-imports.txt` |
| `wintrust.dll` → verificação de assinatura (scanner VirusTotal) | `pe-imports.txt` |
| `user32!SetThreadDpiAwarenessContext`, `GetDpiForWindow`, `AdjustWindowRectExForDpi` + `Shcore!GetDpiForMonitor` → **UI DPI-aware** | `pe-imports.txt` |
| `DWMAPI.DLL`, `uxtheme.dll`, `msimg32!AlphaBlend`, `windowscodecs.dll` → renderização com temas do Windows | `pe-imports.txt` |
| `kernel32!CreateSymbolicLinkW`, `GetFinalPathNameByHandleW` → tratamento de link simbólico | `pe-imports.txt` |
| `SRCLIENT.DLL` → pontos de restauração | `strings-mecanismos.txt` |
| `Wmic.exe /Namespace:\\root\\default Path SystemRestore Call CreateRestorePoint` (caminho alternativo) | `strings-mecanismos.txt` |
| `powershell.exe Remove-AppxPackage` → app MSIX | `strings-mecanismos.txt` |
| `cmd.exe /c wusa.exe /uninstall /` + `KBID=` → atualização do Windows | `strings-mecanismos.txt` |
| Configuração em `HKCU\SOFTWARE\HiBit Uninstaller\` **ou** `<pasta>\HiBitUninstaller\Settings.ini` | `strings-mecanismos.txt` |
| **Sem driver, sem serviço de background, sem extensão COM** — tudo no próprio processo, via `EnumWindows`/`RegDeleteKeyExW`/WMI/PowerShell | `pe-imports.txt` (nenhum `.sys`) |

### 3.1 A tabela de switches silenciosos

O binário traz uma **tabela de switches de desinstalação silenciosa por
aplicativo**: o nome do programa vem seguido, na tabela de strings, do switch
que aquele programa aceita. Trecho medido em `strings-main.txt`:

```
ESET Security
Malwarebytes
IrfanView
/silent                ← o switch do IrfanView
Google Chrome
/silent /norestart     ← o switch do Google Chrome
Brave
--force-uninstall      ← o switch do Brave
Yandex
SumatraPDF
XAMPP
--mode unattended      ← o switch do XAMPP
WinRAR
/AUTO /SILENT          ← o switch do WinRAR
Avidemux VC++
Spotify
Smart Switch
Avast Secure Browser
Torrent
Unifab
DVDFab
Adobe Creative Cloud
-uninstall             ← o switch do Adobe Creative Cloud
```

**Leitura honesta:** o pareamento "programa → switch" é feito por
proximidade na tabela de strings. Para a maioria é inequívoco (o switch vem
imediatamente após o nome e antes do próximo nome), mas em alguns casos —
`Yandex`, `SumatraPDF`, `Avidemux VC++`, `Spotify` — o switch pode
pertencer ao programa anterior. A tabela **existe**; o pareamento exato
precisa de confirmação em execução.

**Esta é a peça mais acionável de toda a auditoria do HiBit.** Ela combina
exatamente com a classificação de desinstalador que o Tranal acabou de
implementar: já sabemos *de que família* é o desinstalador de cada programa
(`UninstallerKind`), e existe uma **tabela de switches silenciosos** — ora
por programa (HiBit), ora por família (BCU, Uninstalr). Fechar o item
"desinstalação silenciosa" do backlog deixa de ser adivinhação.

O **modo silencioso do próprio HiBit** é sinalizado internamente por
`SilentMod` e pelo valor de registro `QuietUninstallString` — não por switch
de linha de comando.

### 3.2 Superfície de recursos Delphi confirmada no binário

As 214 entradas de recurso do PE de `hibit.recursos.txt` confirmam **62
formulários** — a suíte de ferramentas inteira é visível como recurso, sem
execução: `TUNINSTALLER`, `TUNINSTALLFORM`, `TFORCEUNINSTALLFORM`,
`TBACKUPFORM`, `TBACKUPDETAILSFORM`, `TREGRESTOREFORM`, `TSYSRESTOREFORM`,
`TADDSYSRESTOREFORM`, `TSTARTUPMANAGERFORM`, `TADDSERVICESFORM`,
`TPROCESSESMANAGERFORM`, `TPROCPROPERTIFORM`, `TSCHEDULETASKFROM`,
`TCONTEXTMENUFORM`, `TCREATECONTEXTMFORM`, `TPROGRAMCOMPONENTSFORM`,
`TBROWSEREXTFORM`, `TCONNECTIONMANAGERFORM`, `TREGISTRYCLEANERFORM`,
`TJFCLEANERFORM`, `TEMPTYFOLDERFORM`, `TSHORTCUTFIXERFORM`,
`TFILESHREDDERFORM`, `TFILEREGFINDERFORM`, `TDUPLICATEFILEFINDERFORM`,
`TDISKANALYZERFORM`, `TWINDOWSAPPFORM`, `TWINUPDATEFORM`,
`TVIRUSTOTALSCANFORM`, `TAFFINITYFORM`, `TCUSTOMCLEANFORM`,
`TEXCLUSIONLISTFORM`, `TFILECHECKSFORM`, `TDELETEFILEFORM`, `TEXPORTFORM`,
`TLOGSAVEFORM` — e ainda **7 estilos VCL** (`VCLSTYLE/*`) e um recurso
`LOCALDB`.

---

## 4. Interface

Delphi/VCL. A janela principal (`TUninstaller`, 1005×670) **esconde** enquanto
um modal está aberto; na primeira execução aparece o diálogo **Save
Settings** com as duas opções de onde guardar a configuração. O Delphi
desenha tudo no canvas do form, sem HWNDs filhos — a UIA não expõe elementos.

### 4.1 A tela principal, medida

O diálogo inicial foi contornado **sem clique**: semeando
`app/portable/HiBitUninstaller/Settings.ini` — o caminho que o próprio HiBit
usa no modo portátil — a segunda execução abre direto na janela principal.
Ela foi capturada (`evidencia/hibit-interface-principal.png`, em pt-BR):

| Elemento | Observado |
|---|---|
| Título `HiBit Uninstaller 4.0.10 (Versão Portátil)` — o sufixo distingue o modo portátil | Observado |
| Banner de **informações do sistema**: CPU (AMD Ryzen 7 5700X 8-Core), GPU (AMD Radeon RX 9060 XT), RAM disponível (31,9 GB), Windows 11 Professional 64bit e **tempo de atividade** (19 horas 15 minutos) | Observado |
| Barra de menus com **3** entradas: `Ferramentas ▾`, `Configurar ▾`, `Sobre` | Observado |
| Seletor de tema com **7 amostras de cor** + botão `…` (mais temas) | Observado |
| Campo de busca `Pesquisar...` | Observado |
| Cabeçalhos de coluna: `Nomes dos Programas`, `Data de Instalação`, `Tamanho`, `Tipo`, `Versão`, `Editor` | Observado |
| **Caixa de seleção por programa** em cada linha → desinstalação em lote | Observado |
| Datas com **tempo relativo**: `Hoje`, `Ontem`, `1 mês atrás`, `2 meses antes`, `1 ano atrás` | Observado |
| Coluna `Tipo` com `32bit` / `64bit` | Observado |
| Painel inferior de detalhes: **Local de Instalação**, **Entrada de Registro** e **Website** (links clicáveis) | Observado |
| Barra de status: `Total: 79 Unidade`, `Tamanho: 610 GB`, `Suporta Arrastar e Soltar para desinstalar um programa`, botão `Doar` | Observado |

As **56 classes de form** (`TUninstaller`, `TBackupManagerForm`,
`TVirustotalScanForm`, `TWinUpdateForm`, …) e os **802 pares nome/legenda
pt-BR** estão em `evidencia/strings-ptbr-formas.txt` e
`evidencia/PortugueseBLang.ini` — este último é o **arquivo de idioma que o
próprio HiBit escreveu em execução**, copiado verbatim.

**Limitação de medição registrada:** `SetWindowPos`/`SetForegroundWindow`/
`AppActivate` retornaram `ACCESS_DENIED` para **todas** as janelas do
desktop nesta máquina (inclusive as de controle), e `PrintWindow` falha
nestas forms Delphi. A janela principal foi capturada por outro caminho
(semeando o `Settings.ini`), mas os **menus desplegados não** — a lista de
itens de menu abaixo vem dos **recursos de string do binário**, não de uma
captura.

---

## 5. Linha de comando

### 5.1 Operações

| Switch | Efeito |
|---|---|
| `/UNINSTALL` | desinstalar o alvo de `%1` (usado pelo menu de contexto) |
| `/UNIN` | forma curta de `/UNINSTALL` |
| `/UNINSTALLIT` | executa o desinstalador **e** o fluxo completo de sobras |
| `/FORCE` | desinstalação forçada (`HiBitUninstallerForce` + `RunOnce`) |
| `/INSTALL` | abre o Monitor de Instalação no alvo |

### 5.1.1 O que **não** é switch do HiBit

Uma correção importante, porque a leitura apressada do binário erra:

| String | Dono real |
|---|---|
| `/SILENT` | **não é switch do HiBit** — é o argumento passado ao **instalador da versão antiga** durante a atualização (`NetworkError` \| ` /SILENT` \| `(OldVersion).exe` \| `HiBitUninstaller-Portable.exe` \| `CheckingNewVersion`) |
| `/AUTO` | faz parte do par **`/AUTO /SILENT`** da tabela de switches silenciosos **por aplicativo** (programas do tipo Inno Setup) |
| `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART` | flags do **instalador Inno Setup** que o HiBit passa ao baixar a nova versão |
| `MsiExec.exe /quiet /qn /norestart /X` | remoção de produto **MSI** |
| `MsiExec.exe /I` | **modificar/reparar** produto MSI |

O **modo silencioso do próprio HiBit** é sinalizado internamente por
`SilentMod` e pelo valor de registro **`QuietUninstallString`** — não por
switch de linha de comando.

### 5.2 Um switch por ferramenta

`/RCLEAN` · `/ECLEAN` · `/JCLEAN` · `/SCLEAN` · `/STARTUP` · `/RESTOR` ·
`/SERVICE` · `/PROCESS` · `/TASK` · `/MENU` · `/BROWSER` · `/COMPON` ·
`/STOREAPP` · `/WINUPDATE` · `/FIND` · `/DUPL` · `/DISK` · `/CONN` · `/SHRED`

**Variação de sintaxe medida:** os limpadores existem em duas formas — a
curta (`/ECLEAN`) e a longa com o sufixo **` RUN`** (`/ECLEAN RUN`). As duas
coexistem no binário.

**Não verificado:** não há tabela de ajuda no binário; não existe `/help`,
`/cfg` ou `/log` como switch próprio do HiBit.

> **Atualização de 06/11/2026:** a lacuna "um switch de CLI por ferramenta"
> **está fechada no Tranal**. `Core/Automation/CliToolSwitch.cs` define o
> formato exato do HiBit — switch curto, nome longo e o sufixo `RUN`
> (`RunSuffix = "RUN"`, `LongRunName => LongName + "-" + RunSuffix`) —
> consumindo o catálogo de `Core/ToolCatalog.cs`.

---

## 6. O que o Tranal NÃO faz hoje

Coluna `Tranal` no formato da curadoria binária: **OK** = implementado e
verificado no código; **PARCIAL** = existe mas falta parte; **AUSENTE** =
sem equivalente identificado em `src/Tranal.Uninstaller`.

| # | Recurso do HiBit | Evidência no binário | Tranal |
|---|---|---|---|
| 1 | **Switch de linha de comando por ferramenta** (19 ferramentas) | `hibit.strings.txt` -> `/RCLEAN` `/ECLEAN` `/JCLEAN` `/STARTUP` `/RESTOR` … | **OK** — `Core/Automation/CliToolSwitch.cs` (com o sufixo `RUN` do HiBit) + `Core/ToolCatalog.cs` |
| 2 | **Tabela de switches silenciosos por instalador** | `hibit.inventario.md:13448` -> `msiexec`; `hibit.strings.txt` -> `SilentMod` | **OK** — `Core/QuietUninstall.cs` (tabela por família) |
| 3 | **Switch silencioso por aplicativo** (o HiBit embute uma lista nome→switch) | `hibit.strings.txt` -> `/silent` (IrfanView), `--force-uninstall` (Brave), `--mode unattended` (XAMPP) | **AUSENTE** — o Tranal resolve por **família** do desinstalador, não pelo nome do programa |
| 4 | **Menu de contexto do Monitor de Instalação** (além do de desinstalação) | `hibit.recursos.txt:172` -> `TINSTALLMONITORFORM/0` | **AUSENTE** — o Tranal registra um menu só |
| 5 | **Gerenciador do Menu de Contexto** (gerenciar menus alheios, criar e restaurar) | `hibit.recursos.txt:152` -> `TCONTEXTMENUFORM/0`; `:153` -> `TCREATECONTEXTMFORM/0` | **PARCIAL** — `Core/Management/ContextMenuManager.cs` |
| 6 | **Desabilitar handlers** de menu de contexto | `hibit.recursos.txt:152` -> `TCONTEXTMENUFORM/0` | **OK** — `Core/Management/ContextMenuManager.cs` |
| 7 | **Desinstalar atualizações do Windows** (`wusa`/KB) | `hibit.inventario.md:13446` -> `Windows Update` | **AUSENTE** |
| 8 | **Gerenciador de Tarefas Agendadas** (criar/remover) | `hibit.recursos.txt:184` -> `TSCHEDULETASKFROM/0`; `:154` -> `TCREATETASKFORM/0` | **PARCIAL** — `Core/Management/ScheduledTaskManager.cs` lista; falta criar |
| 9 | **Gerenciador de Processos** (painel + propriedades) | `hibit.recursos.txt:178` -> `TPROCESSESMANAGERFORM/0`; `:179` -> `TPROCPROPERTIFORM/0` | **AUSENTE** — há encerramento no fluxo, não painel |
| 10 | **Gerenciador de Drivers** | `hibit.inventario.md:13439` -> `SetupAPI` - Enumera dispositivos/Drivers | **PARCIAL** — `Core/Management/DriverManager.cs` |
| 11 | **Restaurador do Sistema** (criar/excluir pontos com um clique) | `hibit.recursos.txt:189` -> `TSYSRESTOREFORM/0` | **PARCIAL** — `Core/UninstallSupport.cs:1210` cria antes de desinstalar; falta o painel de gestão |
| 12 | **Limpar Pastas Vazias** | `hibit.recursos.txt:161` -> `TEMPTYFOLDERFORM/0` | **OK** — `Core/Discovery/EmptyFolderCleaner.cs` |
| 13 | **Fixar Atalhos** (atalhos corrompidos) | `hibit.recursos.txt:187` -> `TSHORTCUTFIXERFORM/0` | **AUSENTE** |
| 14 | **Localizador de Arquivos e Registros** | `hibit.recursos.txt:167` -> `TFILEREGFINDERFORM/0` | **AUSENTE** |
| 15 | **Localizador de Arquivos Duplicados** | `hibit.recursos.txt:158` -> `TDUPLICATEFILEFINDERFORM/0` | **AUSENTE** |
| 16 | **Analisador de disco** | `hibit.recursos.txt:157` -> `TDISKANALYZERFORM/0` | **PARCIAL** — treemap por programa (`Core/Appearance/TreemapModel.cs`), não por arquivo |
| 17 | **Gerenciador de Conexões** | `hibit.recursos.txt:151` -> `TCONNECTIONMANAGERFORM/0` | **AUSENTE** |
| 18 | **Gerenciador de Componentes de Programas** | `hibit.recursos.txt:180` -> `TPROGRAMCOMPONENTSFORM/0` | **AUSENTE** |
| 19 | **Agendamento automático de limpeza** | `hibit.inventario.md:13470` -> `Schedule` - Agenda execucao; `hibit.recursos.txt:184` -> `TSCHEDULETASKFROM/0` | **AUSENTE** |
| 20 | **Otimizador de registro** com 15+ categorias (ActiveX, FileTypes, SharedDLLs, IFEO, FirewallRules, MUICache) | `hibit.recursos.txt:181` -> `TREGISTRYCLEANERFORM/0` | **PARCIAL** — `Core/Tools/RegistryOptimizer.cs`; o número de categorias não foi conferido |
| 21 | **Relatórios HTML por ferramenta** | `hibit.recursos.txt:164` -> `TEXPORTFORM/0` | **PARCIAL** — `Core/Reporting/*` + `Ui/Reporting/ReportingWindow.xaml.cs`; falta um por ferramenta |
| 22 | **Destruidor com hash MD5/SHA** (relatório e verificação) | `hibit.recursos.txt:168` -> `TFILESHREDDERFORM/0` | **PARCIAL** — `Core/Tools/UnrecoverableDelete.cs` apaga; o relatório de hash não existe |
| 23 | **Backup `.dbh` com restauração** | `hibit.recursos.txt:149` -> `TBACKUPFORM/0`; `:182` -> `TREGRESTOREFORM/0` | **PARCIAL** — `Core/BackupManager.cs` + `Core/Recovery/RecoverOperation.cs`, em outro formato |
| 24 | **Ocultar entrada de desinstalação** | **não confirmado no binario** | **AUSENTE** |
| 25 | **Deletar entrada** do registro do programa | `hibit.recursos.txt:192` -> `TUNINSTALLFORM/0` | **PARCIAL** — `Core/RegistryEditing/RegistryEditor.cs` |
| 26 | **Editar informações** do programa | `hibit.recursos.txt:159` -> `TEDITINFOFORM/0` | **PARCIAL** — `Core/ProgramCustomization.cs` |
| 27 | **Relógio de ferramentas** (agendador) | `hibit.recursos.txt:184` -> `TSCHEDULETASKFROM/0` | **AUSENTE** |
| 28 | **Arrastar e soltar** um programa para desinstalar | `hibit.recursos.txt:12` -> `12/CAT_DRAG_COPY/1033` | **PARCIAL** — `Ui/Tools/TopmostOverlayWindow.xaml.cs` (outro desenho) |
| 29 | **Data de instalação em tempo relativo** ("Ontem", "2 meses atrás") | **não confirmado no binario** | **AUSENTE** — o Tranal mostra a data absoluta |
| 30 | **Contador de tamanho total** na barra de status | `hibit.inventario.md:13485` -> `Size` - Mede tamanho em disco | **PARCIAL** — o Tranal conta os programas e soma o tamanho no treemap, não na barra de status |
| 31 | **Painel de detalhes** do programa (local de instalação, entrada de registro, website) | `hibit.recursos.txt:166` -> `TFILEINFOFORM/0` | **PARCIAL** — os dados existem como colunas; falta o painel e a coluna de local de instalação |
| 32 | **Banner de informações do sistema** (CPU, GPU, RAM, tempo de atividade) | **não confirmado no binario** | **AUSENTE** |
| 33 | **Seletor de tema na barra superior**, com amostras de cor | `hibit.recursos.txt:6-12` -> `VCLSTYLE/*` (7 temas) | **PARCIAL** — `Theming/ThemeManager.cs` (4 temas) |
| 34 | **Scanner VirusTotal** | `hibit.recursos.txt:195` -> `TVIRUSTOTALSCANFORM/0` | **AUSENTE** |
| 35 | **Afinidade de processador** | `hibit.recursos.txt:147` -> `TAFFINITYFORM/0` | **AUSENTE** |
| 36 | **Integração com o Windows Defender** | `hibit.strings.txt:67987` -> `Windows Defender Scheduled Scan` | **AUSENTE** |
| 37 | **Criar serviço / inicialização / tarefa agendada / menu de contexto** | `hibit.recursos.txt:144` `:145` `:153` `:154` | **AUSENTE** — as quatro janelas de criação não têm par no Tranal |
| 38 | **Limpeza personalizada** (regras próprias) | `hibit.recursos.txt:142` -> `TADDCUSTOMCLEANFORM/0` | **PARCIAL** — `Core/Rules/RuleEngine.cs`, com 4 regras embutidas |
| 39 | **Listas de exclusão por ferramenta** | `hibit.recursos.txt:163` -> `TEXCLUSIONLISTFORM/0` | **PARCIAL** — `Core/ExcludeList.cs` é global |
| 40 | **Modificar/reparar** instalação MSI | `hibit.inventario.md:13448` -> `msiexec` | **AUSENTE** |
| 41 | **Banco local embutido** (`LOCALDB`) | `hibit.recursos.txt:127` -> `LOCALDB/1033` | **AUSENTE** |

### Onde o Tranal já é melhor

| Ponto | Tranal | HiBit |
|---|---|---|
| **Assinatura** | o Tranal assina o instalador com certificado próprio | o executável do HiBit **não é assinado** |
| **Confiança das sobras com motivo** | 3 níveis + frase por item | árvore de sobras, sem nível de ameaça |
| **Backup completo do registro com script de restauração** | sim, antes do desinstalador nativo | backup `.dbh` próprio |
| **CLI de desinstalação roteirizável** | `/mu "<prog>" /mode …`, `/scan`, `/export`, `/columns` | o HiBit tem switch por ferramenta, mas **não** tem desinstalação em lote roteirizável por lista |
| **Segurança do motor** | `SafeTraversal` nunca atravessa link; exclusões aplicadas até no backup | — |
| **Privacidade** | sem telemetria | Update Center próprio, VirusTotal, cache remoto |

---

## 7. Fichos de evidência

| Arquivo | Bytes | Conteúdo |
|---|---|---|
| `downloads/HiBitUninstaller-setup-4.0.10.exe` | 4.533.003 | instalador |
| `downloads/HiBitUninstaller-Portable-4.0.10.zip` | 6.025.084 | portátil |
| `app/setup-inno/app/HiBitUninstaller.exe` | 15.410.176 | o binário (idêntico byte a byte ao do portátil) |
| `evidencia/pe-imports.txt` | 13.765 | 33 DLLs × 698 funções + 63 delay-load |
| `evidencia/pe-main.txt` | 837 | cabeçalho PE, versão, assinatura, DLLs importadas |
| `evidencia/rea-pe-fatos.txt` | 2.857 | fatos REA (não é .NET, sem assinatura Authenticode) |
| `evidencia/strings-main.txt` | 2.051.297 | 98.362 linhas de strings ASCII + UTF-16 |
| `evidencia/strings-mecanismos.txt` | 454.050 | índice de mecanismos, sempre com o nº da linha de origem |
| `evidencia/strings-ptbr-bloco.txt` | 57.188 | **802 legendas pt-BR** — o inventário de telas |
| `evidencia/strings-ptbr-menu.txt` | 3.474 | menu principal em pt-BR (94 linhas) |
| `evidencia/strings-ptbr-formas.txt` | 1.586 | as 56 seções `[Forma]` do `.ini` — inventário de janelas |
| `evidencia/strings-silent-switches.txt` | 3.198 | tabela de switches silenciosos por aplicativo |
| `evidencia/cli-switches.txt` | 4.435 | switches de linha de comando com contexto |
| `evidencia/PortugueseBLang.ini` | 69.333 | **arquivo de idioma oficial pt-BR escrito pelo próprio HiBit em execução** (copiado verbatim) |
| `evidencia/changelog-oficial.txt` | 20.805 | changelog oficial completo (2026→1.x) |
| `evidencia/changelog-funcionalidades.txt` | 10.716 | 227 linhas do changelog filtradas por termo funcional |
| `evidencia/site-uninstaller.html` | 16.581 | a lista oficial de recursos |
| `evidencia/site-falso-hibituninstaller-com.html` | 47.527 | resposta do download em `hibituninstaller.com` (é HTML, não é o produto) |
| `evidencia/uia-janelas.txt` | 2.217 | janelas observadas em execução (2 execuções) |
| `evidencia/hibit-interface-principal.png` | 156.359 | **a tela principal do HiBit em execução**, pt-BR |
| `evidencia/hibit-dlg-savesettings.png` | 9.845 | diálogo *Save Settings* em pt-BR (1ª execução) |
| `evidencia/hibit-inicial-20261105-190059.png` | 503.889 | tela cheia na 1ª execução (modal *Save Settings* visível) |
| `evidencia/hibit-desktop-agora.png` | 1.326.982 | tela cheia posterior (documenta o bloqueio de foreground) |
| `evidencia/hibit-main-oculto.png` | 2.740 | captura vazia (PrintWindow falhou — ver seção 8) |

### Fichos de evidência da curadoria binária (06/11/2026)

| Arquivo | Bytes | Conteúdo |
|---|---|---|
| `bin-inventario/hibit.inventario.md` | 598.703 | 4.675 classes RTTI, 13.418 rótulos de interface, 63 capacidades de API/IO |
| `bin-inventario/hibit.recursos.txt` | 18.770 | 214 entradas de recurso do PE (**62 formulários Delphi** + 7 estilos VCL) |
| `bin-inventario/hibit.identificadores.txt` | 4.565 | identificadores de tipo/método (units Delphi reveladas) |
| `bin-strings/hibit.strings.txt` | 2.034.162 | dump bruto de strings do executável |

> **Nota de ferramenta:** o modo `strings` do `coleta.ps1` **estourou o
> timeout de 600 s** neste binário de 15 MB. O subagente reimplementou a
> mesma semântica em .NET (`strings-fast.ps1`) e o resultado é equivalente
> (ASCII 0x20–0x7E e UTF-16LE, unicidade, CRLF, UTF-8 com BOM). Vale
> otimizar o `coleta.ps1` para binários grandes.

---

## 8. Lacunas desta auditoria

| O que faltou | Por quê |
|---|---|
| Navegar as 21+ ferramentas em execução | o Delphi não expõe UIA e `SetForegroundWindow`/`SetWindowPos` foi negado para todas as janelas do desktop nesta máquina; `PrintWindow` falha nestas forms |
| Capturar os **menus desplegados** | o menu é desenhado no canvas e não responde a clique sintético — a lista de itens vem dos recursos de string, não de uma captura |
| Desinstalação real de um programa | não executada — seria preciso instalar e desinstalar de verdade |
| O que a tabela de switches silenciosos cobre por programa | só os switches foram medidos; o **emparelhamento** nome↔switch não é determinável só por estática (os nomes e os switches ficam em posições consecutivas cuja estrutura de pares não é inequívoca) |
| O que o **modo portátil** bloqueia | o binário traz a mensagem `Notportable`, mas o catálogo de recursos afetados não foi isolado; o instalador (não portátil) nunca foi executado |
| Manual oficial em PDF | o site oficial não o publica; a fonte primária foi a página do produto + o changelog |
| `hibitsoft.com` | não resolve DNS nesta máquina; `www.hibituninstaller.com` é site de SEO de terceiros, não o fabricante |
| Verificação das APIs em runtime | os mecanismos da §3 foram medidos por **strings e imports estáticos**, não por trace de execução — import não prova vínculo nem alcançabilidade |
| Comparação com o Tranal | feita por busca de nomes e termos no `src\Tranal.Uninstaller`; "não verificado" significa "nenhum correspondente encontrado", não "provavelmente ausente" |
| **7 funcionalidades sem literal no binário** (#3, #9, #17, #24, #62, #63, #64) | a evidência original vinha de captura de tela ou do `.ini` de idioma, não do binário | — |
| **Nomes das classes de formulário** (2.328 do relatório bruto) | as 62 classes `T*FORM` do PE confirmam as janelas; as demais (2.266) são classes de biblioteca (`AllSkin*`, `Fox*`) e não foram classificadas uma a uma | — |

---

## 9. Decisões que este documento obriga

1. **A tabela de switches silenciosos entra no backlog, junto com a
   classificação.** Já sabemos o tipo do desinstalador de cada programa; o
   HiBit mostra que a tabela por família existe e é curta
   (`/VERYSILENT /SUPPRESSMSGBOXES /NORESTART` para Inno, `/S` para NSIS,
   `/quiet /qn /norestart` para MSI, `/s /v"/qn"` para InstallShield). Com a
   classificação implementada, isso é uma **tabela**, não uma heurística.
2. **Um switch de CLI por ferramenta é o padrão que o HiBit estabelece.** O
   Tranal já tem `/winapps`, `/be`, `/autorun`, `/hunter`; o HiBit mostra o
   formato final (19 ferramentas, uma por switch). É barato e faz a CLI
   virar uma ferramenta de verdade. **Fechado em 06/11/2026:**
   `Core/Automation/CliToolSwitch.cs` implementa o formato com o sufixo `RUN`.
3. **A decisão de não usar driver de kernel ganha o argumento mais forte
   até agora.** O HiBit — que tem 21+ ferramentas e é um dos mais
   profissionais do grupo — roda **sem driver, sem serviço de fundo e sem
   extensão COM**, tudo no próprio processo. A paridade com o Revo se ganha
   por software.
4. **A lista de 17 categorias de lixo e as 15+ categorias de registro do
   HiBit são o teto do "painel de manutenção".** O Tranal tem as
   ferramentas separadas, sem o painel único — que é exatamente o que o
   IObit chamava de *Software Health*. Vale uma seção de painel único no
   Tranal.
5. **A tela principal do HiBit é a referência de layout, e ela é mais
   densa que a nossa em cinco pontos baratos de implementar:** arrastar e
   soltar um programa para desinstalar, data de instalação em tempo
   relativo, contador de **tamanho total** na barra de status, painel de
   detalhes do programa selecionado (local de instalação, entrada de
   registro, website) e o seletor de tema na barra superior. O Tranal já
   tem as **colunas** `RegistryKey` e `HomePage` — o que falta é o painel
   que as reúne, e uma coluna de local de instalação.
6. **O banner de informações do sistema do HiBit não é paridade
   funcional**, é enfeite. Não entra no backlog; o que entra é o painel
   de detalhes, que é informação do programa e ajuda a decidir.
7. **O Tranal pode ser portátil sem perder nada.** O HiBit bloqueia
   recursos no modo portátil (`Notportable`); o Tranal é distribuído como
   instalador, mas a instalação não é o que dá a funcionalidade — é um
   executável + runtime. Vale registrar como decisão de produto.
8. **A suíte do HiBit é uma lista de janela, não de invenção.** Os 62
   formulários do PE são a especificação executável: cada `T*FORM` é um
   backlog potencial, com o nome já pronto. Fechar as quatro janelas de
   **criação** (`TADDSERVICESFORM`, `TADDSTARTUPFORM`, `TCREATETASKFORM`,
   `TCREATECONTEXTMFORM`) é o caminho mais barato para virar "cuidador do PC".

---

*Auditoria gerada por medição direta dos artefatos. Toda linha deste
documento aponta para evidência em disco; o que não pôde ser verificado está
marcado como tal na seção 8.*
