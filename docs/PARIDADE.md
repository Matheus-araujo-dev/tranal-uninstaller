# Mapa de paridade — Tranal Uninstaller Pro × Revo Uninstaller Pro 5.5.4

Este é o mapa canônico de cobertura. Cada linha corresponde a **uma seção do
catálogo de textos do produto original** (42 seções, 1136 strings, extraído do
`lang\english.ini` do payload do Revo Pro 5.5.4) e diz o que o Tranal faz
naquele ponto.

**Regra do documento:** "parcial" significa que existe, mas com menos
recursos que o original; "implementado" significa que cobre o comportamento
observável da seção. Nada aqui foi inferido: cada linha vem das strings do
original, dos imports do PE ou de medição no sistema.

Legenda: ✅ implementado · 🟡 parcial · ⬜ fora de escopo

---

## 1. Núcleo do desinstalador

| Seção do original | Strings | Tranal | Onde |
|---|---|---|---|
| `UninstallerPro` | 331 | 🟡 | `Ui/UninstallWizardWindow.*` — assistente de 5 etapas, 4 modos, lista de sobras com confiança, resumo por contagem |
| `Scan After Uninstall` | 63 | ✅ | `Core/UninstallSupport.cs` (`LeftoverScanner`), `Core/Removal.cs`, `Core/RecycleBin.cs`, `Core/DeferredDelete.cs` |
| `Uninstaller Options` | 6 | ✅ | `Ui/OptionsWindow.*` — ponto de restauração do sistema, somente o desinstalador nativo, desativar histórico, atalhos de páginas web |
| `Uninstaller List` | 10 | ✅ | `MainWindow.xaml` — lista com 9 colunas, grupos, ordenação |
| `Uninstaller Toolbar` | 29 | ✅ | `MainWindow.xaml` — barra de ferramentas e "Mais Comandos" |
| `Uninstaller StatusBar` | 2 | ✅ | `MainWindow.xaml` — barra de status com contagens |
| `UninstallHistory` | 33 | ✅ | `Core/UninstallHistory.cs` — histórico persistente, desativável pelas opções |

### Modos de desinstalação (medidos nas strings 4234–4237)

O original tem quatro modos. No Tranal eles são **um único enum**
(`UninstallMode`), que governa toda a operação:

| Modo | Original | Tranal |
|---|---|---|
| Built-in | "run only the application's uninstaller without any additional scanning" | `UninstallMode.BuiltIn` — não varre nada |
| Safe | "performs additional scans in the Registry and on the hard drive … the fastest mode" | `UninstallMode.Safe` |
| Moderate | "performs an extended scan … in the most common places" | `UninstallMode.Moderate` |
| Advanced | "performs a deep and thorough scan … the slowest mode" | `UninstallMode.Advanced` |

### Comportamento de cada sobra (medido nas strings 4255–4256 e 4228)

| Comportamento | Original | Tranal |
|---|---|---|
| Arquivos e pastas | "send selected files to the Recycle Bin" | `RecycleBin.Send` (`SHFileOperation`, a mesma API importada pelo original) |
| Chaves de registro | "delete selected registry items" | apagadas em definitivo |
| Item em uso | "will be deleted during the next system restart" | `DeferredDelete.Schedule` (`MoveFileEx` + `MOVEFILE_DELAY_UNTIL_REBOOT`) |
| Itens em negrito | "Only checked bolded items and their subitems will be deleted" | `LeftoverConfidence.High` é o único que vem marcado |

---

## 2. Gerenciador de Backups

| Seção do original | Strings | Tranal | Onde |
|---|---|---|---|
| `Backup Manager` | 48 | ✅ | `Core/BackupManager.cs`, `Ui/BackupManagerWindow.*` |

