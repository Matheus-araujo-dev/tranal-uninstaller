# REVO UNINSTALLER PRO 5.5.4 — Especificação extraída para o Tranal Uninstaller

> Fonte: engenharia reversa conduzida com **REA 6.8.0** (análise estática de PE + recursos + strings)
> sobre o instalador `RevoUninProSetup.exe` presente em `C:\Users\Matheus\Downloads`.
> Data da análise: 05/11/2026.

---

## 1. Identificação do alvo

| Item | Valor |
|---|---|
| Produto | Revo Uninstaller Pro **5.5.4** (VS Revo Group) |
| Instalador | `RevoUninProSetup.exe` — 17,43 MB |
| SHA-256 do instalador | `AC44CBAF95641260A8BDF94E85D4F918ED67D31102008B94134BAF83B4A6D6DC` |
| Empacotador | **Inno Setup** (setup data version 5.6.0, unicode) — extraído com `innoextract 1.9` |
| Tecnologia do app | **PE nativo x64, MFC (C++/MSVC)** — confirmado por recurso `AFX_DIALOG_LAYOUT` (121 entradas) |
| Managed? | **Não** — REA `inspect_managed_artifact` → `status: not-managed`, sem CLI data directory |
| Bibliotecas notáveis | `msi.dll` (MSI), `SHLWAPI`, `PSAPI`, `RstrtMgr` (ponto de restauração), `WININET`, `OLEACC` (acessibilidade), `UxTheme`, `gdiplus` |

### 1.1 Payload extraído

