# Matriz consolidada — Revo + concorrentes × Tranal

> A pergunta que esta matriz responde, do jeito que o dono pediu:
> **"não quero perder em nada pro revo, precisamos ser igual ou melhor que o
> revo em TUDO."**
>
> Cada linha é uma **funcionalidade** observada em algum produto. As colunas
> dizem quem tem. A coluna do Tranal é o estado **medido** do código
> (`src/Tranal.Uninstaller/`), não uma intenção.

Legenda: ✅ tem · 🟡 parcial · ⬜ não tem · ❓ auditoria pendente

## Como ler

| Coluna | Origem |
|---|---|
| **Revo** | `docs/PARIDADE.md` — 42 seções, 1.136 strings do `lang\english.ini` do payload do Revo Pro 5.5.4 |
| **BCU** | `docs/auditoria/BCU.md` — 80 funcionalidades medidas no Bulk Crap Uninstaller 6.3.0 |
| **Tranal** | código e testes do repositório, medidos |
| **Prioridade** | 🔴 muda a arquitetura · 🟡 lacuna de recurso · 🟢 ganho pequeno |

> Uma coluna por concorrente entra aqui conforme a auditoria do software
> correspondente for concluída. A ordem de entrada é a do dono: BCU, HiBit,
> Geek, IObit, Uninstalr, Ashampoo, Total Uninstall, Uninstall Tool, Soft
> Organizer, Wise.

---

## A. Descoberta e classificação

| # | Funcionalidade | Revo | BCU | Tranal | Prioridade |
|---|---|---|---|---|---|
| A1 | Enumeração pelo registro (32 e 64 bits) | ✅ | ✅ | ✅ | — |
| A2 | **Classificação do desinstalador** (MsiExec, Inno, NSIS, InstallShield, SDBInst, Steam, Store, WinFeature, WinUpdate, Chocolatey, Oculus, PowerShell, Simple Delete) | 🟡 | ✅ (14 tipos) | ✅ (12 tipos) | — |
| A3 | Detecção de **Windows Store Apps** | ✅ | ✅ | ✅ (122 MSIX) | — |
| A4 | Detecção de **Windows Features** (WMI) | ⬜ | ✅ | ⬜ | 🟡 |
| A5 | Detecção de **Windows Updates** (WUA) | ⬜ | ✅ | ⬜ | 🟡 |
| A17 | **Remoção de extensões de navegador** com base maliciosa/ad | ⬜ | ⬜ | ⬜ | 🟡 |
| A6 | Detecção de **aplicativos Steam** | ⬜ | ✅ | ⬜ | 🟡 |
| A7 | Detecção de **Chocolatey / Scoop** | ⬜ | ✅ | ⬜ | 🟢 |
| A8 | Detecção de **Oculus** | ⬜ | ✅ | ⬜ | 🟢 |
| A9 | **Varredura de drives** atrás de apps não registrados | ⬜ | ✅ | ⬜ | 🟡 |
| A10 | Detecção de **produtos MSI** por GUID | 🟡 | ✅ | ⬜ | 🟡 |
| A11 | Marcação de **componente do sistema** | ✅ | ✅ | ✅ | — |
| A12 | Marcação de **protegido** (`NoRemove`) | ✅ | ✅ | ⬜ | 🟡 |
| A13 | Marcação de **atualização** de outro app | 🟡 | ✅ | ⬜ | 🟢 |
| A14 | Detecção de **desinstalador corrompido/ausente** | ✅ | ✅ | ⬜ | 🟡 |
| A15 | **Pastas de instalação personalizadas** do usuário | ⬜ | ✅ | ⬜ | 🟢 |
| A16 | **Filtros rápidos** (só básicas / só sistema / só auto-inicialização / só navegadores) | ✅ | ✅ | ✅ | — |

## B. Desinstalação

| # | Funcionalidade | Revo | BCU | Tranal | Prioridade |
|---|---|---|---|---|---|
| B1 | Rodar o desinstalador nativo | ✅ | ✅ | ✅ | — |
| B2 | **Desinstalação silenciosa** | ✅ | ✅ | ⬜ (modo `builtin` só) | 🔴 |
| B3 | **Gerar desinstalador silencioso ausente** (inclusive por automação de interface) | 🟡 | ✅ | ⬜ | 🔴 |
| B4 | **Desinstalar via MsiExec** (GUID) | 🟡 | ✅ | ⬜ | 🟡 |
| B5 | **Desinstalar manualmente** (pular o desinstalador, listar o que apagar) | 🟡 | ✅ | ⬜ | 🟡 |
| B6 | **Desinstalar por janela / diretório / arquivo** | ✅ | ✅ | 🟡 (overlay) | 🟢 |
| B7 | **Desinstalação simultânea** com prevenção de colisão | ⬜ | ✅ | ⬜ | 🟡 |
| B8 | **Ordenação inteligente** (loud primeiro) | ⬜ | ✅ | ⬜ | 🟢 |
| B9 | **Simulação / dry run** | 🟡 | ✅ | ✅ (`/scan`) | — 🟢 Tranal melhor |
| B10 | **Painel de progresso** com estados (executando/aguardando/concluído/falhou/pulado) | ✅ | ✅ | 🟡 | 🟡 |
| B11 | Controle manual do progresso: **pular / encerrar / rodar agora** | ⬜ | ✅ | 🟡 (só matar) | 🟡 |
| B12 | **Abortar a tarefa** sem matar o desinstalador em execução | ⬜ | ✅ | ⬜ | 🟢 |
| B13 | **Monitorar silencioso** atrás de popups (daemon de automatização) | ⬜ | ✅ | ⬜ | 🟡 |
| B14 | **Finalizar automaticamente** desinstalador travado | ✅ | ✅ | ✅ | — |
| B15 | **Comandos antes e depois** da desinstalação | 🟡 | ✅ | ⬜ | 🟡 |
| B16 | **Assistente de desinstalação em etapas** | ✅ (5 etapas) | ✅ (6 etapas) | ✅ (5 etapas) | — |
| B17 | Lista de **aplicativos relacionados** que talvez queiram sair junto | 🟡 | ✅ | ⬜ | 🟡 |
| B18 | **Remover da tarefa** quem não desinstala em silêncio | 🟡 | ✅ | ⬜ | 🟢 |
| B19 | **Impedir desligamento/reinício** durante a tarefa | ⬜ | ✅ | ⬜ | 🟢 |
| B20 | **Ponto de restauração** antes de desinstalar | ✅ | ✅ | ✅ | — |
| B21 | **Backup completo do registro** antes de desinstalar, com script de restauração | ✅ | 🟡 (só .reg) | ✅ | — 🟢 Tranal melhor |

## C. Sobras e confiança

| # | Funcionalidade | Revo | BCU | Tranal | Prioridade |
|---|---|---|---|---|---|
| C1 | Varredura de sobras após a desinstalação | ✅ | ✅ | ✅ | — |
| C2 | **4 modos** (builtin / seguro / moderado / avançado) | ✅ | 🟡 (varredura de lixo por nível) | ✅ | — |
| C3 | **Níveis de confiança** | 🟡 (negrito = alta) | ✅ (5 níveis) | ✅ (3 níveis) | — |
| C4 | **Justificativa textual por item** ("o diretório é usado por outros aplicativos") | ⬜ | ✅ | ✅ (11 motivos, coluna "Porquê") | — |
| C5 | **Atribuição pela cadeia de desinstalação** (item citado explicitamente) | 🟡 | ✅ | ⬜ | 🔴 |
| C6 | Confiança de **pasta** (vazia, executáveis presentes, editor reconhecido…) | ⬜ | ✅ (7 critérios) | ⬜ | 🟡 |
| C7 | Confiança de **inicialização** (RunOnce, compatível com o app) | ⬜ | ✅ | ⬜ | 🟢 |
| C8 | Aviso ao mexer em item de **baixa confiança** | ⬜ | ✅ | 🟡 (só não marca) | 🟢 |
| C9 | **Backup antes de remover sobras** | ✅ | ✅ | ✅ | — |
| C10 | Arquivos de sobra vão para a **lixeira** | ✅ | ✅ | ✅ | — |
| C11 | **Exportar lista de sobras** | ⬜ | ✅ | ⬜ | 🟢 |
| C12 | **Limpeza de pastas "Arquivos de Programas"** (vazias e pela metade) | 🟡 | ✅ | ⬜ | 🟡 |
| C13 | **Gerenciador de inicialização** integrado | ✅ | ✅ | 🟡 | 🟡 |
| C14 | Limpeza de **regras de firewall**, UserAssist, relatórios de erro, CLSID, áudio, eventos | ⬜ | ✅ (17 grupos) | 🟡 (ferramentas próprias) | 🟢 |
| C15 | Desinstalador **simples gerado** para app órfão | ⬜ | ✅ | ⬜ (decisão do §8 do PARIDADE) | 🟢 |

## D. Interface, filtro e automação

