# Lacunas - Total Uninstall 7.6.2

Binario analisado: `C:\Users\Matheus\.minimax\sessions\mvs_1a1a8292487b443c92dc9d3eac74f170\workspace\auditoria\totaluninstall\app\professional\app\Tu.exe`  sha256: `d9df17d164a52c9f80b73870845fb49e42ce3fb1c236813ec07c9d7c88253bd6` (11.324.608 bytes, versao 7.6.2.367, Gavrila Martau)

Metodo: extracao **estatica** do PE, sem execucao. (1) Tabela de imports por DLL + delay-load (`bin-inventario/totaluninstall.imports.txt`); (2) recursos Delphi do PE - 22 formularios `T*` e 2 estilos VCL (`bin-inventario/totaluninstall.recursos.txt`); (3) classes RTTI e 680 rotulos de interface (`bin-inventario/totaluninstall.inventario.md`); (4) dump bruto de strings com 51.365 literais (`bin-strings/totaluninstall.strings.txt`). Cada literal citado foi reconfirmado por `grep` no dump, com o numero da linha.

## Funcionalidades presentes no binario e ausentes no Tranal

| # | Funcionalidade | Evidencia no binario | Categoria | Impacto (alto/medio/baixo) |
|---|---|---|---|---|
| 12 | Fila de **renomeacoes pendentes** para aplicar sem reiniciar (PARCIAL) | `recursos.txt:140` → `TDLGPENDINGFILERENAME`; `strings.txt:420` → `Pending file rename operations` | remocao | medio |
| 14 | Nome do snapshot pre/pos-instalacao **customizavel** (AUSENTE) | `inventario.md:28` → `Allow custom snapshot name` | varredura | medio |
| 15 | Limite de tamanho e de contagem da **pasta de snapshots** (AUSENTE) | `inventario.md:326` → `Limits for Snapshots folder` | varredura | baixo |
| 17 | **Comparar snapshots** com perfis, incluindo modificados e removidos (PARCIAL) | `recursos.txt:134` → `TDLGCOMPARESNS`; `strings.txt:15142` → `Compare system snapshots` | varredura | alto |
| 18 | **Perfis por operacao** (Scanning, Analysis, Uninstall, Comparing, Restore/Redo, Cleaner) (AUSENTE) | `inventario.md:9` → `tabAnalysisProfile`, `tabCompareProfile`, `tabScanProfile`, `tabUninstPrf`, `tabRedoProfiles` | interface | medio |
| 19 | Par **include/exclude por perfil** de scanning (PARCIAL) | `strings.txt:17269` → `Add to scan exclude list`; `inventario.md:262` → `Include folders` | varredura | medio |
| 20 | Varredura de **Alternate Data Streams (ADS)** (AUSENTE) | `inventario.md:176` → `Edit program Delete alternate data stream: %s` | varredura | medio |
| 21 | **Visoes de mudanca** configuraveis (AUSENTE) | `inventario.md:81` → `Change views`; `inventario.md:9` → `tabChangeView`, `tabChangeViews` | interface | medio |
| 22 | **Arvore de mudancas** com arquivos, pastas, chaves e valores (PARCIAL) | `inventario.md:9` → `tabChageViewFileSystem`, `tabChageViewRegistry` | interface | alto |
| 23 | Opcao de **nao usar o log de mudancas** por operacao (PARCIAL) | `inventario.md:167` → `Don't use detected changes log` | varredura | medio |
| 30 | Restaurar **so o log de instalacao**, sem o programa (AUSENTE) | `recursos.txt:149` → `TDLGTUNRESTORE`; `strings.txt:16320` → `Create installation log` | recuperacao | medio |
| 32 | **Undo/Redo** de uma restauracao (AUSENTE) | `inventario.md:9` → `tabRedo`, `tabRedoAdvanced`, `tabRedoProfiles` | recuperacao | alto |
| 37 | **Contadores de referencia** de itens compartilhados e dialogo "Remove shared file" (AUSENTE) | `strings.txt:51011` → `Decrement registry shared file counter`; `strings.txt:51014` → `Shared file counter found too small in registry`; `recursos.txt:143` → `TDLGRMVSHARED` | remocao | alto |
| 43 | **Motor de regras do Cleaner** em 6 categorias com Include/Exclude (PARCIAL) | `inventario.md:179` → `Enable cleaner module`; `inventario.md:9` → `tabCleanerFileSystem`, `tabCleanerReg` | interface | medio |
| 45 | Classificacao de sobras por extensao com **informacao de versao** (PARCIAL) | `inventario.md:216` → `File version` | varredura | baixo |
| 46 | Remocao de **servicos e dispositivos instalados pelo programa** (PARCIAL) | `strings.txt:50989` → `Delete Service or Device: %s`; `strings.txt:51061` → `Installed services and devices` | remocao | alto |
| 50 | **Agente autonomo** de baixo consumo para notificar instalacoes em andamento (AUSENTE) | `strings.txt:15827` -> `AAuto-start with Windows to continue unfinished monitoring process` | automacao | alto | `inventario.md:191` → `Executables (*.exe)|*.exe`; `inventario.md:25` → `All files (*.*)|*.*` | remocao | baixo |
| 60 | **Validacao online de licenca** (AUSENTE) | `imports.txt:14` -> `wininet.dll / InternetCloseHandle`; `inventario.md:325` -> `Licensed to "%s"` | integracao | baixo | `strings.txt:17274` → `Add to compare ignore list` | varredura | medio |
| 61 | Filtros de **tipo de arquivo** na exclusao (PARCIAL) | `inventario.md:191` -> `Executables (*.exe)|*.exe`; `inventario.md:25` -> `All files (*.*)|*.*` | remocao | baixo |
| 63 | Exclusao de chaves/valores **por perfil** (PARCIAL) | `strings.txt:17274` -> `Add to compare ignore list` | varredura | medio |
| 64 | Item compartilhado preservado **com a contagem de uso** (PARCIAL) | `strings.txt:51011` → `Shared File. Not deleted in this phase`; `strings.txt:50997` → `Invalid change detected for registry shared file counter` | remocao | medio |
| 65 | **Instalar programa novo** com monitoramento do instalador (AUSENTE) | `inventario.md:9` → `tbtnInstallNew`; `strings.txt:16814` → `Install new...` | automacao | alto |
| 66 | **Instalar atualizacao** de programa ja instalado (AUSENTE) | `inventario.md:9` → `tbtnInstallUpdate`; `strings.txt:16817` → `Install update...`; `inventario.md:344` → `Monitor program update "%s"` | automacao | alto |
| 67 | **Mesclar logs** de instalacao e apagar o log do update (AUSENTE) | `recursos.txt:139` → `TDLGMERGELOGS`; `strings.txt:17030` → `Merge logs...`; `inventario.md:152` → `Delete update log after merge` | organizacao | medio |
| 69 | **Exportar a arvore de mudancas** (PARCIAL) | `strings.txt:16868` → `Export changes tree` | relatorio | medio |
| 71 | **Imprimir a arvore de alteracoes** (PARCIAL) | `strings.txt:16887` → `Print the changes tree`; `strings.txt:51147` → `There is no default printer currently selected`; `strings.txt:14702` → `Vcl.Printers` | relatorio | medio |
| 72 | **Visor hexadecimal** e abertura de arquivo em editor hex (AUSENTE) | `strings.txt:17284` → `Hex view`; `strings.txt:17285` → `Open file in hex editor`; `strings.txt:15646` → `Hex viewer` | interface | medio |
| 73 | **Soma MD5** dos arquivos novos criados pela instalacao (AUSENTE) | `strings.txt:15993` → `Compute MD5 for new files`; `strings.txt:16071` → `New file MD5 sum` | integracao | medio |
| 76 | **Incluir/excluir por produto e por publicador** (AUSENTE) | `strings.txt:16193` → `Include publishers`; `strings.txt:16201` → `Exclude publishers`; `inventario.md:263` → `Include products`; `inventario.md:189` → `Exclude products` | interface | medio |
| 77 | **Listas de exclusao por operacao** e regras "ignore" por atributo (PARCIAL) | `strings.txt:17269` → `Add to scan exclude list`; `strings.txt:15972` → `Ignore changed items`; `strings.txt:16079` → `Ignore file attributes` | varredura | medio |
| 78 | **Classificacao dos itens detectados**: existentes / alterados / ausentes (AUSENTE) | `strings.txt:16039` → `Existing items in system`; `strings.txt:16048` → `Items altered in system`; `strings.txt:16044` → `Items missing from system` | interface | alto |
| 79 | **Customizacao dos botoes** da barra e legendas (AUSENTE) | `strings.txt:16594` → `Add or Remove Buttons`; `strings.txt:16995` → `Button caption`; `strings.txt:16996` → `(Show or hide the toolbar button captions` | interface | baixo |
| 82 | **Deteccao da localizacao de instalacao** (PARCIAL) | `strings.txt:15746` → `Installation location detection`; `inventario.md:273` → `Installation locations` | varredura | medio |
| 83 | **Dependencias entre pacotes MSI** (AUSENTE) | `strings.txt:50864` → `Depends on packages&Uninstall the selected application(s)?%Error uninstalling %d application(s).` | integracao | alto |
| 84 | **Verificacao de DCOM** do produto (AUSENTE) | `strings.txt:51107` → `DCOM not installed` | interface | baixo |
| 85 | Painel/listagem propria de **servicos e dispositivos instalados** (PARCIAL) | `strings.txt:51061` → `Installed services and devices`; `inventario.md:9` → `tabAutorunViews` | interface | medio |
| 87 | **Historico de instalacoes** (PARCIAL) | `strings.txt:50901` → `Install history` | relatorio | medio |
| 88 | **Retomar o monitoramento inacabado** no arranque do Windows (AUSENTE) | `strings.txt:15827` → `AAuto-start with Windows to continue unfinished monitoring process` | automacao | medio |
| 90 | Distinguir **programa novo** de **programa atualizado** na lista (PARCIAL) | `strings.txt:15625` → `New or updated program` | interface | medio |
| 93 | **Acompanhar o tema do Explorer** (PARCIAL) | `strings.txt:15017` → `toUseExplorerTheme`; `strings.txt:14682` → `Vcl.Themes` | interface | baixo |
| 94 | **Licenca por familia / multiplos computadores / avaliacao / manutencao expirada** (AUSENTE) | `strings.txt:50980` -> `Family License`; `strings.txt:50982` -> `Multiple Computers License`; `strings.txt:50913` -> `Family license is not valid.4The maintenance period for your license has expired.` | integracao | baixo |

**Total: 41 lacunas** — 22 `AUSENTE` e 19 `PARCIAL`, das 94 funcionalidades mapeadas na tabela de `TOTALUNINSTALL.md`.

### Observacoes de impacto

- **Alto**: 17, 22, 32, 37, 46, 65, 66, 78, 83 — sao diferencas de produto, nao de acabamento. O Tranal nao tem revisao granular por perfil, arvore de mudancas, undo/redo de restauracao, contadores de referencia compartilhados, instalacao monitorada de programa novo/atualizacao, nem resolucao de dependencias MSI.
- **Baixo**: 15, 45, 61, 79, 84, 93 —sao cosmeticamente irrelevantes; 60 e 94 (licenca) nao tem impacto porque o Tranal e free e sem licenca.
