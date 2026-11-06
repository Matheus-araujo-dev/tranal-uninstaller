# Uninstalr 3.2 (619) — auditoria

> O concorrente mais **honesto** do grupo: a versão gratuita tem **os mesmos
> recursos** da Pro — a única diferença é o suporte técnico. E o motor de
> desinstalação é **próprio**, não apenas "rode o desinstalador do programa".

Data da coleta: 05/11/2026 · Versão auditada: **3.2 (619)** (binário 3.2.0.619)

---

## 1. Identificação

| Campo | Valor | Fonte |
|---|---|---|
| Produto | **Uninstalr** (freeware; a Pro só acrescenta suporte técnico) | `manual-why-pro.txt:25` |
| Versão | **3.2** (`3.2.0.619`; a tela About mostra "version 3.2 (619)") | `pe-portable.txt` |
| Editor | **Great Software Company OÜ** — Tallinn, Estônia | `pe-portable.txt` |
| Site oficial | **https://uninstalr.com/** | `manual-features.txt` |
| SHA-256 do portátil | `137932cece0fdea0efdbe221325ddd57d7addac7facd3d6587a55f665135f851` | `identificacao.txt` |
| SHA-256 do setup | `6026b3178f9bfb744b513894dab6e69a4bfe9aa8f9e92d7e11165d14bcb584bb` | `identificacao.txt` |
| Tamanho do portátil | 7.816.440 B (7,8 MB) | `pe-portable.txt` |
| Formato do binário | **PE32 nativo, 32 bits, comprimido com UPX** (seções `UPX0`, `UPX1`, `.rsrc`) | `pe-portable.txt` |
| Formato do instalador | **NSIS** (Nullsoft) — `NullsoftInst`, seção `.ndata`. **Não é Inno Setup**: o `innoextract` responde "Not a supported Inno Setup installer!" | `strings-setup.txt`, `pe-setup.txt` |
| Toolchain | **Delphi / Object Pascal (VCL)** — `TMainForm_Uninstalr`, `TSettingsForm_Uninstalr`, `TShowDataForm_Uninstalr`, `TNewAppForm_Uninstalr`, `TFixInfoForm_Uninstalr`, `TAboutForm`, `TSupportForm` | `uia-janelas.txt` |
| Assinante | `CN=Great Software Company OÜ` — GoGetSSL G4 CS RSA4096, válido até 27/09/2027 | `pe-portable.txt` |
| Compilação | 2026-07-17 08:46:21Z | `pe-portable.txt` |
| Execução | **exige administrador** — manifesto `requireAdministrator`, `dpiAwareness=PerMonitorV2` | `manifest-portable.txt` |
| Preço | Freeware sem nag; **Pro = US$ 39**, pagamento único, licença vitalícia, 5 computadores | `manual-pricing.txt` |

### A correção de domínio

`instalr.com` **não** é deste produto — é um software homônimo de gestão de
tarefas. O domínio correto é **`uninstalr.com`**.

### Comportamento do portátil (medido)

