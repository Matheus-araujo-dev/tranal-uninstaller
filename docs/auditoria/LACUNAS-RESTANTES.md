# Lacunas restantes (medido em 10/11/2026)

Este documento lista a **uma** funcionalidade que ainda nao existe no
Tranal, entre as 725 funcionalidades medidas contra os 11 concorrentes.

Cada linha diz **o que falta** e **por que** ela continua de fora. Nenhuma
linha aqui esta marcada como "OK": a medicao e o que vale, e medir e dizer
"ok" sem o codigo seria mentira.

O numero da cobertura esta em `PARIDADE-11-CONCORRENTES.md`
(78,2% — 725 funcionalidades, 537 OK, 157 PARCIAL, 1 AUSENTE, 30 FORA por
decisao).
O Revo Pro 5.5.4 segue com 68 de 70 OK e 2 FORA por decisao registrada
(cobertura 100%, conforme a tabela acima).

## UNINSTALLTOOL T3#46 — Extensao de shell empacotada em MSIX para o Windows 11

**O que e.** O Uninstall Tool empacota a extensao de menu de contexto em um
pacote MSIX, o que faz o Windows 11 mostrar a entrada com o icone do
aplicativo empacotado, e nao como uma entrada solta no registro.

**O que o Tranal tem hoje.** As entradas de menu de contexto do Tranal no
Explorer — "Rastrear a instalacao", "Shred", "Monitor de instalacao" e a
entrada de desinstalar — registradas em `HKCU\Software\Classes\...\shell`,
com `MUIVerb`, `Icon` e a subchave `command`. O registro e o mesmo que o
Windows le; o que muda no MSIX e **quem** hospeda o icone (e o pacote
assinado, em vez do executavel solto).

**Por que continua de fora.** Empacotar o Tranal em MSIX muda a forma de
instalacao do programa inteiro (o instalador atual e o Inno Setup, e o
programa e assinado). E uma decisao de produto — assinatura, instalador,
distribuicao e local de instalacao — e nao uma feature.

**Medido, nao suposto.** A diferenca entre as duas formas nao e de
funcionalidade: e de **quem** hospeda o icone da entrada. O que o Windows le
e o mesmo registro nos dois casos; o MSIX muda a assinatura do icone. Nao
ha como medir essa diferenca sem um pacote empacotado, e um pacote
empacotado e exatamente a decisao pendente.

## Fechadas nesta rodada

**SOFTORGANIZER #26 — o gerenciador do log de rastreio.** O log de
instalacao e a fonte do que o instalador gravou no computador, e e ele que a
desinstalacao dirigida pelo log remove. O nucleo ja sabia acrescentar e
excluir um item do log (`Core/Reporting/TraceLogExporter.cs`, `TraceLogEditor`),
mas **ninguem chamava**: a pessoa nao tinha como corrigir o log antes da
remocao. O detalhe esta em `EVIDENCIA-LOTE31-GERENCIADOR.md`:

- a **tela do gerenciador** (`Ui/Tracing/TraceManagerWindow.xaml.cs:28`) mostra
  o log, exclui o item selecionado e acrescenta outro (tipo + caminho); o
  comando esta na barra do modulo da base de logs (`Ui/Modules/ModuleViews.cs:279`)
  e na janela do proprio log;
- a **linha de comando** responde por `/log-add` e `/log-drop`
  (`src/Tranal.Cli/LogCommands.cs:205` e `:247`);
- o item acrescentado a mao entra com **confianca media**, nao alta: ninguem
  mediu que ele pertence ao programa;
- a **gravacao do log ficou em um lugar so** (`Core/Modules/TraceStore.cs:622`,
  `GravarAtomicamente`): antes o arquivo era escrito em dois lugares, e o
  `SavePayload` devolvia "gravado" mesmo quando a gravacao falhava;
- a suite tem 9 testes proprios (`Lote31FeatureTests.cs`) e o teste de ponta a
  ponta (`tools/test-lote31.ps1`) tem 13 verificacoes no aplicativo de
  verdade (o teste cria um log sintetico e o apaga no fim).

**SOFTORGANIZER #59, WISE #62 e BCU T4#80 — a assinatura do desinstalador e
o comando do usuario.** A verificacao da assinatura digital ja existia no
nucleo (`Core/Discovery/UninstallerCertificate.cs`) e so os testes a
chamavam: ninguem conseguia saber se o desinstalador de um programa era
assinado. O comando do usuario (`ShellCommand`) tambem nao tinha quem o
chamasse. O detalhe esta em `EVIDENCIA-LOTE30-ASSINATURA.md`:

- a **assinatura** tem tela (`Ui/UninstallerSignatureWindow.xaml.cs:45`),
  item no menu "Mais comandos" e linha de comando (`/signature` e
  `/signatures`);
- ela e **sob demanda** e nao uma coluna: `Verify` custa cerca de 160 ms por
  arquivo (medido) e a varredura de 51 desinstaladores custa 8,1 s — uma
  coluna assim seria impossivel de usar;
- a **assinatura por catalogo de componentes do Windows** nao e alcancavel
  com as APIs gerenciadas do .NET 10 nem com P/Invoke estavel (medido em
  varias combinacoes). Em vez de mentir, o produto **avisa** que o
  executavel e um componente do Windows;
- **um bug real apareceu no caminho**: a pergunta "qual e o executavel do
  desinstalador" tinha seis copias que discordavam entre si. A tela dizia
  que o "AMD Install Manager" nao tinha desinstalador registrado, porque o
  registro nomeia `MsiExec.exe` sem caminho. A regra subiu para
  `Core/Discovery/UninstallerExecutable.cs` (`Declared` e `Resolve`, que
  procura no PATH como o Windows faz);
- a tela **fala a lingua do catalogo**: o texto do nucleo (portugues fixo) e o
  estado traduzido apareciam lado a lado, e se contradiziam no caso do
  instalador do Windows. O nucleo agora separa a explicacao do estado
  (`Message`, para a linha de comando) do detalhe tecnico da falha
  (`Detail`), que a interface traduz;
- o "executar aplicativos externos" do BCU e **desabilitavel**: a preferencia
  `AllowRunningUserCommands` (`Core/Settings/AppSettings.cs:123`) esconde o
  botao "Executar" quando esta desligada;
- a suite tem 23 testes proprios (`Lote30FeatureTests.cs`) e o teste de ponta
  a ponta (`tools/test-lote30.ps1`) tem 13 verificacoes no aplicativo de
  verdade.

**GEEK T2#34 e #13, GEEK T4#80, HiBit #80, HiBit T2#21 e #21, IObit #29,
TOTALUNINSTALL T6#34 e T6#35, SOFTORGANIZER #22 e #25 - o que ja existia e
nao tinha dono.** Onze linhas do RECON mediam funcionalidade que o Tranal
**tinha**, mas que ninguem conseguia alcancar: a varredura de atalhos, a
remocao de KB do Windows, o pacote portatil, o backup do programa com
undo/redo, o Centro de mudancas e o aviso "use o Centro de mudancas para
cancelar". O RECON media o resultado (o que a tela mostra) e nao via o codigo
morto. O detalhe esta em `EVIDENCIA-LOTE29-DONO.md`:

- a varredura de atalhos (`Core/Discovery/ShortcutDiscovery.cs`) aparece na
  tela de Descoberta, com o icone e a localizacao de cada programa;
- a remocao de KB do Windows (`Core/Management/WindowsUpdateManager.cs`)
  ganha comando proprio na linha de comando (`/windows-updates` e
  `/windows-update-remove`) e botao na tela de Descoberta;
- o pacote portatil (`Core/Reporting/PortableArchive.cs`) ganha
  `/portable-export`, `/portable-import` e `/portable-import-list`;
- o Centro de mudancas (`Core/Recovery/UndoChangesCenter.cs`) passa a ter
  tela (`Ui/Tools/ToolViews.cs:632`), comando (`/undo-center`) e a remocao
  **entra nele em producao** (`Core/UninstallSupport.cs:1620`);
- o **perfil do programa** (o que ele gravou no computador) passa a ser
  gravado e mostrado na linha do centro;
- o aviso "use o Centro de mudancas para cancelar" sai no resumo da tela e
  na linha de comando;
- o limite em MB da pasta de backup existia (`Core/Recovery/SnapshotLimits.cs:49`)
  e a medicao do RECON estava defasada;
- **um bug real apareceu no caminho**: desfazer devolvia o conteudo mas a
  mudanca continuava marcada como nao desfeita
  (`Core/Recovery/ProgramBackup.cs`, `GravarOperacoes` relia o disco e
  perdia o estado novo). O bug so era invisivel porque ninguem consumia o
  estado;
