# Lacunas - HiBit Uninstaller

Binario analisado: `workspace/auditoria/hibit/app/setup-inno/app/HiBitUninstaller.exe` (15.410.176 B)  sha256: `89092212bd920b84d235a5d4ea89ccaa7f64ec32fcaf769f1e9cdb8070c3dcea`
Metodo: inventario estatico do PE32 (Delphi/Embarcadero, **nao empacotado**) — classes RTTI, rotulos de interface, **recursos de formulario Delphi** (214 entradas, das quais **62 formularios** `T*FORM` e 7 estilos VCL) e identificadores de tipo/metodo; cruzamento com o dump bruto de strings. Fontes: `bin-inventario/hibit.inventario.md`, `bin-inventario/hibit.recursos.txt`, `bin-inventario/hibit.identificadores.txt`, `bin-strings/hibit.strings.txt`.
A coluna `#` abaixo e o **numero da linha na tabela de funcionalidades** de `HIBIT.md` (secao 2). Lista exata: todas as linhas `AUSENTE` e `PARCIAL` da secao 2, uma a uma.

## Funcionalidades presentes no binario e ausentes no Tranal

| # | Funcionalidade | Evidencia no binario | Categoria | Impacto (alto/medio/baixo) |
|---|---|---|---|---|
| 1 | Lista com **colunas completas**: falta a coluna de **Tipo (32bit/64bit)** e a de **local de instalacao** | `hibit.recursos.txt:191` -> `TUNINSTALLER/0`; `:159` -> `TEDITINFOFORM/0` | interface | medio |
| 3 | **Desinstalacao em lote** com selecao multipla na grade | nao confirmado no binario (literal de lote nao localizado nesta rodada) | automacao | alto |
| 4 | **Desinstalacao forcada reagendada** para o proximo logon via `RunOnce` | `hibit.recursos.txt:171` -> `TFORCEUNINSTALLFORM/0`; `hibit.inventario.md:13448` -> `Schedule` - Agenda execucao | automacao | alto |
| 5 | **Varredura de sobras em arvore** por categoria (registro / arquivo / tarefa / servico) | `hibit.recursos.txt:181` -> `TREGISTRYCLEANERFORM/0`; `hibit.inventario.md:13463` -> `Restore` - Restaura backups | varredura | medio |
| 6 | **Backup proprio `.dbh`** (`BackupFiles.zip` + `FileList.txt` + `Registry.ini`) com restauracao | `hibit.recursos.txt:149` -> `TBACKUPFORM/0`; `:148` -> `TBACKUPDETAILSFORM/0` | recuperacao | medio |
| 8 | **Relatorios HTML por ferramenta** (`OpReport`, `ScanLog`, `ServicesReport`, `DriversReport`, `StartupReport`, `SchedulerReport`, `ContextMenuReport`, `UninstallerReport`) | `hibit.recursos.txt:164` -> `TEXPORTFORM/0`; `hibit.inventario.md:13469` -> `Report` - Gera relatorios | relatorio | medio |
| 9 | **Ocultar entrada** de desinstalacao ("Hide original uninstallation entry") | nao confirmado no binario | interface | baixo |
| 10 | **Deletar a entrada do registro** do programa como acao avulsa | `hibit.recursos.txt:192` -> `TUNINSTALLFORM/0`; `hibit.inventario.md:13432` -> `RegDeleteKey` | remocao | medio |
| 11 | **Editar informacoes** do programa | `hibit.recursos.txt:159` -> `TEDITINFOFORM/0` | interface | baixo |
| 13 | **Copiar dados** do programa para a area de transferencia | `hibit.recursos.txt:166` -> `TFILEINFOFORM/0` | interface | baixo |
| 15 | **Modificar/reparar** instalacao MSI (`ModifyPath`, `MsiExec.exe /I`) | `hibit.inventario.md:13448` -> `msiexec` - Desinstalacao via msiexec; `hibit.recursos.txt:180` -> `TPROGRAMCOMPONENTSFORM/0` | remocao | medio |
| 17 | **Menu de contexto do Monitor de Instalacao** (entrada separada no shell) | `hibit.recursos.txt:172` -> `TINSTALLMONITORFORM/0`; `:173` -> `TINSTALLMONITORIGNOREFORM/0` | integracao | medio |
| 18 | **Gerenciador do Menu de Contexto** — lista, cria e remove menus do shell, com "Restaurar" e "Excluir tudo" | `hibit.recursos.txt:152` -> `TCONTEXTMENUFORM/0`; `:153` -> `TCREATECONTEXTMFORM/0` | organizacao | alto |
| 20 | **Monitoramento de instalacao** com eventos por tipo (`ApplicationInstalled`, `ApplicationUninstalled`, `ApplicationDeleteRestore`, `DeviceInstalled`) | `hibit.recursos.txt:172` -> `TINSTALLMONITORFORM/0`; `hibit.inventario.md:13471` -> `Monitor` - Monitora em segundo plano | automacao | alto |
| 21 | Desinstalar **atualizacoes do Windows** (`wusa` / KB) | `hibit.inventario.md:13446` -> `Windows Update` - Interage com Windows Update; `hibit.recursos.txt:197` -> `TWINUPDATEFORM/0` | remocao | alto |
| 59 | **Arrastar e soltar** um programa para desinstalar | `hibit.recursos.txt:12` -> `12/CAT_DRAG_COPY/1033` (cursor de arrastar) | interface | medio |
| 60 | **Painel de informacoes do sistema** no banner (CPU, GPU, RAM, tempo de atividade) | nao confirmado no binario | interface | baixo |
| 61 | **Seletor de tema na barra superior**, com amostras de cor | `hibit.recursos.txt:6-12` -> `VCLSTYLE/CARBON` / `COBALTXEMEDIA` / `ICEBERGCLASSICO` / `LUNA` / `TABLETDARK` / `WINDOWSMODERNDARK` / `WINDOWSSLATEGRAY` (7 temas VCL no PE) | interface | baixo |
| 62 | **Botao Doar** | nao confirmado no binario | interface | baixo |
| 63 | **Data de instalacao em tempo relativo** ("Hoje", "Ontem", "1 mes atras") | nao confirmado no binario (captura de tela, nao string do binario) | interface | medio |
| 64 | **Coluna Tipo (32bit / 64bit)** por programa | nao confirmado no binario | interface | medio |
| 65 | **Contador de tamanho total** dos programas na barra de status | `hibit.inventario.md:13485` -> `Size` - Mede tamanho em disco | interface | medio |
| 66 | **Painel de detalhes** do programa (local de instalacao, entrada de registro, website) | `hibit.recursos.txt:166` -> `TFILEINFOFORM/0` | interface | medio |
| 67 | **Gerenciador de Inicializacao** que cria e edita itens de autostart (o Tranal so lista) | `hibit.recursos.txt:188` -> `TSTARTUPMANAGERFORM/0`; `:145` -> `TADDSTARTUPFORM/0`; `:160` -> `TEDITSTARTUPFORM/0` | organizacao | alto |
| 68 | **Gerenciador de Servicos** que cria servico (o Tranal so gerencia os existentes) | `hibit.recursos.txt:185` -> `TSERVICESFORM/0`; `:144` -> `TADDSERVICESFORM/0`; `hibit.inventario.md:13435` -> `CreateService` | organizacao | alto |
| 69 | **Gerenciador de Processos** (painel) | `hibit.recursos.txt:178` -> `TPROCESSESMANAGERFORM/0` | interface | medio |
| 70 | **Gerenciador de Tarefas Agendadas** | `hibit.recursos.txt:184` -> `TSCHEDULETASKFROM/0`; `hibit.inventario.md:13438` -> `TaskScheduler` - Agenda tarefas | organizacao | alto |
| 71 | **Gerenciador de Menu de Contexto** (janela propria, com "Restaurar" e "Excluir tudo") | `hibit.recursos.txt:152` -> `TCONTEXTMENUFORM/0` | organizacao | medio |
| 72 | **Gerenciador de Componentes de Programas** (DLLs compartilhadas) | `hibit.recursos.txt:180` -> `TPROGRAMCOMPONENTSFORM/0` | organizacao | medio |
| 73 | **Gerenciador de Extensoes de Navegador** para mais navegadores que Chrome/Edge/Firefox/Opera | `hibit.recursos.txt:150` -> `TBROWSEREXTFORM/0` | remocao | medio |
| 74 | **Gerenciador de Conexoes** (rede) | `hibit.recursos.txt:151` -> `TCONNECTIONMANAGERFORM/0` | interface | medio |
| 75 | **Backups & Reports** (painel proprio de backup e de relatorio) | `hibit.recursos.txt:149` -> `TBACKUPFORM/0`; `:148` -> `TBACKUPDETAILSFORM/0`; `:164` -> `TEXPORTFORM/0` | relatorio | medio |
| 76 | **Restaurador do Sistema** (criar/excluir pontos com um clique) | `hibit.recursos.txt:189` -> `TSYSRESTOREFORM/0`; `:146` -> `TADDSYSRESTOREFORM/0`; `hibit.inventario.md:13441` -> `SRSetRestorePoint` | recuperacao | alto |
| 77 | **Limpador de Registro** com 15+ categorias (ActiveX, FileTypes, SharedDLLs, IFEO, FirewallRules, MUICache) | `hibit.recursos.txt:181` -> `TREGISTRYCLEANERFORM/0` | remocao | alto |
| 78 | **Limpador de Arquivos de Lixo** com 17 categorias | `hibit.recursos.txt:174` -> `TJFCLEANERFORM/0`; `:175` -> `TJFSETTINGFORM/0`; `hibit.identificadores.txt:169` -> `TList<JunkFilesCleaner.TCleanFile.CleanS.TFileInfo>` | remocao | alto |
| 80 | **Fixador de Atalhos** (atalhos corrompidos) | `hibit.recursos.txt:187` -> `TSHORTCUTFIXERFORM/0` | organizacao | medio |
| 81 | **Destruidor de Arquivos** com 5 algoritmos (MD5 / SHA-1 / SHA-256 / SHA-512 / CRC32) e relatorio de hash | `hibit.recursos.txt:168` -> `TFILESHREDDERFORM/0`; `hibit.inventario.md:13486` -> `Shred` - Apaga de forma segura | remocao | medio |
| 82 | **Localizador de Arquivos e Registros** | `hibit.recursos.txt:167` -> `TFILEREGFINDERFORM/0` | varredura | medio |
| 83 | **Localizador de Arquivos Duplicados** | `hibit.recursos.txt:158` -> `TDUPLICATEFILEFINDERFORM/0`; `hibit.inventario.md:13484` -> `Duplicate` - Detecta duplicados | varredura | medio |
| 84 | **Analisador de disco** (por arquivo, nao por programa) | `hibit.recursos.txt:157` -> `TDISKANALYZERFORM/0`; `hibit.inventario.md:13485` -> `Size` - Mede tamanho em disco | interface | medio |
| 86 | **Gerenciador de Atualizacoes do Windows** (painel proprio) | `hibit.recursos.txt:197` -> `TWINUPDATEFORM/0`; `hibit.inventario.md:13446` -> `Windows Update` | organizacao | alto |
| 87 | **Propriedades de processo** (painel dedicado) | `hibit.recursos.txt:179` -> `TPROCPROPERTIFORM/0` | interface | baixo |
| 88 | **Afinidade de processador** do programa | `hibit.recursos.txt:147` -> `TAFFINITYFORM/0`; `hibit.strings.txt:25783` -> `GetProcessAffinityMask`; `:25791` -> `SetProcessAffinityMask`; `:70074` -> `Affinity1.Caption=Affinity` | interface | baixo |
| 89 | **Scanner VirusTotal** (verificacao de arquivo na nuvem) | `hibit.recursos.txt:194` -> `TVIRUSTOTALAPIFORM/0`; `:195` -> `TVIRUSTOTALSCANFORM/0`; `hibit.strings.txt:18270` -> `VirusTotal_API`; `:22823` -> `AddVirusTotalScannertoContextMenu1` | integracao | medio |
| 90 | Integracao com o **Windows Defender** (cache, limpeza, varredura agendada) | `hibit.strings.txt:65936` -> `Defender`; `:67985` -> `Windows Defender Cache Maintenance`; `:67987` -> `Windows Defender Scheduled Scan`; `hibit.inventario.md:13456` -> `Defender` - Integra-se ao Defender | integracao | medio |
| 91 | **Criar item de inicializacao** do zero | `hibit.recursos.txt:145` -> `TADDSTARTUPFORM/0`; `:160` -> `TEDITSTARTUPFORM/0` | organizacao | medio |
| 92 | **Criar servico** do Windows | `hibit.recursos.txt:144` -> `TADDSERVICESFORM/0`; `hibit.inventario.md:13435` -> `CreateService` | organizacao | medio |
| 93 | **Criar tarefa agendada** | `hibit.recursos.txt:154` -> `TCREATETASKFORM/0` | automacao | medio |
| 94 | **Criar menu de contexto** no Explorer | `hibit.recursos.txt:153` -> `TCREATECONTEXTMFORM/0` | integracao | medio |
| 95 | **Limpeza personalizada** (regras proprias por ferramenta) | `hibit.recursos.txt:142` -> `TADDCUSTOMCLEANFORM/0`; `:155` -> `TCUSTOMCLEANFORM/0`; `hibit.strings.txt:57758` -> `UCustomClean` | organizacao | alto |
| 96 | **Listas de exclusao / ignorar** por ferramenta (monitor, registro, junk, atalhos) | `hibit.recursos.txt:143` -> `TADDIGNOREMONITORFORM/0`; `:163` -> `TEXCLUSIONLISTFORM/0`; `:169` -> `TFJIGNOREFORM/0`; `:186` -> `TSHORTCATIGNORELISTFORM/0`; `hibit.strings.txt:19967` -> `RegIgnoreList` | organizacao | medio |
| 97 | **Restaurar o registro** a partir do backup | `hibit.recursos.txt:182` -> `TREGRESTOREFORM/0` | recuperacao | alto |
| 98 | **Verificacao de integridade de arquivo** e limpeza pontual de arquivo | `hibit.recursos.txt:165` -> `TFILECHECKSFORM/0`; `:166` -> `TFILEINFOFORM/0`; `:156` -> `TDELETEFILEFORM/0` | varredura | medio |
| 99 | **Sete temas VCL** embutidos no binario | `hibit.recursos.txt:6-12` -> `VCLSTYLE/CARBON` / `COBALTXEMEDIA` / `ICEBERGCLASSICO` / `LUNA` / `TABLETDARK` / `WINDOWSMODERNDARK` / `WINDOWSSLATEGRAY` | interface | baixo |
| 100 | **Banco local embutido** (`LOCALDB`) | `hibit.recursos.txt:127` -> `LOCALDB/1033` | organizacao | baixo |
| 101 | **Salvar log** de ferramenta em arquivo | `hibit.recursos.txt:176` -> `TLOGSAVEFORM/0`; `hibit.inventario.md:13466` -> `Log` - Mantem registro/log | relatorio | baixo |

