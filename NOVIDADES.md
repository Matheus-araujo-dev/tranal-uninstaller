# Novidades

## v1.4 — partida a frio corrigida, integridade das preferências por programa e instalador

### Novo: o Tranal agora sabe que tipo de desinstalador cada programa tem

Até aqui o Tranal tratava todo desinstalador do mesmo jeito: rodava a cadeia de
desinstalação como vinha do registro e procurava sobras pelo nome do programa.
A auditoria de concorrentes mostrou que essa é a lacuna de estrutura mais
importante — é o que o Bulk Crap Uninstaller faz de diferente, e é o que
destrava a desinstalação silenciosa, o desinstalador por MsiExec e a
geração de desinstalador para programa sem desinstalador.

Agora cada programa é **classificado** pelo tipo do desinstalador:

| Tipo | Como é reconhecido |
|---|---|
| `MsiExec` | o registro tem `WindowsInstaller=1` ou o comando é `msiexec` |
| `InnoSetup` | o desinstalador é o `unins000.exe` |
| `Nsis` | o desinstalador é o `uninstall.exe` |
| `InstallShield` | o caminho tem "InstallShield" ou é `setup.exe /x{…}` |
| `SdbInst` | o comando é o `sdbinst.exe` |
| `StoreApp` | a pasta de instalação está no `WindowsApps` |
| `Steam`, `Chocolatey`, `Oculus` | a loja aparece no comando |
| `PowerShell` | o desinstalador é um script |
| `SimpleDelete` | o comando só apaga arquivos (`cmd /c rmdir`) |

Apareceram **duas colunas novas** na lista: "Tipo de desinstalador" e
"Código do produto" (o GUID do Windows Installer). Também dá para pedir as
duas na exportação, pela CLI:

```
tranal /export "C:\lista.csv" /csv /columns "Program,UninstallerKind,ProductCode"
```

O valor da coluna é o **identificador fixo** do tipo (`MsiExec`, `InnoSetup`,
`Nsis`…), e não um texto traduzido: a coluna exportada continua legível por
qualquer ferramenta, em qualquer idioma, e a tela não ganha uma segunda
tradução que possa divergir do código.

**Medido nesta máquina** (211 programas instalados):

| Tipo | Programas |
|---|---|
| `MsiExec` | 149 (todos com o código do produto preenchido) |
| `Unknown` | 42 |
| `InnoSetup` | 9 |
| `Nsis` | 5 |
| `SdbInst` | 3 |
| `Steam` | 3 |

O "desconhecido" é honesto: são os desinstaladores de fabricante (Chromium,
Visual Studio, .NET, Riot, Electron) que não seguem nenhum empacotador
conhecido — classificá-los como se fossemseria inventar. A classificação
também é **conservadora de propósito**: o casamento do NSIS é pelo nome
**do arquivo**, não por substring, para que um `EAUninstall.exe` não seja
confundido com o `uninstall.exe` do NSIS (foi exatamente o que aconteceu na
primeira medição, e virou teste de regressão).

### Novo: instalador (setup)

O Tranal agora tem **instalador de verdade**, como o Revo: um `TranalUninstaller-Setup.exe`
(62,07 MB) com **wizard em português**, **escolha da pasta de instalação** e entrada em
**"Adicionar e remover programas"**.

- **O programa instalado ocupa 1,09 MB** (5 arquivos). O runtime do .NET Desktop que o WPF exige
  (60,04 MB) viaja dentro do setup e **só é instalado quando o computador não o tem** — assim o
  setup não depende de nada externo e funciona sem internet. Quem já tem o .NET 10 Desktop
  Runtime instala em segundos.
- O runtime é instalado **antes** de qualquer arquivo do programa, e o **código de saída é
  conferido** (`0` = sucesso, `3010` = sucesso mas precisa reiniciar). Se a instalação do runtime
  falhar, a instalação é **cancelada** e o computador fica como estava.
