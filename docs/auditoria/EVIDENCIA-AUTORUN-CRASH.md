# Evidencia medida — o StartUp Tuner e o Crash Analyzer

**Medido em 09/11/2026**, no Windows 11 da maquina de desenvolvimento.

Este documento registra o que a auditoria mediu no **lote 21**: duas
funcionalidades do Ashampoo que estavam em PARCIAL porque **faltavam os
comandos**, nao a regra.

- **T4#47 — StartUp Tuner**: "habilitar, desabilitar, adicionar, editar e
  remover entradas de inicializacao". Habilitar, desabilitar e remover
  existiam; **adicionar** e **editar** nao tinham consumidor na interface.
- **T4#51 — Crash Analyzer**: "relatorios de falha do Windows Event Log,
  agrupados por programa, com export e busca online". A leitura e o
  agrupamento existiam; o **export** e a **busca online** nao.

## 1. A regra que faltava: editar

O nucleo (`Core/Tools/AutorunManager.cs`) tinha `Add`, `SetEnabled` e
`Remove`. Faltava a **edicao** — e nao apenas a chamada da tela: o que o
concorrente edita e o **nome e o comando** da entrada, e cada natureza de item
tem o que pode mudar.

| Natureza | O que a edicao troca | Onde |
|---|---|---|
| Chave de registro | o nome do valor e o comando apontado por ele | `UpdateRunKeyValue` |
| Pasta de inicializacao | o nome do arquivo e o executavel apontado por ele | `UpdateStartupFile` |
| Servico | o nome de exibicao e o binario | `UpdateService` |
| Tarefa agendada | so o executavel da acao | `UpdateScheduledTask` |

O nome de um servico e de uma tarefa agendada e a **identidade** deles no
Windows: nao muda. O que muda e o que aponta para o executavel.

A edicao passa pelas mesmas regras de seguranca da remocao
(`SafetyRules.IsProtectedRegistryKey` e a lista de exclusoes), e item de
sistema nao e editado.

Quando a entrada editada e um **arquivo** da pasta de inicializacao, o
arquivo antigo vai para o **backup** antes de ser trocado — o backup antes de
remover e regra do projeto, e trocar o atalho e remover o conteudo antigo.

## 2. A regra que faltava: o que e uma chave e o que e uma pasta

`AutorunManager.ResolveKind` decide a natureza do item a partir do local
informado: caminho de chave de registro (`HKEY_...`) e chave; pasta e pasta
de inicializacao; vazio e a chave de execucao do usuario.

A regra mora no nucleo, **em um lugar so**: a tela e a linha de comando
perguntam; o nucleo responde. Nenhuma das duas tem uma copia da decisao.

## 3. O export e a busca online do Crash Analyzer

A regra de como uma falha vira uma linha de relatorio mora no nucleo
(`CrashAnalyzer.ToHtmlReport`), e a de como o HTML e escrito, no
`ToolReportWriter` — o mesmo de todas as outras ferramentas. Os **textos**
sao de quem chama: a tela fala o idioma da interface e a CLI fala o dela
(`CrashReportLabels`).

A busca online monta o termo com o nome do programa e a pista do tipo de
evento (`"Programa" crash`, `"Programa" hang`) e usa o motor configurado
(`WebSearch`).

## 4. A linha de comando

```
tranal /autorun                                - lista os itens
tranal /autorun /add "nome" "comando" ["local"] - cria um item
tranal /autorun /edit "nome" ["novoNome"] ["novoComando"] - edita um item
tranal /autorun /enable "nome"                 - ativa um item
tranal /autorun /disable "nome"                - desativa um item
tranal /autorun /remove "nome"                 - remove um item
tranal /crashanalyzer                          - as falhas agrupadas
tranal /crashanalyzer /export "arquivo"        - grava o relatorio HTML
tranal /crashanalyzer /search "programa"       - o endereco da busca online
```

A leitura de varios valores depois de uma opcao mora no `CliArgs`
(`ValuesAfter`): a lista de valores de uma opcao acaba na proxima opcao.

## 5. Medido de ponta a ponta

`tools/test-lote21.ps1` sobe o aplicativo de verdade e a CLI de verdade:
**44 verificacoes**, todas verdes. Entre elas:

- a tela do Gerenciador de Autorun tem "Adicionar" e "Editar", e lista os
  **62** itens de inicializacao medidos na maquina;
- a tela do Crash Analyzer tem "Exportar relatorio" e "Pesquisar na web";
- o ciclo completo pela linha de comando, medido **no registro**: criar
  (o valor aparece), editar (o nome novo aparece, o antigo some, o comando
  novo e o do valor), desativar (o valor ganha o prefixo do estado), ativar
  (volta ao nome normal) e remover (o valor some);
- o relatorio HTML das falhas e gravado e e um HTML valido;
- editar um item que nao existe falha com o motivo.

O teste apaga tudo o que criou (o item de inicializacao do teste) e devolve o
`settings.json` — a maquina de quem roda fica como estava. Medido no fim:
**sobrou valor de teste no registro: nao**.

A suite tem **26 testes proprios** (`Lote21FeatureTests.cs`).