| Recurso | Original | Tranal |
|---|---|---|
| Ponto de restauração por operação | "stored … after every deleted registry entry, file or folder" | `BackupManager.Create` + `UninstallEngine.RemoveLeftovers` |
| Restaurar tudo | `Restore Full Registry Backup` | `BackupManager.Restore(point)` |
| Restauração item a item | `Custom Restore` | `BackupManager.Restore(point, selecionados)` |
| Excluir / Excluir todos | `Delete`, `Delete All` | `BackupManager.Delete`, `DeleteAll` |
| Excluir por tempo | `Delete by Time` (1 semana … 1 ano, data personalizada) | `BackupManager.DeleteOlderThan` |
| Erro de leitura do backup | "Error reading the backup data file!" | `BackupManager.Load` devolve `null` |

---

## 3. Ferramentas de limpeza de rastros

| Seção do original | Strings | Tranal | Onde |
|---|---|---|---|
| `Track Cleaner Browsers` | 20 | ✅ | `Core/Tools/TrackCleaner.cs` (módulo `Browsers`) |
| `Windows Tracks Cleaner` | 22 | ✅ | `Core/Tools/TrackCleaner.cs` (módulo `Windows`) |
| `Office Track Cleaner` | 12 | ✅ | `Core/Tools/TrackCleaner.cs` (módulo `Office`) |
| `EvidenceRemoverPro` / `Evidence Remover` | 18 | ✅ | `Core/Tools/EvidenceRemover.cs` |
| `Unrecoverable Delete` / `UnrecoverableDeletePro` | 18 | ✅ | `Core/Tools/UnrecoverableDelete.cs` |
| `Junk Files Cleaner Tool` / `JunkFilesCleanerPro` / `Junk Files Cleaner Options` | 36 | ✅ | `Core/Tools/JunkCleaner.cs` |
| `Context Menu Junk Files Cleaner` | 8 | 🟡 | a ferramenta existe; o item de menu no Explorer ainda não |
| `WindowsCleanerPro` | 1 | ✅ | cache de miniaturas — `TrackCleaner` (módulo `Windows`) |

Rastros cobertos (medidos nas strings 508–516 e 707–725):

| Rastro | Original | Tranal |
|---|---|---|
| Páginas visitadas | `Clear History of Visited Internet Web Pages` | `browsers-history` |
| Barra de endereços | `Delete Address Bar History` | `browsers-topsites`, `browsers-addressbar` |
| Temporários da internet | `Delete Temporary Internet Files` | `browsers-cache`, `browsers-codecache`, `browsers-gpucache`, `browsers-inetcache` |
| Cookies | `Delete Cookies` | `browsers-cookies` |
| Index.dat | `Delete Index.dat Files` (adiado para o reinício) | `browsers-indexdat` |
| Downloads | `Delete Download History` | `browsers-history` (mesmo banco SQLite) |
| Sessão | `Delete Session History` | `browsers-sessions` |
| Formulários | `Delete Form History` | `browsers-formhistory` |
| Documentos recentes | `Clear the Recent Documents History` | `windows-recentdocs` |
| Menu Iniciar (Executar) | `Clear the Start Menu Run History` | `windows-runmru` |
| Pesquisa de arquivos | `Clear the Find File History` | `windows-wordwheel` |
| Diálogos Abrir/Salvar | `Clear the Common Dialog Open/Save Recent History` | `windows-opensave` |
| Última pasta visitada | `Clear the Common Dialog Last Visited Folder History` | `windows-lastvisited` |
| Área de transferência | `Empty the Clipboard` | `clipboard` (via API do user32) |
| Miniaturas | `Delete Thumbnail Cache` | `windows-thumbnailcache` |
| Temporários do Windows | `Delete Windows Temporary Files` | `windows-temp`, `windows-usertemp` |
| Dumps de falha | `Delete crash memory dump files` | `windows-crashdumps`, `windows-minidump`, `windows-livekernel` |
| Fragmentos do CHKDSK | `Delete Chkdsk (Check Disk) recovered file fragments` | `windows-chkdsk` |
| Office (Word, Excel, Access, PowerPoint) | `Clear the Microsoft Word Recent Documents History` … | `office-mru` |