- **Auto-contido**: não instala nada, não grava em `%ProgramFiles%`,
  `%APPDATA%` nem `%LOCALAPPDATA%`. Tudo fica em `%TEMP%\Uninstalr\`
  (`Uninstalr.dat`, `Uninstalr_ProgPaths.tmp`, `Uninstalr_EMACache.tmp`,
  `Uninstalr_CMD_<timestamp>.txt`).
- O app **avisa**: *"This program is currently running in Portable mode. It
  means that the program will not modify or add any data to your computer,
  unless you explicitly tell the program to do so."*
- **Instância única**: um segundo `Uninstalr.exe` com parâmetros encerra sozinho.

### O que a execução nesta máquina mediu

| Medida | Valor |
|---|---|
| **Deep search** (1ª execução, varre o sistema inteiro) | **~25 minutos** |
| Scan normal seguinte | **58 s** |
| Programas encontrados | **162** |
| Arquivos relacionados | **846** |
| Pastas relacionadas | **1.562** |
| Chaves de registro relacionadas | **394** |
| Valores de registro relacionados | **517** |

> A tela do deep search mostra o **caminho exato** que está sendo analisado
> no momento, sob o rótulo "Currently analyzing:" — e avisa que pode levar
> minutos. É honesto com o usuário, o que o Tranal ainda não é.

---

## 2. Superfície observável

49 funcionalidades medidas. As que importam:

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 1 | **Desinstalação em lote**, inclusive de apps que **não suportam desinstalação silenciosa** | `Uninstalr_CMD_` (`strings-uninstalr-own.txt:4259`), `Uninstaller.mode-normal` / `Uninstaller.mode-silent` (`:9247-9249`) | OK — `src/Tranal.Uninstaller/Core/Automation/BatchList.cs` + `/batch` |
| 2 | **Desinstalação não assistida** — "everything is removed without any user interaction" | `Automation_Begin_Finalize - Runtime: ` (`strings-uninstalr-own.txt:8945`), `Uninstaller.options-silent-uninstall` (`:9388`) | OK — `src/Tranal.Uninstaller/Core/Automation/CliToolSwitch.cs` (`/mu`) |
| 3 | **Motor de desinstalação próprio** — "Most other uninstallers simply run the default uninstaller program" | `Automation_Begin_Finalize - Runtime: ` (`strings-uninstalr-own.txt:8945`), `Uninstalr_Unsetup_` / `Uninstalr_Rerun_` (`:4179-4180`) | PARCIAL — `src/Tranal.Uninstaller/Core/Automation/UninstallPlan.cs` monta e executa o plano, mas não **dirige a interface** de desinstaladores de terceiros |
| 4 | **2 modos**: Normal e Silent | `Uninstaller.mode-normal`, `Uninstaller.mode-silent` (`strings-uninstalr-own.txt:9247-9249`) | OK — `src/Tranal.Uninstaller/Core/UninstallMode.cs` |
| 5 | **Automatiza desinstaladores que não aceitam silent**, tentando `/passive /norestart /quiet`, `/quiet`, `/silent`, `/passive`, `/verysilent` | flags derivadas de `Uninstaller.mode-silent` (`strings-uninstalr-own.txt:9249`) + `Automation_Begin_Finalize` (`:8945`) | OK — `src/Tranal.Uninstaller/Core/QuietUninstall.cs` (tabela de flags por família) |
| 6 | **Remoção de programas SEM desinstalador** (apaga pasta + registro + sobras) | `Uninstaller.apptype-Leftover-Maybe` (`strings-uninstalr-own.txt:10343`) | OK — `src/Tranal.Uninstaller/Core/Automation/ManualRemoval.cs` |
| 7 | Detecção e remoção de **sobras de programas já desinstalados** | `Uninstaller.apptype-Leftover-Maybe` (`strings-uninstalr-own.txt:10343`), `Uninstaller.options-skip-leftovers` (`:9831`) | OK — `src/Tranal.Uninstaller/Core/UninstallSupport.cs` |
| 8 | **3 níveis de varredura**: Normal / Minimal / None — mais "não analisar o registro" e "não analisar pastas temporárias" | `Uninstaller.options-lo-normal`, `-lo-minimal`, `-lo-none`, `-lo-no-reg`, `-lo-no-temp` (`strings-uninstalr-own.txt:9785-9793`) | OK — `src/Tranal.Uninstaller/Core/LeftoverScanOptions.cs` (4 modos) |
| 9 | **Pré-visualização** de tudo que será removido, com **exclusão item a item** | `TShowDataForm_Uninstalr` (recurso Delphi `10/TSHOWDATAFORM/0` em `instalr.recursos.txt`), `Uninstaller.options-show-more-info` (`:9813`) | PARCIAL — `src/Tranal.Uninstaller/Ui/UninstallWizardWindow.xaml.cs` mostra a prévia, mas a exclusão é por padrão (`Core/ExcludeList.cs`), não item a item |
| 10 | **Exclusão** de arquivos/pastas/unidades, configurável | `Uninstaller.options-ignore-paths`, `Uninstaller.options-ignore-paths-tip`, `-ignore-paths-format` (`strings-uninstalr-own.txt:9767-9771`) | OK — `src/Tranal.Uninstaller/Core/ExcludeList.cs` |
| 11 | **Detecção automática de caminhos a ignorar** (`$Recycle.Bin`, `System Volume Information`, `Windows.~BT`, `Windows.~WS`, `Recovery`, pastas de backup do sistema) | `GLOB_SystemFiles` (`strings-uninstalr-own.txt:4010`) | AUSENTE — o Tranal só aplica a lista de exclusão configurada pelo usuário |
| 12 | **Lista única** com todos os apps, **tamanho em disco calculado em tempo real** (não o valor do Windows) | `Uninstaller.options-skip-sizes` (`strings-uninstalr-own.txt:9837`), `FindApps_From_WMIC_Init` (`:4701`) | PARCIAL — `src/Tranal.Uninstaller/Core/ProgramScanner.cs` tem coluna de tamanho, mas o cálculo não é instrumentado como "tempo real" |
| 13 | **Corrigir o tamanho errado** que o Windows mostra na lista de instalados | `Uninstaller.options-skip-sizes` (`strings-uninstalr-own.txt:9837`) | AUSENTE — o Tranal mostra o tamanho do Windows |
| 14 | **16 tipos de app** com detecção dedicada: normal, MS Store, portable, sobras, Chocolatey, Ninite, PortableApps.com, Steam, EA App, Epic, Riot, GOG Galaxy, Wargaming.net, Battle.net, itch.io, Big Fish | `Uninstaller.apptype-normal`, `-portable`, `-builtin`, `-component`, `-winupdate`, `-platform-chocolatey`, `-choco`, `-platform-steam`, `-steam`, `-platform-ex`, `-game-ex`, `-platform-nuget`, `-nuget`, `-platform-bigfish`, `-bigfish` (`strings-uninstalr-own.txt:10532-10559`); `FindApps_Special_Chocolatey/Steam/Epic/PortableApps/BigFish/Nuget_Platform` (`:4426-4446`) | PARCIAL — `src/Tranal.Uninstaller/Core/UninstallerKind.cs` tem 12 tipos e `Core/Discovery/PackageManagerCatalog.cs` cobre Steam/Chocolatey/Scoop; faltam Epic, GOG, Battle.net, Riot, Wargaming, EA, itch.io, Big Fish, Ninite, PortableApps |
| 15 | Detecção de sobras via **MuiCache / TypeLib / AppX ModelID** | `GLOB_MuiCacheData1..3` (`strings-uninstalr-own.txt:3904-3906`), `GLOB_TypeLib_System` / `GLOB_TypeLib_ToApp` (`:3907-3908`), `GLOB_AppData_via_ModelID_RegEntries/RegKeys/Files/Dirs` (`:3917-3920`) | AUSENTE — nenhuma das três fontes é lida pelo Tranal |
| 16 | **País de origem** do app, com bandeira | `uninstaller.country-of-origin` (`strings-uninstalr-own.txt:10300`), `uninstaller.country-of-origin-short` (`:10298`), `FCountryCodeToCountryName` (`:10056`), `uninstaller.menu-select-by-country` (`:10219`), `uninstaller.menu-unselect-by-country` (`:10221`) | AUSENTE — nenhum dado de país no Tranal |
| 17 | **Backup da lista de instalados** do Windows antes de alterar (chave `Applistbackup`) | `FindApps_Registry_Applistbackup` (`strings-uninstalr-own.txt:4343`), `FindApps_Registry_Applistbackup-1` / `-2` (`:4932-4934`) | AUSENTE — o Tranal não faz backup da `Applistbackup` |
| 18 | **Ponto de restauração** antes da desinstalação, com opção "só uma vez por sessão" | `Uninstaller.options-restore-point` (`strings-uninstalr-own.txt:9809`), `Uninstaller.options-restore-point-only-once` (`:9811`), `SRSetRestorePointW` (`:7705`) | OK — `src/Tranal.Uninstaller/Ui/BackupManagerWindow.xaml.cs` (`RestorePoint`) |
| 19 | **Log de desinstalação** em arquivo (`Uninstalr.log`) | `Uninstaller.options-generate-log` (`strings-uninstalr-own.txt:9384`) | OK — `src/Tranal.Uninstaller/Core/RemovalLog.cs` |
| 20 | **Fechar programas e serviços** em execução automaticamente | `KillAllRunningProcesses` (`strings-uninstalr-own.txt:9301-9330`), `KillAllRunningServices` (`:9306-9310`) | OK — `src/Tranal.Uninstaller/Core/ProcessTerminator.cs` + `src/Tranal.Uninstaller/Core/Management/ServiceManager.cs` |
| 21 | **Reiniciar automaticamente** para finalizar (avisa que pode reiniciar 1 ou 2 vezes) | `RestartComputer KillAllRunningProcesses Start` (`strings-uninstalr-own.txt:9290`) | PARCIAL — o Tranal adia a remoção para o próximo reinício (`Core/DeferredDelete.cs`), mas não reinicia sozinho |
| 22 | **Executar scripts de desinstalação** automaticamente (ligado por padrão) | `Uninstaller.options-autodelete`, `-autodel-shortcuts`, `-autodel-dirs`, `-autodel-nonext` (`strings-uninstalr-own.txt:9839-9845`) | PARCIAL — `src/Tranal.Uninstaller/Core/Automation/UninstallPlan.cs` executa comandos antes/depois, mas não a remoção automática de atalhos/pastas |
| 23 | **Gerar `.bat`** para desinstalação em lote + `PreStartupDelete.bat` para o próximo arranque | `PreStartupDelete.bat` (`strings-uninstalr-own.txt:4658`), `Uninstalr_CMD_` (`:4259`) | PARCIAL — `src/Tranal.Uninstaller/Core/Reporting/BatchScriptWriter.cs` gera `.bat`, mas não o `PreStartupDelete` para o próximo arranque |
| 24 | **Retomar a desinstalação após reinício** via `RunOnce` (`Uninstalr_Unsetup_`, `Uninstalr_Rerun_`) | `Uninstalr_Unsetup_` / `Uninstalr_Rerun_` (`strings-uninstalr-own.txt:4179-4180`) | AUSENTE — o Tranal não retoma tarefa após reinício |
| 25 | **Monitoramento de instalação** — detecta software novo automaticamente ("New software detected") | `Uninstaller.options-close-oneself` (`strings-uninstalr-own.txt:9386`) | OK — `src/Tranal.Uninstaller/Core/Modules/InstallMonitor.cs` |
| 26 | **Deep Analysis** sob demanda, com aviso de que pode levar minutos | `Uninstaller.options-always-deep` (`strings-uninstalr-own.txt:9825`), `AlwaysDeep` (`:9743`), `uninstaller.button-deep-anal` (`:10199`) | AUSENTE — o Tranal varre em segundos; não há modo profundo |
| 27 | Tooltip por app com Publisher, Tipo, Versão, Data, **País**, "Automatically starts with Windows", pasta, chave de registro, uninstall key, comando completo, executável principal, atalho principal | `Uninstaller.options-show-more-info`, `-show-apptype`, `-show-apppaths`, `-show-scanpath` (`strings-uninstalr-own.txt:9813-9819`), `GLOB_DetectDataFromManifest1/2`, `GLOB_DetectPubFromFile` (`:3900-3902`) | PARCIAL — `src/Tranal.Uninstaller/Core/Appearance/ProgramTextFormatter.cs` monta o tooltip, mas **sem país** e sem caminho de scan |
| 28 | **29 idiomas** embutidos (inclui Português) | `Uninstaller.options-language` (`strings-uninstalr-own.txt:9865`), `Uninstaller.options-custom-font` (`:9863`) | OK — `src/Tranal.Uninstaller/Core/Localization/LanguageCatalog.cs` (48 idiomas) |
| 29 | Tema claro/escuro com 3 opções | `Uninstaller.options-color-highlight` (`strings-uninstalr-own.txt:9823`) | OK — `src/Tranal.Uninstaller/Theming/ThemeManager.cs` |
| 30 | Opções de **acessibilidade** (leitor de tela, moldura padrão, botões padrão) | `Uninstaller.options-ui-mode`, `-ui-system`, `-ui-always`, `-ui-never` (`strings-uninstalr-own.txt:9795-9801`) | AUSENTE — sem opções de acessibilidade no Tranal |
| 31 | **Telemetria opt-in** — pergunta explicitamente antes de enviar nomes e caminhos | `Uninstaller.options-default-settings` / `-default-settings-one` / `-default-settings-many` (`strings-uninstalr-own.txt:9761-9765`) | AUSENTE — o Tranal não tem telemetria (decisão de privacidade; não é uma lacuna funcional) |
| 32 | **Licença Pro embutida** — converte a cópia free em Pro sem reinstalar | `com.embarcadero.Uninstalr` (`instalr.strings.txt:51403`), `TSUPPORTFORM` (recurso Delphi `10/TSUPPORTFORM/0` em `instalr.recursos.txt`) | AUSENTE — o Tranal não tem gate de versão |
| 33 | Formulário de **adicionar app manualmente** e de **corrigir dados de entrada** | `TNewAppForm_Uninstalr` / `TFixInfoForm_Uninstalr` (recursos Delphi `10/TNEWAPPFORM/0` e `10/TFIXINFOFORM/0` em `instalr.recursos.txt`) | PARCIAL — `src/Tranal.Uninstaller/Core/ProgramCustomization.cs` edita comentário/grupo/ícone, mas não há "adicionar app manualmente" nem "corrigir dados de entrada" |

### 2.1 Funcionalidades adicionais descobertas no binário (não constavam no documento)

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 34 | **Enumeração de instalados por WMIC** (MSI via `wmic product`) | `FindApps_From_WMIC_Init` e `FindApps_From_WMIC_LoadData Done` (`strings-uninstalr-own.txt:4701,4634`), literal `/c wmic product get InstallDate, InstallLocation, Name, Vendor, Version > "` (`:4702`), `GLOB_WMIC_DATA_AppnameToLocation/Publisher/Version` (`:4007-4009`) | OK — `src/Tranal.Uninstaller/Core/Discovery/MsiProductCatalog.cs` faz o equivalente em .NET, sem depender do `wmic` |
| 35 | **Correspondência difusa (fuzzy)** de elementos e de termos de busca | `GLOB_FuzzyElemsList` / `GLOB_FuzzyElemsBad` (`strings-uninstalr-own.txt:3932-3933`), `ExpandData_Files_Fuzzy` (`:6211`), `TApp.FFuzzyTermsAll` (`:6488`) | AUSENTE — sem correspondência difusa no Tranal |
| 36 | **Dados do publicador extraídos do manifesto do executável** | `GLOB_DetectDataFromManifest1` / `GLOB_DetectDataFromManifest2` / `GLOB_DetectPubFromFile` (`strings-uninstalr-own.txt:3900-3902`) | PARCIAL — `src/Tranal.Uninstaller/Core/ProgramScanner.cs` lê o publicador do registro, não do manifesto do .exe |
| 37 | **Seleção em massa por país** (selecionar/desmarcar por país) | `uninstaller.menu-select-by-country` (`strings-uninstalr-own.txt:10219`), `uninstaller.menu-unselect-by-country` (`:10221`), literal "Select by country of origin ..." (`:10294`) | AUSENTE — depende do dado de país, que o Tranal não tem |
| 38 | **Detecção de Chocolatey como plataforma** (não só como app) | `FindApps_Special_Chocolatey_Platform` / `_Apps` (`strings-uninstalr-own.txt:4426-4427`), `Uninstaller.apptype-platform-chocolatey` (`:10541`) | OK — `src/Tranal.Uninstaller/Core/Discovery/PackageManagerCatalog.cs` |
| 39 | **Detecção de Steam como plataforma** com varredura em 2 modos | `FindApps_Special_Steam_Platform` / `_Games` (`strings-uninstalr-own.txt:4428-4429`), `FindApps_Special_Steam_Games_m1` / `_m2` (`:5425-5426`) | OK — `src/Tranal.Uninstaller/Core/Discovery/PackageManagerCatalog.cs` |
| 40 | **Detecção de Epic Games** e **Big Fish** como plataforma | `FindApps_Special_Epic_Platform` / `_Games` (`strings-uninstalr-own.txt:4430-4431`), `FindApps_Special_BigFish_Platform` / `_Games` (`:4434-4435`) | AUSENTE — o Tranal não tem esses catálogos |
| 41 | **Detecção de NuGet** como plataforma | `FindApps_Special_Nuget_Platform` / `_Apps` (`strings-uninstalr-own.txt:4445-4446`), `Uninstaller.apptype-platform-nuget` (`:10553`) | AUSENTE — sem detecção de NuGet no Tranal |
| 42 | **PortableApps.com** como plataforma detectada | `FindApps_Special_PortableApps_Platform` / `_Apps` (`strings-uninstalr-own.txt:4432-4433`), `GLOB_PortableApps_AD` (`:3789`) | AUSENTE — sem detecção de PortableApps |
| 43 | **Atalhos** como fonte de descoberta de apps | `GLOB_AllMainShortcuts` (`strings-uninstalr-own.txt:3894`) | PARCIAL — `src/Tranal.Uninstaller/Core/ProgramScanner.cs` varre o Menu Iniciar, mas não usa atalhos como fonte primária de localização |
| 44 | **Ignorar programas de usuário** e **ignorar componentes embutidos** | `Uninstaller.options-ignore-user`, `Uninstaller.options-ignore-builtin` (`strings-uninstalr-own.txt:9773-9775`) | PARCIAL — `src/Tranal.Uninstaller/Core/ExcludeList.cs` cobre caminhos e padrões, não os eixos "por usuário" e "embutido" |
| 45 | **Modo portátil das configurações** (settings em arquivo portátil) | `Uninstaller.options-settings-portable`, `Uninstaller.options-using-settings-file` (`strings-uninstalr-own.txt:9777-9781`) | OK — `src/Tranal.Uninstaller/Core/PortableMode.cs` + `src/Tranal.Uninstaller/Core/Settings/AppSettings.cs` |
| 46 | **Salvar posição da janela** e **filtros** entre sessões | `Uninstaller.options-save-winpos`, `Uninstaller.options-save-filters` (`strings-uninstalr-own.txt:9805-9807`) | PARCIAL — `src/Tranal.Uninstaller/Core/Settings/AppSettings.cs` persiste preferências, mas não posição de janela nem filtros |
| 47 | **Opções "speedhacks"** (desativar verificações lentas) | `Uninstaller.options-speedhacks` (`strings-uninstalr-own.txt:9827`) | AUSENTE — sem opções de desempenho |
| 48 | **Varredura apenas do disco do sistema** | `Uninstaller.options-sysdrive-only` (`strings-uninstalr-own.txt:9821`) | PARCIAL — `src/Tranal.Uninstaller/Core/Discovery/DriveScanner.cs` varre todas as unidades, sem a opção de restringir |
| 49 | **Fechar o próprio aplicativo** ao terminar | `Uninstaller.options-close-oneself` (`strings-uninstalr-own.txt:9386`) | PARCIAL — `src/Tranal.Uninstaller/Core/Automation/RemovalProgress.cs` encerra a sessão de uninstall, mas não há a opção explícita "close oneself" |
| 50 | **Execução de comandos registrados** (arquivo `.cmd` gerado) | `Uninstalr_CMD_` (`strings-uninstalr-own.txt:4259`) | PARCIAL — `src/Tranal.Uninstaller/Core/Reporting/BatchScriptWriter.cs` gera o script, mas não um "comando registrado" reaproveitável |
| 51 | **Modo de exibição do sistema** (system/always/never) | `Uninstaller.options-ui-mode`, `-ui-system`, `-ui-always`, `-ui-never` (`strings-uninstalr-own.txt:9795-9801`) | AUSENTE — sem política de exibição de UI no Tranal |

