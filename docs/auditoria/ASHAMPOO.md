# Ashampoo UnInstaller 27.0.1 Professional Edition — auditoria

> O **Ashampoo UnInstaller** é o concorrente com a maior **superfície funcional**
> já levantada: **66 capacidades** inventariadas, 38 presentes nas duas edições
> e **28 exclusivas do PRO 27**. Ele não é só um desinstalador — é também um
> **monitor de instalação** (grava o log do que o instalador criou), um
> **forense pós-instalação** (cria log de desinstalação para programas que já
> estavam instalados), um **laboratório de snapshots** (compara o estado do
> sistema entre dois pontos no tempo) e um **pacote de manutenção do Windows**
> (registro, disco, rastros, serviços, autorun, plugins de navegador,
> recuperação de arquivos, atualização de programas). São duas edições: o
> **FREE 9.00.00**, de 2019, um **stub de 1,6 MB** que só baixa o produto; e o
> **PRO 27.0.1**, de outubro de 2026, instalador **Inno Setup 7.0.0.3** de
> 46,8 MB por **EUR 40**.

Data da coleta: 05/11/2026 · Versão auditada: **27.0.1** (edição **Professional**,
liberada em 16/10/2026; o **FREE 9.00.00**, de 20/12/2019, é a porta de entrada do
produto e foi o único instalador efetivamente executado nesta auditoria)

---

## 1. Identificação

| Campo | Valor | Fonte |
|---|---|---|
| Produto | **Ashampoo® UnInstaller Pro 27** (catálogo Ashampoo, `product_id` 19116, idstring `2903`) | `evidencia/ash-api-uninstaller-products.json` |
| Variante FREE | **Ashampoo® UnInstaller FREE** (catálogo Ashampoo, `product_id` 10318, idstring `2203`) | `evidencia/ash-api-uninstaller-products.json` |
| Versão PRO | `27.0.1`, `version_release_date` 2026-10-16, `trial_days` 20, `license_type` perpetual, `product_type` trial | `evidencia/ash-api-uninstaller-products.json` |
| Versão FREE | `9.00.00`, release 2019-12-20, `trial_days` 0, `license_type` free | `evidencia/ash-api-uninstaller-products.json` |
| Versão do instalador FREE | `9.00.00-eid.33904` (FileVersion `9.0.0.0`, ProductName "Ashampoo UnInstaller FREE") | `evidencia/pe-installer-free.txt` |
| Versão do instalador PRO | `27.0.1` (FileVersion `27.0.1`, ProductName "Ashampoo UnInstaller Pro 27 Setup") | `evidencia/pe-installer-pro.txt` |
| Editor | **Ashampoo GmbH & Co. KG** — Schafjückenweg 2, 26180 Rastede, Niedersachsen, Alemanha — `info@ashampoo.com` | `evidencia/pe-installer-free.txt` (certificado), `evidencia/padfile-uninstaller.xml` |
| Domínio oficial | `ashampoo.com` (www.ashampoo.com) — API de produtos, manifesto e página do produto | `evidencia/ash-api-uninstaller-products.json`, `evidencia/site-produto-uninstaller.html` |
| Site do produto | https://www.ashampoo.com/en-us/uninstaller | `evidencia/site-produto-uninstaller.html` |
| URL de download FREE | `https://www.ashampoo.com/ashampoo_uninstaller_free_sm.exe` (`eternal_download_url`) | `evidencia/ash-api-uninstaller-products.json` |
| URL de download PRO | `https://www.ashampoo.com/ashampoo_uninstaller_pro_27.exe` (`eternal_download_url`) | `evidencia/ash-api-uninstaller-products.json` |
| SHA-256 do instalador FREE | `b06f9899da6e7eba4ede16ab0d04439a693106c29920e66dee5a0b55c5029f62` | `evidencia/pe-installer-free.txt` |
| Tamanho do instalador FREE | 1.676.704 bytes (1,60 MiB) | `evidencia/pe-installer-free.txt` |
| SHA-256 do instalador PRO | `08a36652800b6b608041d6a445c38079c66239b2751fa4041e07132b9dc9184d` | `evidencia/pe-installer-pro.txt` |
| Tamanho do instalador PRO | 49.098.296 bytes (46,83 MiB) | `evidencia/pe-installer-pro.txt` |
| Formato do instalador FREE | **stub UPX-packed** — PE32+ / x86-64, seções `UPX0`/`UPX1`/`.rsrc`, 11 DLLs / **14 imports**; overlay de 12.192 B = **só o certificado Authenticode** | `evidencia/pe-installer-free.txt`, `evidencia/pea-imports-instaladores.txt` |
| Formato do instalador PRO | wrapper próprio Ashampoo (PE32 / i386, seções `.itext`/`.didata`) + **Inno Setup 7.0.0.3** no overlay (LZMA, 47.966.264 B); 5 DLLs / **150 imports** + 3 delay-load | `evidencia/pe-installer-pro.txt`, `evidencia/pea-imports-instaladores.txt` |
| Assinatura | Authenticode PKCS#7 (`signedData`), SHA-256, cadeia GlobalSign → `Ashampoo GmbH & Co. KG`, validade 2024-05-06 → **2027-06-24** | `evidencia/pe-installer-free.txt` |
| Preço e canal (documentado) | **EUR 40**, shareware, **20 dias de trial**; release "Major Update" em 16/10/2026 | `evidencia/padfile-uninstaller.xml` |
| FREE é porta de entrada do PRO | `upgrades_to: "2903"` no catálogo de produtos | `evidencia/ash-api-uninstaller-products.json` |
| Executáveis do produto | `UIFREE.exe` (FREE), `UI27.exe` (PRO), `UI15.exe` e `UI15Guard.exe` (v15) | `evidencia/ash-api-uninstaller-products.json`, `evidencia/manual-troubleshooting.txt` |
| Destino de instalação | `C:\Program Files\Ashampoo\Ashampoo App` + `products\` (via Ashampoo App) ou `C:\Program Files (x86)\Ashampoo\Ashampoo UnInstaller 15` (v15) | `evidencia/manual-troubleshooting.txt` |
| Logs em disco | `%PUBLIC%\Documents\Ashampoo\Ashampoo UnInstaller\` → `\logs`, `\logs_raw` | `evidencia/manual-logfolders.txt` |
| Tipos de instalador reconhecidos | Wise, Inno Setup, InstallShield (e por nome de arquivo) | `evidencia/manual-troubleshooting.txt` |

### O domínio e a armadilha do link intermediário

`https://www.ashampoo.com/en/uninstaller/download` responde **404** — o site
migrou de `/en/` para `/en-us/`. E o link intermediário
`https://www.ashampoo.com/en-us/dld/2203/uninstaller-free` **não entrega o
binário**: devolve uma **página HTML de 285.018 bytes** (via
`<meta http-equiv="refresh">`) cujo link real é
`https://www.ashampoo.com/ashampoo_uninstaller_free_sm.exe`.
Evidência: `evidencia/site-dld-2203.html` (primeiros bytes
`3C 21 44 4F 43 54 59 50 45` = `<!DOCTYPE`), cópia idêntica em
`downloads/ashampoo-uninstaller-free.bin`.

### Correção da rodada anterior (revisão de 2026-11-05)

A §1 escrita na primeira rodada afirmava que o instalador PRO **não** era Inno
Setup ("innoextract: Unexpected setup loader revision: 2"). Isso está **errado**:
a varredura do arquivo inteiro encontrou as assinaturas
`Inno Setup Setup Data (7.0.0.3)` no offset **740.624** (dentro de `.data` do
próprio wrapper) e **47.558.923** (no overlay). A falha do `innoextract` 1.9
(suporta até 6.0.5) é **compatibilidade da ferramenta**, não ausência de Inno
Setup. O payload é Inno Setup 7.0.0.3 em LZMA e **não pôde ser extraído** com o
que existe nesta máquina (§8).

---

## 2. Superfície observável

Inventário de **funcionalidades** (não de telas). **66 itens**, numerados de 1 a
66 sem lacuna. A última coluna diz em qual edição a capacidade está documentada:
**FREE + PRO** = existe nas duas; **só PRO** = não existe na edição FREE.

> **Todos os 66 itens são documentais** (manuais oficiais FREE e Pro 27, site
> oficial e manifesto PAD). O que foi **medido** nesta máquina está na §3
> (imports, empacotamento, assinatura) e na §4.1 (instalador FREE) — a
> §4.2 inteira é documental, por causa da limitação de medição registrada.

