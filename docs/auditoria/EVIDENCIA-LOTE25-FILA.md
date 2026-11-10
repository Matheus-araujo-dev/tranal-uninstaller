# Evidencia do lote 25: a fila de desinstalacao na tela, a colisao declarada e o primeiro plano

Medido em 09/11/2026, no codigo de `src/Tranal.Uninstaller/` e
`src/Tranal.Cli/`, e na suite de testes.

Este lote fecha **duas** linhas do RECON do Bulk Crap Uninstaller 6.3.0: a
**fila de desinstalacao com prevencao de colisao** (que a tela nao usava) e o
**"rodar em primeiro plano"** de quem nao desinstala em silencio. A terceira
linha tocada (abortar a tarefa) continua PARCIAL, com a nota corrigida.

## 1. As tres linhas

| Recorrencia | Funcionalidade | Antes | Agora |
|---|---|---|---|
| BCU T2#26 | **Desinstalacao simultanea** com prevencao de colisao | PARCIAL | OK |
| BCU T2#34 | **Abortar a tarefa inteira**, sem matar o desinstalador em execucao | PARCIAL | PARCIAL (nota corrigida) |
| BCU T2#36 | **Desinstalacao silenciosa de quem nao aceita silent**: rodar em primeiro plano ou remover da tarefa | PARCIAL | OK |

Cobertura: de **74,9%** (725 funcionalidades, 513 OK, 181 PARCIAL, 1 AUSENTE,
30 FORA) para **75,2%** (725, 515 OK, 179 PARCIAL, 1 AUSENTE, 30 FORA). Na
prova de cobertura com o Revo Pro dentro (`PARIDADE-11-CONCORRENTES.md`), de
**77,1%** (795 funcionalidades, 581 OK) para **77,4%** (795, 583 OK). O BCU
subiu de 79,4% para **81,4%**.

## 2. A fila: uma regra, duas entradas

`Core/Automation/UninstallQueue.cs` tem **uma** implementacao da regra, e a
versao assincrona (`RunAsync`, `:153`) e a de verdade: a versao sincrona
(`Run`, `:107`) delega para ela. Nao ha duas regras que podem divergir.

O que a fila garante:

- **um desinstalador por vez** — a trava e de processo inteiro (`:87`,
  `Gate`), e vale para a janela e para a linha de comando ao mesmo tempo;
- **a colisao declarada** — dois programas que dividem a pasta de instalacao
  ou o mesmo executavel de desinstalacao (`:274`, `DetectCollisions`);
- **quem nao desinstala em silencio sai da tarefa** (`:20`,
  `UninstallQueueOutcome.RemovedFromTask`), em vez de travar o lote;
- **a parada da tarefa** — quando o usuario manda encerrar, o que ainda nao
  comecou nao comeca (`:176`, o `stopRequested` marca o que nao rodou como
  `Aborted`, `:33`). O desinstalador que ja esta rodando **nao** e morto.

## 3. A tela usa a fila (e a guarda)

`Ui/UninstallWizardWindow.xaml.cs` mudou na etapa que roda os
desinstaladores nativos:

- a fila (`:763`, `UninstallQueue.RunAsync`) substituiu o laco "para cada
  programa";
- a operacao inteira roda dentro da **marca de "desinstalacao em
  andamento"** (`:761`, `UninstallGuards.RunGuardedAsync`);
- a colisao e o "saiu da tarefa" sao lidos do resultado da fila (`:795`) e
  mostrados no painel do rodape.

**A guarda de processo crossed era o outro lado do mesmo buraco.** A guarda
existia (`CheckRunning`, `Core/Automation/UninstallGuards.cs:143`) e a marca
existia (`MarkRunning`, `:173`), mas **ninguem marcava** — o `grep` do RECON
achava um unico hit, e era um teste. Agora `RunGuarded` (`:211`) e
`RunGuardedAsync` (`:237`) marcam antes e limpam no `finally` (mesmo quando a
operacao falha). E o que impede que DUAS desinstalacoes rodem ao mesmo tempo
na mesma maquina.

## 4. O "rodar em primeiro plano"

Quem nao desinstala em silencio **nao e um erro**: e um programa que precisa
da pessoa na frente da tela. O painel do rodape do assistente lista esses
programas e oferece rodar o desinstalador com a janela dele, um por vez:

- o painel (`Ui/UninstallWizardWindow.xaml:200`, `WizardLeftTaskText`, e
  `:206`, `WizardForegroundButton`);
- a acao (`UninstallWizardWindow.xaml.cs:845`, `ForegroundButton_Click`),
  que roda o **comando registrado** do programa — o que abre a janela do
  desinstalador;
- o modo do comando mora no nucleo (`Core/UninstallSupport.cs:1295`,
  `NativeUninstallCommand`), com tres valores: `Registered` (a janela),
  `Quiet` (o comando que o fabricante declara como quieto) e `Silent` (o
  comando silencioso derivado do tipo do desinstalador). O comportamento
  antigo foi **preservado**: quem chamava com "nao silencioso" continua
  recebendo o comando quieto do registro.

## 5. A regra do "ainda instalado" subiu para o nucleo

Dizer se o programa continua instalado estava na linha de comando
(`Program.IsStillInstalled`). A regra subiu para o motor
(`Core/UninstallSupport.cs:1366`, `UninstallEngine.IsStillInstalled`), e a
linha de comando passou a consumir o motor. A tela e a CLI perguntam a mesma
coisa ao mesmo lugar.

## 6. Os testes

`src/Tranal.Tests/Lote25FeatureTests.cs` tem **14** testes proprios:

- a fila assincrona roda **um desinstalador por vez**;
- a fila **para** quando o usuario manda encerrar (o que nao comecou fica
  `Aborted`);
- a fila assincrona tem a **mesma regra** da sincrona (mesmos resultados);
- quem nao desinstala em silencio **sai da tarefa**;
- a **colisao** e declarada na fila;
- a guarda **marca** a operacao e **limpa** no fim, e limpa **mesmo quando a
  operacao falha**;
- o motor diz se o programa **continua instalado** (com uma chave de teste
  criada e apagada no `HKCU`);
- o comando do desinstalador tem **tres modos**;
- a ligacao com a tela (a fila, a guarda, o painel e o primeiro plano) e as
  chaves novas nos dois idiomas.

A suite inteira: **1491 testes verdes** (era 1477).

Nenhum teste roda desinstalador de verdade: a fila e testada por injecao,
como as outras suites do projeto.

## 7. O que NAO foi medido, e por que

A desinstalacao em si **nao** foi medida na maquina: ela e destrutiva, e o
Tranal nao desinstala nada do computador de quem usa sem pedido. O que foi
medido:

- a fila e a guarda, por injecao, na suite (os 14 testes acima);
- a ligacao da tela com a fila e com a guarda, conferida **sobre o fonte**
  (a suite nao referencia o projeto grafico — a mesma limitacao declarada
  em todos os lotes anteriores).

O **que continua de fora** e o "Stop current uninstall task" imediato do BCU:
o Tranal nao mata o desinstalador nativo em execucao. Matar o desinstalador
no meio deixa o programa meio desinstalado, e um programa meio desinstalado
e pior do que um programa desinstalado. A tarefa inteira para no proximo
programa, e o que ja rodou termina.

A regra do projeto e a mesma de sempre: **a regra fica em um lugar so e o
resto consome**. A fila esta em `Core/Automation/UninstallQueue.cs`, a guarda
em `Core/Automation/UninstallGuards.cs`, o "continua instalado" e o comando
do desinstalador em `Core/UninstallSupport.cs` — e a tela e a linha de
comando consomem os tres.
