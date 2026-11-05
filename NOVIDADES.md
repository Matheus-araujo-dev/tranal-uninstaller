# Novidades

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
