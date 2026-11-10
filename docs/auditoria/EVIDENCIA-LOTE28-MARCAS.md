# Evidencia do lote 28: a lista diz o que o programa e

Medido em 10/11/2026, no codigo de `src/Tranal.Uninstaller/`, na suite de
testes e no aplicativo de verdade.

Este lote fecha **cinco** linhas do RECON: as quatro marcacoes do BCU
(**protegido**, **atualizacao**, **orfao** e **invalido**) e a marcacao
`(32-bit)` do Uninstall Tool. E corrige **uma** linha que media a FORMA e
nao a capacidade: a do Geek, que media "coluna em vez de sufixo no nome" -
com este lote, a forma passa a ser a mesma do concorrente.

## 1. As linhas

| Recorrencia | Funcionalidade | Antes | Agora |
|---|---|---|---|
| BCU T1#13 (RECON-B) | Marcacao de **protegido** (tag `NoRemove` do editor) | PARCIAL | OK |
| BCU T1#14 (RECON-B) | Marcacao de **atualizacao** de outro aplicativo | PARCIAL | OK |
| BCU T1#15 (RECON-B) | Marcacao de **nao registrado (orfao)** | PARCIAL | OK |
| BCU T1#16 (RECON-B) | Marcacao de **aplicativo invalido / desinstalador corrompido** | PARCIAL | OK |
| UNINSTALLTOOL T1#3 (RECON-D) | Marcacao `(32-bit)` em programas de 32 bits em Windows 64 | PARCIAL | OK |
| UNINSTALLTOOL #3 (RECON-UNINSTALLTOOL-SOFTORGANIZER-WISE) | Marcacao `(32-bit)` na propria linha da lista | PARCIAL | OK |
| GEEK T1#3 (RECON-CATALOGO-HIBIT-GEEK) | Indicacao de app 32-bit dentro do nome | OK (forma diferente) | OK (mesma forma) |

Cobertura: de **76,3%** (725 funcionalidades, 523 OK, 171 PARCIAL, 1 AUSENTE,
30 FORA) para **77,0%** (725, 528 OK, 166 PARCIAL, 1 AUSENTE, 30 FORA). Na
prova de cobertura com o Revo Pro dentro (`PARIDADE-11-CONCORRENTES.md`), de
**78,4%** (795 funcionalidades, 591 OK) para **79,0%** (795, 596 OK). O BCU
subiu de 82,5% para **86,6%** e o Uninstall Tool de 71,3% para **72,3%**.

As duas linhas do Uninstall Tool medem a MESMA funcionalidade (a linha 3 do
catalogo `UNINSTALLTOOL.md`), e as duas precisaram ser remediadas: a cadeia
aplica os relatorios em ordem e a ultima aplicacao vence.

## 2. O que ja era medido e o que faltava

Nenhuma das cinco marcas era palpite: **todas as cinco ja tinham dado medido
no modelo**. O que nao existia era a **camada que mostra**.

| Marca | De onde vem o fato |
|---|---|
| 32 bits | a maquina do executavel, lida no disco (`Core/ProgramScanner.cs:197`, `DetectBits`) |
| atualizacao | o detector proprio (`Core/Discovery/UpdateMarker.cs:68`, `Detect`) e o valor `IsUpgrade` do registro (`ProgramScanner.cs:164`) |
| orfao | a ausencia de chave de registro na entrada |
| invalido | a saude do desinstalador (`Core/UninstallerHealth.cs:55`, `Evaluate`) |
| protegido | a tag `NoRemove` do proprio programa (`ProgramScanner.cs:168`) |

O que faltava era mostrar isso na lista principal. O RECON media, por
exemplo, que a deteccao de atualizacao existia mas "**nao ha marcacao/coluna
de atualizacao na lista principal** - o sinal do registro `IsUpgrade` nao
aparece em nenhuma coluna".

## 3. A regra: as marcas moram em um lugar so

O nucleo novo e `src/Tranal.Uninstaller/Core/ProgramMarks.cs`:

- `ProgramMark` (`:16`) e o conjunto de marcas: `ThirtyTwoBits`, `Update`,
  `Orphan`, `Invalid`, `Protected`;
- `ProgramMarkInfo` (`:48`) e o que o Tranal entendeu: `Is32Bits` (`:51`),
  `IsUpdate` (`:54`), `IsOrphan` (`:57`), `IsInvalid` (`:60`), `IsProtected`
  (`:63`), `UpdateDetail` (`:69`) e `Marks` (`:72`);
- `ProgramMarks.Detect` (`:96`) e a REGRA: ela le os fatos e devolve as
  marcas. A lista, a exportacao e a linha de comando consomem esse
  resultado - **ninguem monta a marca por conta propria**.

O texto e do catalogo de idiomas (`ProgramMarks.Label`, `:171`, chave
`ProgramMark.*`); o sufixo `(32-bit)` e a chave `ProgramMark.BitsSuffix`; o
nome da coluna e `Common.Marks`.

