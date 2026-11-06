# Lacunas - Uninstalr

Binario analisado: `auditoria\instalr\downloads\Uninstalr_Portable.exe`  sha256: `137932cece0fdea0efdbe221325ddd57d7addac7facd3d6587a55f665135f851` (7.816.440 bytes, Delphi/Embarcadero, v3.2.0.619)
Metodo: o PE esta comprimido com UPX, entao as strings do app nao aparecem no dump do arquivo. A extracao veio do **dump de memoria do processo em execucao** (`instalr\strings-uninstalr-own.txt`, recorte UTF-16 das linhas 110001-121501) e do catalogo de recursos Delphi (`bin-inventario\instalr.recursos.txt`, onde cada formulario aparece como `10/<TFORM>/0`). Cada linha abaixo foi reconfirmada por `grep` no dump; a coluna "Evidencia no binario" traz o identificador literal e o arquivo+linha.

## Funcionalidades presentes no binario e ausentes no Tranal

| # | Funcionalidade | Evidencia no binario | Categoria | Impacto (alto/medio/baixo) |
|---|---|---|---|---|
| 1 | **Motor de desinstalacao que dirige a interface** de desinstaladores de terceiros (nao apenas executa o comando) | `Automation_Begin_Finalize - Runtime: ` (`strings-uninstalr-own.txt:8945`), `Uninstalr_Unsetup_` / `Uninstalr_Rerun_` (`:4179-4180`) | automacao | alto |
| 2 | **Retomar a desinstalacao depois do reinicio** via `RunOnce` | `Uninstalr_Unsetup_` / `Uninstalr_Rerun_` (`strings-uninstalr-own.txt:4179-4180`) | automacao | alto |
| 3 | **Deteccao de sobras por MuiCache** | `GLOB_MuiCacheData1` / `GLOB_MuiCacheData2` / `GLOB_MuiCacheData3` (`strings-uninstalr-own.txt:3904-3906`) | varredura | alto |
| 4 | **Deteccao de sobras por TypeLib** | `GLOB_TypeLib_System` / `GLOB_TypeLib_ToApp` (`strings-uninstalr-own.txt:3907-3908`) | varredura | alto |
| 5 | **Deteccao de sobras por AppX ModelID** (registro, chaves, arquivos e pastas) | `GLOB_AppData_via_ModelID_RegEntries` / `_RegKeys` / `_Files` / `_Dirs` (`strings-uninstalr-own.txt:3917-3920`) | varredura | alto |
| 6 | **Pais de origem** do app, com bandeira | `uninstaller.country-of-origin` (`strings-uninstalr-own.txt:10300`), `uninstaller.country-of-origin-short` (`:10298`), `FCountryCodeToCountryName` (`:10056`) | interface | medio |
| 7 | **Selecionar/desmarcar em massa por pais** | `uninstaller.menu-select-by-country` (`strings-uninstalr-own.txt:10219`), `uninstaller.menu-unselect-by-country` (`:10221`), literal "Select by country of origin ..." (`:10294`) | interface | medio |
| 8 | **Deteccao de Chocolatey como plataforma** (separada de app) | `FindApps_Special_Chocolatey_Platform` / `_Apps` (`strings-uninstalr-own.txt:4426-4427`), `Uninstaller.apptype-platform-chocolatey` (`:10541`) | varredura | medio |
| 9 | **Deteccao de Steam como plataforma**, com varredura em dois modos | `FindApps_Special_Steam_Platform` / `_Games` (`strings-uninstalr-own.txt:4428-4429`), `FindApps_Special_Steam_Games_m1` / `_m2` (`:5425-5426`) | varredura | medio |
| 10 | **Deteccao de Epic Games** como plataforma | `FindApps_Special_Epic_Platform` / `_Games` (`strings-uninstalr-own.txt:4430-4431`) | varredura | medio |
| 11 | **Deteccao de Big Fish** como plataforma | `FindApps_Special_BigFish_Platform` / `_Games` (`strings-uninstalr-own.txt:4434-4435`), `Uninstaller.apptype-platform-bigfish` (`:10557`) | varredura | medio |
| 12 | **Deteccao de NuGet** como plataforma | `FindApps_Special_Nuget_Platform` / `_Apps` (`strings-uninstalr-own.txt:4445-4446`), `Uninstaller.apptype-platform-nuget` (`:10553`) | varredura | medio |
| 13 | **Deteccao de PortableApps.com** | `FindApps_Special_PortableApps_Platform` / `_Apps` (`strings-uninstalr-own.txt:4432-4433`), `GLOB_PortableApps_AD` (`:3789`) | varredura | medio |
| 14 | **Correspondencia difusa (fuzzy)** de elementos e de termos de busca | `GLOB_FuzzyElemsList` / `GLOB_FuzzyElemsBad` (`strings-uninstalr-own.txt:3932-3933`), `ExpandData_Files_Fuzzy` (`:6211`), `TApp.FFuzzyTermsAll` (`:6488`) | varredura | medio |
| 15 | **Backup da lista de instalados** do Windows (chave `Applistbackup`) antes de alterar | `FindApps_Registry_Applistbackup` (`strings-uninstalr-own.txt:4343`), `FindApps_Registry_Applistbackup-1` / `-2` (`:4932-4934`) | recuperacao | alto |
| 16 | **Deteccao automatica de caminhos a ignorar** (pastas de sistema, backup, `$Recycle.Bin`) | `GLOB_SystemFiles` (`strings-uninstalr-own.txt:4010`) | varredura | medio |
| 17 | **Deep Analysis** sob demanda, com modo "sempre profundo" | `Uninstaller.options-always-deep` (`strings-uninstalr-own.txt:9825`), `AlwaysDeep` (`:9743`), `uninstaller.button-deep-anal` (`:10199`) | varredura | medio |
| 18 | **Reiniciar o computador automaticamente** para finalizar a desinstalacao | `RestartComputer KillAllRunningProcesses Start` (`strings-uninstalr-own.txt:9290`) | automacao | medio |
| 19 | **Remocao automatica de atalhos, pastas e proximos passos** ao desinstalar | `Uninstaller.options-autodelete`, `-autodel-shortcuts`, `-autodel-dirs`, `-autodel-nonext` (`strings-uninstalr-own.txt:9839-9845`) | remocao | medio |
| 20 | **PreStartupDelete.bat** — script de limpeza que roda no proximo arranque | literal `PreStartupDelete.bat` (`strings-uninstalr-own.txt:4658`) | automacao | medio |
| 21 | **Execucao de comandos registrados** (`.cmd` reaproveitavel, `Uninstalr_CMD_`) | literal `Uninstalr_CMD_` (`strings-uninstalr-own.txt:4259`) | automacao | medio |
| 22 | **Opcoes de acessibilidade** (leitor de tela, moldura padrao, botoes padrao) | `Uninstaller.options-ui-mode`, `-ui-system`, `-ui-always`, `-ui-never` (`strings-uninstalr-own.txt:9795-9801`) | interface | baixo |
| 23 | **Modo de exibicao do sistema** (system / always / never) | `Uninstaller.options-ui-mode`, `-ui-system`, `-ui-always`, `-ui-never` (`strings-uninstalr-own.txt:9795-9801`) | interface | baixo |
| 24 | **Opcoes "speedhacks"** para desligar verificacoes lentas | `Uninstaller.options-speedhacks` (`strings-uninstalr-own.txt:9827`) | interface | baixo |
| 25 | **Ignorar programas de usuario** e **ignorar componentes embutidos** como eixos de filtro | `Uninstaller.options-ignore-user`, `Uninstaller.options-ignore-builtin` (`strings-uninstalr-own.txt:9773-9775`) | interface | medio |
| 26 | **Corrigir o tamanho errado** que o Windows mostra na lista de instalados | `Uninstaller.options-skip-sizes` (`strings-uninstalr-own.txt:9837`) | interface | medio |
| 27 | **Fechar o proprio aplicativo** ao terminar | `Uninstaller.options-close-oneself` (`strings-uninstalr-own.txt:9386`) | interface | baixo |
| 28 | **Salvar posicao da janela e filtros** entre sessoes | `Uninstaller.options-save-winpos`, `Uninstaller.options-save-filters` (`strings-uninstalr-own.txt:9805-9807`) | interface | baixo |
| 29 | **Publisher extraido do manifesto do executavel** (e nao do registro) | `GLOB_DetectDataFromManifest1` / `GLOB_DetectDataFromManifest2` / `GLOB_DetectPubFromFile` (`strings-uninstalr-own.txt:3900-3902`) | varredura | medio |
| 30 | **Atalhos como fonte de descoberta** de aplicativos | `GLOB_AllMainShortcuts` (`strings-uninstalr-own.txt:3894`) | varredura | medio |
| 31 | **Varredura apenas do disco do sistema** (opcao) | `Uninstaller.options-sysdrive-only` (`strings-uninstalr-own.txt:9821`) | interface | baixo |
| 32 | **Telemetria opt-in** com confirmacao explicita | `Uninstaller.options-default-settings`, `-default-settings-one`, `-default-settings-many` (`strings-uninstalr-own.txt:9761-9765`) | integracao | baixo |
| 33 | **Licenca Pro embutida** no app (free == Pro em recursos) | `TSUPPORTFORM` (recurso Delphi `10/TSUPPORTFORM/0`, `instalr.recursos.txt:124`), `com.embarcadero.Uninstalr` (`instalr.strings.txt:51403`) | organizacao | baixo |
| 34 | **Formulario de correcao de dados** de entrada e de **adicionar app manualmente** | `TFIXINFOFORM` (recurso `10/TFIXINFOFORM/0`, `instalr.recursos.txt:118`), `TNEWAPPFORM` (recurso `10/TNEWAPPFORM/0`, `instalr.recursos.txt:121`) | organizacao | medio |