| # | Funcionalidade | Revo | BCU | Tranal | Prioridade |
|---|---|---|---|---|---|
| D1 | **Colunas customizáveis** | ✅ | ✅ (18 fixas) | ✅ (14) | 🟡 (mais colunas) |
| D2 | **Filtro por Regex** e condições (incluir/excluir, negação) | ⬜ | ✅ | ⬜ | 🟡 |
| D3 | **Lista de desinstalação em arquivo** (`.bcul`) | ⬜ | ✅ | ⬜ | 🟡 |
| D4 | **Desinstalação em lote roteirizável** pela linha de comando | ⬜ | ✅ | ⬜ | 🟡 |
| D5 | **Treemap** | ✅ | ✅ | ⬜ | 🟢 |
| D6 | **Legenda de cores** | ✅ | ✅ | ⬜ | 🟢 |
| D7 | **Classificação do usuário** (estrelas) | ⬜ | ✅ | ⬜ | 🟢 |
| D8 | **Verificação de certificado** do desinstalador | ✅ | ✅ | ⬜ | 🟡 |
| D9 | **Cópia avançada** para a área de transferência (padrão composto) | 🟡 | ✅ | ⬜ | 🟢 |
| D10 | **Exportar** dados do programa | ✅ | ✅ (XML) | ✅ (CSV/XML/HTML) | — |
| D11 | **Exportar para script** de desinstalação em lote | ⬜ | ✅ | ⬜ | 🟢 |
| D12 | Script PowerShell para remover Store Apps | ⬜ | ✅ | ⬜ | 🟢 |
| D13 | **Renomear entrada** | 🟡 | ✅ | ⬜ | 🟢 |
| D14 | **Excluir entrada do registro** sem desinstalar | ✅ | ✅ | ⬜ | 🟢 |
| D15 | **Assumir a propriedade** | ⬜ | ✅ | ⬜ | 🟢 |
| D16 | Abrir chave no **Regedit** / Programas e Recursos / Recuperação do Sistema | 🟡 | ✅ | ⬜ | 🟢 |
| D17 | **Pesquisar on-line** o programa (Google, AlternativeTo, GitHub…) | 🟡 | ✅ | 🟡 | 🟢 |
| D18 | **Painel lateral** de configurações | 🟡 | ✅ | ⬜ | 🟢 |
| D19 | **Agrupamento** automático pela coluna de ordenação | 🟡 | ✅ | ⬜ | 🟢 |
| D20 | **Modo para daltônicos** / DPI / tema do sistema | 🟡 | ✅ | 🟡 | 🟢 |
| D21 | **Grupos e anotações** do usuário | ✅ | ⬜ | ✅ | — |
| D22 | **Galeria de ícones** | ✅ | ⬜ | ✅ | — |
| D23 | **Atualização automática** | ✅ | ✅ | ✅ | — |

## E. Ferramentas do painel (fora do núcleo do desinstalador)

| # | Funcionalidade | Revo | BCU | Tranal | Prioridade |
|---|---|---|---|---|---|
| E1 | **Gerenciador de autoruns** | ✅ | ✅ | ✅ (346 itens) | — |
| E2 | **Limpeza de rastros** (navegador, Windows, Office) | ✅ | ⬜ | ✅ | — |
| E3 | **Exclusão irrecuperável** | ✅ | ⬜ | ✅ | — |
| E4 | **Limpeza de arquivos temporários** | ✅ | ⬜ | ✅ | — |
| E5 | **Otimizador de registro** | ✅ | ⬜ | ✅ | — |
| E6 | **Monitor de instalação** (AppBar / log de mudanças) | ✅ | ✅ (tweaks) | ✅ | — |
| E7 | **Janela flutuante sobre o app** (kill, desinstalar, propriedades) | ✅ | ⬜ | ✅ | — |
| E8 | **Menu de contexto do Explorer** | ✅ | ⬜ | ✅ | — |
| E9 | **Gerenciador de backups** (restaurar tudo, item a item, por tempo) | ✅ | ⬜ | ✅ | — |
| E10 | Remoção definitiva de **drivers** | ✅ | 🟡 | ⬜ | 🟡 |

---

## F. Backlog priorizado — "o que implementar"

Ordenado pelo que **desbloqueia mais coisa** por unidade de esforço. Cada
item aponta para a auditoria que o revelou.

### F.1 🔴 Núcleo — muda a arquitetura

| # | Item | De onde veio | Por quê |
|---|---|---|---|
| 1 | ✅ **Classificar o desinstalador** (enum `UninstallerKind`, em `Core/UninstallerKind.cs`) — 12 tipos, medidos nos 211 programas desta máquina | BCU A2 | Habilita silencioso, MsiExec, Store App, órfão, PowerShell. É o eixo de tudo. |
| 2 | ✅ **Atribuir sobras pela cadeia de desinstalação**, não só pelo nome — **implementado pela varredura de MSI** (`Core/MsiInstallLog.cs`): o pacote `.msi` em cache é lido pela API do próprio Windows Installer e diz, arquivo por arquivo e chave por chave, o que o instalador gravou. O log é capturado **antes** do desinstalador nativo (depois o pacote sai do cache) e conferido **depois**. Medido nesta máquina: **149 de 149** pacotes MSI abriram; 26.210 arquivos e 21.105 valores de registro lidos; **63,1%** dos caminhos de arquivo e **83,0%** dos valores de registro resolvem para item que existe hoje. Novo motivo `LeftoverReason.InstallerLog` — o mais forte que existe, porque não é casamento de nome, é o registro do instalador | BCU C5 · **Geek H.4** | Separa "a pasta do programa" de "a pasta **dele**". Errar aqui é apagar coisa de outro. |
| 3 | ✅ **Justificativa textual por item de sobra** — enum `LeftoverReason` (11 motivos), coluna "Porquê" na lista de sobras e na saída da CLI; a tradução mora em `Core/Localization/LeftoverLabels.cs`, consumida pela tela e pela CLI | BCU C4 | O Tranal já tem 3 níveis; agora diz **por quê**. A regra de marcagem não mudou: só confiança alta entra na remoção. |
| 4 | ✅ **Desinstalação silenciosa** — tabela de switches por família (`/VERYSILENT /SUPPRESSMSGBOXES /NORESTART` para Inno, `/S` para NSIS, `/quiet /qn /norestart` para MSI, `/s /v"/qn"` para InstallShield), com a classificação como chave; coluna "Desinstalador silencioso", marca no assistente e `/silent` na CLI. **185 dos 211 programas** da máquina (87,7%) | BCU B2/B3 · **HiBit H.2** | Recurso que o dono sente na prática: sai do PC sem clicar 40 vezes. O HiBit mostra que a tabela existe e é curta — com a classificação pronta, isso deixa de ser heurística. |
| 5 | ✅ **Motor de desinstalação próprio**: rodar o desinstalador do programa **e** automatizar o que ele não faz (o Uninstalr é explícito: "most other uninstallers simply run the default uninstaller program"), com retomada após reinício — Motor de desinstalação próprio: o plano (`Core/Automation/UninstallPlan.cs`) ordena as etapas (comando antes, desinstalador nativo, varredura, remoção, comando depois) e o executor (`UninstallPlanExecutor`) percorre todas sem que a falha de uma engula as outras. A retomada após reinício é a sessão (`Core/Automation/UninstallSession.cs`), que grava o passo concluído. **A parte de dirigir a interface do desinstalador de terceiros (o sentido do Uninstalr) não foi feita** — exige automação de interface.| BCU B3 · **Uninstalr H.3** | É o que torna a desinstalação silenciosa confiável: em vez de rezar para o desinstalador aceitar `/S`, o Tranal aciona os controles. |
| 6 | ✅ **Filtro Regex + lista de desinstalação em arquivo + desinstalação em lote roteirizável** — Filtro Regex + lista em arquivo + desinstalação em lote roteirizável: `Core/Automation/BatchList.cs` lê um arquivo `.txt`/`.csv` com nome exato ou `regex:` seguido de expressão regular (regex inválida é recusada com a mensagem e a linha). A CLI ganhou `tranal /batch "arquivo" [/remove]`, que faz dry-run por padrão.| BCU D2/D3/D4 | É o que torna o Tranal usável em script e em grande escala. |
| 7 | ✅ **Tracing de usuário em tempo real, sem driver** — o Soft Organizer grava o que o instalador mudou (registro **e** disco) com rastreio de arquivo em tempo real e **n**ão usa driver (`K_DRIVER_SYS`: 0 `.sys`). O Tranal trocou o **polling** de 30 s (`Core/Modules/InstallMonitor.cs:31`) pela observação de escrita: `Core/Tracing/WriteWatcher.cs` (leitura bloqueante na thread dedicada, interrompida por `CancelIoEx` — `OVERLAPPED` não funciona neste ambiente) grava no log v2 (`Core/Tracing/TraceLogV2.cs`), que guarda o tipo de mudança de cada entrada (`Core/Tracing/TraceEntry.cs`) | **Soft Organizer H.9** | Troca **polling** por observação de escrita, e o `TraceStore` passa a ser o registro canônico do que mudou. Sem driver: a decisão F.4 continua de pé. |

### F.2 🟡 Recurso — lacunas claras

