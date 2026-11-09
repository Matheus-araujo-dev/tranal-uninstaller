# Evidencia medida — a arvore de alteracoes, a impressao e o rastreio

**Medido em 09/11/2026**, no Windows 11 da maquina de desenvolvimento.

Este documento registra o que a auditoria mediu no **lote 19**: a arvore de
alteracoes (o que o instalador gravou, entre dois snapshots), a impressao da
lista de programas e a ligacao da janela de rastreio com a interface.

## 1. O problema medido: dois modulos orfaos

A varredura de consumidores encontrou duas telas que **existiam e ninguem
abria**:

| Tela | O que ela tem | Quem a abria |
|---|---|---|
| `Ui/Reporting/ReportingWindow.xaml.cs` | a janela de impressao (a lista de programas, como vai sair na folha) | **ninguem** |
| `Ui/Tracing/TracingWindow.xaml.cs` | o rastreio em tempo real, a politica de processos e a biblioteca de snapshots com a comparacao A x B | **ninguem** |

A medicao do orfao: a busca por `new ReportingWindow(` e por `new
TracingWindow(` em todo o `src/` nao devolvia nenhuma ocorrencia fora da
própria classe. A `PrintLayout` (o nucleo da impressao) tinha teste e a CLI
usava o `/report-html`, mas **a lista de programas nao tinha como ser
impressa na interface** — e a comparacao de snapshots, que e a base de
varias funcionalidades auditadas, nao tinha tela.

Isso e o que a regra do projeto diz: *"modulo orfao nao conta"*. Um modulo
sem consumidor nao e uma feature entregue.

## 2. A correcao

- o menu principal ganhou **"Imprimir lista..."** (e o atalho **Ctrl+P**),
  que abre a janela de impressao com a lista que esta na tela (a mesma
  busca e o mesmo filtro);
- o menu principal ganhou **"Rastreio e comparacao A x B..."**, que abre a
  janela de rastreio (ela **nao e modal**: o rastreio acompanha o trabalho
  do usuario);
- a janela de impressao passou a receber uma **tabela** (`PrintTable`), e
  nao a lista de programas: e a mesma janela que imprime a lista e a arvore
  de alteracoes;
- a aba de snapshots da janela de rastreio ganhou **"Exportar arvore..."** e
  **"Imprimir arvore..."**.

A regra de impressao mora em **um lugar so** (`Core/Reporting/PrintTable.cs`):
a quebra de pagina, o cabecalho que se repete e o rodape. A lista de
programas (`PrintLayout`) e a arvore de alteracoes (`ChangeTreeReport`) sao
as duas **tabelas** que consomem essa regra.

## 3. A arvore de alteracoes

O nucleo (`Core/Reporting/ChangeTreeReport.cs`) transforma a comparacao
entre dois snapshots em uma tabela de quatro colunas — tipo, mudanca, item e
detalhe — e essa mesma tabela e a que sai exportada e a que sai impressa.

MEDIDO pela propria linha de comando, com dois snapshots sinteticos (o
formato e o do proprio Tranal):

```
$ tranal /comparar teste-sintetico-antes teste-sintetico-depois
Arvore de alteracoes
====================

Antes:  teste-sintetico-antes
Depois: teste-sintetico-depois
Alteracoes: 6

Arquivo | Novo | C:\Programa\novo.dll | 500 B
Pasta | Nova | C:\Programa\Novo |
Registro | Nova chave | HKCU\Software\Programa\Extra |
Arquivo | Modificado | C:\Programa\app.exe | 2 KB
Valor | Modificado | HKCU\Software\Programa\Versao | 1.0 -> 2.0
Arquivo | Removido | C:\Programa\config.ini | 100 B
```

As seis alteracoes do retrato sintetico aparecem: arquivo novo, arquivo
removido, arquivo modificado, pasta nova, chave de registro nova e valor de
registro alterado (com o valor antigo e o novo).

O mesmo comando nos quatro destinos:

```
$ tranal /comparar ... /export arvore.csv    # Tipo;Mudanca;Item;Detalhe
$ tranal /comparar ... /export arvore.html   # <!DOCTYPE html> + as linhas
$ tranal /comparar ... /export arvore.txt    # texto simples
$ tranal /comparar ... /print                # o layout de impressao
```

O layout de impressao, medido:

