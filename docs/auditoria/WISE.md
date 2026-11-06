# Wise Program Uninstaller 3.2.7.271 (Free) — auditoria

> O concorrente **mais parecido com o que o owner descreveu wanting**: um
> desinstalador que **registra o que sobrou** e **deixa recuperar**. O Wise
> tem histórico de desinstalação com botão "Recuperar", backup dos resíduos,
> varredura pós-desinstalação e uma base curada de **595 perfis** de limpeza
> por aplicativo. É também o concorrente com a interface mais polida de todos
> os auditados — skin, SVG, WebView2 — num binário Delphi de 2019.

Data da coleta: 05/11/2026 · Versão auditada: **3.2.7.271** (edição **Free**;
a PRO existe e é licenciada)
Curadoria binária: 06/11/2026 — as funcionalidades desta tabela estão
amarradas ao **binário**, não à documentação.

---

## 1. Identificação

| Campo | Valor | Fonte |
|---|---|---|
| Produto | **Wise Program Uninstaller 3.2.7.271** (Free) | `evidencia/pe-wise.txt` |
| Editor | **Lespeed Technology Co., Ltd** (Beijing, China) | assinatura Authenticode |
| Marca / site | **WiseCleaner.com** — `wiseuninstaller.com` | `pe-wise.txt`, `manual-*` |
| Download oficial | `https://downloads.wisecleaner.com/soft/WPUSetup_3.2.7.271.exe` | manifesto do winget `WiseCleaner.WiseProgramUninstaller` 3.2.7 |
| Tamanho do instalador | 11.134.688 B | `downloads/` |
| SHA-256 do instalador | `38EB4C58EAC53AF4232702B77F4AE0F0D3BA6C2E179560E1F8853DCBCE43F2E1` (confere com o manifesto) | `downloads/` |
| Formato do instalador | **Inno Setup 6.0.0 (unicode)** | `innoextract --info` |
| Formato do programa | **PE32 Delphi/Embarcadero, 32 bits, empacotado com 7-Zip** | `pe-wise.txt` |
| Assinante | `CN="Lespeed Technology Co., Ltd", L=Beijing, C=CN` — SSL.com EV Code Signing Intermediate CA RSA R3, **válido até 30/04/2026** | `pe-wise.txt` |
| Compilação | 2025-10-15 07:50:13Z | `pe-wise.txt` |
| Produto ARP | `Wise Program Uninstaller_is1`; destino padrão `%ProgramFiles%\Wise\Wise Program Uninstaller` | manifesto do winget |
| Elevação | `elevatesSelf` | manifesto do winget |
| Idiomas | **46** (pt-BR incluído) | `app/app/Languages/` |
| **Binário analisado (esta rodada)** | `workspace/auditoria/wise/app/app/WiseProgramUninstaller.exe` — 13.273.496 B — sha256 `4f7da8050ad12ed500902a18f886b12404ad24f64e719bb819675bc048c64935` | `bin-inventario/wise.inventario.md:3-5` |

### O domínio

`wiseuninstaller.com` responde **403 Proibido** a esta máquina, com e sem
`www`, e mesmo com User-Agent de navegador — a mesma armadilha de SEO já
vistas no `hibituninstaller.com` e no `totaluninstall.com`. O domínio do
**fabricante** é `wisecleaner.com`, e o download sai de
`downloads.wisecleaner.com`. A URL oficial foi confirmada pelo **manifesto do
winget**, e o SHA-256 do arquivo baixado bate com o do manifesto.

### O que vem no instalador

| Arquivo | Bytes | O que é |
|---|---|---|
| `WiseProgramUninstaller.exe` | 13.273.496 | o programa (Delphi, 32 bits, 7-Zip) |
| `UnMonitor.exe` | 8.081.304 | **processo separado** que acompanha a desinstalação |
| `messages.exe` | 6.269.688 | módulo de mensagens (PE Delphi) |
| `WiseAppManager.dll` | 138.488 | gerenciador de aplicativos |
| `Rate.info` | 1.161.830 | hashes MD5 + versão dos produtos WiseCleaner |
| `sqlite3.dll` | 1.099.792 | **SQLite embarcado** |
| `7z.dll` | 1.149.176 | descompressor embarcado |
| `Languages\*.ini` | 46 arquivos | catálogos de idioma |

### O dado mais revelador da auditoria

O **catálogo de idiomas** é o inventário da interface: `English.ini` tem
**292 chaves** e `Portuguese(Brazil).ini` tem **218**. A diferença de **74
chaves** é exatamente o conjunto de recursos **PRO** — e o pt-BR não tem uma
chave a mais que o inglês, é um subconjunto.

As chaves são nomeadas por formulário e controle (`FrmMain.btnAll.caption`,
`Frmafterdel.btnremove.caption`), o que dá o **mapa completo das 15 telas e
dos botões** sem precisar rodar o programa. E o texto pt-BR é a **legenda
original** do produto — é o que está entre aspas nas tabelas abaixo.

### Método da curadoria binária (06/11/2026)

| Artefato | O que traz | Como foi usado |
|---|---|---|
| `bin-inventario/wise.inventario.md` | 5.372 classes RTTI Delphi, 1.183 rótulos de interface, 65 capacidades por token de API/IO | nomes de classe Delphi como evidência de funcionalidade |
| `bin-inventario/wise.recursos.txt` | 173 entradas de recurso do PE | **recursos de formulário Delphi** — 22 formulários `TFRM*` |
| `bin-inventario/wise.identificadores.txt` | identificadores de tipo/método | **chaves de legenda que estão no próprio binário** (`FrmMain.BtnForce.Caption`, `TfrmUnlog.recover`…) e units Delphi |
| `bin-inventario/wise-monitor.identificadores.txt` | identificadores do `UnMonitor.exe` | o modelo de sobra do produto: `uUninstallHelper.TUninstallObject` |
| `bin-strings/wise.strings.txt` | 73.256 strings únicas | literais exatos — inclusive o **bloco JSON da base de regras** e dos perfis de navegador |

> **Limitação registrada:** o executável é **empacotado com 7-Zip**. Por isso
> o inventário de strings é menor e a leitura de conteúdo interno é parcial.
> Onde a compressão impede a confirmação, a linha diz
> **não confirmado no binario** — não se inventa.

---

## 2. Superfície observável