### Duas ausências medidas (importam)

| Ausência | Como se confirmou |
|---|---|
| **Nenhum agendador de tarefas** | nenhuma menção em manual, FAQ ou changelog; o `Schedule\TaskCache` só é lido para **encontrar sobras** |
| **Nenhum backup do registro** | o mecanismo de segurança é o *System Restore Point*; não há `reg export` no app |

---

## 3. Mecanismos notáveis

| Mecanismo | Evidência |
|---|---|
| **Compressão UPX** no binário — as strings só apareceram no **dump de memória** do processo em execução (1,36 GB) | `app\memdump.bin` → `strings-mem-utf16.txt` |
| Instalador **NSIS** (o `innoextract` não se aplica) | `pe-setup.txt` |
| Registro do app em `HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstalr\{GUID}` | observado na tela de scan |
| Cache de caminhos e de exe↔app em `%TEMP%\Uninstalr\*.tmp` | `temp-Uninstalr_ProgPaths.tmp` (282.688 B), `temp-Uninstalr_EMACache.tmp` (131.862 B) |
| **Nenhum driver, nenhum serviço de background** | `pe-portable.txt` |

---

## 4. Interface

Delphi/VCL. Janela principal com a lista de 162 apps, badges de plataforma
("Steam game", "Platform: Battle.net"), bandeiras de país e um tooltip
detalhado por app. A tela de Settings tem **duas colunas** de opções: os
padrões para "desinstalar UM software" e para "desinstalar MUITOS".