### 2.1 Núcleo do desinstalador e lista de programas

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 17 | Desinstalar vários programas de uma vez (caixas de seleção) | nao confirmado no binario — doc.: `manual-free.txt:60` | **OK** - Desinstalar vários programas de uma vez (caixas de seleção)/`src/Tranal.Uninstaller/MainWindow.xaml:591` (`SelectionMode="Extended"`) `src/Tranal.Uninstaller/Core/Automation/BatchList.cs:17` (`public sealed class BatchList`) `src/Tranal.Cli/ExtraCommands.cs:994` (`BatchList.Load`) |
| 18 | Limpeza em profundidade após a desinstalação (in-depth cleaning), habilitada por padrão | nao confirmado no binario — doc.: `manual-free.txt:191-193`, `manual-pro27.txt:156-157` | OK — `Core/UninstallSupport.cs` (`LeftoverScanner`, `LeftoverReason`) |
| 19 | Procedimento de desinstalação em 5 etapas, reversível mesmo sem log | nao confirmado no binario — doc.: `site-produto-uninstaller.html:5226` | OK — 5 etapas em `docs/PARIDADE.md` §"Modos de desinstalação"; reversão em `Core/Recovery/ProgramBackup.cs` |
| 20 | Histórico das instalações/desinstalações recentes, com botão de desinstalar | nao confirmado no binario — doc.: `manual-free.txt:33-35`, `manual-free.txt:198-200` | **OK** - Histórico das instalações/desinstalações recentes, com botão de desinstalar/`src/Tranal.Uninstaller/Core/History/HistoryReport.cs:86` (`public static class HistoryReport`) `src/Tranal.Uninstaller/Ui/Tools/HistoryViews.cs:98` (`HistoryReport.Read`) `src/Tranal.Cli/HistoryCommands.cs:53` (`HistoryReport.Read`) a tela traz data em tempo relativo, os residuos e o botao de recuperar. |
| 22 | Remover a entrada da lista do Windows de programas instalados, sem desinstalar | nao confirmado no binario — doc.: `manual-free.txt:114-116` | **OK** - Remover a entrada da lista do Windows de programas instalados, sem desinstalar/modulo da lista do Windows `Core/Management/WindowsListEntry.cs:44` |
| 23 | Filtrar a lista por texto e ocultar categorias comuns (ex.: .NET Framework) | nao confirmado no binario — doc.: `manual-free.txt:69-71` | **OK** - A busca textual e a ocultacao de categorias comuns existem (`MainWindow.xaml.cs:264` `HiddenFilterCheck` liga/desliga `ShowSystemComponents`) |
| 24 | Filtrar a lista **por tamanho** (subtópicos) | nao confirmado no binario — doc.: `manual-free.txt:60`, `manual-pro27.txt:39` | **OK** - **FALSO POSITIVO do documento:** afirma "nenhum filtro por tamanho localizado no `src/`"; existem `all`/`small`/`medium`/`large` - `docs/auditoria/MATRIZ.md:146` |
| 25 | Criar relatório (tabela) de todos os programas instalados, aberto no navegador | nao confirmado no binario — doc.: `manual-free.txt:170-172` | OK — `Core/Exporters.cs` (`ToHtml`) |
| 26 | Exportar a lista de programas para arquivo e **importar** lista exportada (marcar não instalados) | nao confirmado no binario — doc.: `manual-pro27.txt:104-114` | **OK** - Casa cada item importado com o instalado por nome e versao |
| 28 | Instalar um programa com monitoramento (arrastar o setup ou "Browse") | nao confirmado no binario — doc.: `manual-free.txt:205-211`, `manual-pro27.txt:167-170` | OK — `Core/Modules/InstallMonitor.cs`, `Core/Modules/TracedPrograms.cs` |
| 29 | Atalhos de acesso rápido por módulo na tela de visão geral (tiles) | nao confirmado no binario — doc.: `manual-free.txt:28` | **OK** - Os modulos sao paineis com botao proprio na tela principal (`MainWindow.xaml.cs:588`) |
| 59 | Ratings por estrelas (1–5) com base online e download de database | nao confirmado no binario — doc.: `manual-free.txt:51-53`, `manual-pro27.txt:142-144` | **FORA POR DECISAO** - Base online exige servico: `docs/auditoria/BINARIO.md` secao 3.2 (1a linha, "o Tranal e offline, sem cadastro e sem telemetria") e `docs/PARIDADE.md:481` |
| 60 | Busca online do programa e "Rate online" | nao confirmado no binario — doc.: `manual-pro27.txt:128-137` | **PARCIAL** - Busca online do programa e "Rate online"/`src/Tranal.Uninstaller/Core/WebSearch.cs:19` (`public static class WebSearch`) **Nota**: a busca online existe e e acionada pelo detalhe do programa; falta a acao de avaliar ("rate online"). |
| 62 | Comparação de tamanho declarado vs. real do programa, sincronizável com o Windows | nao confirmado no binario — doc.: `site-produto-uninstaller.html:5412` | **PARCIAL** - Comparação de tamanho declarado vs. real do programa, sincronizável com o Windows/`src/Tranal.Uninstaller/Core/ProgramColumns.cs:9` (`public enum ProgramColumn`) `src/Tranal.Uninstaller/Core/Modules/ProgramImpactAnalyzer.cs:56` (`public static class ProgramImpactAnalyzer`) **Nota**: existe a medicao de tamanho real e a coluna de tamanho, mas nao ha a tela de comparacao entre o declarado e o real, nem a sincronizacao com o Windows. |
| 63 | Categoria automática expandida + reset para "Unsorted" | nao confirmado no binario — doc.: `site-produto-uninstaller.html:5413`, `5425` | **AUSENTE** - Categoria automática expandida + reset para "Unsorted"/grep "Unsorted" em src/ -> 0 hits o agrupamento por coluna existe (`Core/ProgramColumns.cs`), mas nao ha a categoria "Unsorted" nem o reset para ela |
| 64 | País do editor com bandeira e nome | nao confirmado no binario — doc.: `site-produto-uninstaller.html:5414` | **OK** - `src/Tranal.Uninstaller/Core/Appearance/CountryDetector.cs:51` (`Detect`) le o recurso de versao do executavel e devolve codigo, nome e bandeira; `FromCode` em `:77`, bandeira em `BandeiraDe` `:133`, nome pela tabela unica `CountryNames.Nome` (`src/Tranal.Uninstaller/Core/Localization/CountryNames.cs:113`, chamada em `CountryDetector.cs:125`). Coluna na grade: `src/Tranal.Uninstaller/Core/ProgramColumns.cs:105` (enum), `:215` (definicao), `:283` e `:314-321` (render e `CountryOf`); `src/Tranal.Uninstaller/Ui/ProgramRow.cs:76`; `src/Tranal.Uninstaller/Ui/ProgramColumnLayout.cs:45` e `:77`. Binario do concorrente: nao confirmado no binario |
| 66 | Desinstalar o próprio produto pelo Windows "Apps & Features" | nao confirmado no binario — doc.: `manual-pro27.txt:513-521` | **OK** - `grep UninstallDisplayName/UninstallDisplayIcon` -> `tranal.iss:93-94`; o Inno Setup grava a chave `Uninstall` no registro |

### 2.2 Monitor de instalação e log de instalação

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 1 | Monitor de instalação integrado que grava log detalhado de cada instalação | nao confirmado no binario — doc.: `manual-free.txt:5`, `manual-pro27.txt:6` | OK — `Core/Modules/InstallMonitor.cs`, `Core/Modules/TraceStore.cs` |
| 2 | Detecção automática do início/fim da instalação (com aviso "Installation completed" manual como fallback) | nao confirmado no binario — doc.: `manual-free.txt:211`, `manual-pro27.txt:170` | OK — `Core/Modules/TracedPrograms.cs` (fotografia antes → instalador → fotografia depois) |
| 3 | Ícone na bandeja do sistema para o monitor de instalação, com acionamento manual | nao confirmado no binario — doc.: `manual-pro27.txt:13-14` | **AUSENTE** - Ícone na bandeja do sistema para o monitor de instalação, com acionamento manual/grep "NotifyIcon" em src/ -> 0 hits o monitor de instalacao existe (`Core/Modules/InstallMonitor.cs`), mas a bandeja nao |
| 4 | Reconhecimento de tipos de instalador (Wise, Inno Setup, InstallShield) e por nome de arquivo | nao confirmado no binario — doc.: `manual-troubleshooting.txt:5` | OK — `Core/UninstallerKind.cs` (12 tipos, `docs/auditoria/MATRIZ.md` A2 ✅) |
| 5 | Separação entre instalação e atualização, reduzindo detecções falsas | nao confirmado no binario — doc.: `site-produto-uninstaller.html:5515` | **PARCIAL** - Separação entre instalação e atualização, reduzindo detecções falsas/`src/Tranal.Uninstaller/Core/Discovery/UpdateMarker.cs:53` (`public static class UpdateMarker`) **Nota**: a distincao novo x atualizado existe no nivel do programa; dentro do log as entradas nao carregam essa marcacao (`Core/Tracing/TraceLogV2.cs`). |
| 6 | Log por programa com arquivos e alterações de registro | nao confirmado no binario — doc.: `manual-free.txt:76-86` | OK — `Core/Modules/TraceStore.cs` |
| 7 | Mostrar detalhe do log (entradas de registro modificadas etc.) | nao confirmado no binario — doc.: `manual-free.txt:83-85` | OK — `Core/Modules/TraceStore.cs` |
| 8 | Exportar lista de arquivos do log (txt) | nao confirmado no binario — doc.: `manual-free.txt:90-92` | **OK** - `ToText` lista os arquivos com caminho, tamanho e data (`TraceLogExporter.cs:58-64`) |
| 9 | Exportar alterações de registro do log (.reg) | nao confirmado no binario — doc.: `manual-free.txt:97-99` | **OK** - Usa o tipo real do valor (`GetValueKind`): binario sai `hex:` |
| 10 | Editar manualmente o log de instalação (remover entradas) | nao confirmado no binario — doc.: `manual-pro27.txt:144-146` | **OK** - Edicao de entrada (arquivo, pasta, chave, valor) com gravacao do log |
| 11 | Apagar o log sem desinstalar (o programa passa a ser "unmonitored") | nao confirmado no binario — doc.: `manual-free.txt:104-106` | OK — `Core/Modules/TraceStore.cs` (apaga o log) |
| 12 | Importar logs do UnInstaller 4 | nao confirmado no binario — doc.: `manual-free.txt:177-179` | **AUSENTE** - Importar logs do UnInstaller 4/grep "Uninstaller4" em src/ -> 0 hits o importador valida o formato e recusa qualquer outro (`Core/Modules/TraceStore.cs`) |
| 13 | Abrir a pasta de logs no Explorer | nao confirmado no binario — doc.: `manual-free.txt:184-186` | **OK** - Abrir a pasta de logs no Explorer/`src/Tranal.Uninstaller/Core/Tools/SystemActions.cs:42` (`public static bool AbrirPastaDeLogs`) `src/Tranal.Uninstaller/Core/RemovalLog.cs:168` (`LogDirectory`) `src/Tranal.Cli/InspectionCommands.cs:41` (`ExecutarPastaDeLogs`) |
| 14 | **Análise forense / log pós-instalação** para programas instalados sem monitoramento ("Create post-install log") | nao confirmado no binario — doc.: `manual-pro27.txt:43-46`, `site-produto-uninstaller.html:5428-5430` | **OK** - Analise forense pos-instalacao com procedencia `medido`/`inferido` marcada (`ForensicAnalyzer.cs:74`) |
| 15 | Análise forense em lote (processar todas as aplicações compatíveis de uma vez) | nao confirmado no binario — doc.: `site-produto-uninstaller.html:5429` | **PARCIAL** - Análise forense em lote (processar todas as aplicações compatíveis de uma vez)/`src/Tranal.Uninstaller/Core/Management/SoftwareHealth.cs:105` (`public static class SoftwareHealth`) **Nota**: o `SoftwareHealth.Analyze` varre todos os programas de uma vez, mas e uma analise de saude, nao a analise forense do concorrente. |
| 61 | Instal guard: definir como o programa identifica setups e quais propriedades de arquivo usar | nao confirmado no binario — doc.: `manual-pro27.txt:489-490` | **OK** - Instal guard: definir como o programa identifica setups e quais propriedades de arquivo usar/modulo do Instal guard `Core/Automation/InstallerGuard.cs:153`; a deteccao do instalador `Core/Automation/InstallerGuard.cs:250`; as regras embutidas `Core/Automation/InstallerGuard.cs:169` |

### 2.3 Snapshots e análise forense

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 16 | Log de instalação **criado a partir da diferença entre dois snapshots** | nao confirmado no binario — doc.: `manual-pro27.txt:275-279` | OK — `Core/Modules/TracedPrograms.cs` + `Core/Modules/SystemSnapshot.cs` (`Diff`) |
| 33 | **Snapshots** do estado do sistema (criar, nomear, listar, apagar) | nao confirmado no binario — doc.: `manual-pro27.txt:246-269` | **OK** - `Ui/Tracing/TracingWindow.xaml.cs:361` cria, `:408` apaga, `:519` lista |
| 34 | **Comparar dois snapshots** e visualizar as diferenças por categoria, com "Generate report" | nao confirmado no binario — doc.: `manual-pro27.txt:281-285` | **OK** - Expoe `ModifiedFiles`, `ModifiedRegistryValues`, `RemovedFiles`, `UnchangedFiles` (`TracingWindow.xaml.cs:578-583` mostra as contagens) |
| 35 | "Create 2 snapshots now and show changes" (fluxo automático de diff) | nao confirmado no binario — doc.: `manual-pro27.txt:271-273` | **OK** - Captura e comparacao estao na mesma tela (`TracingWindow.xaml.cs:361` e `:373`), com o diff A x B descrito em `:571-583` |
| 36 | Wizard de snapshots por caso de uso, com nomes automáticos | nao confirmado no binario — doc.: `manual-pro27.txt:287-288` | **PARCIAL** - Wizard de snapshots por caso de uso, com nomes automáticos/`src/Tranal.Uninstaller/Core/Tracing/SnapshotLibrary.cs:60` (`public static class SnapshotLibrary`) `src/Tranal.Uninstaller/Ui/Tracing/TracingWindow.xaml.cs:361` (`SnapshotLibrary.Capture`) **Nota**: o snapshot aceita nome, mas o nome e digitado — nao ha wizard por caso de uso nem nome automatico. |
| 37 | Snapshot: seleção de drives incluídos e pastas excluídas | nao confirmado no binario — doc.: `manual-pro27.txt:492-493` | **PARCIAL** - Snapshot: seleção de drives incluídos e pastas excluídas/`src/Tranal.Uninstaller/Core/Tracing/SnapshotLibrary.cs:60` (`public static class SnapshotLibrary`) `src/Tranal.Uninstaller/Core/Modules/SystemSnapshot.cs:43` (`public sealed class SystemSnapshot`) **Nota**: as raizes sao parametrizaveis no nucleo, mas a interface chama `Capture(nome, rotulo)` sem raizes — nao ha selecao de unidades nem pastas excluidas. |

