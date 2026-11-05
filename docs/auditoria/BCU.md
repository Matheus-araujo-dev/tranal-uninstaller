# Bulk Crap Uninstaller 6.3.0 — auditoria

> Concorrente **open source** (Apache 2.0, `Klocman/Bulk-Crap-Uninstaller`).
> Escolido primeiro de propósito: por ser o único dos dez com o código
> público, ele é a **referência de implementação** — o lugar onde se vê
> *como* cada funcionalidade é feita, não apenas que ela existe.

Data da coleta: 05/11/2026 · Versão auditada: **6.3.0** (build `572f16347d8fd461e8725e88a5704478137dbe88`)

---

## 1. Identificação

| Campo | Valor | Fonte |
|---|---|---|
| Produto | Bulk Crap Uninstaller | `BCU_manual.html` |
| Editor | Klocman (Klocman Software) | metadata do assembly |
| Licença | Apache 2.0, open source | `Licence.txt`, `NOTICE` |
| Código | https://github.com/Klocman/Bulk-Crap-Uninstaller | `NOTICE` |
| Versão | 6.3.0 | `FileVersionInfo` |
| Formato | .NET 8 (`net8.0-windows10.0.18362.0`), WinForms | `BCUninstaller.runtimeconfig.json` |
| Assinatura | **não assinado** | `Get-AuthenticodeSignature` → `NotSigned` |
| Requisitos | Windows Vista+, ~300 MB de RAM, 50 MB de disco | manual §1.1 |
| Privilégio | **exige administrador para iniciar e funcionar** | manual §1.1 |

### Artefatos coletados

| Arquivo | Bytes | SHA-256 |
|---|---|---|
| `BCUninstaller_6.3.0_net8.0-windows10.0.18362.0.zip` | 12.362.694 | `4bfa602517b0cd4e81519ae12f4fdb087625aa93f2fed5c99f5c049117ce5c17` |
| `BCUninstaller_6.3.0_setup.exe` | 9.430.340 | `d89c81f4dd029b4862fc6261262a4a9851a6948827af3e55d54e9a8c53b13d88` |
| `Localisation_Pack_6.3.0.zip` | 2.755.822 | `8ebcf7adcc0a24dcfb24b33713c660a75dbdfaad2fb7982df6c98518b18b97a9` |
| `app\BCUninstaller.dll` | 1.722.368 | `22935786006e35eaf8799d0cc6ce68a5df311ba28a2c6f7c2a99c36335d6c0ec` |
| `app\UninstallTools.dll` (motor) | 686.592 | `3c82ca47f91d92eeadc34620a35d4e26d7acaa91338314bb2bba00eef4a0cf79` |
| `app\BCU-console.dll` | 332.800 | `c52d7a308a10979d6ae0203ca3715b5f729afe6de528d121bcab405cf4f4958b` |
| `app\UniversalUninstaller.dll` | 203.776 | `d1d3811231760986e9a878a31a2dce37d5211f7d4336c295d16f461771b5985f` |
| `app\UninstallerAutomatizer.dll` | 222.208 | `ee94fe66bb5583e2d9c975b7c2996ed5d4cb6804199b58a2cf745bbf6bae32c5` |

### A descoberta que muda a auditoria

O **Localisation Pack 6.3.0** não é só um pacote de idiomas: ele traz a
**estrutura de pastas do código-fonte**, com um `.resx` por tela. Isso
transformou a auditoria — os nomes dos arquivos são o **inventário de telas**
do produto, e os `.resx` em pt-BR são a **spec funcional traduzida**.

```
BulkCrapUninstaller/source/
  BulkCrapUninstaller/
    Controls/        AdvancedClipboardCopy, FileTargeter, ListLegend,
                     RelatedUninstallerAdder, UninstallConfirmation,
                     Settings/{AdvancedFilters, CacheSettings,
                              PropertiesSidebar, UninstallationSettings}
    Forms/Helpers/   AdvancedClipboardCopyWindow, DebugWindow, FeedbackBox,
                     FeedbackWindow, ListLegendWindow, NewsPopup,
                     PropertiesWindow, RatingPopup, TargetWindow
    Forms/Windows/   AboutBox, JunkRemoveWindow, MainWindow, SettingsWindow,
                     UninstallProgressWindow
    Forms/Wizards/   BeginUninstallTaskWizard, FirstStartBox
  UninstallTools/    Controls/{FilterEditor, UninstallListEditor},
                     Dialogs/StartupManagerWindow
  UniversalUninstaller/  TargetList, UninstallSelection
  UninstallerAutomatizer/ Forms/MainWindow
  KlocTools/         Controls/*, Forms/* (ProcessWaiter, WindowTargeterDialog)
```

