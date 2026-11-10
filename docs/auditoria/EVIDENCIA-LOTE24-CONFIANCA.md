# Evidencia do lote 24: a nota das sobras em cinco niveis, os criterios de pasta e o aviso de perigo

Medido em 09/11/2026, no codigo de `src/Tranal.Uninstaller/` e
`src/Tranal.Cli/`, na suite de testes e no aplicativo publicado
(`publish/preview`) e na linha de comando publicada (`publish/cli`).

Este lote fecha **tres** linhas do RECON do Bulk Crap Uninstaller 6.3.0: os
**cinco niveis de confianca** (Muito bom / Bom / Duvidoso / Mau /
Desconhecido), os **dois criterios de pasta** que faltavam (muitos arquivos
e sem subdiretorios) e o **aviso de perigo** ao mexer em item de baixa
confianca. A quarta linha tocada (os grupos de sobras) continua PARCIAL,
com a nota corrigida: a tela ja e uma arvore com categorias, mas as
categorias sao as do Tranal e nao as 17 do BCU.

## 1. As quatro linhas

| Recorrencia | Funcionalidade | Antes | Agora |
|---|---|---|---|
| BCU T3#42 | **5 niveis de confianca**: Muito bom / Bom / Duvidoso / Mau / Desconhecido | PARCIAL | OK |
| BCU T3#44 | Confianca de **pasta**: com "muitos arquivos" e "sem subdiretorios" | PARCIAL | OK |
| BCU T3#46 | Aviso explicito ao mexer em item de **baixa confianca** ("pode ser muito perigoso") | PARCIAL | OK |
| BCU T3#49 | **Grupos de sobras** por categoria, com scanners proprios | PARCIAL | PARCIAL (nota corrigida) |

Cobertura: de **74,5%** (725 funcionalidades, 510 OK, 184 PARCIAL, 1 AUSENTE,
30 FORA) para **74,9%** (725, 513 OK, 181 PARCIAL, 1 AUSENTE, 30 FORA). Na
prova de cobertura com o Revo Pro dentro (`PARIDADE-11-CONCORRENTES.md`), de
**76,7%** (795 funcionalidades, 578 OK) para **77,1%** (795, 581 OK). O BCU
subiu de 76,3% para **79,4%**.

## 2. A nota: a regra mora no nucleo

`Core/LeftoverRatings.cs` e o **unico** lugar que decide a nota de uma
sobra:

- `LeftoverRating` (`:22`) tem os cinco niveis: `VeryGood`, `Good`,
  `Doubtful`, `Bad` e `Unknown` — o vocabulario do BCU;
- `Rate` (`:91`) e a regra, e ela e uma **tabela**: sem motivo nao ha nota;
  confianca alta com motivo certo (a pasta de instalacao registrada, um alvo
  informado pelo usuario, o nome exato) da "Muito bom"; confianca alta com
  outro motivo da "Bom"; confianca media com motivo fraco (o nome apenas
  parece) da "Duvidoso"; confianca media com motivo forte da "Bom"; e
  confianca baixa da "Mau", porque ali o vinculo com o programa e so uma
  semelhanca de nome;
- `IsSafe` (`:108`) e `IsDangerous` (`:116`) dizem o que dispensa o aviso e
  o que e perigoso;
- `RiskyItems` (`:122`) e `MarkedRiskyItems` (`:132`) sao as duas listas: as
  sobras de nota ruim e as **marcadas** de nota ruim.

A diferenca entre a **confianca** (tres niveis: Alta, Media, Baixa) e a
**nota** (cinco niveis) e proposital. A confianca mede a forca do vinculo
entre a sobra e o programa, e e ela que decide o que fica marcado para
remocao. A nota e o que a pessoa le: ela junta a confianca com o **motivo**,
e por isso que um item de confianca media com nome exato e "Bom", enquanto o
mesmo item com nome so parecido e "Duvidoso".

## 3. Os dois criterios de pasta que faltavam

`Core/Discovery/FolderConfidenceEvaluator.cs` avalia a pasta por **nove**
criterios (eram sete). Os dois novos:

- `ManyFilesCriterion` (`:120`, "muitos-arquivos"): a pasta tem 50 arquivos
  ou mais (`ManyFilesThreshold`, `:126`). O veredito sai em `:278`;
- `NoSubdirectoriesCriterion` (`:123`, "sem-subdiretorios"): a pasta nao tem
  subdiretorio. O veredito sai em `:290`.

A inspecao (`:358`, `Inspect`) passou a contar os **arquivos** e a dizer se
ha **subdiretorio** — antes ela so contava as entradas do primeiro nivel.