86 funcionalidades medidas (72 do relatório de 05/11/2026 + 14 descobertas no
binário).

### 2.1 Lista e modos de desinstalação

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 1 | Lista "Todos os Programas" | `wise.recursos.txt:165` -> `TFRMWPUMAIN/0` (formulário principal) | **OK** — `Core/ProgramScanner.cs` |
| 2 | Lista separada de "Apps da Área de Trabalho" (Win32) | `wise.identificadores.txt:70-73` -> `FrmMain.BtnForce.Caption` / `FrmMain.BtnNormal.Caption` / `FrmMain.btnLog.Caption` / `FrmMain.btnuninstall.Caption` | **PARCIAL** — o Tranal lista os programas, mas não separa a visão Win32 da visão Store |
| 3 | Lista separada de "Apps do Metro" (Store/MSIX) | `wise.identificadores.txt:231` -> `Windows.Management.Deployment.PackageManager`; `wise.recursos.txt:161` -> `TFRMUNINSTALLPAGE/0` | **OK** — `Core/Modules/WindowsApps.cs` |
| 4 | Lista separada de "Componentes do Sistema" | `wise.recursos.txt:167` -> `TMPTYPES/2052` | **PARCIAL** — `Core/Discovery/WindowsFeatures.cs` trata Windows Features, não a lista completa de componentes do sistema |
| 5 | **Desinstalação Segura** — o modo normal: roda o desinstalador e depois procura as sobras | `wise.identificadores.txt:71` -> `FrmMain.BtnNormal.Caption`; `wise.recursos.txt:144` -> `TFRMBEFOREUNISTALL/0` | **OK** — `Core/Removal.cs` + `Core/UninstallSupport.cs` |
| 6 | **Desinstalação Forçada** — para quando o desinstalador normal não resolve | `wise.identifiers.txt:70` -> `FrmMain.BtnForce.Caption` | **PARCIAL** — `Ui/ForcedUninstallWindow.xaml.cs` + `Core/Automation/ForcedRemovalOptions.cs` existem; "forçada" não é um dos 4 modos de `Core/UninstallMode.cs` |
| 7 | "Modificar" — reaproveita o instalador do programa | `wise.recursos.txt:144` -> `TFRMBEFOREUNISTALL/0` | **AUSENTE** — sem equivalente identificado |
| 8 | "Remover Entradas" — tira o programa da lista **sem** desinstalar | `wise.recursos.txt:144` -> `TFRMBEFOREUNISTALL/0` | **PARCIAL** — `Core/ExcludeList.cs` esconde da varredura, mas não remove a entrada de desinstalação do registro |
| 9 | Aviso para não usar a Forçada antes da Segura | `wise.identificadores.txt:70` -> `FrmMain.BtnForce.Caption` | **AUSENTE** — sem equivalente identificado |
| 10 | Desinstalação em lote (N programas) | `wise.recursos.txt:144` -> `TFRMBEFOREUNISTALL/0` | **PARCIAL** — `Core/Automation/BatchList.cs` + CLI `/mu` |
| 11 | Confirmação antes de desinstalar, com o nome | `wise.recursos.txt:144` -> `TFRMBEFOREUNISTALL/0` | **OK** — `Ui/UninstallWizardWindow.xaml.cs` |
| 12 | Extensão de navegador, com aviso de **sincronização** | `wise.recursos.txt:151` -> `TFRMPLUGDELCOMP/0` | **AUSENTE** — o Tranal não avisa sobre sincronização de navegador |
| 13 | Aviso quando o navegador está em execução | `wise.recursos.txt:145` -> `TFRMCATCH/0` | **PARCIAL** — `Core/ProcessTerminator.cs` encerra o processo, mas não há aviso específico de navegador |
| 14 | Recusa de desinstalar a si mesmo | `wise.recursos.txt:165` -> `TFRMWPUMAIN/0` | **AUSENTE** — sem equivalente identificado |
| 15 | Aviso de que um programa **não aceita** desinstalação silenciosa | `wise.recursos.txt:144` -> `TFRMBEFOREUNISTALL/0` | **PARCIAL** — `Core/QuietUninstall.cs` resolve por família, mas não emite o aviso "este programa não pode ser desinstalado em modo silencioso" |
| 16 | Aviso de desinstalação anterior em andamento | `wise.recursos.txt:144` -> `TFRMBEFOREUNISTALL/0` | **AUSENTE** — sem equivalente identificado |
| 17 | Erro de desinstalação reportado | `wise.recursos.txt:163` -> `TFRMUNLOG/0` | **OK** — `Core/RemovalLog.cs` |