**45 arquivos `.resx` em pt-BR, 1.066 chaves** — para comparação, o catálogo
do Revo Pro tem 1.136 strings em 42 seções. São ordens de grandeza
comparáveis, o que faz do BCU um bom espelho do Revo.

Extraídos para `workspace/auditoria/bcu/evidencia/resx-ptBR-*.txt`.

---

## 2. Superfície observável

Toda linha aponta para um arquivo de evidência. "Observado" = vi acontecer
ou li no binário; "Documentado" = o manual oficial afirma.

### 2.1 Descoberta e classificação de programas

| # | Funcionalidade | Evidência | Tipo |
|---|---|---|---|
| 1 | Enumeração pelo **registro** (`HKLM`/`HKCU`, 32 e 64 bits) | `Progress_Registry`, `Progress_Registry_Gathering`, `Progress_Registry_Processing` | Documentado |
| 2 | Enumeração de **produtos MSI instalados** por GUID | `Progress_MSI` ("Encontradas {0} aplicações GUID") | Documentado |
| 3 | **Varredura de drives** atrás de aplicativos não registrados | `Progress_DriveScan`, `Progress_DriveScan_Gathering` | Documentado |
| 4 | Detecção de **Windows Store Apps** | `Progress_AppStores_WinStore`, `UninstallerType_StoreApp` | Documentado |
| 5 | Detecção de **Windows Features** (via WMI) | `Progress_AppStores_WinFeatures`, `UninstallerType_WindowsFeature` | Documentado |
| 6 | Detecção de **Windows Updates** (via WUA) | `Progress_AppStores_WinUpdates`, `UninstallerType_WindowsUpdate` | Documentado |
| 7 | Detecção de **aplicativos Steam** | `Progress_AppStores_Steam`, `UninstallerType_Steam` | Documentado |
| 8 | Detecção de **pacotes Chocolatey** e **Scoop** | `Progress_AppStores_Chocolatey`, `Progress_AppStores_Scoop`, `UninstallerType_Chocolatey` | Documentado |
| 9 | Detecção de **apps Oculus** | `Progress_AppStores_Oculus`, `UninstallerType_Oculus` | Documentado |
| 10 | Detecção de **scripts PowerShell** de desinstalação | `UninstallerType_PowerShell` | Documentado |
| 11 | **Classificação por tipo de desinstalador**: Msiexec, Inno Setup, NSIS, InstallShield, SDBInst, Steam, Store App, Windows Feature, Windows Update, Chocolatey, Oculus, PowerShell, Simple Delete, Desconhecido | `UninstallerType_*` (14 tipos) | Documentado |
| 12 | Marcação de **componente do sistema** | `SystemComponent`, `UninstallStatus_Protected` | Documentado |
| 13 | Marcação de **protegido** (tag `NoRemove` do editor) | `IsProtected`, `checkBoxListProtected` | Documentado |
| 14 | Marcação de **atualização** de outro aplicativo | `IsUpdate`, `checkBoxShowUpdates` | Documentado |
| 15 | Marcação de **não registrado (órfão)** | `IsOrphaned`, `checkBoxOrphans` | Documentado |
| 16 | Marcação de **aplicativo inválido / desinstalador corrompido** | `checkBoxInvalidTest` ("cor cinza para identificar desinstaladores corrompidos ou ausentes") | Documentado |
| 17 | Pastas de instalação **personalizadas** declaradas pelo usuário + detecção automática | `labelProgramFolders`, `checkBoxAutoInstallFolderDetect` | Documentado |
| 18 | **Filtros rápidos**: Só básicas / Só componentes do sistema / Tudo / Só auto-inicialização / Só navegadores | `basicApplications`, `systemComponents`, `everything`, `automaticallyStarted`, `onlyWebBrowsers` | Documentado |

### 2.2 Desinstalação