- O **desinstalador** apaga o programa, os atalhos e a entrada do menu de contexto do Windows
  (deixá-la apontando para um executável apagado seria referência órfã). Ele **não** apaga os
  dados do usuário: comentários, grupos, preferências e pontos de restauração do registro
  continuam na pasta de dados. (Medido na máquina: o Revo Uninstaller, ao desinstalar, **apaga**
  essa pasta junto — as sobras do programa.)
- **O instalador é assinado digitalmente** com um certificado autossinado da Tranal (o arquivo
  público está em `installer/certificado/TranalUninstallerPro.cer`). Quem instalar esse
  certificado na máquina vê "Tranal Uninstaller Pro" em vez de "editor desconhecido". A
  publicação assina todo instalador automaticamente — trocar por um certificado de uma
  autoridade é só substituir o secret do CI.
- **Aviso do antivírus**: mesmo assinado, o instalador pode ser marcado por antivírus como
  programa potencialmente indesejável (propriedade de instalador novo, não do Tranal). A
  compactação do setup é **zip**, e não LZMA, por causa disso: medido nesta máquina, com LZMA o
  McAfee bloqueava e apagava o instalador; com zip o bloqueio desaparece e o tamanho muda 0,3%.

### Correções

- **O aplicativo não abria na primeira execução.** Sem o arquivo de preferências
  (`settings.json`), duas coisas que só aparecem nesse caminho derrubavam o
  arranque: o `Checked` do botão de tema disparava durante o carregamento do
  XAML, antes de o botão existir, e o diálogo de primeiro lançamento apontava
  como proprietária uma janela que ainda não tinha sido exibida (o WPF recusa
  isso). O aplicativo agora abre, pergunta o tema, grava a preferência e mostra
  a janela principal — e a primeira pintura já sai no tema escolhido.
- **Um arquivo de preferências que deserializa mas não serve não derruba mais a
  listagem.** `{"Programs": null}` era aceito como se fosse válido, e o arquivo
  do usuário era sobrescrito na gravação seguinte. Agora lista nula ou item nulo
  na lista tratam o arquivo como corrompido: ele é preservado como
  `program-data.json.corrompido`, o aplicativo avisa na barra de status e
  começa do zero.
- **O arquivo corrompido não é mais renomeado por uma simples exportação.**
  A exportação é uma operação de leitura: ela lê o arquivo do usuário por conta
  própria e não mexe nele.
- **A cópia de segurança não é mais descartada.** Quando a troca atômica não é
  possível, a versão anterior é copiada para `.bak` **antes** da substituição —
  e, se a cópia falhar, a gravação é abortada, em vez de sobrescrever o arquivo
  sem backup. O arquivo temporário também não sobra mais na pasta de dados.
- **"Já existe um grupo com esse nome" não aparece mais quando o problema é
  outro.** O gerenciador de grupos distingue conflito de nome de falha de
  gravação, e a falha de gravação é avisada. Renomear ou apagar que falham
  desfazem a mudança na tela.
- **O "Ajustar ao conteúdo" não se perde ao trocar de modo.** Voltar para
  "Detalhes" mantinha a largura que o usuário acabara de ajustar.
- **O título do Gerenciador de grupos acompanha o idioma** da interface.
- **A janela principal não fica sem ícone** quando o arquivo escolhido não
  carrega como imagem: o carregamento do ícone passou a ter um código só.

### Testes

- 302 testes verdes. O primeiro arranque é conferido de ponta a ponta por
  `tools/test-cold-start.ps1`, que sobe o aplicativo de verdade sem o arquivo de
  preferências e devolve as preferências de quem rodou o teste.

## v1.3 — colunas customizáveis, exportação com escolha de colunas, grupos, comentário e ícone

Funcionalidades levantadas no inventário do original (strings 15001, 15054–15060,
15066, 15070–15072, 15079–15081, 15173, 15216–15217, 15239–15240, 15281–15285,
15298, 15360–15364 e 15366 do `lang\english.ini` do Revo Pro 5.5.4).

### Novas funcionalidades

- **Colunas customizáveis** — a janela "Colunas" (15054) escolhe quais colunas a
  lista mostra; a coluna do nome é fixa (sem ela a lista perde o sentido). O menu do
  cabeçalho traz "Remover esta coluna" (15055) e o alinhamento (15056–15060),
  incluindo "Ajustar ao conteúdo", que mede o maior texto da coluna. A escolha fica
  nas preferências: reabrir o Tranal mostra as mesmas colunas.
