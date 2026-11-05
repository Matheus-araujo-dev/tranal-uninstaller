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

### Lote 1 — segurança e alcance da desinstalação (strings 15102, 15125–15126, 15162–15166, 15265, 15307–15309, 15315–15317, 15345, 15352–15353)

Funcionalidades levantadas no inventário do original e levadas ao Tranal:

| Recurso do original | Strings | Tranal | Onde |
|---|---|---|---|
| "Add to Exclude List" | 15102 | "Adicionar à lista de exclusões" no menu de contexto da lista de sobras | `Core/ExcludeList.cs`, `Ui/UninstallWizardWindow.*` |
| "Files and Folders to Exclude" / "Folder to exclude" | 15315–15316 | Gerenciador de exclusões: pastas e arquivos protegidos | `Ui/ExcludeListWindow.*` |
| "Registry Keys to Exclude" | 15317 | Gerenciador de exclusões: chaves de registro protegidas | idem |
| "Forcibly stop running executable files during their uninstall" | 15265 | opção nas Opções → Desinstalador | `Core/ProcessTerminator.cs`, `Core/Settings/AppSettings.cs` |
| "Kill the Process and Continue" / "Selected file is a running process!…" | 15125–15126 | os executáveis do programa são encerrados antes da remoção, quando a opção está ligada | `UninstallEngine.RemoveLeftovers` |
| "Make a System Restore Point and a full Registry backup before uninstalling" | 15162 | as duas marcas na etapa de opções do assistente | `Ui/UninstallWizardWindow.*` |
| "Create a full Registry Backup before uninstall" / "Creating a full Registry backup…" | 15307, 15163, 15165–15166 | backup de todos os hives em `.reg` + script de restauração, feito **antes do desinstalador nativo** | `Core/FullRegistryBackup.cs` |
| "Show the option to scan all Windows accounts for leftovers" | 15345 | interruptor mestre nas Opções → Desinstalador | `AppSettings.ShowScanAllAccountsOption` |
| "Scan all Windows accounts for leftovers" | 15352 | varre `HKEY_USERS\<SID>` e o `AppData` dos outros perfis | `Core/UserProfiles.cs`, `Core/LeftoverScanOptions.cs` |
| "Uninstall from all Windows accounts" | 15353 | os itens das outras contas só vêm marcados quando esta opção está ligada | `LeftoverScanOptions.SelectOtherAccountItems` |

**A lista de exclusões é a regra que realmente protege.** O item excluído
**não aparece** na varredura e o motor **recusa removê-lo** mesmo que ele
chegue marcado por outro caminho (`UninstallEngine.RemoveOne`). O backup feito
antes da remoção também pula os itens excluídos — um backup não pode guardar
justamente o que o usuário mandou proteger. O encerramento de processos também
respecta a lista: encerrar o processo de um item protegido seria tocar no item
protegido tanto quanto apagar a pasta. E excluir um **arquivo** dentro de uma
pasta protege o arquivo mesmo quando a pasta é removida: a remoção recursiva
cega levaria o arquivo junto, então o conteúdo sai item a item e a pasta
permanece. O casamento é por prefixo de pasta/chave, e `HKLM\…` casa com
`HKEY_LOCAL_MACHINE\…`.

**O backup completo do registro vem antes do desinstalador nativo.** O
original diz "Make a System Restore Point and a full Registry backup **before
uninstalling**" (15162). Depois do desinstalador nativo, as chaves do próprio
programa já foram apagadas e o `.reg` resultante não serviria para devolver
nada — por isso o backup é preparado por quem conduz a operação (o assistente
e a CLI), e o motor de remoção apenas o carrega no relatório.

**As contas do Windows têm duas portas, não uma.** "Procurar em todas as
contas" revela os itens das outras contas, mas eles **não vêm marcados**;
só "Desinstalar de todas as contas" os marca. A opção "marcar todas as
sobras" **não** marca itens de outra conta: ler "marcar tudo" como "remover
também os dados dos outros usuários da máquina" seria uma leitura perigosa
demais.

**O backup completo do registro usa o `reg.exe` do Windows**, e não uma
reescrita própria do formato `.reg`: é o caminho nativo, preserva todos os
tipos de valor (inclusive `REG_BINARY`, `REG_QWORD` e `REG_MULTI_SZ`) e não
depende de o Tranal saber reescrever cada tipo sem erro — que é exatamente o
tipo de bug que torna um backup inutilizável. O backup só é considerado
válido quando **todos** os hives foram exportados.