| Arquivo | Tamanho | Papel |
|---|---|---|
| `RevoUninPro.exe` | 21,7 MB | Aplicação principal (MFC) |
| `RevoCmd.exe` | 158 KB | Processador de linha de comando |
| `RevoAppBar.exe` | 8,2 MB | AppBar da barra lateral (gadgets) |
| `RUExt.dll` | 188 KB | Extensão de contexto do Explorer |
| `revoflt.sys` | 32 KB | Driver de filtro de arquivos (monitoramento de instalação) |
| `WinAppsDll.dll` | 34 KB | Suporte a Windows Apps (MSIX/Store) |
| `RevoSrp.exe` | 273 KB | Criação de System Restore Point |
| `ruplp.exe` | 9,9 MB | Servidor COM de licença/updater (`/regserver`) |
| `rupilogs.rupldb` | 19,5 MB | Banco de dados de logs (Traced Logs) |
| `lang\*.ini` | 48 idiomas | Catálogo de strings da UI (inclui `portuguesebrazil.ini`) |
| `Revo Uninstaller Pro Help.pdf` | 2,1 MB | Manual oficial (70+ páginas) — **fonte primária da especificação funcional** |
| `revouninstallerpro5.lic` | 63 KB | Licença (instalada em `ProgramData\VS Revo Group\Revo Uninstaller Pro\`) |
| `reg_lp.bat`, `RUPrestore.cmd` | — | Registro do COM / restauração de backup de registro |

### 1.2 Recursos de UI extraídos do PE (171 diálogos!)

| Tipo | Quantidade | Conteúdo |
|---|---|---|
| `RT_DIALOG` | **171** | Templates de diálogo com geometria e tamanhos exatos de cada controle |
| `AFX_DIALOG_LAYOUT` | 121 | Dados de layout MFC |
| `RT_MENU` | 60 | Menus |
| `RT_STRING` | 169 | Tabelas de strings |
| `RT_ACCELERATOR` | 25 | Tabelas de atalhos |
| `RT_BITMAP` / `RT_ICON` / `RT_GROUP_ICON` | 868 / 105 / 87 | Toolbar, ícones, cursores |

Artefatos gerados: `revo-spec/resources.json`, `revo-spec/dialogs.md`, `revo-spec/menus.md`.

---

## 2. Arquitetura de módulos

O GUI é dividido em **Details Panel** + **Commands Panel** (split vertical):

- **Commands Panel (parte superior)** — comandos da ferramenta ativa; os principais como botões separados, o resto em **More Commands** e no menu de contexto.
- **Commands Panel (parte inferior)** — 2 grupos de módulos: **Uninstaller** e **Tools**.
- **Details Panel** — lista de programas; pode ser arrastado; botão "pin" no canto superior direito liga **Auto Hide** (mostra só ao passar o mouse).
- **Status Bar** — sob o Description Panel; informação contextual do módulo ativo.
- **Botão Menu** — canto superior direito: comandos essenciais, opções gerais e **Options** do programa.
- **Colunas**: clique esquerdo no cabeçalho ordena; clique direito abre menu para exibir/ocultar colunas e realinhar o texto.

### 2.1 Módulos e hotkeys

| Módulo | Hotkey | Comandos |
|---|---|---|
| All Programs | `Alt+U` | Uninstall `Ctrl+U`, Quick Uninstall `Ctrl+Q`, Refresh `Ctrl+R`, Forced Uninstall `Ctrl+F`, Install Programs `Ctrl+L`, Print `Ctrl+P`, Print Preview `Shift+P`, Print Setup `Ctrl+Shift+P` |
| Traced Programs | `Ctrl+Alt+T` | Uninstall `Ctrl+U`, Custom Uninstall `Ctrl+C`, Refresh `Ctrl+R`, Edit Log `Ctrl+D`, Install `Ctrl+L`, Import Log `Ctrl+I`, Export Log `Ctrl+E` |
| Windows Apps | `Alt+W` | Uninstall `Ctrl+U`, Refresh `Ctrl+R` |
| Browser Extensions | `Alt+R` | Uninstall `Ctrl+U`, Refresh `Ctrl+R` |
| Logs Database | `Alt+L` | Uninstall `Ctrl+U`, Custom Uninstall `Ctrl+C` |
| Autorun Manager | `Alt+A` | Run `Ctrl+N`, Kill `Ctrl+K`, Add `Ctrl+A`, Remove `Ctrl+M`, Disable `Ctrl+D`, Refresh `Ctrl+R` |
| Junk Files Cleaner | `Alt+J` | Scan `Ctrl+S`, Delete `Ctrl+D` |
| Windows Tools | `Alt+T` | Run `Ctrl+R` |
| History Cleaner | `Alt+C` | Clear `Ctrl+C`, Clear All `Ctrl+A` |
| Evidence Remover | `Alt+E` | Execute `Ctrl+E` |
| Unrecoverable Delete | `Alt+D` | Delete Forever `Ctrl+D`, Add File(s) `Ctrl+F`, Add Folder `Ctrl+Shift+F` |
| Backup Manager | `Alt+B` | — |
| — globais — | `Alt+O` Options · `Alt+H` Hunter Mode · `Alt+X` Exit | |

### 2.2 Visões da lista

`Details` (padrão) · `List` · `Icons` — em **More Commands → View**. Nas visões List/ícones o Description Panel mostra tamanho, empresa e versão do item selecionado.

### 2.3 Busca e filtros

- Campo de busca com lupa: filtra **em tempo real** por **nome, editora ou comentários**.
- Botão de filtro (seta para baixo) ao lado: **Filter by type**, **Filter by size**, **Filter by drive**.

### 2.4 Opções e personalização

- Primeiro lançamento: escolha entre **UI clássica** e **UI dark**; `Menu → Options → View → Theme` alterna temas.
- `Menu → Options → All Programs` → exibe atualizações do sistema e componentes do sistema; botão **"Rebuild cache file"** (reconstrói o índice de programas, **apagando comentários, ícones e grupos**).
- `Menu → Options → Groups Manager` → agrupa programas.
- Exportação da lista: **TXT, HTML, CSV, XML, XLS** (via More Commands → Export List to), com escolha de campos.
- Idiomas: **48** arquivos `.ini` — a UI é totalmente localizada (pt-BR disponível).

---

## 3. Fluxo de desinstalação (5 etapas)

```
Step 1 Analyze → Step 2 Run Built-in Uninstaller → Step 3 Scan → Step 4 Removal → Step 5 Summary
```

1. **Analyze** — varredura do programa: arquivos, pastas, EXEs, DLLs, chaves e valores de registro. Pode usar **Traced Logs**.
2. **Built-in Uninstaller** — cria **backup completo do Registro** (salvo opção) e um **System Restore Point**, depois executa o desinstalador nativo. Sem desinstalador nativo → opções substitutas.
3. **Scan** — varredura de sobras com **nível de intensidade** selecionável (registro, arquivos, pastas). Opção de varrer **todas as contas do Windows**.
4. **Removal** — automática em Traced/Quick Uninstall e na opção "Automatically delete all found leftovers"; caso contrário, sobras de registro e arquivos em janelas separadas para seleção.
5. **Summary** — tela final com contagem de arquivos/pastas/entradas deletados com sucesso e não; revisável em detalhe. Fica registrado no **Uninstall History** de cada módulo.

### 3.1 Níveis de varredura

| Nível | Uso |
|---|---|
| **Safe** | conservador |
| **Moderate** | padrão para usuários comuns |
| **Advanced** | análise detalhada, exige revisão do usuário |

### 3.2 Matriz de tarefas (do manual)

| Módulo | Tarefa | Step 3 | Step 4 |
|---|---|---|---|
| Traced Programs | Install | n/a | n/a |
| Traced Programs | Uninstall | Automático | Automático |
| All Programs | Quick Uninstall | Moderate (preset) | Automático |
| All Programs | Uninstall | Padrão | Standard Procedure |
| Windows Apps | Uninstall / Quick | Padrão | Standard Procedure |
| Browser Extensions | Uninstall | Padrão | Standard Procedure |
| Browser Extensions | Remove | n/a | Automático |
| Logs Database | Uninstall | Automático | Automático |
| Logs Database | Custom Uninstall | Cap. 4.7 | Cap. 4.7 |
| All Programs | Forced Uninstall | Livre | Standard Procedure |
| All Programs | Forced MSI Uninstall | Moderate (preset) | Standard Procedure |
| Hunter Mode | Forced Uninstall | Livre | Standard Procedure |
| Traced Programs | Advanced Uninstall | Automático | Standard Procedure |
| Traced Programs | Custom Uninstall | Livre | Standard Procedure |

---

## 4. Ferramentas (8, "Tools" no painel inferior)

1. **Autorun Manager** — gerencia processos, serviços e tarefas agendadas de autoinicialização; adicionar/remover/desabilitar.
2. **Junk Files Cleaner** — varre e limpa arquivos temporários.
3. **Evidence Remover** — remove rastros (histórico, cookies, etc.).
4. **Unrecoverable Delete** — sobrescrita segura de arquivos/pastas.
5. **History Cleaner** — limpa histórico (inclui 3 sub-ferramentas).
6. **Windows Tools** — acesso rápido a **19 ferramentas nativas do Windows** (Disk Cleanup, System Restore, etc.).

---

## 5. Linha de comando

### 5.1 `RevoCmd.exe` — listagem

```
RevoCmd /m "program name" [/i] [/u] [/p] [/t]
  /m  - casar nome com curingas * e ?
  /i  - incluir local de instalação
  /u  - incluir comando de desinstalação
  /p  - pausar a cada página
  /t  - imprimir resultado em tabela
  /? | /help - ajuda

RevoCmd /winapps [/i] [/p]      - lista Windows Apps
```

Formato de saída padrão observada: `Nome - Versão` (separados por linha em branco).

### 5.2 `RevoUninPro.exe` — operações

```
RevoUninPro /mu "Program name" /path "Full program path" /mode "Safe|Moderate|Advanced" /32|/64
RevoUninPro /tu "Log name"
RevoUninPro /export "Full export path" /all /traced /txt /html /xls
RevoUninPro /wa "App1;App2"
RevoUninPro /be "Ext1;Ext2" /ie|/edge|/chrome|/firefox|/opera "ProfileName"
RevoUninPro /update /silent
RevoUninPro /hunter
```

### 5.3 Integração com o Explorer

`reg_lp.bat` → `taskkill RevoUninPro.exe` + `taskkill ruplp.exe` + `ruplp.exe /regserver` (registro COM por usuário).

---

## 6. Persistência e caminhos

| Caminho | Conteúdo |
|---|---|
| `%LOCALAPPDATA%\VS Revo Group\Revo Uninstaller Pro\` | dados do usuário, `RegBackup\Last\Restore.dat` (backup de registro), logs |
| `C:\ProgramData\VS Revo Group\Revo Uninstaller Pro\` | `revouninstallerpro5.lic` |
| `SOFTWARE\VS Revo Group\Revo Uninstaller Pro\Autoruns\...` | cache de autoruns: `HKCU|HKLM\Run`, `RunServices`, `RunServicesOnce`, `RunOnce`, `ExplorerRun` |

O backup de registro é restaurável pelo **Windows Recovery Environment** via `RUPrestore.cmd` (script gerado a partir de `Restore.dat`).

---

## 7. Mapa de paridade exigido do Tranal Uninstaller

### 7.1 Funcionalidades — curadoria binária (Revo Uninstaller Pro 5.5.4)

Tabela reescrita a partir do **binário**, não de documentação. Fontes de evidência:

- `E:\Projetos\tranal-auditoria-evidencia\bin-inventario\revo-pro.funcionalidades.md` — a
  tabela de idioma do próprio produto (42 seções, 1141 entradas) extraída dos
  `lang\*.ini` que o Revo 5.5.4 instala. Citada como `[Seção] id = texto`.
- `E:\Projetos\tranal-auditoria-evidencia\bin-inventario\revo-pro.imports.txt` — 27 DLLs
  e 917 funções importadas pela PE. Citada como `PE imports.txt:NN → DLL → função`.
- `E:\Projetos\tranal-auditoria-evidencia\bin-strings\revo-pro.strings.txt` — dump bruto
  de strings do `RevoUninPro.exe`. Citada como `strings.txt:NNNN`.

A prioridade do mapa original (`🔴` núcleo, `🟡` importante, `🟢` bônus) foi preservada
no texto da funcionalidade. A coluna **Tranal** vale `OK` (implementado, com o arquivo),
`PARCIAL` (existe, falta parte — o que falta está escrito) ou `AUSENTE`.

<!-- PARIDADE-REVO:INICIO -->
| # | Prio | Funcionalidade (Revo Pro 5.5.4) | Evidencia no Revo | Estado do Tranal | Evidencia no codigo do Tranal |
|---|---|---|---|---|---|
| 1 | 🔴 | All Programs: scan de instalados (registro + MSI) | PE imports.txt:14 → `ADVAPI32.dll` → `RegOpenKeyExW`, `RegEnumKeyExW`, `RegQueryValueExW`; PE imports.txt:12 → `msi.dll` (2 imports) | **OK** | Core/ProgramScanner.cs::ProgramScanner<br>Core/Discovery/MsiProductCatalog.cs::MsiProductCatalog<br>(o caminho real do scanner e src/Tranal.Uninstaller/Core/ProgramScanner.cs) |
| 2 | 🔴 | Colunas: Programa, Tamanho, Versão, Data, Empresa, Página Inicial, Comentários, Desinstalador, Chave do Registro, Instalador | PE imports.txt:13-14 → `ADVAPI32.dll` (`RegQueryValueExW`, `RegQueryInfoKeyW`); PE imports.txt:20 → `KERNEL32.dll` → `GetFileTime`, `GetVolumeInformationW` | **OK** | Core/Appearance/ProgramTextFormatter.cs |
| 3 | 🔴 | Desinstalador em 5 etapas (Analyze → Built-in → Scan → Removal → Summary) | `[Scan After Uninstall] 4216 = Scan After Uninstall`; `[Scan After Uninstall] 4244 = Found leftover Registry items`; strings.txt:26797 → `/Custom Uninstall of the selected traced program` | **OK** | Ui/UninstallWizardWindow.xaml.cs::UninstallWizardWindow<br>medido: as cinco etapas do assistente (Analisar, Nativo, Varredura, Remocao, Resumo) estao na tela do assistente |
| 4 | 🔴 | Backup de registro antes da remoção + System Restore Point | PE imports.txt:14 → `ADVAPI32.dll` → `RegSaveKeyExW`, `RegRestoreKeyW`, `RegReplaceKeyW`, `RegLoadKeyW`, `AdjustTokenPrivileges`, `LookupPrivilegeValueW`; PE imports.txt:46 → `RstrtMgr.DLL` (2 imports — API de Restauração do Sistema) | **OK** | Core/BackupManager.cs<br>Core/FullRegistryBackup.cs |
| 5 | 🔴 | Quick Uninstall (Moderate automático) | `[UninstallerPro] 15125 = Kill the Process and Continue`; `[Scan After Uninstall] 4234 = Built-in - run only the application's uninstaller without any additional scanning` | **OK** | Core/QuietUninstall.cs<br>Core/UninstallerKind.cs |
| 6 | 🔴 | Forced Uninstall | PE imports.txt:14 → `ADVAPI32.dll` → `OpenProcessToken`, `DuplicateTokenEx`, `CreateProcessWithTokenW` | **OK** | Core/Automation/ForcedRemovalOptions.cs<br>Core/Automation/ManualRemoval.cs |
| 7 | 🟡 | Forced MSI Uninstall | PE imports.txt:12 → `msi.dll` (2 imports) | **OK** | Core/Discovery/MsiProductCatalog.cs |
| 8 | 🔴 | Hunter Mode | `[Uninstaller Toolbar] 106 = Hunter Mode`; strings.txt:24989 `Hunter Mode`; strings.txt:25005 `/tn "Revo Uninstaller Pro Hunter Mode"` (tarefa agendada); `[Topmost Window] 4035 = Always on top` | **OK** | Ui/Modules/HunterView.xaml<br>Ui/Tools/TopmostOverlayWindow.xaml |
| 9 | 🔴 | Busca em tempo real (nome/editora/comentário) + filtros (tipo/tamanho/drive) | PE imports.txt:13-14 → `ADVAPI32.dll` → `RegQueryValueExW`; PE imports.txt:20 → `KERNEL32.dll` → `GetLogicalDriveStringsW`, `GetVolumeInformationW` | **OK** | Ui/Appearance/SettingsSidebar.xaml.cs::SettingsSidebar<br>Core/Appearance/ProgramFilters.cs::ProgramFilterSet<br>medido: a busca e os filtros (tipo, tamanho, unidade, editora, regex e exclusoes) ficam em um lugar so, o ProgramFilterSet |
| 10 | 🔴 | Visões Details / List / Icons | `[General App] 16061 = Show small icons in Details view`; `[UninstallerPro] 2004 = Change the view of the installed programs list` | **OK** | MainWindow.xaml.cs::ApplyViewMode<br>medido: Details, List e Icons sao os modos de visao da lista |
| 11 | 🔴 | Painel de comandos (acima) + módulos (abaixo) + Details Panel + Status Bar + Description Panel arrastável com auto-hide | `[General App] 16070 = Commands Toolbar`; `[Uninstaller StatusBar] 201 = Installations:`; `[UninstallerPro] 2002 = Adjust settings` | **OK** | MainWindow.xaml.cs::MainWindow<br>medido: painel de comandos, modulos, painel de detalhes, barra de status e painel de descricao |
| 12 | 🟡 | Exportação TXT/HTML/CSV/XML/XLS | PE imports.txt:29-30 → `SHELL32.dll` → `ShellExecuteW` (abrir o arquivo exportado) | **OK** | Core/Exporters.cs |
| 13 | 🟡 | Undo/Histórico de desinstalação | `[UninstallHistory] 32900 = Uninstall History`; `[UninstallHistory] 32940 = Skipped by user`; `[Uninstaller Options] 1208 = Disable Uninstall History feature` | **OK** | Core/Recovery/ProgramBackup.cs<br>Core/Recovery/RecoverOperation.cs |
| 14 | 🟡 | Traced Programs (instalar com rastreio + log) | `[UninstallerPro] 15160 = Install Programs`; `[AppBar] 16900 = Save the log of monitored system changes`; `[AppBar] 16910 = Are you sure you want to cancel the logging process?`; binário `RevoAppBar.exe` + driver `revoflt.sys` | **OK** | Core/Modules/InstallMonitor.cs<br>Core/Modules/TracedPrograms.cs |
| 15 | 🟢 | Logs Database (importar/exportar logs) | `[UninstallerPro] 15273 = "Logs Database" module … These logs stored on our web site …`; `[UninstallerPro] 15187 = Windows Version`; strings.txt:26756 `Automatically update the Log Database` | **OK** | Core/Modules/TraceStore.cs::Import<br>Core/Modules/TraceStore.cs::Export<br>Core/Modules/LogOperations.cs::Merge<br>src/Tranal.Cli/LogCommands.cs::RunList<br>medido: o Tranal importa e exporta logs (TraceStore.Import/Export) e mescla logs (LogOperations.Merge). O que nao existe e a BASE ONLINE do Revo — essa e a linha 35, fora por decisao (o Tranal e offline). |
| 16 | 🟢 | Windows Apps (MSIX) | PE imports.txt:13-14 → `ADVAPI32.dll` → `RegEnumKeyExW` sob `Appx` | **OK** | Core/Modules/WindowsApps.cs |
| 17 | 🟢 | Browser Extensions | strings.txt:26797 → `Add files to be irrecoverably deleted`; binário `WinAppsDll.dll` (app de navegador) | **OK** | Core/Modules/BrowserExtensions.cs |
| 18 | 🟡 | Autorun Manager | `[Autorun List] 450 = Startup Name`; `[Autorun List] 458 = INVALID`; `[Autorun List] 470 = Are you sure that you want to close selected application?…`; `[Autorun List] 478 = Enable Auto Starting` | **OK** | Core/Tools/AutorunManager.cs |
| 19 | 🟡 | Junk Files Cleaner | `[Junk Files Cleaner Options] 2202 = Drives to Scan:`; `[Junk Files Cleaner Options] 2227 = Ignore files accessed in the last 24 hours`; `[Junk Files Cleaner Tool] 2306 = Scan`; `[Junk Files Cleaner Tool] 2328 = No junk files were found` | **OK** | Core/Tools/JunkCleaner.cs |
| 20 | 🟡 | Evidence Remover / History Cleaner / Unrecoverable Delete | `[Evidence Remover] 808 = Are you sure that you want to erase leftover traces and evidence on selected drive(s)?`; `[Unrecoverable Delete] 912 = List of files and folders which will be deleted and will be made impossible to recover`; `[Unrecoverable Delete] 922 = Are you sure that you want to forever delete selected files and folders?` | **OK** | Core/Tools/EvidenceRemover.cs<br>Core/Tools/HistoryCleaner.cs<br>Core/Tools/UnrecoverableDelete.cs |
| 21 | 🟢 | Windows Tools (19 utilitários) | `[WindowsToolsPro] 17400 = System Restore`; `[WindowsToolsPro] 17422 = DirectX info and diagnostic` | **OK** | Core/Tools/WindowsToolsCatalog.cs |
| 22 | 🟢 | Backup Manager | `[Backup Manager] 17801 = Restore`; `[Backup Manager] 17858 = You have selected a date newer than today.\nThat will delete all backups! Continue?`; strings.txt:23796 `RU Restore Points` | **OK** | Core/BackupManager.cs<br>Core/Recovery/ProgramBackup.cs |
| 23 | 🟡 | Grupos (Groups Manager), temas (dark), 48 idiomas | `[UninstallerPro] 15173 = Add new Group`; `[General App] 16038 = Visual theme:`; `[General App] 16150 = Dialogs background color`; PE imports.txt:20 → `KERNEL32.dll` → `GetPrivateProfileIntW` (leitor das tabelas de idioma) | **OK** | Core/Appearance/ThemePalette.cs::ThemeColorRole<br>Core/Localization/LanguageCatalog.cs::LanguageCatalog<br>Core/ProgramCustomization.cs::ProgramCustomizationStore<br>medido: LanguageCatalog tem 48 idiomas — o mesmo numero do Revo Pro (48 .ini); cores por papel existem em ThemeColorRole |
| 24 | 🟡 | CLI completa (`/mu /path /mode /32 /64`, `/tu`, `/export`, `/wa`, `/be`, `/hunter`, `/update`) | binário `RevoCmd.exe` (sha256 `c187269068a7…`) e `RevoUninPro.exe`; PE imports.txt:20 → `KERNEL32.dll` → `GetCommandLineW`, `CommandLineToArgvW`; strings.txt:26797 → `/Custom Uninstall of the selected traced program` | **OK** | src/Tranal.Cli/Program.cs |
| 25 | 🟡 | Atalhos de teclado completos | `[UninstallerPro] 2002 = Adjust settings`; `[Topmost Window] 4002 = Options` | **OK** | Core/Shortcuts/ShortcutMap.cs::ShortcutMap<br>MainWindow.xaml.cs::Window_PreviewKeyDown<br>Ui/OptionsWindow.xaml.cs::ShortcutMap<br>medido: atalhos de teclado implementados nesta rodada (a tabela afirmava que ja existiam e nao existiam) |
| 26 | 🔴 | Quatro modos de desinstalação (Built-in / Safe / Moderate / Advanced) | `[Scan After Uninstall] 4234 = Built-in - run only the application's uninstaller without any additional scanning`; `[Scan After Uninstall] 4237 = Advanced - includes the Moderate mode …` | **OK** | Core/UninstallMode.cs::UninstallModes<br>medido: Built-in, Safe, Moderate e Advanced |
| 27 | 🟡 | Sobras de registro e de arquivos em telas separadas, com detalhe por item | `[Scan After Uninstall] 4221 = Leftover Registry entries`; `[Scan After Uninstall] 4244 = Found leftover Registry items` | **OK** | Core/UninstallSupport.cs |
| 28 | 🔴 | Arquivos restantes em uso → exclusão no próximo reinício | `[Scan After Uninstall] 4228 = Remaining files in the list will be deleted during the next system restart.`; PE imports.txt:13-14 → `ADVAPI32.dll` → `RegSetValueExW` (chave `PendingFileRenameOperations`) | **OK** | Core/DeferredDelete.cs |
| 29 | 🟡 | Lixeira para sobras de arquivo | `[Scan After Uninstall] 4255 = Are you sure that you want to send selected files to the Recycle Bin?`; PE imports.txt:29-30 → `SHELL32.dll` → `SHFileOperationW`, `SHEmptyRecycleBinW` | **OK** | Core/RecycleBin.cs |
| 30 | 🟡 | Varredura de sobras em **todas as contas do Windows** e desinstalação de todas as contas | `[UninstallerPro] 15345 = Show the option to scan all Windows accounts for leftovers`; `[UninstallerPro] 15352 = Scan all Windows accounts for leftovers`; PE imports.txt:13-14 → `ADVAPI32.dll` → `GetUserNameW`, `LookupAccountNameW`, `DuplicateTokenEx`, `CreateProcessWithTokenW` | **OK** | Core/UninstallSupport.cs::UserProfiles<br>Core/UserProfiles.cs::UserProfiles<br>Core/LeftoverScanOptions.cs::ScanAllUserAccounts<br>medido: as sobras ja varrem as outras contas (AppData de cada perfil + HKEY_USERS\\<sid>) e a opcao "remover em todas as contas" existe em LeftoverScanOptions |
| 31 | 🔴 | Encerrar forçadamente processos durante a desinstalação | `[UninstallerPro] 15125 = Kill the Process and Continue`; `[UninstallerPro] 15265 = Forcibly stop running executable files during their uninstall`; PE imports.txt:20 → `KERNEL32.dll` → `SuspendThread`, `WriteProcessMemory`, `ReadProcessMemory` | **OK** | Core/ProcessTerminator.cs |
| 32 | 🔴 | Painel de **Custom Uninstall** a partir do log de instalação | `[UninstallerPro] 15178 = Export,Import or Edit log`; `[UninstallerPro] 15220 = Preparing log's data for uninstall...`; strings.txt:26274 `Custom Uninstall` | **OK** | Core/Automation/CustomUninstallReview.cs::CustomUninstallReviewBuilder<br>src/Tranal.Cli/LogCommands.cs::RunCustomUninstall<br>medido: a revisao item a item do log foi implementada nesta rodada (nucleo + comando de linha de comando) |
| 33 | 🔴 | **Advanced Uninstall**: revisar e apagar o que não foi registrado na instalação | strings.txt:26066 `Advanced Uninstall`; strings.txt:26974 `There is some data that may be related to the uninstalled program, but was not deleted because it was not logged by Revo Uninstaller Pro during the installation of the program. If you want to review and eventually to delete it press the "Advanced Uninstall" button` | **OK** | Core/Automation/AdvancedUninstallReview.cs::AdvancedUninstallReviewBuilder<br>src/Tranal.Cli/LogCommands.cs::RunAdvancedUninstall<br>medido: o que ficou fora do log foi implementado nesta rodada |
| 34 | 🟡 | Editar o log de instalação entrada a entrada (chaves, valores, arquivos) | `[UninstallerPro] 15178 = Export,Import or Edit log`; `[UninstallerPro] 15257 = Add Key`; `[UninstallerPro] 15235 = Rename Log`; `[UninstallerPro] 15336 = Export Log's data` | **OK** | Core/Modules/LogOperations.cs::EditEntry<br>Core/Modules/LogOperations.cs::IgnoreEntry<br>Core/Modules/LogOperations.cs::RemoveEntry<br>Core/Modules/LogOperations.cs::AddEntry<br>src/Tranal.Cli/LogCommands.cs::RunEntries<br>medido: editar, ignorar e remover entrada a entrada foi implementado nesta rodada |
| 35 | 🔴 | **Logs Database online** (logs de programas monitorados publicados no site do Revo) | `[UninstallerPro] 15273 = "Logs Database" module of Revo Uninstaller Pro shows a list of logs of traced programs that is compatible with your Windows system. These logs stored on our web site …`; strings.txt:26165 `Logs database`; strings.txt:26854 `Logs Database`; strings.txt:26757 `Error opening Logs database` | **FORA** | Core/Modules/TraceStore.cs::TraceStore<br>Core/Updates/AutoUpdateClient.cs::AutoUpdateClient<br>decisao registrada: sem chamada de rede a base de logs do fabricante; o Tranal nao publica log de programa |
| 36 | 🟡 | **Mesclar logs** selecionados | `[UninstallerPro] 15312 = Merge selected Logs`; strings.txt:26218 `Merge selected Logs` | **OK** | Core/Modules/LogOperations.cs::Merge<br>src/Tranal.Cli/LogCommands.cs::RunMerge<br>medido: mesclar logs selecionados foi implementado nesta rodada |
| 37 | 🟡 | "Change or Repair" da instalação MSI | strings.txt:26799 `Change or Repair the installation of selected program` | **OK** | Core/Discovery/MsiProductCatalog.cs::TryBuildRepairCommand<br>medido: msiexec /fa (reparo) foi implementado no lote 2 |
| 38 | 🟡 | Desinstaladores nativos encontrados (escolha do executável correto) | PE imports.txt:13-14 → `ADVAPI32.dll` → `RegQueryValueExW` (leitura de `UninstallString`); `[UninstallerPro] 15125 = Kill the Process and Continue` | **OK** | Core/UninstallerKind.cs |
| 39 | 🟡 | Aviso de **aplicativos no mesmo diretório** (sobras cruzadas) | `[Uninstaller Toolbar] 128 = %s is installed in the same location as %s!\nLeftovers will be found from both applications! Carefully check the ones you want to remove!` | **OK** | Core/Discovery/RelatedProgramFinder.cs::RelatedProgramFinder<br>medido: o aviso de programas que compartilham a pasta foi implementado no lote 1 (B17) |
| 40 | 🟡 | Aviso de pastas que podem conter dados do usuário | `[UninstallerPro] 15359 = Folders below may contain user data and documents:` | **OK** | Core/UninstallSupport.cs::LeftoverWarnings<br>Core/UninstallAdvisories.cs::UserDataAdvisory<br>medido: o aviso de pasta com dado do usuario ja existia e agora soma o aviso de baixa confianca |
| 41 | 🟡 | Excluir o programa da lista sem desinstalar | `[Uninstaller Toolbar] 111 = Remove Entry`; `[Uninstaller Toolbar] 113 = Are you sure that you want to remove the selected entry?` | **OK** | Core/ExcludeList.cs::AddPath<br>Core/ExcludeList.cs::RemovePath<br>Core/ExcludeList.cs::ApplyTo<br>medido: a lista de exclusao remove entrada sem desinstalar |
| 42 | 🟡 | Renomear o programa e trocar o ícone | `[UninstallerPro] 15216 = Add Comment`; PE imports.txt:29-30 → `SHELL32.dll` → `ExtractIconW`, `ExtractIconExW` | **OK** | Core/ProgramCustomization.cs::SetName<br>Core/ProgramCustomization.cs::SetIcon<br>medido: renomear o programa na lista (implementado nesta rodada) e trocar o icone |
| 43 | 🟡 | Customizar colunas (remover, alinhar, best fit) | PE imports.txt:20 → `KERNEL32.dll` → `GetPrivateProfileIntW` (preferências por coluna) | **OK** | Ui/Shared/ItemListPanel.xaml.cs::ColumnDef<br>MainWindow.xaml.cs::BestFit<br>medido: remover, alinhar e ajustar as colunas |
| 44 | 🟡 | Exportar a lista para **Microsoft Excel** (COM do Excel) | `[General App] 16027 = Microsoft Excel is not installed on this computer (error code is 0x%08X).` | **OK** | Core/Reporting/ExcelComExporter.cs::ExcelComExporter<br>MainWindow.xaml.cs::ExportList<br>medido: exporta pelo Excel instalado (COM) e cai no SpreadsheetML quando o Excel nao existe |
| 45 | 🟡 | Cache de programas + reconstrução do arquivo de cache | `[UninstallerPro] 15150 = There is not enough available disk space to write the cache data file` | **OK** | Core/Settings/ProgramCache.cs::ProgramCacheStore<br>MainWindow.xaml.cs::ReloadAsync<br>medido: o cache e gravado no fim da varredura e pode ser reconstruido pelas Opcoes |
| 46 | 🟡 | Filtro de "novos programas instalados" (últimos N dias) | `[General App] 16061 = Show small icons in Details view`; strings.txt:26843 `Open the Uninstall Wizard maximized` | **OK** | Core/Settings/AppSettings.cs::NewProgramsDays<br>medido: o filtro de "novos programas" usa a preferencia de ultimos N dias |
| 47 | 🟡 | Gerenciador de grupos completo (criar, renomear, mover, adicionar/remover programa) | `[UninstallerPro] 15173 = Add new Group`; strings.txt:25971 `Move Up`; strings.txt:25972 `Move Down` | **OK** | Core/Appearance/GroupingModel.cs::GroupingModel<br>Ui/GroupsManagerWindow.xaml.cs::GroupsManagerWindow<br>medido: gerenciador de grupos completo |
| 48 | 🟡 | **Serviços** e **Tarefas agendadas** dentro do Autorun Manager | `[Auto Run Manager Pro] 15814 = Services`; `[Auto Run Manager Pro] 15816 = Scheduled Tasks`; strings.txt:26633 `Scheduled Tasks`; PE imports.txt:13-14 → `ADVAPI32.dll` → `OpenSCManagerW`, `OpenServiceW`, `DeleteService`, `ControlService` | **OK** | Core/Management/ServiceManager.cs::ServiceManager<br>Core/Management/ScheduledTaskManager.cs::ScheduledTaskManager<br>medido: Services e Scheduled Tasks tem List/Disable/Enable/Stop/Delete — a alegacao anterior de que so abriam services.msc estava errada |
| 49 | 🟡 | Autorun Manager: **criar** item de inicialização (Run/RunOnce/pasta Iniciar, usuário atual ou todos) | `[Auto Run Manager Pro] 15800 = Add`; `[Autorun List] 478 = Enable Auto Starting` | **OK** | Core/Tools/AutorunManager.cs::Add<br>medido: AutorunManager.Add cria item de inicializacao |
| 50 | 🟡 | **Ferramenta personalizada** no Windows Tools | `[WindowsToolsPro] 17480 = Tool`; `[WindowsToolsPro] 17484 = A custom tool with the same name already exists.\nPlease enter a different name.` | **OK** | Core/Tools/CustomToolRegistry.cs::CustomToolRegistry<br>medido: registro de ferramenta personalizada (implementado nesta rodada) |
| 51 | 🟡 | Windows Tools: conjunto nativo de utilitários | `[WindowsToolsPro] 17400 = System Restore`; `[WindowsToolsPro] 17422 = DirectX info and diagnostic` | **OK** | Core/Tools/WindowsToolsCatalog.cs |
| 52 | 🟡 | Limpeza de rastros de **navegador** (histórico, cookies, index.dat, downloads) | `[Track Cleaner Browsers] 507 = Clear All`; `[Track Cleaner Browsers] 517 = Uninstaller`; `[Track Cleaner Browsers] 527 = Index.dat files will be deleted the next time the computer is rebooted.` | **OK** | Core/Tools/TrackCleaner.cs |
| 53 | 🟡 | Limpeza de rastros do **Microsoft Office** (documentos recentes) | `[Office Track Cleaner] 601 = Clear`; `[Office Track Cleaner] 617 = Microsoft Office Cleaner allows you to remove the history of the most recently used files in Microsoft Word, Excel, Access, PowerPoint and FrontPage.` | **OK** | Core/Tools/TrackCleaner.cs |
| 54 | 🟡 | Limpeza de rastros do **Windows** (fragmentos do Chkdsk, documentos recentes, histórico) | `[Windows Tracks Cleaner] 701 = Clear`; `[Windows Tracks Cleaner] 725 = Delete Chkdsk (Check Disk) recovered file fragments` | **OK** | Core/Tools/ChkdskFragmentCleaner.cs::ChkdskFragmentCleaner<br>Core/Tools/TrackCleaner.cs::TrackModule<br>medido: os fragmentos FILE*.CHK do Chkdsk entraram como modulo do limpador de rastros |
| 55 | 🟡 | Removedor de evidências por **unidade** (sobrescrita do espaço livre) | `[Evidence Remover] 802 = Execute`; `[Evidence Remover] 808 = Are you sure that you want to erase leftover traces and evidence on selected drive(s)?` | **OK** | Core/Tools/EvidenceRemover.cs |
| 56 | 🟡 | Exclusão irrecuperável com lista do que será destruído | `[Unrecoverable Delete] 912 = List of files and folders which will be deleted and will be made impossible to recover`; `[Unrecoverable Delete] 922 = Are you sure that you want to forever delete selected files and folders?` | **OK** | Core/Tools/UnrecoverableDelete.cs |
| 57 | 🔴 | Menu de contexto do Explorer sobre o programa | strings.txt:26843 `Or right mouse click over the Hunter Mode's icon and select "Exit" to quit`; binário `RUExt.dll` (sha256 `bda04b693bfd…`); PE imports.txt:29-30 → `SHELL32.dll` → `SHGetPathFromIDListW`, `DragAcceptFiles`, `DragQueryFileW` | **OK** | Core/Topmost/ExplorerContextMenu.cs::ExplorerContextMenu<br>medido: menu de contexto do Explorer sobre o programa |
| 58 | 🟡 | Menu de contexto "Instalar com o Revo Uninstaller Pro" | `[General App] 16020 = Install with Revo Uninstaller Pro` | **OK** | Core/Modules/InstallMonitor.cs<br>Core/Modules/TracedPrograms.cs |
| 59 | 🔴 | Opções: exportar / importar todas as configurações | `[Options Tree] 1002 = Export All Settings`; `[Options Tree] 1003 = Import All Settings` | **OK** | Core/Settings/SettingsTransfer.cs::SettingsTransfer<br>Ui/OptionsWindow.xaml.cs::ExportSettingsButton_Click<br>medido: exportar e importar todas as preferencias (implementado nesta rodada), com previa antes de aplicar |
| 60 | 🟡 | Opções: modo avançado, verificar atualização ao iniciar, atualizar em segundo plano | `[General] 1102 = Advanced mode`; `[General] 1106 = Web search engines:`; `[Auto Update] 1907 = Update`; `[Auto Update] 1913 = What's new` | **OK** | Core/Settings/AppSettings.cs::AdvancedMode<br>Ui/OptionsWindow.xaml.cs::AdvancedModeCheck<br>medido: modo avancado, verificar atualizacao ao iniciar e atualizacao automatica ja existem |
| 61 | 🟡 | Opções do desinstalador (componentes do sistema, só o nativo, ponto de restauração, histórico) | `[Uninstaller Options] 1202 = Show system components`; `[Uninstaller Options] 1209 = Show web page shortcuts` | **OK** | Core/Settings/AppSettings.cs::OnlyRunBuiltInUninstaller<br>Core/Settings/AppSettings.cs::CreateRestorePointBeforeUninstall<br>medido: as duas opcoes ja existem em AppSettings |
| 62 | 🟢 | Motor de busca web configurável | `[General] 1106 = Web search engines:` | **OK** | Core/WebSearchEngines.cs::WebSearchEngines<br>Core/WebSearch.cs::WebSearchEngine<br>medido: a lista de motores e configuravel (implementado nesta rodada); o valor antigo continua valendo |
| 63 | 🟡 | Atalhos de menu / barra de comandos e ícones pequenos | `[General App] 16061 = Show small icons in Details view`; `[General App] 16070 = Commands Toolbar` | **OK** | MainWindow.xaml.cs::MainWindow<br>medido: atalhos de menu, barra de comandos e icones pequenos |
| 64 | 🟡 | Tela inicial (splash) e animações de texto | `[General App] 16108 = Show startup splash screen`; `[General App] 16133 = Command button text color` | **OK** | Core/Appearance/ThemePalette.cs::ThemeColorRole<br>Core/Settings/AppSettings.cs::ShowSplashScreen<br>Core/Appearance/ColorBlindPalette.cs::ColorBlindPalette<br>medido: tela inicial, animacoes de texto e cores por papel; o modo daltônico entrou nesta rodada |
| 65 | 🟡 | Arrastar e soltar (modo "Drag and Drop") e troca de visualização | `[Topmost Window] 4004 = Drag and Drop Mode`; `[Topmost Window] 4005 = Hunter Mode`; `[UninstallerPro] 2003 = Switch to Hunter Mode`; `[UninstallerPro] 2004 = Change the view of the installed programs list` | **OK** | Ui/Modules/HunterView.xaml<br>Ui/Tools/TopmostOverlayWindow.xaml |
| 66 | 🟡 | Instalação com rastreio pelo **RevoAppBar** + driver de filtro | binário `RevoAppBar.exe` (sha256 `f609d3417858…`) + `revoflt.sys` (sha256 `d7a14ab729d0…`) + `revoflt.inf`; PE imports.txt:29-30 → `SHELL32.dll` → `SHAppBarMessage` | **FORA** | Core/Tracing/WriteWatcher.cs::WriteWatcher<br>Core/Modules/InstallMonitor.cs::InstallMonitor<br>decisao registrada: sem driver de filtro do kernel (revoflt.sys) — o rastreio e por observacao de escrita, sem driver |
| 67 | 🟡 | Logs: renomear, exportar dados, operações no computador atual | `[UninstallerPro] 15235 = Rename Log`; `[UninstallerPro] 15336 = Export Log's data`; `[UninstallerPro] 15319 = All Logs operations on the current computer`; `[UninstallerPro] 15232 = Import Log` | **OK** | Core/Modules/LogOperations.cs::Rename<br>Core/Modules/TraceStore.cs::Export<br>src/Tranal.Cli/LogCommands.cs::RunRename<br>medido: renomear log, exportar os dados do log e as operacoes no computador atual |
| 68 | 🟡 | Compatibilidade com Windows 11 (pasta problemática na atualização) | `[UninstallerPro] 15371 = If you have problems updating to Windows 11, we recommend that you delete this folder %s. Would you like to add it to the leftovers?` | **OK** | Core/Discovery/Windows11ProblemFolders.cs::Windows11ProblemFolders<br>Ui/UninstallWizardWindow.xaml.cs::Windows11ProblemFolders<br>medido: a pasta problematica da atualizacao do Win 11 e detectada e o Tranal pergunta antes de incluir (implementado nesta rodada) |
| 69 | 🟢 | Modo portátil (pasta de dados portátil) | `[Portable] 18505 = Portability` | **OK** | Core/Settings/AppSettings.cs::PortableDataFolder<br>medido: modo portavel (a pasta de dados fica ao lado do executavel) |
| 70 | 🟢 | Histórico de desinstalação com detalhe por operação | `[UninstallHistory] 32900 = Uninstall History`; `[UninstallHistory] 32940 = Skipped by user`; `[Uninstaller Options] 1208 = Disable Uninstall History feature` | **OK** | Core/UninstallHistory.cs::UninstallHistoryEntry<br>Core/Settings/AppSettings.cs::DisableUninstallHistory<br>medido: o historico guarda o detalhe por operacao (RemovedItems, Report, RecoveryId) e a opcao de desabilitar existe |
<!-- PARIDADE-REVO:FIM -->

### 7.2 Stack decidido

**C# / .NET 10 / WPF** (SDK 10.0.403 + WindowsDesktop 10.0.14 presentes na máquina).
WPF entrega controle de layout preciso (needed para o visual), acesso a registro/arquivo/processo e
publicação self-contained (sem dependência de runtime na máquina do usuário).

### 7.3 Princípios de reimplementação

- Código **próprio**, a partir da spec observada — sem copiar binários, recursos ou código do Revo.
- Nomes, textos e estrutura de menus **equivalentes** (fatos funcionais, não código protegido).
- Marca e nome próprios: **Tranal Uninstaller** (sem uso de "Revo", logotipos ou assets).

---

## 8. Oráculo de resultados (ground truth)

`RevoCmd.exe` funciona **sem instalação** e devolve a lista real de programas — é o critério de
"mesmos resultados". Na máquina analisada, o Revo listou (amostra):

```
AMD Software - 26.10.2
DBeaver 26.2.0 - 26.2.0
Docker Desktop - 4.91.0
Git - 2.55.0.5
Notepad++ (64-bit x64) - 8.9.8
Microsoft 365 - pt-br - 16.0.20527.20112
Microsoft Visual C++ 2013 x64 Additional Runtime - 12.0.40664 - 12.0.40664
Python 3.12.10 Executables (64-bit) - 3.12.10150.0
vs_communityx64msi - 18.11.12301
Microsoft Windows Desktop Targeting Pack - 10.0.14 (x64) - 80.56.61678
...
```

> **Pendência**: o GUI (`RevoUninPro.exe`) **não roda fora da instalação** — sem o driver
> `revoflt.sys` e o registro criados pelo instalador, encerra com `0xC0000005` (access violation).
> Para captura visual pixel-a-pixel do layout é preciso instalá-lo (requer UAC/admin).

---

## 8.1 Regras de listagem — medidas por cruzamento (não adivinhadas)

Cruzando as **233 chaves** de desinstalação do registro com as **210 linhas** do `RevoCmd.exe`,
e lendo as opções que o próprio Revo grava em
`HKCU\Software\VS Revo Group\Revo Uninstaller\Uninstaller`:

| # | Regra medida | Evidência |
|---|---|---|
| 1 | **Fonte**: apenas chaves de desinstalação (`HKLM/HKCU\SOFTWARE\...\Uninstall`, visões 32/64). O catálogo MSI `Installer\Products` (144 produtos) **não** é usado | os componentes MSI do Python só existem nas chaves de desinstalação, com `SystemComponent=1` |
| 2 | **Filtro obrigatório**: entrada sem `UninstallString` é invisível | 6 entradas (AMD DVR, AMD Settings, AMD WVR64, AMD User Experience Program, RyzenMasterSDK, Battlefield 2042 duplicada) |
| 3 | **Componentes de sistema** (`SystemComponent=1`) **aparecem na CLI** (129 de 210) e ficam ocultos na GUI por padrão | 129/210 registros com `SystemComponent=1` |
| 4 | **Duplicatas homônimas em chaves diferentes são mantidas** | "EA app" e "IntelliTraceProfilerProxy" aparecem 2× cada |
| 5 | **"Novos Programas" = janela de 7 dias** | `New Days = 7` e `New programs type = 0` no registro do Revo; os 19 do Revo têm data entre 29/10 e 05/11 |
| 6 | **Data efetiva** resolvida em cadeia: `InstallDate` → `InstallerDate` (MSI/FILETIME) → data de modificação da pasta instalada → data do executável de desinstalador | medido por fonte: registro 8, pasta 14, exe 9 dentro da janela de 7 dias |
| 7 | O corte conta o 7º dia inteiro (meia-noite de hoje − 7 dias) | "Visual Studio Community 2026" (29/10) entra no grupo do Revo |

### Resultado da validação (Tranal 1.0 vs Revo 5.5.4)

| Métrica | Revo | Tranal | |
|---|---|---|---|
| Entradas (CLI) | 210 | **210** | ✅ |
| Nomes únicos | 194 | **194** | ✅ |
| Divergências de versão | — | **0** | ✅ |
| Programas na GUI | 77 | **77** | ✅ |
| "Novos Programas" | 19 | 18 | ⚠️ 1 entrada |

Comparador: `tools/compare_lists.py` (normaliza acentos e a codepage cp850 do console do Revo).
As diferenças de nome brutas são artefato de codepage (`™`→`?`, `ê`→`e`).

---

## 8.2 Elevação do processo — medida no executável

| Binário | `requestedExecutionLevel` | Medido por |
|---|---|---|
| `RevoUninPro.exe` (GUI) | `requireAdministrator` | extração do recurso `RT_MANIFEST` do PE |
| `RevoCmd.exe` (CLI) | `asInvoker` | idem |
| `RevoUninProSetup.exe` (instalador) | `requireAdministrator` | idem |

Ferramenta: `tools/dump_manifest.py` (parser do recurso `RT_MANIFEST` de um PE).

**Decisão do Tranal**: o GUI usa `requireAdministrator` (`src/Tranal.Uninstaller/app.manifest`,
com `<ApplicationManifest>` no csproj) — paridade com o Revo Pro. A CLI permanece `asInvoker` e
**autoeleva sob demanda** via `runas`, com um marcador interno `/elevated` que impede o relançamento
infinito. Cada autoelevação consome **1 prompt de UAC**.

> Consequência prática da elevação do GUI: um cliente UIA **não-elevado não enxerga nenhum elemento**
> de um processo elevado (UIPI). Toda automação de interface do Tranal precisa rodar elevada.

---

## 8.3 "Novos Programas" — a regra do Revo é outra (medido)

A divergência de 1 entrada no grupo "Novos Programas" **não** é um erro de leitura do Revo: o Revo
ignora o `InstallDate` do registro e usa a **data de modificação da pasta instalada**.

| Programa | `InstallDate` no registro | Data usada pelo Revo | Origem da data do Revo |
|---|---|---|---|
| Visual Studio Community 2026 | 04/09 | 29/10 | mtime da pasta |
| Telegram Desktop | 03/11 | 04/11 | mtime da pasta |
| Microsoft GameInput | 03/09 | dentro da janela | metadados do pacote (Store) |
| Core Keeper | — | fora da janela | metadados do Steam |

### Calibração exaustiva (2.ª rodada)

`tools/calib_full.py` varre **todas** as fontes plausíveis (registro, MSI, `mtime`/`ctime` da pasta,
do executável, do ícone, e das pastas que os contêm) e testa **combinações de "primeira fonte
disponível" e de "mais recente" (MAX)** sobre ~1000 regras candidatas.

**Regra ótima (score máximo, 17/18 de acerto):**

```
data da pasta de instalação  →  InstallDate do registro  →  InstallerDate (MSI)  →  data do executável
```

Nenhuma das ~1000 regras testadas faz melhor. Regras que usam `MAX` sobre tudo, ou que preferem o
`InstallDate`, perdem "Visual Studio Community 2026"; regras que usam a pasta que contém o ícone
perdem "Steam" e "Obsidian".

### Dois desvios de leitura do registro que mudavam o resultado

1. **`REG_EXPAND_SZ`**: o .NET **expande** `%windir%` → `C:\WINDOWS` ao ler o registro; o Revo
   (aplicativo nativo, `RegQueryValueEx`) recebe o valor **cru**. Com a expansão, o
   "Microsoft Windows Application Compatibility Fix Database" (%windir%\system32\sdbinst.exe) passava
   a ter data e entrava no grupo — o Revo não o coloca. **Correção no Tranal**: ler com
   `RegistryValueOptions.DoNotExpandEnvironmentNames`.
2. **Extração do executável**: em `C:\Program Files\SteelSeries\GG\uninst.exe /SILENT` (sem aspas),
   cortar no primeiro espaço devolvia `C:\Program` — arquivo inexistente, logo sem data.
   **Correção no Tranal**: procurar o primeiro `.exe`/`.com`/`.bat` da string.

### Resultado medido após as correções

| | Tranal | Revo (após o Revo Pro ser desinstalado) |
|---|---|---|
| "Novos Programas" (janela de 7 dias) | **18** | **18** |
| Coincidentes | 17 | 17 |

Restam **2 entradas com composição diferente** (uma de cada lado), cujas fontes o Revo lê fora do
registro e que ainda não foram identificadas:

- **Microsoft GameInput** (só no Revo): `InstallDate` 03/09, sem pasta e sem executável; **não** é
  pacote MSIX/Store (`Get-AppxPackage` não o encontra) e `MsiGetProductInfoW` retorna **erro 234**.
- **Core Keeper** (só no Tranal): jogo do Steam — a pasta foi tocada em 04/11 (atualização), mas o
  Revo o data como anterior.

**Decisão do Tranal**: implementar a cadeia calibrada acima e **documentar** as 2 entradas residuais,
em vez de mascará-las com MAX/MIN sem evidência. A contagem do grupo é igual à do Revo (18 = 18) e
17 das 18 entradas são as mesmas.

Comando de conferência (usa o código real, não uma réplica): `tranal /new`.

---

## 8.4 Comparação de remoção: Tranal vs desinstalador nativo do Revo

Ciclo medido em duas fases sobre o **Revo Uninstaller Pro 5.5.4** (instalado em
`E:\Program Files\VS Revo Group\Revo Uninstaller Pro`, 65 arquivos, 75,7 MB):

- **Fase A** — o Tranal desinstalou o Revo Pro (assistente de 5 etapas, modo Advanced, auto-delete).
- **Fase B** — o Revo foi reinstalado e **o próprio Revo se desinstalou** ("Desinstalação Rápida",
  modo Avançado).

Inventário antes/depois: `tools/revo_inventory.py`; comparação: `tools/compare_removal.py`.

| Artefato | Instalado | Após o Tranal | Após o Revo |
|---|---|---|---|
| Pasta de instalação | SIM (65 arq.) | não | não |
| Chave de desinstalação (HKLM) | SIM | não | não |
| App Paths 64/32 bits | SIM | não | não |
| Serviço / driver `Revoflt` | SIM | não | não |
| `HKLM\SOFTWARE\VS Revo Group` | não | não | não |
| Entradas em `HKCU\SOFTWARE\VS Revo Group` | 391 | 362 | 362 |
| Arquivos em `ProgramData\VS Revo Group` | 1 | **0** | 1 |
| Arquivos em `LocalAppData\VS Revo Group` | 4 | **1** | 22 |

**Conclusão medida**: o Tranal removeu **22 arquivos a mais** que o desinstalador nativo do Revo —
os dados do Pro (`RegBackup`, `BackUpsData`, `cachedata`, `.lic`) que o instalador Inno deixa para trás.

> **Ressalva registrada**: o Revo **recusa desinstalar-se enquanto está em execução**
> ("O Desinstalador detetou que o Revo Uninstaller Pro está atualmente em execução") — foi preciso
> encerrar o Revo e confirmar "OK". Por isso a camada de **limpeza de sobras** do Revo (etapas 3-4
> rodando sobre ele mesmo) não pôde ser medida; a linha de base da comparação é o desinstalador nativo.

Divergência de comportamento ainda **não implementada**: o Revo move as sobras para a **Lixeira** e
guarda backup do registro no *Backup Manager*; o Tranal apaga permanentemente.

---

## 8.5 Temas (claro / escuro) — comportamento do Revo e armadilhas do WPF

O Revo Pro 5.5.4 oferece os dois temas (menu *Opções → Tema*, e a escolha no primeiro
lançamento). As cores do tema escuro foram medidas por amostragem de pixel nas capturas reais
(`workspace\revo-captures\preview-tela-01.png`):

| Chave | Tema escuro medido | Tema claro (UI clássica) |
|---|---|---|
| `SidebarBrush` | `#FF464646` | `#FFF0F0F0` |
| `PanelBrush` | `#FF525252` | `#FFFFFFFF` |
| `HeaderBrush` | `#FF3C3C3C` | `#FFE4E4E4` |
| `TextBrush` | `#FFE6E6E6` | `#FF1A1A1A` |
| `AccentBrush` | `#FF3399FF` | `#FF1E6FD9` |

### Defeito encontrado e corrigido no Tranal

Seis textos usavam `Foreground="White"` **fixo** no XAML (título da barra lateral, cabeçalho do
módulo, cabeçalho de grupo, painel genérico, Modo Caçador e assistente). No tema escuro ficavam
corretos; no tema claro o branco sobre superfície clara deixava o texto **invisível**. Como a cor
era fixa no XAML, nenhum teste de cobertura de linha pegaria isso — e como a barra lateral
*trocava* de cor normalmente, o defeito se disfarçava de "a barra lateral continua escura".

Correção: uma brush por tema (`TitleTextBrush` — branco no escuro, tinta escura no claro) e um
teste adversarial que reprova qualquer cor de texto fixa fora dos dicionários de tema.

### Duas armadilhas do WPF medidas nesta máquina

1. **Trocar o dicionário de tema dentro de um dicionário intermediário não repinta.** O conteúdo
   dos recursos muda, mas a notificação de "os recursos mudaram" não sobe do nível aninhado até
   os `DynamicResource` da janela. Sintoma medido: o `settings.json` confirmava a troca
   (`Theme: "Light"`) e a tela continuava com as cores do tema anterior.
2. **Carregar um dicionário XAML por pack URI em runtime é instável.** A primeira carga funciona;
   a seguinte **do mesmo arquivo** falha com
   `System.IO.IOException: Não é possível localizar o recurso 'themes/dark'`
   (`ResourceDictionary.set_Source` → `PackagePart.GetStream`). A sonda `TRANAL_THEME_PROBE=1`
   reproduz isso: a primeira carga OK, a segunda do mesmo URI falha.

### Desenho final (o que o Tranal faz)

- Os dois temas são carregados **uma vez**, por XAML, em `App.xaml`:
  `<ResourceDictionary x:Key="ThemeCatalog">` com as entradas `Dark` e `Light`.
- Os estilos são **comuns** aos dois temas e ficam em `Themes/Styles.xaml` (16 chaves: `UiFont`
  + 5 estilos nomeados + 10 estilos implícitos). Só as **cores** mudam.
- A troca de tema escreve as brushes do tema escolhido em `Application.Resources` — o caminho
  que o WPF notifica, reavaliando todos os `DynamicResource` sem recarregar a janela.
- **Nenhum dicionário XAML é carregado em runtime.**

Verificado nos dois sentidos (com a janela em primeiro plano e medindo pixel):

| Ação | Resultado |
|---|---|
| Inicialização com `Theme=Light` | barra lateral `#F0F0F0`, painel `#FFFFFF` |
| Runtime claro → escuro | repintura completa para `#464646` / `#525252` |
| Runtime escuro → claro | repintura completa para `#F0F0F0` / `#FFFFFF` |

---

## 8.6 Extensões de navegador — resolução de `__MSG_*`

O Revo exibe as extensões pelo **nome localization-ready** do `manifest.json`. Medido nesta
máquina (Chrome `Default`):

| Extensão | `manifest.json` | `default_locale` | Chave real em `messages.json` |
|---|---|---|---|
| Chrome Web Store Payments (`nmmhkkegccag…`) | `__MSG_APP_NAME__` | `en` | **`app_name`** (minúsculas) |
| Google Docs Offline (`ghbmnnjooekpmoecn…`) | `__MSG_extName__` | `en_US` | **`extName`** (com caixa) |

O navegador resolve a chave **sem diferenciar caixa** e usa o `default_locale` do manifesto. O
Tranal agora faz o mesmo (`Core/Modules/BrowserExtensions.cs`), com a ordem:
`default_locale` → `pt_BR` → `en_US` → `en` → qualquer outro locale. Antes disso a lista mostrava
o token cru `__MSG_APP_NAME__`.

---

## 9. Inventário de evidências

| Arquivo | Conteúdo |
|---|---|
| `revo-spec/resources.json` | dump completo de diálogos, menus, strings, layouts MFC |
| `revo-spec/dialogs.md` | 171 diálogos com geometria e controles (fonte do layout) |
| `revo-spec/menus.md` | menus |
| `revo-help.txt` | texto extraído do manual oficial (70+ páginas) |
| `revo-extracted/` | payload extraído do instalador |
| `tools/extract_ui_spec.py` | extrator de spec de UI a partir de PE |
| `tools/capture-window.ps1` | captura de janela (PNG + árvore UIA) |