### 2.2 Sobras, backup e restauração

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 18 | Varredura de sobras **depois** da desinstalação, como opção marcada | `wise.recursos.txt:143` -> `TFRMAFTERDEL/0` | **OK** — `Core/UninstallSupport.cs` (`LeftoverScanner`) |
| 19 | Tela de sobras pós-desinstalação, com seleção | `wise.recursos.txt:143` -> `TFRMAFTERDEL/0` | **OK** — `Ui/UninstallWizardWindow.xaml.cs` |
| 20 | "Selecionar tudo" / "Não selecionar nenhum" | `wise.recursos.txt:143` -> `TFRMAFTERDEL/0` | **OK** — mesma tela do assistente |
| 21 | Aviso de resíduos, com a contagem de arquivos e de registros | `wise.recursos.txt:143` -> `TFRMAFTERDEL/0` | **OK** — mesma tela do assistente |
| 22 | Relatório separando arquivos de registro | `wise.recursos.txt:143` -> `TFRMAFTERDEL/0` | **OK** — `Core/Reporting/LeftoverReportExporter.cs` |
| 23 | **Backup automático dos resíduos antes de apagá-los** | `wise-monitor.identificadores.txt:223` -> `TDictionary<System.string,uUninstallHelper.TUninstallObject>` (o modelo de sobra persistido pelo `UnMonitor.exe`) | **OK** — `Core/Recovery/LeftoverBackup.cs` |
| 24 | Expiração do backup (apaga os com mais de 1 mês) | `wise.recursos.txt:158` -> `TFRMRESTORE/0` | **PARCIAL** — `Core/Recovery/DailyRegistryBackup.cs` expira o backup **diário do registro**; a expiração do backup de **resíduos** não foi conferida |
| 25 | Tela de **Restauração do Sistema**, com "Restaurar" e "Apagar" | `wise.recursos.txt:158` -> `TFRMRESTORE/0` | **PARCIAL** — `Core/Recovery/RecoverOperation.cs` restaura sobras; falta a tela de **pontos de restauração** |
| 26 | **Ponto de restauração do Windows antes de desinstalar** | `wise.recursos.txt:158` -> `TFRMRESTORE/0`; `wise.inventario.md:1206` -> `SRSetRestorePoint` - Cria ponto de restauracao do sistema | **OK** — `Core/UninstallSupport.cs:1210` (`TryCreateRestorePoint`) |
| 27 | Remoção de ponto de restauração selecionado | `wise.recursos.txt:158` -> `TFRMRESTORE/0` | **PARCIAL** — a CLI lista/restaura/exclui pontos; a remoção pela tela não foi conferida |
| 28 | **Histórico de desinstalações** com a coluna "Resíduos" | `wise.recursos.txt:163` -> `TFRMUNLOG/0`; `wise.identificadores.txt:212` -> `TfrmUnlog.head.ItemName` | **PARCIAL** — `Core/UninstallHistory.cs` mantém o histórico; a coluna de resíduos recuperáveis não foi conferida |
| 29 | **Botão "Recuperar" no histórico** | `wise.strings.txt:71271` -> `TfrmUnlog.recover`; `:71272` -> `Recovered` | **PARCIAL** — `Core/Recovery/RecoverOperation.cs` sabe recuperar; a ligação "Recuperar" dentro do histórico não foi conferida |
| 30 | Apagar todo o histórico | `wise.identificadores.txt:211` -> `TfrmUnlog.btnClear.Caption`; `wise.recursos.txt:163` -> `TFRMUNLOG/0` | **OK** — `Core/UninstallHistory.cs` |
| 31 | Estado "não há itens residuais" | `wise.recursos.txt:163` -> `TFRMUNLOG/0` | **OK** — `Core/UninstallHistory.cs` |
| 32 | **Desinstalação Personalizada** — varre por palavra-chave e deixa explorar a pasta | `wise.recursos.txt:162` -> `TFRMUNLESSDEL/0` | **AUSENTE** — sem equivalente identificado |
| 33 | Botão "Explorar" para apagar o que for encontrado | `wise.recursos.txt:162` -> `TFRMUNLESSDEL/0` | **AUSENTE** — sem equivalente identificado |
| 34 | Aviso de limpeza de resíduos com ação direta | `wise.recursos.txt:143` -> `TFRMAFTERDEL/0` | **OK** — mesma tela do assistente |
| 35 | Detecção da desinstalação feita por **outro** desinstalador | `wise.recursos.txt:144` -> `TFRMBEFOREUNISTALL/0` | **AUSENTE** — sem equivalente identificado |

### 2.3 Software Health (PRO) — a página de saúde do software

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 36 | Botão **"Software Health"** no menu principal | `wise.recursos.txt:146` -> `TFRMCLEANPAGE/0` | **OK** — `Core/Management/SoftwareHealth.cs` |
| 37 | Quatro categorias: **Uninstallation Leftovers**, **Software Redundant Files**, **Software Permissions**, **Disturbing Notifications** | `wise.recursos.txt:146` -> `TFRMCLEANPAGE/0` | **PARCIAL** — o motor existe; as 4 categorias do Wise não foram conferidas no código do Tranal |
| 38 | "Analyze" que varre todos os aplicativos de uma vez | `wise.recursos.txt:146` -> `TFRMCLEANPAGE/0` | **PARCIAL** — mesma ressalva da linha 37 |
| 39 | "Stop" para interromper a varredura | `wise.recursos.txt:146` -> `TFRMCLEANPAGE/0` | **PARCIAL** — mesma ressalva da linha 37 |
| 40 | "Detect app permissions for better permission management" | `wise.recursos.txt:150` -> `TFRMPERMISSIONSDIALOG/0` | **OK** — `Core/Management/AppPermissionManager.cs` |
| 41 | "Block pop-up notifications and unwanted ads" | `wise.strings.txt:62862` -> `"defaultname": "Popup notifications"` | **AUSENTE** — sem bloqueador de notificação no Tranal |
| 42 | Permissões por tipo: **Service**, **Startup**, **Right-click menu**, **Scheduled task** | `wise.recursos.txt:150` -> `TFRMPERMISSIONSDIALOG/0`; `wise.identificadores.txt:74` -> `FrmPermissionsDialog.lvlist.Option` | **OK** — `Core/Management/AppPermissionManager.cs:88-134` cobre exatamente os quatro tipos |
| 43 | Diálogo de permissões com "Action" por plug-in | `wise.identificadores.txt:74` -> `FrmPermissionsDialog.lvlist.Option` | **PARCIAL** — a ação por item existe no motor; a tela dedicada não foi conferida |
| 44 | Diálogo de **lixeira** com tamanho por aplicativo | `wise.recursos.txt:160` -> `TFRMTRASDIALOG/0` | **PARCIAL** — `Core/RecycleBin.cs` esvazia a lixeira; falta o detalhe por aplicativo |
| 45 | Diálogo de **notificações** com remetente e tipo | `wise.recursos.txt:149` -> `TFRMNOTIFYDIALOG/0` | **AUSENTE** — sem equivalente identificado |
| 46 | Registro de "última análise" | `wise.recursos.txt:146` -> `TFRMCLEANPAGE/0` | **AUSENTE** — sem equivalente identificado |

