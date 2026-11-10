# Evidencia do lote 34: o nucleo ja sabia e a tela nao dizia

Medido em 10/11/2026, no codigo de `src/Tranal.Uninstaller/`, na suite de
testes e no aplicativo de verdade.

Este lote tem um tema so: **a distancia entre o que o nucleo sabia e o que a
pessoa conseguia fazer**. Das sete linhas que fecharam, **tres eram medicao
desatualizada** (o codigo ja resolvia, em outro lugar) e **quatro eram
feature mesmo faltando** — uma delas um **bug de verdade**, que escondia o
Windows recusando apagar um arquivo.

## 1. O que o lote entregou

| Recorrencia | Funcionalidade | Antes | Agora |
|---|---|---|---|
| UNINSTALLTOOL T1#21 | Remocao de **arquivos somente-leitura** | PARCIAL | OK |
| UNINSTALLTOOL T1#20 e GEEK T2#29 | Remocao de **objetos COM** registrados / **desregistro de COM** antes de remover | PARCIAL | OK |
| UNINSTALLTOOL T2#29 | **Exportacao do resultado da varredura para XML** | PARCIAL | OK |
| UNINSTALLTOOL T6#84 | **Limpar o cache** da aplicacao | PARCIAL | OK |
| WISE T1#15 | Aviso de que um programa **nao aceita** desinstalacao silenciosa | PARCIAL | OK |
| WISE T6#59 | Contagem e **tamanho total** dos programas | PARCIAL | OK |

Cobertura: de **80,3%** (795 funcionalidades, 606 OK, 156 PARCIAL, 1 AUSENTE,
32 FORA) para **81,1%** (795, 613 OK, 149 PARCIAL, 1 AUSENTE, 32 FORA). Na
prova sem o Revo Pro dentro (`RELATORIO-11-CONCORRENTES.md`), de **78,3%**
(725, 538 OK, 156 PARCIAL) para **79,3%** (725, 545 OK, 149 PARCIAL).

Por concorrente: GEEK de 88,6% para **90,9%**; UNINSTALLTOOL de 72,3% para
**76,6%**; WISE de 70,9% para **73,3%**.

A medicao da rodada esta em `RECON-LOTE34-2026-11-10.md`, que entra **no fim**
da cadeia (`tools/auditoria/rodar-cadeia.ps1`): a medicao mais recente vence
por linha do catalogo.

## 2. O bug de verdade: o arquivo somente-leitura nao saia

A regra do atributo somente-leitura **existia** no nucleo, em um lugar so:
`Core/Automation/StubbornRemover.cs` (`ClearReadOnly`, `:171`). O backup e a
destruicao definitiva ja a consumiam.

O caminho principal de remocao **nao** consumia: `Core/UninstallSupport.cs`
fazia `File.Delete` direto, e o Windows recusa apagar um arquivo marcado como
somente-leitura. O item voltava como falha (ou ficava agendado para o proximo
reinicio), e a pessoa via um programa que "nao quer sair".

Agora **todo** caminho de remocao consome a regra:

- `RemoveFileSafely` (`:1856`) — o arquivo dentro de uma pasta;
- `RemoveOne` (`:2040`) — o item de sobra marcado como arquivo, que e o
  caminho que a pessoa ve.

O teste do lote pegou o bug **antes** da correcao: a primeira versao do
`ARemocaoApagaArquivoSomenteLeitura` falhou com "Access to the path ... is
denied" — o caminho principal nao consumia a regra. A correcao do codigo
(nao do teste) e o que deixou a suite verde.

## 3. O desregistro de COM no fluxo

O desregistro de COM existia (`Core/Automation/ComUnregister.cs`), com comando
proprio na linha de comando — mas **nao estava ligado ao fluxo de
desinstalacao**. Era uma ferramenta separada: a pessoa desinstalava o
programa e o registro COM ficava orfao (o Windows apontando para uma DLL que
nao existe mais).

