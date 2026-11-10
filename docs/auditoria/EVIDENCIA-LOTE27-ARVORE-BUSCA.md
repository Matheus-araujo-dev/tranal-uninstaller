# Evidencia do lote 27: a arvore de alteracoes detectadas e a busca dentro dela

Medido em 09/11/2026, no codigo de `src/Tranal.Uninstaller/` e
`src/Tranal.Cli/`, na suite de testes e no aplicativo de verdade.

Este lote fecha **duas** linhas do RECON (a **arvore de alteracoes
detectadas** e a **busca textual** das alteracoes, ambas do Total Uninstall) e
corrige **tres** linhas que mediram errado a **exportacao** e a **impressao**
da arvore — as duas ja existiam, e o RECON media de uma versao antiga.

## 1. As linhas

| Recorrencia | Funcionalidade | Antes | Agora |
|---|---|---|---|
| TOTALUNINSTALL T2#22 (RECON-D) | **Arvore de alteracoes detectadas** com arquivos, pastas, chaves e valores de registro | PARCIAL | OK |
| TOTALUNINSTALL #22 (RECON-ASHAMPOO-TOTALUNINSTALL) | **Arvore de mudancas** com arquivos, pastas, chaves e valores | PARCIAL | OK |
| TOTALUNINSTALL T2#11 (RECON-D) | **Busca textual** nas alteracoes detectadas | PARCIAL | OK |
| TOTALUNINSTALL #69 (RECON-ASHAMPOO-TOTALUNINSTALL) | **Exportar a arvore de mudancas** | PARCIAL | OK |
| TOTALUNINSTALL #71 (RECON-ASHAMPOO-TOTALUNINSTALL) | **Imprimir a arvore de alteracoes** | PARCIAL | OK |

As tres ultimas eram medicao desatualizada: o RECON falava de um
`PrintLayout.cs` que nao existe mais e de um `grep PrintTree` que dava zero
porque o botao de imprimir da arvore se chama `PrintChangeTreeButton_Click`.
A exportacao e a impressao da arvore **existem** e foram medidas de novo no
aplicativo de verdade.

Cobertura: de **76,0%** (725 funcionalidades, 521 OK, 173 PARCIAL, 1 AUSENTE,
30 FORA) para **76,3%** (725, 523 OK, 171 PARCIAL, 1 AUSENTE, 30 FORA). Na
prova de cobertura com o Revo Pro dentro (`PARIDADE-11-CONCORRENTES.md`), de
**78,1%** (795 funcionalidades, 589 OK) para **78,4%** (795, 591 OK). O
TOTALUNINSTALL subiu de 72,3% para **74,5%**.

## 2. A regra: a classificacao mora em um lugar so

Antes, a comparacao A x B tinha **duas** apresentacoes independentes: a
**tabela** (exportacao e impressao, com uma linha por alteracao) e a
**descricao em texto** (`DescreverComparacao`). A tabela e a arvore podiam
divergir: o que se lia na tela nao era o que saia no arquivo.

Agora:

- a **classificacao** (que tipo de item, que mudanca, qual caminho/chave/
  valor) mora em `Core/Reporting/ChangeTreeReport.cs:86` — `Items`. E a
  **fonte unica**;
- a **tabela** deriva dela: `Rows` (`:68`) e `Table` (`:132`);
- a **arvore** deriva dela tambem: `Core/Reporting/ChangeTreeNodes.cs:86` —
  `Build` monta os nos a partir da comparacao.

O que se le na tela e o que sai no arquivo sao **as mesmas alteracoes**.

A estrutura do no mora no nucleo (`Core/Reporting/ChangeTreeItem.cs`:
`ChangeTreeChange` em `:6`, `ChangeTreeCategory` em `:21` e `ChangeTreeItem`
em `:44`). O **texto** e decidido por quem mostra:

- a interface traduz pelo catalogo (`Core/Localization/ChangeTreeLabels.cs`:
  `Category` em `:19`, `Label` em `:36`, `Change` em `:63`);
- a linha de comando escreve em portugues no codigo
  (`src/Tranal.Cli/ExtraCommands.cs:1582`, `TituloDoNo`).

## 3. A hierarquia e a busca

A arvore tem tres niveis: **categoria** (arquivos, pastas, registro, valores
de registro), **grupo de mudanca** (novo, modificado, removido) e a
**alteracao** (o caminho, a chave, o valor).

A busca mora em `Core/Reporting/ChangeTreeNodes.cs:137` — `Filter`:

- a busca **preserva a hierarquia**: um no so continua na arvore quando ele
  tem um filho que casa. Filtrar por "novo.dll" mostra
  `Arquivos > Novos > C:\Programa\novo.dll`, e nao a folha solta;
- a busca **diz quantas sobraram**: a tela mostra "1 de 6 alteracao(oes)" e a
  linha de comando mostra "Alteracoes: 1 de 6".

`Total` (`:172`) conta e `Describe` (`:209`) escreve a arvore em texto.

## 4. A tela

A arvore esta na aba **Snapshots** da janela de rastreio
(`src/Tranal.Uninstaller/Ui/Tracing/TracingWindow.xaml`):

- `ChangeTreeView` (`:203`) — a arvore;
- `ChangeTreeSearchBox` (`:194`) — a busca;
- `ChangeTreeCountText` (`:198`) — a contagem ("1 de 6").

A tela monta os nos (`PreencherArvore`, `TracingWindow.xaml.cs:680`) e cada
no vira um item (`NoVisual`, `:714`). A busca dispara
`ChangeTreeSearchBox_TextChanged` (`:708`), que refaz a arvore. **A tela nao
sabe montar arvore nem filtrar** — ela so mostra o que o nucleo devolveu. E o
que impede a tela de contar uma alteracao diferente da que a linha de comando
exporta.

## 5. A linha de comando

`tranal /comparar "antes" "depois"` ganhou duas opcoes:

- `/arvore` — a arvore em texto, com a hierarquia (as mesmas nos do nucleo);
- `/busca "termo"` — a arvore filtrada, com a contagem do que sobrou.

O codigo esta em `src/Tranal.Cli/ExtraCommands.cs` (`/busca` em `:1555`,
`TituloDoNo` em `:1582`).

## 6. Os testes

`src/Tranal.Tests/Lote27FeatureTests.cs` tem **17** testes proprios:

- a arvore tem **quatro categorias** (arquivos, pastas, registro, valores);
- cada categoria tem o grupo de mudanca e a contagem;
- a alteracao aparece com o caminho (ou a chave) e o detalhe;
- a **tabela** e a **arvore** vem da mesma fonte (`ChangeTreeReport.Items`);
- a busca **preserva a hierarquia** (um no sem filho que casa sai);
- a busca devolve a contagem do que sobrou;
- a busca **nao distingue maiuscula de minuscula**;
- a busca vazia devolve a arvore inteira;
- a arvore em texto sai com a indentacao da hierarquia;
- a rotulo vem do **catalogo** (e nao do codigo) na interface;
- a linha de comando tem `/arvore` e `/busca`;
- as chaves novas estao **nos dois idiomas**, com as mesmas chaves.

A suite inteira: **1525 testes verdes** (era 1508).

## 7. O teste de ponta a ponta

`tools/test-lote27.ps1` roda o aplicativo de verdade e a linha de comando
publicada, e prova **21** coisas:

1. a arvore mostra a categoria de arquivos, a de pastas, a de registro e a de
   valores de registro;
2. a arvore mostra o grupo de mudanca e o item com a hierarquia;
3. a busca filtra a arvore, mostra o item procurado e esconde o que nao casa;
4. a janela principal do Tranal aparece e a janela de rastreio abre;
5. a lista mostra os dois snapshots (o de antes e o de depois);
6. os dois snapshots ficam marcados ao mesmo tempo;
7. a comparacao termina com "Comparacao concluida";
8. a arvore da tela tem nos, mostra a categoria e mostra o item novo;
9. a tela conta as alteracoes ("6 alteracao(oes)");
10. a busca da tela filtra a arvore, mostra o item procurado, esconde o que
    nao casa e a tela conta o que sobrou ("1 de 6 alteracao(oes)").

**Nada de software instalado e tocado.** O teste escreve dois snapshots
**sinteticos** (no formato que o proprio Tranal grava) e apaga tudo no final.

## 8. O que NAO foi medido, e por que

Nenhum concorrente pago foi executado: a comparacao e de **funcionalidade**,
nao de qualidade, velocidade ou taxa de deteccao. O que foi medido:

- a regra, na suite (os 17 testes acima);
- a ligacao com a tela e com a linha de comando, no aplicativo de verdade
  (`tools/test-lote27.ps1`), com snapshots sinteticos.

A regra do projeto e a mesma de sempre: **a regra fica em um lugar so e o
resto consome**. A classificacao das alteracoes esta em
`Core/Reporting/ChangeTreeReport.cs` e a hierarquia e a busca em
`Core/Reporting/ChangeTreeNodes.cs` — e a tela, a linha de comando e a suite
consomem os dois.