- o proprio `tools/auditoria/aplicar-recon.ps1` tinha um furo: ele nao lia o
  `RECON-D-2026-11-08.md` (o estado esta na segunda coluna e a busca
  comecava na terceira). MEDIDO: 38 e 48 linhas passaram a aplicar, e o
  numero de cobertura nao mudou - o furo nao inflava nada;
- **a funcionalidade que aparece em varios relatorios precisa ser corrigida
  em todos**: o `SOFTORGANIZER #22` estava remediado em um relatorio e
  continuava PARCIAL, porque o outro (aplicado por ultimo) media o estado
  antigo. As quatro linhas do `RECON-C-2026-11-08.md` foram corrigidas
  tambem.

**BCU T1#13, T1#14, T1#15 e T1#16 e UNINSTALLTOOL T1#3 - as marcas do
programa na lista.** O RECON media que o Tranal tinha o dado (a tag
`NoRemove`, o valor `IsUpgrade`, a saude do desinstalador, a maquina do
executavel) mas **nao tinha a marcacao na lista principal**: o sinal nao
aparecia em nenhuma coluna. Alem disso, a marcacao `(32-bit)` era uma coluna
(`Tipo`) e nao o sufixo no nome, como no Uninstall Tool e no Geek. O detalhe
esta em `EVIDENCIA-LOTE28-MARCAS.md`:

- a REGRA mora no nucleo, em um lugar so: `Core/ProgramMarks.cs` (`Detect`,
  `:96`) devolve as marcas - 32 bits, atualizacao, orfao, invalido e
  protegido - e a lista, a exportacao e a linha de comando consomem esse
  resultado;
- cada marca e um **fato medido** (o disco, o registro, o desinstalador), e
  nao um palpite: quando o Tranal nao sabe, a marca nao existe;
- a regra do **orfao** mora em um lugar so (`ProgramMarks.IsOrphan`, `:141`) e
  a coluna "Origem" passa a consumir essa mesma regra;
- a coluna "Protegido" passou a considerar a tag `NoRemove` do proprio
  programa, e nao so a lista de exclusao do Tranal;
- o **nome exibido** carrega o sufixo `(32-bit)`, como no Uninstall Tool e no
  Geek, e a identidade do programa (chave de registro, nome cru) continua
  separada;
- a suite tem 22 testes proprios (`Lote28FeatureTests.cs`) e um teste de ponta
  a ponta no aplicativo de verdade (`tools/test-lote28.ps1`, 12 verificacoes):
  80 programas com marca na linha de comando, 29 com `(32-bit)` na tela e 0
  divergentes entre os dois. O teste so le a lista - nenhum software instalado
  da maquina e tocado.

**TOTALUNINSTALL T2#22, T2#11 e #22, #69, #71 — a arvore de alteracoes
detectadas, a busca textual dentro dela, a exportacao e a impressao.** A
comparacao A x B tinha DUAS apresentacoes independentes (a tabela, usada na
exportacao e na impressao, e a descricao em texto da tela), e elas podiam
diverigir: o que se lia na tela nao era o que saia no arquivo. Alem disso, a
apresentacao era uma tabela — nao uma arvore hierarquica — e nao havia
busca dentro das alteracoes. O detalhe esta em
`EVIDENCIA-LOTE27-ARVORE-BUSCA.md`:

- a CLASSIFICACAO mora no nucleo, em um lugar so
  (`Core/Reporting/ChangeTreeReport.cs:86`, `Items`): a tabela e a arvore
  derivam dela, entao o que a pessoa le na tela e o que sai no arquivo sao as
  mesmas alteracoes;
- a hierarquia e a busca moram em `Core/Reporting/ChangeTreeNodes.cs`:
  `Build` (`:86`) monta os nos (categoria, grupo de mudanca, alteracao),
  `Filter` (`:137`) filtra **preservando a hierarquia** e diz quantas
  alteracoes sobraram ("1 de 6");
- a tela tem a arvore, a busca e a contagem na aba de snapshots da janela de
  rastreio; a linha de comando tem `/arvore` e `/busca "termo"`;
- a suite tem 17 testes proprios (`Lote27FeatureTests.cs`) e um teste de
  ponta a ponta no aplicativo de verdade (`tools/test-lote27.ps1`, 21
  verificacoes), com snapshots sinteticos que o proprio teste apaga.

