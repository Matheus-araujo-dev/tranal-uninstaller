# Evidencia do lote 31: o gerenciador do log de rastreio

Medido em 10/11/2026, no codigo de `src/Tranal.Uninstaller/`, na suite de
testes e no aplicativo de verdade.

Este lote deu dono a um modulo do nucleo que **ninguem chamava** e fechou uma
**duplicacao de gravacao**: o arquivo de log de instalacao era escrito em dois
lugares diferentes, e cada um tinha um defeito.

## 1. O que o lote entregou

| Recorrencia | Funcionalidade | Antes | Agora |
|---|---|---|---|
| SOFTORGANIZER #26 (RECON-UNINSTALLTOOL-SOFTORGANIZER-WISE e RECON-C) | Trace manager: excluir itens e editar valores antes de aplicar | PARCIAL | OK |

Cobertura: de **78,1%** (725 funcionalidades, 536 OK, 158 PARCIAL, 1 AUSENTE,
30 FORA) para **78,2%** (725, 537 OK, 157 PARCIAL, 1 AUSENTE, 30 FORA). Na
prova de cobertura com o Revo Pro dentro (`PARIDADE-11-CONCORRENTES.md`), de
**80,0%** (795 funcionalidades, 604 OK) para **80,1%** (795, 605 OK). O Soft
Organizer subiu de 80,5% para **81,8%**.

A linha #39 do mesmo concorrente ("Log de mudancas em arquivo por aplicativo
(`TracedApp.changes`)") continua **PARCIAL**, e continua honesto: ha um log por
aplicativo (no formato do Tranal, `Tranal.TraceLog.v1`), mas nao ha um arquivo
com o nome e o formato do concorrente.

## 2. O modulo que nao tinha dono

`Core/Reporting/TraceLogExporter.cs` (`TraceLogEditor`, `:344`) sabia
acrescentar e excluir um item do log de instalacao — arquivo, pasta ou chave
de registro. **Nao tinha quem o chamasse**: so os testes o usavam.

Por que isso importa: o log de instalacao e a fonte do que o instalador
gravou no computador, e e ele que a desinstalacao dirigida pelo log remove.
Sem um editor, a pessoa nao tinha como corrigir o log antes da remocao — nem
para tirar de la o que nao era do programa, nem para acrescentar o que o
rastreio nao viu.

Agora o modulo tem dois donos:

- **a tela do gerenciador** (`Ui/Tracing/TraceManagerWindow.xaml.cs:28`): a
  lista do log, o botao de excluir o item selecionado e o painel de
  acrescentar (tipo + caminho). O comando fica na barra do modulo da base de
  logs (`Ui/Modules/ModuleViews.cs:279`, `LogsManager`) e tambem na janela do
  proprio log (`Ui/TracedLogWindow.xaml`, `TracedLogManager`);
- **a linha de comando** (`src/Tranal.Cli/LogCommands.cs:205` e `:247`):
  `/log-add <id> /caminho "<caminho>" /tipo arquivo|pasta|chave` e
  `/log-drop <id> /caminho "<caminho>"`.

O item acrescentado a mao entra no log com **confianca media**, nao alta
(medido em `Lote31FeatureTests.cs`): ninguem mediu que aquele item pertence ao
programa, e dizer que foi medido seria mentira.

## 3. A duplicacao que o lote fechou

O arquivo de log era escrito em **dois lugares**:

| Onde | Como escrevia | O que faltava |
|---|---|---|
| `Core/Reporting/TraceLogExporter.cs` (`GravarAtomicamente`) | temporario + `File.Replace` + copia `.bak` | as contagens do envelope sao reescritas a mao |
| `Core/Modules/TraceStore.cs` (`WriteLogFile`) | `File.WriteAllText` | **sem copia**: quem escrevesse por aqui perdia a versao anterior |

A **gravacao do log esta agora em um lugar so**: `TraceStore.GravarAtomicamente`
(`:622`), e toda escrita do arquivo de log — criar, editar entrada, ignorar,
remover, acrescentar, mesclar — passa por ali. E a gravacao e atomica em toda
escrita, nao so na do editor.

O editor deixou de escrever o arquivo por conta propria: ele le o log pelo
`TraceStore` (`LoadPayload`), muda o conteudo pelo `LogOperations` (`AddEntry`,
`RemoveEntry`) e devolve o log ja gravado. **A regra mora em um lugar so.**

### O bug que a duplicacao escondia

`TraceStore.SavePayload` devolvia `true` **mesmo quando a gravacao falhava**:
o `WriteLogFile` era `void` e o erro era engolido. A interface e a linha de
comando diziam "gravado" quando o arquivo nao tinha sido escrito. Agora a
gravacao atomica devolve o resultado, e o `SavePayload` propaga.

## 4. A tela

`Ui/Tracing/TraceManagerWindow.xaml` tem a lista do log (tipo, caminho e o
estado da entrada: no log, ignorado ou acrescentado), o painel de acrescentar
(tipo + caminho) e o botao de excluir o item selecionado. O status diz o que
aconteceu, e o texto vem do catalogo (19 chaves novas, em pt-BR e en-US, as
mesmas nos dois idiomas).

A tela **nao decide o que e seguro apagar**: isso e do motor, com a confianca
que ele mede. Ela so organiza o que o log vai dizer.

## 5. Os testes

- `src/Tranal.Tests/Lote31FeatureTests.cs`: **9 testes proprios** (acrescentar
  arquivo, pasta e chave; excluir; a confianca do item acrescentado a mao; a
  gravacao atomica com a copia `.bak` e sem o temporario sobrando; a regra do
  log em um lugar so; a tela e os seus dois chamadores; a linha de comando
  acrescentando e excluindo de verdade; e as chaves de catalogo nos dois
  idiomas).
- `tools/test-lote31.ps1`: **13 verificacoes** no aplicativo de verdade (6 da
  linha de comando e 7 da tela). O teste cria **um** log sintetico na base de
  logs, mexe so nele e **apaga o arquivo no final** (com a copia `.bak` que a
  gravacao atomica deixa ao lado). Nenhum software e instalado ou removido.
- Suite completa: **1604 testes verdes**.
