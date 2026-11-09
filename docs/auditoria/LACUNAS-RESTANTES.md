# Lacunas restantes (medido em 09/11/2026)

Este documento lista as **tres** funcionalidades que ainda nao existem no
Tranal, entre as 795 funcionalidades medidas contra os 11 concorrentes.

Cada linha diz **o que falta** e **por que** ela continua de fora. Nenhuma
linca aqui esta marcada como "OK": a medicao e o que vale, e medir e dizer
"ok" sem o codigo seria mentira.

O numero da cobertura esta em `PARIDADE-11-CONCORRENTES.md`
(74,2% — 795 funcionalidades, 558 OK, 3 AUSENTE, 32 FORA por decisao).
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
nao empacotado (`net10.0-windows`, sem as projecoes do WinRT). Usar a
Central de Acoes de verdade exige empacotar o Tranal em MSIX — que e a
mesma coisa que a lacuna do Uninstall Tool abaixo (T3#46). Fazer as duas
depende da mesma decisao de empacotamento, e essa decisao ainda nao foi
tomada.

## UNINSTALLTOOL T6#85 — Paineis ancoraveis ("push pin") com pre-visualizacao

**O que e.** O Uninstall Tool deixa o usuario fixar paineis na janela: o
painel fica ancorado (nao some quando o usuario troca de tela) e aparece uma
pre-visualizacao do conteudo quando o mouse passa sobre ele.

**O que o Tranal tem hoje.** A barra lateral com as ferramentas e o painel
de conteudo, que troca de tela por vez. O conteudo de cada painel ja e
carregado sob demanda (`ItemListPanel.RunAsync`), entao a **pre-visualizacao**
e um leitor do que o painel traria — nao uma segunda carga.

**Por que continua de fora.** O painel ancorado muda o modelo da janela
principal (varias telas vivas ao mesmo tempo, com ciclo de vida proprio). E
uma mudanca de arquitetura da interface, nao uma feature: fazer isso por
cima do modelo atual de "uma tela por vez" seria remendo, e o proprio projeto
rejeita remendo.

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
programa e assinado). E o mesmoitem de empacotamento do IOBIT T7#34 acima.

## Como fechar

As tres linhas fecham com **uma** decisao: empacotar o Tranal em MSIX e
usar a API de notificacoes do Windows Runtime. E uma decisao de produto
(assinatura, instalador, distribuicao), nao de codigo — e por isso que ela
esta registrada aqui, e nao no meio do codigo, onde pareceria um esquecimento.
