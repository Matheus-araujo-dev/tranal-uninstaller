# Novidades

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
