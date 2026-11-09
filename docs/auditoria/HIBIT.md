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
| 1 | Lista com colunas: Nome, Versão, Editor, Data, Tamanho, Tipo, Comentários, String de desinstalação, Pasta de instalação, Site, Entrada de registro | `hibit.recursos.txt:191` -> `TUNINSTALLER/0` (formulário principal); `hibit.recursos.txt:192` -> `TUNINSTALLFORM/0`; `hibit.recursos.txt:159` -> `TEDITINFOFORM/0` | **PARCIAL** - `src/Tranal.Uninstaller/Core/ProgramColumns.cs:9-120` (24 colunas no catalogo) e `:187-218` (o catalogo de colunas), `:253` (`Value`, a mesma interpretacao da tela e da exportacao); grade montada em `src/Tranal.Uninstaller/MainWindow.xaml.cs:583`. **Nota**: **a "Pasta de instalacao" nao e coluna da grade** — nao existe `ProgramColumn` para a pasta do programa (o catalogo tem `UninstallerLocation`, que e o caminho do desinstalador, em `:57`); a pasta do programa vive no formatador de texto, no filtro e no detalhe. Binario do concorrente: `hibit.recursos.txt:191` |
| 2 | Desinstalação pelo desinstalador nativo | `hibit.recursos.txt:192` -> `TUNINSTALLFORM/0`; `hibit.recursos.txt:171` -> `TFORCEUNINSTALLFORM/0` | **OK** - `Core/Removal.cs` - motor de remocao com desinstalador nativo, modo silencioso e registro de erro do Windows (`ErrorCodeOf` em `:57`) |
| 3 | **Desinstalação em lote** ("Desinstalar Selecionado", botão + 6 itens de menu) | **não confirmado no binario** (literal de lote nao localizado nesta rodada) | **OK** - Multi-selecao **confirmada** em `MainWindow.xaml:398`; confirmacao de lote em `MainWindow.xaml.cs:1663`; motor em `Core/Automation/UninstallQueue.cs`; CLI `src/Tranal.Cli/ExtraCommands.cs:968` (`/batch`) |
| 4 | **Desinstalação forçada** ("Remoção Forçada") — reagendada para o próximo logon via `RunOnce` | `hibit.recursos.txt:171` -> `TFORCEUNINSTALLFORM/0`; `hibit.inventario.md:13448` -> `Schedule` - Agenda execucao | **PARCIAL** - `src/Tranal.Uninstaller/Core/Automation/RunOnceScheduler.cs:91` (`ScheduleUninstall`, grava em `HKCU\...\RunOnce`, `:43`) e `:50` (`Schedule`); a tela lista e cancela os agendados em `src/Tranal.Uninstaller/Ui/Tools/InspectorViews.cs:182` e `:216`. **Nota**: **nada chama `ScheduleUninstall`** — `grep "ScheduleUninstall" em src/` -> 2 hits (a definicao e `src/Tranal.Tests/FinderRunOnceAndHexTests.cs:122`), e a desinstalacao forcada (`src/Tranal.Uninstaller/Core/Automation/ForcedRemovalOptions.cs:41`) remove o alvo apontado, sem reagendar para o proximo logon. Binario do concorrente: `hibit.recursos.txt:171` (`TFORCEUNINSTALLFORM/0`) |
| 5 | **Varredura de sobras** em **árvore** (TreeView), com AutoClean e RegBackup | `hibit.recursos.txt:181` -> `TREGISTRYCLEANERFORM/0`; `hibit.inventario.md:13463` -> `Restore` - Restaura backups | **PARCIAL** - Varredura rica em `src/Tranal.Uninstaller/Core/UninstallSupport.cs:529` (`LeftoverScanner.Scan`), motivos em `:49-123` (14 motivos, com `ObsoleteShortcut` em `:88`, `ObsoleteStartMenu` em `:79`, `StartupEntry` em `:115`, `InstallerLog` em `:105`); backup do registro ligado a operacao (`src/Tranal.Uninstaller/Core/BackupManager.cs:165` `Create`, `:193` `AddRegistryKey`). **Nota**: a tela usa **`ListView`** (`src/Tranal.Uninstaller/Ui/UninstallWizardWindow.xaml:164`), **nao arvore**, e o gerenciador de servico/tarefa tem painel proprio (`src/Tranal.Uninstaller/Core/Management/ServiceManager.cs:114`, `src/Tranal.Uninstaller/Core/Management/ScheduledTaskManager.cs:80`) que nao entra na arvore de sobras. Binario do concorrente: `hibit.recursos.txt:181` |
| 6 | **Backup próprio `.dbh`** (Hibit Backup File): `BackupFiles.zip` + `FileList.txt` + `Registry.ini` | `hibit.recursos.txt:149` -> `TBACKUPFORM/0`; `:148` -> `TBACKUPDETAILSFORM/0` | **PARCIAL** - `src/Tranal.Uninstaller/Core/BackupManager.cs:165` (cria ponto), `:193` (chave de registro), `:225` (arquivo), `:257` (pasta), `:282` (salva), `:601` (restaura); painel em `src/Tranal.Uninstaller/Ui/BackupManagerWindow.xaml.cs:180`. **Nota**: o formato e **JSON + arquivos** (`src/Tranal.Uninstaller/Core/BackupManager.cs:200` grava `registry/*.json`), **nao `.dbh`** com `BackupFiles.zip`/`FileList.txt`/`Registry.ini`. Binario do concorrente: `hibit.recursos.txt:149` |
| 7 | **Ponto de restauração** antes de desinstalar — WMI `CreateRestorePoint` e `SRSetRestorePointW`/`SRRemoveRestorePoint` (`SRCLIENT.DLL`) | `hibit.inventario.md:13441` -> `SRSetRestorePoint` - Cria ponto de restauracao do sistema; `hibit.recursos.txt:189` -> `TSYSRESTOREFORM/0`; `:146` -> `TADDSYSRESTOREFORM/0` | **OK** - `Core/UninstallSupport.cs:1397` cria o ponto; ligado ao assistente em `Ui/UninstallWizardWindow.xaml.cs:554-566` |
| 8 | **Relatórios HTML por ferramenta**: `OpReport`, `ScanLog`, `ProgramCompReport`, `WindowsStoreReport`, `ServicesReport`, `DriversReport`, `StartupReport`, `SchedulerReport`, `ContextMenuReport`, `UninstallerReport` | `hibit.recursos.txt:164` -> `TEXPORTFORM/0`; `hibit.inventario.md:13469` -> `Report` - Gera relatorios | **PARCIAL** - O **gerador de relatorio HTML por ferramenta existe**: `src/Tranal.Uninstaller/Core/Reporting/ToolReportWriter.cs:40` (monta o HTML e grava o arquivo), usado pela CLI em `src/Tranal.Cli/RegistryCommands.cs:105` (`/report-html`, flag em `:48`) e por `src/Tranal.Uninstaller/Core/Tools/DriveCleaner.cs:256`. **Nota**: o Tranal **nao tem os 10 relatorios nomeados** do HiBit — ha um relatorio por ferramenta, hoje do limpador de registro. Binario do concorrente: `hibit.recursos.txt:164` (`TEXPORTFORM/0`) |
| 9 | **Ocultar entrada de desinstalação** ("Hide original uninstallation entry") | **não confirmado no binario** | **OK** - `src/Tranal.Uninstaller/Core/Management/WindowsListEntry.cs:57` (`Hide`, renomeia a chave com o sufixo `.TranalHidden` de `:47`) e `:85` (`Restore`); acionado no menu da lista em `src/Tranal.Uninstaller/MainWindow.xaml.cs:1311` (e a remocao da entrada em `:1326`). Binario do concorrente: nao confirmado no binario |
| 10 | **Deletar entrada** do registro do programa | `hibit.recursos.txt:192` -> `TUNINSTALLFORM/0`; `hibit.inventario.md:13432` -> `RegDeleteKey` - Remove chaves de registro | **PARCIAL** - Apagar chave e valor existe (`src/Tranal.Uninstaller/Core/RegistryEditing/RegistryEditor.cs:125` e `:174`) e a remocao em lote existe em `src/Tranal.Uninstaller/Core/RegistryEditing/RegistryBatchRemover.cs:82`; a CLI consome em `src/Tranal.Cli/RegistryCommands.cs:52` (`/registry-remove`, `:68`) e a remocao da chave no fluxo de desinstalacao em `src/Tranal.Uninstaller/Core/UninstallSupport.cs:2007`. **Nota**: **nao ha botao dedicado "remover so a entrada"** na interface grafica — a remocao em lote de chaves e **CLI-only**. Binario do concorrente: `hibit.recursos.txt:192` (`TUNINSTALLFORM/0`) |
| 11 | **Editar informações** do programa | `hibit.recursos.txt:159` -> `TEDITINFOFORM/0` | **OK** - `Core/ProgramCustomization.cs:136` (store), `:189` (`For`); acoes em `MainWindow.xaml.cs:1174` (renomear), `:1177` (comentario), `:1182` (icone), `:1185` (comandos) |
| 12 | Abrir chave de registro / pasta de instalação / site do programa | `hibit.recursos.txt:166` -> `TFILEINFOFORM/0`; `hibit.inventario.md:13467` -> `Shell` - Integra ao shell do Windows | **OK** - `Core/Management/SystemNavigator.cs:40` monta os alvos por programa; `:58` devolve a chave de registro; consumo na UI em `MainWindow.xaml.cs:1191-1201` ("Abrir no Windows") |
| 13 | Copiar dados do programa para a área de transferência | `hibit.recursos.txt:166` -> `TFILEINFOFORM/0` | **OK** - `Core/Appearance/ProgramTextFormatter.cs:47` monta o texto, `:215` (`Describe`) escolhe o formato; menu de copia em `MainWindow.xaml.cs:726-734` (texto / colunas / comando / detalhes) |
| 14 | Exportar a lista (HTML / TXT / CSV) | `hibit.recursos.txt:164` -> `TEXPORTFORM/0`; `hibit.inventario.md:13470` -> `Export` - Exporta dados | **OK** - `Core/Exporters.cs` cobre TXT/CSV/HTML/XLS/XML; UI em `MainWindow.xaml.cs:714-721` e `:1490-1494` |
| 15 | **Modificar/reparar** instalação MSI (`ModifyPath`, `MsiExec.exe /I`) | `hibit.inventario.md:13448` -> `msiexec` - Desinstalacao via msiexec; `hibit.recursos.txt:180` -> `TPROGRAMCOMPONENTSFORM/0` | **OK** - `Core/Discovery/MsiProductCatalog.cs:122` monta o comando de reparo, `:265` executa; produtos MSI expostos em `Ui/Discovery/DiscoveryWindow.xaml.cs` |

