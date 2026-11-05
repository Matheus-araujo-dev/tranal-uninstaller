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
| 2 | **Atribuir sobras pela cadeia de desinstalação**, não só pelo nome — **começando pela varredura de MSI** (`Installer\UserData\…\Products`, `Installer\Folders`, componentes do .msi), que o Geek já faz (`CScannerMSI::ScanMSITraces`) e que cobre **149 dos 211 programas** desta máquina | BCU C5 · **Geek H.4** | Separa "a pasta do programa" de "a pasta **dele**". Errar aqui é apagar coisa de outro. |
| 3 | ✅ **Justificativa textual por item de sobra** — enum `LeftoverReason` (11 motivos), coluna "Porquê" na lista de sobras e na saída da CLI; a tradução mora em `Core/Localization/LeftoverLabels.cs`, consumida pela tela e pela CLI | BCU C4 | O Tranal já tem 3 níveis; agora diz **por quê**. A regra de marcagem não mudou: só confiança alta entra na remoção. |
| 4 | **Desinstalação silenciosa** — começar pela **tabela de switches** (por família de instalador: `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART` para Inno, `/S` para NSIS, `/quiet /qn /norestart` para MSI, `/s /v"/qn"` para InstallShield; e por aplicativo, como a tabela do HiBit), com a classificação já implementada como chave; depois, o gerador por automação de interface | BCU B2/B3 · **HiBit H.2** | Recurso que o dono sente na prática: sai do PC sem clicar 40 vezes. O HiBit mostra que a tabela existe e é curta — com a classificação pronta, isso deixa de ser heurística. |
| 5 | **Motor de desinstalação próprio**: rodar o desinstalador do programa **e** automatizar o que ele não faz (o Uninstalr é explícito: "most other uninstallers simply run the default uninstaller program"), com retomada após reinício | BCU B3 · **Uninstalr H.3** | É o que torna a desinstalação silenciosa confiável: em vez de rezar para o desinstalador aceitar `/S`, o Tranal aciona os controles. |
| 6 | **Filtro Regex + lista de desinstalação em arquivo + desinstalação em lote roteirizável** | BCU D2/D3/D4 | É o que torna o Tranal usável em script e em grande escala. |

### F.2 🟡 Recurso — lacunas claras

| # | Item | De onde veio |
|---|---|---|
| 7 | Desinstalar via MsiExec (GUID) | BCU B4 |
| 8 | Desinstalar manualmente (pular o desinstalador e listar o que apagar) | BCU B5 |
| 9 | Painel de progresso com pular / encerrar / rodar agora | BCU B10/B11 |
| 10 | Detecção de Windows Features e Windows Updates | BCU A4/A5 |
| 11 | Detecção de Steam, Chocolatey, Scoop | BCU A6/A7 |
| 12 | Varredura de drives atrás de apps não registrados | BCU A9 |
| 13 | Limpeza de pastas "Arquivos de Programas" vazias/pela metade | BCU C12 |
| 14 | Comandos antes e depois da desinstalação | BCU B15 |
| 15 | Verificação de certificado do desinstalador | BCU D8 |
| 16 | Colunas novas: ✅ Tipo de desinstalador e ✅ Código do produto (implementados); ⬜ Desinstalador silencioso, Localização do desinstalador, Fonte, Componente do sistema, Protegido, GUID, Inicialização | BCU D1 |
| 17 | Gerenciador de inicialização integrado ao ciclo de desinstalação | BCU C13 |
| 18 | Remoção definitiva de drivers | Revo E10 |
| 19 | **Um switch de CLI por ferramenta** (o HiBit tem 19) + a forma longa com sufixo ` RUN` | HiBit H.2 |

### F.3 🟢 Ganho pequeno

| # | Item | De onde veio |
|---|---|---|
| 20 | Treemap | BCU D5 |
| 21 | Legenda de cores | BCU D6 |
| 22 | Painel lateral de configurações | BCU D18 |
| 23 | Agrupamento automático por coluna | BCU D19 |
| 24 | Renomear entrada / excluir entrada do registro / assumir propriedade | BCU D13/D14/D15 |
| 25 | Copia avançada para a área de transferência | BCU D9 |
| 26 | Exportar para script de desinstalação em lote | BCU D11 |
| 27 | Classificação do usuário (estrelas) | BCU D7 |

### F.4 O que **não** fazer (decisão registrada)

| Item | Motivo |
|---|---|
| **Driver de kernel** (`revoflt.sys`) | o Revo intercepta o sistema de arquivos com `DeviceIoControl`. O Tranal usa as APIs do Windows. Menos risco, sem assinatura de driver. Decisão já registrada no `PARIDADE.md` §8 — **e** o BCU, que é open source, também não usa driver. A paridade se ganha por software, não por kernel. |
| **Gerar desinstalador simples para app órfão** | perigoso: o nome da pasta não prova a posse. O BCU faz; o Tranal decide não. O risco é real (apagar `Temp` inteiro). O dono pode reverter essa decisão se quiser paridade literal — mas é uma **decisão do dono**, não um esquecimento. |
| **Classificação de usuário com servidor** | exige infraestrutura e política de privacidade; o ganho é de conveniência, não de função. |

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

---

## G. Estado da auditoria

| Software | Documento | Estado |
|---|---|---|
| Bulk Crap Uninstaller 6.3.0 | [BCU.md](BCU.md) | ✅ completo — 80 funcionalidades medidas, 5 linhas de comando, 6 categorias de sobras |
| IObit Uninstaller 16.1.0.4 | [IOBIT.md](IOBIT.md) | ✅ completo — 142 funcionalidades medidas, 95 telas, 29 DLLs importadas |
| HiBit Uninstaller 4.0.10 | [HIBIT.md](HIBIT.md) | ✅ completo — 58 funcionalidades, 21+ ferramentas, 802 legendas pt-BR, 33 DLLs importadas |
| Uninstalr 3.2 | [UNINSTALR.md](UNINSTALR.md) | ✅ completo — 49 funcionalidades medidas, deep search de 25 min medido, 162 apps / 846 arquivos / 1.562 pastas / 394 chaves |
| Geek Uninstaller 1.6.0.200 | [GEEK.md](GEEK.md) | ✅ completo — **60 strings** (superfície inteira), menus e assistente capturados |
| HiBit Uninstaller | — | 🔄 em curso |
| Geek Uninstaller | — | 🔄 em curso |
| Uninstalr | — | 🔄 em curso |
| Total Uninstall | — | 🔄 em curso |
| Ashampoo UnInstaller | — | ⏳ na fila |
| Uninstall Tool | — | ⏳ na fila |
| Soft Organizer | — | ⏳ na fila |
| Wise Program Uninstaller | — | ⏳ na fila |

Cada auditoria concluída **atualiza esta matriz** e pode **mudar o backlog** —
um recurso que só o BCU tem hoje pode ser comum a todos, e aí vira
prioridade máxima; um recurso que só o BCU tem pode ser nicho e cair para 🟢.