- **Exportação com escolha de colunas** — "Select the columns you want to export:"
  (15066) abre a mesma janela de colunas. Sem nenhuma coluna marcada a exportação
  não acontece (15298). O CSV exportado carrega a anotação e o grupo do programa,
  exatamente como a tela mostra.
- **Grupos** — "Groups Manager" (15001), "Group name:" (15070) e "Add" (15071):
  o gerenciador cria, renomeia e apaga grupos, e o menu de contexto do programa
  tem "Adicionar ao grupo" (15080) e "Remover do grupo" (15081). Renomear um grupo
  leva os programas junto; apagar um grupo **não** apaga programa nenhum — o grupo
  é classificação, não desinstalação.
- **Comentário por programa** — "Add Comment" (15216), "Edit Comment" (15217) e
  "Enter your comment:" (15281). A anotação do usuário é uma coluna própria
  ("Anotação"), separada dos "Comentários" que vêm do registro.
- **Ícone por programa** — "Change Icon" (15079), "Choose Icon" (15282), "Select an
  icon from the list:" (15284) e "File path:" (15283). A galeria é montada com os
  ícones que já existem na máquina (os dos programas instalados) e também aceita
  um caminho de arquivo.
- **Filtro "Hidden"** (15362) — interruptor no popup de filtros que mostra os
  componentes do sistema. É a mesma preferência de "Mostrar componentes do
  sistema" das Opções: uma regra, dois lugares para chegar nela.
- As preferências por programa ficam em `program-data.json`, ao lado das demais
  preferências; as colunas escondidas ficam em `settings.json`.

### Correções

- **O clique direito na lista seleciona a linha apontada.** Sem isso, o menu de
  contexto agia sobre a linha selecionada antes — e o usuário acabaria anotando o
  programa errado, sem nenhum aviso.
- **O menu de contexto não quebrava mais quando não havia nenhum grupo.** O item
  "(nenhum grupo criado ainda)" era o mesmo objeto nos dois submenus, e um item de
  menu não pode ter dois pais: a exceção era engolida e o menu abria sem
  "Adicionar ao grupo" e "Remover do grupo".
- **`program-data.json` não perde mais nada.** A gravação é atômica (o conteúdo
  vai para um arquivo temporário e só então substitui o original) e deixa uma
  cópia `.bak`. Um arquivo ilegível é preservado como `program-data.json.corrompido`
  em vez de ser sobrescrito pela gravação seguinte.
- **Falha ao gravar é avisada.** O comentário aparecia na lista e sumia ao fechar
  o aplicativo, sem nenhum aviso; agora a interface diz que não foi possível
  salvar.
- **A lista e a exportação não podem discordar sobre o valor de uma coluna.** O
  catálogo de colunas (`Core/ProgramColumns.cs`) é a única fonte que sabe quais
  colunas existem e que valor elas têm; as duas telas consomem essa fonte.
- **A versão vazia aparece como "No Version"**, como no original, na lista e na
  exportação.
- **O modo de visualização (Detalhes/Lista/Ícones) sobrevive** à reconstrução da
  grade: trocar de idioma ou de colunas não devolve a lista ao layout "Detalhes".
- **Um arquivo que não tem ícone não substitui o ícone do programa**: o caminho
  escolhido é recusado e o ícone original continua valendo.
- **A identidade de um programa passou a incluir a visão do registro (32/64
  bits)**: a mesma subchave existe nas duas visões, e a anotação de uma não pode
  aparecer na outra.
- **O Gerenciador de Grupos ganhou botão de fechar.** A janela só saía pelo X da
  moldura, ao contrário de todas as outras do aplicativo.
- **A coluna do nome passou a ordenar** como no original.

### Mudança de formato: a exportação

O `tranal /export` passa a usar **um conjunto fixo de colunas** (o do original:
nome, versão, tipo, data, empresa, página inicial, comentários, comando de
desinstalação e chave do registro) quando o usuário não escolhe nenhuma, em vez
de despejar o catálogo inteiro. Quem quiser escolher usa
`/export "caminho" /csv /columns "Program,Version,Note"`.

