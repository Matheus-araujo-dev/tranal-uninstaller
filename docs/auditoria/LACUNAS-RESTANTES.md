# Lacunas restantes (medido em 09/11/2026)

Este documento lista as **duas** funcionalidades que ainda nao existem no
Tranal, entre as 795 funcionalidades medidas contra os 11 concorrentes.

Cada linha diz **o que falta** e **por que** ela continua de fora. Nenhuma
linha aqui esta marcada como "OK": a medicao e o que vale, e medir e dizer
"ok" sem o codigo seria mentira.

O numero da cobertura esta em `PARIDADE-11-CONCORRENTES.md`
(74,3% — 795 funcionalidades, 559 OK, 2 AUSENTE, 32 FORA por decisao).
O Revo Pro 5.5.4 segue com 70 de 70.

## IOBIT T7#34 — Integracao com a Central de Acoes do Windows

**O que e.** O IObit registra as notificacoes do programa na Central de Acoes
do Windows (a lista de avisos do painel direito), e nao so na area de
notificacoes.

**O que o Tranal tem hoje.** As notificacoes internas do Tranal (o sino, com
as dicas e os avisos agendados) e o icone na bandeja do sistema, que produz o
aviso no baloes do proprio Windows.

**Por que continua de fora.** A Central de Acoes e a API de notificacoes do
Windows Runtime (`Windows.UI.Notifications`), e o Tranal roda como aplicativo
nao empacotado (`net10.0-windows`, sem as projecoes do WinRT).

**Medido, nao suposto.** Em 09/11/2026 o balao da bandeja foi disparado de
verdade, com AppUserModelID registrado, e o Windows aceitou tudo:
`SetCurrentProcessExplicitAppUserModelID` -> 0x00000000, e `Shell_NotifyIcon`
(ADD, SETVERSION e MODIFY com NIF_INFO) -> True. A Central de Acoes, porem,
nao recebeu nada: a contagem do banco real do Windows
(`%LOCALAPPDATA%\Microsoft\Windows\Notifications\wpndatabase.db`) ficou
em **27 antes e 27 depois**, com o mesmo `Order` maximo. A medicao esta em
`EVIDENCIA-CENTRAL-ACoes.md`, e os dois probes que a fazem estao versionados
em `tools/auditoria/probes`.

Usar a Central de Acoes de verdade exige empacotar o Tranal em MSIX — que e a
mesma coisa que a lacuna do Uninstall Tool abaixo (T3#46). Fazer as duas
depende da mesma decisao de empacotamento, e essa decisao ainda nao foi
tomada.

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
programa e assinado). E o mesmo item de empacotamento do IOBIT T7#34 acima.

## Fechada nesta rodada

**UNINSTALLTOOL T6#85 — Paineis ancoraveis ("push pin") com pre-visualizacao.**
Era a ultima lacuna de **codigo** entre os 11 concorrentes, e fechou com o
que o Uninstall Tool tem, medido no binario dele (`Click push pin to keep
open`, `Close Preview` e `Close Window`):

- a regra esta no nucleo, em um lugar so — `Core/Appearance/DockedPanel.cs`
  (qual painel esta ancorado) e `Core/Appearance/PanelPreview.cs` (o que a
  pre-visualizacao mostra);
- a janela principal tem a coluna do painel ancorado e o botao que fixa;
- a linha de comando tem `tranal /painel` (com `/dock` e `/undock`);
- a suite tem 18 testes proprios (`Lote17FeatureTests.cs`).

A pre-visualizacao so mostra o numero depois que o painel carregou de
verdade: antes disso, o texto e o nome e a descricao. Mostrar "0 itens"
antes da carga seria mentira.

## Como fechar

As duas linhas fecham com **uma** decisao: empacotar o Tranal em MSIX e
usar a API de notificacoes do Windows Runtime. E uma decisao de produto
(assinatura, instalador, distribuicao), nao de codigo — e por isso que ela
esta registrada aqui, e nao no meio do codigo, onde pareceria um esquecimento.
