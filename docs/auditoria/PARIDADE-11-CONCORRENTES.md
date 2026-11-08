# Prova de cobertura por concorrente

Gerado por `tools\auditoria\provar-paridade.ps1`. Cada linha diz o que o
concorrente tem e o que o Tranal tem, **conferido no codigo**.

Estado: `OK` o Tranal tem · `PARCIAL` tem parte · `AUSENTE` falta ·
`FORA` limite declarado · `NAO CONFIRMADO` o dump do concorrente nao permite confirmar.

A **cobertura** conta `OK` e `FORA` (o que o Tranal tem, ou o que foi
declarado fora por decisao). `PARCIAL` aparece na coluna propria: a
funcionalidade existe, mas nao inteira — por isso ela NAO entra na
cobertura, e por isso o total medido e sempre maior que a soma das
colunas.

| Concorrente | Funcionalidades medidas | OK | Parcial | Ausente | Fora | Nao confirmado | Cobertura |
|---|---:|---:|---:|---:|---:|---:|---:|
| ASHAMPOO | 76 | 25 | 24 | 25 | 2 | 0 | 35.5% |
| BCU | 97 | 47 | 24 | 26 | 0 | 0 | 48.5% |
| GEEK | 44 | 28 | 3 | 13 | 0 | 0 | 63.6% |
| HIBIT | 66 | 23 | 15 | 24 | 4 | 0 | 40.9% |
| IOBIT | 40 | 12 | 7 | 17 | 4 | 0 | 40% |
| SOFTORGANIZER | 77 | 32 | 19 | 26 | 0 | 0 | 41.6% |
| TOTALUNINSTALL | 94 | 45 | 27 | 22 | 0 | 0 | 47.9% |
| UNINSTALLTOOL | 94 | 33 | 29 | 32 | 0 | 0 | 35.1% |
| UNINSTALR | 51 | 21 | 7 | 20 | 3 | 0 | 47.1% |
| WISE | 86 | 28 | 32 | 26 | 0 | 0 | 32.6% |
| REVO PRO 5.5.4 | 70 | 68 | 0 | 0 | 2 | 0 | 100% |
| **TOTAL** | **795** | **362** | **187** | **231** | **15** | **0** | **47.4%** |

Apuracoes linha a linha aplicadas: 173.

## O que ainda falta (estado AUSENTE)

### ASHAMPOO — 25