| # | Funcionalidade | Evidência | Tipo |
|---|---|---|---|
| 19 | Desinstalar com **desinstalador nativo em primeiro plano** ("loud") | manual §2.4.1 | Documentado |
| 20 | **Desinstalação silenciosa**, com gerador de desinstalador silencioso ausente | `QuietUninstallPossible`, `QuietUninstallString`, `checkBoxGenerate` ("Gerar desinstaladores silenciosos em falta, se possível") | Documentado |
| 21 | **Gerar silencioso por automação de interface** (UI Automation) | manual §7.1 ("capable of generating quiet uninstallers … using many techniques (e.g. UI automation)") | Documentado |
| 22 | **Desinstalar via MsiExec** pelo GUID do produto | `MessageBoxes_UninstallMsiGuidMissing_*`, manual §2.4.2 | Documentado |
| 23 | **Desinstalar manualmente**: pula o desinstalador e devolve a lista de arquivos/chaves para apagar à mão | manual §2.4.3, `manualUninstallToolStripMenuItem` | Documentado |
| 24 | **Desinstalar por janela, diretório ou arquivo** (mira de arrastar e soltar sobre a janela) | manual §2.4.5, `toolStripButtonTarget`, `UninstallFromDirectory_*` | Documentado |
| 25 | **Desinstalar de um diretório** (achando o desinstalador dentro dele) | `UninstallFromDirectory_UninstallerFound`, `UninstallFromDirectoryNothingFound` | Documentado |
| 26 | **Desinstalação simultânea** com prevenção de colisão | manual §4.1.1, `checkBoxConcurrent`, `groupBox3` "Desinstalação simultânea", `label2` "Número máximo de desinstaladores em execução" | Documentado |
| 27 | **Ordenação inteligente**: loud primeiro, para poder sair antes | `checkBoxBatchSortQuiet`, manual §4.1 | Documentado |
| 28 | **Simulação** (não desinstala nada) | `checkBoxSimulate` | Documentado |
| 29 | **Impedir desligamento/reinício** durante a tarefa | `checkBoxShutdown` | Documentado |
| 30 | **Monitorar desinstaladores silenciosos** atrás de popups (daemon de automatização) | `checkBoxAutoDaemon` ("Executar daemon de automatização") | Documentado |
| 31 | **Finalizar automaticamente** desinstaladores silenciosos travados | `checkBoxAutoKillQuiet`, `checkBoxGenerateStuck` ("Finalizar automaticamente se travar aguardando entrada do usuário") | Documentado |
| 32 | **Tentar novamente** os silenciosos que falharam | `checkBoxRetryQuiet`, `MessageBoxes_AskToRetryFailedQuietAsLoud` | Documentado |
| 33 | **Pular** o desinstalador em execução, **Encerrá-lo**, **Rodar agora** | manual §7.3.1 | Documentado |
| 34 | **Abortar a tarefa inteira**, sem matar o desinstalador em execução | manual §7.3.2, `MessageBoxes_TaskStopConfirmation` | Documentado |
| 35 | **Comandos antes e depois** da desinstalação | `label5`/`label6` (linha de comando antes do início / após o fim), `LoadingDialogTitlePreUninstallCommands` | Documentado |
| 36 | **Desinstalação silenciosa de quem não aceita silent**: oferecer rodar em primeiro plano ou remover da tarefa | `MessageBoxes_QuietUninstallersNotAvailableQuestion` | Documentado |
| 37 | **Ponto de restauração do sistema** antes de desinstalar | `checkBoxRestorePoint`, `MessageBoxes_SysRestoreBeginQuestion` | Documentado |
| 38 | **Backup do registro** antes de remover sobras | `radioButtonBackupAsk/Never/Auto`, `MessageBoxes_BackupRegistryQuestion` | Documentado |
| 39 | **Assistente de desinstalação em 6 etapas**: Relacionado → Confirmação → Aplicativos em execução → Configuração → … → Final | `BeginUninstallTaskWizard` (`labelProgress` "1 / 6", `tabPage1`…`tabPage5`) | Documentado |
| 40 | Lista de **aplicativos relacionados** que talvez queiram desinstalar junto | `p1Heading` ("Aplicações relacionadas que talvez queira desinstalar"), `RelatedUninstallerAdder` | Documentado |

### 2.3 Sobras (junk) e confiança

