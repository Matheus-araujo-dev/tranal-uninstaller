# Evidencia medida - a Central de Acoes do Windows e o "Action Center" do IObit

**Medido em 09/11/2026**, no Windows 11 da maquina de desenvolvimento.

Este documento existe por causa de uma **correcao**: a linha IOBIT T7#34 foi
descrita, ate aqui, como "integracao com a Central de Acoes do Windows". A
medicao mostrou que essa leitura estava errada — e o que era certo esta
escrito no fim.

## 1. A medicao do Windows

### A pergunta

O Tranal tem as duas coisas: as notificacoes internas (o sino) e o icone na
bandeja, que produz o aviso no balao do proprio Windows. A pergunta era se o
balao da bandeja do Tranal chega na Central de Acoes do Windows.

### O que foi feito

Dois programas de teste, versionados em `tools/auditoria/probes/`:

1. **`probe-wpn`** — abre o banco de notificacoes do proprio Windows
   (`%LOCALAPPDATA%\Microsoft\Windows\Notifications\wpndatabase.db`) pela
   `winsqlite3.dll` do sistema e conta as notificacoes. E o banco onde a
   Central de Acoes guarda o que o usuario ve.
2. **`probe-balao2`** — registra um AppUserModelID, cria um icone na bandeja e
   dispara um balao de verdade, pela mesma API que o Tranal usa
   (`Core/Management/SystemTray.cs`).

A medicao e a contagem do banco **antes** e **depois** do balao, no banco
real do Windows.

### O que o Windows respondeu ao balao

```
SetCurrentProcessExplicitAppUserModelID -> 0x00000000
Shell_NotifyIcon(ADD) -> True
Shell_NotifyIcon(SETVERSION) -> True
Shell_NotifyIcon(MODIFY com NIF_INFO) -> True
esperando 8 segundos para o shell processar...
```

Tudo aceito: o AUMID foi registrado e o balao foi pedido, com `NIF_INFO`.

### O que o banco da Central de Acoes mostrou

Antes do balao:

```
banco: C:\Users\<usuario>\AppData\Local\Microsoft\Windows\Notifications\wpndatabase.db (existe: True)
--- contagem ---
  notificacoes=27 maiorOrder=1336
```

Depois do balao (8 segundos depois):

```
banco: C:\Users\<usuario>\AppData\Local\Microsoft\Windows\Notifications\wpndatabase.db (existe: True)
--- contagem ---
  notificacoes=27 maiorOrder=1336
```

**27 antes, 27 depois, o mesmo `Order` maximo.** Nenhuma notificacao nova
entrou na Central de Acoes.

A medicao foi refeita de ponta a ponta nesta rodada. O numero absoluto (27)
depende do que o Windows ja tinha guardado e muda com o tempo — o que prova
o ponto e a **igualdade** entre as duas contagens e o `Order` maximo
inalterado.

**Conclusao da medicao:** o balao da bandeja (`Shell_NotifyIcon` com
`NIF_INFO`) **nao** vira notificacao da Central de Acoes.

## 2. A correcao: o "Action Center" do IObit nao e a Central de Acoes do Windows

Um balao que nao chega na Central de Acoes nao é, por si so, um problema: o
Windows **tem** um caminho para notificacoes de aplicativo de desktop (o
atalho no Menu Iniciar com `System.AppUserModel.ID`, documentado em
"enable-desktop-toast-with-appusermodelid"). Entao a medicao nao provava
que a linha fosse impossivel — provava que a **leitura** da linha estava
errada.

E o que a leitura do arquivo de idiomas do concorrente mostra (o
`lng-english-plano.txt`, secao `[ActionCenter_M]`):

```
M_HideAC=Hide
M_ShowAC=Show
M_AC_Tips=Get better security protection and performance boost with recommended programs.
M_Fix=Install now
M_LearnMore=Learn more
M_RuningWell=Latest version installed
M_Outdated=Outdated
M_NotInstalled=Not installed
M_Insatlled=Installed
M_Update=Update now
M_ASCCaption=System Performance
M_IMFCaption=Malware Protection
M_DBCaption=Driver Updater
M_SDCaption=Disk Optimization
M_SUCaption=Software Updater
```

