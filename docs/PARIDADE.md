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
| `Uninstaller List` | 10 | ✅ | `MainWindow.xaml` — lista montada a partir do catálogo de colunas (`Core/ProgramColumns.cs`), grupos, ordenação por cabeçalho |
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

### Lote 2 — colunas, grupos, comentários e ícones (strings 15001, 15054–15060, 15066, 15070–15072, 15079–15081, 15173, 15216–15217, 15239–15240, 15281–15285, 15298, 15360–15364, 15366)

| Recurso do original | Strings | Tranal | Onde |
|---|---|---|---|
| "Columns" | 15054 | janela de escolha das colunas da lista (a coluna do nome é fixa) | `Ui/ColumnsWindow.*`, `Core/ProgramColumns.cs` |
| "Remove This Column" | 15055 | "Remover esta coluna" no menu do cabeçalho | `MainWindow.xaml.cs` (`HideColumn`) |
| "Alignment" / "Left" / "Center" / "Right" / "Best Fit" | 15056–15060 | submenu do cabeçalho; "Ajustar ao conteúdo" mede o maior texto da coluna | `MainWindow.xaml.cs` (`SetAlignment`, `BestFit`) |
| "Select the columns you want to export:" | 15066 | a mesma janela de colunas, no modo exportação — e, na CLI, `/export "caminho" /columns "Program,Version"` | `Ui/ColumnsWindow.*`, `Core/ProgramColumns.cs` |
| "You must select at least one column to export!" | 15298 | a exportação não acontece sem uma coluna | `ColumnsWindow.OkButton_Click` |
| "Group name:" / "Add" / "Group" | 15070–15072 | gerenciador de grupos + coluna "Grupo" | `Ui/GroupsManagerWindow.*`, `Core/ProgramCustomization.cs` |
| "Groups Manager" | 15001 | "Gerenciador de Grupos" no menu "Mais Comandos" | `MainWindow.xaml.cs` |
| "Add new Group" | 15173 | botão "Adicionar" do gerenciador | `ProgramCustomizationStore.CreateGroup` |
| "Change Icon" / "Choose Icon" / "Select an icon from the list:" / "File path:" / "Icon index" | 15079, 15282, 15284, 15283, 15240 | galeria com os ícones que já existem na máquina + caminho de arquivo | `Ui/IconPickerWindow.*` |
| "Add Comment" / "Edit Comment" / "Enter your comment:" | 15216–15217, 15281 | menu de contexto do programa + coluna "Anotação" | `Ui/CommentWindow.*` |
| "Add to Group" / "Remove from Group" | 15080–15081 | submenus do menu de contexto do programa | `MainWindow.xaml.cs` |
| "Hidden" | 15362 | interruptor no popup de filtros, sobre a mesma preferência das Opções | `AppSettings.ShowSystemComponents` |
| "Do you want to keep your preferences? Icons, comments, groups, etc." | 15364 | as preferências ficam em disco; a pergunta nunca é feita | `Core/ProgramCustomization.cs` (`program-data.json`) |

**O catálogo de colunas é um só.** `Core/ProgramColumns.cs` é a única fonte que
sabe quais colunas existem, como elas se chamam na tela (traduzido) e como se
chamam no arquivo exportado (fixo, para o arquivo continuar legível por
qualquer ferramenta). A lista da tela e a exportação consomem essa fonte — não
existe uma segunda lista de colunas em cada um dos dois lugares, e o valor de
uma coluna na tela é o mesmo que vai para o arquivo: as duas coisas passam por
`ProgramColumns.Value`.

**Renomear um grupo leva os programas junto.** Um grupo renomeado que deixasse
os programas órfãos seria uma perda de informação; apagar um grupo, pelo
contrário, **não** apaga programa nenhum — o grupo é classificação, não
desinstalação.

**A anotação do usuário é uma coluna própria.** Os "Comentários" do instalador
(vindos do registro) e a anotação do usuário são colunas diferentes ("Comentários"
e "Anotação"): o Revo não tem como verificável um sobrescrita do comentário do
instalador.

**A galeria de ícones usa o que a máquina já tem.** Os ícones dos programas
instalados e o caminho de um arquivo — copiar uma galeria de terceiros seria
distribuir material de terceiros.