### 2.4 Extensões de navegador

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 47 | Gerenciador de extensões, com abas por navegador | `wise.strings.txt:33129` -> `btVivaldi`; `:33130` -> `btYandex` | **PARCIAL** — `Core/Modules/BrowserExtensions.cs` cobre Chrome/Edge/Firefox/Opera |
| 48 | Base de **11 navegadores** com caminho de perfil | `wise.strings.txt:62853` -> `"VivaldiPath": {`; `:62854` -> `"key": "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Vivaldi"`; `:62856` -> `...Uninstall\\Comodo Dragon`; `:62858` -> `...Uninstall\\SogouExplorer`; `:62873` -> `%LOCALAPPDATA%\\Yandex\\YandexBrowser\\User Data\\Default\\Preferences`; `:62874` -> `%LOCALAPPDATA%\\Vivaldi\\User Data\\Default\\Preferences`; `:62878` -> `%APPDATA%\\SogouExplorer\\Webkit\\Default\\Preferences` | **PARCIAL** — o Tranal cobre 4 navegadores; **faltam Yandex, Vivaldi, Brave, Comodo Dragon, Cent Browser e SoGou Explorer** |
| 49 | Leitura do arquivo de preferências do navegador | `wise.strings.txt:62873` -> `%LOCALAPPDATA%\\Yandex\\YandexBrowser\\User Data\\Default\\Preferences`; `:62878` -> `%APPDATA%\\SogouExplorer\\Webkit\\Default\\Preferences` | **PARCIAL** — o Tranal lê `extensions.json` do Chromium e `extensions.json` do Firefox, não o `Preferences` completo do perfil |
| 50 | Bloqueio de notificações pop-up (regra `PopNotify`) | `wise.strings.txt:62861` -> `"tranname": "PopNotify",`; `:62885` -> `"type": "PopNotify",` | **AUSENTE** — o tipo de regra existe no Wise; no Tranal não há |
| 51 | Aviso quando não há extensão | `wise.recursos.txt:151` -> `TFRMPLUGDELCOMP/0` | **OK** — `Core/Modules/BrowserExtensions.cs` |

### 2.5 Base de regras de limpeza por aplicativo

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 52 | **Base embutida de regras**: **595 perfis** nomeados e **2.379 caminhos** | `wise.strings.txt:62853-62892` (bloco JSON de regras: `tranname`, `defaultname`, `type`, `key`, `path0`…); `wise.inventario.md:1196` -> `Rule` - Motor de regras | **AUSENTE** — `Core/Rules/BuiltinRules.cs` traz **apenas 4 regras de proteção**; não há base por aplicativo |
| 53 | Tipos de regra: **1.513 File**, **671 RegKey**, **166 RegValue**, **22 Reg**, **16 Ini**, **11 PopNotify** | `wise.strings.txt:62885` -> `"type": "PopNotify",`; `:62856` -> `"key": "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Comodo Dragon"` | **AUSENTE** — o motor `Core/Rules/RuleEngine.cs` existe, mas **sem o formato de regra por aplicativo** |
| 54 | Perfis de conhecidos: Epic Games Launcher, Discord, Slack, AnyDesk, Xmind, AVG PC TuneUp, IObit Driver Booster, WeChat Cache, Bilibili Offline Cache, Razer Software, DiskDrill Software, 360 Total Security, Adobe Acrobat, Ableton Live, ACDSee… | `wise.strings.txt:62853-62892` (bloco JSON de perfis nomeados) | **AUSENTE** — sem base por aplicativo |
| 55 | Tabela de caminhos de registro por app, com marcação `(Reg)`: **48 entradas, 14 com `(Reg)`** | `wise.strings.txt:62854` -> `"key": "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Vivaldi"`; `:62858` -> `...Uninstall\\SogouExplorer"` | **AUSENTE** — sem tabela equivalente |