## Observacoes

- **A evidencia mais forte do HiBit e o nome do formulario Delphi.** Sao 62
  recursos `T*FORM` no PE; cada um e uma janela do produto. Isso torna a
  suite de 21+ ferramentas verificavel **sem executar nada** — e e o que
  distingue esta curadoria das anteriores, que dependiam de captura de tela.
- As quatro janelas de **criacao** (`TADDSERVICESFORM`, `TADDSTARTUPFORM`,
  `TCREATETASKFORM`, `TCREATECONTEXTMFORM`) formam um bloco coerente: o
  HiBit nao so **gerencia** o que o Windows tem, ele **cria**. O Tranal hoje
  so gerencia. Impacto alto, porque e a mesma diferenca entre "listar
  programas" e "cuidar do PC".
- Fechadas nesta rodada por existirem no codigo do Tranal: switch de CLI por
  ferramenta com sufixo `RUN` (`Core/Automation/CliToolSwitch.cs`), tabela de
  switch silencioso por familia (`Core/QuietUninstall.cs`), desabilitar
  handlers de menu de contexto (`Core/Management/ContextMenuManager.cs`),
  menu de contexto do Explorer (`Core/Topmost/ExplorerContextMenu.cs`),
  remover Store/MSIX (`Core/Modules/WindowsApps.cs`), pastas vazias
  (`Core/Discovery/EmptyFolderCleaner.cs`), backup e restauracao
  (`Core/BackupManager.cs`, `Core/Recovery/RecoverOperation.cs`), ponto de
  restauracao (`Core/UninstallSupport.cs:1210`) e exportacao
  (`Core/Exporters.cs`).
- As linhas marcadas "nao confirmado no binario" vem de captura de tela ou do
  `.ini` de idioma, nao do executavel: sao pendentes de confirmacao, nao
  ausencias provadas.