**O filtro "Hidden" é a mesma preferência das Opções.** O interruptor do popup de
filtros e a caixa "Mostrar componentes do sistema" das Opções mexem no mesmo
`AppSettings.ShowSystemComponents`: uma regra, dois lugares para chegar nela.

**O conjunto padrão da exportação é fixo.** A exportação usa, quando o usuário não
escolhe colunas, o conjunto do original — nome, versão, tipo, data, empresa,
página inicial, comentários, comando de desinstalação e chave do registro. Ele
não é o catálogo inteiro: o formato do arquivo exportado não pode mudar quando o
usuário mexe nas colunas da tela. Na CLI, `/export "caminho" /columns
"Program,Version,Note"` escolhe as colunas; os nomes aceitos são os do arquivo
exportado (fixos, independentes do idioma), e é por isso que o comando é
roteirizável.

**O CSV e o XML usam o nome fixo da coluna; o HTML usa o título traduzido.** O
CSV e o XML são lidos por ferramenta — o nome não pode mudar com o idioma. O
HTML é lido por gente.

### Partida a frio (medido)

O primeiro arranque — sem `settings.json` — é um caminho que o Revo também
faz, e no Tranal ele **não funcionava**. Duas falhas de ordenação, uma atrás
da outra, impediam o aplicativo de abrir:

| Defeito | Sintoma | Correção |
|---|---|---|
| O `Checked` do botão de tema dispara durante o `InitializeComponent` | `NullReferenceException` no arranque | Guarda no handler (`OkButton is null`) |
| O diálogo de primeiro lançamento apontava como `Owner` uma janela que ainda não tinha sido exibida | `InvalidOperationException` — o WPF recusa | O diálogo abre sem `Owner`, centralizado na tela |
| Durante a pergunta de tema o diálogo é a única janela do app | O app terminaria no fim da pergunta (`ShutdownMode`) | `OnExplicitShutdown` durante a pergunta, restaurado depois |

O tema continua sendo aplicado **antes** da primeira pintura da janela
principal: a escolha não gera "flash".

**O primeiro arranque é conferido de ponta a ponta**, e não só por leitura de
fonte: `tools/test-cold-start.ps1` sobe o aplicativo de verdade sem o arquivo
de preferências, confere o diálogo de tema, a gravação da preferência e a
janela principal, e devolve as preferências de quem rodou o teste.

### Integridade das preferências por programa (medido)

O `program-data.json` é o arquivo onde ficam os comentários, os ícones e os
grupos do usuário. Perder esse arquivo é perder trabalho dele, e por isso as
garantias são explícitas:

| Situação | Comportamento |
|---|---|
| Arquivo ausente | Estado normal de primeira execução |
| Arquivo ilegível (JSON inválido) | Preservado como `program-data.json.corrompido`; o aplicativo avisa na barra de status e começa do zero |
| Arquivo que deserializa mas não serve (`{"Programs": null}`, item nulo na lista) | Preservado do mesmo jeito — JSON válido não é o mesmo que JSON utilizável |
| Arquivo que não deu para ler (aberto por outro processo) | **Não** é renomeado: ele não está corrompido. A próxima gravação guarda a versão anterior em `.bak` |
| Gravação falha | O `.bak` anterior **não** é descartado: se a cópia falhar, a gravação é abortada em vez de sobrescrever sem backup |
| Exportação | É uma operação de leitura: não renomeia nem altera o arquivo do usuário |

A distinção entre "não deu para gravar" e "já existe um grupo com esse nome"
também é explícita: as operações de grupo devolvem `GroupResult`, e a janela
mostra a mensagem correspondente.

### Instalador (medido)

O original é distribuído por um instalador próprio; o Tranal também passa a
ser. O script é `installer/tranal.iss` (Inno Setup 6.7.3), compilado por
`tools/publish-tranal.ps1 -Variant setup`.

| Recurso | Original | Tranal | Onde |
|---|---|---|---|
| Assistente de instalação | ✅ | ✅ wizard do Inno Setup, **pt-BR e inglês** | `installer/tranal.iss` |
| Escolha da pasta de instalação | ✅ | ✅ | idem (`DisableDirPage=no`) |
| Entrada em "Adicionar e remover programas" | ✅ | ✅ | idem (`AppId` fixo) |
| Atalho no Menu Iniciar e na Área de Trabalho | ✅ | ✅ (área de trabalho como tarefa opcional) | idem |
| Desinstalador | ✅ | ✅ | idem |
| Instalação sem dependência externa | ✅ | ✅ o runtime do .NET viaja dentro do setup | idem |
| Assinatura digital do instalador | ✅ (autoridade) | ⚠️ **autossinada** — o Windows mostra "editor desconhecido" até o `.cer` ser instalado | `installer/certificado/TranalUninstallerPro.cer` |