O CSV e o XML mantêm os nomes fixos das colunas (iguais em qualquer idioma); o
HTML passou a usar os títulos traduzidos, porque quem lê HTML é gente. No XML, o
elemento do registro passou a se chamar `<Entry>`: o elemento da coluna do nome é
`<Program>`, e um `<Program><Program>` confundiria quem lê o arquivo.

---

## v1.2 — lista de exclusões, processos em execução, todas as contas e backup do registro

Quatro funcionalidades que faltavam para fechar a paridade de segurança e de
alcance da desinstalação. Todas medidas nas strings do original (inventário de
331 strings da seção `UninstallerPro` do `lang\english.ini` do Revo Pro 5.5.4).

### Novas funcionalidades

- **Lista de exclusões** — o item protegido **não aparece** na varredura e o motor
  **recusa removê-lo** mesmo que ele chegue marcado por outro caminho. O backup feito antes
  da remoção também pula os itens excluídos: um backup não pode guardar justamente o que o
  usuário mandou proteger. Excluir um **arquivo** dentro de uma pasta protege o arquivo mesmo
  quando a pasta é removida: a remoção recursiva cega levaria o arquivo junto, então o conteúdo
  sai item a item e a pasta permanece. O encerramento de processos também respeita a lista.
  O casamento é por prefixo de pasta/chave, e `HKLM\…` casa com `HKEY_LOCAL_MACHINE\…`. Tem
  gerenciador próprio na interface e comando `/exclude` na CLI.
- **Encerrar os executáveis em execução** antes de remover os arquivos deles — a opção
  "Forcibly stop running executable files during their uninstall" do original. O processo do
  próprio Tranal nunca é encerrado, e um processo só entra na lista quando o executável está
  dentro da pasta que vai ser removida — nunca "qualquer processo com o mesmo nome".
- **Varredura em todas as contas do Windows** — as sobras dos outros perfis aparecem
  (`HKEY_USERS\<SID>` e o `AppData` de cada perfil). As contas vêm do `ProfileList` do
  registro, que é o catálogo real de perfis da máquina. Os itens das outras contas **não vêm
  marcados**: só a opção "Desinstalar de todas as contas do Windows" os marca. A opção
  "marcar todas as sobras" **não** marca itens de outra conta.
- **Backup completo do registro antes de desinstalar** — todos os hives em `.reg`, com script
  de restauração, feito **antes do desinstalador nativo**: depois dele, o desinstalador do
  próprio programa já apagou as chaves dele e o `.reg` não serviria para nada. Usa o
  `reg.exe` do Windows em vez de reescrever o formato `.reg` por conta própria: é o caminho
  nativo, preserva todos os tipos de valor (inclusive `REG_BINARY`, `REG_QWORD` e
  `REG_MULTI_SZ`) e não depende de o Tranal saber reescrever cada tipo sem erro. O backup só
  é considerado válido quando **todos** os hives foram exportados.

### Correções

- O `LeftoverScanner` recebia modo e "marcar tudo" como parâmetros posicionais soltos; com
  mais um comportamento, quatro parâmetros booleanos ficaram impossíveis de ler. Passam a
  viajar como um único objeto nomeado (`LeftoverScanOptions`).
- **O motor nunca atravessa um atalho** (junction, symbolic link, ponto de montagem).
  Medido nesta máquina: o .NET 10 atravessa o atalho ao enumerar uma pasta recursivamente e
  falha com `UnauthorizedAccessException` ao apagá-la — e a falha deixava a pasta pela
  metade. Como o alvo do atalho está **fora** da pasta que o usuário mandou remover,
  apagá-lo seria perda de dados não pedida. Agora o atalho é removido **como atalho** e o
  alvo fica intacto, nos dois caminhos da remoção (o que apaga a pasta inteira e o que apaga
  item a item por causa da lista de exclusões), **quando o próprio item de remoção é um
  atalho**, na contagem de tamanho de pasta, no varredor de programas, na cópia do backup e
  na varredura do limpador de arquivos temporários.