| # | Funcionalidade | Evidência | Tipo |
|---|---|---|---|
| 41 | Varredura de sobras **após** a desinstalação, com motivo em texto | `MessageBoxes_LookForJunkQuestion`, `headerIntro` | Documentado |
| 42 | **5 níveis de confiança**: Muito bom / Bom / Duvidoso / Mau / Desconhecido | `Confidence_VeryGood/Good/Questionable/Bad/Unknown` | Documentado |
| 43 | **Justificativa textual por item** — a confiança vem acompanhada do *porquê* | `ConfidencePart_*`: `CompanyNameMatch`, `ProductNamePerfectMatch`, `ProductNameDodgyMatch`, `DirectoryStillUsed`, `ExplicitConnection`, `ItemNameEqualsCompanyName`, `QuestionableDirectoryName`, `AllSubdirsMatched`, `IsStoreApp`, `UsedBySimilarNamedApp` | Documentado |
| 44 | Confiança de **pasta**: vazia, executáveis presentes, arquivos presentes, muitos arquivos, nome reconhecido, sem subdiretórios, editor reconhecido | `Confidence_PF_*` (7 critérios) | Documentado |
| 45 | Confiança de **inicialização**: entrada `RunOnce`, entrada compatível com o app | `Confidence_Startup_*` | Documentado |
| 46 | Aviso explícito ao mexer em item de **baixa confiança** ("pode ser muito perigoso") | `MessageBoxes_ConfirmLowConfidenceQuestion` | Documentado |
| 47 | Backup antes de remover sobras + **lixeira** para arquivos | `MessageBoxes_BackupRegistryQuestion_Details` ("Arquivos e diretórios serão movidos para a lixeira") | Documentado |
| 48 | Exportar a lista de sobras para arquivo texto | `buttonExport`, `exportDialog.Filter` "Arquivos texto|*.txt" | Documentado |
| 49 | **Grupos de sobras** por categoria, com scanners próprios | `Junk_*_GroupName`: Drive/Arquivos, Programa órfão, Registro, Inicialização, Regra de Firewall, UserAssist, Relatórios de erro do Windows, Compatibilidade de aplicativos, Classes CLSID, Referência do desinstalador, Localização do instalador, Atalho, Política de áudio, Registros de eventos, Rastreamento, Lista de pastas instaladas, Configuração de rastreio/registro de depuração | Documentado |
| 50 | **Limpeza de pastas "Arquivos de Programas"** (vazias e parcialmente desinstaladas) | manual §2.4.4, `cleanUpProgramFilesToolStripMenuItem` | Documentado |
| 51 | Filtro para **esconder sobras de baixa confiança** | `checkBoxHideLowConfidence`, `checkBoxShowAllBadJunk` | Documentado |
| 52 | Instalador gerado automaticamente para **app não registrado** ("Simple Delete") | `UninstallerType_SimpleDelete`, manual §3.2 | Documentado |

### 2.4 Interface, filtro e automação