## 4. A regra do orfao: um lugar so, dois consumidores

O "orfao" do BCU e a entrada que **nao veio do registro**: o executavel foi
achado no disco, sem entrada de desinstalacao que o registre. A regra mora
em `ProgramMarks.IsOrphan` (`:141`):

- nao e MSI (`IsWindowsInstaller` / `IsMsi`);
- nao e StoreApp (`Kind == StoreApp`);
- e nao tem chave de registro (`RegistryPath` e `RegistryKey` vazios).

A coluna "Origem" da lista **consome essa mesma regra**
(`Core/ProgramColumns.cs:429`, `SourceOf`, e `:438`) - antes ela tinha a
regra repetida por conta propria. Agora nao ha duas definicoes de "orfao":
ha uma, e dois consumidores.

## 5. A coluna "Protegido" passou a considerar as duas coisas

Antes, a coluna "Protegido" da lista mostrava **a lista de exclusao do
Tranal** - e nao a tag `NoRemove` do proprio programa. Agora
`ProgramColumns.IsProtected` (`:451`) considera as duas (`:458`): o programa
que pede para nao ser removido aparece como protegido, e a lista de exclusao
continua valendo.

## 6. O nome exibido carrega a marca de 32 bits

E o que o Uninstall Tool e o Geek fazem: o programa de 32 bits aparece como
**Nome (32-bit)**.

- `ProgramMarks.WithBits` (`:182`) junta o nome com o sufixo do catalogo
  (`:187`);
- a marca so existe em **Windows de 64 bits** (`:103`): em um Windows de 32
  bits, todo programa e de 32 bits e a marca nao diria nada;
- o **nome exibido** e `ProgramRow.Label` (`Ui/ProgramRow.cs:63`), e o binding
  da lista passou a consumir ele (`MainWindow.xaml:27`);
- a **identidade** do programa continua crua em `ProgramRow.DisplayName`
  (`:41`): e o que a selecao, a copia e a coluna de exportacao usam. O que
  muda e o texto que a pessoa le, nao o que o Tranal identifica.

## 7. A coluna "Marcas" na lista

`ProgramColumn.Marks` (`Core/ProgramColumns.cs:125`) entrou no catalogo de
colunas (`:224`), visivel por padrao e exportavel pela linha de comando. O
valor vem de `ProgramMarks.Text` (`:156`) - as etiquetas traduzidas,
separadas por virgula.

A lista mostra o nome com a marca de 32 bits **e** a coluna "Marcas" mostra
todas as marcas. A exportacao (`tranal /export "arquivo" /csv /columns
"Program,Type,Marks"`) leva as duas.

## 8. O que foi medido no aplicativo de verdade

`tools/test-lote28.ps1` - **12 verificacoes, todas verdes**:

- a linha de comando gera o arquivo da lista e o cabecalho tem a coluna
  `Marks`;
- **80 programas com marca** na linha de comando, **79** deles de 32 bits;
- a linha de comando **recusa** uma coluna que nao existe, e a recusa lista
  as 25 colunas validas (a ultima e `Marks`);
- a janela de colunas da tela oferece **Marcas** (25 colunas);
- a lista carregou **78 programas**, **29** deles com o sufixo `(32-bit)`;
- os **29** nomes visiveis foram conferidos contra a linha de comando, um a
  um: **0 divergentes**.

O teste so **le** a lista: nenhum software e instalado, removido ou tocado.

A suite tem **22 testes proprios** (`src/Tranal.Tests/Lote28FeatureTests.cs`),
que cobrem a deteccao de cada marca (inclusive os negativos: 64 bits nao e
marcado, programa registrado nao e orfao, instalador do Windows nao e orfao,
desinstalador saudavel nao e invalido), a coluna, o nome exibido, a regra do
orfao em um lugar so, a coluna "Protegido" e a paridade dos dois idiomas.

## 9. O que este lote NAO faz

- **Nao existe Windows Feature / Windows Update como tipo de desinstalador.**
  O BCU tem essa distincao (`T1#11`). Distinguir um recurso do Windows de um
  programa instalado exigiria heuristicas frageis sobre o registro, e o risco
  de falso positivo (tratar um programa comum como recurso do sistema) e
  pior que a ausencia. A medicao do binario do BCU nao da uma regra confiavel
  para isso. Fica de fora, e por isso a linha do RECON continua como esta.
- **A marca de 32 bits nao entra em Windows de 32 bits**: ali todo programa
  e de 32 bits, e a marca nao diria nada.
- **O orfao continua sendo o que o disco mostra**: a tela de Descoberta
  (`Core/Discovery/DriveScanner.cs:82`) segue sendo a varredura que acha os
  executaveis orfaos; a marca e a mesma regra, mostrada na lista.
