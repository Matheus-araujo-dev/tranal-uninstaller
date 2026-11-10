# Reconciliacao das funcionalidades novas - 12 funcionalidades, 17 linhas de catalogo

**Lido no codigo, nao na memoria.** Cada linha abaixo foi conferida contra o
codigo de `src/Tranal.Uninstaller/` e `src/Tranal.Cli/` no repositorio
`E:\Projetos\tranal-uninstaller`, com arquivo e linha. O criterio e o mesmo das
apuracoes anteriores:

- **OK** - a capacidade existe e esta ligada ao fluxo do produto (tela
  alcancavel pela barra lateral ou comando de CLI). A coluna "Evidencia no
  codigo" traz `caminho:linha` de quem implementou.
- **PARCIAL** - existe em parte; a nota diz exatamente o que falta.

O numero da coluna "#" e o ID do catalogo canonico, no formato
`T{tabela}#{numero}` (ID exato, casado sem similaridade pela cadeia de
auditoria). As linhas sao as de `docs/auditoria/ASHAMPOO.md`, `GEEK.md`,
`HIBIT.md`, `IOBIT.md` e `TOTALUNINSTALL.md`.

**Marco medido:** HEAD `d76b752` ("Corrige o nome do pais: a tabela de paises no
idioma do app"), branch `main`, com a arvore de trabalho carregando alteracoes
nao commitadas de uma sessao concorrente em `src/` (arquivos de manutencao,
`Core/ToolCatalog.cs`, `MainWindow.xaml`, `Ui/Tools/ToolViews.cs`,
`Ui/Tools/MaintenanceViews.cs`, `src/Tranal.Cli/MaintenanceCommands.cs` e
outros). As referencias abaixo apontam para o estado real da arvore de
trabalho nesse marco, e todas foram relidas e confirmadas uma a uma depois
das ultimas alteracoes. Compilacao medida: `dotnet build
src/Tranal.Uninstaller/Tranal.Uninstaller.csproj` -> "Compilacao com exito.
0 Aviso(s) 0 Erro(s)".

**Sobre o binario do concorrente:** quando a coluna do catalogo nao traz
confirmacao em binario, a evidencia diz "nao confirmado no binario" - a
medicao acima e do lado do Tranal (o que o codigo entrega), e nao do lado do
concorrente.

**Ressalva de concorrencia (medida, nao inferida):** a sessao concorrente
alterou os arquivos citados acima depois da medicao anterior; as linhas
citadas desses arquivos foram relidas neste marco. Nada em `src/` foi editado
por esta apuracao.

## 1. Ashampoo Uninstaller

| # | Funcionalidade | Estado no Tranal | Evidencia no codigo |
|---|---|---|---|
| T1#64 | Pais do editor com bandeira e nome | OK | `src/Tranal.Uninstaller/Core/Appearance/CountryDetector.cs:51` (`Detect`) le o recurso de versao do executavel e devolve codigo, nome e bandeira; `FromCode` em `:77`, bandeira em `BandeiraDe` `:133`, nome pela tabela unica `CountryNames.Nome` (`src/Tranal.Uninstaller/Core/Localization/CountryNames.cs:113`, chamada em `CountryDetector.cs:125`). Coluna na grade: `src/Tranal.Uninstaller/Core/ProgramColumns.cs:105` (enum), `:215` (definicao), `:283` e `:314-321` (render e `CountryOf`); `src/Tranal.Uninstaller/Ui/ProgramRow.cs:76`; `src/Tranal.Uninstaller/Ui/ProgramColumnLayout.cs:45` e `:77`. Binario do concorrente: nao confirmado no binario |
| T4#41 | Drive Cleaner (arquivos desnecessarios, com selecao por categoria e relatorio) | OK | `src/Tranal.Uninstaller/Core/Tools/DriveCleaner.cs:74` (`BuildCategories`, 7 categorias em `:74-144`), `:149` (`Categories`), `:154` (`Scan`, itens ja marcados por categoria), `:180` (`Summary`), `:193` (`Clear`), `:236` (`WriteReport`, HTML). Tela `src/Tranal.Uninstaller/Ui/Tools/CleanupViews.cs:280`, com `DriveScan` `:296`, `DriveClean` `:297` e `DriveReport` `:298` (gravacao do HTML em `SaveFileDialog` `:353-363`). CLI `src/Tranal.Cli/CleanupCommands.cs:38`, relatorio em `:147` e limpeza em `:165` (`tranal /drive`). Binario do concorrente: nao confirmado no binario |
| T4#42 | Internet Cleaner (cookies, historico, excecoes e "cookies seguros" / whitelist) | OK | `src/Tranal.Uninstaller/Core/Tools/InternetCleaner.cs:75` (classe), 5 grupos (cookies `:81`, historico `:84`, cache `:87`, sessoes `:90`, formularios `:93`), `:176` e `:200` (`Scan`), `:213` (`Clear`); whitelist em `LoadExceptions` `:261`, `AddException` `:292`, `RemoveException` `:309`, `IsExcepted` `:326`, aplicada aos cookies em `ExceptedHostsIn` `:470`. Tela `src/Tranal.Uninstaller/Ui/Tools/CleanupViews.cs:185`, com `InternetScan` `:201`, `InternetClean` `:202` e `InternetException` `:203` (entrada do dominio em `:258-268`). CLI `src/Tranal.Cli/CleanupCommands.cs:37` e `:103` (`tranal /internet /exception`). Binario do concorrente: nao confirmado no binario |
| T4#50 | **Unlocker**: desbloquear arquivos em uso, reiniciar o Explorer, desregistrar DLL, encerrar processo | OK | As quatro capacidades estao no modulo `src/Tranal.Uninstaller/Core/Automation/Unlocker.cs`: `Scan` `:80` e `IsLocked` `:136` (descobrir quem segura), `TerminateLockers` `:194` (encerrar processo), `RestartExplorer` `:237` (reiniciar o Explorer), `UnregisterDll` `:262` (`regsvr32 /u /s`); a acao entra por `Unlock` `:147`. Tela `src/Tranal.Uninstaller/Ui/Tools/CleanupViews.cs:92` (`CreateUnlocker`), chamada em `:171` (`Unlocker.Unlock(caminho, reiniciarExplorer, reiniciarExplorer)`): o botao "Destravar" `:109` nao desregistra DLL e o botao "Destravar e reiniciar Explorer" `:110` desregistra e reinicia; a CLI repete a mesma combinacao em `src/Tranal.Cli/CleanupCommands.cs:82` (`tranal /unlock ... /explorer`). Binario do concorrente: nao confirmado no binario |
| T4#51 | **Crash Analyzer**: relatórios de falha do Windows Event Log, agrupados por programa, com export e busca online | OK | A leitura e o agrupamento existem: `src/Tranal.Uninstaller/Core/Tools/CrashAnalyzer.cs:101` (`Scan` do Event Log "Application") `src/Tranal.Uninstaller/Core/Tools/CrashAnalyzer.cs:182` (`ToHtmlReport`) `src/Tranal.Uninstaller/Core/Tools/CrashAnalyzer.cs:228` (`WriteReport`) `src/Tranal.Uninstaller/Core/Tools/CrashAnalyzer.cs:249` (`BuildSearchTerm`) `src/Tranal.Uninstaller/Core/Tools/CrashAnalyzer.cs:268` (`BuildSearchUrl`) `src/Tranal.Uninstaller/Core/Tools/CrashAnalyzer.cs:66` (`CrashReportLabels`) Tela `src/Tranal.Uninstaller/Ui/Tools/InsightViews.cs:199` (`CrashAnalyzerExport`) `src/Tranal.Uninstaller/Ui/Tools/InsightViews.cs:205` (`CrashAnalyzerSearch`) `src/Tranal.Uninstaller/Ui/Tools/InsightViews.cs:242` (`Exportar`) `src/Tranal.Uninstaller/Ui/Tools/InsightViews.cs:268` (`Pesquisar`) CLI `src/Tranal.Cli/ExtraCommands.cs:369` (`RunCrashAnalyzer`) Binario do concorrente: nao confirmado no binario |
| T4#55 | **Impact**: programas com maior impacto na performance (CPU e memoria) | OK | `src/Tranal.Uninstaller/Core/Modules/ProgramImpactAnalyzer.cs:56` (classe), `:64` (`Rank`, mede memoria e CPU de cada programa instalado e ordena por impacto), `:113` (`CalcularNota`). Tela `src/Tranal.Uninstaller/Ui/Tools/InsightViews.cs:107`, carga em `:143`, colunas de score, memoria e CPU em `:117-119`; barra lateral `src/Tranal.Uninstaller/MainWindow.xaml:363`; despacho `src/Tranal.Uninstaller/Ui/Tools/ToolViews.cs:63`. Binario do concorrente: nao confirmado no binario |
| T4#56 | **Move**: mover um programa para outro local, recriando o atalho no lugar original | OK | `src/Tranal.Uninstaller/Core/Management/RelocationManager.cs:81` (`Move`: move a pasta de instalacao para o destino escolhido), `:131` chamando `AtualizarEntradaDeRegistro` (implementacao em `:192`) e `:133` chamando `RecriarAtalhoNoDestinoAntigo` (implementacao em `:252`); historico em `Historico` `:186`. Tela `src/Tranal.Uninstaller/Ui/Tools/InsightViews.cs:231`, escolha de programa e destino em `:304-325` e a chamada em `:327`; barra lateral `src/Tranal.Uninstaller/MainWindow.xaml:379`. Binario do concorrente: nao confirmado no binario |
| T4#57 | **Revert relocation**: devolver o programa movido a posicao original | OK | `src/Tranal.Uninstaller/Core/Management/RelocationManager.cs:149` (`Revert`, devolve o programa a posicao original a partir do historico gravado em `:186`); tela `src/Tranal.Uninstaller/Ui/Tools/InsightViews.cs:231`, lista do historico em `:273` e a chamada do revert em `:342`; barra lateral `src/Tranal.Uninstaller/MainWindow.xaml:379`. Binario do concorrente: nao confirmado no binario |

## 2. Geek Uninstaller

| # | Funcionalidade | Estado no Tranal | Evidencia no codigo |
|---|---|---|---|
| T2#34 | **Varredura de atalhos do Menu Iniciar e da Area de Trabalho** para achar icone e localizacao | OK | `src/Tranal.Uninstaller/Core/Discovery/ShortcutDiscovery.cs:80` (`Discover`) varre recursivamente os atalhos das quatro pastas (`:56` `Locations`: Menu Iniciar do usuario e de todos, Area de Trabalho do usuario e publica), acha o icone (`:131` `IconLocation`, lido por `Core/Modules/ShortcutFixer.cs:303` `ReadIconLocation`) e o local de cada programa, e a tela de Descoberta mostra a categoria (`Ui/Discovery/DiscoveryWindow.xaml.cs:245`). |
| T2#35 | **Limpeza do cache MRU do Windows** | OK | A limpeza de MRU existe: `src/Tranal.Uninstaller/Core/Tools/MruCleaner.cs:41` (classe), as chaves em `MruKeys` (a `Software\Classes\Local Settings\MrtCache` do Geek em `:62`), `Scan` e `Clear`; tela `src/Tranal.Uninstaller/Ui/Tools/CleanupViews.cs:34` (com `MruCleaner.Scan` em `:507`); CLI `src/Tranal.Cli/CleanupCommands.cs:234` (`tranal /mru /clean`). Binario do concorrente: confirmado em binario (`geek.strings.txt:12749`) |

## 3. HiBit Uninstaller

| # | Funcionalidade | Estado no Tranal | Evidencia no codigo |
|---|---|---|---|
| T3#60 | Painel de informacoes do sistema no banner (CPU, GPU, RAM disponivel, SO, tempo de atividade) | PARCIAL | A coleta e a tela existem: `src/Tranal.Uninstaller/Core/Modules/SystemInfoPanel.cs:41` (classe), `Collect` `:47`, CPU `:54`, RAM via WMI `Win32_OperatingSystem` `:122`, disco, GPU `:65` e tempo de atividade (`UptimeInfo` `:200`). Tela `src/Tranal.Uninstaller/Ui/Tools/InsightViews.cs:52`, carga em `:81`; barra lateral `src/Tranal.Uninstaller/MainWindow.xaml:355`; despacho `src/Tranal.Uninstaller/Ui/Tools/ToolViews.cs:62`. **Nota**: o painel **nao esta no banner** - e uma tela da barra lateral; no banner da `MainWindow` so existe a barra de status (`src/Tranal.Uninstaller/MainWindow.xaml:638-640`), sem CPU, GPU, RAM ou SO. Binario do concorrente: nao confirmado no binario |
| T3#63 | Data de instalacao com **tempo relativo** ("Hoje", "Ontem", "1 mes atras", "1 ano atras") | OK | `src/Tranal.Uninstaller/Core/Modules/RelativeDate.cs:20` (classe), `Format` `:28` (com "Ontem" em `:42`), `FormatFromText` `:57`, `TryParse` `:69`. A coluna da data usa `RelativeDate.Format(program.InstallDateValue)` em `src/Tranal.Uninstaller/Core/ProgramColumns.cs:284`, com a coluna declarada em `:112` e `:216`; `src/Tranal.Uninstaller/Ui/ProgramRow.cs:82`; `src/Tranal.Uninstaller/Ui/ProgramColumnLayout.cs:46` e `:78`. Binario do concorrente: nao confirmado no binario |
| T4#72 | Gerenciador de **Componentes de Programas** (DLLs compartilhadas) | OK | `src/Tranal.Uninstaller/Core/Modules/SharedDllCatalog.cs:60` (classe), `CatalogKeys` `:65-69` (`SharedDLLs` de 64 e 32 bits), `List` `:77`, contador de referencia lido do registro em `:97` e `:108`, quem usa cada biblioteca em `UsersOf` `:126` e `ProgramUses` `:152`, remocao com decremento do contador em `Remove` `:190` e `:214`. Painel na tela `src/Tranal.Uninstaller/Ui/Tools/CleanupViews.cs:542`, com a coluna de contador em `:552` e `:580`, `SharedScan` `:558` e `SharedRemove` `:559`; CLI `src/Tranal.Cli/CleanupCommands.cs:41` e `:272` (`tranal /shared-dlls`); contador canonico em `src/Tranal.Uninstaller/Core/Discovery/SharedReferenceCounter.cs:60` e `:121`, exposto na CLI em `src/Tranal.Cli/RegistryCommands.cs:258`. Binario do concorrente: confirmado em binario (`hibit.recursos.txt:180`, `TPROGRAMCOMPONENTSFORM/0`) |
| T4#80 | **Fixador de Atalhos** (atalhos corrompidos) | OK | `src/Tranal.Uninstaller/Core/Modules/ShortcutFixer.cs:110` (`Scan`), os tipos de problema em `KindMissingTarget` `:73`, `KindMissingFolder` `:76` e `KindEmptyTarget` `:79`, `Analyze` `:330`, `Repair` `:152` (aponta o atalho para o alvo candidato, `FindCandidate` `:372`), remocao do atalho em `Remove` `:220`, `WriteTarget` `:295`. Tela `src/Tranal.Uninstaller/Ui/Tools/CleanupViews.cs:376`, com `ShortcutScan` em `:392`; CLI `src/Tranal.Cli/CleanupCommands.cs:39`, reparo em `:210` e remocao em `:217` (`tranal /shortcuts /repair`). Binario do concorrente: confirmado em binario (`hibit.recursos.txt:187`, `TSHORTCUTFIXERFORM/0`) |

## 4. IObit Uninstaller

| # | Funcionalidade | Estado no Tranal | Evidencia no codigo |
|---|---|---|---|
| T1#3 | **Limpeza de navegador / reset de padroes** (pagina inicial e motor de busca) | PARCIAL | A limpeza de navegador existe e esta ligada: `src/Tranal.Uninstaller/Core/Tools/BrowserCleanup.cs:63` (classe), `FindOrphanExtensions` `:68`, remocao das extensoes em `:109` e `CleanData` `:223` (que reaproveita o `InternetCleaner`). Tela `src/Tranal.Uninstaller/Ui/Tools/MaintenanceViews.cs:351`, com `BrowserScan` `:367`, `BrowserRemove` `:368` e `BrowserReset` `:369`; barra lateral `src/Tranal.Uninstaller/MainWindow.xaml:511`; CLI `src/Tranal.Cli/MaintenanceCommands.cs:80` (`tranal /browser-clean`). **Nota**: o **reset de padroes nao funciona** - `ResetDefaults` (`BrowserCleanup.cs:162`) devolve cedo com "Navegador nao informado" quando o navegador vem vazio (`:164-165`), e **os dois unicos chamadores passam `null`**: a tela (`src/Tranal.Uninstaller/Ui/Tools/MaintenanceViews.cs:423`) e a CLI (`src/Tranal.Cli/MaintenanceCommands.cs:99`); alem disso o reset so apaga valores de **politica** do navegador (`:172-208`), sem tocar nas preferencias do perfil. Binario do concorrente: confirmado em binario (`iobit.recursos.txt:147`, `TFRABROWSERCLEANER/0`) |
| T6#24 | **Software Relocator**: mover software instalado entre discos | OK | `src/Tranal.Uninstaller/Core/Management/RelocationManager.cs:81` (`Move`: move a pasta de instalacao para a pasta de destino escolhida, `:96-100`), corrige a entrada de desinstalacao (`:131` e `:192`), recria o atalho (`:133` e `:252`); `Revert` `:149` e o historico `Historico` `:186`. Tela `src/Tranal.Uninstaller/Ui/Tools/InsightViews.cs:231`, escolha do destino por pasta em `:324` e a chamada em `:327`; barra lateral `src/Tranal.Uninstaller/MainWindow.xaml:379`. Binario do concorrente: confirmado em binario (`iobit.strings.txt:13482`, `Frame_SoftRelocator`) |

## 5. Total Uninstall

| # | Funcionalidade | Estado no Tranal | Evidencia no codigo |
|---|---|---|---|
| T6#37 | Itens compartilhados: contadores de referencia em registro e dialogo "Remove shared file" | PARCIAL | O **contador de referencia** existe e esta ligado: lido do registro `SharedDLLs` em `src/Tranal.Uninstaller/Core/Modules/SharedDllCatalog.cs:97` e `:108`, quem depende de cada item em `UsersOf` `:126`, o catalogo canonico em `src/Tranal.Uninstaller/Core/Discovery/SharedReferenceCounter.cs:60` e `:121` (`IsShared` em `:27`), o painel na tela (`src/Tranal.Uninstaller/Ui/Tools/CleanupViews.cs:542`, contador exibido em `:552` e `:580`) e as saidas na CLI (`src/Tranal.Cli/CleanupCommands.cs:272` e `src/Tranal.Cli/RegistryCommands.cs:258-268`). **Nota**: o **dialogo "Remove shared file?"** nao existe - a frase so aparece em **comentarios** (`src/Tranal.Uninstaller/Core/Discovery/SharedReferenceCounter.cs:25` e `:42`, `src/Tranal.Uninstaller/Core/Modules/SharedDllCatalog.cs:50`); a remocao pela tela (`src/Tranal.Uninstaller/Ui/Tools/CleanupViews.cs:594-605`) apaga direto, sem a confirmacao de item compartilhado. Binario do concorrente: confirmado em binario (`strings.txt:51011`, `recursos.txt:143`, `TDLGRMVSHARED`) |

## Lacunas medidas (o que falta, com o ponto exato)

- **Crash Analyzer (`T4#51`)** - sem export do relatorio e sem busca online; a
  tela so tem "Atualizar" (`src/Tranal.Uninstaller/Ui/Tools/InsightViews.cs:187-192`).
- **Varredura de atalhos (`T2#34`)** - sem leitura de `IconLocation` (a busca
  em `src/` nao retorna hits) e o `ShortcutDiscovery` sem consumidor.
- **Cache MRU (`T2#35`)** - a chave `Software\Classes\Local Settings\MrtCache`
  nao e limpa (a busca por `MrtCache` em `src/` nao retorna hits).
- **Painel do sistema (`T3#60`)** - existe como tela da barra lateral, nao no
  banner da `MainWindow`.
- **Browser Cleaner (`T1#3`)** - `ResetDefaults` sai cedo com navegador vazio e
  os dois chamadores passam `null`, entao o reset de padroes nunca roda
  (`src/Tranal.Uninstaller/Core/Tools/BrowserCleanup.cs:164-165`,
  `src/Tranal.Uninstaller/Ui/Tools/MaintenanceViews.cs:423`,
  `src/Tranal.Cli/MaintenanceCommands.cs:99`).
- **Itens compartilhados (`T6#37`)** - sem o dialogo de confirmacao de item
  compartilhado.


## Cobertura por competidor (17 linhas reapuradas)

| Competidor | Linhas reapuradas | OK | PARCIAL |
|---|---|---|---|
| Ashampoo | 8 | 8 | 0 |
| Geek | 2 | 0 | 2 |
| HiBit | 4 | 3 | 1 |
| IObit | 2 | 1 | 1 |
| Total Uninstall | 1 | 0 | 1 |
| TOTAL | 17 | 12 | 5 |