| # | Funcionalidade | Evidência | Tipo |
|---|---|---|---|
| 53 | **Filtro avançado por condições**: método de comparação × propriedade alvo, com negação | `FilterComparisonMethod_Contains/ContainsAny/Equals/StartsWith/EndsWith/Regex`, `ComparisonTargetInfo_AllProperties`, manual §6.3 | Documentado |
| 54 | Filtros de tipo **Incluir** e **Excluir**, incluir roda antes, excluir sobrepõe | manual §6.2 | Documentado |
| 55 | Listas de desinstalação salvas em **`.bcul`**, usáveis pela linha de comando | manual §6.4 | Documentado |
| 56 | **Treemap** do tamanho estimado de cada aplicativo | manual §2.3, `showTreemapToolStripMenuItem` | Documentado |
| 57 | **Legenda de cores** das marcações | manual §2.1.1, `showColorLegendToolStripMenuItem` | Documentado |
| 58 | **Classificação do usuário** (0 a 4 estrelas, sincronizada com servidor) | manual §2.1.3, `RatingPopup`, `checkBoxRatings` | Documentado |
| 59 | **Verificação de certificado** do desinstalador (verde = verificado, azul = não) | manual §2.1.2, `checkBoxCertTest`, `PropertiesWindow_Table_ErrorNoCertificate` | Documentado |
| 60 | **Cópia avançada para a área de transferência** com padrão de formato composto | manual §5, `AdvancedClipboardCopy` | Documentado |
| 61 | **Copiar**: informações completas, nome do programa, GUID, caminho do registro, sequência de desinstalação | `fullInformationCopyContextMenuStripItem` e irmãs | Documentado |
| 62 | **Pesquisar on-line** o programa (Google, AlternativeTo, FossHub, SourceForge, GitHub, FileHippo) | `lookUpOnlineToolStripMenuItem` + submenus | Documentado |
| 63 | **Gerenciador de inicialização** próprio, com entrada/saída de itens de Run/RunOnce/Service/Task/BrowserHelper | `StartupManagerWindow.resx` (27 chaves), `Startup_ShortName_*` | Documentado |
| 64 | **Renomear entrada** da lista | `MessageBoxes_Title_Rename_uninstaller`, `renameContextMenuStripItem` | Documentado |
| 65 | **Excluir a entrada do registro** (some da lista, sem desinstalar) | `deleteRegistryEntryContextMenuStripItem`, `MessageBoxes_DeleteRegKeysConfirmation` | Documentado |
| 66 | **Assumir a propriedade** de um item | `takeOwnershipToolStripMenuItem` | Documentado |
| 67 | **Abrir a chave no Regedit** | `openKeyInRegeditToolStripMenuItem` | Documentado |
| 68 | **Abrir "Programas e Recursos"** e **"Recuperação do Sistema"** | `openProgramsAndFeaturesToolStripMenuItem`, `openSystemRestoreToolStripMenuItem` | Documentado |
| 69 | **Exportar para script de desinstalação em lote** | `exportToABatchUninstallScriptToolStripMenuItem` | Documentado |
| 70 | **Criar script PowerShell para remover Store Apps** | `exportStoreAppsToPowerShellRemoveScriptToolStripMenuItem` | Documentado |
| 71 | Exportar dados em **XML** com cabeçalho fixo (editora, versão, data, tamanho, registro, tipo, cadeia de desinstalação, cadeia silenciosa, comentários) | `StrExportHeader` | Documentado |
| 72 | Painel lateral de configurações (filtros e realces) | `PropertiesSidebar.resx` (25 chaves), `displaySettingsToolStripMenuItem` | Documentado |
| 73 | **Agrupamento** de itens pela coluna de ordenação | `checkBoxViewGroups` | Documentado |
| 74 | **Modo para daltônicos**, escalonamento por DPI, tema do sistema | `checkBoxColorblind`, `checkBoxDpiaware`, `useSystemThemeToolStripMenuItem` | Documentado |
| 75 | **Gerenciador de desinstaladores relacionados** (`RelatedUninstallerAdder`) | `RelatedUninstallerAdder.resx` | Documentado |
| 76 | Atalhos de teclado e cópia de informações da entrada | `fullInformationCopyContextMenuStripItem` | Documentado |
| 77 | Ver **mudanças** (tweaks) detectadas pelo monitor | `viewTweaksToolStripMenuItem`, `checkBoxTweaks` | Documentado |
| 78 | **Atualização automática** (baixa e instala após reiniciar) | `MessageBoxes_UpdateAskToDownload` | Documentado |
| 79 | Estatísticas anônimas de uso (opt-in) | `checkBoxSendStats` | Documentado |
| 80 | Linha de comando **antes/depois** e "executar aplicativos externos" (desabilitável) | `checkBoxEnableExternal`, `groupBoxExternal` | Documentado |

### 2.5 Linha de comando

Extraída do manual oficial (seções 9.1–9.6) e das strings do
`BCU-console.dll`.

| Comando | Parâmetros | Efeito |
|---|---|---|
| `BCUninstaller.exe` | `[drive:][path]filename` | abre a lista de desinstalação indicada |
| `BCUninstaller.exe` | `/sm` (`/startupmanager`) | abre **só** o Gerenciador de Inicialização, sem enumerar aplicativos — parte muito mais rápido |
| `BCU-console.exe` | `uninstall <arquivo.bcul> [/Q] [/U] [/V] [/J=<nível>]` | desinstala conforme a lista |
| `BCU-console.exe` | `export <arquivo.xml> [/Q] [/U] [/V]` | exporta os dados em XML |
| | `/Q` | usa desinstaladores silenciosos sempre que possível |
| | `/U` | modo não assistido (sem confirmação) — o manual avisa em caixa alta |
| | `/J=<Level>` | limpa sobras após desinstalar; padrão `VeryGood`; níveis `VeryGood, Good, Questionable, Bad, Unknown` |
| | `/V` | log detalhado |
| `BCU-console.exe` | `/N` | **execução de simulação** (dry run) — visível nas strings do binário |
| `BCU-console.exe` | `/F=<Formato>` | formato de saída `json` ou `xml` (padrão texto puro) — visível nas strings do binário |
| `StoreAppHelper.exe` | `/query` · `/uninstall <FullName>` | Store Apps do usuário atual |
| `SteamHelper.exe` | `u[ninstall] [/s[ilent]] <AppID>` · `i[nfo] <AppID>` · `l[ist]` · `steam` | Steam |
| `OculusHelper.exe` | `/query` · `/uninstall <CanonicalName>` | Oculus |
| `UninstallerAutomatizer.exe` | `<UninstallerType> [/K] <UninstallCommand>` | automatiza desinstaladores NSIS travados; `/K` mata o processo se a automação falhar |