Números medidos nesta máquina:

| Métrica | Valor |
|---|---|
| Tamanho do setup | 62.069.531 bytes (62,07 MB) |
| Payload instalado | 1.087.873 bytes (1,09 MB, 5 arquivos) |
| Redistribuível do .NET Desktop Runtime embutido | 60.035.296 bytes (10.0.14) |
| Runtime presente na máquina | `C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App\10.0.14` |

Duas decisões que valem registro:

1. **O programa instalado é a versão leve** (1,09 MB) e o runtime entra só
   quando falta. O setup fica menor que o pacote autocontido (62 MB contra
   132 MB) e o programa instalado ocupa 1/123 do espaço.
2. **O runtime é instalado em `PrepareToInstall`**, antes de qualquer arquivo
   do programa, com o código de saída conferido. Se ele falhar, a instalação é
   cancelada (`PrepareToInstall` devolvendo texto aborta com código 7) e nada
   é alterado no computador. Na seção `[Run]` do Inno **não existe**
   `ignoreexitcode`: o código de saída do instalador do .NET não pode ser
   ignorado, e confiar no número dele seria pior do que conferir o resultado
   no disco.

A compactação do setup é **zip**, não LZMA: medido nesta máquina, com LZMA o
McAfee bloqueava e apagava o executável do instalador como "programa
potencialmente indesejável" (falso positivo conhecido em instaladores com
carga LZMA). Com zip o bloqueio desaparece e o tamanho muda 0,3%.

O que o desinstalador **não** faz: apagar os dados do usuário. Comentários,
grupos e preferências moram em `%LOCALAPPDATA%` e continuam lá. A entrada do
menu de contexto do Windows **é** removida, pelo próprio programa
(`--unregister-context-menu`), porque deixar o registro apontando para um
executável apagado seria referência órfã.

### Desinstalação medida na máquina (instalador do Tranal x Revo)

Ciclo completo rodado na máquina do dono: instalar → conferir → desinstalar
pelo próprio "Adicionar e remover programas" → conferir.

| O que acontece | Tranal | Revo Uninstaller |
|---|---|---|
| Remove o programa instalado | ✅ | ✅ |
| Remove a entrada de "Adicionar e remover programas" | ✅ | ✅ |
| Remove os atalhos do Menu Iniciar | ✅ | ✅ |
| **Preserva a pasta de dados do usuário** (`%LOCALAPPDATA%`) | ✅ | ❌ **apagou** (4 backups e 61 backups de registro junto) |
| Remove a entrada do menu de contexto do Windows | ✅ (pelo próprio programa) | — |

A diferença é a que importa para quem usa: o Revo varre as sobras do programa
desinstalado e leva a pasta de `%LOCALAPPDATA%` junto — foi o que aconteceu
com as preferências e com os pontos de restauração do registro do dono. O
desinstalador do Tranal apaga só o que ele instalou.

Defeito encontrado e corrigido nesse mesmo ciclo (não aparece na leitura do
script, só na execução): a bandeira `postinstall` da seção `[Run]` faz o
instalador executar o programa com as credenciais do usuário **não** elevado, e
o Tranal exige administrador. O Windows barra o lançamento com *"CreateProcess
falhou; código 740"*. A entrada precisa de `runascurrentuser` — assim o
programa herda o token elevado do instalador e nem aparece um segundo pedido de
UAC.

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
| Suíte de testes | **302 verdes**, 0 falhas |
| Mutation check (defeito volta, o teste precisa reprovar) | **9 de 9** detectados |
| Partida a frio (de ponta a ponta, com o aplicativo de verdade) | **OK** — diálogo de tema, preferência gravada, janela principal, nenhuma exceção nova |
| Catálogos de idioma | **601 chaves** em pt-BR e en-US, em paridade |
| Programas listados | 77 (19 em "Novos Programas", 58 em "Outros Programas"); 210 com os componentes do sistema à mostra |
| Varredura completa | 150 ms |
| Ambiente do usuário | intocado pela suíte (comparação byte a byte antes/depois) |