---

## 4. Ferramentas do painel

| Seção do original | Strings | Tranal | Onde |
|---|---|---|---|
| `Auto Run Manager Pro` / `Autorun List` / `Autorun Toolbar` | 53 | ✅ | `Core/Tools/AutorunManager.cs` — 346 itens reais nesta máquina |
| `WindowsToolsPro` / `WinTools` | 53 | ✅ | `Core/Tools/WindowsToolsCatalog.cs` |
| `Outlook Control` | 11 | 🟡 | limpeza de rastros coberta; o painel de controle do Outlook não é separado |
| `Registry Optimizer` | 2 | ✅ | `Core/Tools/RegistryOptimizer.cs` — "Select where to scan: Start menu" |

---

## 5. Janela Principal Flutuante e menu de contexto

| Seção do original | Strings | Tranal | Onde |
|---|---|---|---|
| `Topmost Window` | 34 | ✅ | `Core/Topmost/TopmostTargetResolver.cs`, `Ui/Tools/TopmostOverlayWindow.*` |
| `ContextMenu Uninstaller` | 9 | ✅ | `Core/Topmost/ExplorerContextMenu.cs` |

Ações do overlay medidas nas strings 4011–4035: `EXIT`, `Uninstall`, `Stop Auto
Starting`, `Kill Process`, `Kill and Delete Process`, `Open Containing Folder`,
`Info`, `Properties`, tamanho (Small/Middle/Large), `Transparency`, `Always on
top`, `Drag and Drop Mode`, `Hunter Mode`, `Open Main Window`.

---

## 6. Módulos do painel

| Seção do original | Strings | Tranal | Onde |
|---|---|---|---|
| `Windows Apps` | 20 | ✅ | `Core/Modules/WindowsApps.cs` — 122 pacotes MSIX reais |
| `Browser Extensions` | 23 | ✅ | `Core/Modules/BrowserExtensions.cs` — Chrome/Edge/Firefox/Opera, `__MSG_*` resolvido |
| `General App` | 147 | 🟡 | opções gerais, temas, idiomas, filtros — sem o painel de "favoritos"/personalização completo |
| `Common` | 31 | ✅ | dicionário e rotulos compartilhados |

---

## 7. Ferramentas adicionais

| Seção do original | Strings | Tranal | Onde |
|---|---|---|---|
| `AppBar` | 11 | ✅ | `Core/Modules/TraceStore.cs` + `TracedPrograms.cs` + `Core/Modules/InstallMonitor.cs` — log de mudanças monitoradas |
| `Portable` | 11 | ✅ | `Core/PortableMode.cs` — pasta de dados própria + carregamento rápido |
| `Auto Update` | 19 | ✅ | `Core/Updates/AutoUpdateClient.cs` |
| `General` | 6 | ✅ | modo avançado, busca na web, atualização automática — `Core/Settings/AppSettings.cs` |
| `About` | 1 | ✅ | `MainWindow.xaml` |

---

## 8. Fora de escopo (decidido, não esquecido)

| Item | Motivo |
|---|---|
| **Driver de kernel** (`revoflt.sys`) | o original usa `DeviceIoControl` para interceptar o sistema de arquivos. O Tranal usa as APIs do Windows (`SHFileOperation`, `MoveFileEx`, WMI `SystemRestore`) e não carrega driver próprio — menos risco e nenhuma assinatura de driver. |
| **Remoção definitiva de drivers** | não há no Tranal; drivers instalados saem pelo desinstalador nativo do próprio fabricante. |

---

## 9. Como este mapa é mantido

- Toda linha nasce de uma medição: strings do original, imports do PE, ou
  comparação de execução.
- Ao implementar algo, a linha muda **neste arquivo** e no `NOVIDADES.md`.
- O inventário bruto de strings **não** é versionado: é material de análise.
  O que fica no repositório é o fato derivado.