E o painel e um **frame Delphi dentro do aplicativo**:
`TFrameActionCenter8`, `UnitActionCenter8`, `TActionCenterAppList.FAppList.Add`,
`btn_ActionCenter`, `Nav_Colle_AC=Action Center`. A janela que o acompanha e
o downloader do proprio IObit (`[ACD_frmActionCenter]`,
`lblCaption.Caption=IObit Downloader`, com velocidade, progresso e
verificacao de MD5).

**O "Action Center" do IObit e um painel interno que lista as areas do
proprio fabricante (Advanced SystemCare, Malware Fighter, Driver Booster,
Smart Defrag, IObit Software Updater, os navegadores e o resto da suite),
com o estado de cada uma (instalado, desatualizado, nao instalado) e a acao
do item (instalar agora, verificar agora, saber mais).**

Nao ha, no binario do IObit, integracao com a Central de Acoes do Windows.

## 3. O que mudou na auditoria

- A linha **IOBIT T7#34** foi corrigida: a descricao "integracao com a
  Central de Acoes do Windows" era inferida do nome "Action Center" e estava
  errada. A descricao medida e "**Painel 'Action Center'**: o estado de cada
  area e a acao de cada item, em um painel so".
- Com a descricao certa, a linha deixa de depender de empacotamento MSIX: o
  Tranal tem a **mesma forma** (itens com estado e acao), e a acao leva a
  ferramenta do Tranal que age sobre o item. Isso e a **Central**
  (`Core/Modules/CentralOverview.cs`).
- O que difere do concorrente, e e uma decisao do projeto: o painel do IObit
  e uma vitrine dos produtos do proprio fabricante. O do Tranal lista as
  **capacidades do Tranal** (atualizacoes, programas novos, saude, o proprio
  Tranal). O Tranal nao tem suite de produtos para vender e nao vai fazer
  propaganda de nada.

A regra da auditoria e "medido vale. Inferido nao vale" — e a medicao foi o
que mostrou que a inferencia anterior estava errada.

## 4. O que o Tranal passou a ter (medido no aplicativo de verdade)

A Central e a adaptacao do painel do IObit. O que existe hoje:

- o nucleo, em `Core/Modules/CentralOverview.cs`: junta as quatro areas do
  Tranal (atualizacoes de programas, programas novos, saude do sistema e a
  propria versao do Tranal) em uma lista so, ordenada por prioridade;
- a tela, em `Ui/Modules/CentralView.cs`, com as colunas item, area,
  prioridade e detalhe, e dois comandos (recarregar e abrir a ferramenta que
  age sobre o item);
- o botao na barra lateral (`ModuleCentral`) e a linha de comando
  `tranal /central`;
- 14 testes proprios na suite (`CentralFeatureTests.cs`) e um teste de ponta
  a ponta no aplicativo de verdade (`tools/test-central.ps1`).

### O que o teste de ponta a ponta mediu

No aplicativo publicado, pela propria automacao de interface do Windows:

```
[OK]    a janela principal do Tranal aparece
[OK]    o botao da Central existe na barra lateral
[OK]    a tela da Central abriu (comando de recarregar)
[OK]    a tela da Central tem o comando de abrir a ferramenta
[OK]    o titulo do painel e o da Central
[OK]    a lista da Central existe
[OK]    a Central carregou (a linha de status chegou ao resumo)
[OK]    a Central mostra as colunas de prioridade e detalhe
[OK]    a lista mostra os itens que o resumo anuncia
[OK]    nenhuma excecao nova no log de inicializacao
    itens na Central: 62
    colunas: Item, Area, Prioridade, Detalhe
    status final: 152 item(ns) precisam de atencao
```

Duas coisas foram **medidas** aqui e nao presumidas:

1. os itens da lista aparecem na automacao de interface como
   `ControlType.DataItem` (e nao `ListItem`), porque o painel usa `GridView`
   — a primeira versao do teste procurava `ListItem` e contava zero;
2. o painel passa por tres textos de carregamento ("Verificando...",
   "Carregando...", "Loading...") antes do resumo. O teste espera pelo
   **resumo** ("N item(ns) precisam de atencao"), e nao por "deixou de
   carregar": aceitar "Carregando..." seria um falso positivo — e foi
   exatamente o que a primeira versao do teste fez.