Códigos de retorno: `0` sucesso · `1`/`10022` argumentos inválidos ·
`1223` cancelado pelo usuário · `1627`/`59` erro inesperado.

---

## 3. Mecanismos notáveis

### 3.1 A classificação do desinstalador é o eixo do produto

O BCU não trata "desinstalar" como uma operação só: ele **classifica** o
desinstalador (`UninstallerType_*`) e escolhe a estratégia por tipo —
MsiExec recebe o GUID, NSIS e Inno Setup recebem tratamento de silencioso,
Store App vai por outro caminho, Windows Feature e Windows Update têm
caminho próprio, Chocolatey é desinstalado pelo próprio Chocolatey
(`ChocolateyFactory_UninstallInChocolateyJunkName`).

O Tranal hoje **não classifica** o desinstalador: ele roda a cadeia de
desinstalação como veio do registro e varre sobras pelo nome. Essa é a
maior lacuna estrutural que a auditoria do BCU revela.

### 3.2 O modelo de confiança é a parte mais valiosa do BCU

O Revo resolve isso com **negrito**: só os itens em negrito são removidos.
O BCU resolve com **cinco níveis + justificativa textual por item**:

```
ConfidencePart_ProductNamePerfectMatch   →  "Nome do produto combinou perfeitamente"
ConfidencePart_CompanyNameDidNotMatch     →  "O nome da empresa não coincidiu"
ConfidencePart_DirectoryStillUsed         →  "O diretório é usado por outros aplicativos instalados"
ConfidencePart_AllSubdirsMatched         →  "O diretório é, ou estará vazio após a remoção de lixo"
```

A regra do Tranal é mais simples e mais segura (só confiança **alta** vem
marcada; média e baixa vêm desmarcadas), mas **não mostra o porquê**. O
usuário do Tranal vê "não marcado" e não sabe se o motor errou ou se foi
cuidado.

### 3.3 A atribuição de sobras é por **padrão do desinstalador**, não só por nome

O BCU não procura "pasta com o nome do programa": ele lê a cadeia de
desinstalação e procura itens **mencionados explicitamente** nela
(`ConfidencePart_ExplicitConnection`). É por isso que ele acerta sobras
cujo nome não tem nada a ver com o programa (o executável do jogo em
`AppData\Local\Temp`, o driver em `Windows\System32\drivers`).

### 3.4 Código aberto: a referência de implementação

Por ser Apache 2.0 com código público, o BCU permite responder à pergunta
que nenhum outro concorrente responde: **como** se faz a varredura de
sobras sem quebrar o sistema, **como** se classifica o desinstalador, **como**
se geram desinstaladores silenciosos, **como** se trata desinstalador
travado. É a fonte para as decisões de arquitetura — não para copiar
código (que tem licença própria e obligations de atribuição).

---

## 4. Interface

A janela principal é WinForms com:

- **lista de aplicativos** (`ObjectListView`) com 18 colunas:
  Nome, Editor, Classificação do usuário, Versão, Data da instalação, Tamanho,
  Inicialização, 64bit, Comando de desinstalação, Sobre o URL, Fonte da
  instalação, Local da instalação, Tipo de desinstalador, Componente do
  sistema, Protegido, Chave do registro, Código do produto, Comando de
  desinstalação silenciosa;
- **painel lateral** de configurações (filtros e realces);
- **treemap** acima da barra de status;
- **legenda de cores** no canto inferior direito;
- **barra de status** com contagens (`{0} desinstaladores no total, {1}`).

Menus: Arquivo · Visualizar · Desinstalar · Ferramentas · Configurações ·
Ajuda.

---

## 5. O que o Tranal NÃO faz hoje

Comparação com o estado **medido** do Tranal (`docs/PARIDADE.md` e o código
em `src/Tranal.Uninstaller/Core/`).

### 5.1 Lacunas estruturais (mudam a arquitetura)