| # | Item | De onde veio |
|---|---|---|
| 8 | ✅  Desinstalar via MsiExec (GUID) — Desinstalar via MsiExec (GUID): `Core/Discovery/MsiProductCatalog.cs` lista os produtos MSI com o ProductCode e monta `msiexec /x {GUID}`; GUID inválido é recusado com mensagem.| BCU B4 |
| 9 | ✅  Desinstalar manualmente (pular o desinstalador e listar o que apagar) — Desinstalar manualmente: `Core/Automation/ManualRemoval.cs` monta a lista de candidatos com a confiança de cada um e **não remove nada**; a remoção manual fica com o usuário.| BCU B5 |
| 10 | ✅  Painel de progresso com pular / encerrar / rodar agora — Painel de progresso com pular / encerrar / rodar agora: o modelo é `Core/Automation/RemovalProgress.cs` (testável sem interface) e a superfície é `Ui/Automation/AutomationWindow.xaml`. O assistente liga os três comandos: pular a varredura, encerrar a operação e tirar o adiamento para o próximo reinício.| BCU B10/B11 |
| 11 | ✅  Detecção de Windows Features e Windows Updates — Windows Features e Windows Updates: `Core/Discovery/WindowsFeatures.cs` lê os recursos opcionais pelo DISM e as atualizações pelo COM `Microsoft.Update.Session`, declarando a origem de cada informação (medido x inferido).| BCU A4/A5 |
| 12 | ✅  Detecção de Steam, Chocolatey, Scoop — Steam, Chocolatey e Scoop: `Core/Discovery/PackageManagerCatalog.cs` lê os VDF/ACF do Steam, a pasta `lib` do Chocolatey e a pasta `apps` do Scoop, e diz quem **não** tem desinstalador próprio.| BCU A6/A7 |
| 13 | ✅  Varredura de drives atrás de apps não registrados — Varredura de drives atrás de apps não registrados: `Core/Discovery/DriveScanner.cs` é opt-in, respeita o `SafeTraversal` (não atravessa atalho) e tem limite explícito de profundidade, pastas e arquivos.| BCU A9 |
| 14 | ✅  Limpeza de pastas "Arquivos de Programas" vazias/pela metade — Limpeza de pastas de programas vazias: `Core/Discovery/EmptyFolderCleaner.cs` só mexe abaixo das pastas de programas, nunca remove a própria `Program Files` e envia para a Lixeira.| BCU C12 |
| 15 | ✅  Comandos antes e depois da desinstalação — Comandos antes e depois da desinstalação: `UninstallPlanOptions.PreCommand` e `PostCommand` entram no plano como etapas; o comando do usuário roda por `cmd.exe` com `ShellCommand.Quote` na interpolação.| BCU B15 |
| 16 | ✅  Verificação de certificado do desinstalador — Verificação de certificado do desinstalador: `Core/Discovery/UninstallerCertificate.cs` usa o `WinVerifyTrust` (o veredito do Windows) e a cadeia do `X509Chain` para explicar; devolve **estado**, nunca exceção.| BCU D8 |
| 17 | Colunas novas: ✅ Tipo de desinstalador, ✅ Código do produto (GUID), ✅ Desinstalador silencioso, ✅ Localização do desinstalador, ✅ Fonte, ✅ Componente do sistema, ✅ Protegido, ✅ Inicialização, ✅ Estrelas e ✅ Grupo — todas em `Core/ProgramColumns.cs`, com o valor **independente do idioma** (o identificador fixo da origem, o do tipo do desinstalador) e a coluna de inicialização **vazia enquanto não há dado** | BCU D1 |
| 18 | ✅  Gerenciador de inicialização integrado ao ciclo de desinstalação — Gerenciador de inicialização integrado ao ciclo de desinstalação: `Core/Management/AppPermissionManager.cs` correlaciona as entradas de inicialização (via `AutorunManager`) com a pasta de instalação do programa.| BCU C13 |
| 19 | ✅  Remoção definitiva de drivers — Remoção definitiva de drivers: `Core/Management/DriverManager.cs` lista os pacotes da Driver Store pelo `pnputil` (pelo nome publicado `oemNN.inf`, que não depende do idioma) e remove com `pnputil /delete-driver`. Pacote de fabricante Microsoft é protegido.| Revo E10 |
| 20 | ✅ **Um switch de CLI por ferramenta** (o HiBit tem 19) + a forma longa com sufixo `RUN` — Um switch de CLI por ferramenta + a forma longa com sufixo RUN: o catálogo é `Core/Automation/CliToolSwitch.cs` (nome curto, nome longo e a forma `--ferramenta-run`). A CLI lista com `tranal /switches`.| HiBit H.2 |
| 21 | ✅ **Painel único de "Software Health"** com 4 categorias: sobras, arquivos redundantes, permissões e notificações — Painel único de Software Health com 4 categorias: `Core/Management/SoftwareHealth.cs` reúne sobras, arquivos repetidos, permissões e avisos em um relatório só, consumindo a varredura de sobras e os avisos que o núcleo já tem.| IObit · **Wise H.5** — dois concorrentes independentes, mesma ideia: deixa de ser nicho |
| 22 | ✅ **Histórico de desinstalações com "Recuperar"** — o histórico canônico é o `UninstallHistory` (`Core/UninstallHistory.cs`), agora com identificador, versão, editora, itens removidos e o ponteiro do backup (`RecoveryId`); a recuperação mora em `Core/Recovery/` (`LeftoverBackup` guarda o conteúdo, `ProgramBackup` devolve, `RecoverOperation` registra o undo/redo) e o backup das sobras entra no fluxo **antes** de apagar (`UninstallEngine.BackupLeftovers`), gravando o identificador no histórico | **Wise H.5** · **Uninstall Tool H.7** · **Soft Organizer H.9** | Três concorrentes pedem a mesma peça: o Wise chama de "Recuperar", o Uninstall Tool de "reverter a instalação rastreada", o Soft Organizer de "Undo Changes Center" — **um item só**, não três |
| 23 | ✅ **Backup dos resíduos** antes de apagá-los, com expiração — `Core/Recovery/LeftoverBackup.cs` copia o conteúdo (arquivo, pasta e o valor do registro, com o **tipo real**) para `%LOCALAPPDATA%\Tranal Group\Tranal Uninstaller Pro\backups\leftovers`, grava um manifesto e expira o que passou do prazo (`Expire`); o backup entra no fluxo de remoção (`UninstallEngine.BackupLeftovers`) e a restauração devolve arquivo e registro | **Wise H.5** — o Tranal fazia backup do registro, não dos resíduos |
| 24 | ✅ **Desinstalação Forçada** como modo explícito, e **Desinstalação Personalizada** por palavra-chave — Desinstalação Forçada como modo explícito e Desinstalação Personalizada por palavra-chave: `Core/Automation/ForcedRemovalOptions.cs` — o alvo apontado é o item garantido, e a palavra-chave casa por palavra inteira (`NameMatch.ContainsWord`), de modo que "Adobe" não casa com "AdobeFresco".| **Wise H.5** |
| 25 | ✅  Gerenciador de permissões de app: serviço, inicialização, menu de contexto, tarefa agendada — Gerenciador de permissões de app (serviço, inicialização, menu de contexto, tarefa agendada): `Core/Management/AppPermissionManager.cs` e `Core/Management/ContextMenuManager.cs` — os quatro tipos em uma lista só, por programa.| **Wise H.5** |
| 26 | ✅ **Motor de regras em arquivo + base de regras por aplicativo** — `Core/Rules/` (`RemovalRule`, `RuleEngine`, `BuiltinRules`): o arquivo `rules.json` tem padrão, categoria (`Protect`/`Exclude`/`Include`/`Cleanup`) e escopo (geral ou por aplicativo), com precedência fixa (a proteção do sistema vence tudo; `Protect` > `Exclude` > `Include` > `Cleanup`; a regra do app vence a geral; empate: a primeira). **A regra fica em um lugar só**: `Core/UninstallSupport.cs` (a varredura de sobras) e `Core/Tools/JunkCleaner.cs` (a limpeza de lixo) consomem o mesmo julgamento — `Exclude` não varre, `Protect` desmarca, e uma regra escrita como caminho protege o caminho no limpador | **Wise H.5** · **Total Uninstall H.6** | |
| 27 | ✅ **Desinstalação em lote na interface** (seleção múltipla → remover em bloco) — a lista principal passou de `SelectionMode="Single"` para `"Extended"` (`MainWindow.xaml`), e o caminho de lote **existia** mas nunca chegava a N > 1. A confirmação do lote passou a usar `UserMessages.Confirm` (nunca omitida) e a mensagem foi traduzida (`Cmd.UninstallManyConfirm`) | **Uninstall Tool H.7** · **Total Uninstall H.6** |
| 28 | ✅ **Log de remoção por item, com código de erro** — implementado em `Core/RemovalLog.cs` (a regra: o código de erro, o CSV, o JSON e o nome do arquivo), com `RemovalOutcome.ErrorCode` recebendo o código do Windows nos blocos de captura. A interface grava e mostra o caminho; a CLI grava e mostra o caminho. O `Core/MsiInstallLog.cs` continua sendo o *leitor* do log do instalador — este e o *escritor* do log de remoção. **15 testes novos** | **Uninstall Tool H.7** · **Geek H.4** |
| 29 | ✅ **Exportar a varredura de sobras em XML** — `Core/Reporting/LeftoverReportExporter.cs` exporta a *varredura* (não só a lista de programas), com o motivo e a confiança de cada item | **Uninstall Tool H.7** |
| 30 | ✅ **Fluxo de snapshot pré/pós-instalação com comparação A×B** — `Core/Tracing/SnapshotComparer.cs` compara dois `SnapshotState` e expõe criados, modificados e removidos (o `SystemSnapshot.Diff` legado só expunha `NewFiles`/`NewFolders`/`NewRegistryKeys`); a comparação fica na tela de rastreio (`Ui/Tracing/TracingWindow.xaml`) | **Total Uninstall H.6** |
| 31 | ✅ **Biblioteca de snapshots nomeados** — `Core/Tracing/SnapshotLibrary.cs`: criar, listar, apagar, carregar e comparar (`Save`/`List`/`Get`/`LoadState`/`Delete`/`Compare`), com metadados (nome, rótulo, data) | **Total Uninstall H.6** · **Ashampoo H.8** |
| 32 | ✅ **Desinstalação dirigida pelo log de instalação** — `Core/Tracing/ForensicAnalyzer.cs` monta o plano de remoção a partir do log (`BuildPlan(MsiInstallLog)` e `BuildPlan(TraceLogV2)`), com a procedência de cada item (`medido` ou `inferido`) e a conversão para `LeftoverItem`: a remoção pode vir do log, não só do desinstalador nativo | **Total Uninstall H.6** · **Ashampoo H.8** |
| 33 | ✅ **Backup do programa com restauração completa e undo/redo** — `Core/Recovery/ProgramBackup.cs`: o perfil do programa (`ProgramProfile`, com arquivos, pastas e valores de registro), o registro das operações (`RecoverOperation`, com `Undo` e `Redo`) e a restauração que devolve o conteúdo do backup ao disco | **Total Uninstall H.6** | |
| 34 | ✅ **Exportação/importação portátil de programas monitorados** — `Core/Reporting/PortableArchive.cs` exporta e importa um `.zip` autocontido (`Export`/`Import`) e `ProgramListImporter` importa a lista de programas exportada, casando cada item com o instalado por nome e versão | **Total Uninstall H.6** |
| 35 | ✅ **Operações pendentes de renomeação visíveis sem reiniciar** — `Core/Recovery/PendingOperations.cs` lê a fila oficial do Windows (`PendingFileRenameOperations`), mostra as operações pendentes e tenta aplicá-las na hora; quando o Windows segura o arquivo, a mensagem diz isso e a operação continua na fila | **Total Uninstall H.6** | |
| 36 | ✅ **Análise forense pós-instalação** — `Core/Tracing/ForensicAnalyzer.cs`: `Analyze` classifica a procedência de cada mudança (`medido`/`inferido`) e monta o plano de remoção a partir do log de um programa **já instalado**, sem nunca apresentar inferência como medição | **Ashampoo H.8** |
| 37 | ✅ **Gerenciador próprio de serviços e tarefas agendadas** — hoje o Tranal só **abre** `services.msc` e `taskschd.msc` (`Core/Tools/WindowsToolsCatalog.cs:85,78`) — Gerenciador próprio de serviços e tarefas agendadas: `Core/Management/ServiceManager.cs` (WMI `Win32_Service`) e `Core/Management/ScheduledTaskManager.cs` (os arquivos XML de `%SystemRoot%\System32\Tasks`, lidos porque o `schtasks` traz o cabeçalho no idioma do sistema). O `schtasks.exe` é o Windows que executa a ação.| **Ashampoo H.8** |
| 38 | ✅ **Importar lista exportada e editar entrada do log de instalação** — `Core/Reporting/PortableArchive.cs` (`ProgramListImporter.Import`/`ImportContent`) importa a lista exportada e casa cada item com o programa instalado; `Core/Reporting/TraceLogExporter.cs` (`TraceLogEditor`) adiciona e remove entrada de arquivo, pasta e chave de registro | **Ashampoo H.8** |
| 39 | ✅ **Exportar o log de instalação em HTML/CSV/TXT** e **exportar as alterações de registro para `.reg`** — `Core/Reporting/TraceLogExporter.cs` (`ToText`/`ToCsv`/`ToHtml`/`Write`) e `Core/Reporting/ReportFormats.cs` (`ToReg`/`WriteReg`), que usa o **tipo real** do valor (`GetValueKind`): o binário sai `hex:`, não `hex(2):` | **Ashampoo H.8** |
| 40 | ✅ **Três avisos de segurança antes de apagar** — "pastas abaixo podem conter dados do usuário e documentos", "remover este produto pode fazer recursos do Windows ou outros softwares pararem de funcionar" e "programas instalados na mesma pasta: as sobras de todos serão encontradas". Implementado em `Core/UninstallAdvisories.cs` (a regra), `Core/Localization/AdvisoryLabels.cs` (o texto), com painel na etapa da lista de sobras e aviso na CLI. **34 testes novos** | **Revo** §Fechamento das 🟡 — o terceiro é o mais grave: apagar as sobras de um programa pode levar junto o que o outro gravou |
| 41 | ✅ **Log de rastreio com tipo de mudança e nome/dado antigo** — `Core/Tracing/TraceEntry.cs` guarda o tipo (`Created`/`Deleted`/`Renamed`/`Overwritten`/`Unknown`), o caminho antigo e o novo e o dado anterior; o log v2 (`Core/Tracing/TraceLogV2.cs`) é o registro canônico — o `TracePayload` legado guardava só `Files`, `Folders` e `RegistryKeys` | **Revo** §Fechamento das 🟡 |
| 42 | ✅ **Usar o log correspondente durante a desinstalação** — `Core/Tracing/ForensicAnalyzer.cs` monta o plano de remoção a partir do log do programa (`BuildPlan`), com a procedência marcada, e a janela de rastreio (`Ui/Tracing/TracingWindow.xaml`) oferece o log correspondente do programa selecionado | **Revo** §Fechamento das 🟡 |
| 43 | ✅ **Continuar a desinstalação interrompida por reinício** — Continuar a desinstalação interrompida por reinício: `Core/Automation/UninstallSession.cs` grava o passo concluído a cada etapa, de forma atômica; a retomada não repete o que já deu certo.| **Revo** §Fechamento das 🟡 — encosta no item 5 do F.1 (motor de desinstalação próprio, com retomada) |
| 44 | ✅ **Unir logs de um mesmo programa num log só** — `Core/Tracing/TraceLogV2.cs` (`ToTracedInstall`) funde os logs do mesmo programa num registro só, preservando a ordem e a procedência de cada entrada | **Revo** §Fechamento das 🟡 |
| 45 | ✅ **Exportar para Microsoft Excel** — `Core/Reporting/ReportFormats.cs` (`ToExcel`) gera SpreadsheetML 2003: XML puro, que abre no Excel sem o Office instalado | **Revo** §Fechamento das 🟡 |
| 46 | ✅ **Imprimir a lista** — `Core/Reporting/PrintLayout.cs` monta a paginação (colunas, linhas, rodapé) e `Ui/Reporting/ReportingWindow.xaml` imprime, com a visualização antes | **Revo** §Fechamento das 🟡 · **Total Uninstall H.6** |
| 47 | ✅ **Alinhar coluna + "Best Fit" + remover coluna** — já existia: `MainWindow.xaml.cs:386-398` (menu do cabeçalho), `:440` (`BestFit`), `:460` (`SetAlignment`). **A medição inicial errou** ao olhar só `Core/ProgramColumns.cs` — o alinhamento é uma questão de **exibição**, e por isso mora na janela, não no modelo | **Revo** §Fechamento das 🟡 |
| 48 | ✅ **Incluir / excluir processos do rastreio** — `Core/Tracing/TraceProcessPolicy.cs` (`Include`/`Exclude`/`IncludePath`/`ExcludePath`/`ShouldObserve`) filtra por nome e por caminho, e serializa em JSON | **Revo** §Fechamento das 🟡 |
| 49 | ✅ **Backup completo do registro todo dia** — `Core/Recovery/DailyRegistryBackup.cs` roda o backup completo uma vez por dia (`RunIfDue`), com o marcador da última execução e a validade configurável; o **formato** do backup continua sendo o do `Core/FullRegistryBackup.cs` (o `reg.exe` do Windows) | **Revo** §Fechamento das 🟡 | |

