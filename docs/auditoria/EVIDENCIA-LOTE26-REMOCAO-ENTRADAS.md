# Evidencia do lote 26: remover entradas da lista do Windows, em lote

Medido em 09/11/2026, no codigo de `src/Tranal.Uninstaller/` e
`src/Tranal.Cli/`, na suite de testes e no aplicativo de verdade.

Este lote fecha **quatro** linhas do RECON (o **"Batch Remove Entries"** do
Geek Uninstaller, o **"Remove entry"** do HiBit, do Wise e do Uninstall Tool) e
corrige **cinco** linhas que mediram errado a acao **"Modificar"** — que
existe na tela desde o commit `5ff6428`, e o RECON ainda dizia que nao.

## 1. As linhas

| Recorrencia | Funcionalidade | Antes | Agora |
|---|---|---|---|
| GEEK T1#10 | **Remove Entry** + **Batch Remove Entries** | PARCIAL | OK |
| GEEK #3 (RECON-HIBIT-GEEK-IOBIT-UNINSTALR) | **Batch Remove Entries** — remocao em lote das entradas | AUSENTE | OK |
| HIBIT T1#10 (RECON-CATALOGO-HIBIT-GEEK) | **Deletar entrada** do registro do programa | PARCIAL | OK |
| HIBIT #10 (RECON-HIBIT-GEEK-IOBIT-UNINSTALR) | **Apagar a entrada** do programa como acao avulsa | PARCIAL | OK |
| HIBIT T1#10 (RECON-B) | **Deletar entrada** do registro do programa | PARCIAL | OK |
| WISE T1#8 | **Remover Entradas** (tira o programa da lista sem desinstalar) | PARCIAL | OK |
| WISE #8 (RECON-UNINSTALLTOOL-SOFTORGANIZER-WISE) | **Remover Entradas** | PARCIAL | OK |
| ASHAMPOO #47 | **Remover a entrada** do programa da lista do Windows sem desinstalar | AUSENTE | OK |

E as cinco linhas de **"Modificar"**, que o RECON media como "sem acao na
interface" — medicao desatualizada, corrigida com a evidencia medida:

| Recorrencia | Funcionalidade | Antes | Agora |
|---|---|---|---|
| BCU T5#86 | **Modificar a instalacao** (reparo / alterar instalacao) | PARCIAL | OK |
| TOTALUNINSTALL T1#10 | **Modify** — tela de modificacao de componentes | PARCIAL | OK |
| WISE T1#7 | **Modificar** — reaproveita o instalador do programa | PARCIAL | OK |
| UNINSTALLTOOL #10 | **Acao Modify** (alterar componentes do instalador) | PARCIAL | OK |
| WISE #7 (RECON-UNINSTALLTOOL-SOFTORGANIZER-WISE) | **Modificar** | PARCIAL | OK |

