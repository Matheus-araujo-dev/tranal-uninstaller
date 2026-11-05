# Geek Uninstaller 1.6.0.200 (Free) — auditoria

> O concorrente **mínimo**. Toda a superfície observável do Geek gratuito
> cabe em **60 strings** — contra 1.136 do Revo Pro e 1.066 do BCU. Serve
> como a referência do que um desinstalador enxuto ainda precisa ter, e do
> que ele dispensa.

Data da coleta: 05/11/2026 · Versão auditada: **1.6.0.200** (edição **Free**)

---

## 1. Identificação

| Campo | Valor | Fonte |
|---|---|---|
| Produto | Geek Uninstaller Free (pessoal, sem suporte) | `manual-download.txt` |
| Versão | **1.6.0.200** | `pe-geek-exe.txt` |
| Editor | Thomas Koen / Geek Uninstaller (Bélgica) | `manual-download.txt` (© 2012–2026) |
| Site | https://geekuninstaller.com/ | `manual-overview.html` |
| Download | `https://geekuninstaller.com/geek.zip` (botão "ZIP (4.64 MB)") | `manual-download.txt` |
| SHA-256 do zip | `B4EED5DFC430ECF74A7B2AB98EDE605D32D51FCF79BB2A0878021EA88671CDC9` | `sha256-geek-zip.txt` |
| Conteúdo do zip | **1 arquivo: `geek.exe`** (11.980.432 B) — sem instalador | `app/geek.exe` |
| Formato | **PE32 nativo, 32 bits, Windows GUI** — C++/MSVC (seções `.text .rdata .data .fptable .rsrc`), UI com o Codejock **XTP** | `pe-geek-exe.txt`, `uia-geek-arvore.txt` |
| Assinante | `CN=CrystalBit Solutions, O=CrystalBit Solutions, S=West-Vlaanderen, C=BE` — Sectigo Public Code Signing CA R36, **válido até 13/04/2029** | `pe-geek-exe.txt` |
| Compilação | 2026-10-14 11:20:34Z | `pe-geek-exe.txt` |
| Imphash | `c3059cf0fda04abb514a6832b76a7df6` | `pe-imports-resumido.txt` |
| Requisitos | Windows 11/10/7/8/8.1, 32 e 64 bits (inclusive ARM); **exige administrador**; em x64/ARM64 roda como nativo de 64 bits | `manual-download.txt` |

### O launcher de 32 bits (medido)

`geek.exe` é um **launcher**: na execução ele extrai `geek64.exe`
(4.154.000 B) em `%TEMP%` e o executa — as strings do binário contêm
`geek64.exe` e `geek-arm64.exe`, e o release notes 1.4.3.102 documenta
"geek64.exe is extracted to the same directory (on 64-bit Windows)". O
processo `geek64.exe` foi observado em execução com a janela
"Geek Uninstaller 1.6.0.200".

**Nada foi gravado** em `%ProgramFiles%` nem no Menu Iniciar: o Geek não
precisa de instalador.

### Onde o Geek guarda o estado (medido)

| Local | Conteúdo |
|---|---|
| `%APPDATA%\Geek Uninstaller\prefs.xml` | XML `<PortableProgramData version="2">`: `ColorScheme`, `InitialSortOrder`, `InitialSortParam`, `Interface_Language`, `Window_Maximized`, `Window_Rect`, `column_0..3` |
| `%APPDATA%\Geek Uninstaller\cache.dat` | cache binário (10.919 B) |
| `%APPDATA%\Geek Uninstaller\IconsCache\*.ico` | cache de ícones (~5,2 KB por programa) |
| `HKCU\Software\Geek Uninstaller\LastRunDate` | `REG_QWORD` |
| `%TEMP%\geek64.exe` | o binário de 64 bits extraído pelo launcher |

### O dado mais revelador da auditoria