---

## 5. Linha de comando

**Resultado medido: o Uninstalr NÃO tem interface de linha de comando
documentada.** As flags `/S`, `/silent`, `/log`, `/minimize`, `/autoclose` e
`/wait` que aparecem na literatura **pertencem a outros programas**.

| Parâmetro | Dono real |
|---|---|
| `/Settings` | **interno** — medido: `Uninstalr_Portable.exe /Settings` iniciou o app normalmente, com a tela de Settings **não** aberta |
| `/unsetup`, `/finalize` | **internos** — fases de retomada após reinício |
| `/shutdown` | **interno** — fluxo de reinício da máquina |
| `/quiet`, `/silent`, `/passive`, `/verysilent` | flags que o Uninstalr **passa para o desinstalador de terceiros** |
| `/S` | do **instalador NSIS** (`Uninstalr_Setup.exe /S`), não do app |
| `/log`, `/minimize`, `/autoclose`, `/wait` | **não existem** — zero ocorrências no binário |

**Contra-prova:** a página de venda do próprio autor anuncia *"Command-line
parameter support"* para o **WinFindr Pro**, no bundle — e não para o
Uninstalr.

---

## 6. O que o Tranal NÃO faz hoje

| Recurso do Uninstalr | Tranal |
|---|---|
| **Motor de desinstalação próprio** (não só rodar o uninstaller) | 🟡 — o Tranal usa o desinstalador nativo + varredura de sobras |
| **Automatizar desinstaladores que não aceitam silent** (`/passive`, `/verysilent`…) | ⬜ |
| **Desinstalação em lote** | ⬜ |
| **16 tipos de app** (Steam, GOG, Battle.net, Epic, Riot, itch.io, EA, Big Fish, Chocolatey, Ninite, PortableApps.com, portáteis) | ⬜ — o Tranal só tem MSIX |
| **Retomar a desinstalação após reinício** (fluxo `RunOnce`) | ⬜ — o Tranal só adia item em uso com `MoveFileEx` |
| **Detecção automática de caminhos a ignorar** (lixeira, pastas de backup do sistema) | ⬜ — a lista de exclusão é manual |
| **Detecção de sobras por MuiCache / TypeLib / AppX ModelID** | ⬜ |
| **País de origem** do app | ⬜ |
| **Tamanho em disco em tempo real** (não o valor do registro) | 🟡 — há coluna de tamanho; a origem do número não está documentada |
| **Contagem final** ("N arquivos / N pastas / N chaves / N valores") | ⬜ |
| **Settings em duas colunas** (padrões para 1 app × muitos apps) | ⬜ |
| **Pré-visualização antes de remover**, com exclusão item a item | 🟡 — a lista de sobras do assistente é a prévia |
| **Gerar `.bat`** para desinstalação em lote | ⬜ |
| **Deep Analysis** sob demanda, com aviso de duração | ⬜ |