### F.3 🟢 Ganho pequeno

| # | Item | De onde veio |
|---|---|---|
| 50 | ✅ Treemap — `Core/Appearance/TreemapModel.cs` monta o mapa de área a partir da lista de programas (tamanho estimado), com o retângulo de cada entrada | BCU D5 |
| 51 | ✅ Legenda de cores — `Core/Appearance/ColorLegend.cs` monta a legenda por confiança (`LeftoverConfidence`) e por tipo de desinstalador (`UninstallerKind`) | BCU D6 |
| 52 | ✅ Painel lateral de configurações (filtros e realces) — `Core/Appearance/ProgramFilters.cs` tem a **regra** (filtros e realces, com a precedência fixa) e `Ui/Appearance/SettingsSidebar.xaml` é o painel: o popup de filtros, a caixa de busca e o painel editam o **mesmo** conjunto de filtros, e o realce pinta o nome da linha (`HighlightPalette`) | BCU D18 |
| 53 | ✅ Agrupamento automático por coluna — `Core/Appearance/GroupingModel.cs` agrupa a lista pela coluna escolhida, com o rótulo e a contagem de cada grupo | BCU D19 |
| 54 | ✅ Renomear entrada / excluir entrada do registro / assumir propriedade — `Core/RegistryEditing/RegistryEditor.cs`, com a proteção em três partes: a regra da varredura, as raízes críticas dos hives e a área `Microsoft\Windows` | BCU D13/D14/D15 |
| 55 | ✅ Cópia avançada para a área de transferência — `Ui/Appearance/ProgramClipboard.cs` com quatro formatos: texto simples, separado por tabulação, comando de desinstalação e detalhes completos | BCU D9 |
| 56 | ✅ Exportar para script de desinstalação em lote — `Core/Reporting/BatchScriptWriter.cs` (`Build`/`Write`) gera o script; `Core/Reporting/ReportFormats.cs` (`ToBatchScript`) é a porta única | BCU D11 |
| 57 | ✅ Classificação do usuário (estrelas) — `Core/Appearance/ProgramRating.cs` (0 a 5 estrelas, com `Clamp` e `Stars`) e o campo `Rating` em `Core/ProgramCustomization.cs` | BCU D7 |
| 58 | ✅ **Mais skins no tema** (4 no Tranal: Dark, Slate, Contrast e Light) e **cor personalizada** por papel (fundo, seleção, texto, botões e diálogos) — o catálogo mora em `Core/Appearance/ThemePalette.cs` (fonte única para as opções e para o menu) e a cor personalizada é aplicada por `ThemeManager.ApplyCustomColors`; cor inválida é ignorada | **Revo** §Fechamento das 🟡 |
| 59 | ✅ Copiar para a área de transferência como comando geral — o comando "Copiar para a área de transferência" (`Ui/Appearance/ProgramClipboard.cs`) serve tanto à área de transferência quanto ao comando geral, nos quatro formatos | **Revo** §Fechamento das 🟡 |
| 60 | ✅ Desativar animações de texto — `Ui/Appearance/TextAnimation.cs` (animação por propriedade anexada `anim:TextAnimation.Animate`) e a preferência `DisableTextAnimations` | **Revo** §Fechamento das 🟡 |
| 61 | ✅ Tela de inicialização (splash) — `Ui/Appearance/SplashWindow.xaml`, ligado em `App.xaml.cs` com um mínimo de 900 ms, e a preferência `ShowSplashScreen` | **Revo** §Fechamento das 🟡 |
| 62 | ✅ Enviar comentário / Suporte online — `Core/Appearance/SupportRequest.cs` monta o pedido de suporte com o diagnóstico do ambiente, e o comando "Suporte online" | **Revo** §Fechamento das 🟡 |
| 63 | ✅ "Do you want to keep your preferences? Icons, comments, groups" — a regra mora em `Core/Appearance/PreferenceRetention.cs` (`ApplyAfterUninstall`, `ItemIds`); o assistente oferece a caixa "Manter as minhas preferências" **só quando há algo para manter**, e a CLI respeita a preferência `KeepPreferencesOnUninstall` | **Revo** §Fechamento das 🟡 |
| 64 | ✅ Abrir o assistente maximizado, focar a janela "Traced Programs" no arranque, mostrar caixas de seleção na visualização de detalhes — o assistente abre com `WindowState="Maximized"`; a preferência `FocusTracedProgramsOnStartup` foca a tela "Traced Programs" no arranque; e as caixas de seleção viraram **coluna própria** da visualização de detalhes (`ProgramColumnLayout.CheckColumnWidth`, com a preferência `ShowCheckBoxesInDetailsView`) | **Revo** §Fechamento das 🟡 |

