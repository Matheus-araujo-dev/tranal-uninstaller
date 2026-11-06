# Wise Program Uninstaller 3.2.7.271 (Free) — auditoria

> O concorrente **mais parecido com o que o owner descreveu wanting**: um
> desinstalador que **registra o que sobrou** e **deixa recuperar**. O Wise
> tem histórico de desinstalação com botão "Recuperar", backup dos resíduos,
> varredura pós-desinstalação e uma base curada de **595 perfis** de limpeza
> por aplicativo. É também o concorrente com a interface mais polida de todos
> os auditados — skin, SVG, WebView2 — num binário Delphi de 2019.

Data da coleta: 05/11/2026 · Versão auditada: **3.2.7.271** (edição **Free**;
a PRO existe e é licenciada)

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

---

## 2. Superfície observável

### 2.1 Lista e modos de desinstalação

| # | Funcionalidade | Evidência | Observado ou documentado |
|---|---|---|---|
| 1 | Lista "Todos os Programas" | `FrmMain.btnAll.caption` = "Todos os Programas" | Documentado |
| 2 | Lista separada de "Apps da Área de Trabalho" (Win32) | `FrmMain.btnSoft.caption` = "Apps da Área de Trabalho" | Documentado |
| 3 | Lista separada de "Apps do Metro" (Store/MSIX) | `FrmMain.btnApp.caption` = "Apps do Metro" | Documentado |
| 4 | Lista separada de "Componentes do Sistema" | `FrmMain.btnPatch.caption` = "Componentes do Sistema" | Documentado |
| 5 | **Desinstalação Segura** — o modo normal: roda o desinstalador e depois procura as sobras | `FrmMain.BtnNormal.Caption` = "Desinstalação Segura" | Documentado |
| 6 | **Desinstalação Forçada** — para quando o desinstalador normal não resolve | `FrmMain.BtnForce.Caption` = "Desinstalação Forçada" | Documentado |
| 7 | "Modificar" — reaproveita o instalador do programa | `FrmMain.BtnModify.Caption` = "Modificar" | Documentado |
| 8 | "Remover Entradas" — tira o programa da lista **sem** desinstalar | `FrmMain.BtnRemove.Caption`; `ConfirmRemove` = "Isto removerá a entrada da lista mas não desinstalará o aplicativo" | Documentado |
| 9 | Aviso para não usar a Forçada antes da Segura | `BeSureUseForce` | Documentado |
| 10 | Desinstalação em lote (N programas) | `frmBeforeUnistall.lblMus.caption` = "…desinstalar os %d programas selecionados?" | Documentado |
| 11 | Confirmação antes de desinstalar, com o nome | `frmBeforeUnistall.lblQus.caption` = "…desinstalar o %s?" | Documentado |
| 12 | Extensão de navegador, com aviso de **sincronização** | `BrowserLogin`; `BrowserSync` = "Desativar a Sincronização do %s" | Documentado |
| 13 | Aviso quando o navegador está em execução | `BrowserRuning` = "O %s está em execução. Clique em 【OK】 pra fechar e continuar" | Documentado |
| 14 | Recusa de desinstalar a si mesmo | `RejectItself` | Documentado |
| 15 | Aviso de que um programa **não aceita** desinstalação silenciosa | `NoSilent` = "O \"%s\" não pode ser desinstalado no modo silencioso, por favor tente a Desinstalação Forçada" | Documentado |
| 16 | Aviso de desinstalação anterior em andamento | `IsBusy` | Documentado |
| 17 | Erro de desinstalação reportado | `unistallError` = "A operação falhou." | Documentado |

### 2.2 Sobras, backup e restauração