Agora a regra mora em um lugar so — `ComUnregister.UnregisterFolder` (`:96`) —
e o assistente a consome **antes** de apagar os arquivos
(`Ui/UninstallWizardWindow.xaml.cs`, `DesregistrarCom`, `:1370`; a chamada,
`:1012`). A tela so **conta**: quantos componentes foram desregistrados e
quantos falharam, e o resultado aparece no resumo da desinstalacao.

## 4. A exportacao da varredura em XML

O exportador existia (`Core/Reporting/LeftoverReportExporter.cs`, `WriteXml`,
`:77`), e o unico consumidor era a linha de comando
(`/inspection-leftovers`). A tela nao tinha acao nenhuma.

O lote 34 da duas acoes, e o exportador continua sendo a **regra em um lugar
so**:

- **a tela do assistente** (`Ui/UninstallWizardWindow.xaml.cs`,
  `ExportLeftoversButton_Click`, `:1394`) — botao `WizardExportLeftoversButton`
  na etapa da varredura;
- **a tela da desinstalacao personalizada**
  (`Ui/ManualRemovalWindow.xaml.cs`, `ExportXmlButton_Click`) — botao
  `ManualExportXmlButton`, ao lado do CSV que ja existia.

A segunda e a que importa para o uso real: a etapa da varredura do assistente
so chega **depois** do desinstalador nativo, e a varredura que a pessoa faz
**sem remover nada** e a da desinstalacao personalizada. Com o XML ali, a
exportacao fica alcancavel sem desinstalar nada — que e o que o Uninstall
Tool faz.

## 5. As tres medicoes desatualizadas

Tres das linhas estavam marcadas como PARCIAL por uma medicao antiga, e o
codigo **ja resolvia**, em outro lugar:

| Linha | Onde o codigo ja estava |
|---|---|
| WISE T1#15 (aviso de desinstalacao silenciosa) | `Ui/UninstallWizardWindow.xaml.cs:623` (`Supports`), com o nucleo em `Core/QuietUninstall.cs:112` |
| WISE T6#59 (contagem e tamanho total) | `MainWindow.xaml.cs:411` (`ProgramTotalsCalculator`) e `:413` (`StatusText`) |
| UNINSTALLTOOL T6#84 (limpar o cache) | `Ui/OptionsWindow.xaml.cs:697` (`RebuildCacheButton_Click`), que invalida o cache em `:699` |

Nenhuma linha foi fechada sem codigo: a remediacao **reescreveu a medicao**,
com a evidencia nova e a linha real. Fechar linha sem codigo seria mentira —
e deixar "PARCIAL" quando o codigo ja resolvia seria perder funcionalidade que
existe.

## 6. Os testes

- `src/Tranal.Tests/Lote34FeatureTests.cs`: **10 testes proprios**. O mais
  importante e o que apaga um arquivo somente-leitura de verdade, pela
  `UninstallEngine.RemoveLeftovers` (o caminho que a pessoa usa), e os que
  medem que o caminho principal consome a regra do `StubbornRemover` e que o
  desregistro de COM esta no fluxo.
- `tools/test-lote34.ps1`: o teste de ponta a ponta. Ele **nao instala e nao
  remove nada**: varre (leitura), grava UM arquivo XML na pasta temporaria e
  apaga o cache de programas — que a proxima varredura reconstroi, e o
  programa avisa que vai reconstruir.
- `tools/auditoria/reancorar-citacoes.py`: a remediacao das citacoes dos
  RECON. Ela **reancora** (a ocorrencia mais proxima do simbolo) e nunca
  reescreve o texto da evidencia. Medido: as 17 citacoes que o lote 34
  deslocou (a insercao do `using` e dos metodos novos moveu as linhas do
  assistente e da tela de desinstalacao personalizada) foram reancoradas, e a
  suite voltou a passar.
- Suite completa: **1641 testes verdes**.

## 7. O que continua de fora

Continua de fora, e continua honesto: o **desinstalador nativo em execucao**
nao e medido na maquina. Desinstalar e destrutivo, e o Tranal nao desinstala
nada do computador de quem usa sem pedido. O que foi medido e a regra (por
injecao, na suite) e a ligacao da tela (sobre o fonte).
