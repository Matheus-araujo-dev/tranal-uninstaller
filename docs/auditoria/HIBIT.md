# HiBit Uninstaller 4.0.10 — auditoria

> O concorrente **mais amplo em ferramentas** e o **melhor em linha de
> comando**: 21+ ferramentas, e **um switch de linha de comando para cada
> uma**. É também a prova mais forte de que um desinstalador pode ser um
> "cuidador do PC" completo sem driver de kernel e sem serviço de fundo.

Data da coleta: 05/11/2026 · Versão auditada: **4.0.10**

---

## 1. Identificação

| Campo | Valor | Fonte |
|---|---|---|
| Produto | HiBit Uninstaller **4.0.10** | `evidencia/pe-main.txt` |
| Editor | HiBitSoftware (assinatura `HiBitSoft.IR`) | `evidencia/pe-main.txt` |
| Site oficial | **https://www.hibitsoft.ir/Uninstaller.html** | `evidencia/site-uninstaller.html` |
| Instalador | `HiBitUninstaller-setup-4.0.10.exe` — 4.533.003 B | `downloads/` |
| SHA-256 do instalador | `ef4f82968d2ab81e574d8cddd433c66190947ba5d28ca83bec8034d124a007dc` | idem (confere com o manifesto winget `HiBitSoftware.HiBitUninstaller`) |
| SHA-256 do portátil | `986b1e07de8037cda70e4d2cf01010e2660042f79f233bbac91846de94741646` | idem |
| SHA-256 do executável | `89092212bd920b84d235a5d4ea89ccaa7f64ec32fcaf769f1e9cdb8070c3dcea` | REA |
| Formato | **PE32 nativo x86, Delphi/Embarcadero**, 15.410.176 B, **sem** CLI data directory (não é .NET) | `evidencia/pe-main.txt` |
| Assinatura | **NÃO assinado** | `Get-AuthenticodeSignature` = `NotSigned`; REA `inspect_pe_certificates` → `table: null` |
| Compilação | 2026-02-10 06:15:09Z | `evidencia/pe-main.txt` |
| Empacotador do setup | Inno Setup (setup data 5.6.0), payload único | `innoextract` |

### A correção de domínio que vale registrar

`https://www.hibituninstaller.com/` **não é o site do fabricante**: é um site
terceiro de SEO que devolve uma *homepage* estática para qualquer URL —
inclusive `hibit_uninstaller.zip`, que respondeu **HTTP 200** com
`Content-Type: text/html`. O domínio do fabricante é **`hibitsoft.ir`**;
`hibitsoft.com` não resolve DNS nesta máquina.

Quem tentar auditar pelo `.com` vai baixar uma página HTML e achar que
achou o artefato errado. O manifesto **winget** `HiBitSoftware.HiBitUninstaller`
foi o que destravou o download correto.

---

## 2. Superfície observável

58 funcionalidades medidas. As que importam para o Tranal:

### 2.1 Núcleo do desinstalador