A **superfície observável inteira** do Geek gratuito está em
`evidencia/langstrings-en.txt`: **60 strings**, em 9 blocos
(`MainMenu`, `ListCtrl`, `ExplorerBar`, `UninstallWizard`, `DlgAbout`,
`Messages`, `ProVersion`, `HTMLReport`, `Misc`).

| Produto | Strings observáveis |
|---|---|
| Revo Pro 5.5.4 | 1.136 (42 seções) |
| Bulk Crap Uninstaller 6.3.0 | 1.066 (45 telas) |
| IObit Uninstaller 16.1 | 95 telas de diálogo |
| **Geek Uninstaller 1.6.0 Free** | **60** |

---

## 2. Superfície observável

### 2.1 Menu (reconstruído e **capturado**)

| Menu | Itens |
|---|---|
| **File** | Export to HTML… · Exit |
| **Action** | Uninstall… · Modify… · **Force Removal** · **Batch Force Removal** · Remove Entry · **Batch Remove Entries** · Registry Entry · Installation Folder · Program Website · Open in Microsoft Store · Browse Location… · Browse Source… · Google for |
| **View** | Desktop Apps · Microsoft Store Apps · Refresh · **Reset Columns** · Color Scheme (Match Windows light/dark · Light · Dark) · **Interface Language** |
| **Help** | About… · Home Page |

**Atalhos de teclado (observados na captura do menu Ação):**

| Atalho | Comando |
|---|---|
| `Enter` | Desinstalar… |
| `Shift+Del` | Forçar Remoção |
| `Ctrl+R` | Entrada no Registro |
| `Ctrl+E` | Pasta de Instalação |
| `Ctrl+W` | Página web do programa |
| `Ctrl+S` | Exportar para HTML… |
| `F5` | Atualizar |
| `Ctrl+Tab` | alternar Desktop ↔ Microsoft Store |
| `ESC` | resetar o filtro |

### 2.2 Funcionalidades

| # | Funcionalidade | Evidência | Tipo |
|---|---|---|---|
| 1 | Lista com **3 colunas visíveis** (Nome, Tamanho, Instalado Em); o XML prevê uma 4ª (`column_0..3`) | `prefs-geek.xml` | Observado |
| 2 | Total agregado no rodapé ("%d programs of %s size in total") | `langstrings-en.txt` | Observado |
| 3 | Indicação de app 32-bit dentro do nome ("(32-bit)") | `mark32bitItem` | Observado |
| 4 | **Filtro incremental por digitação**, sem campo visível ("Type to find a program") | `itemShortSearchTip`, `uia-geek-arvore.txt` (classe `Edit`) | Observado |
| 5 | Aviso "No matching items, press ESC to reset" | `staticNoItemsMatchingFilterTip` | Documentado |
| 6 | **Desinstalação** pelo desinstalador do programa | `mnuActionUninstall` | Observado |
| 7 | **Modificar/reparar** a instalação | `mnuActionModify` | Documentado |
| 8 | **Force Removal** — remoção sem executar o desinstalador, com confirmação | `mnuActionDelete`, `msgForceRemovalQuestion` | Observado |
| 9 | **Batch Force Removal** | `mnuActionDeleteBatch` | Documentado |
| 10 | **Remove Entry** + **Batch Remove Entries** | `mnuActionRemoveEntry`, `msgRemoveEntryQuestion` | Observado |
| 11 | **Assistente de desinstalação** com sobras em **2 categorias**: File System (%d items) e Registry (%d items), em **árvore** | `treeItemFileSystem`, `treeItemRegistry`, `wiz-wizard-*.png` | **Observado** |
| 12 | Mensagens do assistente, **medidas em execução**: "Alguns itens órfãos encontrados!" → "Gerando lista de traços…" → "Todos os traços removidos" | `wiz-pos-finalizar-*.png`, `wiz-final-*.png` | **Observado** |
| 13 | **Cancelamento** da remoção de sobras com confirmação | `msgCancelWizard` | Documentado |
| 14 | Diálogo de falha de desinstalação com opção de **limpar a entrada do registro e os arquivos** | `msgUninstallFailed` | Documentado |
| 15 | "Browse..." dentro das sobras encontradas | `treeItemPopopMenuBrowse` | Documentado |
| 16 | Aba de **Microsoft Store Apps** (`Ctrl+Tab`) | `mnuViewWinStoreApps`, `geek-store-apps.png` | Observado |
| 17 | **Reset Columns** | `mnuViewListColumnsReset` | Documentado |
| 18 | **Tema** automático (light/dark do Windows) | `mnuViewColorScheme*` | Documentado |
| 19 | **Interface multilíngue** (pt-BR incluído) | `prefs-geek.xml` (`Portuguese_Brazilian`) | Observado |
| 20 | **Exportação em HTML** ("Generated by %s on %s", "Installed Programs (%s)") | `HTMLReport`, `dlg-export-*.png` | Observado |
| 21 | Ir para a **chave do registro**, a **pasta de instalação**, o **site do programa**, a **Microsoft Store** | `mnuActionJumpTo*` | Observado |
| 22 | **Browse Location…** e **Browse Source…** | `mnuActionBrowseLocation`, `mnuActionBrowseSource` | Documentado |
| 23 | **Google for** — pesquisar o programa no navegador | `mnuActionGoogleFor` | Documentado |
| 24 | Verificação de **nova versão** (HTTP) | `msgNewVersionAvailable*`, `WININET.dll` | Documentado |
| 25 | Categoria **Windows Updates** | `Misc.WindowsUpdatesText` | Documentado |
| 26 | Aviso quando a remoção da entrada falha por falta de permissão | `msgRemoveEntryFailed` | Documentado |

