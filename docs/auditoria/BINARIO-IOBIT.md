# Lacunas - IObit Uninstaller

Binario analisado: `workspace/auditoria/iobit/app/payload/IObitUninstaler.exe` (10.646.976 B)  sha256: `320c59f6d0f29af8a75342fc0242c4bf5dffa100d300a25666c514eb84ec2617`
Metodo: inventario estatico do PE32 (Delphi/VCL) — classes RTTI, rotulos de interface, **recursos de formulario Delphi** (194 entradas) e identificadores de tipo/metodo; cruzamento com o dump bruto de strings do executavel. Fontes: `bin-inventario/iobit.inventario.md`, `bin-inventario/iobit.recursos.txt`, `bin-inventario/iobit.identificadores.txt`, `bin-strings/iobit.strings.txt`.
A coluna `#` abaixo e o **numero da linha na tabela de funcionalidades** de `IOBIT.md` (secao 2). Lista exata: todas as linhas `AUSENTE` e `PARCIAL` da secao 2, uma a uma.

## Funcionalidades presentes no binario e ausentes no Tranal

| # | Funcionalidade | Evidencia no binario | Categoria | Impacto (alto/medio/baixo) |
|---|---|---|---|---|
| 1 | Remocao de extensoes de navegador **com base de assinaturas** maliciosa/ad (base `PlugDB.dbd` de 1,6 MB, 5 navegadores com recurso proprio `PLUGIN_*`) | `iobit.strings.txt:37337` -> `PlugDB.dbd`; `iobit.recursos.txt:109-114` -> `PLUGIN_CHROME`/`PLUGIN_DEFAULT`/`PLUGIN_EDGE`/`PLUGIN_FIREFOX`/`PLUGIN_IE`/`PLUGIN_OPERA`; `iobit.inventario.md:9` -> `TADPluginDBHelper` | remocao | alto |
| 2 | Classificacao da extensao em **Malicious / Ad / Trusted** | `iobit.strings.txt:40554` -> `Malicious Extension`; `:40556` -> `Ad Extension`; `:34156` -> `Ad-based Browser Extensions`; `iobit.inventario.md:9` -> `TADPluginData` | varredura | alto |
| 3 | Limpeza de navegador com **reset de pagina inicial e motor de busca** | `iobit.recursos.txt:147` -> `TFRABROWSERCLEANER/0`; `iobit.inventario.md:9` -> `TAllBrowserCleaner` | remocao | medio |
| 4 | Remocao de **plugins** de navegador (NPAPI/ActiveX, recurso `PLUGIN_DEFAULT`) | `iobit.recursos.txt:110` -> `PLUGIN_DEFAULT/0`; `iobit.inventario.md:9` -> `TADPluginData` | remocao | alto |
| 5 | **Bloatware Remover** com base propria (`BloatApp.dll`) e lista de Appx pre-instalados do Windows | `iobit.strings.txt:46869` -> `Bloatware Remover`; `:34662` -> ` Bloatware Remover`; `:40142` -> `BloatApp.dll` | varredura | alto |
| 6 | **Bundleware**: detectar o que veio junto com o programa e desinstalar em conjunto (formulario e helper proprios) | `iobit.strings.txt:34695` -> `~Bundleware is software that includes other programs that may be installed without your notice when you install a main program.`; `:9872` -> `Tfrm_UninstallBundle`; `:9874` -> `TUninstallBundleHelper`; `iobit.recursos.txt:154` -> `TFRAME_BUNDLE/0`; `:181` -> `TFRM_UNINSTALLBUNDLE/0` | varredura | alto |
| 7 | **Stubborn Program Remover** com base de 4.000+ programas travados e passos de remocao conhecidos | `iobit.strings.txt:11326` -> `TStubbornData`; `:11327` -> `StubbornHelper`; `:11330` -> `TStubbronUninstallManager`; `:11332` -> `TStubbornCheckThread`; `iobit.recursos.txt:121` -> `TFORM_CHECKSTUBBRON/0`; `:127` -> `TFORM_FORCESTUBBRON/0`; `:142` -> `TFORM_STUBBORNTOOL/0` | remocao | medio |
| 8 | **Force Uninstall+**: desinstalar programa **sem entrada de registro** | `iobit.recursos.txt:162` -> `TFRMFORCEDUNINSTALL/0`; `iobit.inventario.md:9` -> `TAppsRemoval`, `TAnalysisThread` | remocao | medio |
| 9 | **Quick Uninstall**: confirmar automaticamente o desinstalador original | nao confirmado no binario (literal de confirmacao automatica nao localizado) | automacao | baixo |
| 10 | Aviso de que o desinstalador **vai deixar residuo** | nao confirmado no binario | interface | medio |
| 11 | Confirmar/avisar antes de apagar **arquivo pessoal** | nao confirmado no binario | interface | baixo |
| 12 | **Sobras por categoria** (Registro / Arquivo / Tarefas / Servicos / DLL) | `iobit.identificadores.txt:113` -> `ScanItem.FilePath.Count:`; `:114` -> `ScanItem.RegRoot.Count:` | varredura | medio |
| 16 | **Desinstalacao em lote** (multi-selecao) | nao confirmado no binario (literal de lote nao localizado nesta rodada) | automacao | alto |
| 17 | **Easy Uninstall**: arrastar o icone do programa para a janela | `iobit.inventario.md:82` -> `AllowMeesageForVistaAbove WM_DROPFILES Failed` | interface | medio |
| 18 | **Install Monitor** com log por categoria (registro, arquivo, tarefa, servico, DLL, inicializacao) e **restauracao** do que foi gravado | `iobit.recursos.txt:163` -> `TFRMINSTALLMONITOR/0`; `:166` -> `TFRMMONITORLOG/0`; `:184` -> `TIUINSTALLMONITORFRM/0`; `iobit.strings.txt:35860` -> `Install Monitor detects and logs system changes in installation, which ensures all changes can be restored in future thorough uninstall.` | automacao | alto |
| 19 | **Log de instalacao em SQLite embarcado** | `iobit.inventario.md:92` -> `An internal logic error in SQLite` | organizacao | baixo |
| 20 | **Desinstalar pelo log de instalacao** (o log do Install Monitor vira a fonte da remocao) | `iobit.strings.txt:44602` -> `Programs logged by Install Monitor can be uninstalled thoroughly in future uninstallation.`; `:44974` -> `UninstallMonitor.exe` | automacao | alto |
| 21 | Monitor de instalacao em **segundo plano no startup** | nao confirmado no binario (a chave `Run` e artefato de execucao, nao do binario) | automacao | alto |
| 22 | **File Shredder** com 3 modos de sobrescrita | `iobit.recursos.txt:126` -> `TFORM_FILESHARDDER/0`; `iobit.strings.txt:44161` -> `FileShredder`; `:28741` -> `fUnit_FileShredder`; `:46986` -> `ToolPopFileShredder` | remocao | medio |
| 23 | Botao **Shred no Explorer** (handler de menu de contexto) | `iobit.inventario.md:83` -> `AllowMeesageForVistaAbove WM_ExplorerShredder Failed`; `:84` -> `WM_MenuRightStart` | integracao | medio |
| 24 | **Software Relocator**: mover software instalado entre discos | `iobit.strings.txt:13482` -> `Frame_SoftRelocator`; `:13525` -> `btn_StartSoftMove`; `:13500` -> `TSoftMoveThread`; `:12902` -> `TSoftMoveLan`; `iobit.recursos.txt:136` -> `TFORM_MAINSOFTMOVE/0` | automacao | medio |
| 25 | **Bloqueio de popups** de notificacao | nao confirmado no binario | integracao | medio |
| 27 | **Software Updater** de programas de terceiros (atualizar todos) | `iobit.recursos.txt:141` -> `TFORM_SOFTWAREUPDATER/0`; `iobit.strings.txt:11558` -> `SoftwareUpdaterForm`; `:11601` -> `TForm_SoftwareUpdater` | integracao | medio |
| 28 | Remocao de item **fixado na barra de tarefas** e de Jump Lists | `iobit.strings.txt:28639` -> `TaskbarPin`; `:45597` -> `taskbarunpin`; `:45602` -> `TaskbarPin:FilePath=`; `:45961` -> `TaskbarJumpLists` | remocao | medio |
| 29 | Desinstalar **Windows Updates** | `iobit.inventario.md:3023` -> `Windows Update` - Interage com Windows Update (token de API ativo) | remocao | alto |
| 32 | Verificacao de **programas relacionados** antes de desinstalar | `iobit.recursos.txt:174` -> `TFRM_CHECKRELATEDFORM/0` | varredura | medio |
| 33 | Aviso de **sincronizacao do Chrome** ao remover extensao | `iobit.recursos.txt:175` -> `TFRM_CHROMESYNCNOTICE/0` | interface | baixo |
| 34 | Integracao com a **Central de Acoes do Windows** (Action Center) | `iobit.recursos.txt:148` -> `TFRAMEACTIONCENTER8/0`; `iobit.identificadores.txt:115` -> `TActionCenterAppList.FAppList.Add` | integracao | baixo |
| 35 | Gerencia de **tarefas agendadas** via COM Task Scheduler | `iobit.identificadores.txt:118` -> `TTaskSchedulerInterfaces1.IniTaskScheduler.CoCreateInstance`; `iobit.inventario.md:3012` -> `schtasks`; `:3013` -> `TaskScheduler` | organizacao | medio |
| 36 | Voto / avaliacao de programas e plugins pela comunidade | `iobit.recursos.txt:135` -> `TFORM_NEWAPPVOTE/0`; `:167` -> `TFRMPLUGINVOTE/0`; `:168` -> `TFRMPROMOTEVOTE/0` | interface | baixo |
| 37 | Captura de tela do sistema anexada ao fluxo de suporte | `iobit.recursos.txt:188` -> `TMESCRSHOTFORM/0` | interface | baixo |
| 38 | **Catalogo embutido de Appx pre-instalados** (a lista do que o Windows traz de fabrica) | `iobit.identificadores.txt:103` -> `Microsoft.Windows.Ai.Copilot.Provider`; `:107` -> `Microsoft.Windows.DevHome`; `:98` -> `Microsoft.WebView2Runtime.Stable_8wekyb3d8bbwe`; `:110` -> `Microsoft.Xbox.TCUI` | varredura | medio |
| 39 | **Gerenciador de licenca** do proprio produto (ativar PRO) | `iobit.recursos.txt:133` -> `TFORM_LICENSEMANAGER/0`; `:134` -> `TFORM_LICENSEMESSAGE/0` | interface | baixo |
| 40 | Tela de **dados de uso** do programa (telemetria) | `iobit.recursos.txt:123` -> `TFORM_DISPLAYDATA/0`; `:122` -> `TFORM_EXPRIED/0` | relatorio | baixo |

## Observacoes

- **Impacto alto = diferenca real de produto**: sao itens que um usuario
  profissional esperaria de um desinstalador comercial e que o Tranal ainda
  nao entrega.
- As linhas marcadas "nao confirmado no binario" sao funcionalidades cujo
  unico apoio era o `Language\English.lng` (que **nao** e o binario). A
  ausencia de literal no executavel **nao prova** a ausencia do recurso —
  por isso elas entram na lista como pendentes de confirmacao, nao como
  ausencias provadas.
- **Divergencia intencional**: a linha 39 (licenca) e a linha 40
  (telemetria) nao sao backlog — o Tranal nao comercializa e nao tem
  telemetria por decisao. Aparecem aqui para registro.
- Fechadas nesta rodada por existirem no codigo do Tranal: Hunter Mode
  (`Core/Modules/HunterMode.cs`), Software Permissions
  (`Core/Management/AppPermissionManager.cs`), ponto de restauracao
  (`Core/UninstallSupport.cs`), historico de desinstalacao
  (`Core/UninstallHistory.cs`), auto-clean de residuos
  (`Core/UninstallSupport.cs`) e lixeira (`Core/RecycleBin.cs`).