As linhas de **exportacao** (#69) e **impressao** (#71) da arvore eram
medicao desatualizada: as duas capacidades existiam, e o RECON falava de um
`PrintLayout.cs` que nao existe mais.

**GEEK T1#10, HIBIT T1#10, WISE T1#7 e T1#8 — "Remover entradas" e "Batch
Remove Entries", e a acao "Modificar" que media errada.** O RECON media que a
interface nao tinha acao para tirar o programa da lista "Adicionar e Remover
Programas" sem desinstalar, e que o "Modificar" nao chegava a tela. As duas
medicoes estavam erradas. O detalhe esta em
`EVIDENCIA-LOTE26-REMOCAO-ENTRADAS.md`:

- a REGRA mora no nucleo, em um lugar so: a acao avulsa
  (`Core/Management/WindowsListEntry.cs:109`) delega para a acao em lote
  (`:136`, `RemoveMany`), e as duas passam pela mesma remocao
  (`Core/RegistryEditing/RegistryBatchRemover.cs:121`), que respeita a chave
  protegida e a visao do registro do programa;
- a tela tem a acao em lote (menu "Mais comandos", `CmdRemoveEntriesBatch`),
  com a confirmacao que diz, na cara do usuario, que **os programas nao serao
  desinstalados** — remover entrada nao e desinstalar;
- a linha de comando tem `tranal /remove-entries "programa"`;
- a suite tem 17 testes proprios (`Lote26FeatureTests.cs`) e um teste de ponta
  a ponta no aplicativo de verdade (`tools/test-lote26.ps1`, 7 verificacoes),
  que cria uma entrada de teste no HKCU, apaga e limpa tudo — nenhum software
  instalado da maquina e tocado.


**UNINSTALLTOOL T6#85 — Paineis ancoraveis ("push pin") com pre-visualizacao.**
Era a ultima lacuna de **codigo** entre os 11 concorrentes, e fechou com o
que o Uninstall Tool tem, medido no binario dele (`Click push pin to keep
open`, `Close Preview` e `Close Window`):

- a regra esta no nucleo, em um lugar so — `Core/Appearance/DockedPanel.cs`
  (qual painel esta ancorado) e `Core/Appearance/PanelPreview.cs` (o que a
  pre-visualizacao mostra);
- a janela principal tem a coluna do painel ancorado e o botao que fixa;
- a linha de comando tem `tranal /painel` (com `/dock` e `/undock`);
- a suite tem 18 testes proprios (`Lote17FeatureTests.cs`) e um teste de
  ponta a ponta no aplicativo de verdade (`tools/test-painel-ancorado.ps1`).

A pre-visualizacao so mostra o numero depois que o painel carregou de
verdade: antes disso, o texto e o nome e a descricao. Mostrar "0 itens"
antes da carga seria mentira.

**IOBIT T7#34 — Painel "Action Center": o estado de cada area e a acao de
cada item.** Esta linha estava descrita como "integracao com a Central de
Acoes do Windows", e a descricao estava **errada**: ela foi inferida do nome
"Action Center" e das strings do binario. A medicao do proprio binario
mostrou o que a feature e — e o detalhe esta em
`EVIDENCIA-CENTRAL-ACoes.md`:

- o painel do IObit e um frame Delphi dentro do aplicativo
  (`TFrameActionCenter8`, `UnitActionCenter8`, `TActionCenterAppList`) que
  lista as areas do **proprio fabricante** com o estado de cada uma
  (instalado, desatualizado, nao instalado) e a acao do item (instalar
  agora, verificar agora, saber mais);
- o Tranal tem a **mesma forma**, com as capacidades do proprio Tranal: a
  **Central** (`Core/Modules/CentralOverview.cs`), a tela
  (`Ui/Modules/CentralView.cs`), o botao na barra lateral, o comando
  `tranal /central`, 14 testes proprios (`CentralFeatureTests.cs`) e um
  teste de ponta a ponta no aplicativo de verdade (`tools/test-central.ps1`).

A diferenca e uma decisao do projeto, e ela esta escrita no modulo: o
painel do IObit e uma vitrine dos produtos dele; o do Tranal lista as
capacidades do Tranal. O Tranal nao tem suite de produtos para vender e nao
vao fazer propaganda de nada.

**TOTALUNINSTALL T5#54, T7#69 e T7#71 — exportar e imprimir a arvore de
alteracoes.** A comparacao entre dois snapshots ja existia, mas morria
como texto numa caixa: nao tinha para onde ir. Agora ela vira uma **tabela**
(tipo, mudanca, item, detalhe) que sai em texto, CSV e HTML, e que e
impressa com a janela de impressao do proprio Windows:

- a regra de impressao mora em um lugar so, no `PrintTable`: a lista de
  programas e a arvore de alteracoes usam o mesmo codigo de pagina e de
  cabecalho;
- a arvore esta no nucleo (`Core/Reporting/ChangeTreeReport.cs`), e a tela
  (aba de snapshots da janela de rastreio) e a linha de comando
  (`tranal /comparar "antes" "depois"`) consomem a mesma;
- a comparacao ganhou um **atalho de biblioteca**: `tranal /snapshots`
  lista e `tranal /snapshots /delete "nome"` apaga.

Dois modulos orfaos ganharam consumidor no mesmo lote: a janela de
impressao (que ninguem abria) e a janela de rastreio (que ninguem abria) —
esta ultima levava o rastreio em tempo real, a politica de processos e a
biblioteca de snapshots. Agora as duas abrem pelo menu principal
("Imprimir lista..." e "Rastreio e comparacao A x B..."), e a impressao tem
atalho (Ctrl+P).

**HIBIT T2#18 e T4#71 — Gerenciador de Menu de Contexto.** A linha do
HiBit ("lista, cria e apaga menus do shell, com \"Restaurar\" e \"Apagar
tudo\"") estava em PARCIAL, e a medicao mostrou que ela estava **errada
desde a origem**: o modulo que a sustentava (`ContextMenuManager`) nunca
funcionou — a leitura abria o caminho do registro duas vezes, e por isso
devolvia **zero** entradas em qualquer maquina. O detalhe esta em
`EVIDENCIA-MENU-CONTEXTO.md`:

- a tela nova (`Ui/Tools/ContextMenuViews.cs`) lista as entradas do Explorer
  e as do Tranal, com o dono de cada uma e o que pode ser feito com ela
  (o botao "Gerenciador de menu de contexto" fica na barra lateral);
- o nucleo (`Core/Management/ContextMenuOverview.cs`) tem **Restaurar**,
  **Desativar**, **Remover**, **Criar entrada** e **Apagar tudo** — e o
  backup das chaves vem antes de qualquer remocao;
- a linha de comando expoe os mesmos verbos (`tranal /contextmenu`, com
  `/restore`, `/disable`, `/remove`, `/create` e `/removeall`);
- a suite tem 49 testes proprios (`Lote20FeatureTests.cs`) e um teste de
  ponta a ponta no aplicativo de verdade (`tools/test-contextmenu.ps1`, 37
  verificacoes).

A medicao na maquina: **41 entradas** na tela — 4 do Tranal e 37 do shell.
Oito delas pertencem a programas (OneDrive, IObit, McAfee) e as outras
sao do Windows, que **nao podem ser removidas** (remover uma quebra o
Explorer).

**ASHAMPOO T4#47 e T4#51 — o StartUp Tuner inteiro e o Crash Analyzer com
export e busca online.** As duas linhas estavam em PARCIAL por causa de
**comandos que faltavam**, nao da regra — e o detalhe esta em
`EVIDENCIA-AUTORUN-CRASH.md`:

- o **StartUp Tuner** ficou completo: habilitar, desabilitar, **adicionar**,
  **editar** e remover. A edicao mora no nucleo (`AutorunManager.Update`) e
  sabe o que cada natureza de item aceita — chave de registro (nome e
  comando), pasta de inicializacao (o arquivo e o executavel), servico (o
  nome de exibicao e o binario) e tarefa agendada (o executavel da acao). O
  item de sistema nao e editado, e o arquivo trocado vai para o backup antes;
- a natureza do item (chave de registro ou pasta) e decidida pelo nucleo, em
  um lugar so (`AutorunManager.ResolveKind`): a tela e a linha de comando
  perguntam, o nucleo responde;
- o **Crash Analyzer** ganhou o **export** (relatorio HTML, com a regra de
  escrita no `ToolReportWriter`, o mesmo de todas as outras ferramentas) e a
  **busca online** (o termo com o nome do programa e a pista do tipo de
  evento, pelo motor configurado);
- a linha de comando expoe os mesmos verbos: `tranal /autorun` (com `/add`,
  `/edit`, `/enable`, `/disable` e `/remove`) e `tranal /crashanalyzer` (com
  `/export` e `/search`);
- a suite tem 26 testes proprios (`Lote21FeatureTests.cs`) e um teste de
  ponta a ponta no aplicativo de verdade (`tools/test-lote21.ps1`, 44
  verificacoes).

A medicao na maquina: o ciclo inteiro foi medido **no registro** — criar (o
valor aparece), editar (o nome novo aparece, o antigo some, o comando novo e o
do valor), desativar (o valor ganha o prefixo do estado), ativar (volta ao
nome normal) e remover (o valor some). A tela do Gerenciador de Autorun
listou **62** itens de inicializacao, e o teste devolveu a maquina ao estado
anterior: **sobrou valor de teste no registro: nao**.

**GEEK T1#2, T1#21, T1#22, T2#35 e T2#44; IOBIT T2#9 e T2#10; WISE T2#33;
ASHAMPOO T1#60 — as acoes do programa na lista.** Nove linhas de uma vez, e
o detalhe esta em `EVIDENCIA-LOTE22-ACOES.md`:

- **Microsoft Store** (o "Ir para a ... Microsoft Store" do Geek): o destino
  e um URI de busca com o nome do programa e a editora. A regra mora em
  `Core/Management/SystemNavigator.cs` — o mesmo lugar que ja conhecia o
  registro e o painel do Windows;
- **Browse Location... e Browse Source...**: a pasta do programa e a pasta do
  desinstalador, ambas no mesmo submenu;
- **"Avaliar on-line"**: a acao que leva a pagina onde o programa pode ser
  avaliado. O endereco e separado da acao (abrir o navegador muda a maquina
  de quem esta usando; o endereco pode ser conferido por teste);
- **o aviso de residuo** (o "still leaves" do IObit): quantos itens o
  desinstalador do fabricante deixa para tras — a contagem e a que a
  varredura mediu;
- **o modo rapido** (o "Quick Uninstall" do IObit): confirmar e seguir. Antes
  era um atalho que rodava o mesmo assistente de cinco etapas; agora e uma
  confirmacao e a cadeia inteira, com a regra do que sai no nucleo
  (`ExecutionPolicy.ItensDoModoRapido`: as sobras marcadas, menos as que
  podem conter dado do usuario);
- **a categoria vazia** (o "no programs in this category" do Geek): a string
  existia, mas nao tinha onde aparecer. Agora a categoria do usuario entra
  na arvore mesmo sem programa, e a tela e a linha de comando dizem que
  nao ha programa nela;
- a linha de comando expoe os mesmos verbos: `tranal /inspection-rate`,
  `tranal /inspection-store` e `tranal /categories`;
- a suite tem 11 testes proprios (`Lote22FeatureTests.cs`) e um teste de
  ponta a ponta no aplicativo de verdade (`tools/test-lote22.ps1`).

Quatro das nove linhas estavam em PARCIAL porque **o RECON mediu errado**:
o total do rodape, a chave `MrtCache` e o botao "Explorar" ja existiam no
codigo. A correcao do registro esta no RECON, com a evidencia medida.


**GEEK T1#11 e T1#15 — as sobras em arvore, com categorias, contagem, busca e
navegacao.** O Geek mostra as sobras em **arvore**, com dois nos ("File System
(%d items)" e "Registry (%d items)") e um comando "Browse..." dentro delas. O
Tranal tinha a categoria por coluna, em uma lista. Agora a arvore existe, e o
detalhe esta em `EVIDENCIA-LOTE23-SOBRAS.md`:

- a regra do agrupamento mora no nucleo, em um lugar so
  (`Core/LeftoverGroups.cs`): as chaves das duas categorias, a contagem, a
  ordem do Geek e a busca que filtra as sobras;
- a arvore e um **controle compartilhado** (`Ui/Shared/LeftoversTree.xaml`),
  usado pelas duas telas que mostram sobras — o assistente de desinstalacao e
  a desinstalacao forcada;
- cada folha mostra a caixa de marcacao, o caminho (em negrito quando a
  confianca e alta) e o detalhe, com o motivo do julgamento;
- o comando "Explorar" abre a pasta do item no Explorer e a chave de registro
  no editor de registro;
- a suite tem 15 testes proprios (`Lote23FeatureTests.cs`) e um teste de
  ponta a ponta no aplicativo de verdade (`tools/test-lote23.ps1`, 9
  verificacoes), que **nao remove nada** — fecha a tela sem clicar em "Remover".

Continua de fora, e continua honesto: a **busca textual nas alteracoes
detectadas** do Total Uninstall (T2#11). A busca deste lote e a das sobras; a
das alteracoes e outra tela (a comparacao de snapshots, que ainda aparece
como texto). Fechar aquela linha e um lote proprio.


**BCU T3#42, T3#44 e T3#46 — a nota das sobras em cinco niveis, os criterios de
pasta e o aviso de perigo.** O BCU mostra o quanto e seguro mexer em cada
sobra, com cinco niveis (Muito bom, Bom, Duvidoso, Mau, Desconhecido), e avisa
quando a pessoa mexe num item ruim. O Tranal tinha tres niveis de confianca
e nenhum aviso ligado a eles. O detalhe esta em
`EVIDENCIA-LOTE24-CONFIANCA.md`:

- a **nota** mora no nucleo, em um lugar so (`Core/LeftoverRatings.cs`): ela
  junta a confianca (tres niveis, que decide o que fica marcado) com o motivo
  do julgamento, e e por isso que o mesmo item pode ser "Bom" ou "Duvidoso"
  conforme o motivo;
- a avaliacao de pasta ganhou os **dois criterios que faltavam** (muitos
  arquivos e sem subdiretorios), e a seguranca foi verificada: a pasta com
  documento do usuario continua na confianca mais baixa, porque o desconto
  por dado do usuario e maior que a soma dos criterios novos;
- o **aviso de perigo** entra quando o usuario marca um item de nota ruim, e
  some quando ele desmarca. Ele fica no painel de avisos da etapa das sobras
  — nao e dialogo;
- a nota aparece na folha da arvore e na saida da linha de comando, com o
  mesmo texto nos dois;
- a suite tem 23 testes proprios (`Lote24FeatureTests.cs`) e um teste de
  ponta a ponta no aplicativo de verdade (`tools/test-lote24.ps1`, 8
  verificacoes), que nao remove nada.

Continua de fora, e continua honesto: os **17 grupos de sobras** do BCU
(T3#49). A tela de sobras e uma arvore com categorias, mas as categorias sao
as do Tranal (sistema de arquivos, registro e as outras), e nao as 17 do
concorrente.


**BCU T2#26 e T2#36 — a fila de desinstalacao na tela, a colisao declarada e o
"rodar em primeiro plano".** A fila existia no nucleo, mas so a linha de
comando a usava: a tela rodava os desinstaladores em um laco solto, sem a
prevencao de colisao e sem a marca de "desinstalacao em andamento". O
detalhe esta em `EVIDENCIA-LOTE25-FILA.md`:

- a **fila** tem uma unica implementacao (a assincrona), e a sincrona delega
  para ela: um desinstalador por vez (a trava e de processo inteiro), a
  colisao declarada, quem nao desinstala em silencio sai da tarefa e a parada
  da tarefa no proximo programa;
- a **marca de operacao** passou a ser tomada: e ela que impede que DUAS
  desinstalacoes rodem ao mesmo tempo na mesma maquina. Antes a guarda
  existia, mas ninguem marcava — e uma guarda que nunca encontra nada nao
  protege nada;
- o **"rodar em primeiro plano"** e o painel do rodape do assistente: quem
  nao desinstalou em silencio aparece la, com o comando que roda o
  desinstalador com a janela dele, um por vez;
- a regra do "ainda instalado" subiu do programa da linha de comando para o
  motor, e a tela e a CLI passam a perguntar ao mesmo lugar;
- a suite tem 14 testes proprios (`Lote25FeatureTests.cs`); suite com 1491
  testes verdes.

A desinstalacao em si **nao** foi medida na maquina: ela e destrutiva, e o
Tranal nao desinstala nada do computador de quem usa sem pedido. O que foi
medido e a regra (por injecao, na suite) e a ligacao da tela (sobre o fonte).

Continua de fora, e continua honesto: o **"Stop current uninstall task"
imediato** do BCU (T2#34). O Tranal nao mata o desinstalador nativo em
execucao — matar o desinstalador no meio deixa o programa meio desinstalado, e
um programa meio desinstalado e pior do que um programa desinstalado. A
tarefa inteira para no proximo programa, e o que ja rodou termina.


## Como fechar

A linha que resta fecha com **uma** decisao: empacotar o Tranal em MSIX.
E uma decisao de produto (assinatura, instalador, distribuicao), nao de
codigo — e por isso que ela esta registrada aqui, e nao no meio do codigo,
onde pareceria um esquecimento.