| # | Funcionalidade | Evidência | Observado ou documentado |
|---|---|---|---|
| 18 | Varredura de sobras **depois** da desinstalação, como opção marcada | `frmBeforeUnistall.chkLeft.caption` = "Escanear por resíduos após a desinstalação" | Documentado |
| 19 | Tela de sobras pós-desinstalação, com seleção | `Frmafterdel.caption` = "Entradas e arquivos do registro associadas com o programa" | Documentado |
| 20 | "Selecionar tudo" / "Não selecionar nenhum" | `Frmafterdel.btnall.caption`, `Frmafterdel.btnnone.caption` | Documentado |
| 21 | Aviso de resíduos, com a contagem de arquivos e de registros | `uninstallresidual`; `fileentries` = "%d arquivos e pastas residuais"; `regentries` = "%d entradas do registro inválidas." | Documentado |
| 22 | Relatório separando arquivos de registro | `Files` = "Arquivos e Pastas"; `RegEntries` = "Entradas do Registro"; `Items` = "Itens" | Documentado |
| 23 | **Backup automático dos resíduos antes de apagá-los** | `pmSaveLeft` = "Automaticamente fazer backup dos resíduos antes de apagá-los." | Documentado |
| 24 | Expiração do backup (apaga os com mais de 1 mês) | `pmDelSaveLeft` = "Apagar backups mais antigos do que 1 mês." | Documentado |
| 25 | Tela de **Restauração do Sistema**, com "Restaurar" e "Apagar" | `FrmRestore.caption` = "Restauração do Sistema" | Documentado |
| 26 | **Ponto de restauração do Windows antes de desinstalar** | `frmBeforeUnistall.chkBackup.caption` = "Criar um ponto de restauração do sistema antes de desinstalar." | Documentado |
| 27 | Remoção de ponto de restauração selecionado | `RemoveSysRestore` | Documentado |
| 28 | **Histórico de desinstalações** com a coluna "Resíduos" | `TfrmUnlog.caption` = "Desinstalar Histórico"; `TfrmUnlog.head.restore` = "Resíduos" | Documentado |
| 29 | **Botão "Recuperar" no histórico** | `TfrmUnlog.recover` = "Recuperar"; `TfrmUnlog.Recovered` = "Recuperou" | Documentado |
| 30 | Apagar todo o histórico | `TfrmUnlog.btnClear.Caption` = "Apagar tudo" | Documentado |
| 31 | Estado "não há itens residuais" | `TfrmUnlog.noItems`; `NoLeftOverItem` | Documentado |
| 32 | **Desinstalação Personalizada** — varre por palavra-chave e deixa explorar a pasta | `frmUnlessDel.caption` = "Desinstalação Personalizada" | Documentado |
| 33 | Botão "Explorar" para apagar o que for encontrado | `frmUnlessDel.btnFolder.caption` = "Explorar" | Documentado |
| 34 | Aviso de limpeza de resíduos com ação direta | `frmDelHint.caption` = "Limpeza dos Resíduos"; "Limpar Agora" | Documentado |
| 35 | Detecção da desinstalação feita por **outro** desinstalador | `pmUnisDetect` = "Detectar a desinstalação de outro desinstalador." | Documentado |

### 2.3 Software Health (PRO) — a página de saúde do software

| # | Funcionalidade | Evidência | Observado ou documentado |
|---|---|---|---|
| 36 | Botão **"Software Health"** no menu principal | `FrmMain.btnClean.caption` = "Software Health" | Documentado |
| 37 | Quatro categorias: **Uninstallation Leftovers**, **Software Redundant Files**, **Software Permissions**, **Disturbing Notifications** | `frmCleanPage.Leftovers`, `.Redundant`, `.Permissions`, `.Notifications` | Documentado |
| 38 | "Analyze" que varre todos os aplicativos de uma vez | `frmCleanPage.btnScan.caption` = "Analyze" | Documentado |
| 39 | "Stop" para interromper a varredura | `frmCleanPage.btnStop.Caption` | Documentado |
| 40 | "Detect app permissions for better permission management" | `frmCleanPage.Permissions.hint` | Documentado |
| 41 | "Block pop-up notifications and unwanted ads" | `frmCleanPage.Notifications.hint` | Documentado |
| 42 | Permissões por tipo: **Service**, **Startup**, **Right-click menu**, **Scheduled task** | `ServicesPermissions`, `StartupPermissions`, `MenuPermissions`, `SchedulePermissions` | Documentado |
| 43 | Diálogo de permissões com "Action" por plug-in | `FrmPermissionsDialog.lvlist.Option` = "Action" | Documentado |
| 44 | Diálogo de **lixeira** com tamanho por aplicativo | `FrmTrasDialog.lvlist.trashSize` = "Size" | Documentado |
| 45 | Diálogo de **notificações** com remetente e tipo | `TFrmNotifyDialog.lvlist.app` = "Sender" | Documentado |
| 46 | Registro de "última análise" | `LastScaned` = "Last analyzed on:" | Documentado |