### 2.2 Integração com o Windows

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 16 | **Menu de contexto do Explorer**: `lnkfile\shell\HiBitUninstaller` e `exefile\shell\HiBitUninstaller` | `hibit.inventario.md:13493` -> `ContextMenu` - Adiciona ao menu de contexto; `hibit.recursos.txt:153` -> `TCREATECONTEXTMFORM/0` | **OK** - `Core/Topmost/ExplorerContextMenu.cs:189` liga/desliga a entrada em HKCU; `:132` gera o script `.reg`; `:156` apaga a entrada |
| 17 | **Menu de contexto do Monitor de Instalação**: `Msi.Package\shell\HiBitMonitor` e `exefile\shell\HiBitMonitor` | `hibit.recursos.txt:172` -> `TINSTALLMONITORFORM/0`; `:173` -> `TINSTALLMONITORIGNOREFORM/0` | **OK** - a entrada do monitor de instalacao `Core/Topmost/ExplorerContextMenu.cs:336` |
| 18 | **Gerenciador do Menu de Contexto** — lista, cria e remove menus do shell, com "Restaurar" e "Excluir tudo" | `hibit.recursos.txt:152` -> `TCONTEXTMENUFORM/0`; `:153` -> `TCREATECONTEXTMFORM/0` | **PARCIAL** - `src/Tranal.Uninstaller/Core/Management/ContextMenuManager.cs:70` (`List`) e `:137` (`ListFor`, filtra por programa), `:148` (`Remove`); painel em `src/Tranal.Uninstaller/Ui/Management/ManagementView.xaml.cs:175`. **Nota**: **nao ha criacao de menu arbitrario**, nem "Restaurar", nem "Apagar tudo" — a unica acao e remover. Binario do concorrente: `hibit.recursos.txt:152` (`TCONTEXTMENUFORM/0`) |
| 19 | **Desabilitar handlers** de menu de contexto (`DisableCon\Directory\shellex\…`, `Folder`, `Drive`, `exefile`, `lnkfile`, `Directory\Background`, `DesktopBackground`, `CLSID\{645FF040-…}`) | `hibit.recursos.txt:152` -> `TCONTEXTMENUFORM/0`; `hibit.inventario.md:13493` -> `ContextMenu` | **PARCIAL** - Os pontos do shell que o HiBit desabilita **sao enumerados** em `src/Tranal.Uninstaller/Core/Management/ContextMenuManager.cs:58-67` (`*`, `AllFilesystemObjects`, `Directory`, `Directory\Background`, `Folder`, `Drive`, `Folder\...\Directory\Background`) e o limpador de registro os cobre (`src/Tranal.Uninstaller/Core/Tools/RegistryCleaner.cs:989-994`, categoria "Extensoes do shell"). **Nota**: **nao ha acao de "desabilitar"** — so apagamento (`ContextMenuManager.cs:148`); faltam `exefile`, `lnkfile`, `DesktopBackground` e `CLSID\{645FF040-...}`. Binario do concorrente: `hibit.recursos.txt:152` (`TCONTEXTMENUFORM/0`) |
| 20 | **Monitoramento de instalação** com eventos `ApplicationInstalled`, `ApplicationUninstalled`, `ApplicationDeleteRestore`, `DeviceInstalled` | `hibit.recursos.txt:172` -> `TINSTALLMONITORFORM/0`; `hibit.inventario.md:13471` -> `Monitor` - Monitora em segundo plano | **OK** - `Core/Modules/InstallMonitor.cs:103` inicia e monitora por comparacao de fotografia (`Core/Modules/SystemSnapshot.cs:136`); eventos gravados em `Core/Modules/LogOperations.cs:165` |
| 21 | Desinstalar **atualizações do Windows**: `cmd.exe /c wusa.exe /uninstall /` + `KBID=` | `hibit.inventario.md:13446` -> `Windows Update` - Interage com Windows Update | **OK** - **A catalogacao do HiBit esta obsoleta.** `Core/Management/WindowsUpdateManager.cs:33` invoca `wusa.exe` (`:52`) com o KB e le o codigo de saida (`:78`/`:84`); `:95` apaga varios KBs. A CLI expoe o comando de Windows Update em `src/Tranal.Cli/ExtraCommands.cs` |
| 22 | Desinstalar **app MSIX**: `powershell.exe Remove-AppxPackage` | `hibit.inventario.md:13448` -> `msiexec` - Desinstalacao via msiexec; `hibit.recursos.txt:196` -> `TWINDOWSAPPFORM/0` | **OK** - `Core/Modules/WindowsApps.cs:96` desinstala por `PackageFullName` (`:103`); painel em `Ui/Modules/ModuleViews.cs:140`/`:159`; CLI `/wa` em `src/Tranal.Cli/ExtraCommands.cs:51` |

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
| 59 | **Arrastar e soltar** para desinstalar um programa (anunciado na barra de estado) | `hibit.recursos.txt:12` -> `12/CAT_DRAG_COPY/1033` (cursor de arrastar) | **OK** - `Core/Modules/HunterMode.cs:41` resolve o alvo arrastado (inclusive `.lnk`, `:57`/`:232`); area de drop em `Ui/Modules/HunterView.xaml` |
| 60 | Painel de informações do sistema no banner (CPU, GPU, RAM disponível, SO, tempo de atividade) | `hibit.inventario.md:13431` -> (sem token equivalente — ausência **não** prova) | **PARCIAL** - A coleta e a tela existem: `src/Tranal.Uninstaller/Core/Modules/SystemInfoPanel.cs:41` (classe), `Collect` `:47`, CPU `:54`, RAM via WMI `Win32_OperatingSystem` `:122`, disco, GPU `:65` e tempo de atividade (`UptimeInfo` `:200`). Tela `src/Tranal.Uninstaller/Ui/Tools/InsightViews.cs:52`, carga em `:81`; barra lateral `src/Tranal.Uninstaller/MainWindow.xaml:355`; despacho `src/Tranal.Uninstaller/Ui/Tools/ToolViews.cs:62`. **Nota**: o painel **nao esta no banner** - e uma tela da barra lateral; no banner da `MainWindow` so existe a barra de status (`src/Tranal.Uninstaller/MainWindow.xaml:638-640`), sem CPU, GPU, RAM ou SO. Binario do concorrente: nao confirmado no binario |
| 61 | Seletor de tema com amostras de cor na barra superior (7 amostras + botão "…") | `hibit.recursos.txt:6-12` -> `VCLSTYLE/CARBON`, `COBALTXEMEDIA`, `ICEBERGCLASSICO`, `LUNA`, `TABLETDARK`, `WINDOWSMODERNDARK`, `WINDOWSSLATEGRAY` (7 temas VCL no PE) | **PARCIAL** - `src/Tranal.Uninstaller/Core/Appearance/ThemePalette.cs:94` (`ThemeSkinCatalog.All`: **4** temas — Dark, Slate, Contrast, Light) e `:384` (`Roles`); seletor no menu principal em `src/Tranal.Uninstaller/MainWindow.xaml.cs:982-989`; tema automatico do Windows em `src/Tranal.Uninstaller/Theming/ThemeProbe.cs:11`. **Nota**: **4 temas, nao 7**, e o seletor e um item de menu — **nao ha amostras de cor na barra superior**. Binario do concorrente: `hibit.recursos.txt:6-12` (7 estilos VCL no PE) |
| 62 | Botão **Doar** (adicionado na 4.0.10) | **não confirmado no binario** | **AUSENTE** - `grep "Doar/Donate/Doacao" em src/` -> **0 hits**. Funcao sem equivalente; irrelevante para paridade funcional. Binario do concorrente: nao confirmado no binario |
| 63 | Data de instalação com **tempo relativo** ("Hoje", "Ontem", "1 mês atrás", "2 meses antes", "1 ano atrás") | **não confirmado no binario** (captura de tela, não string do binário) | **OK** - `src/Tranal.Uninstaller/Core/Modules/RelativeDate.cs:20` (classe), `Format` `:28` (com "Ontem" em `:42`), `FormatFromText` `:57`, `TryParse` `:69`. A coluna da data usa `RelativeDate.Format(program.InstallDateValue)` em `src/Tranal.Uninstaller/Core/ProgramColumns.cs:284`, com a coluna declarada em `:112` e `:216`; `src/Tranal.Uninstaller/Ui/ProgramRow.cs:82`; `src/Tranal.Uninstaller/Ui/ProgramColumnLayout.cs:46` e `:78`. Binario do concorrente: nao confirmado no binario |
| 64 | Coluna **Tipo** com `32bit` / `64bit` por programa | **não confirmado no binario** | **OK** - **A catalogacao do HiBit esta obsoleta.** A coluna existe: `Core/ProgramColumns.cs:21` no enum, `:239` no render, e o bit e detectado em `Core/ProgramScanner.cs:276` (`DetectBits`, chamado em `:177`) |
| 65 | Contadores na barra de status: total de programas **e tamanho total ocupado** ("Total: 79 Unidade", "Tamanho: 610 GB") | `hibit.inventario.md:13485` -> `Size` - Mede tamanho em disco | **PARCIAL** - A barra de status existe e mostra a **contagem**: `src/Tranal.Uninstaller/MainWindow.xaml.cs:212` (`UpdateStatus`) e `:221` (`Status.Installations`), com o filtro aplicado em `:222`. **Nota**: o **tamanho total** foi implementado no nucleo (`src/Tranal.Uninstaller/Core/Modules/RelativeDate.cs:148` `ProgramTotalsCalculator.Compute` e `:129` `ProgramTotals.Text`), mas esse calculo **so tem consumidor de teste** (`src/Tranal.Tests/InsightsAndRelocationTests.cs:101`): `grep "ProgramTotalsCalculator" em src/` -> 3 hits, todos no nucleo ou no teste — **a barra de status nao mostra o tamanho**. Binario do concorrente: `hibit.inventario.md:13485` |
| 66 | Painel de detalhes do programa selecionado: **Local de Instalação**, **Entrada de Registro** e **Website** | `hibit.recursos.txt:166` -> `TFILEINFOFORM/0` | **OK** - `Ui/Tools/TopmostOverlayWindow.xaml.cs:485-486` mostra editora e local; `Core/ProgramColumns.cs:242` (site) e `:256` (chave de registro) |