```
Arvore de alteracoes
Tranal Uninstaller Pro — 6 alteracao(oes) — antes: teste-sintetico-antes | depois: teste-sintetico-depois — 09/11/2026 18:05

Tipo     | Mudanca    | Item                          | Detalhe
Arquivo  | Novo       | C:\Programa\novo.dll          | 500 B
Pasta    | Nova       | C:\Programa\Novo              |
Registro | Nova chave | HKCU\Software\Programa\Extra  |
Arquivo  | Modificado | C:\Programa\app.exe           | 2 KB
Valor    | Modificado | HKCU\Software\Programa\Versao | 1.0 -> 2.0
Arquivo  | Removido   | C:\Programa\config.ini        | 100 B
Página 1 de 1
```

## 4. Um defeito real encontrado no caminho

MEDIDO: `tranal /comparar "antes" "depois" /export "arquivo.csv"` **nao
exportava a comparacao** — exportava a **lista de programas** (211
entradas). A causa: o `/export` (que exporta a lista) era testado antes do
`/comparar` na cadeia de comandos, e ele "ganhava" a flag.

A correcao foi de ordem, nao de caso: `/comparar` e `/snapshots` passou a
ser testados **antes** de `/export`. O `/export` continua exportando a
lista quando o comando e outro.

Este defeito nao apareceu nos testes: a suite nao exercita a linha de
comando. Ele apareceu quando o comando foi medido de verdade.

## 5. A captura de snapshot de verdade

A captura pela interface (aba de snapshots da janela de rastreio) foi
medida em separado: ela varre o sistema inteiro (ate 45 segundos por
parte) e gravou um snapshot real:

```
$ tranal /snapshots
Snapshots:
  teste-antes  (09/11/2026 18:00)  id=80dd3e0f-ed02-4033-8668-0f52200cd3b7
```

O teste de ponta a ponta (`tools/test-comparacao.ps1`) usa snapshots
**sinteticos** de proposito: assim ele nao depende de varrer o sistema
inteiro e nao depende do estado da maquina de quem roda. Ele apaga tudo
que cria (os snapshots e os arquivos exportados).

## 6. O que os testes mediram

`tools/test-impressao.ps1`, no aplicativo de verdade (12 verificacoes):

```
[OK]    a janela principal do Tranal aparece
[OK]    o botao do menu principal existe
[OK]    o menu principal tem o item de imprimir a lista
[OK]    a janela de impressao abre com a pre-visualizacao
[OK]    a janela de impressao tem o botao de imprimir
    elementos na pre-visualizacao: 170
[OK]    o menu principal tem o item de rastreio
[OK]    a janela de rastreio abre
[OK]    a aba de snapshots pode ser aberta
[OK]    a lista de snapshots aparece
[OK]    a aba de snapshots tem o botao de exportar a arvore
[OK]    a aba de snapshots tem o botao de imprimir a arvore
[OK]    nenhuma excecao nova no log de inicializacao
```

`tools/test-comparacao.ps1`, pela linha de comando (18 verificacoes): a
listagem da biblioteca, a comparacao, as seis alteracoes do retrato, a
exportacao em CSV, HTML e texto, o cabecalho do CSV, o HTML, o layout de
impressao e o rodape de pagina, e a limpeza final.

E a suite tem 16 testes proprios do lote (`Lote19FeatureTests.cs`), que
cobrem o nucleo: a paginacao (o cabecalho se repete em cada pagina, o
documento vazio tem uma pagina, o rodape diz a pagina), as seis alteracoes
da arvore, a exportacao nos tres formatos, a gravacao do arquivo, o layout
de impressao, o menu, o atalho Ctrl+P, a aba de snapshots e a janela de
impressao.

## 7. O que a auditoria mudou

As tres linhas do **Total Uninstall** que estavam PARCIAIS e agora estao
OK, com evidencia no codigo:

| # | Funcionalidade |
|---|---|
| T5#54 | Exportar para arquivo ou **imprimir** as alteracoes detectadas |
| T7#69 | **Exportar a arvore de mudancas** |
| T7#71 | **Imprimir a arvore de alteracoes** (com selecao de impressora) |

A cobertura subiu de **74,5% para 74,8%** (795 funcionalidades medidas, 563
OK, 199 PARCIAIS, 1 AUSENTE, 32 FORA por decisao). O Revo Pro 5.5.4 segue
com 68 de 70 OK e 2 FORA por decisao.