| # | Funcionalidade | Verificado |
|---|---|---|
| 62 | Comparação de tamanho declarado vs. real do programa, sincronizável com o Windows | nao apurado |
| 63 | Categoria automática expandida + reset para "Unsorted" | nao apurado |
| 64 | País do editor com bandeira e nome | nao apurado |
| 66 | Desinstalar o próprio produto pelo Windows "Apps & Features" | nao apurado |
| 12 | Importar logs do UnInstaller 4 | nao apurado |
| 15 | Análise forense em lote (processar todas as aplicações compatíveis de uma vez) | nao apurado |
| 61 | Instal guard: definir como o programa identifica setups e quais propriedades de arquivo usar | nao apurado |
| 36 | Wizard de snapshots por caso de uso, com nomes automáticos | nao apurado |
| 37 | Snapshot: seleção de drives incluídos e pastas excluídas | nao apurado |
| 41 | Drive Cleaner (arquivos desnecessários, com seleção por categoria e relatório) | nao apurado |
| 42 | Internet Cleaner (cookies, histórico, exceções e "cookies seguros" / whitelist) | nao apurado |
| 44 | File Associator (tipos de arquivo sem programa associado, editar/undo) | nao apurado |
| 50 | **Unlocker**: desbloquear arquivos em uso, reiniciar o Explorer, desregistrar DLL, encerrar processo | nao apurado |
| 51 | **Crash Analyzer**: relatórios de falha do Windows Event Log, agrupados por programa, com export e busca online | nao apurado |
| 53 | **Software Updater**: atualizar programas instalados, em lote, com lista negra de atualizações | nao apurado |
| 54 | **Hibernation**: colocar um programa para dormir (desabilita autostart e serviços) e acordá-lo depois | nao apurado |
| 55 | **Impact**: programas com maior impacto na performance (CPU e memória) | nao apurado |
| 56 | **Move**: mover um programa para outro local, recriando o atalho no lugar original | nao apurado |
| 57 | **Revert relocation**: devolver o programa movido à posição original | nao apurado |
| 65 | Instalação via "Ashampoo App" (`C:\Program Files\Ashampoo\Ashampoo App`, produtos em `products\`) | nao apurado |
| 71 | Enumeração de dispositivos/drivers no instalador (SetupDi) | nao apurado |
| 73 | Instalador protegido por senha (criptografia ChaCha20 do setup) | nao apurado |
| 74 | Fechamento automático de aplicações que travam a instalação (e reinício delas) | nao apurado |
| 75 | Manifesto que redireciona DLLs de sistema (`comctl32`, `mpr`, `netapi32`, `netutils`, `textshaping`, `version`) | nao apurado |
| 76 | Compilador Delphi (Embarcadero) do instalador e detecção de sistema | nao apurado |

### BCU — 26

| # | Funcionalidade | Verificado |
|---|---|---|
| 13 | Marcação de **protegido** (tag `NoRemove` do editor) | nao apurado |
| 16 | Marcação de **aplicativo inválido / desinstalador corrompido** | nao apurado |
| 26 | **Desinstalação simultânea** com prevenção de colisão | nao apurado |
| 27 | **Ordenação inteligente**: loud primeiro, para poder sair antes | nao apurado |
| 29 | **Impedir desligamento/reinício** durante a tarefa | nao apurado |
| 30 | **Monitorar desinstaladores silenciosos** atrás de popups (daemon de automatização) | nao apurado |
| 32 | **Tentar novamente** os silenciosos que falharam | nao apurado |
| 36 | **Desinstalação silenciosa de quem não aceita silent**: oferecer rodar em primeiro plano ou remover da tarefa | nao apurado |
| 40 | Lista de **aplicativos relacionados** que talvez queiram desinstalar junto | nao apurado |
| 45 | Confiança de **inicialização**: entrada `RunOnce`, entrada compatível com o app | nao apurado |
| 52 | Instalador gerado automaticamente para **app não registrado** ("Simple Delete") | nao apurado |
| 67 | **Abrir a chave no Regedit** | nao apurado |
| 75 | **Gerenciador de desinstaladores relacionados** (`RelatedUninstallerAdder`) | nao apurado |
| 79 | Estatísticas anônimas de uso (opt-in) | nao apurado |
| 81 | **Hotkeys globais** do sistema, registrados pelo processo | nao apurado |
| 82 | **Colocar o PC para dormir** quando a tarefa termina, com contagem regressiva | nao apurado |
| 83 | **Relatório de erro automático** (crash report) com envio pela web | nao apurado |
| 85 | **Desabilitar a proteção** de um item marcado como protegido | nao apurado |
| 86 | **Modificar a instalação** (reparo / alterar instalação) | nao apurado |
| 88 | **Auto-ajustar a largura de todas as colunas** | nao apurado |
| 89 | **Adicionar os selecionados como filtro** (a seleção vira critério) | nao apurado |
| 90 | **Limpar caches** da aplicação | nao apurado |
| 91 | **Abrir a Limpeza de Disco do Windows** | nao apurado |
| 92 | **Saída serializada em JSON** (além de XML/CSV) | nao apurado |
| 93 | **Compressão Brotli** em backups/arquivos gerados | nao apurado |
| 95 | Filtro que **esconde o que foi publicado pela Microsoft** | nao apurado |

### GEEK — 13

| # | Funcionalidade | Verificado |
|---|---|---|
| 2 | Total agregado no rodapé ("%d programs of %s size in total") | nao apurado |
| 5 | Aviso "No matching items, press ESC to reset" | nao apurado |
| 10 | **Remove Entry** + **Batch Remove Entries** | nao apurado |
| 14 | Diálogo de falha de desinstalação com opção de **limpar a entrada do registro e os arquivos** | nao apurado |
| 15 | "Browse..." dentro das sobras encontradas | nao apurado |
| 16 | Aba de **Microsoft Store Apps** (`Ctrl+Tab`) | nao apurado |
| 22 | **Browse Location…** e **Browse Source…** | nao apurado |
| 29 | **Desregistro de COM** (`regsvr32 /u`) antes de remover | nao apurado |
| 33 | **Correspondência aproximada (Levenshtein)** de caminho de instalação | nao apurado |
| 34 | **Varredura de atalhos do Menu Iniciar e da Área de Trabalho** para achar ícone e localização | nao apurado |
| 35 | **Limpeza do cache MRU do Windows** | nao apurado |
| 39 | **Painel "Active Files"** de arquivos em uso | nao apurado |
| 42 | **Enumeração por SID do usuário** dos pacotes MSIX | nao apurado |

### HIBIT — 24

| # | Funcionalidade | Verificado |
|---|---|---|
| 4 | **Desinstalação forçada** ("Remoção Forçada") — reagendada para o próximo logon via `RunOnce` | nao apurado |
| 8 | **Relatórios HTML por ferramenta**: `OpReport`, `ScanLog`, `ProgramCompReport`, `WindowsStoreReport`, `ServicesReport`, `DriversReport`, `StartupReport`, `SchedulerReport`, `ContextMenuReport`, `UninstallerReport` | nao apurado |
| 9 | **Ocultar entrada de desinstalação** ("Hide original uninstallation entry") | nao apurado |
| 17 | **Menu de contexto do Monitor de Instalação**: `Msi.Package\shell\HiBitMonitor` e `exefile\shell\HiBitMonitor` | nao apurado |
| 18 | **Gerenciador do Menu de Contexto** — lista, cria e remove menus do shell, com "Restaurar" e "Excluir tudo" | nao apurado |
| 21 | Desinstalar **atualizações do Windows**: `cmd.exe /c wusa.exe /uninstall /` + `KBID=` | nao apurado |
| 60 | Painel de informações do sistema no banner (CPU, GPU, RAM disponível, SO, tempo de atividade) | nao apurado |
| 62 | Botão **Doar** (adicionado na 4.0.10) | nao apurado |
| 63 | Data de instalação com **tempo relativo** ("Hoje", "Ontem", "1 mês atrás", "2 meses antes", "1 ano atrás") | nao apurado |
| 64 | Coluna **Tipo** com `32bit` / `64bit` por programa | nao apurado |
| 65 | Contadores na barra de status: total de programas **e tamanho total ocupado** ("Total: 79 Unidade", "Tamanho: 610 GB") | nao apurado |
| 68 | Gerenciador de **Serviços** | nao apurado |
| 69 | Gerenciador de **Processos** (painel) | nao apurado |
| 72 | Gerenciador de **Componentes de Programas** (DLLs compartilhadas) | nao apurado |
| 73 | Gerenciador de **Extensões de Navegador** | nao apurado |
| 77 | **Limpador de Registro** (15+ categorias: ActiveX, FileTypes, SharedDLLs, IFEO, FirewallRules, MUICache) | nao apurado |
| 80 | **Fixador de Atalhos** (atalhos corrompidos) | nao apurado |
| 82 | **Localizador de Arquivos e Registros** | nao apurado |
| 87 | **Propriedades de processo** (painel dedicado) | nao apurado |
| 88 | **Afinidade de processador** do programa | nao apurado |
| 92 | **Criar serviço** do Windows | nao apurado |
| 93 | **Criar tarefa agendada** | nao apurado |
| 98 | **Verificação de integridade de arquivo** e limpeza pontual de arquivo | nao apurado |
| 100 | **Banco local embutido** (`LOCALDB`) | nao apurado |

### IOBIT — 17

| # | Funcionalidade | Verificado |
|---|---|---|
| 3 | **Limpeza de navegador / reset de padrões** (página inicial e motor de busca) | nao apurado |
| 4 | Remoção de **plugins** do navegador | nao apurado |
| 5 | **Bloatware Remover**: pré-instalados do Windows, Windows Accessories e Windows Features, com marcação *Permanent* / *Warning* | nao apurado |
| 6 | **Bundleware**: detectar o que veio junto com o programa e desinstalar em conjunto | nao apurado |
| 7 | **Stubborn Program Remover** com base de **4.000+ programas** travados | nao apurado |
| 11 | Confirmar/avisar antes de apagar **arquivo pessoal** | nao apurado |
| 19 | Log guardado em **SQLite** (`sqlite3.dll`, 37 funções importadas) | nao apurado |
| 23 | Botão **Shred no Explorer** | nao apurado |
| 24 | **Software Relocator**: mover software instalado entre discos | nao apurado |
| 25 | **Bloqueio de popups** de notificação | nao apurado |
| 27 | **Software Updater** integrado (atualizar todos) | nao apurado |
| 29 | **Desinstalar Windows Updates** | nao apurado |
| 33 | Aviso de **sincronização do Chrome** ao remover extensão | nao apurado |
| 34 | Integração com a **Central de Ações do Windows** (Action Center) | nao apurado |
| 36 | **Voto / avaliação** de programas e plugins pela comunidade | nao apurado |
| 37 | **Captura de tela** do sistema no fluxo de suporte | nao apurado |
| 38 | **Catálogo embutido de Appx pré-instalados** (a lista do que o Windows traz de fábrica) | nao apurado |

### SOFTORGANIZER — 26

| # | Funcionalidade | Verificado |
|---|---|---|
| 9 | **Removal tools from the developers** (link para o desinstalador oficial) | nao apurado |
| 10 | Filtro **"Large Applications"** | nao apurado |
| 15 | Notificações internas (sino) e dicas | nao apurado |
| 22 | **Undo Changes Center** | nao apurado |
| 23 | Reverter uninstallação cronologicamente, renomear ou restaurar | nao apurado |
| 24 | Descarte de mudanças antigas do histórico | nao apurado |
| 25 | Aviso "use o Undo Changes Center para cancelar" | nao apurado |
| 29 | Aviso para melhorar a qualidade do rastreio | nao apurado |
| 42 | Rastreamento de **aplicativos adicionais** instalados junto | nao apurado |
| 46 | Detecção de **desinstalação externa** (app removido por fora) | nao apurado |
| 50 | "Rarely used apps" | nao apurado |
| 52 | Instalação silenciosa de programas | nao apurado |
| 54 | **Centro de dicas "Did You Know"** com dicas contextuais (arrastar para desinstalar, desinstalação externa, atualização, multilíngue) | nao apurado |
| 57 | **Carga e descarregamento de chaves** e substituição de chave | nao apurado |
| 59 | **Verificação de assinatura digital** de executáveis | nao apurado |
| 60 | **Enumeração de impressoras** e fila de impressão (para desinstalar driver de impressora) | nao apurado |
| 61 | **Descoberta de dispositivos Bluetooth LE** | nao apurado |
| 63 | Informações de **usuário e estação de rede** | nao apurado |
| 64 | Consulta da **Lixeira** para reportar itens removidos | nao apurado |
| 67 | **Tarefas de segundo plano** executadas pelo app | nao apurado |
| 68 | **Itens compartilhados e dependentes** entre programas | nao apurado |
| 69 | **Startup gear** (gerenciador de inicialização próprio) | nao apurado |
| 70 | Formulário de **notações do log** de rastreamento | nao apurado |
| 71 | Formulário do **resultado da reversão do log** | nao apurado |
| 74 | Telas de **licenciamento** (chave, assinatura, expiração da assinatura) | nao apurado |
| 77 | **Notificações internas agendadas** (avisos de dica) | nao apurado |

### TOTALUNINSTALL — 22

| # | Funcionalidade | Verificado |
|---|---|---|
| 14 | Nome do snapshot pré e pós-instalação customizável ("Allow custom snapshot name") | nao apurado |
| 15 | Pasta de snapshots com limite de tamanho e contagem ("Limits for Snapshots folder") | nao apurado |
| 18 | **Perfis** separados: Scanning, Analysis, Uninstall, Comparing, Restore/Redo, Cleaner, Auto-run view | nao apurado |
| 20 | Varredura de **Alternate Data Streams** (ADS) | nao apurado |
| 21 | **Visões de mudança** configuráveis ("Change views") | nao apurado |
| 50 | **Agente autônomo** de baixo consumo para notificar instalações em andamento | nao apurado |
| 60 | Validação **online de licença** | nao apurado |
| 30 | Restaurar **só o log de instalação**, sem o programa | nao apurado |
| 32 | **Undo/Redo** de uma restauração | nao apurado |
| 37 | Itens compartilhados: contadores de referência em registro e diálogo "Remove shared file" | nao apurado |
| 65 | **Instalar programa novo** com monitoramento do instalador | nao apurado |
| 66 | **Instalar atualização** de programa já instalado, monitorando o update | nao apurado |
| 67 | **Mesclar logs** de instalação (e apagar o log do update depois) | nao apurado |
| 72 | **Visor hexadecimal** e abertura de arquivo em editor hex/texto | nao apurado |
| 73 | **Soma MD5 dos arquivos novos** criados pela instalação | nao apurado |
| 76 | **Incluir/excluir por produto e por publicador** | nao apurado |
| 78 | **Classificação dos itens detectados**: existentes no sistema / alterados / ausentes | nao apurado |
| 79 | **Customização dos botões da barra** e legendas dos botões | nao apurado |
| 83 | **Dependências entre pacotes MSI** ("Depends on packages") | nao apurado |
| 84 | **Verificação de DCOM** do produto | nao apurado |
| 88 | **Retomar o monitoramento inacabado** no arranque do Windows | nao apurado |
| 94 | **Licença por família / múltiplos computadores / avaliação / manutenção expirada** | nao apurado |

### UNINSTALLTOOL — 32

| # | Funcionalidade | Verificado |
|---|---|---|
| 10 | **Modify** — tela de modificação de componentes | nao apurado |
| 15 | Lote com **teto de itens** e aviso explícito | nao apurado |
| 18 | Detecção de **itens relacionados** e **dependentes** | nao apurado |
| 19 | **Cancelamento forçado** de um desinstalador travado, com aviso de riscos | nao apurado |
| 28 | **Log detalhado de remoção por item**, com código de erro | nao apurado |
| 38 | **Reverter** a instalação rastreada | nao apurado |
| 39 | **Anexar** dados rastreados a um item existente | nao apurado |
| 40 | Detecção de **múltiplas entradas** de instalação | nao apurado |
| 41 | **Entrada customizada** quando o instalador não deixou registro | nao apurado |
| 44 | Menu de contexto do Explorer: **"Install and Trace"** para `.exe`/`.msi` | nao apurado |
| 45 | Menu de contexto dos **atalhos**: "Uninstall with Uninstall Tool" | nao apurado |
| 46 | Extensão de shell **empacotada em MSIX** para o Windows 11 | nao apurado |
| 47 | Serviço de rastreamento **versionado**, com prompt de atualização | nao apurado |
| 48 | Setup instala o serviço e o **driver de kernel** | nao apurado |
| 57 | Tarefas criadas pela Microsoft **ocultadas** por segurança | nao apurado |
| 67 | Relatório de **crash** com ficheiro de dump | nao apurado |
| 68 | **Contornar o UAC** criando uma tarefa elevada do Task Scheduler executada a cada abertura do app | nao apurado |
| 69 | **Ícone do Uninstall Tool no Painel de Controle** | nao apurado |
| 70 | **Popup de aplicações recém-instaladas** | nao apurado |
| 72 | **Programas recentes na barra secundária** | nao apurado |
| 76 | **Minimizar na bandeja durante a instalação** | nao apurado |
| 77 | **Exibir o ícone na bandeja do sistema** | nao apurado |
| 78 | **Iniciar automaticamente minimizado na bandeja** | nao apurado |
| 79 | **Navegar até a pasta do programa com o Total Commander** | nao apurado |
| 80 | **Criar item personalizado na lista** | nao apurado |
| 82 | **Lista compacta de aplicativos** | nao apurado |
| 83 | **Chave de licença com lista negra** e chave separada para a versão portátil | nao apurado |
| 84 | **Limpar o cache** da aplicação | nao apurado |
| 90 | **Detecção de falso positivo** ao fixar a localização de um item | nao apurado |
| 91 | **Coleta de identificação da máquina** para a licença | nao apurado |
| 92 | **Validação de cadeia de certificados** embarcada (CRYPTO/SSL) | nao apurado |
| 94 | **Install Tracker com driver de kernel**: minifiltro de sistema de arquivos + callback de registro + callback de processos | nao apurado |

### UNINSTALR — 20

| # | Funcionalidade | Verificado |
|---|---|---|
| 10 | **Exclusão** de arquivos/pastas/unidades, configurável | nao apurado |
| 14 | **16 tipos de app** com detecção dedicada: normal, MS Store, portable, sobras, Chocolatey, Ninite, PortableApps.com, Steam, EA App, Epic, Riot, GOG Galaxy, Wargaming.net, Battle.net, itch.io, Big Fish | nao apurado |
| 15 | Detecção de sobras via **MuiCache / TypeLib / AppX ModelID** | nao apurado |
| 16 | **País de origem** do app, com bandeira | nao apurado |
| 21 | **Reiniciar automaticamente** para finalizar (avisa que pode reiniciar 1 ou 2 vezes) | nao apurado |
| 23 | **Gerar `.bat`** para desinstalação em lote + `PreStartupDelete.bat` para o próximo arranque | nao apurado |
| 24 | **Retomar a desinstalação após reinício** via `RunOnce` (`Uninstalr_Unsetup_`, `Uninstalr_Rerun_`) | nao apurado |
| 26 | **Deep Analysis** sob demanda, com aviso de que pode levar minutos | nao apurado |
| 27 | Tooltip por app com Publisher, Tipo, Versão, Data, **País**, "Automatically starts with Windows", pasta, chave de registro, uninstall key, comando completo, executável principal, atalho principal | nao apurado |
| 30 | Opções de **acessibilidade** (leitor de tela, moldura padrão, botões padrão) | nao apurado |
| 35 | **Correspondência difusa (fuzzy)** de elementos e de termos de busca | nao apurado |
| 36 | **Dados do publicador extraídos do manifesto do executável** | nao apurado |
| 37 | **Seleção em massa por país** (selecionar/desmarcar por país) | nao apurado |
| 40 | **Detecção de Epic Games** e **Big Fish** como plataforma | nao apurado |
| 41 | **Detecção de NuGet** como plataforma | nao apurado |
| 42 | **PortableApps.com** como plataforma detectada | nao apurado |
| 43 | **Atalhos** como fonte de descoberta de apps | nao apurado |
| 47 | **Opções "speedhacks"** (desativar verificações lentas) | nao apurado |
| 49 | **Fechar o próprio aplicativo** ao terminar | nao apurado |
| 51 | **Modo de exibição do sistema** (system/always/never) | nao apurado |

### WISE — 26

| # | Funcionalidade | Verificado |
|---|---|---|
| 7 | "Modificar" — reaproveita o instalador do programa | nao apurado |
| 9 | Aviso para não usar a Forçada antes da Segura | nao apurado |
| 12 | Extensão de navegador, com aviso de **sincronização** | nao apurado |
| 14 | Recusa de desinstalar a si mesmo | nao apurado |
| 16 | Aviso de desinstalação anterior em andamento | nao apurado |
| 32 | **Desinstalação Personalizada** — varre por palavra-chave e deixa explorar a pasta | nao apurado |
| 33 | Botão "Explorar" para apagar o que for encontrado | nao apurado |
| 35 | Detecção da desinstalação feita por **outro** desinstalador | nao apurado |
| 41 | "Block pop-up notifications and unwanted ads" | nao apurado |
| 45 | Diálogo de **notificações** com remetente e tipo | nao apurado |
| 46 | Registro de "última análise" | nao apurado |
| 50 | Bloqueio de notificações pop-up (regra `PopNotify`) | nao apurado |
| 52 | **Base embutida de regras**: **595 perfis** nomeados e **2.379 caminhos** | nao apurado |
| 53 | Tipos de regra: **1.513 File**, **671 RegKey**, **166 RegValue**, **22 Reg**, **16 Ini**, **11 PopNotify** | nao apurado |
| 54 | Perfis de conhecidos: Epic Games Launcher, Discord, Slack, AnyDesk, Xmind, AVG PC TuneUp, IObit Driver Booster, WeChat Cache, Bilibili Offline Cache, Razer Software, DiskDrill Software, 360 Total Security, Adobe Acrobat, Ableton Live, ACDSee… | nao apurado |
| 55 | Tabela de caminhos de registro por app, com marcação `(Reg)`: **48 entradas, 14 com `(Reg)`** | nao apurado |
| 60 | Datas **relativas** no histórico | nao apurado |
| 66 | Ativação de licença PRO online, com validade e renovação | nao apurado |
| 68 | "Traduzir pro…" — convida a traduzir | nao apurado |
| 69 | Programa de melhoria da experiência do usuário (opt-in) | nao apurado |
| 73 | Tela **"My View"** (visão do usuário) | nao apurado |
| 74 | Diálogo **"Catch"** (captura do estado do sistema antes da ação) | nao apurado |
| 77 | **Renovação de licença** | nao apurado |
| 79 | **Desinstalar componente de plugin** | nao apurado |
| 82 | Acesso ao **gerenciador de credenciais** do Windows | nao apurado |
| 83 | Detecção de **geolocalização** | nao apurado |