**A seguranca foi verificada, nao presumida.** Os dois criterios somam
ponto, e "alta confianca" significa "marcado para remocao". A pasta com
documento do usuario tem de continuar na confianca mais baixa, com ou sem os
criterios novos: o desconto por dado do usuario (-2 ou -3) e maior que a
soma dos dois criterios (+2). Isso esta em teste
(`Lote24FeatureTests.cs`, `A_pasta_com_dado_do_usuario_continua_na_confianca_mais_baixa`).

## 4. O aviso de perigo

O aviso de "baixa confianca" que ja existia dizia quais itens **nao**
vieram marcados. O que faltava era o outro lado: o que acontece quando o
usuario **marca** um item de nota ruim.

- `LeftoverWarningKind.Dangerous` (`Core/UninstallSupport.cs:191`) e o novo
  tipo de aviso;
- `DangerousKey` (`:220`) e a chave do texto;
- a regra (`:248`) e uma linha: os itens **marcados** cuja nota nao e segura
  (`LeftoverRatings.MarkedRiskyItems`). Um item marcado e depois desmarcado
  sai do aviso;
- a arvore avisa quando a marcacao muda
  (`Ui/Shared/LeftoversTree.xaml.cs:35`, `SelectionChanged`) e o assistente
  refaz o aviso na hora (`Ui/UninstallWizardWindow.xaml.cs:189`).

O aviso **nao e dialogo**: ele fica no painel de avisos da etapa das
sobras, que e onde a pessoa revisa o que vai ser apagado.

## 5. Onde a nota aparece

- **Na tela**: cada folha da arvore mostra a nota no detalhe, ao lado do tipo
  e do motivo (`Ui/Shared/LeftoversTree.xaml.cs:183`). Os rotulos traduzidos
  moram em `Core/Localization/LeftoverLabels.cs:33` (`Rating`).
- **Na linha de comando**: `tranal /scan "programa"` mostra a nota de cada
  item (`src/Tranal.Cli/Program.cs:894`), com o mesmo texto da tela.
- **Nos catálogos**: cinco chaves novas de rotulo e uma de aviso, nos dois
  idiomas (1437 -> 1443 chaves, as mesmas nos dois).

## 6. Medido na janela de verdade

`tools/test-lote24.ps1` abre o aplicativo publicado, entra na desinstalacao
forcada, analisa uma pasta temporaria e mede a nota na propria interface
(UI Automation). As **8 verificacoes** passaram:

1. a janela principal do Tranal aparece;
2. o botao da desinstalacao forcada existe;
3. a tela de desinstalacao forcada abre;
4. a tela mostra a arvore das sobras;
5. a folha da sobra mostra a nota de cinco niveis — o texto medido na folha
   foi **"Pasta - Alvo informado - Muito bom - E um alvo informado por
   voce"**;
6. a tela de desinstalacao forcada fecha;
7. a linha de comando mostra a nota — a varredura real do **AMD Install
   Manager** devolveu `[Registro] HKEY_LOCAL_MACHINE\...\Uninstall\{7C5812B4-...}
   nota: Muito bom` e `[Pasta] C:\Program Files\AMD\AMDInstallManager nota:
   Muito bom`;
8. a nota da linha de comando e um dos cinco rotulos.

Nada foi removido: o teste fecha a tela sem clicar em "Remover", e a
varredura da linha de comando e **sem `/remove`** (so relatorio). A pasta de
teste e apagada no final.

O nome dos programas vem da propria linha de comando (o `/export`), e nao da
interface: o nome do item da lista chega vazio pela automacao, e um nome
vazio nao descobre programa nenhum.

## 7. Os testes

`src/Tranal.Tests/Lote24FeatureTests.cs` tem **23** testes proprios: a nota
nos cinco niveis, a nota da sobra, o que e seguro e o que e perigoso, os
itens de risco e os itens marcados de risco, o aviso de perigo (aparece,
some quando desmarca, nao aparece para item de nota boa), os rotulos nos
dois idiomas, os dois criterios de pasta (com a pasta real, medidos), a
seguranca da pasta com dado do usuario e a ligacao com a tela e com a linha
de comando.

A suite inteira: **1477 testes verdes** (era 1454).

## 8. O que continua de fora

**BCU T3#49 — os grupos de sobras** continua PARCIAL, e continua honesto: a
tela de sobras e uma **arvore com categorias** (o lote 23 trocou a lista),
com a regra no nucleo (`Core/LeftoverGroups.cs:51`), mas as categorias sao
as do Tranal (sistema de arquivos, registro e as outras que apareceram) e
**nao as 17 do BCU**. As ferramentas com scanner proprio existem e sao as
do BCU (`JunkCleaner`, `RegistryCleaner`, `TrackCleaner`,
`PrivacyTraceCleaner`).

A regra do projeto e a mesma de sempre: **a regra fica em um lugar so e o
resto consome**. A nota mora em `Core/LeftoverRatings.cs`, os criterios da
pasta em `Core/Discovery/FolderConfidenceEvaluator.cs`, e a tela, a linha de
comando e os avisos consomem os tres.