- **A desinstalação forçada volta a remover o alvo que o usuário apontou.** O mesmo
  caminho entrava na lista duas vezes — como pasta de instalação e como pasta achada pelo
  nome do programa — e a ocorrência fraca (desmarcada) sombreava a forte. O resultado era
  que a desinstalação forçada encontrava a pasta e não removia nada. Agora vale a
  ocorrência mais forte, e o alvo informado entra primeiro na lista.
- **Apontar para um arquivo não remove mais a pasta em que ele está.** Ao corrigir a
  deduplicação, a pasta que contém o arquivo passou a ser tratada como "pasta de
  instalação" — e a desinstalação forçada de um executável em `%LOCALAPPDATA%\Temp`
  passaria a remover a pasta `Temp` inteira, com tudo o que o usuário tem lá dentro. A
  desinstalação forçada de um arquivo remove **o arquivo**; a pasta aparece na lista e o
  usuário pode marcá-la se quiser.
- **O relatório da remoção não esconde mais nada.** Uma pasta com item protegido é um item
  na lista, mas o conteúdo sai item a item: o resumo agora diz quantos itens saíram de
  dentro dela, quantos ficaram protegidos, quantos falharam e quantos foram adiados para o
  próximo reinício. Um item em uso que não pôde ser apagado não é engolido em silêncio: ele
  aparece na contagem e no texto do relatório — na interface e na linha de comando, com os
  mesmos números.

## v1.1 — fechar as lacunas do REA e a cobertura das ferramentas

### Novas funcionalidades (medidas nas strings do original)

- **Painéis visíveis ou ocultos**: submenu **Painéis** no menu principal com o cabeçalho
  principal, o painel de descrição, a barra de status e as ferramentas da barra lateral —
  e **Restaurar layout padrão**. É o menu "Exibir" do Revo (strings 16004–16009, 16104–16106).
- **Marcar todas as sobras por padrão**: opção do Revo que passa a marcar também a
  confiança média e baixa. Continua valendo o padrão do original — só a alta confiança vem
  marcada — e a confiança de cada item continua sendo exibida.
- **Ocultar mensagens de informação e de aviso**: as duas opções do original. A confirmação
  de uma ação destrutiva nunca é omitida.
- **Menu de contexto do Windows Explorer**: a entrada "desinstalar com o Tranal" passa a ser
  **registrada de verdade** (antes o script era só gerado) e a preferência **Mostrar comandos
  no menu de contexto do Windows** liga e desliga na hora. É opt-in: nada é registrado sem o
  usuário pedir.
- **Apagar o log do programa monitorado após a desinstalação**.
- **Tracks Cleaner em três limpadores separados** — Navegadores, Microsoft Office e Windows —
  como no original, com os 32 rastros de navegador medidos nesta máquina.
- **Menu de contexto das listas das ferramentas**: marcar/desmarcar todos, marcar/desmarcar os
  selecionados, abrir a pasta que contém, propriedades e **pesquisar {termo} na web**.
- **Mecanismo de busca na web** funcionando de verdade (Google, Bing, DuckDuckGo, Yahoo) — a
  preferência existia, mas nada a consumia.

### Correções

- **O Otimizador de Registro nunca foi registrado**: o botão estava na barra lateral, mas a
  tela não existia no registro de telas — o clique abria um **painel vazio**. Agora o catálogo
  de ferramentas mora no núcleo (`Core/ToolCatalog.cs`) e é a fonte única da barra lateral, do
  registro de telas e da suíte de testes.
- **Excluir um backup com arquivo travado** apagava o manifesto antes da pasta: com um arquivo
  em uso, a exclusão falhava e deixava uma pasta órfã invisível para o aplicativo. A correção
  existia só no comentário do código; agora está implementada e tem teste.
- **Os rótulos de grupo da barra lateral ficavam invisíveis**: usavam `GroupHeaderBrush`, que é
  a cor de **fundo** do cabeçalho de grupo, como cor de texto. A chave existia nos dois temas,
  então nada acusou — o texto é que não aparecia. Agora há teste que reprova brush de fundo
  usado como cor de texto.
