# Evidencia medida - a Central de Acoes do Windows (IOBIT T7#34)

**Medido em 09/11/2026**, no Windows 11 da maquina de desenvolvimento.

## A pergunta

O IObit registra as notificacoes do programa na **Central de Acoes** do
Windows (a lista de avisos do painel direito), e nao so na area de
notificacoes. O Tranal tem as duas coisas: as notificacoes internas (o sino,
com as dicas e os avisos agendados) e o icone na bandeja do sistema, que
produz o aviso no balao do proprio Windows.

A pergunta e se o balao da bandeja do Tranal chega na Central de Acoes.

## O que foi feito

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

## O que o Windows respondeu ao balao

```
SetCurrentProcessExplicitAppUserModelID -> 0x00000000
Shell_NotifyIcon(ADD) -> True
Shell_NotifyIcon(SETVERSION) -> True
Shell_NotifyIcon(MODIFY com NIF_INFO) -> True
esperando 8 segundos para o shell processar...
```

Tudo aceito: o AUMID foi registrado e o balao foi pedido, com `NIF_INFO`.

## O que o banco da Central de Acoes mostrou

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

## Conclusao

O balao da bandeja (`Shell_NotifyIcon` com `NIF_INFO`) **nao** vira
notificacao da Central de Acoes. A Central de Acoes recebe o que a API de
notificacoes do Windows Runtime (`Windows.UI.Notifications`) entrega, e essa
API exige um aplicativo **empacotado** (MSIX), com a identidade declarada —
o Tranal roda como aplicativo nao empacotado (`net10.0-windows`).

Fechar a linha **T7#34**, portanto, **nao e uma feature de codigo**: e a
mesma decisao de produto do empacotamento MSIX (a linha **T3#46** do
Uninstall Tool). ate essa decisao, a linha fica **AUSENTE**, e a medicao
deste documento e o que sustenta esse estado.

## Como reproduzir

```
dotnet run --project tools\auditoria\probes\probe-wpn
dotnet run --project tools\auditoria\probes\probe-balao2
dotnet run --project tools\auditoria\probes\probe-wpn
```

A terceira repeticao e a que conta: e ela que mostra que a contagem nao
mudou.
