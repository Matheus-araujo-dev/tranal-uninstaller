# Lacunas restantes (medido em 09/11/2026)

Este documento lista a **uma** funcionalidade que ainda nao existe no
Tranal, entre as 725 funcionalidades medidas contra os 11 concorrentes.

Cada linha diz **o que falta** e **por que** ela continua de fora. Nenhuma
linha aqui esta marcada como "OK": a medicao e o que vale, e medir e dizer
"ok" sem o codigo seria mentira.

O numero da cobertura esta em `PARIDADE-11-CONCORRENTES.md`
(74,9% — 725 funcionalidades, 513 OK, 181 PARCIAL, 1 AUSENTE, 30 FORA por
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


## Como fechar

A linha que resta fecha com **uma** decisao: empacotar o Tranal em MSIX.
E uma decisao de produto (assinatura, instalador, distribuicao), nao de
codigo — e por isso que ela esta registrada aqui, e nao no meio do codigo,
onde pareceria um esquecimento.
