# Reconciliação de lacunas — Ashampoo, Total Uninstall, Uninstall Tool, Soft Organizer, Wise

**Lido no codigo, nao na memoria.** Cada linha de lacuna dos cinco documentos
`docs/auditoria/BINARIO-<CONCORRENTE>.md` foi conferida contra
`src/Tranal.Uninstaller/Core/**`, `src/Tranal.Uninstaller/Ui/**`,
`src/Tranal.Cli/**` e `installer/tranal.iss`. Nenhuma afirmacao abaixo vem do
documento de lacuna: quando o documento diz que o Tranal nao tem a
funcionalidade, isso foi verificado no codigo (e vice-versa).

**Total reconciliado: 263 linhas** (Ashampoo 59, Total Uninstall 41,
Uninstall Tool 60, Soft Organizer 45, Wise 58).

## Metodo

- **Busca antes de negativa.** Cada "ausente" foi procurado por termo, sinonimo
  e nome de classe via `grep` sobre `src/` — nunca por memoria. Termos usados,
  entre outros: `ServiceManager`, `ScheduledTaskManager`, `SnapshotLibrary`,
  `SnapshotComparer`, `EditEntry`, `ToReg`, `ProgramListImporter`, `Rstrtmgr`,
  `RmStartSession`, `RegLoadKey`, `RegUnLoadKey`, `RegReplaceKey`, `HexView`,
  `MD5`, `WinVerifyTrust`, `PopNotify`, `tranname`, `path0`, `NotifyIcon`,
  `KeyGesture`, `EnumPrinters`, `Bluetooth`, `Geolocation`,
  `UninstallDisplayName`, `ImpersonateLoggedOnUser`, `PrivilegeCheck`.
- **Estados medidos:** `OK` = a capacidade esta no codigo; `PARCIAL` = existe em
  parte, ou em outra forma; `AUSENTE` = buscada e nao encontrada no codigo do
  Tranal; `FORA` = nao existe por decisao registrada.
- **Onde o simbolo nao foi localizado com certeza**, a linha esta marcada
  `nao confirmado no codigo` — nao `AUSENTE`. Ver secao 4.
- **Regra de citacao:** caminho:linha do Tranal sempre que a linha for `OK` ou
  `PARCIAL`; para `AUSENTE`, o termo procurado e o resultado do `grep`.

## Catalogo canonico — como ler o numero da linha

O `#` das tabelas `BINARIO-<CONCORRENTE>.md` e o numero da funcionalidade no
catalogo canonico `docs/auditoria/<CONCORRENTE>.md` (coerente com os totais
declarados: "94 funcionalidades mapeadas" em `UNINSTALLTOOL.md` e
`TOTALUNINSTALL.md`; "77" em `SOFTORGANIZER.md`). Essa correspondencia e
**inferida**, nao relida linha a linha — ver secao 4. As citacoes abaixo usam o
formato `BINARIO-<X>.md #N` como ancora primaria.

---
## 1. Ashampoo UnInstaller 27.0.1 - `BINARIO-ASHAMPOO.md` (59 linhas)