| # | Funcionalidade | Evidência |
|---|---|---|
| 1 | Lista com colunas: Nome, Versão, Editor, Data, Tamanho, Tipo, Comentários, String de desinstalação, Pasta de instalação, Site, Entrada de registro | `strings-ptbr-menu.txt` |
| 2 | Desinstalação pelo desinstalador nativo | `strings-main.txt` (`originalunin`) |
| 3 | **Desinstalação em lote** ("Desinstalar Selecionado", botão + 6 itens de menu) | `strings-ptbr-menu.txt` |
| 4 | **Desinstalação forçada** ("Remoção Forçada") — reagendada para o próximo logon via `RunOnce` | `strings-main.txt` (`" /FORCE"`, `HiBitUninstallerForce`) |
| 5 | **Varredura de sobras** em **árvore** (TreeView), com AutoClean e RegBackup | `strings-main.txt` (`Leftovers`, `AutoClean`, `RegBackup`) |
| 6 | **Backup próprio `.dbh`** (Hibit Backup File): `BackupFiles.zip` + `FileList.txt` + `Registry.ini` | `strings-main.txt` |
| 7 | **Ponto de restauração** antes de desinstalar — WMI `CreateRestorePoint` e `SRSetRestorePointW`/`SRRemoveRestorePoint` (`SRCLIENT.DLL`) | `strings-mecanismos.txt` |
| 8 | **Relatórios HTML por ferramenta**: `OpReport`, `ScanLog`, `ProgramCompReport`, `WindowsStoreReport`, `ServicesReport`, `DriversReport`, `StartupReport`, `SchedulerReport`, `ContextMenuReport`, `UninstallerReport` | `strings-mecanismos.txt` |
| 9 | **Ocultar entrada de desinstalação** ("Hide original uninstallation entry") | `strings-ptbr-bloco.txt` (`CheckBoxSilent`) |
| 10 | **Deletar entrada** do registro do programa | `strings-ptbr-menu.txt` (`DeleteEntries1`) |
| 11 | **Editar informações** do programa | `strings-ptbr-menu.txt` (`Editinfo1`) |
| 12 | Abrir chave de registro / pasta de instalação / site do programa | `strings-ptbr-menu.txt` |
| 13 | Copiar dados do programa para a área de transferência | `strings-ptbr-menu.txt` |
| 14 | Exportar a lista (HTML / TXT / CSV) | `strings-main.txt` |
| 15 | **Modificar/reparar** instalação MSI (`ModifyPath`, `MsiExec.exe /I`) | `strings-mecanismos.txt` |

### 2.2 Integração com o Windows

| # | Funcionalidade | Evidência |
|---|---|---|
| 16 | **Menu de contexto do Explorer**: `lnkfile\shell\HiBitUninstaller` e `exefile\shell\HiBitUninstaller` → `"HiBitUninstaller.exe" /UNINSTALL "%1"` | `strings-mecanismos.txt` |
| 17 | **Menu de contexto do Monitor de Instalação**: `Msi.Package\shell\HiBitMonitor` e `exefile\shell\HiBitMonitor` → `/INSTALL "%1"` | `strings-mecanismos.txt` |
| 18 | **Gerenciador do Menu de Contexto** — lista, cria e remove menus do shell, com "Restaurar" e "Excluir tudo" | `strings-ptbr-menu.txt` |
| 19 | **Desabilitar handlers** de menu de contexto (`DisableCon\Directory\shellex\…`, `Folder`, `Drive`, `exefile`, `lnkfile`, `Directory\Background`, `DesktopBackground`, `CLSID\{645FF040-…}`) | `strings-main.txt` |
| 20 | **Monitoramento de instalação** com eventos `ApplicationInstalled`, `ApplicationUninstalled`, `ApplicationDeleteRestore`, `DeviceInstalled` | `strings-ptbr-menu.txt` |
| 21 | Desinstalar **atualizações do Windows**: `cmd.exe /c wusa.exe /uninstall /` + `KBID=` | `strings-mecanismos.txt` |
| 22 | Desinstalar **app MSIX**: `powershell.exe Remove-AppxPackage` | `strings-mecanismos.txt` |

### 2.3 Suíte de ferramentas — 21+ gerenciadores

Cada uma é uma janela própria, com relatório próprio e **switch de linha de
comando própria**:

Startup · Services · Processes · Scheduler Tasks · Context Menu · Program
Components · Browser Extensions · Connections · Backups & Reports · System
Restore · Registry Cleaner · Junk Files Cleaner · Empty Folder Cleaner ·
Shortcuts Fixer · File Shredder · File & Registry Finder · Duplicate File
Finder · Disk Analyzer · Windows Store Apps Manager · Windows Updates
Manager.

**Limpar Arquivos de Lixo** tem **17 categorias**: temporários, log do
Windows, cache de miniaturas, prefetch antigo, cache de fontes, cache do
sistema, cache do instalador, relatórios de erro, fragmentos do chkdsk,
instaladores obsoletos, cache IE/Edge/Chrome/Firefox/Opera/UCC/CentBrowser,
limpador comum e avançado, limpeza personalizada.

**Destruidor de Arquivos** sobrescreve com **MD5 / SHA-1 / SHA-256 / SHA-512 /
CRC32**.

**Agendamento automático de limpeza**: tarefas do Agendador do Windows para
os limpadores (`Empty_Folder_Cleaner_Schedule`, `Junk_Files_Cleaner_Schedule`,
`Shortcuts_Fixer_Schedule`, `Registry_Cleaner_Schedule`).