### F.4 O que **não** fazer (decisão registrada)

| Item | Motivo |
|---|---|
| **Driver de kernel** (`revoflt.sys`) | o Revo intercepta o sistema de arquivos com `DeviceIoControl`. O Tranal usa as APIs do Windows. Menos risco, sem assinatura de driver. Decisão já registrada no `PARIDADE.md` §8 — **e** o BCU, que é open source, também não usa driver. A paridade se ganha por software, não por kernel. |
| **Gerar desinstalador simples para app órfão** | perigoso: o nome da pasta não prova a posse. O BCU faz; o Tranal decide não. O risco é real (apagar `Temp` inteiro). O dono pode reverter essa decisão se quiser paridade literal — mas é uma **decisão do dono**, não um esquecimento. |
| **Classificação de usuário com servidor** | exige infraestrutura e política de privacidade; o ganho é de conveniência, não de função. |
| **"Install Programs"** (instalar monitorando) | é o **outro lado** do produto: o Tranal é um desinstalador. Instalar com monitoramento em tempo real é um produto por si só, e traz a mesma complexidade de kernel que o driver foi para evitar. Decisão do dono — registrada aqui para não virar esquecimento. |
| **"Logs Database"** (base de logs na web) | serviço online **proprietário** da Revo: não pode ser copiado, e reimplementá-lo exigiria uma rede de logs comunitária que o Tranal não tem e não vai ter. O equivalente honesto já existe: o `Core/MsiInstallLog.cs` lê o **log do próprio instalador**, local e verificável. |
| **"Skip User Account Control (UAC) prompt"** | manter o caminho de UAC do Windows. É o que evita que o Avast e o McAfee marcarem o produto — o Uninstall Tool, que cria tarefa agendada elevada para contornar o UAC, paga esse preço. |

---

## H. O que cada concorrente acrescenta

A matriz das seções A–E compara Revo e BCU com o Tranal, porque são os dois
que definem o alvo (o Revo) e a referência de implementação (o BCU, open
source). Os demais concorrentes entram aqui, pelo que **acrescentam** — é
dessa coluna que saem as lacunas que só eles revelam.

### H.1 IObit Uninstaller 16.1.0.4 — [IOBIT.md](IOBIT.md)

| Recurso | IObit | Tranal | Prioridade |
|---|---|---|---|
| **Remoção de extensões de navegador** com base maliciosa/ad | ✅ | ⬜ | 🟡 alta |
| **Bloatware Remover** (pré-instalados, Accessories, Windows Features) | ✅ | ⬜ | 🟡 |
| **Bundleware**: detectar o que veio junto | ✅ | ⬜ | 🟡 |
| **Desinstalar pelo log de instalação** | ✅ (PRO) | ⬜ | 🟡 |
| **Stubborn Program Remover** (base de 4.000+ programas) | ✅ (PRO) | ⬜ | 🟢 |
| **Force Uninstall+** (sem entrada de registro) | ✅ | 🟡 | 🟡 |
| **Software Health** (painel único de 9 categorias) | ✅ | 🟡 | 🟢 |
| **Desinstalar Windows Updates** | ✅ | ⬜ | 🟡 |
| **Desinstalação em lote** (multi-seleção) | ✅ | ⬜ | 🟡 |
| **Software Relocator** (mover entre discos) | ✅ | ⬜ | 🟢 |
| **File Shredder** com 3 modos de sobrescrita | ✅ | ✅ | — |
| Aviso de que o desinstalador "ainda vai deixar" resíduo | ✅ | ⬜ | 🟢 |
| Aviso antes de apagar arquivo pessoal | ✅ | ⬜ | 🟡 |
| **Drivers de kernel** (4 × 7 arquiteturas) | ✅ | ⬜ | — (decisão: não) |
| **Linha de comando de desinstalação** | ⬜ **nenhum switch** | ✅ | — 🟢 Tranal melhor |

**O achado que muda a prioridade de A4 e A5:** Windows Features e Windows
Updates não são exclusividade do BCU — o IObit também os cobre, e o IObit é
usado por muito mais gente. Os dois sobem de 🟡 para 🟡 **alta**.

**O achado que reforça a decisão sobre driver:** o IObit precisa de 4
drivers × 7 arquiteturas (28 binários) para remover arquivo em uso. O Tranal
resolve com encerramento de processo e adiamento para o reinício. O custo de
um driver agora está medido, não suposto.

### H.2 HiBit Uninstaller 4.0.10 — [HIBIT.md](HIBIT.md)

| Recurso | HiBit | Tranal | Prioridade |
|---|---|---|---|
| **Tabela de switches silenciosos por instalador** (`/VERYSILENT`, `/S`, `/quiet /qn`, `/silent`) | ✅ | ⬜ | 🔴 **alta** — a classificação já existe, então isso é uma tabela, não heurística |
| **Switch de CLI por ferramenta** (19 ferramentas, uma por switch) | ✅ | 🟡 | 🟡 |
| **Ocultar entrada de desinstalação** | ✅ | ⬜ | 🟢 |
| **Deletar entrada** do registro do programa | ✅ | ⬜ | 🟢 |
| **Editar informações** do programa | ✅ | ⬜ | 🟢 |
| **Gerenciador do Menu de Contexto** (gerenciar menus alheios) | ✅ | ⬜ | 🟢 |
| **Desabilitar handlers** de menu de contexto | ✅ | ⬜ | 🟢 |
| **Desinstalar atualizações do Windows** (`wusa`/KB) | ✅ | ⬜ | 🟡 |
| **Gerenciador de Tarefas Agendadas** (criar/remover) | ✅ | ⬜ | 🟡 |
| **Restaurador do Sistema** (criar ponto com um clique) | ✅ | 🟡 | 🟡 |
| **Otimizador de registro** com 15+ categorias | ✅ | 🟡 | 🟢 |
| **Relatórios HTML por ferramenta** | ✅ | ⟡ | 🟢 |
| **Agendamento automático de limpeza** | ✅ | ⬜ | 🟢 |
| **Backup próprio** com restauração (`.dbh`) | ✅ | 🟡 | 🟢 |
| **Sufixo ` RUN`** na forma longa do switch | ✅ | ⬜ | 🟢 |
| **Driver de kernel** | ⬜ | ⬜ | — (decisão: não) |

**O achado que mais importa:** o HiBit traz uma **tabela de switches
silenciosos por aplicativo** — o nome do programa vem seguido, na tabela de
strings, do switch que aquele programa aceita (`IrfanView` → `/silent`,
`Google Chrome` → `/silent /norestart`, `Brave` → `--force-uninstall`,
`XAMPP` → `--mode unattended`, `WinRAR` → `/AUTO /SILENT`,
`Adobe Creative Cloud` → `-uninstall`). Combinada com a classificação de
desinstalador **já implementada** no Tranal, fecha o item F.1-4 do backlog
sem adivinhação: a estratégia passa a ser "escolher o switch pelo tipo (e
pelo programa)".

**O achado que reforça a decisão sobre driver:** o HiBit tem 21+ ferramentas
e roda **sem driver, sem serviço de fundo e sem extensão COM** — tudo no
próprio processo. É o argumento mais forte que temos de que a paridade com
o Revo se ganha por software.

**Correção de domínio registrada:** `hibituninstaller.com` **não** é o site
do fabricante — é um site de SEO que devolve HTML para qualquer URL. O
domínio correto é **`hibitsoft.ir`**.

### H.3 Uninstalr 3.2 — [UNINSTALR.md](UNINSTALR.md)

| Recurso | Uninstalr | Tranal | Prioridade |
|---|---|---|---|
| **Motor de desinstalação próprio** (não só rodar o uninstaller do app) | ✅ | 🟡 | 🔴 **alta** |
| **16 tipos de app** (Steam, GOG, Battle.net, Epic, Riot, itch.io, EA, Big Fish, Chocolatey, Ninite, PortableApps.com, portáteis) | ✅ | ⬜ | 🟡 |
| **Retomar a desinstalação após reinício** (fluxo `RunOnce`) | ✅ | ⬜ | 🟡 |
| **Detecção automática de caminhos a ignorar** (lixeira, `System Volume Information`, pastas de backup do sistema) | ✅ | ⬜ | 🟡 |
| **Detecção de sobras por MuiCache / TypeLib / AppX ModelID** | ✅ | ⬜ | 🟡 |
| **Desinstalação em lote** mesmo sem suporte a silent | ✅ | ⬜ | 🟡 |
| **Tamanho em disco em tempo real** (não o valor do registro) | ✅ | 🟡 | 🟢 |
| **Contagem final** ("N arquivos / N pastas / N chaves / N valores") | ✅ | ⬜ | 🟡 |
| **Settings em duas colunas** (padrões para 1 app × muitos apps) | ✅ | ⬜ | 🟢 |
| **Gerar `.bat`** para desinstalação em lote | ✅ | ⬜ | 🟢 |
| **País de origem** do app (bandeira) | ✅ | ⬜ | 🟢 |
| **Pré-visualização** com exclusão item a item | ✅ | 🟡 | — |
| **Backup do registro** antes de remover | ⬜ **não tem** | ✅ | — 🟢 Tranal melhor |
| **CLI de desinstalação** | ⬜ **nenhuma** | ✅ | — 🟢 Tranal melhor |