| # | Funcionalidade | O que existe no Tranal hoje (caminho:linha) | Estado | Evidencia |
|---|---|---|---|---|
| 1 | Analise forense: log de desinstalacao de programa **ja instalado** | `Core/Tracing/ForensicAnalyzer.cs:179` `Analyze` / `:288` `BuildPlan(MsiInstallLog)` / `:350` `BuildPlan(TraceLogV2)` | OK | Analise forense pos-instalacao com procedencia `medido`/`inferido` marcada (`ForensicAnalyzer.cs:74`) |
| 2 | Analise forense **em lote** (todas as apps de uma vez) | `Core/Tracing/ForensicAnalyzer.cs:179` - so por programa | AUSENTE | `grep Analyze` mostra so a assinatura por `InstalledProgram`; `SoftwareHealth.Analyze` (`:119`) varre todos, mas e health, nao forense |
| 3 | Biblioteca de **snapshots nomeados, listaveis e apagaveis** | `Core/Tracing/SnapshotLibrary.cs` - `Save` (metadados `:110`), `List`, `delete`, `Compare` (`:298`) | OK | `Ui/Tracing/TracingWindow.xaml.cs:361` cria, `:408` apaga, `:519` lista |
| 4 | Comparacao de snapshots que mostra **o que mudou**, nao so o que nasceu | `Core/Tracing/SnapshotComparer.cs` `Compare` | OK | Expoe `ModifiedFiles`, `ModifiedRegistryValues`, `RemovedFiles`, `UnchangedFiles` (`TracingWindow.xaml.cs:578-583` mostra as contagens) |
| 5 | **Gerenciador proprio de servicos** (tipo de inicializacao, iniciar/parar) | `Core/Management/ServiceManager.cs` - `List`/`Disable`/`Enable`/`Stop`/`delete` | OK | **FALSO POSITIVO do documento:** ele afirma que o Tranal so abre `services.msc` (`Core/Tools/WindowsToolsCatalog.cs:85`); o gerenciador proprio existe - `docs/auditoria/BINARIO.md:160-161` |
| 6 | **Gerenciador proprio de tarefas agendadas** (habilitar/desabilitar/excluir) | `Core/Management/ScheduledTaskManager.cs` - `List`/`Disable`/`Enable`/`delete` | OK | **FALSO POSITIVO do documento:** ele afirma que so abre `taskschd.msc` (`WindowsToolsCatalog.cs:78`) - `docs/auditoria/BINARIO.md:160-161` |
| 7 | **Editar manualmente o log de instalacao**, entrada a entrada | `Core/Modules/LogOperations.cs:375` `EditEntry`; `:459` `IgnoreEntry`; `:490` `AddEntry`; `removeEntry` | OK | Edicao de entrada (arquivo, pasta, chave, valor) com gravacao do log |
| 8 | Exportar alteracoes de registro do log para **`.reg`** | `Core/Reporting/ReportFormats.cs:162` `ToReg` / `:214` `WriteReg` | OK | Usa o tipo real do valor (`GetValueKind`): binario sai `hex:` |
| 9 | Exportar logs de desinstalacao em **HTML, CSV e TXT** | `Core/Reporting/TraceLogExporter.cs` - `ToText`/`ToCsv`/`ToHtml`/`Write` | OK | O log exporta nesses tres formatos (a lista exporta em `Core/Exporters.cs:39,49,63`) |
| 10 | **Importar lista de programas** exportada (marcando o que nao esta instalado) | `Core/Reporting/PortableArchive.cs:357` `ProgramListImporter` / `:365` `Import` / `:393` `ImportContent` | OK | Casa cada item importado com o instalado por nome e versao |
| 11 | **Unlocker**: desbloquear arquivo em uso, reiniciar o Explorer, desregistrar DLL | `Core/ProcessTerminator.cs:156` `Terminate`; `Core/DeferredDelete.cs:47` `MoveFileEx` | AUSENTE | Encerrar processo e adiar remocao para o reboot existem; **reinicio do Explorer e `DllUnregister` nao**: `grep regsvr32|DllUnregister|Explorer.*restart` -> 0 hits |
| 12 | **Undeleter**: recuperar arquivo apagado acidentalmente | `Core/Recovery/ProgramBackup.cs:228` `Undo` / `:255` `Redo`; `Core/Recovery/RecoverOperation.cs` | PARCIAL | Recupera **o que o proprio Tranal removeveu** (backup antes da remocao); nao ha undelete de arquivo apagado fora do Tranal |
| 13 | **Crash Analyzer**: relatorios de falha do Windows Event Log agrupados por programa | `Core/Tools/PrivacyTraceCleaner.cs:154` (grupo `EventLogs`) | AUSENTE | O Tranal **limpa** Event Logs (6 grupos em `PrivacyTraceCleaner.cs:12-31`); nao agrupa crash por programa |
| 14 | **File Wiper** com sobrescrita de espaco livre por unidade | `Core/Tools/UnrecoverableDelete.cs` (sobrescrita do arquivo); `Core/Tools/EvidenceRemover.cs` | PARCIAL | Sobrescrita do arquivo removido existe; **nao ha** varredura de espaco livre por volume (`grep FreeSpace` -> so `Core/Settings/ProgramCache.cs:345`) |
| 15 | **File Associator** (tipos sem programa associado, editar/undo) | `Core/RegistryEditing/RegistryEditor.cs:368,382` (editor generico de registro) | AUSENTE | `grep FileAssoc|DefaultIcon|ProgId` -> 0 hits; nao ha painel de associacoes de arquivo |
| 16 | **Hibernation**: colocar um programa para dormir e acorda-lo depois | - | AUSENTE | `grep Hibernat|Suspend` -> 0 hits no `src/` (so `Thread.Sleep` em `Ui/Appearance/ProgramClipboard.cs:66`) |
| 17 | **Impact**: ranking por impacto em performance (CPU/memoria) | `Core/Appearance/TreemapModel.cs:104` `Build` (tamanho); `Core/Appearance/SmartOrdering.cs:25` | AUSENTE | So ha ordenacao e mapa de **tamanho**; nenhuma medicao de CPU/memoria por programa |
| 18 | **Move / Revert relocation** de um programa instalado | - | AUSENTE | `grep Relocat|Move.*install` -> 0 hits; nenhuma operacao de mover/reverter a pasta de instalacao |
| 19 | **Software Updater** de programas de terceiros, em lote, com lista negra | `Core/Updates/AutoUpdateClient.cs:74` `CheckAsync` | AUSENTE | O feed do Tranal atualiza **so o proprio Tranal**; nao ha atualizador de terceiros |
| 20 | **Ratings por estrelas** com base online + "rate online" | `Core/Appearance/ProgramRating.cs` (rating **local**); `Core/ProgramCustomization.cs:463` | FORA | Base online exige servico: `docs/auditoria/BINARIO.md` secao 3.2 (1a linha, "o Tranal e offline, sem cadastro e sem telemetria") e `docs/PARIDADE.md:481` |
| 21 | **Desfragmentacao** de drives | `Core/Tools/WindowsToolsCatalog.cs:43` (`cleanmgr.exe`) | FORA | Nao e dominio de desinstalacao: `docs/auditoria/BINARIO.md` secao 3.2 (analisador de disco, gerenciador de conexoes, de fontes, papel de parede, destruidor de arquivos: nao e dominio de desinstalacao) |
| 22 | Filtro da lista **por tamanho** (subtopicos) | `Core/Appearance/ProgramFilters.cs:160` `SizeFilter`; `MainWindow.xaml.cs:325,349-352` | OK | **FALSO POSITIVO do documento:** afirma "nenhum filtro por tamanho localizado no `src/`"; existem `all`/`small`/`medium`/`large` - `docs/auditoria/MATRIZ.md:146` |
| 23 | Comparacao de tamanho **declarado vs. real**, sincronizavel com o Windows | `Core/ProgramScanner.cs:405` `MeasureFolder` | AUSENTE | Existe estimativa e medicao isoladas; **nao ha** tela de comparacao nem sincronizacao |
| 24 | **Pais do editor** com bandeira | - | AUSENTE | `grep Country|RegionInfo|flag` -> 0 hits |
| 25 | Categoria automatica com **reset para "Unsorted"** | `Core/Appearance/GroupingModel.cs:80` `Group` | AUSENTE | Agrupamento por coluna existe; **nao ha** categoria "Unsorted" nem reset - `grep Unsorted|SemCategoria` -> 0 hits |
| 26 | **Whitelist / "cookies seguros"** no Internet Cleaner | `Core/Tools/PrivacyTraceCleaner.cs:12-31` (6 grupos fixos) | AUSENTE | Nao ha whitelist de cookies; os 6 grupos sao fixos |
| 27 | **Registry Optimizer 2** com "Super Safe Mode" | `Core/Tools/RegistryOptimizer.cs:72` `ScanStartMenu` / `:129` `remove` (com `SafetyRules` em `:146`) | PARCIAL | Otimizador de registro e regras de protecao existem; **nao ha** o modo "Super Safe Mode" nomeado |
| 28 | **Exclusao de backups por idade** (7/14/30 dias) | `Core/BackupManager.cs:586` `DeleteOlderThan`; `Core/Recovery/LeftoverBackup.cs:440` `Expire`; `Ui/BackupManagerWindow.xaml.cs:393` | OK | Exclusao por data existe nos dois niveis (backup de sobras e pontos de restauracao) |
| 29 | Filtro de tamanho no **Junk/Cache cleaner** | `Core/Tools/JunkCleaner.cs:55` `Scan` | AUSENTE | A varredura do lixo nao tem filtro de tamanho |
| 30 | **Install guard**: definir como o programa identifica setups e quais propriedades de arquivo usar | - | AUSENTE | `grep InstallerGuard|SetupGuard` -> 0 hits |