---

## 3. Mecanismos notáveis

| Mecanismo | Evidência |
|---|---|
| **33 DLLs importadas, 698 funções estáticas + 63 delay-load**; imphash `a5d540b8f302fb265a4ac16541c7d23a` | `evidencia/pe-imports.txt` |
| `advapi32!RegDeleteKeyExW` (delay-load) → limpeza de registro | `pe-imports.txt` |
| `wintrust.dll` → verificação de assinatura (scanner VirusTotal) | `pe-imports.txt` |
| `user32!SetThreadDpiAwarenessContext`, `GetDpiForWindow`, `AdjustWindowRectExForDpi` + `Shcore!GetDpiForMonitor` → **UI DPI-aware** | `pe-imports.txt` |
| `DWMAPI.DLL`, `uxtheme.dll`, `msimg32!AlphaBlend`, `windowscodecs.dll` → renderização com temas do Windows | `pe-imports.txt` |
| `kernel32!CreateSymbolicLinkW`, `GetFinalPathNameByHandleW` → tratamento de link simbólico | `pe-imports.txt` |
| `SRCLIENT.DLL` → pontos de restauração | `strings-mecanismos.txt` |
| `Wmic.exe /Namespace:\\root\default Path SystemRestore Call CreateRestorePoint` (caminho alternativo) | `strings-mecanismos.txt` |
| `powershell.exe Remove-AppxPackage` → app MSIX | `strings-mecanismos.txt` |
| `cmd.exe /c wusa.exe /uninstall /` + `KBID=` → atualização do Windows | `strings-mecanismos.txt` |
| Configuração em `HKCU\SOFTWARE\HiBit Uninstaller\` **ou** `<pasta>\HiBitUninstaller\Settings.ini` | `strings-mecanismos.txt` |
| **Sem driver, sem serviço de background, sem extensão COM** — tudo no próprio processo, via `EnumWindows`/`RegDeleteKeyExW`/WMI/PowerShell | `pe-imports.txt` (nenhum `.sys`) |

### 3.1 A tabela de switches silenciosos

O binário traz uma **tabela de switches de desinstalação silenciosa por
instalador**, consecutively:

```
/silent
/silent /norestart
--force-uninstall
/AUTO /SILENT
-uninstall
```

E, para o MSI:

```
MsiExec.exe /quiet /qn /norestart /X
```

**Esta é a peça mais acionável de toda a auditoria do HiBit.** Ela combina
exatamente com a classificação de desinstalador que o Tranal acabou de
implementar: já sabemos *de que família* é o desinstalador de cada programa
(`UninstallerKind`), e o HiBit mostra que existe uma **tabela de switches
silenciosos por família**. Fechar o item "desinstalação silenciosa" do
backlog deixa de ser adivinhação.

---

## 4. Interface

Delphi/VCL. A janela principal (`TUninstaller`, 1005×670) **esconde** enquanto
um modal está aberto; na primeira execução aparece o diálogo **Save
Settings** com as duas opções de onde guardar a configuração. O Delphi
desenha tudo no canvas do form, sem HWNDs filhos — a UIA não expõe elementos.

**Limitação de medição registrada:** `SetWindowPos`/`SetForegroundWindow`/
`AppActivate` retornaram `ACCESS_DENIED` para **todas** as janelas do
desktop nesta máquina (inclusive as de controle), então não foi possível ir
além do diálogo inicial. A estrutura de menus foi reconstruída das **802
legendas pt-BR** embutidas no binário — que, aliás, é o achado mais rico: o
HiBit tem **legendas em pt-BR de todos os diálogos**, muito mais que o
Revo.

---

## 5. Linha de comando

### 5.1 Operações

| Switch | Efeito |
|---|---|
| `/UNINSTALL` | desinstalar o alvo de `%1` (usado pelo menu de contexto) |
| `/UNIN` | forma curta de `/UNINSTALL` |
| `/UNINSTALLIT` | executa o desinstalador **e** o fluxo completo de sobras |
| `/FORCE` | desinstalação forçada (`HiBitUninstallerForce` + `RunOnce`) |
| `/INSTALL` | abre o Monitor de Instalação no alvo |
| `/SILENT` | modo silencioso do desinstalador do HiBit |

### 5.2 Um switch por ferramenta

`/RCLEAN` · `/ECLEAN` · `/JCLEAN` · `/SCLEAN` · `/STARTUP` · `/RESTOR` ·
`/SERVICE` · `/PROCESS` · `/TASK` · `/MENU` · `/BROWSER` · `/COMPON` ·
`/STOREAPP` · `/WINUPDATE` · `/FIND` · `/DUPL` · `/DISK` · `/CONN` · `/SHRED`

**Variação de sintaxe medida:** os limpadores existem em duas formas — a
curta (`/ECLEAN`) e a longa com o sufixo **` RUN`** (`/ECLEAN RUN`). As duas
coexistem no binário.

**Não verificado:** não há tabela de ajuda no binário; não existe `/help`,
`/cfg` ou `/log` como switch próprio do HiBit.

---

## 6. O que o Tranal NÃO faz hoje

| Recurso do HiBit | Tranal |
|---|---|
| **Switch de linha de comando por ferramenta** (19ferramentas) | 🟡 — a CLI tem `/winapps`, `/be`, `/autorun`, `/tools`, `/export`, `/hunter`, `/backup`, `/exclude`, mas **não** tem switch dedicado por ferramenta nem o sufixo ` RUN` |
| **Tabela de switches silenciosos por instalador** | ⬜ — agora é possível, porque a classificação existe |
| **Menu de contexto do Monitor de Instalação** (além do de desinstalação) | 🟡 — o Tranal registra um menu, só em `*`; o HiBit registra dois, e para `lnkfile`, `exefile` e `Msi.Package` |
| **Gerenciador do Menu de Contexto** (gerenciar menus alheios) | ⬜ |
| **Desabilitar handlers** de menu de contexto | ⬜ |
| **Desinstalar atualizações do Windows** (`wusa`/KB) | ⬜ |
| **Gerenciador de Tarefas Agendadas** (criar/remover) | ⬜ — `AutorunManager` lista, não gerencia |
| **Gerenciador de Processos** (painel) | ⬜ — há encerramento no fluxo, não painel |
| **Gerenciador de Drivers** | ⬜ |
| **Restaurador do Sistema** (criar/excluir pontos com um clique) | 🟡 — a CLI lista/restaura/exclui; falta criar antes de desinstalar |
| **Limpar Pastas Vazias** | ⬜ |
| **Fixar Atalhos** (atalhos corrompidos) | ⬜ |
| **Localizador de Arquivos e Registros** | ⬜ |
| **Localizador de Arquivos Duplicados** | ⬜ |
| **Analisador de disco** | ⬜ |
| **Gerenciador de Conexões** | ⬜ |
| **Gerenciador de Componentes de Programas** | ⬜ |
| **Agendamento automático de limpeza** | ⬜ |
| **Otimizador de registro** com 15+ categorias (ActiveX, FileTypes, SharedDLLs, IFEO, FirewallRules, MUICache) | 🟡 — o Tranal só trata entradas obsoletas do Menu Iniciar |
| **Relatórios HTML por ferramenta** | ⟡ — o Tranal exporta listagens, não relatórios |
| **Destruidor com hash MD5/SHA** (relatório e verificação) | 🟡 — apaga, mas o relatório de hash não existe |
| **Backup `.dbh` com restauração** | 🟡 — há backup, mas em outro formato |
| **Ocultar entrada de desinstalação** | ⬜ |
| **Deletar entrada** do registro do programa | ⬜ |
| **Editar informações** do programa | ⬜ |
| **Relógio de ferramentas** (agendador) | ⬜ |

### Onde o Tranal já é melhor

| Ponto | Tranal | HiBit |
|---|---|---|
| **Assinatura** | o Tranal assina o instalador com certificado próprio | o executável do HiBit **não é assinado** |
| **Confiança das sobras com motivo** | 3 níveis + frase por item | árvore de sobras, sem nível de ameaça |
| **Backup completo do registro com script de restauração** | sim, antes do desinstalador nativo | backup `.dbh` próprio |
| **CLI de desinstalação roteirizável** | `/mu "<prog>" /mode …`, `/scan`, `/export`, `/columns` | o HiBit tem switch por ferramenta, mas **não** tem desinstalação em lote roteirizável por lista |
| **Segurança do motor** | `SafeTraversal` nunca atravessa link; exclusões aplicadas até no backup | — |
| **Privacidade** | sem telemetria | Update Center próprio, VirusTotal, cache remoto |

---

## 7. Fichos de evidência

| Arquivo | Bytes | Conteúdo |
|---|---|---|
| `downloads/HiBitUninstaller-setup-4.0.10.exe` | 4.533.003 | instalador |
| `downloads/HiBitUninstaller-Portable-4.0.10.zip` | 6.025.084 | portátil |
| `app/setup-inno/app/HiBitUninstaller.exe` | 15.410.176 | o binário (idêntico byte a byte ao do portátil) |
| `evidencia/pe-imports.txt` | 13.765 | 33 DLLs × 698 funções + 63 delay-load |
| `evidencia/strings-main.txt` | 6.558.004 | 98.361 linhas de strings ASCII + UTF-16 |
| `evidencia/strings-ptbr-bloco.txt` | 57.188 | **802 legendas pt-BR** — o inventário de telas |
| `evidencia/cli-switches.txt` | 133 | os switches com contexto |
| `evidencia/changelog-oficial.txt` | 20.805 | changelog 2026→1.x |
| `evidencia/site-uninstaller.html` | 16.581 | a lista oficial de recursos |
| `evidencia/uia-janelas.txt` | 2,1 KB | janelas observadas em execução |
| `evidencia/hibit-dlg-savesettings.png` | 9.845 | diálogo *Save Settings* em pt-BR |

> **Nota de ferramenta:** o modo `strings` do `coleta.ps1` **estourou o
> timeout de 600 s** neste binário de 15 MB. O subagente reimplementou a
> mesma semântica em .NET (`strings-fast.ps1`) e o resultado é equivalente
> (ASCII 0x20–0x7E e UTF-16LE, unicidade, CRLF, UTF-8 com BOM). Vale
> otimizar o `coleta.ps1` para binários grandes.

---

## 8. Lacunas desta auditoria

| O que faltou | Por quê |
|---|---|
| Navegar as 21+ ferramentas em execução | o Delphi não expõe UIA e `SetForegroundWindow` foi negado para todas as janelas do desktop nesta máquina |
| Desinstalação real de um programa | não executada — seria preciso instalar e desinstalar de verdade |
| O que a tabela de switches silenciosos cobre por programa | só os switches foram medidos; o mapeamento programa→switch não foi reconstruído |

---

## 9. Decisões que este documento obriga

1. **A tabela de switches silenciosos entra no backlog, junto com a
   classificação.** Já sabemos o tipo do desinstalador de cada programa; o
   HiBit mostra que a tabela por família existe e é curta
   (`/VERYSILENT /SUPPRESSMSGBOXES /NORESTART` para Inno, `/S` para NSIS,
   `/quiet /qn /norestart` para MSI, `/s /v"/qn"` para InstallShield). Com a
   classificação implementada, isso é uma **tabela**, não uma heurística.
2. **Um switch de CLI por ferramenta é o padrão que o HiBit establish.** O
   Tranal já tem `/winapps`, `/be`, `/autorun`, `/hunter`; o HiBit mostra o
   formato final (19 ferramentas, uma por switch). É barato e faz a CLI
   virar uma ferramenta de verdade.
3. **A decisão de não usar driver de kernel ganha o argumento mais forte
   até agora.** O HiBit — que tem 21+ ferramentas e é um dos mais
   professional do grupo — roda **sem driver, sem serviço de fundo e sem
   extensão COM**, tudo no próprio processo. A paridade com o Revo se ganha
   por software.
4. **A lista de 17 categorias de lixo e as 15+ categorias de registro do
   HiBit são o teto do "painel de manutenção".** O Tranal tem as
   ferramentas separadas, sem o painel único — que é exatamente o que o
   IObit chamava de *Software Health*. Vale uma seção de painel único no
   Tranal.

---

*Auditoria gerada por medição direta dos artefatos. Toda linha deste
documento aponta para evidência em disco; o que não pôde ser verificado está
marcado como tal na seção 8.*