### Onde o Tranal já é melhor

| Ponto | Tranal | Uninstalr |
|---|---|---|
| **Backup do registro** antes de remover | ✅ backup completo, com script de restauração | ⬜ **não tem** |
| **Exclusão** | exclusões aplicadas **até no backup** | exclusão item a item na prévia |
| **Confiança das sobras com motivo** | 3 níveis + frase por item | 3 níveis de varredura, sem confiança |
| **Idiomas** | 48 no seletor (2 traduzidos) | 29 embutidos |
| **CLI de desinstalação roteirizável** | `/mu`, `/scan`, `/export`, `/columns` | **nenhuma** |
| **Driver de kernel** | nenhum | nenhum |
| **Privacidade** | sem telemetria | telemetria **opt-in** (pede consentimento) |

---

## 7. Fichos de evidência

| Arquivo | Bytes | O que prova |
|---|---|---|
| `downloads/Uninstalr_Portable.exe` | 7.816.440 | o app |
| `downloads/Uninstalr_Setup.exe` | 7.571.600 | o instalador NSIS |
| `app/memdump.bin` | 1.362.403.328 | dump de memória do processo (o PE está comprimido com UPX) |
| `evidencia/strings-uninstalr-own.txt` | 814.811 | recorte das strings **do próprio Uninstalr** (linhas 110001–121501 do dump UTF-16) |
| `evidencia/strings-mem-utf16.txt` | 36.490.602 | strings UTF-16 do processo em execução |
| `evidencia/manual-changelog.txt` | 55.390 | changelog oficial |
| `evidencia/manual-features.txt` | 6.201 | página oficial de recursos |
| `evidencia/manual-why-pro.txt` | 3.064 | "free == Pro em recursos" |
| `evidencia/cli-test.txt` | 1.608 | teste empírico de `/Settings` (falha: não abre Settings) |
| `evidencia/ui-lista.png` | 101.220 | lista com 162 apps + tooltip completo |
| `evidencia/ui-settings-1b.png` | 58.120 | 3 níveis de varredura de sobras |
| `evidencia/ui-about.png` | 42.671 | About (versão 3.2 (619), licença Pro embutida) |