### 2.5 A suíte de ferramentas com evidência de binário

Cada linha cita o **recurso de formulário Delphi** no PE (`hibit.recursos.txt`).

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 67 | Gerenciador de **Inicialização** (Startup) | `hibit.recursos.txt:188` -> `TSTARTUPMANAGERFORM/0`; `:145` -> `TADDSTARTUPFORM/0`; `:160` -> `TEDITSTARTUPFORM/0` | **OK** - `src/Tranal.Uninstaller/Core/Tools/AutorunManager.cs:105` (`Scan`), `:130` (`SetEnabled`, liga/desliga), `:155` (`Remove`); painel na tela em `src/Tranal.Uninstaller/Ui/Tools/ToolViews.cs:129` (`CreateAutorun`), com os comandos Refresh/Enable/Disable/Remove em `:145-148`; a CLI lista em `src/Tranal.Cli/ExtraCommands.cs:200`. Binario do concorrente: `hibit.recursos.txt:188` (`TSTARTUPMANAGERFORM/0`) |
| 68 | Gerenciador de **Serviços** | `hibit.recursos.txt:185` -> `TSERVICESFORM/0`; `:144` -> `TADDSERVICESFORM/0` | **OK** - **A catalogacao do HiBit esta obsoleta.** `Core/Management/ServiceManager.cs:215` tem `Create` (criacao de servico); o painel em `Ui/Management/ManagementView.xaml.cs:154` lista e opera (`Disable` `:268`, apagamento `:280`); CLI `/create-service` em `src/Tranal.Cli/RegistryCommands.cs:282` |
| 69 | Gerenciador de **Processos** (painel) | `hibit.recursos.txt:178` -> `TPROCESSESMANAGERFORM/0` | **OK** - **A catalogacao do HiBit esta obsoleta.** O painel existe: `Ui/Tools/ToolViews.cs:568-616` (colunas e Refresh em `:584`), alimentado por `Core/Modules/ProcessInspector.cs:82`; registrado no catalogo de ferramentas em `Core/ToolCatalog.cs:58`; CLI `/processes` em `src/Tranal.Cli/RegistryCommands.cs:46` |
| 70 | Gerenciador de **Tarefas Agendadas** | `hibit.recursos.txt:184` -> `TSCHEDULETASKFROM/0`; `hibit.inventario.md:13438` -> `TaskScheduler` - Agenda tarefas | **OK** - `Core/Management/ScheduledTaskManager.cs:80` lista, `:179` **cria**; painel em `Ui/Management/ManagementView.xaml.cs:157` (operacoes em `:289`/`:301`); CLI `/create-task` em `src/Tranal.Cli/RegistryCommands.cs:305` |
| 71 | Gerenciador de **Menu de Contexto** | `hibit.recursos.txt:152` -> `TCONTEXTMENUFORM/0` | **PARCIAL** - `src/Tranal.Uninstaller/Core/Management/ContextMenuManager.cs:70` (`List`) e `:137` (`ListFor`, filtro por programa), `:148` (`Remove`); painel em `src/Tranal.Uninstaller/Ui/Management/ManagementView.xaml.cs:175` (e a correlacao por programa em `src/Tranal.Uninstaller/Core/Management/AppPermissionManager.cs:120`). **Nota**: **sem "Restaurar"** e **sem "Apagar tudo"** — so apagamento (`ContextMenuManager.cs:148`). Binario do concorrente: `hibit.recursos.txt:152` (`TCONTEXTMENUFORM/0`) |
| 72 | Gerenciador de **Componentes de Programas** (DLLs compartilhadas) | `hibit.recursos.txt:180` -> `TPROGRAMCOMPONENTSFORM/0` | **OK** - `src/Tranal.Uninstaller/Core/Modules/SharedDllCatalog.cs:60` (classe), `CatalogKeys` `:65-69` (`SharedDLLs` de 64 e 32 bits), `List` `:77`, contador de referencia lido do registro em `:97` e `:108`, quem usa cada biblioteca em `UsersOf` `:126` e `ProgramUses` `:152`, remocao com decremento do contador em `Remove` `:190` e `:214`. Painel na tela `src/Tranal.Uninstaller/Ui/Tools/CleanupViews.cs:542`, com a coluna de contador em `:552` e `:580`, `SharedScan` `:558` e `SharedRemove` `:559`; CLI `src/Tranal.Cli/CleanupCommands.cs:41` e `:272` (`tranal /shared-dlls`); contador canonico em `src/Tranal.Uninstaller/Core/Discovery/SharedReferenceCounter.cs:60` e `:121`, exposto na CLI em `src/Tranal.Cli/RegistryCommands.cs:258`. Binario do concorrente: confirmado em binario (`hibit.recursos.txt:180`, `TPROGRAMCOMPONENTSFORM/0`) |
| 73 | Gerenciador de **Extensões de Navegador** | `hibit.recursos.txt:150` -> `TBROWSEREXTFORM/0` | **OK** - **A catalogacao do HiBit esta obsoleta.** `Core/Modules/BrowserExtensions.cs:83-101` lista **16** navegadores Chromium (chrome, edge, edgebeta, edgedev, brave, vivaldi, yandex, chromium, opera, operagx, comodo, torch, coccoc, cent, chedot, iridium) **mais Firefox** (`:56`) = **17 navegadores**; painel em `Ui/Modules/ModuleViews.cs:224`/`:243`; CLI `/be` em `src/Tranal.Cli/ExtraCommands.cs:52` |
| 74 | Gerenciador de **Conexões** (rede) | `hibit.recursos.txt:151` -> `TCONNECTIONMANAGERFORM/0` | **FORA POR DECISAO** - `docs/auditoria/BINARIO.md` 3.2: "Analisador de disco, gerenciador de conexoes, de fontes, papel de parede, destruidor de arquivos - Nao e dominio de desinstalacao" |
| 75 | **Backups & Reports** | `hibit.recursos.txt:149` -> `TBACKUPFORM/0`; `:148` -> `TBACKUPDETAILSFORM/0`; `:164` -> `TEXPORTFORM/0` | **OK** - Painel de backup em `Ui/BackupManagerWindow.xaml.cs`; painel de relatorio em `Ui/Reporting/ReportingWindow.xaml.cs`; atalhos de menu em `MainWindow.xaml.cs:751` e `:914` |
| 76 | **Restaurador do Sistema** (criar/excluir pontos com um clique) | `hibit.recursos.txt:189` -> `TSYSRESTOREFORM/0`; `:146` -> `TADDSYSRESTOREFORM/0`; `hibit.inventario.md:13441` -> `SRSetRestorePoint` | **PARCIAL** - Criar ponto existe: `src/Tranal.Uninstaller/Core/UninstallSupport.cs:1397` (`TryCreateRestorePoint`, WMI `SystemRestore`), ligado ao assistente em `src/Tranal.Uninstaller/Ui/UninstallWizardWindow.xaml.cs:591` e a CLI em `src/Tranal.Cli/Program.cs:473`; os backups proprios sao restaurados por `src/Tranal.Uninstaller/Core/BackupManager.cs:601`, com painel em `src/Tranal.Uninstaller/Ui/BackupManagerWindow.xaml.cs:304`. **Nota**: **nao exclui pontos de restauracao do Windows** — `grep "SRRemoveRestorePoint" em src/` -> **0 hits**; o que a CLI apaga (`src/Tranal.Cli/ExtraCommands.cs:451-453`, `delete`/`delete-all`/`delete-older`) sao os pontos **proprios** do Tranal (`BackupManager`), nao os do "Restaurador do Sistema". Binario do concorrente: `hibit.recursos.txt:189` (`TSYSRESTOREFORM/0`) |
| 77 | **Limpador de Registro** (15+ categorias: ActiveX, FileTypes, SharedDLLs, IFEO, FirewallRules, MUICache) | `hibit.recursos.txt:181` -> `TREGISTRYCLEANERFORM/0` | **OK** - **A catalogacao do HiBit esta obsoleta.** `Core/Tools/RegistryCleaner.cs:152-250` define **16 categorias**: ActiveX (`:156`), AppPaths (`:162`), Classes COM (`:168`), FileTypes (`:174`), FirewallRules (`:180`), Fontes (`:186`), IFEO (`:192`), Produtos do Installer (`:198`), **MUICache** (`:204`), Abrir com (`:210`), ProgID (`:216`), Itens de inicializacao (`:222`), **SharedDLLs** (`:228`), Extensoes do shell (`:234`), TypeLib (`:240`), Entradas de desinstalacao (`:246`). Varredura em `:264`, remocao em `:322`; painel em `Ui/Tools/ToolViews.cs:486-560` |
| 78 | **Limpador de Arquivos de Lixo** (17 categorias) | `hibit.recursos.txt:174` -> `TJFCLEANERFORM/0`; `:175` -> `TJFSETTINGFORM/0`; `hibit.identificadores.txt:169` -> `TList<JunkFilesCleaner.TCleanFile.CleanS.TFileInfo>` | **PARCIAL** - `src/Tranal.Uninstaller/Core/Tools/JunkCleaner.cs:55` (`Scan`) e `:91` (limpa). **Nota**: as categorias do limpador sao **8** — "temp-user", "temp-windows", "wucache", "browser-cache", "logs", "prefetch", "thumbnails", "recyclebin" (`src/Tranal.Uninstaller/Core/Tools/JunkCleaner.cs:30-32`) — **8, nao 17**; os complements estao em modulos separados (`src/Tranal.Uninstaller/Core/Tools/HistoryCleaner.cs:55`, `TrackCleaner.cs:42`, `ChkdskFragmentCleaner.cs:49`, `PrivacyTraceCleaner.cs:90`). Binario do concorrente: `hibit.recursos.txt:174` (`TJFCLEANERFORM/0`) |
| 79 | **Limpador de Pastas Vazias** | `hibit.recursos.txt:161` -> `TEMPTYFOLDERFORM/0`; `:162` -> `TEMPTYFOLSETTFORM/0` | **OK** - `Core/Discovery/EmptyFolderCleaner.cs:138` acha e `:194` limpa; raiz padrao em `:89` |
| 80 | **Fixador de Atalhos** (atalhos corrompidos) | `hibit.recursos.txt:187` -> `TSHORTCUTFIXERFORM/0` | **OK** - `src/Tranal.Uninstaller/Core/Modules/ShortcutFixer.cs:110` (`Scan`), os tipos de problema em `KindMissingTarget` `:73`, `KindMissingFolder` `:76` e `KindEmptyTarget` `:79`, `Analyze` `:330`, `Repair` `:152` (aponta o atalho para o alvo candidato, `FindCandidate` `:372`), remocao do atalho em `Remove` `:220`, `WriteTarget` `:295`. Tela `src/Tranal.Uninstaller/Ui/Tools/CleanupViews.cs:376`, com `ShortcutScan` em `:392`; CLI `src/Tranal.Cli/CleanupCommands.cs:39`, reparo em `:210` e remocao em `:217` (`tranal /shortcuts /repair`). Binario do concorrente: confirmado em binario (`hibit.recursos.txt:187`, `TSHORTCUTFIXERFORM/0`) |
| 81 | **Destruidor de Arquivos** (MD5 / SHA-1 / SHA-256 / SHA-512 / CRC32) | `hibit.recursos.txt:168` -> `TFILESHREDDERFORM/0`; `hibit.inventario.md:13486` -> `Shred` - Apaga de forma segura | **PARCIAL** - `src/Tranal.Uninstaller/Core/Tools/UnrecoverableDelete.cs:118` (`Delete`, sobrescreve e apaga, 1 a 35 sobrescritas em `:121`), painel na tela em `src/Tranal.Uninstaller/Ui/Tools/ToolViews.cs:656` (`CreateUnrecoverable`) e `:674` (botao de apagar). **Nota**: **nao ha MD5/SHA-1/SHA-256/SHA-512/CRC32 nem relatorio de hash no destruidor** — as somas que existem (`src/Tranal.Uninstaller/Core/Tools/HexViewer.cs:108` MD5 e `:143` SHA-256) sao do visualizador/inspetor (`src/Tranal.Uninstaller/Ui/Tools/InspectorViews.cs:268`), e o SHA-256 interno do codigo serve ao detector de duplicados (`src/Tranal.Uninstaller/Core/Management/SoftwareHealth.cs:426`). Binario do concorrente: `hibit.recursos.txt:168` (`TFILESHREDDERFORM/0`) |
| 82 | **Localizador de Arquivos e Registros** | `hibit.recursos.txt:167` -> `TFILEREGFINDERFORM/0` | **OK** - `src/Tranal.Uninstaller/Core/Discovery/ResourceFinder.cs:42` (`Casa`, padrao glob/regex) e a busca de arquivos e de chaves (`FindFiles`/`FindRegistryKeys`, consumidas em `src/Tranal.Uninstaller/Ui/Tools/InspectorViews.cs:122` e `:125`). Binario do concorrente: `hibit.recursos.txt:167` (`TFILEREGFINDERFORM/0`) |
| 83 | **Localizador de Arquivos Duplicados** | `hibit.recursos.txt:158` -> `TDUPLICATEFILEFINDERFORM/0`; `hibit.inventario.md:13484` -> `Duplicate` - Detecta duplicados | **PARCIAL** - `src/Tranal.Uninstaller/Core/Management/SoftwareHealth.cs:184` (`DuplicateFindings`) com SHA-256 (`:426` `HashOf`), categoria `RedundantFiles` (`:26`), ligado ao painel de saude em `src/Tranal.Uninstaller/Ui/Management/ManagementView.xaml.cs:318` (botao `AnalyzeHealthButton_Click`). **Nota**: **nao e um localizador autonomo** — e a categoria "Arquivos repetidos" do painel Software Health, acionada por programa (`SoftwareHealth.cs:119`, `scanDuplicates`). Binario do concorrente: `hibit.recursos.txt:158` (`TDUPLICATEFILEFINDERFORM/0`) |
| 84 | **Analisador de disco** | `hibit.recursos.txt:157` -> `TDISKANALYZERFORM/0`; `hibit.inventario.md:13485` -> `Size` - Mede tamanho em disco | **FORA POR DECISAO** - `docs/auditoria/BINARIO.md` 3.2: "Analisador de disco, gerenciador de conexoes, de fontes, papel de parede, destruidor de arquivos - Nao e dominio de desinstalacao" |
| 85 | **Gerenciador de Apps da Microsoft Store** | `hibit.recursos.txt:196` -> `TWINDOWSAPPFORM/0` | **OK** - `Core/Modules/WindowsApps.cs:55` lista via `Get-AppxPackage` (`:43`), `:96` desinstala; painel em `Ui/Modules/ModuleViews.cs:140`/`:159`; CLI `/winapps` em `src/Tranal.Cli/ExtraCommands.cs:50` |
| 86 | **Gerenciador de Atualizações do Windows** | `hibit.recursos.txt:197` -> `TWINUPDATEFORM/0`; `hibit.inventario.md:13446` -> `Windows Update` | **OK** - **A catalogacao do HiBit esta obsoleta.** Deixou de ser categoria do Discovery: agora ha modulo proprio `Core/Management/WindowsUpdateManager.cs` - `:33` apaga KB, `:132` lista instaladas; a categoria do Discovery segue em `Ui/Discovery/DiscoveryWindow.xaml.cs:42` |