### Atalhos: o motor nunca atravessa um link

Medido nesta máquina (.NET 10, `mklink /J`):

| Comportamento do .NET | Consequência |
|---|---|
| `Directory.EnumerateFiles(pasta, "*", AllDirectories)` **atravessa** o junction | os arquivos do alvo aparecem como se fossem da pasta |
| `Directory.Delete(pasta, true)` **falha** com `UnauthorizedAccessException` ao chegar no junction | não apaga o alvo, mas deixa a pasta pela metade e o erro não fala do atalho |
| `Directory.Delete(link, false)` apaga **só** o link | o alvo fica intacto |

O alvo de um atalho está **fora** da pasta que o usuário mandou remover.
Apagar arquivo alheio seria perda de dados não pedida — e o Tranal não faz
isso. A regra está em um lugar só (`Core/SafeTraversal.cs`) e vale para:

| Onde | Antes | Agora |
|---|---|---|
| Remoção (pasta inteira) | `Directory.Delete(pasta, true)` — falhava no atalho e deixava a pasta pela metade | percorre a árvore sem atravessar atalho; o link sai como link |
| Remoção (item a item, lista de exclusões) | **atravessava o atalho e apagava o alvo** | o link sai como link; o alvo fica intacto |
| O próprio item de remoção é um atalho | enumerava a raiz e apagava o alvo | remove-se o link e nada mais |
| Contagem de tamanho de pasta | `SearchOption.AllDirectories` — atravessava | `SafeTraversal.MeasureSize` |
| Cópia do backup | recursiva sem guarda — copiava o alvo para dentro do backup | o alvo não entra no backup |
| Varredura do limpador de arquivos temporários | `AllDirectories` — listava (e removeria) o alvo | `SafeTraversal.EnumerateFilesDeep` |

### O relatório não esconde nada

Uma pasta com item protegido é **um** item na lista: a pasta sai do lugar do
usuário, mas o conteúdo sai item a item. O relatório mostra os dois lados:

- `Removido dentro` — quantos itens saíram de dentro da pasta;
- `Protegido dentro`, `Falha dentro`, `Adiado dentro` — quantos ficaram;
- e o item travado aparece no texto (`permaneceu: …`), não é engolido em
  silêncio.

O item em uso é contado **e** agendado para o próximo reinício, como a
regra do projeto exige: `Item em uso → adiado para o próximo reinício`.

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
| `Context Menu Junk Files Cleaner` | 8 | ✅ | `Ui/Shared/ItemListPanel.xaml.cs` — menu de contexto da lista das ferramentas: marcar/desmarcar todos, marcar/desmarcar os selecionados, abrir a pasta que contém, propriedades e "pesquisar {termo} na web" |
| `WindowsCleanerPro` | 1 | ✅ | cache de miniaturas — `TrackCleaner` (módulo `Windows`) |

Rastros cobertos (medidos nas strings 508–516 e 707–725):

| Rastro | Original | Tranal |
|---|---|---|
| Páginas visitadas | `Clear History of Visited Internet Web Pages` | `browsers-history` |
| Barra de endereços | `Delete Address Bar History` | `browsers-topsites` |
| Histórico de impressoras/computadores/pessoas | `Clear the Printers, Computers and People Find History` | `windows-typedpaths` |
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
| Esvaziar a Lixeira | `Empty the Recycle Bin` | `windows-recyclebin` (via `SHEmptyRecycleBin`) |
| Última chave do Regedit | `Clear the Regedit Last Opened Key History` | `windows-regedit-lastkey` |
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
| `Outlook Control` | 11 | ✅ | árvore de ferramentas da barra lateral (`Core/ToolCatalog.cs`): Otimização → Otimizador de Registro, Tracks Cleaner → Navegadores / Microsoft Office / Windows, mais Autorun, Evidências, Exclusão irrecuperável, Ferramentas do Windows e Arquivos temporários |
| `Registry Optimizer` | 2 | ✅ | `Core/Tools/RegistryOptimizer.cs` — "Select where to scan: Start menu" |

---

## 5. Janela Principal Flutuante e menu de contexto