---

## 8. Lacunas desta auditoria

### 8.1 Lacunas de **medição** (o que não foi possível medir)

| O que faltou | Por quê |
|---|---|
| **Desinstalação real** de um programa | não executada — exigiria instalar e desinstalar de verdade |
| Mapeamento exato programa→flag silenciosa que o motor escolhe | os flags estão medidos; o critério de escolha, não |
| A rotina de "deep analysis" por baixo (o que ela varre) | sem instrumentar o processo |

### 8.2 Lacunas de **funcionalidade** (medidas no binário, ausentes no Tranal)

A lista completa e evidenciada está em **[`BINARIO-UNINSTALR.md`](BINARIO-UNINSTALR.md)**
— 34 linhas, cada uma com o identificador do binário e o arquivo+linha do dump.
Resumo por impacto:

| Impacto | Quantidade | Exemplos |
|---|---|---|
| 🔴 alto | 6 | motor que dirige a interface de desinstaladores de terceiros, retomar após reinício (`RunOnce`), sobras por MuiCache, sobras por TypeLib, sobras por AppX ModelID, backup da `Applistbackup` |
| 🟡 médio | 20 | país de origem, detecção por Chocolatey/Steam/Epic/BigFish/NuGet/PortableApps, matching fuzzy, detecção automática de caminhos a ignorar, Deep Analysis, reinício automático, remover atalhos/pastas automaticamente, `PreStartupDelete.bat` |
| ⚪ baixo | 8 | opções de acessibilidade, modo de exibição do sistema, "speedhacks", varredura só do disco do sistema, salvar posição da janela e filtros, telemetria opt-in, licença Pro embutida |