### 2.6 Funcionalidades descobertas **no binário** e ausentes do documento anterior

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 87 | **Propriedades de processo** (painel dedicado) | `hibit.recursos.txt:179` -> `TPROCPROPERTIFORM/0` | **PARCIAL** - `src/Tranal.Uninstaller/Core/Modules/ProcessInspector.cs:12-51` expoe nome, caminho, descricao, empresa, versao, usuario, memoria, threads e data de inicio; painel na tela em `src/Tranal.Uninstaller/Ui/Tools/ToolViews.cs:593` (`CreateProcessPanel`) com 5 colunas em `:602-606` (nome/dono/caminho/memoria/afinidade). **Nota**: o painel mostra **5 colunas** — **nao ha janela dedicada de propriedades** por processo com todos os campos. Binario do concorrente: `hibit.recursos.txt:179` (`TPROCPROPERTIFORM/0`) |
| 88 | **Afinidade de processador** do programa | `hibit.recursos.txt:147` -> `TAFFINITYFORM/0`; `hibit.strings.txt:25783` -> `GetProcessAffinityMask`; `:25791` -> `SetProcessAffinityMask`; `:70074` -> `Affinity1.Caption=Affinity` | **OK** - `src/Tranal.Uninstaller/Core/Modules/ProcessAffinity.cs:51` (`ProcessAffinity`), `:60` (`List`) e `:79` (`Of`, com `GetProcessAffinityMask`/`SetProcessAffinityMask`); painel na tela em `src/Tranal.Uninstaller/Ui/Tools/ToolViews.cs:629` (coluna de afinidade em `:606`) e CLI em `src/Tranal.Cli/InspectionCommands.cs:284`. Binario do concorrente: `hibit.recursos.txt:147` (`TAFFINITYFORM/0`) |
| 89 | **Scanner VirusTotal** (verificação de arquivo na nuvem) | `hibit.recursos.txt:194` -> `TVIRUSTOTALAPIFORM/0`; `:195` -> `TVIRUSTOTALSCANFORM/0`; `hibit.strings.txt:18270` -> `VirusTotal_API`; `:22823` -> `AddVirusTotalScannertoContextMenu1` | **FORA POR DECISAO** - `docs/auditoria/BINARIO.md` 3.2: "Base de logs **online** do Revo - Decisao de produto: o Tranal e offline, sem cadastro e sem telemetria". **Ver Limites**: a decisao nao nomeia o VirusTotal; `grep "VirusTotal" src/` -> **0 hits** confirma que o Tranal nao faz isso |
| 90 | Integração com o **Windows Defender** (cache, limpeza, varredura agendada) | `hibit.strings.txt:65936` -> `Defender`; `:67985` -> `Windows Defender Cache Maintenance`; `:67987` -> `Windows Defender Scheduled Scan`; `hibit.inventario.md:13456` -> `Defender` - Integra-se ao Defender | **FORA POR DECISAO** - Mesma decisao offline (`docs/auditoria/BINARIO.md` 3.2). O Tranal apenas **protege** o Defender da desinstalacao: `Core/Rules/BuiltinRules.cs:22` ("Microsoft Defender", "Antimalware do Windows: a desinstalacao dele deixa o sistema sem protecao"). `grep "Defender" src/` -> 2 hits, ambos de regra/rotulo, **nenhuma integracao** |
| 91 | **Criar item de inicialização** do zero | `hibit.recursos.txt:145` -> `TADDSTARTUPFORM/0`; `:160` -> `TEDITSTARTUPFORM/0` | **OK** - modulo de inicializacao automatica `Core/Tools/AutorunManager.cs:72`; a criacao do item `Core/Tools/AutorunManager.cs:189`; o dialogo de entrada `Ui/Shared/InputDialog.cs:31` |
| 92 | **Criar serviço** do Windows | `hibit.recursos.txt:144` -> `TADDSERVICESFORM/0`; `hibit.inventario.md:13435` -> `CreateService` - Gerencia servicos do Windows | **OK** - `src/Tranal.Uninstaller/Core/Management/ServiceManager.cs:215` (`Create`); exposto na CLI em `src/Tranal.Cli/RegistryCommands.cs:273` (`/create-service`, flag em `:44`) e o painel de servicos em `src/Tranal.Uninstaller/Ui/Management/ManagementView.xaml.cs:264`. Binario do concorrente: `hibit.recursos.txt:144` (`TADDSERVICESFORM/0`) |
| 93 | **Criar tarefa agendada** | `hibit.recursos.txt:154` -> `TCREATETASKFORM/0` | **OK** - `src/Tranal.Uninstaller/Core/Management/ScheduledTaskManager.cs:185` (`Create`); exposto na CLI em `src/Tranal.Cli/RegistryCommands.cs:296` (`/create-task`, flag em `:45`) e o painel de tarefas em `src/Tranal.Uninstaller/Ui/Management/ManagementView.xaml.cs:298`. Binario do concorrente: `hibit.recursos.txt:154` (`TCREATETASKFORM/0`) |
| 94 | **Criar menu de contexto** no Explorer | `hibit.recursos.txt:153` -> `TCREATECONTEXTMFORM/0` | **OK** - `Core/Topmost/ExplorerContextMenu.cs:189` grava a entrada em HKCU (`:202-216`) e grava o script `.reg` (`:224`); `:156` apaga a entrada |
| 95 | **Limpeza personalizada** (regras próprias por ferramenta) | `hibit.recursos.txt:142` -> `TADDCUSTOMCLEANFORM/0`; `:155` -> `TCUSTOMCLEANFORM/0`; `hibit.strings.txt:57758` -> `UCustomClean` | **OK** - `Core/Rules/RuleEngine.cs:43` (motor), `:113` carrega regras do arquivo, `Core/Rules/RemovalRule.cs:63` (regra), `Core/Tools/CustomToolRegistry.cs:68` (ferramentas proprias); regras prontas em `Core/Rules/BuiltinRules.cs` |
| 96 | **Listas de exclusão / ignorar** por ferramenta (monitor, registro, junk, atalhos) | `hibit.recursos.txt:143` -> `TADDIGNOREMONITORFORM/0`; `:163` -> `TEXCLUSIONLISTFORM/0`; `:169` -> `TFJIGNOREFORM/0`; `:186` -> `TSHORTCATIGNORELISTFORM/0`; `hibit.strings.txt:19967` -> `RegIgnoreList` | **PARCIAL** - `src/Tranal.Uninstaller/Core/ExcludeList.cs:233` (`Load`) e `:279` (`Current`); janela em `src/Tranal.Uninstaller/Ui/ExcludeListWindow.xaml.cs:48` (pastas) e `:49` (chaves), com a protecao aplicada no motor (`src/Tranal.Uninstaller/Core/Rules/RuleEngine.cs:274`). **Nota**: **lista global, nao uma por ferramenta** — `src/Tranal.Uninstaller/Core/ExcludeList.cs:19` tem um unico conjunto (`Paths` e `RegistryKeys`). Binario do concorrente: `hibit.recursos.txt:143` (`TADDIGNOREMONITORFORM/0`) |
| 97 | **Restaurar o registro** a partir do backup | `hibit.recursos.txt:182` -> `TREGRESTOREFORM/0` | **OK** - `Core/BackupManager.cs:193` guarda a chave no backup, `:601` restaura; fluxos em `Core/Recovery/ProgramBackup.cs:51` e `Core/Recovery/RecoverOperation.cs:16` |
| 98 | **Verificação de integridade de arquivo** e limpeza pontual de arquivo | `hibit.recursos.txt:165` -> `TFILECHECKSFORM/0`; `:166` -> `TFILEINFOFORM/0`; `:156` -> `TDELETEFILEFORM/0` | **PARCIAL** - A **limpeza pontual existe**: `src/Tranal.Uninstaller/Core/Tools/UnrecoverableDelete.cs:118` (`Delete`), painel em `src/Tranal.Uninstaller/Ui/Tools/ToolViews.cs:656`; as somas MD5/SHA-256 sao calculadas e exibidas em `src/Tranal.Uninstaller/Core/Tools/HexViewer.cs:108` e `:143`, mostradas no inspetor em `src/Tranal.Uninstaller/Ui/Tools/InspectorViews.cs:268-269` e na CLI em `src/Tranal.Cli/MaintenanceCommands.cs:116`. **Nota**: a **verificacao de integridade** (comparar o hash com o esperado) existe no nucleo (`src/Tranal.Uninstaller/Core/Tracing/InstallChecksum.cs:222` `Verify`) mas **nao tem consumidor** — `grep "InstallChecksum.Verify" em src/` -> 1 hit, so em `src/Tranal.Tests/MaintenanceFeatureTests.cs:155`. Binario do concorrente: `hibit.recursos.txt:165` (`TFILECHECKSFORM/0`) |
| 99 | **Sete temas VCL** embutidos no binário | `hibit.recursos.txt:6-12` -> `VCLSTYLE/CARBON` / `COBALTXEMEDIA` / `ICEBERGCLASSICO` / `LUNA` / `TABLETDARK` / `WINDOWSMODERNDARK` / `WINDOWSSLATEGRAY` | **PARCIAL** - `src/Tranal.Uninstaller/Core/Appearance/ThemePalette.cs:94` (`ThemeSkinCatalog.All`) define **4** temas — Dark, Slate, Contrast, Light (`:96-123`); seletor no menu em `src/Tranal.Uninstaller/MainWindow.xaml.cs:982-989`. **Nota**: **4, nao 7**. Binario do concorrente: `hibit.recursos.txt:6-12` (7 estilos VCL no PE) |
| 100 | **Banco local embutido** (`LOCALDB`) | `hibit.recursos.txt:127` -> `LOCALDB/1033` | **AUSENTE** - `grep "LOCALDB/Sqlite/Microsoft.Data.Sqlite" em src/` -> **0 hits**. O estado e JSON: `src/Tranal.Uninstaller/Core/Settings/ProgramCache.cs:154` (`ProgramCacheStore`), `:188` (salva), `:252` (carrega); o log de instalacao vive em `src/Tranal.Uninstaller/Core/Modules/TraceStore.cs:68`. **Nao ha banco local**. Binario do concorrente: `hibit.recursos.txt:127` (`LOCALDB/1033`) |
| 101 | **Salvar log** de ferramenta em arquivo | `hibit.recursos.txt:176` -> `TLOGSAVEFORM/0`; `hibit.inventario.md:13466` -> `Log` - Mantem registro/log | **OK** - `Core/Modules/LogOperations.cs:165` gerencia o log de instalacao (`:175` entradas, `:490` add), `Core/RemovalLog.cs` grava o log por operacao, `Core/Reporting/TraceLogExporter.cs:203` exporta, `Core/Tools/WindowsToolsCatalog.cs:251` e o executor das ferramentas com log proprio |
| 102 | Formulário inicial de **escolha de onde guardar a configuração** | `hibit.recursos.txt:183` -> `TSAVESETTINGSFORM/0` | **OK** - `Ui/FirstRunWindow.xaml.cs` - escolha de onde guardar a configuracao no primeiro uso |

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