**O dado que decide a estratégia de produto:** a versão **gratuita do Uninstalr
tem exatamente os mesmos recursos da Pro** — a Pro só vende suporte técnico.
É a prova de que um desinstalador completo **não precisa** de gate de versão
paga. Isso reforça a estratégia do Tranal: nenhum recurso essencial atrás de
pagamento.

**A refutação que importa:** a literatura afirma que o Uninstalr tem
`/S`, `/silent`, `/log`, `/minimize`, `/autoclose` e `/wait`. **Medido: não
existe.** As flags `/quiet /silent /passive /verysilent` são passadas *para*
desinstaladores de terceiros, e `/S` é do instalador NSIS. Quem construir
uma CLI a partir dessa literatura constrói a errada.

**A correção de domínio:** `instalr.com` é um produto homônimo de gestão de
tarefas. O site do Uninstalr é **`uninstalr.com`**.

### H.4 Geek Uninstaller 1.6.0.200 (Free) — [GEEK.md](GEEK.md)

| Recurso | Geek | Tranal | Prioridade |
|---|---|---|---|
| **Varredura de sobras de MSI** (`ScanMSITraces`, `Installer\UserData\…\Products`, `Installer\Folders`) | ✅ | ⬜ | 🔴 **alta** — 149 dos 211 programas desta máquina são MSI |
| **Diálogo de órfãos** ("Alguns itens órfãos encontrados!") | ✅ | ⬜ | 🟡 |
| **Frase de fechamento do ciclo** ("Todos os traços removidos") | ✅ | ⬜ | 🟡 |
| **Confirmação ao cancelar** a remoção de sobras | ✅ | ⟡ | 🟢 |
| **Batch Force Removal** / **Batch Remove Entries** | ✅ | ⬜ | 🟡 |
| **Modificar/reparar** a instalação | ✅ | ⬜ | 🟢 |
| **Reset Columns** | ✅ | ⬜ | 🟢 |
| **Abrir na Microsoft Store** | ✅ | ⬜ | 🟢 |
| **Browse Source…** (ir à origem do instalador) | ✅ | ⬜ | 🟢 |
| Diálogo de falha com opção de **limpar entrada e arquivos** | ✅ | 🟡 | 🟢 |
| **Adiar remoção para o próximo reinício** | ✅ | ✅ | — |
| **Backup do registro** antes de desinstalar | ❌ | ✅ | — 🟢 Tranal melhor |
| **Ponto de restauração** antes de desinstalar | ❌ | ✅ | — 🟢 Tranal melhor |
| **Lixeira** para os arquivos | ❌ | ✅ | — 🟢 Tranal melhor |
| **CLI** | 1 switch (`/store_apps`) | ✅ | — 🟢 Tranal melhor |

**O dado mais revelador de todas as auditorias:** a superfície observável
inteira do Geek **gratuito** cabe em **60 strings** — contra 1.136 do Revo
Pro e 1.066 do BCU. O desinstalador em si é pequeno; o que o Revo vende
caro é o **painel de manutenção**, que o Tranal já tem.

**A correção que a medição fez:** acredita-se que o Geek aceite arrastar um
`.exe` sobre a janela. **Medido: não aceita** — nenhuma das 22 janelas e
controles tem `WS_EX_ACCEPTFILES`, o `WM_DROPFILES` não produziu resposta, e
passar o caminho como argumento também não fez nada.

**O achado que dá o próximo passo do núcleo:** o Geek varre as sobras de
**MSI** (`CScannerMSI::ScanMSITraces`, `Installer\UserData\…\Products`,
`Installer\Folders`). São **149 dos 211 programas** desta máquina. A
varredura por nome não acha o que o instalador pôs fora da pasta do
programa — e é exatamente o item F.1-2 do backlog.

### H.5 Wise Program Uninstaller 3.2.7.271 — [WISE.md](WISE.md)

| Recurso | Wise | Tranal | Prioridade |
|---|---|---|---|
| **Histórico de desinstalações com botão "Recuperar"** (coluna "Resíduos") | ✅ | ⬜ | 🔴 **alta** — é a combinação "desinstalar + excluir sobras + voltar atrás" |
| **Backup automático dos resíduos** antes de apagá-los, com expiração (1 mês) | ✅ | 🟡 | 🔴 **alta** — o Tranal faz backup do registro, não dos resíduos |
| **Desinstalação Forçada** como modo explícito | ✅ | 🟡 | 🔴 **alta** |
| **Desinstalação Personalizada** por palavra-chave, com "Explorar" | ✅ | ⬜ | 🟡 |
| **Software Health** em 4 categorias (sobras, redundantes, permissões, notificações) | ✅ | ⬜ | 🔴 **alta** — o **mesmo** painel do IObit: agora são dois concorrentes, deixa de ser nicho |
| Detecção de **permissões** de app: serviço, inicialização, menu de contexto, tarefa agendada | ✅ | ⬜ | 🟡 |
| Bloqueio de **notificações pop-up** (navegador e software) | ✅ | ⬜ | 🟡 |
| Base de regras de limpeza **por aplicativo** (**595 perfis**, 2.379 caminhos) | ✅ | ⬜ | 🟡 |
| Gerenciador de extensões de navegador — **11** navegadores | ✅ | 🟡 | 🟡 — faltam Yandex, Vivaldi, Brave, Comodo Dragon, Cent Browser, SoGou |
| Coluna "**Usado pela Última Vez**" | ✅ | ⬜ | 🟢 |
| **Tamanho total** dos programas na lista | ✅ | 🟡 | 🟢 |
| Datas com **tempo relativo** ("%d semana(s) atrás") | ✅ | ⬜ | 🟢 |
| **46 idiomas**, com pt-BR completo (218 chaves) | ✅ | ✅ | — |
| Skins Light/Dark, WebView2, SVG | ✅ | 🟡 | 🟢 |
| Menu de contexto do Explorer | ✅ | ✅ | — |
| Exportar a lista | ✅ | ✅ | — |
| Ponto de restauração antes de desinstalar | ✅ | ✅ | — |
| Varredura de sobras **depois** da desinstalação | ✅ | ✅ | — |
| **Atribuição pela cadeia** (lê o `.msi` em cache) | ❌ | ✅ | — 🟢 **Tranal melhor** |
| **Justificativa por item** (o *porquê* de cada sobra) | ❌ | ✅ | — 🟢 **Tranal melhor** |
| **Silencioso com switch medido por família** | ❌ (só avisa que não dá) | ✅ | — 🟢 **Tranal melhor** |
| **CLI** (`/scan`, `/mu`, `/export`) | ❌ | ✅ | — 🟢 **Tranal melhor** |
| Driver de kernel / serviço de fundo | ❌ | ❌ | — |

**O achado que confirma a decisão de produto:** o "Software Health" com as
**mesmas 4 categorias** apareceu no IObit e agora no Wise — dois concorrentes
independentes, mesma ideia. Deixa de ser curiosidade e vira **prioridade**:
o Tranal tem as ferramentas separadas (`AutorunManager`, `ExplorerContextMenu`,
`JunkCleaner`), e falta o **painel único** que as reúne.

**O achado que dá o próximo passo do núcleo:** o histórico do Wise com
**"Recuperar"** é a forma mais direta de atender o que o owner disse querer —
"desinstalar, excluir as sobras". O Tranal já tem as duas metades
(`TraceStore` registra a instalação; `BackupManager` faz o backup do
registro) e não tem o **histórico com restauração** que junta as duas.

**A correção de domínio:** `wiseuninstaller.com` responde **403** a esta
máquina (com e sem `www`, e com User-Agent de navegador) — é a mesma
armadilha de SEO do `hibituninstaller.com` e do `totaluninstall.com`. O
fabricante é a **Lespeed Technology Co., Ltd** (Beijing), o site é
`wisecleaner.com` e o download sai de `downloads.wisecleaner.com`, confirmado
pelo manifesto do winget com o SHA-256 conferido.

---

### H.6 Total Uninstall 7.6.2 Professional Edition — [TOTALUNINSTALL.md](TOTALUNINSTALL.md)