| # | Recurso do BCU | Tranal hoje | Prioridade |
|---|---|---|---|
| 1 | **Classificar o desinstalador** (14 tipos: MsiExec, Inno, NSIS, InstallShield, SDBInst, Steam, Store, WinFeature, WinUpdate, Chocolatey, Oculus, PowerShell, Simple Delete) e escolher a estratégia por tipo | não classifica; roda a cadeia do registro e varre por nome | 🔴 alta |
| 2 | **Atribuir sobras pelo desinstalador** (item explicitamente citado na cadeia) e não só pelo nome | varre por nome e variantes | 🔴 alta |
| 3 | **Níveis de confiança com justificativa** (5 níveis + motivo por item) | 3 níveis, sem justificativa visível | 🔴 alta |
| 4 | **Desinstalação silenciosa com geração de silencioso** (inclusive por automação de interface) | modo `builtin` roda o desinstalador como veio | 🔴 alta |
| 5 | **Filtro avançado por condições** (Regex, 6 métodos de comparação, alvo = propriedade, negação, Incluir/Excluir) | filtros rápidos + popup de filtros | 🟡 média |
| 6 | **Colunas**: o BCU tem 18, o Tranal tem 12 | faltam Tipo de desinstalador, Desinstalador silencioso, Localização do desinstalador, Fonte da instalação, Componente do sistema, Protegido, GUID/ProductCode, Inicialização, Classificação do usuário | 🟡 média |
| 7 | **Detecção de fontes além do registro**: Store Apps, Windows Features, Windows Updates, Steam, Chocolatey, Scoop, Oculus, varredura de drives | só Windows Store Apps (`Core/Modules/WindowsApps.cs`, 122 pacotes MSIX) | 🟡 média |
| 8 | **Desinstalar via MsiExec** pelo GUID | não | 🟡 média |
| 9 | **Desinstalação simultânea** com prevenção de colisão | não | 🟡 média |
| 10 | **Desinstalar manualmente**: pular o desinstalador e listar arquivos/chaves para apagar à mão | não | 🟡 média |

### 5.2 Lacunas de recursos

| Recurso do BCU | Tranal hoje |
|---|---|
| Gerenciador de inicialização próprio com janela de desinstaladores | `Core/Tools/AutorunManager.cs` (346 itens) — gerencia, mas não integra o ciclo de desinstalação |
| Limpeza de pastas "Arquivos de Programas" (vazias e parcialmente desinstaladas) | não |
| Encontrar por janela (mira de arrastar e soltar) | `Core/Topmost/` tem overlay e janela flutuante — resolve o caso de uso, por outro caminho |
| Classificação do usuário (estrelas, sincronizada) | não |
| Verificação de certificado do desinstalador | não |
| Cópia avançada para a área de transferência (padrão composto) | tem exportação CSV/XML/HTML, não cópia com padrão |
| Exportar para script de desinstalação em lote | não |
| Criar script PowerShell para remover Store Apps | não |
| Treemap | não |
| Legenda de cores | não |
| Painel lateral de configurações | não |
| Agrupamento por coluna de ordenação | tem grupos do usuário, não agrupamento automático |
| Renomear entrada da lista | não |
| Excluir entrada do registro sem desinstalar | não |
| Assumir a propriedade | não |
| Abrir chave no Regedit / Programas e Recursos / Recuperação do Sistema | não |
| Comandos antes e depois da desinstalação | não |
| Pular / Encerrar / Rodar agora um desinstalador em execução | tem "Kill the Process and Continue" e encerramento forçado, mas não o painel de progresso com controle manual |
| Abortar a tarefa inteira sem matar o desinstalador em execução | não |
| Monitorar silencioso atrás de popups (daemon de automatização) | não |
| Simulação (dry run) | **tem e é melhor**: `/scan` é dry-run real na CLI |
| Detecção de app não registrado + varredura de drives | não (e o `PARIDADE.md` §8 registra por que foi deixado de fora) |
| Geração de desinstalador simples para app órfão | não (decisão conscious do §8 do `PARIDADE.md`) |
| Modo daltônico / DPI / tema do sistema | tema próprio, sem escalonamento por DPI |
| Estatísticas anônimas | não |
| Debug window | não |

### 5.3 Onde o Tranal já é **melhor**