| Seção do original | Strings | Tranal | Onde |
|---|---|---|---|
| `Topmost Window` | 34 | ✅ | `Core/Topmost/TopmostTargetResolver.cs`, `Ui/Tools/TopmostOverlayWindow.*` |
| `ContextMenu Uninstaller` | 9 | ✅ | `Core/Topmost/ExplorerContextMenu.cs` — a entrada é registrada de verdade em `HKCU\Software\Classes\*\shell`, e a preferência "Mostrar comandos no menu de contexto do Windows" liga e desliga na hora |

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
| `General App` | 147 | 🟡 | personalização do layout (visibilidade de painéis + "Restaurar layout padrão"), marcar todas as sobras por padrão, ocultar mensagens de informação/aviso, menu de contexto do Windows e apagar o log do programa monitorado — `Core/Settings/AppSettings.cs` e `Ui/OptionsWindow.*`. **Falta**: a personalização fina de barras de ferramentas (arrastar/reordenar), a cor própria dos diálogos e a tela de ativação, que não se aplicam a uma reimplementação independente |
| `Common` | 31 | ✅ | dicionário e rotulos compartilhados |

### Opções do `General App` traduzidas para o Tranal

Medidas nas strings 16004–16009 (visibilidade de painéis), 16031–16032 (mensagens),
16039–16041 (exibição), 16059–16063 (comportamento) e 16104–16106 (restaurar layout).

| Opção do original | Tranal | Onde |
|---|---|---|
| "View → Menu Bar / Main Toolbar / Description Panel / Status Bar / Tools Toolbar" | submenu **Painéis** com quatro marcas + "Restaurar layout padrão" | `MainWindow.xaml.cs` (`BuildPanelsMenu`, `ApplyLayout`, `ResetLayout`) |
| "Reset main window panels settings" / "Reset successful!" | "Restaurar layout padrão" | idem |
| "Check mark all leftovers by default" | "Marcar todas as sobras por padrão" | `LeftoverScanner.Scan(..., checkAllLeftovers)` |
| "Skip information messages" / "Skip warning messages" | "Ocultar mensagens de informação / de aviso" | `Ui/UserMessages.cs` |
| "Show commands in Windows context menu" | "Mostrar comandos no menu de contexto do Windows" (opt-in) | `Core/Topmost/ExplorerContextMenu.cs` (`Apply`) |
| "Delete traced program log after uninstall" | "Apagar o log do programa monitorado após a desinstalação" | `Ui/UninstallWizardWindow.xaml.cs` |
| "Export All Settings" / "Import All Settings" | persistência em `settings.json` | `Core/Settings/AppSettings.cs` |
| "Create daily full Registry backup" | ⬜ fora de escopo: exige um serviço/agendador do Windows | — |

A confirmação de uma ação destrutiva **nunca** é omitida: as opções de
mensagem do original tratam só das mensagens informativas e de aviso.

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
| **Resolver o programa a partir de um arquivo solto** | o Revo, ao apontar para um executável, descobre de qual programa ele é e remove a pasta de instalação desse programa. O Tranal não faz essa correlação: a desinstalação forçada de um arquivo remove **o arquivo** e mostra a pasta na lista, sem marcá-la. Inferir a pasta a partir do diretório do arquivo seria perigoso — apontar para `%LOCALAPPDATA%\Temp\app.exe` removeria a pasta `Temp` inteira. A pasta de instalação continua sendo usada no assistente, onde ela é conhecida de verdade (o registro manda). |

---

## 9. Como este mapa é mantido

- Toda linha nasce de uma medição: strings do original, imports do PE, ou
  comparação de execução.
- Ao implementar algo, a linha muda **neste arquivo** e no `NOVIDADES.md`.
- O inventário bruto de strings **não** é versionado: é material de análise.
  O que fica no repositório é o fato derivado.

### Estado medido na última rodada

| Medida | Valor |
|---|---|
| Build | 0 erros, 0 avisos |
| Suíte de testes | **211 verdes**, 0 falhas |
| Catálogos de idioma | **546 chaves** em pt-BR e en-US, em paridade |
| Programas listados | 76 (18 em "Novos Programas", 58 em "Outros Programas") |
| Varredura completa | 150 ms |
| Ambiente do usuário | intocado pela suíte (comparação byte a byte antes/depois) |
