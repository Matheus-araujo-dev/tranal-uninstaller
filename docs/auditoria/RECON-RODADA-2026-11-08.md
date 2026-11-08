# Reconciliacao da rodada de 08/11/2026 - o que mudou no codigo depois da apuracao

**Metodo: conferido no codigo, depois de implementado.** Esta rodada
implementou as lacunas de alto impacto que a apuracao de 07/11/2026
(`RECON-HIBIT-GEEK-IOBIT-UNINSTALR.md`) tinha medido como ausentes. Cada
linha abaixo foi conferida no codigo depois da implementacao, com o caminho
do arquivo e o simbolo.

O criterio e o mesmo da apuracao anterior:

- **JA EXISTE** - o Tranal entrega a capacidade. A coluna "Evidencia" traz
  `arquivo::simbolo`.
- **PARCIAL** - existe, mas falta parte; a nota diz o que falta.
- **FORA POR DECISAO** - limite declarado do projeto.

O numero da linha e o do catalogo canonico (`docs/auditoria/<CONCORRENTE>.md`).

---

## 1. HiBit Uninstaller

| # | Funcionalidade | Estado no Tranal | Evidencia no codigo |
|---|---|---|---|
| 4 | **Desinstalação forçada** reagendada para o próximo logon | JA EXISTE | `Core/Automation/UninstallResume.cs` (`SaveRemoval`, `Pending`, `ReadItems`, `Discard`) + `Core/Automation/UninstallSession.cs` (`Payload`). A sessao da remocao e gravada ANTES da remocao comecar, com a lista de itens que faltam; `tranal /sessions` lista o que ficou pela metade e `tranal /resume` continua do ponto em que parou. **Nota**: a retomada e por sessao, nao por `RunOnce` |
| 21 | Desinstalar **atualizações do Windows** (`wusa` / KB) | JA EXISTE | `Core/Management/WindowsUpdateManager.cs` (`Remove`, `RemoveMany`, `NumeroDoArtigo`, `ListInstalled`) — remove o KB por `wusa.exe /uninstall`; linha de comando `tranal /updates` |
| 69 | Gerenciador de **Processos** (painel) | JA EXISTE | `Core/Modules/ProcessInspector.cs` (`List`, `RunningProcess`) — lista o que roda, com memoria, caminho do executavel e o programa instalado que e o dono; tela em `Ui/Tools/ToolViews.cs` (`CreateProcessPanel`); linha de comando `tranal /processes` |
| 77 | **Limpador de Registro** com 15+ categorias | JA EXISTE | `Core/Tools/RegistryCleaner.cs` — 16 categorias: ActiveX, App Paths, classes COM, tipos de arquivo, regras de firewall, fontes, Image File Execution Options, produtos do Windows Installer, MuiCache, Abrir com, ProgID, itens de inicializacao, DLLs compartilhadas, extensoes do shell, TypeLib e entradas de desinstalacao. A regra e uma so: registro e sobra quando o caminho apontado nao existe mais no disco. Tela em `Ui/Tools/ToolViews.cs` (`CreateRegistryCleaner`); linha de comando `tranal /regclean` |
| 92 | **Criar serviço** do Windows | JA EXISTE | `Core/Management/ServiceManager.cs` (`Create`) — cria o servico pelo SCM do proprio Windows, com o modo de inicializacao escolhido; linha de comando `tranal /create-service` |
| 93 | **Criar tarefa agendada** | JA EXISTE | `Core/Management/ScheduledTaskManager.cs` (`Create`) — cria pelo `schtasks.exe` do proprio Windows, com disparo diario na hora escolhida; linha de comando `tranal /create-task` |

## 2. Geek Uninstaller

| # | Funcionalidade | Estado no Tranal | Evidencia no codigo |
|---|---|---|---|
| 29 | **Desregistro de COM** (`regsvr32 /u`) antes de remover | JA EXISTE | `Core/Automation/ComUnregister.cs` (`Unregister`, `UnregisterAll`, `RegisteredDlls`) — a lista vem do REGISTRO (a DLL so entra quando o registro aponta para ela) e o desregistro e `regsvr32 /u`; linha de comando `tranal /com-unregister` |

## 3. IObit Uninstaller

| # | Funcionalidade | Estado no Tranal | Evidencia no codigo |
|---|---|---|---|
| 29 | Desinstalar **Windows Updates** | JA EXISTE | `Core/Management/WindowsUpdateManager.cs` (`Remove`, `RemoveMany`) — remocao de KB por `wusa.exe /uninstall`, com o codigo de saida traduzido (0 ok, 3010 reiniciar, 2359302 nao instalado); linha de comando `tranal /updates` |

## 4. Uninstalr

| # | Funcionalidade | Estado no Tranal | Evidencia no codigo |
|---|---|---|---|
| 15 | Detecção de sobras via **MuiCache / TypeLib / AppX ModelID** | PARCIAL | `Core/Discovery/MuiCacheLeftovers.cs` (`Leftovers`, `ExtrairCaminho`) e `Core/Discovery/TypeLibLeftovers.cs` (`List`, `Leftovers`), medidos nesta maquina: o caminho do MuiCache esta no NOME do valor (e nao no dado) e o arquivo da TypeLib esta em "0\win32", com a versao da biblioteca no fim do caminho. **Nota**: a deteccao por AppX ModelID nao foi implementada |
| 24 | **Retomar a desinstalação após reinício** | JA EXISTE | `Core/Automation/UninstallResume.cs` (`SaveRemoval`, `Pending`, `ReadItems`) + `Core/Automation/UninstallSession.cs` (`Payload`, `Pending`) + `Core/Automation/UninstallPlan.cs` (`UninstallPlanExecutor.RunAsync`) — a sessao e gravada antes da remocao e guarda a lista de itens; `tranal /sessions` e `tranal /resume` |