| Recurso | Total Uninstall | Tranal | Prioridade |
|---|---|---|---|
| **Motor de regras de resíduo em XML** (623 elementos, 6 categorias, `Include`/`Exclude`) | ✅ | ⬜ | 🟡 alta |
| **Desinstalação dirigida pelo log de instalação** (sem depender do desinstalador nativo) | ✅ | 🟡 | 🔴 alta |
| **Snapshot pré/pós-instalação com comparação A×B** e perfis de comparação | ✅ | 🟡 | 🔴 alta |
| **Backup do programa com restauração completa e undo/redo** | ✅ | 🟡 | 🔴 alta |
| **Desinstalação em lote** | ✅ | ⬜ | 🔴 alta |
| Exportação/importação portátil de programas monitorados (`.zip`) | ✅ | ⬜ | 🟡 |
| **Operações pendentes de renomeação** visíveis sem reiniciar | ✅ | 🟡 | 🟡 |
| "Don't use detected changes log" + exclusão de chaves/valores | ✅ | 🟡 | 🟢 |
| CLI de snapshot e restauração (`/snapshot`, `/import`, `/restore`) | ✅ (edição Ultimate) | ⬜ | 🟡 |
| Perfis por operação (scanning / analysis / uninstall / comparing / restore-redo) | ✅ | ⬜ | 🟢 |
| Varredura de Alternate Data Streams (ADS) | ✅ | ⟡ | 🟢 |
| Contadores de referência de itens compartilhados + "Remove shared file" | ✅ | ⟡ | 🟢 |
| Limite de tamanho da pasta de backup | ✅ | 🟡 | 🟢 |
| Transferir programas monitorados para outro PC | ✅ | ⟡ | 🟢 |
| Imprimir as alterações detectadas | ✅ | ⟡ | 🟢 |
| Exportar as alterações em XML | ✅ | ✅ | — |
| Varredura de sobras depois do desinstalador nativo | ✅ | ✅ | — |
| Ponto de restauração antes de desinstalar | ✅ | ✅ | — |
| Windows Apps (Store/MSIX) como módulo próprio | ✅ | ✅ | — |
| **Atribuição pela cadeia do `.msi`** | ❌ (só importa `MsiGetProductInfoW` — **consulta**, não atribuição) | ✅ | — 🟢 **Tranal melhor** |
| **Justificativa por item** (o *porquê* de cada sobra) | ❌ | ✅ | — 🟢 **Tranal melhor** |
| **CLI roteirizável** | ❌ (só na edição Ultimate) | ✅ | — 🟢 **Tranal melhor** |
| **Backup do registro com script de restauração** (`.reg` por hive + `Restaurar.bat`) | ❌ | ✅ | — 🟢 **Tranal melhor** |
| **Privacidade** (sem cadastro, sem telemetria, atualização opcional desligada) | ❌ (validação online de licença) | ✅ | — 🟢 **Tranal melhor** |
| **Assinatura do instalador com CA pública** | ✅ (Certum Code Signing 2021) | 🟡 (autossinado) | 🟡 — decisão do dono |
| Driver de kernel | ❌ (32 arquivos, nenhum `.sys`) | ❌ | — |

**O achado que muda a arquitetura:** o Total Uninstall é o primeiro concorrente
com **motor de regras em arquivo** (623 elementos, 6 categorias) — e o Tranal
**já tem o julgamento duplicado**: `Core/UninstallSupport.cs:389`
(`LeftoverScanner`) e `Core/Tools/JunkCleaner.cs` decidem cada um por conta
própria. O item 25 do backlog **não** é "copiar o XML do Total Uninstall": é
**unificar os dois julgamentos** num motor só, com o formato do Total
Uninstall e as regras por app do Wise.

**O achado que dá o próximo passo do núcleo:** o `SystemSnapshot.Diff` do
Tranal existe e é real (`Core/Modules/SystemSnapshot.cs:180`), mas
`SnapshotDiff` só expõe `NewFiles`/`NewFolders`/`NewRegistryKeys` — a
comparação **não cobre modificados nem removidos**, e o snapshot **não é
guardado** (a fotografia só vive durante a operação,
`Core/Modules/TracedPrograms.cs:43`). O Total Uninstall guarda os dois e
compara sob demanda. É o item 29 do backlog, e é trabalho de **núcleo**.

---

### H.7 Uninstall Tool 6.6.0 — [UNINSTALLTOOL.md](UNINSTALLTOOL.md)

| Recurso | Uninstall Tool | Tranal | Prioridade |
|---|---|---|---|
| **Reverter a instalação rastreada** | ✅ | ⬜ | 🔴 alta |
| **Desinstalação em lote** (Batch Force Removal / Batch Remove Entries) | ✅ | ⬜ | 🔴 alta |
| **Log de remoção por item, com código de erro** | ✅ | ✅ `Core/RemovalLog.cs` | 🟡 alta |
| **Exportar a varredura de sobras** em XML | ✅ | ⬜ | 🟡 |
| Rastreamento de **renomeação** de arquivos/pastas | ✅ | ⬜ | 🟡 |
| Rastreamento de **processos criados** | ✅ | ⬜ | 🟡 |
| **Serviço de sistema** versionado e instalável à parte | ✅ | ⬜ | 🟡 |
| Extensão de shell **COM/MSIX** | ✅ | 🟡 | 🟡 |
| Menu de contexto "Install and Trace" | ✅ | ⟡ | 🟡 |
| Detecção de **itens relacionados/dependentes** | ✅ | ⬜ | 🟢 |
| Startup Manager com filtro por categoria e itens UWP | ✅ | 🟡 | 🟢 |
| Idiomas (45 XML) | ✅ | 🟡 | 🟢 |
| Remoção forçada sem rodar o desinstalador | ✅ | ✅ | — |
| Detecção e terminação de processos | ✅ | ✅ | — |
| **Snapshot do sistema antes/depois** | ❌ | ✅ | — 🟢 **Tranal melhor** |
| **Filtro por data/tamanho** | ❌ (só ordena) | ✅ | — 🟢 **Tranal melhor** |
| **Backup e restauração** | ❌ | ✅ | — 🟢 **Tranal melhor** |
| **Limpador de lixo** | ❌ | ✅ | — 🟢 **Tranal melhor** |
| **Otimizador de registro** | ❌ | ✅ | — 🟢 **Tranal melhor** |
| **CLI pública de automação** (5 switches, nenhum de automação) | ❌ | ✅ | — 🟢 **Tranal melhor** |
| **Ponto de restauração** antes de desinstalar | ❌ | ✅ | — 🟢 **Tranal melhor** |
| **Rastreamento por driver de kernel** (minifiltro `CisUtMonitor.sys`) | ✅ | ⬜ | — ❌ **decisão: fora do plano** |
| **Tarefa elevada que contorna o UAC** | ✅ | ❌ | — ❌ **decisão: manter o UAC do Windows** |

**O achado que fecha a decisão do driver:** a contraprova do minifiltro ficou
completa. **Dois** concorrentes usam driver (IObit com 4 drivers × 7
arquiteturas; Uninstall Tool com **um** minifiltro, `CisUtMonitor.sys`) e
**cinco** não usam (BCU, HiBit, Wise, Uninstalr e o próprio Total Uninstall —
32 arquivos, nenhum `.sys`); mais o Soft Organizer (H.9), que também não usa.
O Ashampoo é **não verificado**. O Uninstall Tool mostra que o **minifiltro** é
a via *barata* do driver: um `.sys` só, instalado pelo setup e removível por
script, com o app falando por `DeviceIoControl`. **Continua fora do plano do
Tranal** — o ganho (rastrear evento a evento) não paga o custo (instalação de
driver, assinatura, AVC e um segundo modo de falhar) num produto que já
entrega 87,7% de cobertura de silencioso sem nada disso.

**O achado que dá o próximo passo do núcleo:** o Uninstall Tool tem
**desinstalação em lote** e **log de remoção por item com código de erro**.
São as duas lacunas mais baratas desta matriz: a interface do Tranal já
opera sobre listas (`Core/Modules/TracedPrograms.cs`,
`Core/UninstallSupport.cs`) e o que falta é o **escritor** do log de remoção
— o `Core/MsiInstallLog.cs` é leitor, e a operação ainda não deixa rastro
por item.

**A lição de documentação:** o Uninstall Tool publica `UninstallTool.exe
/install` e esse executável **não existe** no pacote 3.8.2 — automação que
segue a documentação ao pé da letra falha. Qualquer comando nosso no manual
precisa ser **testado contra o binário real**, não escrito de memória.

---

### H.8 Ashampoo UnInstaller 27.0.1 — [ASHAMPOO.md](ASHAMPOO.md)

| Recurso | Ashampoo | Tranal | Prioridade |
|---|---|---|---|
| **Análise forense**: log de desinstalação para programa **já instalado** | ✅ | ⬜ | 🔴 alta |
| **Biblioteca de snapshots nomeados** (criar, listar, apagar, comparar) | ✅ | 🟡 | 🔴 alta |
| **Gerenciador próprio de serviços** (tipo de inicialização, iniciar/parar) | ✅ | 🟡 | 🟡 alta |
| **Gerenciador próprio de tarefas agendadas** (habilitar/desabilitar/excluir) | ✅ | 🟡 | 🟡 alta |
| **Importar lista exportada** (marcando o que não está instalado) | ✅ | ⬜ | 🟡 |
| **Editar manualmente o log de instalação** (entrada a entrada) | ✅ | ⬜ | 🟡 |
| **Exportar logs de desinstalação em HTML/CSV/TXT** | ✅ | 🟡 | 🟢 |
| **Exportar alterações de registro do log para `.reg`** | ✅ | ⬜ | 🟢 |
| **Software Updater** de programas de terceiros | ✅ | ⬜ | 🟢 |
| **Crash Analyzer** (Event Log) | ✅ | ⬜ | 🟢 |
| **Unlocker** (desbloquear arquivo em uso) | ✅ | ⬜ | 🟢 |
| **Undeleter** (recuperar arquivo apagado) | ✅ | ⬜ | 🟢 |
| **File Associator** | ✅ | ⬜ | 🟢 |
| **Hibernation** (dormir programa) | ✅ | ⬜ | 🟢 |
| **Impact** (ranking de performance) | ✅ | ⬜ | 🟢 |
| **Ratings por estrelas** com base online | ✅ | ⬜ | 🟢 |
| **Move / Revert relocation** | ✅ | ⬜ | 🟢 |
| **Agendamento de desinstalação** | ✅ | ⬜ | 🟢 |
| **"Drag & Drop UnInstall"** (atalho de área de trabalho) | ✅ | 🟡 | 🟢 |
| Log de instalação a partir do diff entre dois snapshots | ✅ | ✅ | — |
| Remover entrada da lista sem desinstalar | ✅ | 🟡 | 🟢 |
| Filtro por tamanho na lista | ✅ | ⟡ | 🟢 |
| **Atribuição pela cadeia de desinstalação** (lê o `.msi` em cache) | ❌ | ✅ | — 🟢 **Tranal melhor** |
| **Justificativa por item** | ❌ | ✅ | — 🟢 **Tranal melhor** |
| **Silencioso com switch medido por família** | ❌ (só avisa que não dá) | ✅ | — 🟢 **Tranal melhor** |
| **CLI roteirizável** | ❌ | ✅ | — 🟢 **Tranal melhor** |
| **Privacidade** (sem base online de ratings, sem telemetria) | ❌ | ✅ | — 🟢 **Tranal melhor** |
| Driver de kernel, serviço do Windows, extensão COM de shell | ⟡ **não verificado** | ❌ | — |