### 2.3 A correção: o Geek **não** aceita arrastar executável

A crença popular é que o Geek aceita arrastar um `.exe` sobre a janela.
**Medido: não aceita.**

- Nenhuma das 7 janelas de topo e dos 15 controles-filhos tem
  `WS_EX_ACCEPTFILES` (`drop-log3.txt`: todas com `ACCEPTFILES=False`).
- Um `WM_DROPFILES` postado na janela principal **não produziu nenhuma janela
  nem mensagem** (`drop-log.txt`).
- Passar o caminho de um `.exe` como **argumento** também não produziu
  efeito: `geek.exe "C:\Windows\System32\notepad.exe"`, executado 2 vezes,
  **não** abriu o notepad nem mostrou diálogo.

O binário importa `DragQueryFileW`/`DragFinish` (API do shell), mas nenhuma
janela aceita o drop. É a API disponível, não o recurso.

### 2.4 O que a versão **PRO** promete (banner, não executado)

Real-time installation monitor · Thorough and complete removal · Start-up
programs management · Modern interface, technical support · **Batch removal**.

> O Free já tem *Batch Force Removal* e *Batch Remove Entries*; o "Batch
> removal" do PRO é a desinstalação completa em lote — o que o Free não faz.

---

## 3. Mecanismos notáveis

### 3.1 DLLs importadas

18 DLLs no dump PE local (o levantamento REA lista 19, com `MSIMG32` em
duplicidade), 716 funções:

| DLL | Nº | Papel |
|---|---|---|
| KERNEL32 | 230 | `CreateProcessW`, `OpenProcess`, **`TerminateProcess`**, `WaitForSingleObject(Ex)`, `CreateFileW`, `DeleteFileW`, `RemoveDirectoryW`, **`MoveFileExW`**, `FindFirstFileExW`, `GetVolumeInformationW`, `GetPrivateProfileStringW`, `GetTempPathW` |
| USER32 | 222 | `MessageBoxW`, `SendMessageW`, `PostMessageW`, `SetWindowsHookExW` |
| GDI32 | 87 | desenho da lista/ícones |
| OLEAUT32 | 26 | automação OLE |
| gdiplus | 25 | `GdipCreateBitmapFromHICON`, `GdipDrawPath` |
| ole32 | 22 | `CoCreateInstance`, `CoInitializeEx` (COM para MSIX/Store apps) |
| ADVAPI32 | 18 | `RegOpenKeyExW`, `RegQueryValueExW`, `RegEnumKeyExW`, `RegDeleteKeyW`, `RegDeleteValueW`, `RegSetValueExW`; `AdjustTokenPrivileges`, `LookupPrivilegeValueW`, `OpenProcessToken` |
| COMCTL32 | 16 | common controls |
| SHLWAPI | 16 | helpers de caminho/URL |
| SHELL32 | 14 | `ShellExecuteExW`, **`SHFileOperationW`**, `ExtractIconExW`, `SHGetKnownFolderPath`, **`DragQueryFileW`**, `CommandLineToArgvW` |
| UxTheme | 14 | theming do Windows (dark mode) |
| WININET | 13 | `InternetOpenW`, `InternetOpenUrlW` → **verificação de nova versão** |
| VERSION | 3 | `GetFileVersionInfoW` |
| OLEACC | 2 | acessibilidade MSAA |
| oledlg | 2 | `OleUIBusyW` |
| MSIMG32 | 1 | `AlphaBlend` |
| ntdll | 1 | `RtlGetVersion` |
| WINMM | 1 | `PlaySoundW` |

### 3.2 Mecanismos de remoção

| Mecanismo | Evidência |
|---|---|
| **Kill de processo travado** — `TerminateProcess` importado + logs `> TerminateProcess %u`, `> TerminateProcess failed with error %u`, `> OpenProcess failed with error %u` | `pe-imports-resumido.txt`, `strings-geek-exe.txt` |
| **Remoção adiada para o próximo reinício** quando o arquivo está em uso — "ERROR_ACCESS_DENIED, the file will be deleted on the next system restart" e "ERROR_SHARING_VIOLATION, the folder will be deleted on the next system restart" | `strings-geek-exe.txt` |
| **Desregistro de COM** — `regsvr32.exe /s /u "%s"` | `strings-geek-exe.txt` |
| **Log de desinstalação** — `s_UninstallLog.log` | `strings-geek-exe.txt` |
| **Varredura de sobras** — classes `CUninstaller::FindTraced`, `CWizardScanner`, **`CWizardScannerMSI`**, **`CScannerMSI::ScanMSITraces`**, `CWizardTreeCtrl` | `strings-geek-exe.txt` |
| **Varredura de registro** — `...\CurrentVersion\Uninstall\`, **`...\Installer\UserData\%s\Products\`**, **`...\Installer\Folders`**, `KEY_CLASSES_ROOT`, `KEY_USERS\`, `Software\Classes\ActivatableClasses\Package` (MSIX) | `strings-geek-exe.txt` |
| Detecção de instalação em `Local AppData Programs` e por uninstall string / display icon | `strings-geek-exe.txt` |
| Extração de ícone — `ExtractIconExW` + `GdipCreateBitmapFromHICON` | `pe-imports-resumido.txt` |
| **Mutex de instância única** — `Local\GeekUninstallerMutex` | `strings-geek-exe.txt` |

> **Achado relevante:** o Geek varre as sobras de **MSI** (`ScanMSITraces`) e
> as chaves `Installer\UserData\…\Products` e `Installer\Folders`. É a mesma
> direção que o Tranal ainda não tomou — a varredura por nome não acha o que
> o instalador colocou fora da pasta do programa.

### 3.3 Integração com o sistema

**Nenhuma extensão de shell.** Sem menu de contexto, sem serviço de
background, sem driver.

---

## 4. Interface

Codejock XTP. A árvore UIA expõe 7 elementos: `XTPDockBar` (topo),
`XTPToolBar` (barra de menu), `SysListView32` + `SysHeader32` (lista e
cabeçalho), `Edit` (campo de filtro) e `XTPStatusBar` (rodapé). A lista e o
cabeçalho são controles nativos; menu, barra e rodapé são do XTP — daí a
árvore rasa apesar da interface rica. Os menus são popups `XTPPopupBar`.

| Janela | Classe | Tamanho |
|---|---|---|
| Principal "Geek Uninstaller 1.6.0.200" | `#32770` | 480×600 |
| Confirmação de remoção forçada | `#32770` | 376×162 |
| Wizard de sobras "Alguns itens órfãos encontrados!" | `#32770` | 496×288 |
| "Todos os traços removidos" | `#32770` | 496×288 |
| "Sobre o Geek Uninstaller" | `#32770` | 358×153 |
| "Salvar como" (export HTML) | `#32770` | 1200×720 |