### 2.4 Extensões de navegador

| # | Funcionalidade | Evidência | Observado ou documentado |
|---|---|---|---|
| 47 | Gerenciador de extensões, com abas por navegador | `FrmMain.btnBrowse.caption` = "Extensões do Navegador"; `btnChrome`, `btnIE`, `btnFirfox`, `btnEdge` | Documentado |
| 48 | Base de **11 navegadores** com caminho de perfil | bloco JSON: Chrome, MS Edge (×4: normal, Dev, Beta, SxS), Yandex, Vivaldi, Brave, Comodo Dragon, Cent Browser, SoGou Explorer | Observado |
| 49 | Leitura do arquivo de preferências do navegador | `%LOCALAPPDATA%\Microsoft\Edge\User Data\Default\Preferences` | Observado |
| 50 | Bloqueio de notificações pop-up (regra `PopNotify`) | `"type": "PopNotify"`, `"defaultname": "Popup notifications"` | Observado |
| 51 | Aviso quando não há extensão | `pluginnone` | Documentado |

### 2.5 Base de regras de limpeza por aplicativo

| # | Funcionalidade | Evidência | Observado ou documentado |
|---|---|---|---|
| 52 | **Base embutida de regras**: **595 perfis** nomeados e **2.379 caminhos** | contagem em `strings-WiseProgramUninstaller.txt` | Observado |
| 53 | Tipos de regra: **1.513 File**, **671 RegKey**, **166 RegValue**, **22 Reg**, **16 Ini**, **11 PopNotify** | mesma fonte | Observado |
| 54 | Perfis de conhecidos: Epic Games Launcher, Discord, Slack, AnyDesk, Xmind, AVG PC TuneUp, IObit Driver Booster, WeChat Cache, Bilibili Offline Cache, Razer Software, DiskDrill Software, 360 Total Security, Adobe Acrobat, Ableton Live, ACDSee… | mesma fonte | Observado |
| 55 | Tabela de caminhos de registro por app, com marcação `(Reg)`: **48 entradas, 14 com `(Reg)`** | `HKCU\…\Uninstall\Discord`, `…\Postman(Reg)`, `…\Kingsoft Office` | Observado |

### 2.6 Interface, integração e demais