**O achado que é uma limitação nossa, não do concorrente:** o Ashampoo faz
**análise forense** — cria o log de desinstalação de um programa **já
instalado**. O Tranal só registra o que o instalador gravou **enquanto o
instalador rodava** (`Core/Modules/InstallMonitor.cs`,
`Core/Modules/TracedPrograms.cs:44-56`): para um programa já instalado não
existe fotografia do "antes". É o item 35 do backlog, e o documento exige que
a saída seja marcada como **inferida**, nunca como medida.

**O achado que é uma decisão de produto, não de código:** o Ashampoo traz um
conjunto grande de ferramentas que **não** são desinstalação (Unlocker,
Undeleter, Crash Analyzer, ratings online, File Associator, Impact). Várias
dependem de rede e de base de dados online. O Tranal é deliberadamente
offline, sem cadastro e sem telemetria — a decisão de **não** buscar paridade
literal nesses itens é do dono, e vale **registrar** em vez de silenciar.

---

### H.9 Soft Organizer 10.50 — [SOFTORGANIZER.md](SOFTORGANIZER.md)

| Recurso | Soft Organizer | Tranal | Prioridade |
|---|---|---|---|
| **Tracing de instalação em tempo real** (registro **e** disco) | ✅ | 🟡 — **polling** de snapshot de 30 s | 🔴 **alta** — muda o método (F.1 #7) |
| **Rastreio de arquivos em tempo real** durante a instalação | ✅ | ⬜ | 🔴 **alta** — mesmo item do F.1 #7 |
| **Undo Changes Center** (reverter mudança a mudança, histórico cronológico, renomear entrada) | ✅ | ⬜ | 🔴 **alta** — mesma peça do "Recuperar" (Wise) e do "reverter" (Uninstall Tool) |
| **Desinstalação em lote / multi-seleção** | ✅ | 🟡 — o caminho de código existe, mas a lista principal é `SelectionMode="Single"` | 🔴 **alta** |
| **Detecção de desinstalação externa** (o programa sumiu por fora → oferecer limpar as sobras) | ✅ | ⬜ | 🟡 alta |
| **Ignore List global e retroativa** | ✅ | 🟡 — global para o motor, **não** retroativa sobre os logs já gravados | 🟡 |
| **Log de instalação salvo por app** (`TracedApp.changes`) | ✅ | 🟡 — JSON por trace, um arquivo por rastreio | 🟡 |
| **Varredura profunda para apps não rastreados**, marcada como tal | ✅ | 🟡 — varre sempre, mas não distingue a origem da sobra | 🟡 |
| **"Large Applications"** + ordenação por tamanho decrescente | ✅ | 🟡 — o filtro por tamanho existe, o rótulo e a ordenação não | 🟢 |
| **Agendamento de varreduras** | ✅ | 🟡 — só agenda **remoção** para o próximo reinício | 🟢 |
| **Atualização de programas instalados** (painel de updates) | ✅ | ⬜ | ❌ fora de escopo — recurso de rede |
| **Rating de programa** | ✅ | ⬜ | ❌ fora de escopo — recurso de rede |
| **Removal tools do fabricante** | ✅ | ⬜ | ❌ fora de escopo — recurso de rede |
| **Instalar programas** (com rastreamento ou silencioso) | ✅ | ⬜ | ❌ fora de escopo — é o outro lado do produto |
| **Classificação do desinstalador** por família | ❌ | ✅ | — 🟢 **Tranal melhor** |
| **Desinstalação silenciosa** por família | ❌ (só "instalar em modo silencioso") | ✅ | — 🟢 **Tranal melhor** |
| **Nível de confiança + motivo** por item de sobra | ❌ | ✅ | — 🟢 **Tranal melhor** |
| **Atribuição pela cadeia do `.msi`** | ❌ (só chama `msiexec`) | ✅ | — 🟢 **Tranal melhor** |
| **Ponto de restauração** antes de desinstalar | ❌ | ✅ | — 🟢 **Tranal melhor** |
| **Backup do registro** antes de apagar | ❌ (só das sobras) | ✅ | — 🟢 **Tranal melhor** |
| **Filtros de tipo e de unidade** | ❌ | ✅ | — 🟢 **Tranal melhor** |
| **Exportação** (CSV, XML, TXT **e** HTML) | ❌ (só HTML) | ✅ | — 🟢 **Tranal melhor** |
| **Tarefas agendadas** editadas via COM | ❌ | ✅ | — 🟢 **Tranal melhor** |
| **CLI roteirizável** | ❌ (sem CLI documentada) | ✅ | — 🟢 **Tranal melhor** |
| Driver de kernel | ❌ (nenhum `.sys`) | ❌ | — |

**O achado que muda o método, não o recurso:** o Soft Organizer é
**superior** ao Tranal no tracing de instalação — grava **log em arquivo** do
que o instalador mudou (registro **e** disco), com rastreio de disco em
tempo real. O Tranal faz **polling de snapshot de 30 s**
(`Core/Modules/InstallMonitor.cs:31`, `DefaultIntervalSeconds = 30`), que
pode perder escrita rápida entre duas capturas. E o SO **não usa driver**:
nenhum `.sys` instalado, a única ocorrência de `.sys` nas strings é
`System.SysUtils`. Isso prova que **tracing de usuário em tempo real é
viável sem driver** — é exatamente o caminho do item 7 do F.1, e mais um
concorrente de acordo com a decisão de driver do `MATRIZ.md` F.4.

**O achado que fecha o ciclo:** o Undo Changes Center é a **mesma peça** que
o Wise chama de "Recuperar" e o Uninstall Tool de "reverter a instalação
rastreada". Três concorrentes, uma peça — e o Tranal já tem as duas metades
(`Core/Modules/TraceStore.cs:68` registra o que mudou;
`Core/BackupManager.cs` faz o backup) e não tem o "desfazer". O grep de
`Undo` no `src/` devolve **só** `FofAllowUndo` (`Core/RecycleBin.cs:24`),
que é flag do shell.

**O achado que é uma mudança de método na interface:** o Tranal **tem** o
caminho de código que pergunta *"Deseja desinstalar N programas?"*
(`MainWindow.xaml.cs:1215-1222`), mas a lista principal é
`SelectionMode="Single"` (`MainWindow.xaml:309`) — na prática, nunca se chega
ao N. A multi-seleção passa a ser requisito, e o alvo da desinstalação passa
a ser definido pelas **caixas de marcação** (hoje elas são "apenas indicador
visual", por decisão registrada no próprio código).

**A correção de atribuição:** `codeapps.com` é **domínio à venda**
(Namegarage) e `softorganizer.com` é site homônimo morto. O editor real é a
**ChemTable Software** (assinatura `CN=Software Solutions 365 LLC`), a mesma
casa do **Reg Organizer**. Quem destravou foi o `winget search`
(`ChemTableSoftware.SoftOrganizer`), que entregou URL + SHA do manifesto.

---

## G. Estado da auditoria

| Software | Documento | Estado |
|---|---|---|
| Bulk Crap Uninstaller 6.3.0 | [BCU.md](BCU.md) | ✅ completo — 80 funcionalidades medidas, 5 linhas de comando, 6 categorias de sobras |
| IObit Uninstaller 16.1.0.4 | [IOBIT.md](IOBIT.md) | ✅ completo — 142 funcionalidades medidas, 95 telas, 29 DLLs importadas |
| HiBit Uninstaller 4.0.10 | [HIBIT.md](HIBIT.md) | ✅ completo — 58 funcionalidades, 21+ ferramentas, 802 legendas pt-BR, 33 DLLs importadas |
| Uninstalr 3.2 | [UNINSTALR.md](UNINSTALR.md) | ✅ completo — 49 funcionalidades medidas, deep search de 25 min medido, 162 apps / 846 arquivos / 1.562 pastas / 394 chaves |
| Geek Uninstaller 1.6.0.200 | [GEEK.md](GEEK.md) | ✅ completo — **60 strings** (superfície inteira), menus e assistente capturados |
| Wise Program Uninstaller 3.2.7.271 | [WISE.md](WISE.md) | ✅ completo — 72 funcionalidades, 292 chaves de catálogo (218 em pt-BR), 15 formulários, **595 perfis** de limpeza por aplicativo |
| Total Uninstall 7.6.2 Professional | [TOTALUNINSTALL.md](TOTALUNINSTALL.md) | ✅ completo — **motor de regras em XML** (623 elementos, 6 categorias), 32 arquivos, 20 DLLs importadas, snapshot pré/pós com comparação A×B |
| Ashampoo UnInstaller 27.0.1 | [ASHAMPOO.md](ASHAMPOO.md) | ✅ completo — 66 funcionalidades, delta FREE x PRO, **análise forense** pós-instalação, gerenciador próprio de serviços e tarefas |
| Uninstall Tool 6.6.0 | [UNINSTALLTOOL.md](UNINSTALLTOOL.md) | ✅ completo — 67 funcionalidades, **minifiltro de kernel** (`CisUtMonitor.sys`), desinstalação em lote, log de remoção por item |
| Soft Organizer 10.50 | [SOFTORGANIZER.md](SOFTORGANIZER.md) | ✅ completo — 53 funcionalidades (25 observadas, 28 documentadas), **Undo Changes Center**, tracing em tempo real **sem driver**, editor ChemTable Software |

Cada auditoria concluída **atualiza esta matriz** e pode **mudar o backlog** —
um recurso que só o BCU tem hoje pode ser comum a todos, e aí vira
prioridade máxima; um recurso que só o BCU tem pode ser nicho e cair para 🟢.
