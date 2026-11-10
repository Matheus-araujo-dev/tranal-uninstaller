# Evidencia do lote 23: as sobras em arvore, com categorias, contagem, busca e navegacao

Medido em 09/11/2026, no codigo de `src/Tranal.Uninstaller/`, na suite de
testes e no aplicativo publicado (`publish/preview`), na janela de verdade.

Este lote fecha **duas** linhas do RECON do GEEK que estavam em PARCIAL e
AUSENTE: a **arvore** das sobras, com as duas categorias e a contagem de
itens, e o comando **"Browse..."** dentro delas.

## 1. As duas linhas

| Recorrencia | Funcionalidade | Antes | Agora |
|---|---|---|---|
| GEEK T1#11 | Assistente de desinstalacao com sobras em **2 categorias**: File System (%d items) e Registry (%d items), em **arvore** | PARCIAL | OK |
| GEEK T1#15 | "Browse..." dentro das sobras encontradas | AUSENTE | OK |

A terceira linha que mudou e a mesma capacidade vista pelo outro angulo
(no `RECON-HIBIT-GEEK-IOBIT-UNINSTALR.md`, "navegar para o item"), que
estava em "NAO EXISTE" e agora esta em "JA EXISTE".

Cobertura: de **74,2%** (725 funcionalidades, 508 OK, 186 PARCIAL, 1 AUSENTE,
30 FORA) para **74,5%** (725, 510 OK, 184 PARCIAL, 1 AUSENTE, 30 FORA). Na
prova de cobertura com o Revo Pro dentro (`PARIDADE-11-CONCORRENTES.md`), de
**76,5%** (795 funcionalidades, 576 OK) para **76,7%** (795, 578 OK).

## 2. A regra mora no nucleo

`Core/LeftoverGroups.cs` e o **unico** lugar que sabe como as sobras se
agrupam:

- `LeftoverGroup` (`:16`) e o grupo: a chave da categoria, o nome no idioma
  ativo e os itens;
- `Build` (`:51`) monta os grupos a partir da lista de sobras, e so as
  categorias **com item** entram (uma categoria vazia nao e uma categoria);
- `CategoryOf` (`:111`) decide a categoria de cada sobra: "Registro" vai
  para a chave `registry` (`:41`) e "Pasta"/"Arquivo" para `filesystem`
  (`:38`). Uma categoria que o Tranal ainda nao conhece aparece com o nome
  do proprio tipo — melhor um nome cru do que um item sem categoria;
- `OrderOf` (`:136`) fixa a ordem do Geek: sistema de arquivos, registro e
  as outras categorias, pela ordem em que apareceram;
- `Count` (`:22`) e a contagem que vai no cabecalho do no.

A **busca** tambem mora no nucleo: `Filter` (`:90`) casa o texto procurado
com o caminho, com o detalhe e com o tipo, sem distinguir maiuscula de
minuscula; texto vazio devolve tudo, que e o que a busca apagada significa.

A interface grafica e a linha de comando consomem esses grupos e nunca
montam os seus.

## 3. A tela, compartilhada pelas duas que mostram sobras

`Ui/Shared/LeftoversTree.xaml` e `Ui/Shared/LeftoversTree.xaml.cs` sao o
**mesmo** controle nas duas telas que mostram sobras:

- o assistente de desinstalacao (`Ui/UninstallWizardWindow.xaml:169`);
- a desinstalacao forcada (`Ui/ForcedUninstallWindow.xaml:36`).

O que a arvore faz:

- um no por categoria, com o titulo e a contagem no cabecalho
  (`LeftoversTree.xaml.cs:83`) — o "File System (%d items)" do Geek;
- uma folha por sobra, com a caixa de marcacao, o caminho (em negrito
  quando a confianca e alta) e o detalhe (tipo, tamanho, confianca e o
  **motivo** do julgamento) (`FolhaDe`, `:104`);
- a busca, que filtra a arvore sem mexer na lista original (`:183`);
- o comando **"Explorar"** (`BrowseButton_Click`, `:204`): a pasta do item
  abre no Explorer (`ExternalLaunchers.OpenFolder`, `:216`) e a chave de
  registro abre no editor de registro (`SystemNavigator.Open` com
  `regedit.exe`, `:211`). O botao so fica habilitado com uma folha
  selecionada (`:96` e `:193`).

`LeftoverItem` nao avisa mudanca de propriedade, entao a arvore e
**reconstruida** (`Refresh`, `:66`) quando a marcacao muda — e a caixa de
marcacao acompanha a escolha depois disso.

## 4. Medido na janela de verdade

`tools/test-lote23.ps1` abre o aplicativo publicado, entra na desinstalacao
forcada, analisa uma pasta temporaria e mede a arvore pela propria interface
(UI Automation). As **9 verificacoes** passaram:

1. a janela principal do Tranal aparece;
2. o botao da desinstalacao forcada existe;
3. a tela de desinstalacao forcada abre;
4. a tela mostra a arvore das sobras;
5. a arvore mostra a categoria com a contagem de itens — o no medido foi
   **"Sistema de arquivos (1)"**;
6. a arvore tem a busca;
7. a busca que nao casa esvazia a arvore;
8. a busca apagada devolve os itens;
9. a arvore tem o comando de explorar.

O teste **fecha a tela sem clicar em "Remover"**: nada e apagado da maquina.
A pasta de teste e apagada no final.

## 5. Os testes

`src/Tranal.Tests/Lote23FeatureTests.cs` tem **15** testes proprios: o
agrupamento, a ordem (sistema de arquivos antes do registro), a categoria
desconhecida, a categoria vazia, o titulo vindo do catalogo, a busca por
caminho, por detalhe e por tipo, a busca vazia, a lista sem sobras, a
ligacao com as duas telas, a arvore com busca e explorar, e a existencia das
chaves novas nos dois idiomas.

A suite inteira: **1454 testes verdes** (era 1439).

## 6. O que continua de fora

**TOTALUNINSTALL T2#11 — "Busca textual nas alteracoes detectadas"**
continua em PARCIAL, e continua honesto: a busca que este lote entregou e a
busca **das sobras** (a arvore do assistente e da desinstalacao forcada).
A busca do Total Uninstall e sobre as **alteracoes detectadas** na
comparacao de snapshots (arquivos, pastas, chaves e valores), que e outra
tela — a aba de snapshots da janela de rastreio, onde a comparacao ainda
aparece como texto. Fechar aquela linha e um lote proprio, com a arvore das
alteracoes e a busca nela.

A regra do projeto e a mesma de sempre: **a regra fica em um lugar so e o
resto consome**. O agrupamento das sobras esta em `Core/LeftoverGroups.cs`,
e as duas telas que mostram sobras consomem o mesmo controle.
