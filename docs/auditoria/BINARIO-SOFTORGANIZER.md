# Lacunas - Soft Organizer 10.50

Binario analisado: `C:\Users\Matheus\.minimax\sessions\mvs_1a1a8292487b443c92dc9d3eac74f170\workspace\auditoria\softorganizer\app\so1050-10.50\SoftOrganizer.exe`  sha256: `9db3d05ec314e6c06d57f9787064ce047739a6d7186210a248178fe081ff5809` (29.574.040 bytes, versao 10.50, Chemtable Software)

Metodo: extracao **estatica** do PE, sem execucao. (1) Tabela completa de imports por DLL (904 funcoes) (`bin-inventario/softorganizer.imports.txt`); (2) recursos Delphi do PE - 32 formularios `T*`, recurso `APPSLIST` e estilo VCL escuro (`bin-inventario/softorganizer.recursos.txt`); (3) classes RTTI (4499) e rotulos (`bin-inventario/softorganizer.inventario.md`); (4) tipos WinRT embarcados (`bin-inventario/softorganizer.identificadores.txt`); (5) dump bruto de strings com 99.734 literais (`bin-strings/softorganizer.strings.txt`). Cada literal citado foi reconfirmado por `grep` no dump, com o numero da linha.

## Funcionalidades presentes no binario e ausentes no Tranal

