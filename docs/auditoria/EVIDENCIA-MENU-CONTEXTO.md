# Evidencia medida — o gerenciador de menu de contexto

**Medido em 09/11/2026**, no Windows 11 da maquina de desenvolvimento.

Este documento registra o que a auditoria mediu no **lote 20**: o
gerenciador de menu de contexto do Explorer (o "Context Menu Manager" do
HiBit), e — o que importa mais — os **tres defeitos reais** que a medicao
descobriu no modulo que ele consome.

## 1. O modulo existia, mas nunca funcionou

`Core/Management/ContextMenuManager.cs` listava as entradas de menu de
contexto do Explorer. A leitura abria a chave `Software\Classes` e, dentro
dela, abria **de novo** `Software\Classes\*\shellex\ContextMenuHandlers`:

```csharp
using var classes = baseKey.OpenSubKey(@"Software\Classes");
...
using var handlers = classes.OpenSubKey(location);   // location ja tinha "Software\Classes\..."
```

O caminho procurava `HKCU\Software\Classes\Software\Classes\...`, que
nao existe. **O gerenciador devolvia sempre zero entradas** — e nao por
falta de handlers na maquina.

A medicao que prova isso foi feita com uma varredura independente (a
mesma API do registro, fora do Tranal), comparando os dois caminhos:

| Leitura | Entradas encontradas |
|---|---|
| caminho com o prefixo repetido (o do modulo) | **0** |
| caminho relativo a `Software\Classes` (o certo) | **37** |

A maquina tem 37 entradas de menu de contexto de terceiros: `FileSyncEx`
(OneDrive), `IObitUninstaller`, `McCtxMenu` (McAfee), mais as do proprio
Windows ("Open With", "Sharing", "Enviar para", "Fixar na barra de
tarefas"...).

## 2. As duas convencoes do Explorer

A mesma varredura mostrou que o Explorer usa **duas** convencoes e elas
convivem na mesma pasta:

| Chave | Valor padrao | Leitura antiga |
|---|---|---|
| `Open With` | `{09799AFB-AD67-11d1-ABCD-00C04FC30936}` (CLSID) | CLSID no valor padrao |
| `{90AA3A4E-1CBA-4233-B8BB-535773D48449}` | `Taskband Pin` (nome) | **nao via** (valor nao e CLSID) |

Seis entradas da maquina (`{474C98EE-...}`, `{596AB062-...}`,
`{D6791A63-...}`, `{fbeb8a05-...}`) tem o valor padrao **vazio** e o CLSID
**no nome da chave** — e o modulo as descartava.

A regra agora e uma so (`ContextMenuManager.PareceClsid`): o CLSID e o dos
dois (nome da chave, valor padrao) que parece um CLSID; o outro e o nome
que aparece no menu.

## 3. A mesma chave lida duas vezes

O modulo percorria as duas vistas do registro (64 e 32 bits). Em
`HKLM\Software\Classes`, as duas vistas veem as **mesmas** chaves: cada
entrada saia **duas vezes** na lista, com o mesmo identificador.

Agora a chave e lida uma vez so, pela marca (hive + local + nome).

## 4. O que o gerenciador entrega

A tela nova (`Ui/Tools/ContextMenuViews.cs`, botao "Gerenciador de menu de
contexto" na barra lateral) mostra, em uma lista so:

- as **4** entradas do Tranal (a de desinstalar, a de rastrear a
  instalacao, a de shredar e a do monitor de instalacao);
- as **37** entradas de terceiros, com o dono de cada uma.

O dono e medido, nao suposto: a DLL que implementa a entrada e casada com
a lista de instalados pelo mesmo casamento do Modo Cacador
(`HunterMode.ResolveExecutables`, uma varredura so para todas as entradas
— resolver uma por uma faria uma varredura por entrada).

| Entrada | Implementacao | Classe |
|---|---|---|
| `FileSyncEx` | `FileSyncShell64.dll` (OneDrive) | programa |
| `IObitUninstaller` | `IUMenuRight.dll` (IObit) | programa |
| `McCtxMenu` | `mc-ctxmnu.dll` (McAfee) | programa |
| `Open With`, `Sharing`, `Enviar para`... | `shell32.dll`, `ntshrui.dll` | Windows |

A entrada do **Windows** nao e sobra de programa: remove-la quebraria o
Explorer. O gerenciador **recusa** a remocao delas e diz por que.

Quando o programa nao aparece na lista de instalados (o OneDrive e o
McAfee nao tem "local de instalacao" no registro), a identificacao honesta
e o **nome da DLL** que implementa a entrada — nao um nome adivinhado.

## 5. As acoes

| Acao | Onde | O que faz |
|---|---|---|
| **Restaurar** | `ContextMenuOverview.Restore` | registra as 4 entradas do Tranal no Explorer |
| **Desativar** | `ContextMenuOverview.Disable` | remove as 4 entradas do Tranal |
| **Remover** | `ContextMenuOverview.Remove` | remove a entrada selecionada |
| **Criar entrada** | `ContextMenuOverview.Create` | cria um verbo novo, com o nome e o comando escolhidos |
| **Apagar tudo** | `ContextMenuOverview.RemoveAll` | remove todas as entradas removiveis, **guardando as chaves em um .reg antes** |

O backup antes de remover e regra do projeto ("backup antes de remover"):
as chaves que somem do Explorer sao gravadas em um `.reg` importavel
(`reg import`), e a mensagem diz onde o arquivo ficou.

A regra de gravar o verbo do Explorer esta em **um lugar so**
(`ExplorerContextMenu.WriteShellEntry`): a entrada de desinstalar, as
entradas do Tranal e as entradas criadas pelo usuario passam todas por
ele.

## 6. A linha de comando

```
tranal /contextmenu                     - lista as entradas
tranal /contextmenu /restore            - registra as entradas do Tranal
tranal /contextmenu /disable            - remove as entradas do Tranal
tranal /contextmenu /remove "id"        - remove uma entrada
tranal /contextmenu /create "nome" "comando" - cria uma entrada
tranal /contextmenu /removeall          - apaga todas (com backup)
```

## 7. Medido de ponta a ponta

`tools/test-contextmenu.ps1` sobe o aplicativo de verdade e a CLI de
verdade: **37 verificacoes**, todas verdes. Entre elas:

- o botao existe na barra lateral e a tela abre, com as 5 colunas
  (Entrada, Dono, Local, Estado, Implementacao);
- a tela e a linha de comando mostram a **mesma** quantidade de entradas;
- uma entrada de terceiro criada so para o teste aparece na listagem e pode
  ser removida pelo nome;
- uma entrada criada pelo gerenciador aparece e pode ser removida;
- remover uma entrada do Windows e **recusado**, com o motivo;
- "restaurar" e "desativar" fazem a ida e a volta: a maquina volta ao
  estado inicial das entradas do Tranal.

O teste apaga tudo o que criou (a chave de registro de teste, a CLSID de
teste e a entrada criada) e devolve o `settings.json` — a maquina de quem
roda fica como estava.