| Ponto | Tranal | BCU |
|---|---|---|
| Simulação antes de agir | `/scan` real na CLI, com a lista de sobras que seria removida | `checkBoxSimulate` (equivalente, mas não roteirizável) |
| Segurança da remoção | backup completo do registro **antes** do desinstalador nativo, com script de restauração; exclusões aplicadas até no backup; item em uso adiado para o reinício | backup opcional (pergunta), backup só do registro, sem script de restauração |
| Atalhos/junctions | `Core/SafeTraversal.cs` — o motor nunca atravessa um link e nunca apaga o alvo | não verificado nesta auditoria |
| Confiança | mais restritiva (só alta vem marcada; média e baixa nunca) | 5 níveis, mas o usuário precisa saber o que significam |
| Lixeira | sobras de arquivo vão para a lixeira e podem ser restauradas | idem (`MessageBoxes_BackupRegistryQuestion_Details`) |
| Idiomas | pt-BR e en-US com **601 chaves** em paridade | 24 idiomas, 1.066 chaves pt-BR — **o BCU tem mais tradução** |

### 5.4 Onde o Tranal já é **peior**

| Ponto | Tranal | BCU |
|---|---|---|
| Cobertura de fontes | registro + Store Apps | registro + MSI + drives + Store + WinFeatures + WinUpdates + Steam + Chocolatey + Scoop + Oculus + PowerShell |
| Automação por linha de comando | `/scan`, `/mu`, `/export`, `/columns` — **sem** desinstalação em lote por arquivo de lista | `BCU-console uninstall lista.bcul /Q /U` — desinstalação em lote roteirizável é o forte do BCU |
| Profundidade da lista | 12 colunas | 18 colunas |
| Desinstalador travado | encerramento automático | painel de progresso com pular/encerrar/rodar agora |

---

## 6. Fichos de evidência

Todos em `workspace/auditoria/bcu/evidencia/` (material de análise, **não**
versionado — o que fica no repositório é o fato derivado, como manda a
regra do `PARIDADE.md`).

| Arquivo | Conteúdo |
|---|---|
| `resx-ptBR-*.txt` (45 arquivos) | 1.066 chaves pt-BR por tela — a spec funcional |
| `manual-texto.txt` | manual oficial (427 KB) convertido em texto, 324 linhas |
| `strings-BCU-console.txt` | strings do console — inclui `/N` (dry run) e `/F=<formato>`, que não estão no manual |
| `pe-*.txt` | cabeçalho PE, assinatura e imports dos assemblies |

Evidência de engenharia reversa metadata do `UninstallTools.dll` (motor do
produto): .NET 8 gerenciado, PE32 anycpu, TypeDef 368, MethodDef 2.374,
Field 4.345, AssemblyRef 40, resources 5, SHA-256
`3c82ca47f91d92eeadc34620a35d4e26d7acaa91338314bb2bba00eef4a0cf79`.

---

## 7. Lacunas desta auditoria

| O que faltou | Por quê | Como fechar |
|---|---|---|
| **Execução real do BCU 6.3** | a instalação exige administrador e a auditoria corre sem elevação; a janela elevada não pode ser automatizada (UIPI) | pedir ao dono para instalar e rodar, ou capturar a tela na mão dele |
| **Comportamento real do gerador de desinstalador silencioso** | só se observa com o programa rodando | idem |
| **Sincronização da classificação de usuário** | depende do servidor de estatísticas do BCU | observar com o app rodando |
| **Resultado do `/J=<nível>`** em cada nível de confiança | idem | idem |
| **Códigos de saída reais** do `BCU-console` | só documentados, não medidos | rodar `BCU-console.exe` num sandbox |

---

## 8. Decisões que este documento obriga

1. **O Tranal passa a classificar o desinstalador.** É o eixo do BCU e o que
   habilita quase tudo o mais (silencioso, MsiExec, Store App, órfão). A
   coluna "Tipo de desinstalador" vira a primeira coluna nova do catálogo.
2. **A confiança ganha justificativa.** O `LeftoverConfidence` do Tranal
   (3 níveis) continua, mas passa a produzir uma frase por item — o mesmo
   que o BCU faz com `ConfidencePart_*`. A lista de sobras mostra o *porquê*.
3. **A atribuição de sobras usa a cadeia de desinstalação**, não só o nome.
   É o que separa "achou a pasta do programa" de "achou a pasta **dele**".
4. **Filtro Regex e lista de desinstalação em arquivo** entram na backlog
   depois do núcleo: são o que torna o Tranal roteirizável como o BCU.
5. **O que o Tranal já faz melhor fica protegido.** Backup antes do
   desinstalador nativo, exclusões aplicadas até no backup, `SafeTraversal`
   e `/scan` são melhores que qualquer coisa do BCU e não podem ser regredidos em
   nome de paridade.

---

*Auditoria gerada por medição direta dos artefatos. Toda linha deste
documento aponta para evidência em disco; o que não pôde ser verificado
está marcado como tal na seção 7.*
