# Evidencia - Lote 35: a lista que a pessoa usa todo dia (2026-11-10)

O tema do lote e um so: **a lista que a pessoa usa todo dia**.

Duas funcionalidades do Uninstall Tool estavam PARCIAL porque a memoria da
grade nao existia e o realce dos programas recem-instalados nao existia. O
lote constroi as duas - e, medindo a lista na tela, encontra um defeito que
era **anterior** ao lote.

## 1. O defeito que a medicao encontrou (anterior ao lote)

**O que foi medido.** Com o programa aberto e a lista na tela, o cabecalho
"Tamanho" estava sobre o **nome do programa**, e a coluna do nome tinha a
largura da coluna de tamanho. O defeito nao era do cabecalho: era do **mapa**
que liga cada coluna da grade a coluna do catalogo.

**A causa.** A coluna das caixas de selecao (o "Show check boxes in Details
view", Revo 15334) e um **comando**, nao uma coluna do catalogo. Ela entra na
grade antes de todas, e o mapa a contava como se fosse uma coluna do
catalogo. Resultado: cada coluna recebia o nome da seguinte e a largura da
seguinte - e a memoria de larguras que este lote grava saia contaminada com
os nomes trocados.

**A correcao.** A regra do mapa esta no nucleo, em um lugar so:

- `Core/Appearance/ProgramListLayout.cs` — `CommandColumns` (`:48`): quantas
  colunas da grade vem antes das colunas do catalogo;
- `Core/Appearance/ProgramColumnMemory.cs` — `IdAt` (`:87`): o identificador
  da coluna na posicao dada.

A janela consome a regra e nao decide a conta
(`MainWindow.xaml.cs`, `BuildProgramColumns`, `:644`).

**Como foi provado.** O teste de ponta a ponta (`tools/test-lote35.ps1`, 14
verificacoes no aplicativo de verdade) abre o programa, confere que o
cabecalho da coluna do nome e o rotulo do nome (do catalogo de idiomas), e
confere que o texto da celula esta **embaixo** do cabecalho dele. Antes da
correcao, o cabecalho da primeira coluna era "Tamanho".

## 2. A memoria da grade (UNINSTALLTOOL T1#4)

- **A regra mora no nucleo**, em um lugar so:
  `Core/Appearance/ProgramColumnMemory.cs` — `Order` (`:48`) (a ordem guardada
  vale; as colunas novas vao para o fim; a coluna que a pessoa escondeu e
  mostrou de novo nao perde o lugar), `Read` (`:107`) (a largura guardada, com a
  invalida descartada: nem 3 pixels, nem 40.000), `WidthOf` (`:130`),
  `NamesOf` (`:141`) e `WidthsOf` (`:151`).
- **A lista de colunas continua vindo do catalogo**
  (`Core/Appearance/ProgramListLayout.cs`): a memoria e uma camada por cima,
  nunca uma segunda lista.
- **O arquivo de preferencias guarda a memoria**:
  `Core/Settings/AppSettings.cs` — `ColumnOrder` (`:102`), `ColumnWidths`
  (`:110`), `SortColumn` (`:116`) e `SortAscending` (`:119`).
- **A janela aplica e grava**: `MainWindow.xaml.cs` — `BuildProgramColumns`
  (`:644`) monta a grade ja com a memoria, `ColumnHeader_Click` (`:738`) ordena
  por qualquer coluna e guarda a ordenacao, `ApplySavedSort` (`:769`) reaplica
  a ordenacao guardada, `MoveColumn` (`:940`) e `MoverColunaNaGrade` (`:966`)
  mudam a coluna de lugar **dentro** da grade que esta na tela,
  `RememberColumnWidths` (`:992`) le a largura real de cada coluna e
  `SaveColumnMemory` (`:1012`) grava.
- **A diferenca para o concorrente**: o Uninstall Tool memoriza a largura e a
  posicao das colunas; o Revo Pro nao. Aqui a memoria existe.

## 3. O realce do programa novo (UNINSTALLTOOL T1#8)

- **"Programa novo" sao duas regras nomeadas, no mesmo modulo**:
  `Core/Discovery/NewInstallsTracker.cs` — `Cutoff` (`:183`) (a meia-noite: o
  dia inteiro conta, como no Revo) e `IsRecent` (`:204`) (a janela de dias).
- **O grupo e o realce consomem a MESMA regra**: o grupo da lista
  (`Ui/ProgramRow.cs` — `Group`, `:125`) e o realce
  (`Core/Appearance/ProgramFilters.cs` — `HighlightNewPrograms`, `:212`, e
  `HighlightColorOf`, `:515`). E o que impede que o grupo e o realce
  discordem sobre o que e "novo".
- **A tela liga o botao**: o painel lateral tem o item do realce
  (`Ui/Appearance/SettingsSidebar.xaml` — `OptHighlightNewPrograms`) e o
  tratador (`Ui/Appearance/SettingsSidebar.xaml.cs` —
  `HighlightNewProgramsCheck_Click`, `:295`).

## 4. O que foi medido de verdade

- **13 testes proprios** (`src/Tranal.Tests/Lote35FeatureTests.cs`): a ordem
  guardada, a coluna que sumiu, a largura guardada e a invalida, o arquivo de
  preferencias, a janela de "Novos Programas" com o dia inteiro, o realce
  usando a mesma regra do grupo, a grade aplicando a memoria, a janela
  aplicando/movendo/guardando, o botao do painel e as chaves do catalogo nos
  dois idiomas.
- **14 verificacoes de ponta a ponta** (`tools/test-lote35.ps1`) no
  aplicativo de verdade: o cabecalho e o conteudo falam da mesma coluna, o
  grupo dos programas novos existe, o botao do realce responde, o menu do
  cabecalho tem o comando de mover, mover muda a ordem **e** o conteudo das
  linhas acompanha, e o programa aberto de novo abre com a ordem que a pessoa
  deixou. O teste guarda o arquivo de preferencias antes e o devolve no fim:
  o ambiente da pessoa nao fica marcado.
- **A suite completa**: 1654 testes (13 a mais que o lote 34).
- **A auditoria**: `docs/auditoria/RECON-LOTE35-2026-11-10.md` fecha 2 linhas
  do catalogo (UNINSTALLTOOL T1#4 e T1#8) e atualiza a evidencia da T6#71. A
  cobertura subiu de 79,3% para **79,6%** (sem o Revo) e de 81,1% para **81,4%**
  (com o Revo dentro); o Uninstall Tool foi de 76,6% para **78,7%**.
