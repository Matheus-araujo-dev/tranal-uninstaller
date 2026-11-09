# Evidencia do lote 22: as acoes do programa na lista

Medido em 09/11/2026, no codigo de `src/Tranal.Uninstaller/` e
`src/Tranal.Cli/`, no aplicativo publicado (`publish/preview`) e na linha de
comando publicada (`publish/cli`).

Este lote fecha **nove** linhas do RECON que estavam em PARCIAL ou AUSENTE.
Quatro delas estavam **erradas desde a origem**: o RECON afirmava que
faltava algo que ja existia no codigo — e a medicao (no codigo, na tela e na
linha de comando) e o que corrige o registro. As outras cinco eram
funcionalidades que **de fato** nao existiam, e agora existem com a regra no
nucleo, o comando na tela e o comando na linha de comando.

## 1. As nove linhas

| Recorrencia | Funcionalidade | Antes | Agora |
|---|---|---|---|
| GEEK T1#2 | Total agregado no rodape ("%d programs of %s size in total") | PARCIAL | OK |
| GEEK T1#21 | Ir para a chave do registro, a pasta de instalacao, o site do programa e a Microsoft Store | PARCIAL | OK |
| GEEK T1#22 | Browse Location... e Browse Source... | PARCIAL | OK |
| GEEK T2#35 | Limpeza do cache MRU do Windows (`Software\Classes\Local Settings\MrtCache`) | PARCIAL | OK |
| GEEK T2#44 | "Nenhum programa nesta categoria" | PARCIAL | OK |
| IOBIT T2#9 | Quick Uninstall: confirmar automaticamente o desinstalador original | PARCIAL | OK |
| IOBIT T2#10 | Aviso de que o desinstalador vai deixar residuo ("still leaves") | PARCIAL | OK |
| WISE T2#33 | Botao "Explorar" para apagar o que for encontrado | PARCIAL | OK |
| ASHAMPOO T1#60 | Busca online do programa e "Rate online" | PARCIAL | OK |

Cobertura: de **73,0%** (725 funcionalidades, 499 OK, 195 PARCIAL, 1 AUSENTE,
30 FORA) para **74,2%** (725, 508 OK, 186 PARCIAL, 1 AUSENTE, 30 FORA). Na
prova de cobertura (`PARIDADE-11-CONCORRENTES.md`, com o Revo Pro dentro), de
**75,3%** (795 funcionalidades, 567 OK) para **76,5%** (795, 576 OK).

## 2. As acoes do programa (GEEK T1#21, T1#22 e ASHAMPOO T1#60)

**O nucleo.** `Core/Management/SystemNavigator.cs` e o unico lugar que sabe o
comando de cada destino do Windows. O lote acrescentou tres destinos:

- `MicrosoftStore` (`:116`) — o endereco da loja e um URI
  (`ms-windows-store://search/?query=`) e a busca leva o nome do programa e a
  editora, sem repetir a editora quando o nome ja a traz;
- `ProgramFolder` (`:138`) — a pasta de instalacao, quando ela existe no
  disco (e o "Browse Location..." do Geek);
- `UninstallerFolder` (`:155`) — a pasta do desinstalador, tirada do comando
  de desinstalacao (e o "Browse Source..." do Geek).

Os tres entram em `TargetsFor` (`:61`), que e o que a tela e a linha de
comando consomem: ninguem monta o comando por conta propria.