| # | Funcionalidade | Evidencia no binario | Categoria | Impacto (alto/medio/baixo) |
|---|---|---|---|---|
| 2 | Painel lateral com a acao **INSTALL WITH TRACING** (PARCIAL) | nao confirmado no binario | interface | medio |
| 4 | Ordenacao **e agrupamento** da lista (PARCIAL) | nao confirmado no binario | interface | baixo |
| 9 | **Removal tools from the developers** (link para o desinstalador oficial) (AUSENTE) | `recursos.txt:107` → `TAPPLICATIONSREMOVALTOOLSFORM/0` | interface | medio |
| 10 | Filtro **"Large Applications"** (AUSENTE) | nao confirmado no binario | interface | baixo |
| 11 | Verificacao de novas versoes dos programas listados, com "Update to X" (PARCIAL) | `recursos.txt:136` → `TUPDATETHISAPPFORM/0` | integracao | medio |
| 15 | **Notificacoes internas** (sino) e dicas (AUSENTE) | `recursos.txt:127` → `TNOTIFICATIONSDATAMODULE/0` | interface | baixo |
| 20 | **Backup automatico** antes da remocao (PARCIAL) | nao confirmado no binario | recuperacao | medio |
| 22 | **Undo Changes Center** (AUSENTE) | `recursos.txt:134` → `TUNDOINGCHANGESCENTERFORM/0` | recuperacao | alto |
| 23 | **Reverter** a desinstalacao cronologicamente, renomear ou restaurar (AUSENTE) | `recursos.txt:134` → `TUNDOINGCHANGESCENTERFORM/0` | recuperacao | alto |
| 24 | **Descarte de mudancas antigas** do historico (AUSENTE) | nao confirmado no binario | recuperacao | medio |
| 25 | Aviso "use o Undo Changes Center para cancelar" (AUSENTE) | nao confirmado no binario | interface | medio |
| 26 | **Trace manager**: excluir itens e editar valores antes de aplicar (PARCIAL) | nao confirmado no binario | interface | medio |
| 29 | Aviso para **melhorar a qualidade do rastreio** (AUSENTE) | nao confirmado no binario | interface | baixo |
| 30 | **Ignore List** global de itens a nao remover na limpeza (PARCIAL) | nao confirmado no binario | remocao | medio |
| 36 | Rastreamento do **disco em tempo real** (notificacao do SO) (PARCIAL) | `imports.txt:10` → `KERNEL32.dll / FindFirstChangeNotificationW`, `FindNextChangeNotification` | integracao | medio |
| 39 | Log de mudancas em **arquivo por aplicativo** (`TracedApp.changes`) (PARCIAL) | nao confirmado no binario | relatorio | medio |
| 40 | Rastreamento de **DLLs em pastas do sistema** (PARCIAL) | nao confirmado no binario | varredura | medio |
| 41 | **Monitor em processo separado** (PARCIAL) | nao confirmado no binario | integracao | medio |
| 42 | Rastreamento de **aplicativos adicionais** instalados junto (AUSENTE) | nao confirmado no binario | integracao | medio |
| 43 | Extensao de **shell COM** para o menu de contexto (PARCIAL) | nao confirmado no PE (a DLL da extensao e arquivo do payload) | integracao | medio |
| 44 | Chave de configuracao "Windows context menu commands" (PARCIAL) | nao confirmado no binario | integracao | baixo |
| 46 | Deteccao de **desinstalacao externa** (app removido por fora) (AUSENTE) | `recursos.txt:102` → `TAPPLICATIONSEXTERNALDELETEMODULE/0` | integracao | medio |
| 50 | **"Rarely used apps"** (AUSENTE) | nao confirmado no binario | interface | medio |
| 51 | Leftovers de programas **ja desinstalados** (lista de apps deletadas) (PARCIAL) | `recursos.txt:100` → `TAPPLICATIONSDELETEDFORM/0` | organizacao | medio |
| 52 | **Instalacao silenciosa** de programas (AUSENTE) | nao confirmado no binario | automacao | alto |
| 53 | Instalacao de **novas versoes** dos programas (PARCIAL) | nao confirmado no binario | automacao | medio |
| 54 | Centro de dicas **"Did You Know"** com dicas contextuais (AUSENTE) | `recursos.txt:112` → `TDIDYOUKNOWCENTERFORM/0`; `:101` → `TAPPLICATIONSDIDYOUKNOWTIPSDATA/0` | interface | baixo |
| 55 | Aviso de **atualizacao do proprio app** ("Update This App") (PARCIAL) | `recursos.txt:136` → `TUPDATETHISAPPFORM/0` | integracao | baixo |
| 56 | **Backup e restauracao de chaves de registro inteiras** (PARCIAL) | `imports.txt:8` → `ADVAPI32.dll / RegSaveKeyExW`, `RegRestoreKeyW` | recuperacao | medio |
| 57 | **Carga, descarregamento e substituicao** de chaves de registro (AUSENTE) | `imports.txt:8` → `ADVAPI32.dll / RegLoadKeyW`, `RegUnLoadKeyW`, `RegReplaceKeyW` | remocao | medio |
| 58 | **Restart Manager** para liberar arquivos em uso (RmStartSession/RmGetList/RmRegisterResources) (PARCIAL) | `imports.txt:76` → `Rstrtmgr.dll / RmStartSession`, `RmGetList`, `RmRegisterResources`, `RmRestart`, `RmShutdown`, `RmEndSession` | remocao | alto |
| 59 | **Verificacao de assinatura digital** de executaveis (AUSENTE) | `imports.txt:56` → `CRYPT32.dll / CryptQueryObject`, `CryptMsgGetParam` | remocao | medio |
| 60 | **Enumeracao de impressoras** e fila de impressao (AUSENTE) | `imports.txt:18` → `WINSPOOL.DRV / EnumPrintersW`, `GetDefaultPrinterW`, `OpenPrinterW` | organizacao | medio |
| 61 | Descoberta de **dispositivos Bluetooth LE** (AUSENTE) | `identificadores.txt:58` → `Windows.Devices.Bluetooth`; `strings.txt:15394` → `TWinRTBluetoothLEAdapter` | integracao | baixo |
| 63 | Informacoes de **usuario e estacao de rede** (AUSENTE) | `imports.txt:12` → `NETAPI32.dll / NetUserGetInfo`, `NetWkstaGetInfo` | interface | baixo |
| 64 | Consulta da **Lixeira** para relatar itens removidos (AUSENTE) | `imports.txt:30` → `SHELL32.dll / SHQueryRecycleBinW` | interface | baixo |
| 65 | **Configuracao completa de MSI** (instalar, modificar, reparar) (PARCIAL) | `imports.txt:74` → `msi.dll / MsiConfigureProductW`, `MsiSetExternalUIW`, `MsiSetInternalUI` | remocao | alto |
| 67 | **Tarefas de segundo plano** executadas pelo app (AUSENTE) | `recursos.txt:110` → `TBACKGROUNDTASKSMODULE/0` | automacao | medio |
| 68 | **Itens compartilhados e dependentes** entre programas (AUSENTE) | `recursos.txt:130` → `TSHAREDANDDEPENDENTDATAMODULE/0` | organizacao | alto |
| 69 | **Startup gear** (gerenciador de inicializacao proprio) (AUSENTE) | `recursos.txt:132` → `TSTARTUPGEARDATAMODULE/0` | organizacao | alto |
| 70 | Formulario de **notacoes do log** de rastreamento (AUSENTE) | `recursos.txt:105` → `TAPPLICATIONSLOGNOTATIONSFORM/0` | interface | medio |
| 71 | Formulario do **resultado da reversao do log** (AUSENTE) | `recursos.txt:108` → `TAPPLICATIONSREVERTLOGRESULTFORM/0` | recuperacao | medio |
| 74 | Telas de **licenciamento** (chave, assinatura, expiracao) (AUSENTE) | `recursos.txt:118` → `TLICENSINGKEYENTERINGFORM/0`; `:121` → `TLICENSINGSUBSCRIPTIONEXPIREDFORM/0` | interface | baixo |
| 76 | **Impersonacao de token** e verificacao de privilegios (PARCIAL) | `imports.txt:8` → `ADVAPI32.dll / ImpersonateLoggedOnUser`, `SetThreadToken`, `PrivilegeCheck`, `RevertToSelf` | integracao | medio |
| 77 | **Notificacoes internas agendadas** (avisos de dica) (AUSENTE) | `recursos.txt:127` → `TNOTIFICATIONSDATAMODULE/0` | automacao | baixo |

**Total: 45 lacunas** — 26 `AUSENTE` e 19 `PARCIAL`, das 77 funcionalidades mapeadas na tabela de `SOFTORGANIZER.md`.

### Observacoes de impacto

- **Alto**: 22, 23, 52, 58, 65, 68, 69 — o **Undo Changes Center** (22, 23) e o par **itens compartilhados/dependentes + startup gear** (68, 69) sao a diferenca de produto mais clara do Soft Organizer em relacao ao Tranal; o **Restart Manager** (58) e a **configuracao de MSI** (65) sao diferencas tecnicas de motor.
- **Baixo**: 4, 10, 15, 29, 44, 54, 61, 63, 64, 74, 77 — dicas, filtros, Bluetooth, informacoes de rede e o bloco de licenca (74), que nao tem impacto porque o Tranal e free e sem licenca.