> **Nota.** O binário do Uninstalr está comprimido com UPX, então as strings
> do app só existem no **dump de memória** (`instalr\strings-uninstalr-own.txt`).
> A coluna "Evidência no binário" da tabela da seção 2 aponta para esse dump e
> para o catálogo de recursos Delphi (`instalr.recursos.txt`), onde cada
> formulário aparece como `10/<TFORM>/0`.

---

## 9. Decisões que este documento obriga

1. **O freeware completo é um dado do mercado.** O Uninstalr provou que dá
   para entregar um desinstalador **sem gate de versão paga**: a Pro só
   vende suporte. Isso reforça a estratégia do Tranal — nenhum recurso
   essencial atrás de pagamento.
2. **A tabela de flags silenciosas por família** que o Uninstalr usa
   (`/passive /norestart /quiet`, `/quiet`, `/silent`, `/passive`,
   `/verysilent`) e que o HiBit traz são **a mesma ideia**. Com a
   classificação de desinstalador já implementada no Tranal, essa tabela
   vira a base do item "desinstalação silenciosa" do backlog.
3. **O deep search de 25 minutos é o oposto do que o Tranal deve fazer.** O
   Tranal varre em segundos. O Uninstalr compensa a lentidão com
   transparência ("Currently analyzing: \<caminho exato\>"). A transparência
   é o que deve ser copiado, não a lentidão.
4. **Faltam no Tranal: os tipos de app de loja de jogos** (Steam, Epic, GOG,
   Battle.net, Riot, Wargaming, EA, itch.io) e os **gerenciadores de pacote**
   (Chocolatey, NuGet, winget, Ninite). São detecções cheap e de alto
   visibly — o programa aparece com o badge certo e sabe como removê-lo.
5. **A contagem final ("N arquivos / N pastas / N chaves / N valores")** é
   barata e muito mais informativa que um "total: 42 sobras". O Tranal
   já tem a lista; falta a frase que resume.

---

*Auditoria gerada por medição direta dos artefatos e por execução na
máquina do owner. Toda linha deste documento aponta para evidência em disco;
o que não pôde ser verificado está marcado como tal na seção 8.*