Popups de menu: File 213×56 · View 232×135 · Action 203×156 · Help 128×56 ·
Appearance 241×77.

---

## 5. Linha de comando

| Parâmetro | Efeito | Fonte |
|---|---|---|
| `/store_apps` | Abre o app já na aba **Microsoft Store Apps** | `geek-store-apps.png`; `strings-geek-exe.txt`; release notes 1.4.10.155 |

Medido: com `/store_apps` a lista passa a mostrar "App Installer",
"Calculator", "Camera", "ChatGPT", "Claude", "Clock", "Dev Home",
extensões de imagem — **74 programas / 29,0 MB** — em vez dos
**80 programas / 658 GB** do desktop.

- **Não existe modo silencioso / não-interativo.** Nenhuma string de
  silent/unattended/quiet no binário (a única ocorrência é
  `regsvr32.exe /s /u "%s"`, que é desregistro de COM). O site também não
  documenta nenhuma seção de linha de comando.
- **Não aceita caminho de arquivo como argumento** (medido, 2 execuções).
- `CommandLineToArgvW` e `GetCommandLineA/W` são importadas, coerente com o
  único parâmetro acima.
- O launcher repassa o parâmetro ao `geek64.exe` que extrai (medido:
  `/store_apps` chegou ao processo de 64 bits).

---

## 6. O que o Tranal NÃO faz hoje

| Recurso do Geek | Tranal |
|---|---|
| **Diálogo de órfãos** ("Alguns itens órfãos encontrados!") | ⬜ — o Tranal não tem o conceito de órfão |
| **Frase de fechamento do ciclo** ("Todos os traços removidos") | ⬜ |
| **Confirmação ao cancelar** a remoção de sobras | ⟡ — o Tranal cancela o assistente, sem essa confirmação |
| **Batch Force Removal** / **Batch Remove Entries** | ⬜ |
| **Modificar/reparar** a instalação | ⬜ |
| **Reset Columns** | ⬜ |
| **Abrir na Microsoft Store** | ⬜ |
| **Browse Source…** (ir à origem do instalador) | ⬜ |
| Diálogo de falha com opção de **limpar entrada e arquivos** | 🟡 — o Tranal tem modo forçado, mas não esse diálogo |
| **Varredura de sobras de MSI** (`ScanMSITraces`, `Installer\UserData\…\Products`, `Installer\Folders`) | ⬜ |
| Categoria **Windows Updates** | ⬜ |
| **Atalhos de teclado** no menu Ação | 🟡 |
| Coluna de **data de instalação** e de **tamanho** com total agregado | ✅ |

### Onde o Tranal já é melhor

| Ponto | Tranal | Geek |
|---|---|---|
| **Backup do registro antes de desinstalar** | ✅ completo, com script de restauração | ❌ nenhum |
| **Ponto de restauração** antes de desinstalar | ✅ | ❌ nenhum |
| **Confiança das sobras com motivo** | 3 níveis + frase por item | 2 categorias (File System / Registry), sem confiança |
| **Lixeira** para os arquivos | ✅ | ❌ apaga direto (`SHFileOperationW` existe mas sem lixeira) |
| **CLI** | `/scan`, `/mu`, `/export`, `/columns` | 1 switch (`/store_apps`) |
| **Exclusão** contra a remoção | exclusões aplicadas **até no backup** | não há |
| **Ferramentas** (rastros, backup, registro, autoruns) | ✅ painel completo | ❌ só desinstalação |
| **Segurança do motor** | `SafeTraversal` nunca atravessa link | — |

