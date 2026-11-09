# Lacunas restantes (medido em 09/11/2026)

Este documento lista a **uma** funcionalidade que ainda nao existe no
Tranal, entre as 795 funcionalidades medidas contra os 11 concorrentes.

Cada linha diz **o que falta** e **por que** ela continua de fora. Nenhuma
linha aqui esta marcada como "OK": a medicao e o que vale, e medir e dizer
"ok" sem o codigo seria mentira.

O numero da cobertura esta em `PARIDADE-11-CONCORRENTES.md`
(74,5% — 795 funcionalidades, 560 OK, 1 AUSENTE, 32 FORA por decisao).
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

## Como fechar

A linha que resta fecha com **uma** decisao: empacotar o Tranal em MSIX.
E uma decisao de produto (assinatura, instalador, distribuicao), nao de
codigo — e por isso que ela esta registrada aqui, e nao no meio do codigo,
onde pareceria um esquecimento.