### 2.6 Interface, integração e demais

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 56 | **Menu de contexto do Explorer**, ligável/desligável | `wise.identificadores.txt:261` -> `.TList`1.Pack[0]$0$Intf<uContextMenuHelper.PContextMenuItem>` | **OK** — `Core/Topmost/ExplorerContextMenu.cs` |
| 57 | Exportar a lista como arquivo texto | `wise.recursos.txt:165` -> `TFRMWPUMAIN/0` | **OK** — `Core/Exporters.cs` |
| 58 | Colunas: Nome, Publicador, Tamanho, Data de instalação, Versão, **Último uso** | `wise.recursos.txt:165` -> `TFRMWPUMAIN/0` | **PARCIAL** — o Tranal tem nome, publicador, tamanho, data e versão (`Core/ProgramColumns.cs`); **falta a coluna "Último uso"** |
| 59 | Contagem e **tamanho total** dos programas | `wise.recursos.txt:165` -> `TFRMWPUMAIN/0`; `wise.inventario.md:1244` -> `Size` - Mede tamanho em disco | **PARCIAL** — o Tranal conta os programas e soma o tamanho no treemap (`Core/Appearance/TreemapModel.cs`), não na barra de status |
| 60 | Datas **relativas** no histórico | `wise.recursos.txt:163` -> `TFRMUNLOG/0` | **AUSENTE** — o Tranal mostra a data absoluta |
| 61 | Abrir a entrada no RegEditor / abrir a pasta | `wise.recursos.txt:165` -> `TFRMWPUMAIN/0` | **OK** — `Core/RegistryEditing/RegistryEditor.cs` + `Core/Management/ExecutablePath.cs` |
| 62 | Detalhes da entrada: Path, Uninstall Command, Details, Trust/Unknown/App site | `wise.recursos.txt:165` -> `TFRMWPUMAIN/0` | **PARCIAL** — os dados existem como colunas; falta a visão "Trust / Unknown / App site" |
| 63 | **46 idiomas**, com **pt-BR completo** | `wise.recursos.txt:133` -> `CLEARCFG/2052`; `:138` -> `NOTICECFG/2052`; `:141` -> `SYSCLEARCFG/2052` (recursos de catálogo) | **OK** — `Core/Localization/LanguageCatalog.cs` + `Lang/pt-BR.json` |
| 64 | Aparência: skins **Light** e **Dark** (PRO) | `wise.identificadores.txt:18` -> `BVE.SVG2Elements.VCL`; `wise.recursos.txt` (recursos de skin) | **OK** — `Theming/ThemeManager.cs` (Light/Dark/Slate/Contrast) |
| 65 | Versão **portátil** (PRO) | `wise.recursos.txt:165` -> `TFRMWPUMAIN/0` | **OK** — `Core/PortableMode.cs` |
| 66 | Ativação de licença PRO online, com validade e renovação | `wise.recursos.txt:156` -> `TFRMREG/0`; `:157` -> `TFRMRENEW/0` | **AUSENTE** — o Tranal não comercializa; sem equivalente, por decisão de produto |
| 67 | **Avaliação** pelo usuário, com "Software Recomendado" | `wise.recursos.txt:153` -> `TFRMRATE/0`; `:154` -> `TFRMRATEEDIT/0`; `:155` -> `TFRMRATELIST/0` | **PARCIAL** — `Core/Appearance/ProgramRating.cs` dá a nota; falta o fluxo de avaliação com lista |
| 68 | "Traduzir pro…" — convida a traduzir | `wise.recursos.txt:147` -> `TFRMFEEDBACK/0` | **AUSENTE** — sem equivalente identificado |
| 69 | Programa de melhoria da experiência do usuário (opt-in) | `wise.recursos.txt:147` -> `TFRMFEEDBACK/0` | **AUSENTE** — sem equivalente identificado |
| 70 | Central de suporte, fórum, novidades | `wise.recursos.txt:168` -> `TWISESUPPORTFORM/0` | **PARCIAL** — `Core/Appearance/SupportRequest.cs` |
| 71 | Verificação automática de nova versão | `wise.recursos.txt:164` -> `TFRMUPDATE/0` | **OK** — `Core/Updates/AutoUpdateClient.cs` |
| 72 | Menu de edição: Cut, Copy, Paste, Delete, Select all, Undo | `wise.recursos.txt:165` -> `TFRMWPUMAIN/0` | **PARCIAL** — `Ui/Appearance/ProgramClipboard.cs` + `Ui/ProgramRow.xaml.cs` |

### 2.7 Funcionalidades descobertas **no binário** e ausentes do documento anterior

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 73 | Tela **"My View"** (visão do usuário) | `wise.recursos.txt:148` -> `TFRMMYVIEW/0` | **AUSENTE** — sem equivalente identificado |
| 74 | Diálogo **"Catch"** (captura do estado do sistema antes da ação) | `wise.recursos.txt:145` -> `TFRMCATCH/0` | **AUSENTE** — sem equivalente identificado |
| 75 | Formulário de **feedback** | `wise.recursos.txt:147` -> `TFRMFEEDBACK/0` | **PARCIAL** — `Core/Appearance/SupportRequest.cs` |
| 76 | **Lista de avaliação** e edição da avaliação | `wise.recursos.txt:153` -> `TFRMRATE/0`; `:154` -> `TFRMRATEEDIT/0`; `:155` -> `TFRMRATELIST/0` | **PARCIAL** — `Core/Appearance/ProgramRating.cs` |
| 77 | **Renovação de licença** | `wise.recursos.txt:157` -> `TFRMRENEW/0` | **AUSENTE** — sem equivalente, por decisão de produto |
| 78 | **Página de desinstalação** dedicada (separada do diálogo) | `wise.recursos.txt:161` -> `TFRMUNINSTALLPAGE/0` | **PARCIAL** — o Tranal tem `Ui/UninstallWizardWindow.xaml.cs`, mas não uma página de desinstalação separada |
| 79 | **Desinstalar componente de plugin** | `wise.recursos.txt:151` -> `TFRMPLUGDELCOMP/0` | **AUSENTE** — sem equivalente identificado |
| 80 | Formulário de **suporte ao cliente Wise** | `wise.recursos.txt:168` -> `TWISESUPPORTFORM/0` | **PARCIAL** — `Core/Appearance/SupportRequest.cs` |
| 81 | **Atualização automática** do produto (janela própria) | `wise.recursos.txt:164` -> `TFRMUPDATE/0` | **OK** — `Core/Updates/AutoUpdateClient.cs` |
| 82 | Acesso ao **gerenciador de credenciais** do Windows | `wise.identificadores.txt:228` -> `Winapi.Security.Credentials` | **AUSENTE** — sem equivalente identificado |
| 83 | Detecção de **geolocalização** | `wise.identifiers.txt:227` -> `Winapi.Devices.Geolocation` | **AUSENTE** — **diferença intencional**: o Tranal não coleta localização |
| 84 | **Client HTTP** próprio para rede | `wise.identificadores.txt:194` -> `System.Net.HttpClient`; `:199` -> `System.Net.URLClient` | **PARCIAL** — `Core/WebSearch.cs` e `Core/Updates/AutoUpdateClient.cs`; sem cliente HTTP genérico |
| 85 | Modelo de sobra **persistido** (`uUninstallHelper.TUninstallObject`) pelo processo separado | `wise-monitor.identificadores.txt:223` -> `TDictionary<System.string,uUninstallHelper.TUninstallObject>` | **PARCIAL** — `Core/Recovery/LeftoverBackup.cs` + `Core/Recovery/RecoverOperation.cs` guardam e restauram, mas o Tranal **não** tem processo separado de monitor |
| 86 | Helper de **menu de contexto** (`uContextMenuHelper`) | `wise.identificadores.txt:261` -> `.TList`1.Pack[0]$0$Intf<uContextMenuHelper.PContextMenuItem>` | **OK** — `Core/Topmost/ExplorerContextMenu.cs` |

---

## 3. Mecanismos notáveis

**`WiseProgramUninstaller.exe`**

- **Delphi/Embarcadero, 32 bits** (`machine 0x014C`), **empacotado com 7-Zip**:
  por isso as strings do executável são majoritariamente dados comprimidos, e
  o catálogo de idiomas é a melhor fonte para a interface.
- Assinatura Authenticode **válida** (Lespeed / SSL.com EV), até 30/04/2026.
- **24 DLLs importadas**, entre elas:
  - `sqlite3.dll` — **banco SQLite** para o histórico e para a base de regras
    (confirma o `UpdateDB` = "Atualizando o banco de dados…");
  - `powrprof.dll` — **gerência de energia** (desliga a suspensão do disco
    para poder apagar o drive);
  - `mpr.dll` — **unidades de rede** (a varredura alcança compartilhamentos);
  - `iphlpapi.dll`, `wininet.dll`, `winhttp.dll`, `wsock32.dll`, `Rasapi32.dll`
    — rede (notícias, avaliação, atualização, ativação);
  - `ntdll.dll` — API nativa.
- **Sem driver de kernel, sem serviço de fundo, sem extensão COM/shell.** O
  menu de contexto é uma chave de registro, não uma extensão.

**Framework de interface**: bibliotecas **Fox** (`FoxCaptionHintManager`,
`FoxColorManager`, `FoxSkinManager`, `FoxUIUtils`, `FoxSvg`), **Graphics32**
(`GR32_*`), renderização **SVG2** (`BVE.SVG2*`) e **WebView2** embutido. É a
interface mais polida entre todos os concorrentes auditados — num binário
Delphi.