> **O Geek gratuito é o exemplo mais claro de que a lista de recursos do
> Revo Pro é majoritariamente toolset**: o desinstalador em si cabe em 60
> strings. O que o Revo paga caro é o **painel de manutenção** — e é
> exatamente isso que o Tranal já tem.

---

## 7. Fichos de evidência

| Arquivo | Bytes | Conteúdo |
|---|---|---|
| `app/geek.exe` | 11.980.432 | o binário |
| `evidencia/langstrings-en.txt` | 5.829 | **as 60 strings do app** — o inventário completo |
| `evidencia/strings-geek-exe.txt` | 326.188 | strings ASCII + UTF-16 do binário |
| `evidencia/pe-geek-exe.txt` | 878 | cabeçalho PE, assinatura, DLLs importadas |
| `evidencia/pe-imports-resumido.txt` | — | 18 DLLs × 716 funções, com o papel de cada uma |
| `evidencia/manual-download.txt` | 21.373 | página de download + release notes |
| `evidencia/uia-geek-arvore.txt` | 429 | árvore UIA (7 elementos) |
| `evidencia/prefs-geek.xml` | 577 | preferências em PortableProgramData |
| `evidencia/drop-log3.txt` | 1.725 | **prova de que nenhuma janela aceita drop** |
| `evidencia/wiz-*.png` | — | 11 capturas do assistente de desinstalação |
| `evidencia/menu-*.png`, `popup-*.png` | — | capturas dos 4 menus e dos popups |
| `evidencia/elev-Geek_Uninstaller_1_6_0_200-20261105-190211.png` | 42.168 | janela principal em execução |

---

## 8. Lacunas desta auditoria

| O que faltou | Por quê |
|---|---|
| **Desinstalação real** de um programa | não executada — exigiria instalar e desinstalar de verdade |
| Comportamento do **PRO** | software pago; só o banner foi medido |
| Como o Geek calcula o **tamanho** de cada programa | não instrumentado |
| O **Windows Updates** na prática (a string existe, o uso não foi observado) | não navigado |
| Quais itens do `Installer\Folders` o Geek remove | a varredura existe; o critério, não |

---

## 9. Decisões que este documento obriga

1. **O diálogo de órfãos é uma ideia que vale roubar.** O Geek diz
   "Alguns itens órfãos encontrados!" — e oferece removê-los. O Tranal hoje
   nem tem o conceito de órfão. E há um segundo motivo: **sobra de
   confiança baixa que não pertence a nenhum programa instalado** é
   informação útil — hoje o Tranal apenas a lista.
2. **A varredura de sobras de MSI é o próximo passo do núcleo.** O Geek
   varre `Installer\UserData\…\Products` e `Installer\Folders` e tem uma
   classe `CScannerMSI::ScanMSITraces`. São **149 dos 211 programas** desta
   máquina (MSI). A varredura por nome não acha o que o instalador pôs
   fora da pasta do programa — e é exatamente esse o item F.1-2 do backlog.
3. **O resumo do ciclo ("Todos os traços removidos") é uma frase que
   falta.** Custa pouco e fecha a história na tela.
4. **Cancelar a remoção de sobras precisa de confirmação.**
5. **A lista de recursos pagos do Revo é majoritariamente toolset.** Isso
   consolida a aposta do Tranal: desinstalador + painel de manutenção.

---

*Auditoria gerada por medição direta dos artefatos e por execução do
programa. Toda linha deste documento aponta para evidência em disco; o que
não pôde ser verificado está marcado como tal na seção 8.*
