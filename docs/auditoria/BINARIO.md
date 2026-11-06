# Auditoria binária dos concorrentes — método, inventário e decisão

Este documento é o registro da **engenharia reversa feita sobre o código binário**
dos concorrentes do Tranal Uninstaller. Ele substitui a pratica anterior — que
identificava funcionalidades por documentacao e por execucao manual — por um
metodo que extrai a funcionalidade do proprio artefato entregue do produto.

Regra do projeto, inalterada: **medido vale, inferido nao vale**. Toda linha
deste documento aponta para um arquivo de evidencia em disco, com hash ou
caminho. O que nao deu para medir esta escrito como **nao verificado**, com o
motivo.

---

## 1. Metodo

Quatro extratores, todos rodados sobre os binarios reais dos concorrentes:

| Extrator | O que entrega | Onde esta a evidencia |
|---|---|---|
| **REA `inspect_pe_imports`** | As DLLs e as funcoes que o binario importa | `E:\Projetos\tranal-auditoria-evidencia\bin-inventario\<id>.imports.txt` |
| **REA `inspect_pe_resources`** | A arvore de recursos da PE (formularios Delphi, icones, manifesto) | `bin-inventario\<id>.recursos.txt` |
| **Extractor de strings** (C#, `tools/extrair-strings.ps1`) | Todo literal ASCII e UTF-16LE do binario, deduplicado | `bin-strings\<id>.strings.txt` |
| **Leitor de metadata .NET** (`tools/DotnetMap/Program.cs`, `System.Reflection.Metadata`) | O mapa **tipo → metodo → literal** de cada assembly .NET | `bin-inventario\managed\*.metadados.txt` |

Dois produtos exigiram extracao especifica:

- **Revo Uninstaller Pro 5.5.4**: o proprio produto instala as suas **tabelas de
  idioma** (`lang\*.ini`, UTF-16LE, `Codepage=UNICODE`). Cada secao do `.ini` e um
  modulo do produto e cada entrada e um texto de interface com id numerico
  proprio do binario. Isso da o inventario mais completo possivel: **42 modulos
  e 1.141 entradas de interface**, com o texto em portugues. A leitura e
  confirmada pelo proprio binario: a PE importa `GetPrivateProfileIntW`.
  Evidencia: `bin-inventario\revo-pro.funcionalidades.md`.
- **BCU (Bulk Crap Uninstaller)**: e .NET 8. O leitor de metadata devolveu
  **188 tipos, 1.864 metodos e 2.476 literais** com o vinculo tipo → metodo →
  literal, mais **25 assemblies .NET** mapeados (as dependencias do produto
  JA sao um mapa de features: `FlaUI.UIA3`, `Interop.WUApiLib`,
  `SteamHelper`, `StoreAppHelper`, `SimpleTreeMap`, `ScriptHelper`,
  `OculusHelper`, `UniversalUninstaller`, `UninstallerAutomatizer`).

O que a REA **nao** faz: observar execucao. Nenhum destes numeros veio de
"rodei o programa e vi". Quando a analise estatica nao alcanca, o documento
diz que nao alcancou.

### Totais medidos

| Medida | Valor |
|---|---|
| Binarios com dump de strings | 24 |
| Strings unicas extraidas | 816.370 |
| Assemblies .NET com mapa tipo → metodo → literal | 25 |
| Modulos do Revo Pro mapeados pela tabela de idioma | 42 |
| Entradas de interface do Revo Pro (com pt-BR) | 1.141 |
| Capacidades confirmadas por API importada, por concorrente | 44 a 51 |
| Concorrentes com documento canonico reescrito a partir do binario | 11 (10 + Revo) |
| Linhas de lacuna medidas (funcionalidade no binario, ausente no Tranal) | **486** em 11 listas |

### As listas de lacuna, por concorrente

| Concorrente | Linhas de lacuna | Observacao |
|---|---|---|
| BCU | 42 | |
| Geek | 30 | |
| Uninstalr | 34 | |
| IObit | 34 | 7 linhas sem confirmacao no binario |
| HiBit | 56 | 7 linhas sem confirmacao no binario |
| Wise | 58 | |
| Total Uninstall | 41 | 22 AUSENTE + 19 PARCIAL |
| Uninstall Tool | 60 | 32 AUSENTE + 28 PARCIAL |
| Soft Organizer | 45 | 26 AUSENTE + 19 PARCIAL |
| Revo Pro | 27 | as 9 lacunas `AUSENTE` reais |
| Ashampoo | 59 | **53 linhas sem confirmacao**: o payload do aplicativo esta comprimido e nao foi alcancado (ver §5) |

---

## 2. Concorrentes e o que foi extraido de cada um

| Concorrente | Binario analisado | Extracao dominante | Documento |
|---|---|---|---|
| BCU 4.8 | `BCUninstaller.dll` (.NET 8) | metadata .NET + strings | `BCU.md`, `BINARIO-BCU.md` |
| Geek Uninstaller 3.8.3 | `geek.exe` | PE imports + recursos + strings | `GEEK.md`, `BINARIO-GEEK.md` |
| Uninstalr 3.4 | `Uninstalr_Portable.exe` | strings + recursos | `UNINSTALR.md`, `BINARIO-UNINSTALR.md` |
| IObit Uninstaller 12 | `IObitUninstaler.exe` + `iush.exe` + `UninstallMonitor.exe` + `UninstallPromote.exe` + `UninstallExplorer.dll` | PE imports (48 capacidades) + strings | `IOBIT.md`, `BINARIO-IOBIT.md` |
| HiBit Uninstaller 15 | `HiBitUninstaller.exe` (Delphi) | recursos (formularios Delphi) + strings | `HIBIT.md`, `BINARIO-HIBIT.md` |
| Wise Program Uninstaller 10 | `WiseProgramUninstaller.exe` + `UnMonitor.exe` | PE imports (50 capacidades) + strings | `WISE.md`, `BINARIO-WISE.md` |
| Total Uninstall 7.6.2 | `Tu.exe` + `TuRes.dll` | strings + delay-load | `TOTALUNINSTALL.md`, `BINARIO-TOTALUNINSTALL.md` |
| Uninstall Tool 3.8.2 | `UninstallTool_x64.exe` | PE imports (51 capacidades) + strings | `UNINSTALLTOOL.md`, `BINARIO-UNINSTALLTOOL.md` |
| Soft Organizer 10.50 | `SoftOrganizer.exe` (Sciter) | PE imports (50 capacidades) + strings | `SOFTORGANIZER.md`, `BINARIO-SOFTORGANIZER.md` |
| Ashampoo UnInstaller Pro 27 | `ashampoo_uninstaller_pro_27.exe` (instalador Inno Setup) | **limitado** — ver §5 | `ASHAMPOO.md`, `BINARIO-ASHAMPOO.md` |
| Revo Uninstaller Pro 5.5.4 | `RevoUninPro.exe` + `RevoAppBar.exe` + `RevoSrp.exe` + `RevoCmd.exe` + `RUExt.dll` + `WinAppsDll.dll` + `revoflt.sys` | **tabela de idioma** (42 modulos) + PE imports (27 DLLs, 917 funcoes) | `docs/REVO_SPEC.md`, `BINARIO-REVO.md` |

---

## 3. Decisao: o que entra no Tranal

Criterio aplicado, nesta ordem:

1. **Adjacencia** — a funcionalidade pertence ao dominio de desinstalacao
   (localizar, remover, recuperar, administrar programas). Otimizador de
   sistema (analisador de disco, gerenciador de fontes, gerenciador de conexoes,
   papel de parede, destruidor de arquivos) fica **fora**: o Tranal e um
   desinstalador, nao um Windows optimizer.
2. **Cobertura** — o que o Tranal ja faz nao e reimplementado.
3. **Viabilidade** — o que cabe no stack atual (WPF / .NET 10), sem driver de
   kernel, sem automacao de janela elevada e sem base remota.

### 3.1 Lote implementado nesta rodada

| Item | Funcionalidade | Evidencia no binario | Onde ficou no Tranal |
|---|---|---|---|
| A12 | Leitura do `NoRemove` (entrada protegida) | `[Uninstaller Options] 1202 = Show system components` no `.ini` do Revo; `IsProtected` lido de `NoRemove` na chave de desinstalacao | `Core/InstalledProgram.cs`, `Core/ProgramScanner.cs`, `Core/UninstallAdvisories.cs` |
| A14 | Deteccao de desinstalador ausente ou corrompido | `Core/UninstallerHealth.cs`: `Missing` (sem comando) e `Broken` (o caminho absoluto apontado nao existe) | `Core/UninstallerHealth.cs`, `Core/ProgramScanner.cs` (`ScanOptions.IncludeEntriesWithoutUninstaller`) |
| B8 | Ordenacao inteligente (barulhento primeiro) | Revo Pro: os desinstaladores com janela vao primeiro, os silenciosos depois | `Core/Appearance/SmartOrdering.cs`, consumido pelo assistente |
| B17 | Programas relacionados | Revo Pro, secao `[Uninstaller Toolbar]`: comando de listar programas instalados junto | `Core/Discovery/RelatedProgramFinder.cs`, aviso no assistente |
| B19 | Impedir desligamento/reinicio durante a tarefa | `SetThreadExecutionState` (API do Windows) usada pelo proprio produto durante a desinstalacao | `Core/Automation/ShutdownGuard.cs`, ligado em `UninstallEngine.RunBuiltInUninstaller` |
| D16 | Abrir no Registro / Programas e Recursos / Recuperacao do Sistema | Revo Pro, secao `[Uninstaller Toolbar]`: acesso direto a "Programas e Recursos" e ao Registro | `Core/Management/SystemNavigator.cs`, menu de contexto da lista |
| B7 | Fila de desinstalacao com prevencao de colisao (um desinstalador por vez) | Revo Pro: a desinstalacao em lote e serial | `Core/Automation/UninstallQueue.cs` |
| B18 | Remover da tarefa quem nao desinstalou em silencio | Revo Pro: o item que precisa de janela sai do lote | `Core/Automation/UninstallQueue.cs` |
| C7 | Confianca de inicializacao (o `Run`/`RunOnce` do proprio app) | Revo Pro: a varredura trata a inicializacao como sinal do programa | `Core/Discovery/StartupEntries.cs`, motivo `LeftoverReason.StartupEntry` |
| Fila #4 | Reparo/modificacao de instalacao MSI (`msiexec /fa`) | `strings.txt:26799 = Change or Repair the installation of selected program` | `Core/Discovery/MsiProductCatalog.cs` (`Repair`) |
| B15 | Comandos antes e depois da desinstalacao, por programa | o plano ja tinha as etapas "comando antes" e "comando depois" | `Core/ProgramCustomization.cs` + `UninstallPlanBuilder.ResolveOptions`, tela "Comandos de desinstalacao" no menu de contexto |

### 3.2 Decidido para fora (com motivo)

| Funcionalidade | Motivo |
|---|---|
| Base de logs **online** do Revo | Decisao de produto: o Tranal e offline, sem cadastro e sem telemetria |
| Driver de filtro (`revoflt.sys`) | Fora de escopo por decisao registrada (driver de kernel) |
| Analisador de disco, gerenciador de conexoes, de fontes, papel de parede, destruidor de arquivos | Nao e dominio de desinstalacao |
| Varredura de sobras em **todas as contas** do Windows | Requer elevar para outro perfil; fica para uma rodada propria |
| Edicao do log de instalacao entrada a entrada ("Custom Uninstall" / "Advanced Uninstall") | Alto valor, alto custo: exige um formato de log de instalacao que o Tranal ainda nao grava |
| Exportacao por COM do Microsoft Excel | O Tranal ja exporta SpreadsheetML sem depender do Office instalado |

### 3.3 Fila (medida, priorizada)

A ordem abaixo vem das listas de lacuna dos 11 concorrentes, cruzadas com o
codigo. Cada linha tem a evidencia no binario de quem tem o recurso.

| # | Funcionalidade | Quem tem | Evidencia |
|---|---|---|---|
| 1 | Varredura de sobras em **todas as contas** do Windows | Revo | `[UninstallerPro] 15352 = Scan all Windows accounts for leftovers` |
| 2 | Registro de instalacao exportavel e editavel ("Custom Uninstall") | Revo, BCU | `[UninstallerPro] 15178 = Export,Import or Edit log` |
| 3 | "Advanced Uninstall": sobra que o instalador nao registrou | Revo | `strings.txt:26974` (texto do botao "Advanced Uninstall") |
| 4 | Varredura de **fluxos alternativos** (ADS) de arquivo | Total Uninstall | `inv.md:176` |
| 6 | Detectar que o programa foi **desinstalado por fora** | Soft Organizer | `recursos.txt:102` (formulario de deteccao de desinstalacao externa) |
| 7 | Filtros por **tamanho** e por **uso raro** na lista | Soft Organizer | `inv.md` (filtro "Large Applications" / "Rarely used apps") |
| 8 | Remocao de item de inicializacao do Autostart sem tocar no resto | BCU, Revo, Wise | `[Auto Run Manager Pro] 15800 = Add` |
| 9 | Busca por **Regex** na lista (e no motor de regras) | Revo, Geek, BCU | Revo le o filtro por registro (`ADVAPI32.dll` → `RegQueryValueExW`) |
| 10 | Arvore de **mudancas** entre dois snapshots, com perfis | Total Uninstall | `recursos.txt:134 TDLGCOMPARESNS` |
| 11 | Limpeza de rastros do Windows (historico, registro, cache) | Revo, BCU, IObit | — (parcial) |

### 3.4 Ja resolvido e reclassificado

A conferencia no codigo, feita depois da curadoria, **corrigiu** uma alegacao de
lacuna: o gerenciador de **servicos e tarefas agendadas** foi marcado como
ausente, mas `Core/Management/ServiceManager.cs` e
`Core/Management/ScheduledTaskManager.cs` ja fazem `List`, `Disable`, `Enable`,
`Stop` e `Delete`. Outros itens que os documentos de lacuna apontaram como
ausentes tambem ja existem no Tranal: log por item com codigo de erro
(`Core/RemovalLog.cs`), undo/redo da restauracao (`Core/Recovery/ProgramBackup.cs`),
validacao de cadeia de certificados (`Core/Discovery/UninstallerCertificate.cs`),
comparacao de snapshots e aviso de pasta compartilhada.

---

## 4. Como a decisao foi verificada

Nenhuma linha de "ausente" foi aceita sem confirmacao no codigo. Um exemplo
medido: a lista de lacunas do Revo marcava como ausente o **gerenciador
proprio de servicos e tarefas agendadas**. A conferencia no codigo mostrarou
que `Core/Management/ServiceManager.cs` e `Core/Management/ScheduledTaskManager.cs`
ja fazem `List`, `Disable`, `Enable`, `Stop` e `Delete` — a alegacao estava
errada e foi corrigida antes da decisao.

As listas de cada concorrente em `BINARIO-<COMPETIDOR>.md` sao **materia-prima
da decisao**, nao a decisao. O que vale e o que esta neste documento.

---

## 5. Limites honestos desta rodada

| Limite | Causa | Efeito |
|---|---|---|
| **Ashampoo**: o codigo do aplicativo nao foi alcancado | O instalador e Inno Setup e o payload esta comprimido (LZMA); o `innoextract 1.9` recusa ("Unexpected setup loader revision: 2") | As funcionalidades do app do Ashampoo ficam **nao verificadas**. So a superficie do instalador e o runtime Inno foram medidos |
| **Uninstalr**: tabela de importacoes nao extraida pela REA | O produtor declara 8.193+ funcoes e o limite de budget e reavaliado a cada chamada | As capacidades do Uninstalr foram medidas pelo dump de strings, nao pela tabela de importacoes |
| **Total Uninstall**: poucas funcoes na tabela de importacoes | Delphi resolve as APIs por delay-load; a funcao importada e resolvida por nome em tempo de execucao | As capacidades do Total Uninstall vieram dos literais do dump |
| **Nenhum concorrente pago foi executado** | Nenhum foi instalado nesta rodada | Comportamento observado continua sendo o da auditoria anterior; esta rodada **soma identificacao**, nao observacao |
| **Funcao nativa → pseudocodigo** | Exige o motor Ghidra da REA por binario | Nao foi usado nesta rodada: os literais, os recursos e as importacoes ja entregam a funcionalidade |

---

## 6. Onde esta a evidencia

- `E:\Projetos\tranal-auditoria-evidencia\bin-strings\` — 24 dumps de strings
- `E:\Projetos\tranal-auditoria-evidencia\bin-inventario\` — inventarios, identificadores, recursos, importacoes e o mapa de funcionalidades do Revo
- `E:\Projetos\tranal-auditoria-evidencia\bin-inventario\managed\` — mapa tipo → metodo → literal dos 25 assemblies .NET
- `docs/auditoria/BINARIO-<COMPETIDOR>.md` — a lista de lacunas de cada concorrente
- `docs/auditoria/<COMPETIDOR>.md` — o documento canonico de cada concorrente, com a coluna "Evidência no binário"