- A entrada do menu de contexto do Windows deixou de ser registrada por padrão: ela é uma
  mudança visível no ambiente do usuário e passou a depender da opção.
- **"Marcar todos" e "desmarcar todos" não mexiam nas caixas**: a marcação da linha é alterada
  por código, e a linha não avisava a tela. O modelo mudava, a caixa ficava como estava — e o
  pior caso era o inverso: "marcar todos" voltava a marcar o que o usuário tinha desmarcado, e
  o item era apagado. Agora a linha avisa a mudança.
- **A entrada do menu de contexto do Explorer não fazia nada**: ela chamava o executável
  gráfico com `--uninstall-target`, que a interface ignorava. Agora a interface lê o argumento,
  abre a desinstalação forçada e já com a análise feita.
- **A pasta de Downloads entrava na limpeza de rastros**, marcada por padrão: "limpar tudo" no
  limpador do Windows apagaria os arquivos baixados pelo usuário. O original apaga o *histórico*
  de downloads (o mesmo banco dos navegadores), nunca os arquivos. A pasta saiu de lá.
- **O histórico da barra de endereços estava no limpador errado**: o original coloca o rastro
  `TypedPaths` no limpador do Windows ("Clear the Printers, Computers and People Find
  History"), não no de navegadores.
- **Faltavam dois itens do limpador do Windows**: "Esvaziar a Lixeira" e "Limpar o histórico da
  última chave aberta no Regedit". Os dois existem agora.
- As opções de layout feitas pela janela de Opções só valiam no próximo arranque: agora valem
  na hora.

### Cobertura de testes

A suíte passou de **106 para 154 testes**, todos verdes:

- **Ferramentas novas**: Otimizador de Registro, Modo Portátil, menu de contexto do Explorer,
  janela principal flutuante e cliente de atualização.
- **Preferências**: padrão, gravação/recarga e compatibilidade com um `settings.json` antigo.
- **Paridade de interface**: a barra lateral, o catálogo de ferramentas e as telas não podem
  divergir — é o teste que pega o defeito do Otimizador de Registro.
- **Tema**: um brush de fundo não pode pintar texto.
- **Regressões do Gerenciador de Backups**: exclusão com arquivo travado.
- **Segurança da limpeza**: a pasta de Downloads não pode entrar na limpeza de rastros; o
  `TypedPaths` fica no limpador do Windows; a Lixeira aparece como item do limpador do Windows.
- **Cobertura real**: os testes de "marcar todas as sobras" agora montam um programa falso com
  sobra de verdade (uma pasta e uma chave de desinstalação descartável). Antes eles rodavam
  sobre uma lista vazia e passavam sem provar nada.

A suíte continua **sem tocar o ambiente do usuário**: o estado do disco, dos backups, do
registro pendente e das preferências foi comparado antes e depois — idêntico.

---

## v1.0 — primeira rodada completa de paridade

### Desinstalação

- **Quatro modos de desinstalação** (Somente, Seguro, Moderado, Avançado), como no Revo
  Uninstaller Pro. O modo passou a ser a única opção que governa a profundidade da varredura
  (`UninstallMode`), no lugar do antigo "nível de varredura".
- **Varredura antes e depois** da desinstalação: o que existia antes é comparado com o que
  sobrou, e a lista de sobras passa a mostrar a **confiança** de cada item. Como no Revo,
  **só os itens de alta confiança (em negrito) vêm marcados** para remoção.
- **Arquivos e pastas sobras vão para a Lixeira** em vez de sumir em definitivo. Chaves de
  registro continuam sendo apagadas em definitivo — é o comportamento medido no binário do Revo.
- **Item em uso é adiado para o próximo reinício** do Windows, em vez de ser deixado para trás
  em silêncio. O resumo passa a contar esses itens.
- **Gerenciador de Backups**: cada remoção cria um ponto de restauração com o conteúdo real do
  que foi removido. Dá para restaurar tudo, restaurar item a item (restauração personalizada) ou
  excluir por tempo (uma semana, duas, 30, 90, 180 dias, um ano ou data escolhida).
- **Ponto de restauração do sistema** antes de desinstalar, configurável nas Opções.

### Ferramentas novas

- **Janela Principal Flutuante**: um overlay pequeno, sempre no topo e transparente que se
  arrasta sobre outra janela, um ícone ou um item da barra de tarefas. Resolve o alvo e oferece
  desinstalar, impedir inicialização automática, encerrar processo, encerrar e excluir, abrir a
  pasta, informações e propriedades.
- **Menu de contexto do Windows Explorer**: entrada "desinstalar com o Tranal" ao clicar com o
  botão direito num executável.
- **Otimizador de Registro**: varre o Menu Iniciar e encontra atalhos de programas que já não
  existem mais, para removê-los.
- **Verificação de atualização**: pode rodar na inicialização ou em segundo plano.
- **Modo portátil**: pasta de dados própria e carregamento rápido.

### Interface

- As opções do desinstalador migraram para o diálogo de Opções, com persistência em
  `settings.json`.
- Catálogos de idioma ampliados de 299 para **446 chaves** em pt-BR e en-US, sempre em paridade.

### CLI

- `/mode builtin|safe|moderate|advanced` nos comandos de varredura e desinstalação.
- Flags `/recycle`, `/backup` e `/defer` (e seus opostos) para controlar o destino das sobras.
- Comandos do Gerenciador de Backups: `/backup`, `/backup restore <id>`,
  `/backup restore <id> <caminho...>`, `/backup delete <id>`, `/backup delete-all`,
  `/backup delete-older <dias>`.

### Correções

- O `Foreground="White"` fixo no XAML sumia com o texto no tema claro. Agora a cor vem do dicionário
  do tema, e há teste que reprova cor fixa fora dos dicionários.
- A troca de tema em runtime não repintava a janela. Os temas passaram a ser carregados uma vez
  por XAML, e a troca escreve as brushes em `Application.Resources`.
- A extensão de navegador com `__MSG_APP_NAME__` aparecia com o token cru. Agora o nome é
  resolvido pelo `default_locale` do `manifest.json`, sem diferenciar caixa.

### Revisão adversarial do núcleo

O núcleo passou por uma revisão adversarial completa (16 defeitos, 2 deles críticos), e o que
foi corrigido:

**Críticos**
- O roteamento dos rastros de limpeza casava pelo separador errado, e os 29 rastros caíam no
  motor que apaga a **pasta inteira** em vez do conteúdo. Agora todos passam pelo Tracks Cleaner.
- A raiz de uma unidade era tratável com filtro curinga — o que apagaria `pagefile.sys`,
  `hiberfil.sys` e `swapfile.sys` e os agendaria para o próximo boot. A raiz nunca é mais alvo.

**Altos**
- A restauração escrevia onde o manifesto mandasse, sem revalidar a proteção. Um manifesto
  adulterado não vira mais escrita arbitrária em disco.
- Restaurar uma pasta **apagava** o que existia no destino: dado criado depois do backup sumia
  em silêncio. Agora a restauração mescla.
- A suíte de testes apagava os pontos de restauração **reais** do usuário e gravava o
  `settings.json` real. Agora tudo aponta para um diretório temporário.

**Médios e baixos**
- A seleção de restauração casava por referência e virava no-op silencioso.
- `REG_MULTI_SZ` perdia os elementos vazios do meio da lista.
- Um prefixo de registro desconhecido era assumido como `HKEY_LOCAL_MACHINE`.
- A chave de desinstalação de um produto homônimo ("Steam Client" para "Steam") vinha como alta
  confiança e marcada para remoção.
- Excluir um backup com arquivo travado deixava uma pasta órfã invisível para o aplicativo.
- "Lixeira indisponível" deixou de ser reportado como se o item tivesse ido para a Lixeira.
- Os containers do sistema (`C:\Users\Public`, `C:\ProgramData`) são protegidos pelo caminho
  exato — sem impedir a desinstalação de programas dentro de `Program Files`.

Cada defeito tem um teste de regressão. A suíte passou de 91 para **106 testes**, todos verdes.