Duas linhas continuam **PARCIAL** por um motivo que continua valendo: a
**configuracao completa do Windows Installer** (instalar, alem de reparar) do
SOFTORGANIZER (#65) e da ASHAMPOO — o reparo e a desinstalacao MSI sao
montados e a tela aciona o reparo, mas `MsiConfigureProduct` nao existe no
codigo. A nota delas foi corrigida (a tela aciona o reparo), e o estado segue
PARCIAL.

Cobertura: de **75,2%** (725 funcionalidades, 515 OK, 179 PARCIAL, 1 AUSENTE,
30 FORA) para **76,0%** (725, 521 OK, 173 PARCIAL, 1 AUSENTE, 30 FORA). Na
prova de cobertura com o Revo Pro dentro (`PARIDADE-11-CONCORRENTES.md`), de
**77,4%** (795 funcionalidades, 583 OK) para **78,1%** (795, 589 OK). O HIBIT
subiu de 72,7% para **74,2%**.

## 2. A regra: uma so, para a acao avulsa e para o lote

Antes deste lote, **remover a entrada do programa da lista do Windows** tinha
duas regras diferentes: a acao avulsa (`WindowsListEntry.Remove`, que apagava a
chave com `RegistryEditor.DeleteKey`) e a remocao em lote de chaves
(`RegistryBatchRemover.Remove`, que respeita a chave protegida, a visao do
registro e o codigo de erro). Duas regras que podem divergir.

Agora a regra esta **em um lugar so**:

- `src/Tranal.Uninstaller/Core/Management/WindowsListEntry.cs:109` — `Remove`
  (a acao avulsa) **delega** para `:136` — `RemoveMany` (a acao em lote);
- `src/Tranal.Uninstaller/Core/RegistryEditing/RegistryBatchRemover.cs:121` —
  `Remove` (a remocao, com `RegistryTarget` por chave ou valor, a protecao de
  chave protegida e o codigo de erro);
- `src/Tranal.Uninstaller/Core/RegistryEditing/RegistryBatchRemover.cs:85` —
  `FromProgram` monta o alvo a partir da entrada do programa, **preservando a
  visao do registro** (32/64 bits): a mesma subchave existe nas duas visoes e
  sao entradas diferentes.

O `Remove` avulso nao apaga mais a chave por conta propria: a prova esta no
teste `A_regra_da_remocao_de_entrada_mora_em_um_lugar_so`, que reprova o
fonte se `RegistryEditor.DeleteKey` voltar a aparecer ali.

O **rotulo** do alvo (`RegistryTarget.Label`, `RegistryBatchRemover.cs:34`) faz
o relatorio dizer de que entrada se trata — "Entrada de desinstalacao: Nome do
programa" — e nao apenas o caminho da chave.

## 3. A tela

A acao em lote esta no menu **"Mais comandos"**
(`src/Tranal.Uninstaller/MainWindow.xaml.cs:1134`, `CmdRemoveEntriesBatch`), e o
metodo (`:1831`, `RemoveEntriesBatch`) faz, nesta ordem:

1. **sem selecao**, avisa e nao faz nada (a lista e multipla, e a selecao e o
   unico alvo — a mesma regra do lote 25);
2. **pergunta antes** (`UserMessages.Confirm`, `Cmd.RemoveEntriesBatchConfirm`) e
   o texto diz, na cara do usuario, que **os programas nao serao desinstalados**
   e que a operacao nao pode ser desfeita;
3. remove pelo nucleo (`WindowsListEntry.RemoveMany`) e mostra o resultado em
   tres numeros: **removidas**, **protegidas** e **com falha** — o mesmo
   `RemovalReport` que a linha de comando consome.

A acao **avulsa** continua no menu de contexto (`Cmd.RemoveFromWindowsList`) e
agora passa pela mesma regra.

## 4. A linha de comando

O comando novo e `tranal /remove-entries "programa" ["programa"]`
(`src/Tranal.Cli/EntryCommands.cs:24`, `TryRun`):

- aceita **varios** programas de uma vez (e um caminho de registro direto);
- lista o que vai ser removido **antes** de remover, e diz que o programa nao
  e desinstalado;
- pede confirmacao, e a flag `/yes` dispensa a pergunta;
- imprime as mesmas contagens do `RemovalReport` (`Removidas`, `Ja
  inexistentes`, `Protegidas`, `Com falha`).

O comando remove as entradas **sem** comando de desinstalador tambem
(`RequireUninstallString = false`): entrada quebrada e exatamente o caso em que
o usuario quer a entrada fora da lista.

## 5. Os testes

`src/Tranal.Tests/Lote26FeatureTests.cs` tem **17** testes proprios:

- o lote **remove** a entrada do programa (chave de teste no `HKCU`, criada e
  apagada pelo teste);
- a entrada removida sai no relatorio **com o nome do programa**;
- **varias** entradas saem no mesmo lote;
- entrada **sem chave conhecida** vira falha — e nao excecao;
- entrada **protegida** nao e removida (o nome do ultimo segmento e o que a
  regra olha);
- entrada que **nao existe mais** vira "ja inexistente";
- a acao **avulsa usa a mesma regra** do lote;
- a acao avulsa devolve falso quando nao ha chave;
- a regra da remocao **mora em um lugar so** (teste sobre o fonte);
- o alvo **preserva o hive e a visao** do registro do programa;
- programa sem chave **nao vira alvo**;
- a interface **tem a acao** em lote, e a confirmacao vem **antes** da remocao
  (teste sobre o fonte, com a ordem conferida dentro do metodo);
- a linha de comando **tem o comando** e avisa que o programa nao e
  desinstalado;
- as chaves novas estao **nos dois idiomas**, e a confirmacao diz que o programa
  continua instalado.

A suite inteira: **1508 testes verdes** (era 1491).

## 6. O teste de ponta a ponta

`tools/test-lote26.ps1` roda o aplicativo de verdade e a linha de comando
publicada, e prova **7** coisas:

1. a entrada de teste foi criada no `HKCU`;
2. a linha de comando diz que o programa **nao** e desinstalado;
3. a linha de comando conta a entrada removida;
4. a entrada de desinstalacao **sumiu do registro**;
5. a janela principal do Tranal aparece;
6. o botao de mais comandos existe;
7. o menu tem a acao de remover entradas em lote.

**Nada de software instalado e tocado.** O teste cria uma entrada de desinstalacao
de teste no `HKCU` de quem o executa (so o registro — o programa nao existe),
apaga essa entrada e limpa tudo no final.

## 7. O que NAO foi medido, e por que

A remocao de entradas de **software real** nao foi medida: e destrutiva, e o
Tranal nao mexe no computador de quem usa sem pedido. O que foi medido:

- a regra, na suite (os 17 testes acima, com chave de teste no `HKCU`);
- a ligacao com a tela e com a linha de comando, no aplicativo de verdade
  (`tools/test-lote26.ps1`).

A regra do projeto e a mesma de sempre: **a regra fica em um lugar so e o
resto consome**. A remocao de entradas esta em
`Core/Management/WindowsListEntry.cs` e `Core/RegistryEditing/RegistryBatchRemover.cs`
— e a tela, a linha de comando e a suite consomem os dois.