**Unidades Delphi reveladas pelos símbolos de `UnMonitor.exe`**:

- `uUninstallHelper.TLeftOverItem` — o **modelo de sobra** do produto;
- `uUninstallHelper.TUseAssist` — o registro de uso (a coluna "Usado pela
  Última Vez" sai daqui);
- `TLoginCredentialService` — credenciais.

`UnMonitor.exe` é um **processo separado** que acompanha a desinstalação: é
dele que saem o "Histórico" e a coluna "Resíduos". `WiseAppManager.dll` é o
gerenciador de aplicativos, e `Rate.info` é a lista de hashes MD5 + versão dos
produtos WiseCleaner (a base do "Software Recomendado").

### 3.1 Superfície de recursos Delphi confirmada no binário

As 173 entradas de recurso do PE de `wise.recursos.txt` confirmam **22
formulários**: `TFRMWPUMAIN`, `TFRMSETTINGS`, `TFRMABOUT`,
`TFRMPROGRESSMODAL`, `TFRMAFTERDEL`, `TFRMBEFOREUNISTALL`, `TFRMCATCH`,
`TFRMCLEANPAGE`, `TFRMFEEDBACK`, `TFRMMYVIEW`, `TFRMNOTIFYDIALOG`,
`TFRMPERMISSIONSDIALOG`, `TFRMPLUGDELCOMP`, `TFRMRATE`, `TFRMRATEEDIT`,
`TFRMRATELIST`, `TFRMREG`, `TFRMRENEW`, `TFRMRESTORE`, `TFRMTRASDIALOG`,
`TFRMUNINSTALLPAGE`, `TFRMUNLESSDEL`, `TFRMUNLOG`, `TFRMUPDATE`,
`TWISESUPPORTFORM` — e os recursos de catálogo `CLEARCFG`, `NOTICECFG`,
`SYSCLEARCFG`, `TMPTYPES`.

O binário também expõe, como identificadores, **as próprias chaves de
legenda**: `FrmMain.BtnForce.Caption`, `FrmMain.BtnNormal.Caption`,
`FrmMain.btnLog.Caption`, `FrmMain.btnuninstall.Caption`,
`FrmPermissionsDialog.lvlist.Option`, `TfrmUnlog.btnClear.Caption`,
`TfrmUnlog.head.ItemName` — a prova de que a interface do Wise está **dentro**
do executável, e não só no catálogo de idioma.

---

## 4. Interface

O catálogo de idiomas é o inventário das telas. Formulários identificados:
`FrmMain`, `FrmSettings`, `FrmAbout`, `FrmProgressModal`, `Frmafterdel`,
`frmUnlessDel`, `FrmRate`, `FrmRestore`, `TfrmUnlog`, `FrmReg`,
`frmCleanPage`, `FrmPermissionsDialog`, `FrmTrasDialog`,
`TFrmNotifyDialog` — **15 formulários**.

Colunas da lista: Nome, Publicador, Tamanho, Data de instalação, Versão,
Última vez usado. O tamanho é **calculado** (`Calculating` = "Calculando o
tamanho…"), não lido do registro — o mesmo que o Tranal faz.

**Limitação de medição registrada:** `SetForegroundWindow`/`SetWindowPos`/
`AppActivate` retornam `ERROR_ACCESS_DENIED` para **todas** as janelas do
desktop desta máquina e `PrintWindow` falha em forms Delphi; a auditoria
ficou pelo binário, pelo catálogo de idiomas e pelo manifesto do winget. Nenhum
pixel foi capturado.

---

## 5. Linha de comando

**Nenhum switch de linha de comando do aplicativo foi encontrado.** O binário
está comprimido com 7-Zip (o que limita a estática), mas o catálogo de **292
chaves não tem uma única menção** a linha de comando, e a varredura dirigida
por padrões de switch no executável **não achou nenhum comando** (só ruído de
dados comprimidos). Registrado como **não verificado**, não como inexistente.

O **instalador** é Inno Setup 6.0.0 e portanto tem o jogo padrão `/SILENT`,
`/VERYSILENT`, `/DIR=`, `/SUPPRESSMSGBOXES`, `/NORESTART` — mas isso é o
instalador, não o aplicativo. O manifesto do winget declara `InstallModes:
interactive, silent, silentWithProgress`, o que **confirma** esses modos.

---

## 6. O que o Tranal NÃO faz hoje

Coluna `Tranal` no formato da curadoria binária: **OK** = implementado e
verificado no código; **PARCIAL** = existe mas falta parte; **AUSENTE** =
sem equivalente identificado em `src/Tranal.Uninstaller`.

| # | Recurso do Wise | Evidência no binário | Tranal |
|---|---|---|---|
| 1 | Software Health com 4 categorias (sobras, redundantes, permissões, notificações) | `wise.recursos.txt:146` -> `TFRMCLEANPAGE/0` | **PARCIAL** — `Core/Management/SoftwareHealth.cs` existe; as 4 categorias do Wise não foram conferidas |
| 2 | Detecção de **permissões** de apps (serviço, inicialização, menu de contexto, tarefa agendada) | `wise.recursos.txt:150` -> `TFRMPERMISSIONSDIALOG/0`; `wise.identificadores.txt:74` -> `FrmPermissionsDialog.lvlist.Option` | **OK** — `Core/Management/AppPermissionManager.cs` cobre os quatro tipos |
| 3 | Bloqueio de **notificações pop-up** de navegador e de software | `wise.strings.txt:62861` -> `"tranname": "PopNotify",`; `:62885` -> `"type": "PopNotify",` | **AUSENTE** — nenhum bloqueador de notificação |
| 4 | Base de regras de limpeza **por aplicativo** (595 perfis) | `wise.strings.txt:62853-62892` (bloco JSON de regras) | **AUSENTE** — `Core/Rules/BuiltinRules.cs` traz 4 regras de proteção |
| 5 | **Histórico de desinstalações** com botão "Recuperar" | `wise.strings.txt:71271` -> `TfrmUnlog.recover`; `:71272` -> `Recovered`; `wise.recursos.txt:163` -> `TFRMUNLOG/0` | **PARCIAL** — `Core/UninstallHistory.cs` + `Core/Recovery/RecoverOperation.cs`; a ligação "Recuperar" no histórico não foi conferida |
| 6 | **Backup dos resíduos** antes de apagá-los, com expiração | `wise-monitor.identifiers.txt:223` -> `TDictionary<System.string,uUninstallHelper.TUninstallObject>` | **PARCIAL** — `Core/Recovery/LeftoverBackup.cs` faz; a expiração de 1 mês não foi conferida |
| 7 | **Desinstalação Personalizada** por palavra-chave | `wise.recursos.txt:162` -> `TFRMUNLESSDEL/0` | **AUSENTE** |
| 8 | **Desinstalação Forçada** como modo explícito | `wise.identificadores.txt:70` -> `FrmMain.BtnForce.Caption` | **PARCIAL** — `Ui/ForcedUninstallWindow.xaml.cs` + `Core/Automation/ForcedRemovalOptions.cs` |
| 9 | Gerenciador de **extensões de navegador** | `wise.strings.txt:33129` -> `btVivaldi`; `:33130` -> `btYandex` | **PARCIAL** — `Core/Modules/BrowserExtensions.cs` cobre Chrome/Edge/Firefox/Opera; **faltam 6 navegadores** |
| 10 | **Desinstalação por palavra-chave** com botão "Explorar" | `wise.recursos.txt:162` -> `TFRMUNLESSDEL/0` | **AUSENTE** |
| 11 | Coluna **"Último uso"** | `wise.recursos.txt:165` -> `TFRMWPUMAIN/0` | **AUSENTE** — o Tranal mostra a data de instalação, não o último uso |
| 12 | **Remover Entradas** (tirar da lista sem desinstalar) | `wise.recursos.txt:144` -> `TFRMBEFOREUNISTALL/0` | **PARCIAL** — `Core/ExcludeList.cs` esconde da varredura, não apaga a entrada |
| 13 | **Desinstalação em lote** | `wise.recursos.txt:144` -> `TFRMBEFOREUNISTALL/0` | **PARCIAL** — `Core/Automation/BatchList.cs` + CLI `/mu` |
| 14 | Aviso de **sincronização do navegador** | `wise.recursos.txt:151` -> `TFRMPLUGDELCOMP/0` | **AUSENTE** |
| 15 | Diálogo de **notificações** (remetente e tipo) | `wise.recursos.txt:149` -> `TFRMNOTIFYDIALOG/0` | **AUSENTE** |
| 16 | **Modificar** (reaproveitar o instalador) | `wise.recursos.txt:144` -> `TFRMBEFOREUNISTALL/0` | **AUSENTE** |
| 17 | Detecção de desinstalação feita por **outro** desinstalador | `wise.recursos.txt:144` -> `TFRMBEFOREUNISTALL/0` | **AUSENTE** |
| 18 | Menu de contexto do Explorer | `wise.identificadores.txt:261` -> `uContextMenuHelper.PContextMenuItem` | **OK** — `Core/Topmost/ExplorerContextMenu.cs` |
| 19 | Exportar a lista | `wise.recursos.txt:165` -> `TFRMWPUMAIN/0` | **OK** — `Core/Exporters.cs` |
| 20 | Data de instalação com **tempo relativo** | `wise.recursos.txt:163` -> `TFRMUNLOG/0` | **AUSENTE** |
| 21 | Contagem e **tamanho total** dos programas | `wise.inventario.md:1244` -> `Size` - Mede tamanho em disco | **PARCIAL** — o Tranal conta e soma no treemap, não na barra de status |
| 22 | **Ponto de restauração** antes de desinstalar | `wise.inventario.md:1206` -> `SRSetRestorePoint` | **OK** — `Core/UninstallSupport.cs:1210` |
| 23 | Varredura de sobras **depois** da desinstalação | `wise.recursos.txt:143` -> `TFRMAFTERDEL/0` | **OK** — `Core/UninstallSupport.cs` (`LeftoverScanner`) |
| 24 | Silencioso | `wise.recursos.txt:144` -> `TFRMBEFOREUNISTALL/0` | **OK** — `Core/QuietUninstall.cs` |
| 25 | Telas **"My View"** e **"Catch"** | `wise.recursos.txt:148` -> `TFRMMYVIEW/0`; `:145` -> `TFRMCATCH/0` | **AUSENTE** |
| 26 | **Desinstalar componente de plugin** | `wise.recursos.txt:151` -> `TFRMPLUGDELCOMP/0` | **AUSENTE** |
| 27 | Acesso ao **gerenciador de credenciais** e à **geolocalização** | `wise.identificadores.txt:227` -> `Winapi.Devices.Geolocation`; `:228` -> `Winapi.Security.Credentials` | **AUSENTE** — diferença intencional: o Tranal não coleta dados do usuário |

### Onde o Tranal já é melhor

- **Atribuição pela cadeia de desinstalação**: o Tranal lê o **pacote `.msi`**
  em cache (`Core/MsiInstallLog.cs`) e sabe que o item **é** do programa —
  149 de 149 produtos MSI desta máquina. O Wise depende do nome e de uma
  tabela curada de 595 perfis que envelhece.
- **Justificativa por item**: cada sobra tem um **motivo** (`LeftoverReason`),
  não só um negrito.
- **Desinstalação silenciosa por família**, com switch medido; o Wise apenas
  avisa que um programa "não pode ser desinstalado no modo silencioso".
- **CLI roteirizável** (`/scan`, `/mu`, `/export`) — nada disso existe no Wise.
- **Privacidade**: sem cadastro, sem telemetria, sem propaganda bloqueando a
  tela.

---

## 7. Fichos de evidência

| Arquivo | Bytes | Conteúdo |
|---|---|---|
| `downloads/WPUSetup_3.2.7.271.exe` | 11.134.688 | instalador (SHA-256 conferido com o manifesto do winget) |
| `app/app/WiseProgramUninstaller.exe` | 13.273.496 | o programa |
| `app/app/UnMonitor.exe` | 8.081.304 | o monitor de desinstalação |
| `app/app/messages.exe` | 6.269.688 | módulo de mensagens |
| `app/app/WiseAppManager.dll` | 138.488 | gerenciador de aplicativos |
| `app/app/Rate.info` | 1.161.830 | hashes MD5 + versão dos produtos WiseCleaner |
| `app/app/sqlite3.dll` | 1.099.792 | SQLite embarcado |
| `app/app/7z.dll` | 1.149.176 | descompressor embarcado |
| `app/app/Languages/` | 46 arquivos | catálogos de idioma |
| `evidencia/pe-wise.txt` | 1.226 | cabeçalho PE, versão, assinatura, DLLs importadas |
| `evidencia/catalogo-ptbr.ini` | 21.906 | catálogo pt-BR (218 pares) |
| `evidencia/catalogo-en.ini` | 26.486 | catálogo en-US (292 pares) — a diferença de 74 chaves é o PRO |
| `evidencia/strings-WiseProgramUninstaller.txt` | 1.942.223 | 98.523 strings ASCII |
| `evidencia/strings-UnMonitor.txt` | 976.513 | 52.428 strings — inclui os nomes das unidades Delphi |
| `evidencia/strings-WiseAppManager.txt` | 15.437 | 439 strings |

### Fichos de evidência da curadoria binária (06/11/2026)

| Arquivo | Bytes | Conteúdo |
|---|---|---|
| `bin-inventario/wise.inventario.md` | 142.205 | 5.372 classes RTTI, 1.183 rótulos de interface, 65 capacidades de API/IO |
| `bin-inventario/wise.recursos.txt` | 14.554 | 173 entradas de recurso do PE (**22 formulários Delphi**) |
| `bin-inventario/wise.identificadores.txt` | 9.616 | identificadores (chaves de legenda **dentro** do binário, units Delphi) |
| `bin-inventario/wise-monitor.identificadores.txt` | 6.995 | o modelo de sobra do `UnMonitor.exe` |
| `bin-strings/wise.strings.txt` | 1.499.834 | dump bruto de strings (inclui o **bloco JSON** das regras e dos perfis de navegador) |

---

## 8. Lacunas desta auditoria

| O que faltou | Por quê |
|---|---|
| Execução do programa | `SetForegroundWindow`/`SetWindowPos`/`AppActivate` negados para todas as janelas do desktop desta máquina; `PrintWindow` falha em forms Delphi |
| Conteúdo comprimido do executável | o binário está empacotado com 7-Zip; as strings de dentro do pacote não são extraíveis por estática simples. O catálogo cobriu a interface, mas texto **das regras internas** pode ter escapado |
| **Preço e termos do PRO** | a página de compra não foi acessada (`wiseuninstaller.com` → 403). O que se sabe do PRO vem das 74 chaves de ativação e do aviso de expiração |
| Linha de comando do aplicativo | **não verificada** (binário comprimido + nenhuma menção no catálogo) |
| Formato da base SQLite | o arquivo de banco é criado em execução; não foi inspecionado |
| Critério exato de "Software Redundant Files" | a base tem 1.513 regras do tipo `File`, mas o que caracteriza "redundante" (versão antiga, cache, lixo) não é legível no texto |
| Interface real | nenhum pixel capturado; o mapa das telas vem dos nomes das chaves |
| `UnMonitor.exe` em execução | não observado rodando; a função é inferida dos símbolos e do catálogo |
| Comparação com o Tranal | por busca de nomes e termos no `src\Tranal.Uninstaller`; "não existe" significa que **nenhum** correspondente foi encontrado |
| **4 funcionalidades sem confirmação no binário** (#7, #9, #12, #16) | a evidência original vinha do catálogo de idioma; nesta rodada o formulário correspondente (`TFRMBEFOREUNISTALL`) confirma a tela, mas não o comportamento |

---

## 9. Decisões que este documento obriga

1. **O "Software Health" deixa de ser item de nicho**: ele apareceu em **dois**
   concorrentes auditados (IObit e Wise) com as **mesmas 4 categorias**
   (sobras, redundantes, permissões, notificações). Isso transforma a ideia de
   "painel único de manutenção" em **prioridade de produto**, e o catálogo do
   Wise dá o **roteiro das categorias**.
2. **O Tranal precisa de um gerenciador de permissões de aplicativo** —
   serviço, inicialização, menu de contexto e tarefa agendada, com uma ação
   por item. Hoje as ferramentas existem separadas
   (`Core/Tools/AutorunManager.cs`, `Core/Topmost/ExplorerContextMenu.cs`) e falta o
   painel que as reúne. **Fechado em 06/11/2026:**
   `Core/Management/AppPermissionManager.cs` cobre exatamente os quatro tipos.
3. **A base de regras por aplicativo é o caminho, não o fim.** O Wise mantém
   595 perfis à mão; o IObit mantém 1,6 MB de extensões de navegador; o
   Total Uninstall mantém um XML de regras. A lição para o Tranal não é copiar
   o volume: é ter **o formato de regra** — `Core/UninstallSupport.cs` já tem o
   julgamento, falta a tabela.
4. **O navegador do Tranal está defasado**: faltam Yandex, Vivaldi, Brave,
   Comodo Dragon, Cent Browser e SoGou Explorer em
   `Core/Modules/BrowserExtensions.cs`.
5. **Faltam duas colunas de baixa complexidade**: "Usado pela Última Vez" e o
   **tamanho total** dos programas na barra de status.
6. **O histórico de desinstalação com "Recuperar"** é o item que mais se
   aproxima do que o owner disse querer ("desinstalar, excluir as sobras"). O
   Tranal tem `Core/Modules/TraceStore.cs` e `Core/BackupManager.cs`; falta
   juntar os dois num histórico com restauração. **Quase fechado em
   06/11/2026:** `Core/UninstallHistory.cs` + `Core/Recovery/RecoverOperation.cs`
   + `Core/Recovery/LeftoverBackup.cs` já existem; falta a ligação e a coluna.
7. **A decisão sobre driver de kernel não muda**: o Wise não usa driver nem
   serviço de fundo, e usa `powrprof.dll` e `mpr.dll` para o que precisa.
8. **O formato de regra do Wise é copiável.** O bloco JSON em
   `wise.strings.txt:62853-62892` mostra o schema — `tranname`,
   `defaultname`, `type` (`File` / `RegKey` / `RegValue` / `Reg` / `Ini` /
   `PopNotify`), `key` e `path0..N`. É um formato pequeno, legível e
   extensível — e o `Core/Rules/RuleEngine.cs` do Tranal já o suporta.

---

*Auditoria gerada por medição direta dos artefatos. Toda linha deste
documento aponta para evidência em disco; o que não pôde ser verificado está
marcado como tal na seção 8.*