O "Avaliar on-line" (ASHAMPOO T1#60) mora em
`Core/Tools/SystemActions.cs`: `AvaliarOnlineUrl` (`:92`) monta o endereco e
`AvaliarOnline` (`:111`) abre. O endereco e separado da acao porque abrir o
navegador muda a maquina de quem esta usando, e um teste nao pode fazer
isso — o endereco, sim, pode ser conferido.

**A tela.** `MainWindow.xaml.cs` (`:1705`) monta o submenu "Abrir no
Windows" a partir de `SystemNavigator.TargetsFor`, e o item "Avaliar on-line"
(`:1717`) chama `AvaliarPrograma` (`:1628`).

**A linha de comando.** `tranal /inspection-rate "nome"` e
`tranal /inspection-store "nome"` (`src/Tranal.Cli/InspectionCommands.cs`:
`:477` e `:519`). A busca do programa mora em um lugar so
(`LocalizarPrograma`, `:539`) — a pagina do fabricante, a avaliacao e a loja
procuram do mesmo jeito.

**Defeito real encontrado no caminho.** O identificador do item de menu era
derivado do nome do executavel (`"CmdOpen" + alvo.FileName`). Com dois
destinos no Explorer (a pasta do programa e a pasta do desinstalador), os
dois iam receber o mesmo identificador, e o da loja viraria o proprio
endereco da busca. O destino agora carrega o identificador proprio
(`SystemNavigationTarget.MenuId`), e ha teste para isso.

## 3. O aviso de residuo (IOBIT T2#10)

`Core/UninstallAdvisories.cs` tem agora `LeavesResidueAdvisory` (`:175`): o
aviso que diz **quantos** itens o desinstalador do fabricante deixa para
tras. Ele entra em `Build` (`:160`) junto dos avisos do Revo, e o texto mora
em `Core/Localization/AdvisoryLabels.cs` (`:80`).

A contagem e a que a varredura mediu: as sobras **marcadas** (as que a
varredura vao remover). O que o Tranal limpa depois e o que a propria varredura
achou — o aviso nao e palpite.

## 4. O modo rapido (IOBIT T2#9) — confirmar e seguir

O RECON dizia que faltava o "confirmar e seguir" do IObit. A medicao
mostrou que o que existia era um atalho que rodava o **mesmo** assistente de
cinco etapas: nao era confirmar e seguir.

Agora o modo rapido e o verbo de verdade:

- `Core/Automation/ExecutionPolicy.cs` — `ItensDoModoRapido` (`:150`) e a
  regra do que sai: as sobras marcadas, **menos** as que podem conter dado do
  usuario (documento, foto, trabalho). Sem revisao item a item, essa e a
  protecao — e ela mora no nucleo, nao na tela;
- `Ui/UninstallWizardWindow.xaml.cs` — `RunQuickAsync` (`:474`): uma
  confirmacao (os avisos que precedem a desinstalacao) e a cadeia inteira
  (desinstalador nativo, varredura, limpeza), sem etapa no meio. O resumo
  fica na tela e o "Concluir" fecha;
- o menu e o atalho `Shift+Delete` apontam para o mesmo verbo.

## 5. A categoria vazia (GEEK T2#44)

O RECON dizia que nao havia string dedicada. Havia agora — e, para nao ser
uma frase morta, ela tem onde aparecer:

- `Core/Discovery/ProgramCategoryTree.cs` — `Build` recebe as categorias do
  usuario e as coloca na arvore **mesmo quando ainda nao tem programa
  nelas** (a categoria sem programa sumiria da arvore, e nao haveria o que
  dizer);
- `Ui/Tools/ToolViews.cs` — a tela de Categorias mostra a mensagem na
  coluna dos programas quando a categoria esta vazia;
- `src/Tranal.Cli/ShellCommands.cs` — `tranal /categories` mostra a mesma
  mensagem.

## 6. As quatro remedeicoes (o RECON estava errado)

Estas linhas estavam em PARCIAL **por causa de uma medicao errada**. O que a
medição no codigo, na tela e na linha de comando mostrou:

- **GEEK T1#2** (total agregado no rodape): o total ja era calculado no
  nucleo (`ProgramTotalsCalculator`, `Core/Modules/RelativeDate.cs:147`) e
  ja era mostrado no rodape (`MainWindow.xaml.cs:411` e `:413`) — a nota do
  RECON ("so e testado — o rodape ainda mostra so a contagem") nao era
  verdade;
- **GEEK T2#35** (cache MRU): a chave `Software\Classes\Local Settings\
  MrtCache` **esta** na lista que o Tranal limpa
  (`Core/Tools/MruCleaner.cs:62`) — a nota do RECON ("a busca por MrtCache
  nao retorna hits") nao era verdade;
- **IOBIT T2#9** (Quick Uninstall): o menu e o atalho existiam
  (`MainWindow.xaml.cs:1120`) — o que nao existia era o *comportamento* do
  "confirmar e seguir", e ele foi implementado (secao 4);
- **WISE T2#33** (botao "Explorar"): a tela da desinstalacao forcada tem o
  botao (`Ui/ForcedUninstallWindow.xaml:28`,
  `Ui/ForcedUninstallWindow.xaml.cs:57`) — a nota do RECON ("nao ha o botao")
  nao era verdade.

## 7. O que foi medido na maquina

`tools/test-lote22.ps1` (novo, **20 verificacoes, todas verdes**): o teste de
ponta a ponta no aplicativo de verdade e na linha de comando de verdade. A suite de testes cobre o nucleo;
esse script cobre a ligacao com a interface e com a CLI, que a suite nao
alcanca (a suite nao referencia o projeto WPF).

O script **nao remove nada**: o modo rapido e aberto e cancelado. A categoria
de teste e apagada no final, mesmo se o teste falhar, e o
`program-data.json` existente e guardado antes e devolvido no fim.

Medido na janela:

- a lista de programas carregou com **78** programas;
- o menu de contexto do programa tem **"Abrir no Windows"** e **"Avaliar
  on-line"**;
- o submenu "Abrir no Windows" tem **"Abrir na Microsoft Store"** e **"Abrir
  a pasta do programa"**;
- a tela de Categorias tem a linha
  `TranalTesteCategoriaVazia | 0 | Nenhum programa nesta categoria`;
- o modo rapido abre o assistente com o botao **"Desinstalar agora"**, e o
  Cancelar fecha sem remover nada.

Medido na linha de comando:

- `tranal /categories` lista a categoria vazia com a mensagem;
- `tranal /inspection-rate "programa-que-nao-existe-xyz"` falha com o motivo
  (codigo 1);
- `tranal /inspection-store "programa-que-nao-existe-xyz"` falha com o motivo
  (codigo 1).

## 8. A suite

`src/Tranal.Tests/Lote22FeatureTests.cs` (novo): **32 testes** proprios do
lote — a loja (4), as pastas (3), os destinos e os identificadores de menu
(4), o aviso de residuo (3), o endereco de avaliacao (2), as remedeicoes
lidas no fonte (5), o modo rapido (5) e a categoria vazia (3). Com o lote 21,
a suite vai de 1407 para **1439** testes, todos verdes.
