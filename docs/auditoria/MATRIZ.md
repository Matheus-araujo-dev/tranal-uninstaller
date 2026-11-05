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
| C4 | **Justificativa textual por item** ("o diretório é usado por outros aplicativos") | ⬜ | ✅ | ⬜ | 🔴 |
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
| 2 | **Atribuir sobras pela cadeia de desinstalação**, não só pelo nome | BCU C5 | Separa "a pasta do programa" de "a pasta **dele**". Errar aqui é apagar coisa de outro. |
| 3 | **Justificativa textual por item de sobra** | BCU C4 | O Tranal já tem 3 níveis; falta dizer **por quê**. Custa pouco e muda a confiança do usuário. |
| 4 | **Desinstalação silenciosa** com gerador (inclusive por automação de interface) | BCU B2/B3 | Recurso que o dono sente na prática: sai do PC sem clicar 40 vezes. |
| 5 | **Filtro Regex + lista de desinstalação em arquivo + desinstalação em lote roteirizável** | BCU D2/D3/D4 | É o que torna o Tranal usável em script e em grande escala. |

### F.2 🟡 Recurso — lacunas claras

| # | Item | De onde veio |
|---|---|---|
| 6 | Desinstalar via MsiExec (GUID) | BCU B4 |
| 7 | Desinstalar manualmente (pular o desinstalador e listar o que apagar) | BCU B5 |
| 8 | Painel de progresso com pular / encerrar / rodar agora | BCU B10/B11 |
| 9 | Detecção de Windows Features e Windows Updates | BCU A4/A5 |
| 10 | Detecção de Steam, Chocolatey, Scoop | BCU A6/A7 |
| 11 | Varredura de drives atrás de apps não registrados | BCU A9 |
| 12 | Limpeza de pastas "Arquivos de Programas" vazias/pela metade | BCU C12 |
| 13 | Comandos antes e depois da desinstalação | BCU B15 |
| 14 | Verificação de certificado do desinstalador | BCU D8 |
| 15 | Colunas novas: ✅ Tipo de desinstalador e ✅ Código do produto (implementados); ⬜ Desinstalador silencioso, Localização do desinstalador, Fonte, Componente do sistema, Protegido, GUID, Inicialização | BCU D1 |
| 16 | Gerenciador de inicialização integrado ao ciclo de desinstalação | BCU C13 |
| 17 | Remoção definitiva de drivers | Revo E10 |

### F.3 🟢 Ganho pequeno

| # | Item | De onde veio |
|---|---|---|
| 18 | Treemap | BCU D5 |
| 19 | Legenda de cores | BCU D6 |
| 20 | Painel lateral de configurações | BCU D18 |
| 21 | Agrupamento automático por coluna | BCU D19 |
| 22 | Renomear entrada / excluir entrada do registro / assumir propriedade | BCU D13/D14/D15 |
| 23 | Copia avançada para a área de transferência | BCU D9 |
| 24 | Exportar para script de desinstalação em lote | BCU D11 |
| 25 | Classificação do usuário (estrelas) | BCU D7 |

### F.4 O que **não** fazer (decisão registrada)

| Item | Motivo |
|---|---|
| **Driver de kernel** (`revoflt.sys`) | o Revo intercepta o sistema de arquivos com `DeviceIoControl`. O Tranal usa as APIs do Windows. Menos risco, sem assinatura de driver. Decisão já registrada no `PARIDADE.md` §8 — **e** o BCU, que é open source, também não usa driver. A paridade se ganha por software, não por kernel. |
| **Gerar desinstalador simples para app órfão** | perigoso: o nome da pasta não prova a posse. O BCU faz; o Tranal decide não. O risco é real (apagar `Temp` inteiro). O dono pode reverter essa decisão se quiser paridade literal — mas é uma **decisão do dono**, não um esquecimento. |
| **Classificação de usuário com servidor** | exige infraestrutura e política de privacidade; o ganho é de conveniência, não de função. |

---

## G. Estado da auditoria

| Software | Documento | Estado |
|---|---|---|
| Bulk Crap Uninstaller 6.3.0 | [BCU.md](BCU.md) | ✅ completo — 80 funcionalidades medidas, 5 linhas de comando, 6 categorias de sobras |
| HiBit Uninstaller | — | 🔄 em curso |
| Geek Uninstaller | — | 🔄 em curso |
| Uninstalr | — | 🔄 em curso |
| IObit Uninstaller | — | 🔄 em curso |
| Total Uninstall | — | ⏳ na fila |
| Ashampoo UnInstaller | — | ⏳ na fila |
| Uninstall Tool | — | ⏳ na fila |
| Soft Organizer | — | ⏳ na fila |
| Wise Program Uninstaller | — | ⏳ na fila |

Cada auditoria concluída **atualiza esta matriz** e pode **mudar o backlog** —
um recurso que só o BCU tem hoje pode ser comum a todos, e aí vira
prioridade máxima; um recurso que só o BCU tem pode ser nicho e cair para 🟢.