### 2.4 Ferramentas de manutenção, limpeza e recuperação

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 38 | Backups do Registry Optimizer, com restauração, exclusão por idade (7/14/30 dias) e visão detalhada | nao confirmado no binario — doc.: `manual-free.txt:463-509` | **OK** - Exclusao por data existe nos dois niveis (backup de sobras e pontos de restauracao) |
| 39 | Registry Optimizer (limpeza de entradas obsoletas, com relatório e exceções) | nao confirmado no binario — doc.: `manual-free.txt:300-301` | OK — `Core/Tools/RegistryOptimizer.cs` |
| 40 | Registry Optimizer 2 + "Super Safe Mode" (limpeza mais cautelosa) | nao confirmado no binario — doc.: `site-produto-uninstaller.html:5400-5401`, `5432-5433` | **PARCIAL** - Registry Optimizer 2 + "Super Safe Mode" (limpeza mais cautelosa)/`src/Tranal.Uninstaller/Core/Tools/RegistryOptimizer.cs:16` (`public sealed class RegistryOptimizer`) `src/Tranal.Uninstaller/Ui/Tools/ToolViews.cs:37` (`RegistryOptimizer`) **Nota**: o otimizador e as regras de protecao existem; nao ha o modo "Super Safe Mode" nomeado. |
| 41 | Drive Cleaner (arquivos desnecessários, com seleção por categoria e relatório) | nao confirmado no binario — doc.: `manual-pro27.txt:295-298`, `manual-free.txt:270` | **OK** - `src/Tranal.Uninstaller/Core/Tools/DriveCleaner.cs:74` (`BuildCategories`, 7 categorias em `:74-144`), `:149` (`Categories`), `:154` (`Scan`, itens ja marcados por categoria), `:180` (`Summary`), `:193` (`Clear`), `:236` (`WriteReport`, HTML). Tela `src/Tranal.Uninstaller/Ui/Tools/CleanupViews.cs:280`, com `DriveScan` `:296`, `DriveClean` `:297` e `DriveReport` `:298` (gravacao do HTML em `SaveFileDialog` `:353-363`). CLI `src/Tranal.Cli/CleanupCommands.cs:38`, relatorio em `:147` e limpeza em `:165` (`tranal /drive`). Binario do concorrente: nao confirmado no binario |
| 42 | Internet Cleaner (cookies, histórico, exceções e "cookies seguros" / whitelist) | nao confirmado no binario — doc.: `manual-free.txt:281-283`, `manual-pro27.txt:303-309` | **OK** - `src/Tranal.Uninstaller/Core/Tools/InternetCleaner.cs:75` (classe), 5 grupos (cookies `:81`, historico `:84`, cache `:87`, sessoes `:90`, formularios `:93`), `:176` e `:200` (`Scan`), `:213` (`Clear`); whitelist em `LoadExceptions` `:261`, `AddException` `:292`, `RemoveException` `:309`, `IsExcepted` `:326`, aplicada aos cookies em `ExceptedHostsIn` `:470`. Tela `src/Tranal.Uninstaller/Ui/Tools/CleanupViews.cs:185`, com `InternetScan` `:201`, `InternetClean` `:202` e `InternetException` `:203` (entrada do dominio em `:258-268`). CLI `src/Tranal.Cli/CleanupCommands.cs:37` e `:103` (`tranal /internet /exception`). Binario do concorrente: nao confirmado no binario |
| 43 | File Wiper (sobrescrita de espaço livre e de arquivos/pastas) | nao confirmado no binario — doc.: `manual-pro27.txt:512-580` | **PARCIAL** - File Wiper (sobrescrita de espaço livre e de arquivos/pastas)/`src/Tranal.Uninstaller/Core/Tools/UnrecoverableDelete.cs:46` (`public static class`) **Nota**: a sobrescrita do arquivo removido existe; nao ha a varredura de espaco livre por volume. |
| 44 | File Associator (tipos de arquivo sem programa associado, editar/undo) | nao confirmado no binario — doc.: `manual-free.txt:414-457` | **OK** - File Associator (tipos de arquivo sem programa associado, editar/undo)/modulo de associacoes de arquivo `Core/Tools/FileAssociator.cs:84`; a troca do programa `Core/Tools/FileAssociator.cs:159`; o desfazer `Core/Tools/FileAssociator.cs:216` |
| 45 | Desfragmentação de drives (citada entre as ferramentas) | nao confirmado no binario — doc.: `manual-free.txt:270` | **FORA POR DECISAO** - Nao e dominio de desinstalacao: `docs/auditoria/BINARIO.md` secao 3.2 (analisador de disco, gerenciador de conexoes, de fontes, papel de parede, destruidor de arquivos: nao e dominio de desinstalacao) |
| 46 | Gerenciador de serviços: alterar tipo de inicialização, iniciar/parar, ver dependências | nao confirmado no binario — doc.: `manual-free.txt:330-402` | **OK** - **FALSO POSITIVO do documento:** ele afirma que o Tranal so abre `services.msc` (`Core/Tools/WindowsToolsCatalog.cs:85`); o gerenciador proprio existe - `docs/auditoria/BINARIO.md:160-161` |
| 47 | Autorun / StartUp Tuner: habilitar, desabilitar, adicionar, editar e remover entradas de inicialização | nao confirmado no binario — doc.: `manual-free.txt:288-318`, `manual-pro27.txt:316-331` | **PARCIAL** - Autorun / StartUp Tuner: habilitar, desabilitar, adicionar, editar e remover entradas de inicialização/`src/Tranal.Uninstaller/Core/Tools/AutorunManager.cs:72` (`public static class AutorunManager`) `src/Tranal.Uninstaller/Ui/Tools/ToolViews.cs:185` (`AutorunManager.SetEnabled`) `src/Tranal.Uninstaller/Ui/Tools/ToolViews.cs:205` (`AutorunManager.Remove`) **Nota**: habilitar, desabilitar e apagar existem e tem consumidor na tela; **editar** a entrada existente (nome e comando) e **adicionar** nao tem consumidor na interface. |
| 48 | Boot Center: resumo de tempo de boot, autorun e tarefas agendadas (enable/disable/delete) | nao confirmado no binario — doc.: `manual-pro27.txt:311-343` | **OK** - Boot Center: resumo de tempo de boot, autorun e tarefas agendadas (enable/disable/delete)/modulo do Boot Center `Core/Tools/BootCenter.cs:89`; habilitar e desabilitar `Core/Tools/BootCenter.cs:146`; remover `Core/Tools/BootCenter.cs:174` |
| 49 | Privacy Traces Cleaner + limpeza de jump lists | nao confirmado no binario — doc.: `manual-pro27.txt:440-441` | OK — `Core/Tools/EvidenceRemover.cs` (jump lists, prefetch, shellbags) |
| 50 | **Unlocker**: desbloquear arquivos em uso, reiniciar o Explorer, desregistrar DLL, encerrar processo | nao confirmado no binario — doc.: `manual-pro27.txt:437-455` | **OK** - As quatro capacidades estao no modulo `src/Tranal.Uninstaller/Core/Automation/Unlocker.cs`: `Scan` `:80` e `IsLocked` `:136` (descobrir quem segura), `TerminateLockers` `:194` (encerrar processo), `RestartExplorer` `:237` (reiniciar o Explorer), `UnregisterDll` `:262` (`regsvr32 /u /s`); a acao entra por `Unlock` `:147`. Tela `src/Tranal.Uninstaller/Ui/Tools/CleanupViews.cs:92` (`CreateUnlocker`), chamada em `:171` (`Unlocker.Unlock(caminho, reiniciarExplorer, reiniciarExplorer)`): o botao "Destravar" `:109` nao desregistra DLL e o botao "Destravar e reiniciar Explorer" `:110` desregistra e reinicia; a CLI repete a mesma combinacao em `src/Tranal.Cli/CleanupCommands.cs:82` (`tranal /unlock ... /explorer`). Binario do concorrente: nao confirmado no binario |
| 51 | **Crash Analyzer**: relatórios de falha do Windows Event Log, agrupados por programa, com export e busca online | nao confirmado no binario — doc.: `manual-pro27.txt:457-458`, `site-produto-uninstaller.html:5402`, `5435-5436` | **PARCIAL** - A leitura e o agrupamento existem: `src/Tranal.Uninstaller/Core/Tools/CrashAnalyzer.cs:62` (`Scan` do Event Log "Application"), `:134` (`Converter` do evento) e agrupamento por `ProgramName` em `:109-113` (nome em `:154`). Tela `src/Tranal.Uninstaller/Ui/Tools/InsightViews.cs:171`, carga em `:201`, barra lateral em `src/Tranal.Uninstaller/MainWindow.xaml:371`. **Nota**: faltam **export** e **busca online** - a tela tem um unico comando, "Atualizar" (`InsightViews.cs:187-192`), e em `CrashAnalyzer.cs` nao ha gravacao de relatorio (a busca por `Export` e `ToolReportWriter` no arquivo nao retorna hits; o `ToolReportWriter` so e usado em `src/Tranal.Cli/RegistryCommands.cs:105`). Binario do concorrente: nao confirmado no binario |
| 52 | **Undeleter**: recuperar arquivos apagados acidentalmente | nao confirmado no binario — doc.: `manual-free.txt:381-382` | **PARCIAL** - **Undeleter**: recuperar arquivos apagados acidentalmente/`src/Tranal.Uninstaller/Core/Recovery/LeftoverBackup.cs:99` (`public static class LeftoverBackup`) `src/Tranal.Uninstaller/Core/History/HistoryReport.cs:148` (`public static (bool Ok, string Message) Recover`) **Nota**: o Tranal recupera o que ele proprio removeu (backup antes da remocao); nao ha undelete de arquivo apagado fora do Tranal. |
| 53 | **Software Updater**: atualizar programas instalados, em lote, com lista negra de atualizações | nao confirmado no binario — doc.: `manual-pro27.txt:172-175`, `site-produto-uninstaller.html:5407-5408`, `5419-5420` | **OK** - **Software Updater**: atualizar programas instalados, em lote, com lista negra de atualizações/`src/Tranal.Uninstaller/Core/Updates/UpdateService.cs:29` (`public static class UpdateService`) `src/Tranal.Uninstaller/Ui/Tools/UpdateViews.cs:22` (`UpdateService`) `src/Tranal.Cli/UpdateCommands.cs:40` (`UpdateService.AtualizadorDeProgramas`) a lista negra e a preferencia `UpdateBlacklist` (`Core/Settings/AppSettings.cs`), e o feed de programas e `feed/programas.json`. |
| 54 | **Hibernation**: colocar um programa para dormir (desabilita autostart e serviços) e acordá-lo depois | nao confirmado no binario — doc.: `manual-pro27.txt:177-181` | **OK** - **Hibernation**: colocar um programa para dormir (desabilita autostart e serviços) e acordá-lo depois/modulo da dormencia `Core/Tools/ProgramHibernation.cs:69`; o ato de dormir `Core/Tools/ProgramHibernation.cs:115`; o ato de acordar `Core/Tools/ProgramHibernation.cs:209` |
| 55 | **Impact**: programas com maior impacto na performance (CPU e memória) | nao confirmado no binario — doc.: `manual-pro27.txt:212-213` | **OK** - `src/Tranal.Uninstaller/Core/Modules/ProgramImpactAnalyzer.cs:56` (classe), `:64` (`Rank`, mede memoria e CPU de cada programa instalado e ordena por impacto), `:113` (`CalcularNota`). Tela `src/Tranal.Uninstaller/Ui/Tools/InsightViews.cs:107`, carga em `:143`, colunas de score, memoria e CPU em `:117-119`; barra lateral `src/Tranal.Uninstaller/MainWindow.xaml:363`; despacho `src/Tranal.Uninstaller/Ui/Tools/ToolViews.cs:63`. Binario do concorrente: nao confirmado no binario |
| 56 | **Move**: mover um programa para outro local, recriando o atalho no lugar original | nao confirmado no binario — doc.: `manual-pro27.txt:163-165` | **OK** - `src/Tranal.Uninstaller/Core/Management/RelocationManager.cs:81` (`Move`: move a pasta de instalacao para o destino escolhido), `:131` chamando `AtualizarEntradaDeRegistro` (implementacao em `:192`) e `:133` chamando `RecriarAtalhoNoDestinoAntigo` (implementacao em `:252`); historico em `Historico` `:186`. Tela `src/Tranal.Uninstaller/Ui/Tools/InsightViews.cs:231`, escolha de programa e destino em `:304-325` e a chamada em `:327`; barra lateral `src/Tranal.Uninstaller/MainWindow.xaml:379`. Binario do concorrente: nao confirmado no binario |
| 57 | **Revert relocation**: devolver o programa movido à posição original | nao confirmado no binario — doc.: `manual-free.txt:84-86` | **OK** - `src/Tranal.Uninstaller/Core/Management/RelocationManager.cs:149` (`Revert`, devolve o programa a posicao original a partir do historico gravado em `:186`); tela `src/Tranal.Uninstaller/Ui/Tools/InsightViews.cs:231`, lista do historico em `:273` e a chamada do revert em `:342`; barra lateral `src/Tranal.Uninstaller/MainWindow.xaml:379`. Binario do concorrente: nao confirmado no binario |
| 58 | Atalho de área de trabalho "Drag & Drop UnInstall" (arrastar atalhos para desinstalar) | nao confirmado no binario — doc.: `manual-pro27.txt:161` | **PARCIAL** - Atalho de área de trabalho "Drag & Drop UnInstall" (arrastar atalhos para desinstalar)/`src/Tranal.Uninstaller/Core/Modules/HunterMode.cs:38` (`public static class HunterMode`) `src/Tranal.Uninstaller/MainWindow.xaml:189` (`AutomationProperties.AutomationId="ModuleHunterMode"`) **Nota**: o arrastar-e-soltar existe na tela do Hunter, mas nao ha o atalho de area de trabalho do concorrente (`installer/tranal.iss` cria so o icone do programa). |