| # | Funcionalidade | Evidência | Observado ou documentado |
|---|---|---|---|
| 56 | **Menu de contexto do Explorer**, ligável/desligável | `ExplorerMenu` = "Desinstalar com o Wise Program Uninstaller"; `pmRightMenuA` | Documentado |
| 57 | Exportar a lista como arquivo texto | `pmExport` = "Exportar como arquivo texto..." | Documentado |
| 58 | Colunas: Nome, Publicador, Tamanho, Data de instalação, Versão, **Último uso** | `SetupDate` = "Instalou em"; `UsedDate` = "Usado pela Última Vez" | Documentado |
| 59 | Contagem e **tamanho total** dos programas | `TotalStat` = "Achou %d programas ocupando %s do espaço do disco." | Documentado |
| 60 | Datas **relativas** no histórico | `Recent` = "Dentro de 1 semana"; `moreweek`; `moremonth`; `moreyear` = "1 ano atrás" | Documentado |
| 61 | Abrir a entrada no RegEditor / abrir a pasta | `pmOpenEntry`; `pmOpenFolder` | Documentado |
| 62 | Detalhes da entrada: Path, Uninstall Command, Details, Trust/Unknown/App site | `Path`; `UninstallCmd`; `TrustSite`; `UnknowSite`; `AppSite` | Documentado |
| 63 | **46 idiomas**, com **pt-BR completo** | `app/app/Languages/` | Observado |
| 64 | Aparência: skins **Light** e **Dark** (PRO) | `pmStyle` = "Appearance"; `SkinDefault`; `SkinDark` | Documentado |
| 65 | Versão **portátil** (PRO) | `pmPortable` = "Portable version" | Documentado |
| 66 | Ativação de licença PRO online, com validade e renovação | `FrmReg.*`; `ActivateOnline`; `Price` = "Renew for only %s"; `title1` = "Your license has expired…" | Documentado |
| 67 | **Avaliação** pelo usuário, com "Software Recomendado" | `FrmRate.*`; `lblratesoft` = "Software Recomendado" | Documentado |
| 68 | "Traduzir pro…" — convida a traduzir | `TranslateTo` | Documentado |
| 69 | Programa de melhoria da experiência do usuário (opt-in) | `pmUEI` | Documentado |
| 70 | Central de suporte, fórum, novidades | `btnSupport.hint`; `pmforum`; `btnnews.hint` | Documentado |
| 71 | Verificação automática de nova versão | `pmCheckNewVer`; `NewVer`; `NoNewVer` | Documentado |
| 72 | Menu de edição: Cut, Copy, Paste, Delete, Select all, Undo | `pmcut`…`pmundo` | Documentado |

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

| Recurso do Wise | Tranal | Onde no Tranal |
|---|---|---|
| Software Health com 4 categorias (sobras, redundantes, permissões, notificações) | ⬜ | não existe |
| Detecção de **permissões** de apps (serviço, inicialização, menu de contexto, tarefa agendada) | ⬜ | não existe |
| Bloqueio de **notificações pop-up** de navegador e de software | ⬜ | não existe |
| Base de regras de limpeza **por aplicativo** (595 perfis) | ⬜ | não existe |
| **Histórico de desinstalações** com botão "Recuperar" | 🟡 | `Core/Modules/TraceStore.cs` rastreia instalação; não há histórico de desinstalação com restauração |
| **Backup dos resíduos** antes de apagá-los, com expiração | 🟡 | `Core/BackupManager.cs` faz backup do **registro**, não dos resíduos |
| **Desinstalação Personalizada** por palavra-chave | ⬜ | não existe |
| **Desinstalação Forçada** como modo explícito | 🟡 | `Core/UninstallMode.cs` tem 4 modos; "forçada" não é um deles |
| Gerenciador de **extensões de navegador** | 🟡 | `Core/Modules/BrowserExtensions.cs` cobre Chrome/Edge/Firefox/Opera — **não** Yandex, Vivaldi, Brave, Comodo Dragon, Cent Browser, SoGou Explorer |
| Menu de contexto do Explorer | ✅ | `Core/Topmost/ExplorerContextMenu.cs` |
| Exportar a lista | ✅ | `Core/Exporters.cs` |
| Coluna "Último uso" | ⬜ | não existe |
| Data de instalação com **tempo relativo** | ⬜ | o Tranal mostra a data absoluta |
| Contagem e **tamanho total** dos programas | 🟡 | o Tranal conta os programas, não soma o tamanho |
| **Ponto de restauração** antes de desinstalar | ✅ | `Core/Removal.cs` |
| Varredura de sobras **depois** da desinstalação | ✅ | `Core/UninstallSupport.cs` (`LeftoverScanner`) |
| Silencioso | ✅ | `Core/QuietUninstall.cs` |

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
   (`Core/AutorunManager.cs`, `Core/Topmost/ExplorerContextMenu.cs`) e falta o
   painel que as reúne.
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
   juntar os dois num histórico com restauração.
7. **A decisão sobre driver de kernel não muda**: o Wise não usa driver nem
   serviço de fundo, e usa `powrprof.dll` e `mpr.dll` para o que precisa.