### 2.5 Exportação, integração e Windows Apps

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 27 | Exportar logs de desinstalação em HTML, CSV e TXT | nao confirmado no binario — doc.: `site-produto-uninstaller.html:5405` | **OK** - O log exporta nesses tres formatos (a lista exporta em `Core/Exporters.cs:39,49,63`) |
| 30 | Remoção de Windows-Apps / system apps, com remoção parcial e restauração dos apps padrão | nao confirmado no binario — doc.: `manual-free.txt:216-228`, `manual-free.txt:261-263` | **PARCIAL** - Remoção de Windows-Apps / system apps, com remoção parcial e restauração dos apps padrão/`src/Tranal.Uninstaller/Core/Modules/WindowsApps.cs:39` (`public static class WindowsApps`) `src/Tranal.Uninstaller/Ui/Modules/ModuleViews.cs:23` (`WindowsApps`) **Nota**: a listagem e a remocao de pacotes MSIX existem, mas nao ha a remocao parcial nem a restauracao dos apps padrao. |
| 31 | Listar e remover **plugins de navegador** | nao confirmado no binario — doc.: `manual-pro27.txt:215-218` | OK — `Core/Modules/BrowserExtensions.cs` |
| 32 | Remover add-ons e toolbars com um clique | nao confirmado no binario — doc.: `site-produto-uninstaller.html:5226` | **PARCIAL** - Remover add-ons e toolbars com um clique/`src/Tranal.Uninstaller/Core/Modules/BrowserExtensions.cs:53` (`public static class BrowserExtensions`) `src/Tranal.Uninstaller/Ui/Tools/MaintenanceViews.cs:380` (`BrowserCleanup.FindOrphanExtensions`) **Nota**: extensoes e add-ons de Chromium e Firefox sao cobertos; toolbars nao existem no codigo. |
| 65 | Instalação via "Ashampoo App" (`C:\Program Files\Ashampoo\Ashampoo App`, produtos em `products\`) | nao confirmado no binario — doc.: `manual-troubleshooting.txt:13-15` | **FORA POR DECISAO** - Decisao de produto registrada no catalogo canonico: "decisao de produto: o Tranal e distribuido por instalador proprio (`installer/tranal.iss`), sem loja de aplicativos" - `docs/auditoria/ASHAMPOO.md:175` (item 65 do catalogo) |

### 2.6 Linha de comando do desinstalador

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 21 | Editar os parâmetros da linha de comando de desinstalação do programa | nao confirmado no binario — doc.: `manual-free.txt:121-123` | **PARCIAL** - Editar os parâmetros da linha de comando de desinstalação do programa/`src/Tranal.Uninstaller/Core/ProgramColumns.cs:274` (`UninstallerLocationOf`) `src/Tranal.Uninstaller/Core/ProgramCustomization.cs:291` (`public static bool SetCommands`) **Nota**: existe edicao dos comandos antes/depois do programa, mas nao a edicao do `UninstallString` do programa-alvo. |

> O item 21 **não** é uma CLI do Ashampoo: ele edita os parâmetros do comando de
> desinstalação do **programa-alvo** (§5.4).

### 2.6.1 Funcionalidades descobertas na superfície do binário (não constavam no documento)

Tudo o que o binário **permite provar** está na superfície do **instalador**
(`ashampoo_uninstaller_pro_27.exe`): runtime do Inno Setup e manifesto. O
aplicativo em si (`UI27.exe`) está comprimido em LZMA dentro do instalador e
**não é acessível** — ver §8.1.

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 67 | Instalador Inno Setup 7.0.0.3 com mensagens em inglês | `ashampoo-instalador.strings.txt:3467` `Inno Setup Setup Data (7.0.0.3)`; `:3468` `Inno Setup Messages (6.5.0) (u)`; `:5517` `<description>Inno Setup</description>` | **OK** - O empacotamento ja e Inno Setup: `installer/tranal.iss:63` `#define AppName`, `:73` `AppVerName`, secoes `[Setup]`/`[Languages]`/`[Tasks]`/`[Icons]` |
| 68 | Instalador silencioso (`/SILENT`, `/VERYSILENT`), com log (`/LOG`), tarefas (`/TASKS`), senha (`/PASSWORD`), save/load de settings (`/LOADINF`, `/SAVEINF`) | `ashampoo-instalador.inventario.md` §"Rotulos de interface relevantes" (`Instructs Setup to be silent or very silent.`, `Same as /LOG, except it allows you to specify a fixed path/filename to use for the log file.`, `Specifies a list of tasks that should be initially selected.`, `Please specify the password using the /PASSWORD= command-line parameter.`, `Instructs Setup to load the settings from the specified file after having checked the command line, or to save settings to it.`) | **PARCIAL** - Instalador silencioso (`/SILENT`, `/VERYSILENT`), com log (`/LOG`), tarefas (`/TASKS`), senha (`/PASSWORD`), save/load de settings (`/LOADINF`, `/SAVEINF`)/`installer/tranal.iss` tem `SetupLogging=yes` e a tarefa `desktopicon` (switch `/LOG` e `/TASKS` do Inno Setup); **senha** nao e **save/load de settings** tambem nao |
| 69 | Registro do produto para desinstalação pelo Windows | `ashampoo-instalador.strings.txt:3023` `UninstallFilesDir`; `:3024` `UninstallDisplayName`; `:3025` `UninstallDisplayIcon`; `:3034` `CreateUninstallRegKey`; `:3035` `Uninstallable`; `:3088` `UninstallLogMode`; `:3097` `UninstallDisplaySize` | **PARCIAL** - Registro do produto para desinstalação pelo Windows/`installer/tranal.iss` grava as duas chaves pedidas (`UninstallFilesDir` e a chave `Uninstall`) — o `UninstallFilesDir` e gerado pelo Inno Setup, nao declarado |
| 70 | Detecção da versão do Windows suportada (Windows XP → Windows 11, Server 2003 → Server 2025) | `ashampoo-instalador.inventario.md` §"Rotulos de interface relevantes" (`Windows 10`, `Windows 11`, `Windows XP`, `Windows Vista`, `Windows Server 2025`, `Windows Server 2022`, …) | **PARCIAL** - Detecção da versão do Windows suportada (Windows XP → Windows 11, Server 2003 → Server 2025)/`src/Tranal.Uninstaller/Core/Appearance/SupportRequest.cs:18` (`public static class SupportRequest`) **Nota**: o instalador restringe a versao minima (`MinVersion=10.0` no `installer/tranal.iss`) e o pedido de suporte le a versao; nao ha a matriz de versoes suportadas. |
| 71 | Enumeração de dispositivos/drivers no instalador (SetupDi) | `ashampoo-instalador.inventario.md:409-410` → `**SetupDi** - Enumera dispositivos/Drivers` | **PARCIAL** - Enumeração de dispositivos/drivers no instalador (SetupDi)/`src/Tranal.Uninstaller/Core/Discovery/PrinterEnumerator.cs:74` (`public static class PrinterEnumerator`) **Nota**: a enumeracao de drivers existe, mas via `pnputil` e nao pela SetupDi — a busca por "SetupDi" em `src/` nao retorna hits. |
| 72 | Acesso HTTP do instalador (WinHttp) | `ashampoo-instalador.inventario.md:409-411` → `**WinHttp** - Acesso HTTP/FTP` | **PARCIAL** - Acesso HTTP do instalador (WinHttp)/`src/Tranal.Uninstaller/Core/Updates/AutoUpdateClient.cs:56` (`public sealed class AutoUpdateClient`) **Nota**: o Tranal fala HTTP com o servidor de atualizacoes; o instalador em si nao faz acesso HTTP. |
| 73 | Instalador protegido por senha (criptografia ChaCha20 do setup) | `ashampoo-instalador.inventario.md:9` → classe RTTI `TChaCha20Context`; `ashampoo-instalador.strings.txt` → `TSetupEncryptionKey`, `The password you specified is not correct. Please try again.` | **AUSENTE** - Instalador protegido por senha (criptografia ChaCha20 do setup)/grep "Password" em `installer/tranal.iss` -> 0 hits; o Inno Setup do Tranal nao define `Password=` nem `Encryption=` |
| 74 | Fechamento automático de aplicações que travam a instalação (e reinício delas) | `ashampoo-instalador.inventario.md` §"Rotulos de interface relevantes" (`Instructs Setup to attempt closing applications using files that need to be updated, or prevents it from doing so.`, `Instructs Setup to force close when closing applications, or prevents it from doing so.`, `Instructs Setup to attempt restarting applications, or prevents it from doing so.`) | **PARCIAL** - Fechamento automático de aplicações que travam a instalação (e reinício delas)/`installer/tranal.iss` tem `CloseApplications=yes`, mas `RestartApplications=no` — o instalador fecha e nao reinicia. |
| 75 | Manifesto que redireciona DLLs de sistema (`comctl32`, `mpr`, `netapi32`, `netutils`, `textshaping`, `version`) | `ashampoo-instalador.strings.txt:66-71` → `file name="comctl32.dll" loadFrom="%SystemRoot%\system32\" /` e equivalentes | **AUSENTE** - Manifesto que redireciona DLLs de sistema (`comctl32`, `mpr`, `netapi32`, `netutils`, `textshaping`, `version`)/grep "comctl32" e "loadFrom" em `installer/tranal.iss` -> 0 hits |
| 76 | Compilador Delphi (Embarcadero) do instalador e detecção de sistema | `ashampoo-instalador.inventario.md` → `Embarcadero Delphi for Win32 compiler version 36.0 (29.0.55362.2017)`; `ashampoo-instalador.identificadores.txt:22` → `Winapi.Windows.PkgHelper` | **FORA POR DECISAO** - Compilador Delphi (Embarcadero) do instalador e detecção de sistema/decisao registrada do dono: o Tranal e C#/.NET/WPF com instalador Inno Setup — a escolha de stack e do produto, nao uma lacuna funcional |

### 2.7 Delta FREE × PRO — as 28 capacidades exclusivas do PRO 27

Capacidades que **só existem no PRO 27** (não documentadas no manual do FREE):

1. Monitor de instalação com ícone na bandeja e acionamento manual.
2. **Análise forense** — criar log de desinstalação para instalações já
   existentes ("Create post-install log"), inclusive em lote.
3. Criar **log de instalação a partir do diff entre dois snapshots**.
4. Módulo **Snapshots**: criar, nomear, listar, apagar, comparar, "2 snapshots
   now", Wizard, relatório de diferenças.
5. Configuração de snapshot (drives incluídos, pastas excluídas).
6. **Software Updater** (atualizar programas instalados, em lote, com lista negra).
7. **Crash Analyzer** (análise de relatórios de falha do Windows Event Log).
8. **Unlocker** (desbloquear arquivos em uso / encerrar processo / desregistrar
   DLL / reiniciar Explorer).
9. **Undeleter** (recuperação de arquivos).
10. **Privacy Traces Cleaner** + limpeza de jump lists.
11. **Hibernation** (colocar programa para dormir).
12. **Impact** (ranking de programas por impacto em performance).
13. **Move** de programa e **Revert relocation**.
14. Módulo **Boot Center** (tempo de boot + Autorun + Tarefas agendadas com
    enable/disable).
15. Listagem e remoção de **plugins de navegador**.
16. **Export/Import de lista de programas** e exclusão de entradas importadas.
17. **Edit installation log** (edição manual de entradas do log).
18. **Drag & Drop UnInstall** (atalho de área de trabalho).
19. **Registry Optimizer 2** com **Super Safe Mode**.
20. Exportar logs de desinstalação em **HTML, CSV e TXT**.
21. Comparação de tamanho declarado vs. real + sincronização com o Windows.
22. Categoria automática expandida + reset para "Unsorted".
23. País do editor com bandeira.
24. Módulo **Windows Tools** consolidado (Drive Cleaner nomeado, Safe cookies,
    painel de Boot Center).
25. Remoção de add-ons e toolbars em um clique.
26. Detecção de programas instalados mais rápida + análise de registro
    pós-instalação mais precisa.
27. Comparação de snapshots acelerada.
28. Instal guard (configuração de como setups são detectados).

Observação de método: a comparação automática de títulos está em
`evidencia/manual-delta-free-vs-pro27.txt` (FREE: 88 títulos, PRO: 144,
comuns: 96, só no PRO: 48). Esse arquivo é **derivado de renomeações
incluídas** — por exemplo `StartUp Tuner` (FREE) virou `Boot Center > Autorun`
(PRO) —, por isso a lista acima foi filtrada manualmente para separar renomeação
de capacidade nova.

### 2.8 Alegações do owner — verificadas

| Alegação | Veredito | Evidência |
|---|---|---|
| "monitoramento avançado" | **Confirmado** | `manual-pro27.txt:13-14` (ícone na bandeja, trigger manual), `site-produto-uninstaller.html:5226` ("thoroughly monitors and logs every installation"), `5515` (distingue tipos de instalador, separa atualização de instalação) — **Documentado** |
| "análise forense" | **Confirmado — é o nome oficial do recurso** | `site-produto-uninstaller.html:5399` ("Forensic analysis: Create uninstall logs for existing installations"), `5428-5430`; `manual-pro27.txt:43-46` ("Create post-install log") — **Documentado** |
| "snapshots" | **Confirmado** | `manual-pro27.txt:246-288` (módulo completo com comparação), `site-produto-uninstaller.html:5226` ("the snapshot feature tracks and shows every modification made to your system between two points in time") — **Documentado** |
| "solução mais complexa" | **Confirmado em extensão** | 66 funcionalidades inventariadas; 28 exclusivas do PRO; 5 módulos de limpeza/recuperação (Drive Cleaner, Registry Optimizer, Internet Cleaner, File Wiper, Undeleter) + Unlocker + Crash Analyzer + Privacy Traces + Boot Center + Snapshots — **Documentado** |
| "remoção de sobras" | **Confirmado** | `manual-free.txt:191-193` (in-depth cleaning), `site-produto-uninstaller.html:5226` (five-stage uninstall procedure) — **Documentado** |
| "log de instalação" | **Confirmado** | `manual-free.txt:5`, `manual-pro27.txt:6` — **Documentado** |
| "desinstalação silenciosa" | **Não verificável** | não aparece no manual FREE nem no PRO, nem no site; o manual só documenta "Edit uninstall command line parameters" (que edita o comando do *programa-alvo*) — **Não verificado** (§8) |
| "backup antes de apagar" | **Parcial** | existe o módulo **Backups** (`manual-free.txt:463-509`), mas ele está documentado como backup do **Registry Optimizer**, não como backup de arquivos do programa antes da remoção — **Documentado** |
| "filtro por data/tamanho" | **Parcial** | filtro **por tamanho** documentado (`manual-free.txt:60`); filtro **por data** não documentado — **Documentado** |
| "agendamento" | **Não verificável** | não há agendamento de desinstalação no manual; o PRO tem Boot Center com **Tarefas agendadas do Windows** (habilitar/desabilitar/excluir), que é gestão de tarefas existentes, não agendamento próprio — `manual-pro27.txt:333-343` — **Documentado** |

---

## 3. Mecanismos notáveis

### 3.1 Imports do PE (medido com REA `inspect_pe_imports`, 2026-11-05)

Evidência completa: `evidencia/pea-imports-instaladores.txt`.

**Instalador FREE (stub)** — PE32+ x64, 11 DLLs, 14 imports, sem delay-load:

```
advapi32 : RegCloseKey            comctl32 : ImageList_Add
comdlg32 : GetOpenFileNameW       gdi32    : Arc
KERNEL32 : LoadLibraryA, ExitProcess, GetProcAddress, VirtualProtect
ole32    : CoCreateGuid           oleaut32 : VariantCopy
shell32  : DragFinish             shlwapi  : PathFindExtensionA
user32   : GetDC                  version  : VerQueryValueA
imphash (pefile) : md5 c6665f8c245c8059cfa11faa4145781c
```

14 imports para uma janela de setup completa só podem ser um **stub/loader**:
`LoadLibraryA` + `GetProcAddress` + `VirtualProtect` são o trio clássico de
descompactação e redirecionamento de execução. `GetOpenFileNameW` (botão
"Explorar") e `DragFinish` (arrastar e soltar) explicam a única tela de setup
observada. **Observado.**

**Instalador PRO** — PE32 i386, 5 DLLs normais + 3 delay-load, 150 imports + 3 delay:

```
kernel32 (127) : CreateProcessW, GetExitCodeProcess, LoadLibraryA/W/ExW, GetProcAddress,
                 GetCommandLineW, AdjustTokenPrivileges, ExitWindowsEx,
                 GetVolumeInformationW, GetDriveTypeW, GetDiskFreeSpaceW,
                 FindFirstFileW, CreateDirectoryW, ...
comctl32 (1)   : InitCommonControls
user32   (18)  : CreateWindowExW, MessageBoxW, LoadStringW, ExitWindowsEx,
                 MsgWaitForMultipleObjects, TranslateMessage, DispatchMessageW, ...
oleaut32 (15)  : SafeArray* (8), VariantInit/Clear/Copy/ChangeType, Sys*String
advapi32 (15)  : ConvertStringSecurityDescriptorToSecurityDescriptorW, OpenProcessToken,
                 OpenThreadToken, AdjustTokenPrivileges, LookupPrivilegeValueW,
                 GetTokenInformation, AllocateAndInitializeSid, EqualSid, FreeSid,
                 ConvertSidToStringSidW, RegOpenKeyExW, RegQueryValueExW, RegCloseKey
delay: kernel32!GetLogicalProcessorInformation, user32!MessageBoxA, kernel32!GetNativeSystemInfo
imphash (pefile) : md5 88016fcdef7f227c62171d0afad9aae4
```

### 3.2 O que a tabela de imports prova — e o que ela não prova

| Mecanismo | Como se observa | Situação |
|---|---|---|
| Instalador **elevado** (pede privilégio) | `AdjustTokenPrivileges` + `LookupPrivilegeValueW` + `OpenProcessToken`/`OpenThreadToken` + `ConvertStringSecurityDescriptorToSecurityDescriptorW` | **Observado** (PRO) |
| Reinício do Windows possível ao final da instalação | `ExitWindowsEx` | **Observado** (PRO) |
| O instalador **lê a linha de comando** | `GetCommandLineW` | **Observado** (PRO) — o stub FREE **não** importa |
| Execução de processo filho (payload / Inno Setup) | `CreateProcessW` + `GetExitCodeProcess` | **Observado** (PRO) |
| Leitura do registro do sistema | `RegOpenKeyExW`, `RegQueryValueExW` | **Observado** (PRO) |
| Enumeração de disco/unidades | `GetVolumeInformationW`, `GetDriveTypeW`, `GetDiskFreeSpaceW`, `FindFirstFileW` | **Observado** (PRO) |
| Resolução dinâmica de DLLs em runtime | `LoadLibraryA/W/ExW` + `GetProcAddress` | **Observado** (PRO) |
| **`shell32.dll` NÃO está na tabela de imports do PRO** | ⇒ qualquer uso de shell extension / COM de shell é feito por `LoadLibrary` em runtime e **não é observável por PE** | **Não verificado** por PE |
| **WMI** | `ole32.dll` ausente na tabela de imports do PRO e `WbemLocator` **ausente nos dois binários** (varredura de bytes, 05/11/2026) | **Não verificado** para o app; **improvável** no instalador |
| **`MoveFileEx`** | varredura de bytes nos dois instaladores (`MoveFileEx`, `MoveFileExW`, `MoveFileExA`): **nenhuma ocorrência** | **Ausente** nos dois instaladores (medido) |
| Driver de kernel, serviço do Windows, extensão COM de shell | — | **Não verificado**: o app instalado não foi extraído nem executado (§8) |

Sobre a ausência de driver/serviço: `running_in_tray: false` no catálogo
(`evidencia/ash-api-uninstaller-products.json`) indica que o app **não fica em
bandeja por padrão**; o ícone de bandeja documentado no manual aparece apenas
quando o monitoramento é ligado (`manual-pro27.txt:14`). Nada foi observado
quanto a driver, serviço ou extensão de shell — e nada foi **negado**: o
payload do PRO não pôde ser extraído (§8).

### 3.3 Empacotamento

| Artefato | Observação | Fonte |
|---|---|---|
| FREE | Seções `UPX0`/`UPX1`/`.rsrc` → **UPX-packed**; overlay de 12.192 bytes = **somente o certificado Authenticode**, sem payload embutido (o instalador baixa o produto real) | `pe-installer-free.txt` |
| PRO | Wrapper Ashampoo (`.itext`, `.didata`) + overlay de 47.966.264 bytes com **Inno Setup 7.0.0.3 / LZMA** | `pe-installer-pro.txt`, `pea-imports-instaladores.txt` |
| Assinatura | Authenticode válido, GlobalSign EV, validade até 2027-06-24 | `pe-installer-free.txt` |

### 3.4 Snapshot de disco: o que se sabe e o que não se sabe

O manual só permite escolher **quais drives** entram no snapshot e **quais pastas
excluir** (`manual-pro27.txt:492-493`). Isso é consistente com snapshot de
arquivos/registro por drive, **não** com imagem de disco — mas o **nível exato
não é verificável pelo manual**: marcado como **Documentado (parcial)**.

### 3.5 Onde o produto guarda estado (documentado)

| Item | Caminho | Fonte |
|---|---|---|
| Pastas de log e registros de instalação | `C:\Users\Public\Documents\Ashampoo\Ashampoo UnInstaller\`, `\logs`, `\logs_raw` | `manual-logfolders.txt:5-8` |
| Instalação via Ashampoo App | `C:\Program Files\Ashampoo\Ashampoo App` + `products\Ashampoo UnInstaller 15` | `manual-troubleshooting.txt:13-15` |
| Instalação direta (v15) | `C:\Program Files (x86)\Ashampoo\Ashampoo UnInstaller 15` | `manual-troubleshooting.txt:11` |
| Executáveis | `UIFREE.exe` (FREE), `UI27.exe` (PRO), `UI15.exe` + `UI15Guard.exe` (v15) | `ash-api-uninstaller-products.json`, `manual-troubleshooting.txt:9` |

### 3.6 O "Ashampoo App" como modelo de distribuição

É a peça mais transferível da auditoria. O instalador FREE **não embute o
produto**: ele é um stub UPX de 1,6 MB cujo overlay tem só o certificado, e o
programa é baixado pelo **"Ashampoo App"**, um contêiner instalado em
`C:\Program Files\Ashampoo\Ashampoo App`, com cada produto em `products\`
(`manual-troubleshooting.txt:13-15`). A tela medida do instalador pede
exatamente esse diretório (§4.1).

O modelo é: **instalador pequeno e assinado → contêiner único → produtos
baixados depois**. Para o Tranal isso é uma decisão de produto (§9-h), não uma
técnica de desinstalação.

---

## 4. Interface

### 4.1 Interface observada (instalador FREE)

Capturas: `evidencia/installer-free-01-20261105-194145.png`,
`installer-free-02-20261105-194226.png`, `tmp-state-20261105-194815.png`. Árvore
UIA: `evidencia/uia-installer-free.txt` e `uia-installer-free-2.txt`. Registro
da condução do wizard: `evidencia/installer-wizard-log.txt`.

Janela única, classe `Window`, título **"Ashampoo UnInstaller FREE"** (UI em
português):

```
Pane  name='Instalação'                          class='Static'  rect=[1211,524,435,40]
Pane  name='Escolha aqui a localização de armazenamento:' class='Static' rect=[1211,645,435,17]
Pane  name='C:\Program Files\Ashampoo\Ashampoo App' class='Edit' rect=[1211,672,350,25]
Pane  name='Explorar'                            class='Button' rect=[1571,672,75,25]
Pane  name='Seguinte'                            class='Button' rect=[1541,878,105,26]
Pane  name='Cancelar'                            class='Button' rect=[1428,878,105,26]
```

O instalador pede o **diretório de armazenamento do "Ashampoo App"**, com default
`C:\Program Files\Ashampoo\Ashampoo App` — **Observado**
(`installer-wizard-log.txt`, `uia-installer-free.txt`).

### 4.2 Interface documentada (aplicativo) — **inteiramente documental**

- Barra lateral de navegação com atalhos para todos os módulos + tela
  "Overview" com tiles de status ("Feature quick access") e lista "History" das
  instalações recentes (`manual-free.txt:19-35`).
- Estrutura comum dos módulos, em 4 faixas: **(1)** resumo das entradas
  selecionadas + filtro e busca no topo; **(2)** entradas e detalhes no centro;
  **(3)** ações aplicáveis na base; **(4)** "More functions" (mais ações)
  (`manual-pro27.txt:22-31`).
- "Program module structure" idêntico no FREE e no PRO
  (`manual-free.txt:40-49`).
- Estrelas de rating azuis/amarelas conforme a base online
  (`manual-free.txt:51-53`).
- Atalhos de teclado: **não verificado** (nenhuma captura da interface do app).

> **Limitação de medição registrada (Observado).** A janela do instalador abre
> **atrás** das outras; `SetForegroundWindow`/`AppActivate`/`SetWindowPos`
> retornam `ERROR_ACCESS_DENIED` para **todas** as janelas do desktop; o
> instalador **elevado** bloqueia o UIPI, então o clique não chega. Foram
> tentados clique por coordenada real via `GetWindowRect`, `WM_COMMAND` e
> `LegacyIAccessible` — sem efeito por causa da elevação. O log do wizard
> (`installer-wizard-log.txt`) mostra **10 passes** em que o botão "Seguinte" foi
> acionado sem mudança de estado: **a instalação não avançou**.
> → **A interface do aplicativo (`UIFREE.exe`/`UI27.exe`) NÃO foi observada.**
> Toda a §4.2 acima é documental, e nenhuma captura de tela do app existe.

---

## 5. Linha de comando

**Regra desta seção: switch do *instalador* não é switch do *aplicativo*.** Os
dois são listados separadamente e nada foi inventado.

### 5.1 Instalador FREE (stub `ashampoo_uninstaller_free_sm.exe`)

| Switch | Situação | Base |
|---|---|---|
| qualquer switch | **Não verificado** | o stub tem apenas 14 imports e **não importa `GetCommandLineW`** (`pea-imports-instaladores.txt`); o binário está UPX-packed e a varredura de strings (`evidencia/strings-installer-sm.txt`) não expõe nenhum switch. Sem documentação pública de switches para o instalador FREE |

### 5.2 Instalador PRO (`ashampoo_uninstaller_pro_27.exe`)

| Switch | Situação | Base |
|---|---|---|
| qualquer switch | **Não verificado** | o wrapper **importa `GetCommandLineW`** (`pea-imports-instaladores.txt`), ou seja, ele lê a linha de comando, mas **nenhum switch é documentado publicamente** e o payload Inno Setup 7.0.0.3 não pôde ser extraído para ler os switches do instalador Inno (`/SILENT`, `/VERYSILENT`, `/DIR` etc. são switches do Inno Setup, não do Ashampoo) |

### 5.3 Aplicativo (`UIFREE.exe` / `UI27.exe`)

| Switch | Situação | Base |
|---|---|---|
| qualquer switch | **Não verificado** | nenhum switch do aplicativo está documentado no manual FREE (`manual-free.txt`), no manual Pro 27 (`manual-pro27.txt`) ou no site oficial (`site-produto-uninstaller.html`). O app não pôde ser executado (§8) |

### 5.4 O que **não** é um switch do Ashampoo (armadilha de leitura)

O manual documenta "**Edit uninstall command line parameters**"
(`manual-free.txt:121-123`, `manual-pro27.txt:76-78`). Isso **edita os parâmetros
do comando de desinstalação do programa-alvo** — é um recurso do log, não uma
opção de linha de comando do Ashampoo. Por isso nenhum `/S` ou `/silent` foi
atribuído ao produto: **não há CLI verificável**.

---

## 6. O que o Tranal NÃO faz hoje

Base: greps reais em `E:\Projetos\tranal-uninstaller\src\` (165 arquivos `.cs`).
Cada linha "Onde no Tranal" aponta para `arquivo:linha` já visto no grep.
Legenda: ✅ existe e cobre · 🟡 existe parcialmente · ⬜ não existe · ⟡ não
verificado por falta de equivalente claro.

| Recurso do Ashampoo | Tranal | Onde no Tranal (arquivo) |
|---|---|---|
| Monitor de instalação com log detalhado | ✅ | `Core/Modules/InstallMonitor.cs:28` (`InstallMonitor`) — captura periódica + diff em `:201-207` |
| Log por programa (arquivos, pastas, chaves de registro) | ✅ | `Core/Modules/TraceStore.cs:68` (`TraceStore`); payload `Files`/`Folders`/`RegistryKeys` em `:496-520` |
| **Log de instalação a partir do diff entre dois snapshots** | ✅ | `Core/Modules/TracedPrograms.cs:44-56` — fotografia ANTES → executa o instalador → fotografia DEPOIS → `SystemSnapshot.Diff` → filtro de plausibilidade → `TraceStore.Save` |
| **Análise forense**: criar log de desinstalação para programa **já instalado** | ⬜ | nenhum match para `post-install`/`forense`/`forensic`/`retrospective` — o único hit é um comentário de `Core/Tools/UnrecoverableDelete.cs:39` ("ferramentas de forense" de disco). Não existe fotografia do "antes" de uma instalação já concluída |
| Snapshots **nomeados, listáveis e apagáveis** (biblioteca de snapshots) | 🟡 | o motor existe (`Core/Modules/SystemSnapshot.cs:136` `Capture`, `:180` `Diff`), mas a fotografia "só vive durante a operação" (`Core/Modules/TracedPrograms.cs:43`) — sem snapshots persistidos, sem nome, sem comparação sob demanda |
| Comparar dois snapshots | ✅ | `Core/Modules/SystemSnapshot.cs:180` → `SnapshotDiff` (`:557`): devolve `NewFiles`, `NewFolders`, `NewRegistryKeys` — **só o que nasceu**, não o que mudou |
| Limpeza de sobras com **motivo por item** | ✅ | `Core/UninstallSupport.cs:389` (`LeftoverScanner`), `enum LeftoverReason` em `:46` |
| Desinstalação silenciosa por família | ✅ | `Core/QuietUninstall.cs:26` (`QuietUninstall`); classificação em `Core/UninstallerKind.cs` |
| Backup antes de apagar (registro + script de restauração) | ✅ | `Core/BackupManager.cs`, `Core/FullRegistryBackup.cs` |
| Gerenciador de serviços (tipo de inicialização, iniciar/parar, dependências) | 🟡 | `Core/Tools/WindowsToolsCatalog.cs:85` — **launcher de `services.msc`**; sem `ServiceController`/`StartType` no código |
| Tarefas agendadas (habilitar/desabilitar/excluir) | 🟡 | `Core/Tools/WindowsToolsCatalog.cs:78` — **launcher de `taskschd.msc`**; sem `TaskScheduler` |
| Autorun / StartUp Tuner | ✅ | `Core/Tools/AutorunManager.cs` |
| Plugins de navegador | ✅ | `Core/Modules/BrowserExtensions.cs` |
| Remoção de vestígios de uso (jump lists, prefetch, shellbags) | ✅ | `Core/Tools/EvidenceRemover.cs:72,182` (`ScanJumpLists`), `Core/Tools/TrackCleaner.cs` |
| Otimizador de registro | ✅ | `Core/Tools/RegistryOptimizer.cs` |
| Limpeza de disco / arquivos temporários | ✅ | `Core/Tools/JunkCleaner.cs` |
| Internet Cleaner (histórico, cookies, rastros) | ✅ | `Core/Tools/HistoryCleaner.cs`, `Core/Tools/TrackCleaner.cs` |
| File Wiper (exclusão irrecuperável) | ✅ | `Core/Tools/UnrecoverableDelete.cs` |
| Windows-Apps / system apps | ✅ | `Core/Modules/WindowsApps.cs` |
| Exportar lista de programas | ✅ | `Core/Exporters.cs` — `ToText`/`ToCsv`/`ToHtml`/`ToXml` (`Ui/MainWindow.xaml.cs:1169-1172`) |
| Importar lista exportada (marcar não instalados) | ⬜ | nenhum importador de lista em `src/` — `TraceStore.Import` (`Core/Modules/TraceStore.cs:222`) importa um **log do Tranal**, não uma lista de programas |
| Editar manualmente o log de instalação | ⬜ | `TraceStore` grava (`:143`), importa (`:222`), exporta (`:279`) e apaga o log inteiro (`:191`); **nenhum** método de edição de entrada |
| Exportar alterações de registro do log para `.reg` | ⬜ | `TraceStore.Export` (`Core/Modules/TraceStore.cs:279`) grava **JSON** (`Tranal.TraceLog.v1`, `:71`); não há exportador `.reg` de log |
| Exportar logs de desinstalação em HTML/CSV/TXT | 🟡 | a **lista** sai em CSV/TXT/HTML/XML (`Core/Exporters.cs`); o **log de instalação** sai só em JSON (`Core/Modules/TraceStore.cs:279`) |
| Remover entrada da lista de programas sem desinstalar | 🟡 | `Core/ProgramCustomization.cs`, `Core/ExcludeList.cs` — o equivalente exato do Ashampoo **não foi verificado** |
| Filtro por tamanho na lista | ⟡ | filtro por texto existe; filtro por tamanho/ano **não foi verificado** por grep |
| Remover add-ons e toolbars em um clique | ⟡ | `Core/Modules/BrowserExtensions.cs` cobre plugins; o caso "toolbar" **não foi verificado** |
| "Drag & Drop UnInstall" (atalho de área de trabalho) | 🟡 | há `AllowDrop`/`Drop` em `Ui/Modules/HunterView.xaml:5` (arrastar arquivo → `Resolve`, resolve o programa para a tela de sobras) e em `Ui/Tools/TopmostOverlayWindow.xaml:10,53` (arrastar arquivo → `SetTargetFromFiles`, `Ui/Tools/TopmostOverlayWindow.xaml.cs:88`) — mas **não** é o atalho de área de trabalho "Drag & Drop UnInstall" do Ashampoo |
| Software Updater de programas de terceiros | ⬜ | `Core/Updates/AutoUpdateClient.cs` é auto-atualização **do próprio Tranal** |
| Crash Analyzer (Event Log) | ⬜ | nenhum match para `crash`/`CrashAnalyzer` |
| Unlocker (desbloquear arquivo em uso) | ⬜ | nenhum match para `unlock`/`desbloque`; `Core/ProcessTerminator.cs:61` encerra processo, mas não desbloqueia arquivo |
| Undeleter (recuperar arquivo apagado) | ⬜ | nenhum match para `undelet` |
| File Associator | ⬜ | nenhum match para `FileAssociat`/`associat` |
| Hibernation (dormir programa) | ⬜ | nenhum match para `hibernat` |
| Impact (ranking de performance) | ⬜ | nenhum match para `Impact` (o único hit é "Ope**rating**System") |
| Ratings por estrelas com base online | ⬜ | nenhum match para `estrelas`/`avaliacao`; `Core/WebSearch.cs` é busca de programa na web, não avaliação |
| Move / Revert relocation | ⬜ | nenhum match para `reloc` |
| Agendamento de desinstalação | ⬜ | nenhum mecanismo de agendamento próprio no `src/` |
| Desinstalar o próprio produto pelo "Apps & Features" | ⟡ | não verificado |

### 6.1 Onde o Tranal já é melhor

- **Atribuição pela cadeia de desinstalação**: o Tranal lê o **pacote `.msi`**
  em cache (`Core/MsiInstallLog.cs`, `MsiInstallLogReader` em `:63`) e sabe que o
  item **é** do programa — **149 de 149** pacotes MSI desta máquina
  (`NOVIDADES.md`, `docs/PARIDADE.md`). O Ashampoo depende do nome e do log do
  próprio instalador, que só existe se o monitoramento estava ligado.
- **Confiança com motivo**: cada sobra tem um **motivo** (`LeftoverReason`,
  `Core/UninstallSupport.cs:46`), não só um negrito.
- **Backup do registro com script de restauração** antes de apagar
  (`Core/BackupManager.cs`, `Core/FullRegistryBackup.cs`); o Ashampoo só
  documenta backup do *Registry Optimizer*, não do conteúdo removido.
- **CLI roteirizável** (`/scan`, `/mu`, `/export`, `--uninstall-target` —
  `src/Tranal.Cli/Program.cs:19-20,54,60,66`) — o Ashampoo não expõe nenhuma
  CLI verificável (§5).
- **Desinstalação silenciosa por família**, com a classificação do desinstalador
  em `Core/UninstallerKind.cs` e a execução em `Core/QuietUninstall.cs`; o
  Ashampoo apenas avisa que um programa "não pode ser desinstalado no modo
  silencioso" (`manual-free.txt:33-35`).
- **Segurança do motor**: varredura segura de caminhos (`Core/SafeTraversal.cs`),
  exclusão diferida (`Core/DeferredDelete.cs`) e lixeira (`Core/RecycleBin.cs`).
- **Privacidade**: sem cadastro, sem telemetria. O Ashampoo, em troca, tem base
  online de ratings, busca online de programa, Software Updater e Crash
  Analyzer com busca online — todos recursos de rede que o Tranal não tem e não
  quer.
- **Instalador próprio assinado**, com script de build versionado
  (`installer/tranal.iss`) e certificado versionado
  (`installer/certificado/TranalUninstallerPro.cer`).

---

## 7. Fichos de evidência

### 7.1 Downloads

| Ficheiro | Bytes | SHA-256 | Conteúdo |
|---|---|---|---|
| `downloads/ashampoo_uninstaller_free_sm.exe` | 1.676.704 | `b06f9899da6e7eba4ede16ab0d04439a693106c29920e66dee5a0b55c5029f62` | PE32+ x64, UPX, stub do instalador FREE |
| `downloads/ashampoo_uninstaller_pro_27.exe` | 49.098.296 | `08a36652800b6b608041d6a445c38079c66239b2751fa4041e07132b9dc9184d` | PE32 i386, wrapper Ashampoo + Inno Setup 7.0.0.3 no overlay |
| `downloads/ashampoo-uninstaller-free.bin` | 285.018 | — | HTML do link intermediário (falso download) |
| `downloads/padfile-uninstaller.xml` | 82.499 | — | metadados PAD do PRO |
| `downloads/padfile-uninstaller-free.xml` | 0 | — | download vazio (não usado) |

### 7.2 Extraídos / derivados

| Ficheiro | Bytes | Conteúdo |
|---|---|---|
| `app/free-instalador-secao-rsrc.bin` | 311.808 | seção `.rsrc` do stub FREE (recursos `BTN_*`) |
| `app/pro-instalador-bloco-inno-setup-0.bin` | 1.200 | bloco do header Inno Setup extraído do PRO |

### 7.3 Evidência

| Ficheiro | Bytes | Conteúdo |
|---|---|---|
| `evidencia/RESUMO.md` | 45.106 | inventário bruto desta auditoria (8 seções, 66 funcionalidades) |
| `evidencia/pea-imports-instaladores.txt` | 3.539 | imports dos dois PEs (REA, 2026-11-05) |
| `evidencia/pe-installer-free.txt` | 1.270 | cabeçalho/seções/versões/certificado do stub FREE |
| `evidencia/pe-installer-pro.txt` | 2.375 | cabeçalho/seções/versões do instalador PRO |
| `evidencia/pe-scan-pro.txt` | 148 | varredura de PEs embutidos no PRO (1 PE, offset 0) |
| `evidencia/strings-installer-sm.txt` | 68.530 | strings do stub FREE (sem switches) |
| `evidencia/ash-api-uninstaller-products.json` | 7.002 | API oficial de produtos (FREE e PRO) |
| `evidencia/ash-api-products.json` | 703.018 | API oficial de produtos (catálogo completo) |
| `evidencia/ash-ua.js` | 5.426 | script do site coletado na auditoria |
| `evidencia/site-produto-uninstaller.html` | 562.264 | página oficial do Pro 27 (marketing + release notes) |
| `evidencia/site-free-downloads.html` | 939.145 | página de downloads grátis (catálogo FREE, idstring 2203) |
| `evidencia/site-dld-2203.html` | 285.018 | página HTML do link intermediário (falso download) |
| `evidencia/manual-free.txt` | 12.796 | manual oficial FREE (índice integral) |
| `evidencia/manual-pro27.txt` | 23.922 | manual oficial Pro 27 (índice integral) |
| `evidencia/manual-v10.txt` | 19.853 | manual da versão 10 |
| `evidencia/manual-v14.txt` | 22.662 | manual da versão 14 |
| `evidencia/manual-v15.txt` | 22.964 | manual da versão 15 |
| `evidencia/manual-v16.txt` | 24.526 | manual da versão 16 |
| `evidencia/manual-16-full.htm` | 25.481.516 | manual completo em HTML (v16) |
| `evidencia/manual-indice.txt` | 18.152 | índice comparativo dos 6 manuais |
| `evidencia/manual-delta-free-vs-pro27.txt` | 3.340 | delta automático de títulos FREE × PRO 27 |
| `evidencia/manual-troubleshooting.txt` | 1.454 | detecção de instaladores, `UIxxGuard.exe`, exceções de AV |
| `evidencia/manual-logfolders.txt` | 887 | pastas de log esperadas |
| `evidencia/manual-no-leftovers-tutorial.txt` | 262 | tutorial "No more leftovers" (vazio de texto) |
| `evidencia/api-manual-free.json` | 15.872 | Zendesk API do manual FREE (bruto) |
| `evidencia/api-manual-pro27.json` | 39.709 | Zendesk API do manual Pro 27 (bruto) |
| `evidencia/api-manual-troubleshooting.json` | 2.562 | Zendesk API do troubleshooting (bruto) |
| `evidencia/api-manual-logfolders.json` | 1.780 | Zendesk API dos log folders (bruto) |
| `evidencia/api-manual-no-leftovers-tutorial.json` | 1.462 | Zendesk API do tutorial (bruto) |
| `evidencia/api-manual-v10.json` | 26.051 | Zendesk API do manual v10 (bruto) |
| `evidencia/api-manual-v14.json` | 34.980 | Zendesk API do manual v14 (bruto) |
| `evidencia/api-manual-v15.json` | 35.331 | Zendesk API do manual v15 (bruto) |
| `evidencia/api-manual-v16.json` | 39.443 | Zendesk API do manual v16 (bruto) |
| `evidencia/installer-wizard-log.txt` | 4.663 | condução do wizard do instalador FREE (10 passes) |
| `evidencia/uia-installer-free.txt` | 1.368 | árvore UIA do instalador FREE |
| `evidencia/uia-installer-free-2.txt` | 1.368 | segunda árvore UIA do instalador FREE |
| `evidencia/installer-free-01-20261105-194145.png` | 1.897.679 | captura do instalador FREE |
| `evidencia/installer-free-02-20261105-194226.png` | 28.223 | captura do instalador FREE |
| `evidencia/tmp-state-20261105-194815.png` | 1.571.808 | captura de estado da tela |
| `evidencia/tmp-focus-20261105-194538.png` | 30.458 | captura de foco |
| `evidencia/tmp-focus2-20261105-194545.png` | 30.458 | captura de foco (2ª tentativa) |

### 7.4 Ferramentas de captura reusáveis (já funcionam)

`_tools/drive-wizard.ps1` (6.002 B), `_tools/win-act.ps1` (5.637 B),
`_tools/focus-shot.ps1` (4.314 B), `_tools/uia-act.ps1` (4.300 B),
`_tools/uia-dump.ps1` (1.838 B), `_tools/rsrc-dump.ps1` (3.626 B),
`_tools/extract-res.ps1` (3.710 B), `_tools/scan-pe.ps1` (2.178 B).

---

## 8. Lacunas desta auditoria

1. **Aplicativo não instalado e não executado.** O instalador FREE abre
   **elevado** e atrás de outras janelas; `SetForegroundWindow`/`AppActivate`/
   `SetWindowPos` retornam `ERROR_ACCESS_DENIED` para todas as janelas do
   desktop e o UIPI bloqueia o clique. Já foram tentados clique por coordenada
   real (`GetWindowRect`), `WM_COMMAND` e `LegacyIAccessible` — sem efeito
   (`installer-wizard-log.txt`, 10 passes sem mudança de estado).
   **Consequência:** a interface do `UIFREE.exe`/`UI27.exe`, as strings de tela,
   a árvore UIA real e o comportamento em execução do monitor de instalação
   **não foram observados**. A §4.2 é toda documental.
2. **Payload do PRO não extraído.** `innoextract` 1.9 (máx. 6.0.5) falha com
   "Unexpected setup loader revision: 2" porque o instalador é **Inno Setup
   7.0.0.3**. Nenhuma ferramenta nova foi instalada (sem autorização do owner).
   **Consequência:** os executáveis reais (`UI27.exe` e companhia), seus
   imports, recursos, extensões de shell e serviços **não foram medidos**.
3. **Extensões de shell / driver / serviço do Windows** — **não verificado**. O
   wrapper do instalador PRO não importa `shell32.dll`, e o app não foi
   extraído; logo, registro de `ContextMenuHandlers`,
   `ShellIconOverlayIdentifiers` e CLSID de driver **não puderam ser conferidos
   nem negated**.
4. **Uso de WMI** — **não verificado** para o aplicativo (nenhum import de
   `ole32.dll` no instalador e `WbemLocator` ausente nos dois binários; o app
   não foi analisado).
5. **Switches de linha de comando** — **não verificados** para instalador FREE,
   instalador PRO e aplicativo. Nenhum switch é documentado publicamente; o stub
   FREE está UPX-packed e não expõe `GetCommandLineW` na tabela de imports; o
   PRO importa `GetCommandLineW` mas o switch é lido em código que não foi
   extraído.
6. **Nível exato do snapshot (arquivo/registro por drive × imagem de disco)** —
   não determinável pelo manual; só se sabe que existem ajustes de "drives
   incluídos" e "pastas excluídas".
7. **Desinstalação silenciosa / agendamento próprios** — sem documentação e sem
   execução: não dá para afirmar que o Ashampoo faz ou não faz.
8. **Preço e canal** verificados apenas pelo PAD (EUR 40) e pela API
   (`trial_days: 20`); o checkout da loja não foi exercitado.
9. **FREE × PRO por execução real** — o delta da §2.7 é 100% documental
   (manuais + site). A comparação binária real exigiria instalar o PRO (trial de
   20 dias), o que não foi feito.
10. **Errata na §1 da rodada anterior** — a afirmação "o instalador PRO não é
    Inno Setup" foi refutada por medição e corrigida no topo deste documento
    (offset 740.624 e 47.558.923).

---

## 9. Decisões que este documento obriga

1. **Análise forense pós-instalação vira item de produto.** O "Create post-install
   log" do Ashampoo (item 14, o nome oficial é *forensic analysis*) resolve um
   problema que o Tranal hoje **não tem**: o Tranal só registra o que o
   instalador gravou **enquanto o instalador rodava** (`InstallMonitor.cs:201`,
   `TracedPrograms.cs:44-56`). Para um programa **já instalado**, não existe
   fotografia do "antes" — logo, o caminho de fluxos do Ashampoo é o **log de
   instalação** (item 16) mais a varredura pós-desinstalação. **Decisão:** criar
   no Tranal um fluxo de **log de desinstalação para programa já instalado**,
   explicitamente marcado como *inferido*, nunca como *medido*.
2. **O motor de snapshot já existe — falta a biblioteca.** `SystemSnapshot.Capture`
   e `SystemSnapshot.Diff` (`Core/Modules/SystemSnapshot.cs:136,180`) já produzem
   o diff que o Ashampoo usa para gerar o log de instalação
   (`TracedPrograms.cs:53`), e o `Diff` só devolve **o que nasceu**
   (`NewFiles`/`NewFolders`/`NewRegistryKeys`, `SystemSnapshot.cs:557-577`).
   **Decisão:** manter o motor como fonte única e acrescentar **snapshots
   persistidos e nomeados** (criar, listar, apagar, comparar) — hoje a
   fotografia "só vive durante a operação" (`TracedPrograms.cs:43`).
3. **Gerenciador próprio de serviços e de tarefas agendadas.** O Ashampoo tem os
   dois (`manual-free.txt:330-402`, `manual-pro27.txt:311-343`). O Tranal hoje
   só **abre** `services.msc` (`Core/Tools/WindowsToolsCatalog.cs:85`) e
   `taskschd.msc` (`:78`). **Decisão:** implementar edição de serviço
   (tipo de inicialização, iniciar/parar) e gestão de tarefas agendadas, em vez
   de continuar delegando ao MMC.
4. **Importar lista de programas exportada.** O Ashampoo exporta e importa a
   lista, marcando o que não está instalado (item 26). O Tranal só exporta
   (`Core/Exporters.cs`); `TraceStore.Import` (`Core/Modules/TraceStore.cs:222`)
   importa um **log do Tranal**, não uma lista. **Decisão:** importar a lista
   exportada em CSV/XML do próprio Tranal (e, se fizer sentido, de concorrentes),
   com o mesmo caminho de "não instalado".
5. **Desinstalar pelo log de instalação, sem depender do desinstalador nativo.**
   É a conversa direta com `Core/MsiInstallLog.cs`: quando o log do instalador
   está disponível (149 de 149 pacotes MSI desta máquina), a remoção pode ser
   dirigida pelo log, e o desinstalador nativo passa a ser um caminho
   alternativo. **Decisão:** tratar o log como fonte primária de
   responsabilidade, como o Tranal já faz com `LeftoverReason`.
6. **Edição manual do log de instalação.** O Ashampoo permite remover entradas do
   log (item 10). O `TraceStore` grava, importa, exporta e apaga o log inteiro
   (`Core/Modules/TraceStore.cs:143,191,222,279`), mas **não** edita entrada.
   **Decisão:** dar edição de entrada ao `TraceStore`, porque é o único jeito de
   o log deixar de ser apenas histórico.
7. **"Drag & Drop UnInstall".** O Ashampoo põe um atalho na área de trabalho para
   arrastar e desinstalar (item 58). O Tranal tem `AllowDrop`/`Drop` em
   `Ui/Modules/HunterView.xaml:5` e `Ui/Tools/TopmostOverlayWindow.xaml:10,53` —
   mas o arrastar do Tranal **resolve um alvo** (arquivo → `Resolve` /
   `SetTargetFromFiles`), e **não** é o atalho "desinstalar o que foi arrastado"
   da área de trabalho do Ashampoo. **Decisão:** decidir se o Tranal ganha esse
   atalho; se ganhar, nasce do `TopmostTargetResolver`, não de um `Drop` novo.
8. **Exportar logs em HTML/CSV/TXT.** O Tranal já exporta a **lista** em
   CSV/TXT/HTML/XML (`Core/Exporters.cs`); o **log de instalação** sai só em JSON
   (`Core/Modules/TraceStore.cs:279`). **Decisão:** o log ganha os mesmos formatos
   da lista — a regra fica em um lugar só.
9. **Modelo de distribuição "Ashampoo App".** Stub de 1,6 MB assinado que baixa
   o produto para um contêiner único (`C:\Program Files\Ashampoo\Ashampoo App`,
   §3.6). É uma decisão de **produto** para o Tranal: instalador pequeno, canal
   único de atualização, produtos em subpastas. **Decisão:** avaliar o modelo
   contra a reality de distribuição do Tranal antes de qualquer código.

---

*Fonte da verdade: `auditoria/ashampoo/evidencia/RESUMO.md` (45.106 bytes,
500 linhas, 8 seções, 66 funcionalidades). Método: `auditoria/METODO.md`.
Regra do documento: **medido vale, inferido não vale** — cada afirmação deste
arquivo aponta para evidência em `evidencia/` ou para um grep real em
`src/`.*
