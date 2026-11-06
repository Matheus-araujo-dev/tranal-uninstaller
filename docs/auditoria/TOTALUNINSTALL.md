# Total Uninstall 7.6.2 Professional Edition — auditoria

> O concorrente **mais completo em superfície de funcionalidades** entre os auditados: um desinstalador que **fotografa o sistema antes da instalação**, **compara** o antes com o depois para mostrar **exatamente** o que o instalador gravou, e depois **desinstala a partir desse log** — em vez de adivinhar sobras pelo nome do programa. É também o único auditado que traz um **motor de regras de resíduo em XML embarcado** (`CleanerDefinitions.xml`, 623 elementos, 6 categorias com Include/Exclude) e um **agente de background** que avisa quando uma instalação está em andamento. Binário **nativo Win64 em Delphi/VCL**, assinado por **Certum**, executado **elevado** — e com a mesma pegada na edição Essential, porque o portão é de licença, não de arquitetura.

Data da coleta: 05/11/2026 · Versão auditada: **7.6.2** (edição **Professional**)

---

## 1. Identificação

| Campo | Valor | Fonte |
|---|---|---|
| Produto | Total Uninstall — **Professional Edition** 7.6.2 | `evidencia/pe-Tu.exe.txt`; `evidencia/tu-01-janela-principal.png` |
| Versão | **7.6.2** (arquivo 7.6.2.367), lançada em **2025-12-24** | `downloads/martau-download.html` (bloco "Release history") |
| Editor | **Gavrila Martau** (Martau's Software), Cosbuc, Bistrița-Năsăud, RO | `evidencia/pe-Tu.exe.txt`; `downloads/martau-home.html` |
| Título do produto (PE) | "Total Uninstall - Installation monitor, uninstaller and cleaner" | `evidencia/strings-Tu.exe.txt` |
| Site oficial | **`https://www.martau.com/`** | `downloads/martau-home.html` |
| Domínio do briefing | `https://www.totaluninstall.com/` → `/lander` — **estacionado/parking** (GoDaddy) | `evidencia/http-status-direto.txt` |
| URL de download usada | `https://www.total-uninstall.com/archives/Total-Uninstall-Setup-7.6.2.exe` | `downloads/martau-download.html` (link "Download Professional") |
| Segundo instalador | `.../Total-Uninstall-Essential-Setup-7.6.2.exe` (15.822.576 B) | `evidencia/downloads-hashes.txt` |
| SHA-256 do instalador | `4C5BA95AE62C175B648B62A63799ED96796B22E1E571B78E35E1E6561CC54FC1` | `evidencia/downloads-hashes.txt` (medido em disco) |
| Tamanho do instalador | **15.154.488 B** | `downloads/` (medido) |
| Formato do instalador | **Inno Setup 6.0.0 (unicode)** (`innoextract -l`: "setup data version 6.0.0") | `evidencia/innoextract-lista-essential.txt` |
| Formato do binário | **NATIVO Win64, Delphi/VCL** — janela `TfrmTUMain`, forms `TApplication`/`TPUtilWindow`; seções `.tls .rsrc .edata .idata .tls .winlice .boot .reloc` | `evidencia/uia-tu-janelas.txt`; `evidencia/pe-Tu.exe.txt` |
| Assinatura | Authenticode **válida** — `CN=Gavrila MARTAU, O=Gavrila MARTAU, L=Cosbuc, S=Bistrița-Năsăud, C=RO`; emissor `CN=Certum Code Signing 2021 CA`; válida até **12/01/2026** | `evidencia/pe-Tu.exe.txt` |
| Data de compilação | **2025-12-24 07:21:16Z** (timestamp PE, PE64 `0x8664`) | `evidencia/pe-Tu.exe.txt` |
| Requisitos | Windows **x64**; binário **nativo** (sem .NET); **exige elevação** (processo medido com `TOKEN_ELEVATED=True`); **sem driver de kernel**; 20 DLLs importadas; agente de background documentado (binário não localizado no payload) | `evidencia/pe-Tu.exe.txt`; `evidencia/pe-imports-Tu.exe.txt`; `evidencia/arquivos-instalador.txt`; `evidencia/readme-instalador.txt` |
| Preço / trial | Shareware, 30 dias; Professional US$ 24,95–29,95 | `downloads/martau-buy.html` |
| Edições | Essential / Professional / **Ultimate** | `downloads/martau-buy.html` |
| Payload | **32 arquivos**: `Tu.exe` (11.324.608 B), `TuRes.dll` (12.972.720 B), `CleanerDefinitions.xml` (28.003 B), 6 CHM de ajuda, **21** traduções `.lng` (em `Translations/`), `License.rtf`, `ReadMe.txt` | `app/professional/app/` (medido) |
| **Medido: Professional ≡ Essential em módulo** | os dois instaladores extraem **os mesmos 32 arquivos** (mesmos CHM, mesmas traduções, mesmo `CleanerDefinitions.xml`); só `Tu.exe` e `ReadMe.txt` diferem | `evidencia/arquivos-instalador.txt` |

### O domínio errado

O briefing apontava `totaluninstall.com`. Medido: esse domínio (sem hífen) está **estacionado/parking** e redireciona para `/lander` (GoDaddy). O site oficial vivo é **`martau.com`** e os instaladores ficam em **`total-uninstall.com/archives/`** (com hífen). A mesma armadilha de SEO já vista em `hibituninstaller.com` e em `wiseuninstaller.com` — por isso a URL de download foi confirmada pelo link "Download Professional" da página oficial, e não pelo domínio do briefing.

### Estado de execução observado

`Tu.exe` rodou **sem instalação**, direto de `app/professional/app/`, **sem prompt de UAC visível**. A janela principal abriu com o título `Total Uninstall Professional Edition [Unregistered]` e o rodapé mostrou `80 installed programs, 136 system components` + `Pending file rename operations.` O processo estava com **`TOKEN_ELEVATED=True`** (leitura do token) e **não deixou** chave/pasta nomeada "Total Uninstall" / "Martau" / "Gavrila" em `HKCU:\Software`, `HKLM:\SOFTWARE\WOW6432Node`, `%APPDATA%`, `%LOCALAPPDATA%` ou Documentos. Evidência: `evidencia/tu-01-janela-principal.png`, `evidencia/uia-tu-janelas.txt`, `evidencia/rastro-no-sistema.txt`.

### Correções ao inventário bruto

Duas divergências entre o `evidencia/RESUMO.md` e a medição direta em disco, registradas aqui para que o número canônico não se propague:

- **Traduções**: o RESUMO registra "26 `.lng`"; em disco são **21** arquivos em `Translations/` (incluindo `PortugueseBrazilian.lng`). O total de **32 arquivos** do payload confere.
- **`CleanerDefinitions.xml`**: o RESUMO registra "623 elementos"; a contagem direta de tags sobre o arquivo real (28.003 B) devolve **623** — confere. Já `evidencia/cleaner-definitions.txt` (173 B) registra "elementos com atributo Name: 0", ou seja, o método de contagem daquele arquivo falhou; o número 623 vem da contagem de tags do XML.

---

## 2. Superfície observável

Inventário de **funcionalidades** (não de telas). "Observado" = aconteceu nesta máquina; "Documentado" = o manual/site oficial afirma. As referências `sX` são do arquivo `evidencia/strings-funcionalidades.txt`, que carrega o número da linha correspondente em `evidencia/strings-Tu.exe.txt`.

### 2.1 Núcleo do desinstalador

| # | Funcionalidade | Evidência | Observado ou documentado |
|---|---|---|---|
| 1 | Cinco módulos: Installed programs, Monitored programs, Windows Apps, Cleaner, Auto-run Manager | `evidencia/tu-01-janela-principal.png`; `evidencia/readme-instalador.txt` | Observado |
| 2 | "Installed programs" com colunas Program name / Installed on / Size | `evidencia/tu-01-janela-principal.png` | Observado |
| 3 | Contagem de "136 system components" (componentes de sistema distinguidos) | `evidencia/tu-01-janela-principal.png` | Observado |
| 5 | "Windows Apps" (aplicativos Store/MSIX) como módulo próprio | `evidencia/uia-tu-principal.txt`; `evidencia/strings-funcionalidades.txt` (s17205, s16208) | Observado |
| 8 | Ações Analyze / Uninstall / Save na barra de ações | `evidencia/tu-01-janela-principal.png`; `evidencia/uia-tu-principal.txt` | Observado |
| 9 | Layout alternável Programs / Details / Search | `evidencia/tu-01-janela-principal.png`; `evidencia/uia-tu-principal.txt` | Observado |
| 10 | Agrupamento em grupos com árvore e ações: Add root group / Add child group / Edit group / Remove group / Move to root | `evidencia/tu-01-janela-principal.png`; `evidencia/uia-tu-principal.txt` | Observado |
| 12 | "Pending file rename operations" — ver e aplicar renomeações pendentes sem reiniciar | `evidencia/tu-01-janela-principal.png`; `evidencia/strings-funcionalidades.txt` (s15243, s17196) | Observado |
| 24 | **Desinstalar** programa monitorado ou analisado pela árvore de mudanças | `evidencia/strings-funcionalidades.txt` (s50924, s50939) | Documentado |
| 25 | Remoção de **sobras**: "remove the remaining folders, files, registry keys and values" depois do desinstalador nativo | `evidencia/strings-funcionalidades.txt` (s50939); `evidencia/manual-force-uninstall.txt` | Documentado |
| 26 | **Force uninstall** de programas parciais/corrompidos, sem depender do desinstalador | `evidencia/readme-instalador.txt` ("Force uninstall partial or corrupted programs"); `evidencia/manual-force-uninstall.txt` | Documentado |
| 27 | **Desinstalação em lote** ("Batch uninstall programs") | `evidencia/readme-instalador.txt`; `evidencia/manual-batch-uninstall-multiple-programs.txt` | Documentado |
| 51 | Aviso de que o programa monitorado **ainda está em execução** ("It appears that the monitored program is still running.") | `evidencia/strings-funcionalidades.txt` (s51044) | Documentado |
| 52 | Log de desinstalação detalhado; "Program was uninstalled successfully!" / "Program was partially uninstalled" | `evidencia/strings-funcionalidades.txt` (s50920); `evidencia/readme-instalador.txt` | Documentado |
| 55 | Localizar programa por palavra-chave ou **arrastar e soltar** ("Find the program to uninstall by keyword or shortcut drag and drop") | `evidencia/readme-instalador.txt` | Documentado |
| 56 | Resumo e informações detalhadas de cada programa instalado ou monitorado | `evidencia/readme-instalador.txt` | Documentado |
| 61 | Filtros de tipo de arquivo na operação de exclusão (.ico, .dll, .htm/.html, .lnk/.exe) | `evidencia/strings-funcionalidades.txt` (s51043) | Documentado |
| 64 | Item compartilhado preservado: "Not deleted. Contains only files or folders to keep." / "Contains only registry keys to keep." | `evidencia/strings-funcionalidades.txt` (s50968) | Documentado |

### 2.2 Monitor, snapshot e árvore de mudanças

| # | Funcionalidade | Evidência | Observado ou documentado |
|---|---|---|---|
| 4 | "Monitored programs": lista de programas com log de instalação | `evidencia/tu-01-janela-principal.png`; `evidencia/uia-tu-principal.txt` | Observado |
| 11 | Busca textual nas alterações detectadas ("Search for a text in the detected changes") | `evidencia/strings-funcionalidades.txt` (s17220); `evidencia/manual-indice-chm.txt` (`pi_mw_searchpanel.htm`) | Documentado |
| 13 | **Snapshot do sistema antes da instalação** ("Create the pre-install system snapshot"; opções "Create a new system snapshot" / "Use last snapshot") | `evidencia/strings-funcionalidades.txt` (s16284, s16298, s16300, s16303); `evidencia/manual-installation-monitor.txt` | Documentado |
| 14 | Nome do snapshot pré e pós-instalação customizável ("Specify the post-install snapshot name", "Allow custom snapshot name") | `evidencia/strings-funcionalidades.txt` (s16313, s15822) | Documentado |
| 15 | Pasta de snapshots com limite de tamanho e contagem ("Limits for Snapshots folder", "Snapshot count") | `evidencia/strings-funcionalidades.txt` (s15827, s15829) | Documentado |
| 16 | **Monitorar instalação** e detectar automaticamente o nome do programa instalado | `evidencia/manual-installation-monitor.txt` (linhas 10-12); `evidencia/strings-funcionalidades.txt` (s16263) | Documentado |
| 17 | **Comparar snapshots do sistema** ("Compare system snapshots...", "Snapshot compare profiles") | `evidencia/strings-funcionalidades.txt` (s15133, s17024, s15912); `evidencia/manual-indice-chm.txt` (`pi_od_comparingprofile.htm`) | Documentado |
| 18 | **Perfis** separados: Scanning, Analysis, Uninstall, Comparing, Restore/Redo, Cleaner, Auto-run view | `evidencia/strings-funcionalidades.txt` (s15825, s15682, s15760, s16120, s16073, s16170) | Documentado |
| 19 | Perfil de scanning com **chaves/pastas incluídas e excluídas** e snapshot "frozen" | `evidencia/strings-funcionalidades.txt` (s15872, s15882, s15890, s15899, s15862) | Documentado |
| 20 | Varredura de **Alternate Data Streams** (ADS) | `evidencia/strings-funcionalidades.txt` (s15909) | Documentado |
| 21 | **Visões de mudança** configuráveis ("Change views", "View/Apply a defined change view") | `evidencia/strings-funcionalidades.txt` (s15999, s16796); `evidencia/manual-indice-chm.txt` (`pi_od_changeview.htm`) | Documentado |
| 22 | Árvore de alterações detectadas com **arquivos, pastas, chaves e valores** de registro | `evidencia/strings-funcionalidades.txt` (s16482, s16614, s15735); `evidencia/manual-indice-chm.txt` (`pi_od_changeview.htm`) | Documentado |
| 23 | Opção de **não usar o log de mudanças detectadas** ("Don't use detected changes log") | `evidencia/strings-funcionalidades.txt` (s15788) | Documentado |
| 63 | "Don't use detected changes log" + exclusão de chaves/valores de registro **por perfil** | `evidencia/strings-funcionalidades.txt` (s15788, s16080) | Documentado |

### 2.3 Cleaner

| # | Funcionalidade | Evidência | Observado ou documentado |
|---|---|---|---|
| 6 | "Cleaner": limpeza de itens descartáveis seguros | `evidencia/uia-tu-principal.txt`; `evidencia/strings-funcionalidades.txt` (s16073, s16078) | Observado |
| 43 | **Cleaner com 6 categorias de regras**: Windows, System, Browser, Windows Explorer, Programs (arquivo) e Invalid entries (registro), cada uma com Include/Exclude | `evidencia/cleaner-categorias.txt`; `app/professional/app/CleanerDefinitions.xml` | Observado |
| 44 | **Scanner de registro** e **scanner de sistema de arquivos** no Cleaner | `evidencia/strings-funcionalidades.txt` (s15849, s16108, s16117) | Documentado |
| 45 | "File extensions with version information" (classificação por extensão) | `evidencia/strings-funcionalidades.txt` (s15841) | Documentado |
| 46 | Remoção de **serviços e dispositivos** instalados por programas; alerta para antivírus/anti-spyware/firewall/driver | `evidencia/strings-funcionalidades.txt` (s51060, s50907, s50978, s50979, s50877) | Documentado |
| 47 | Detecção de **serviços** via API (`CloseServiceHandle` importado) | `evidencia/strings-funcionalidades.txt` (s17561); `evidencia/pe-imports-Tu.exe.txt` | Observado |
| 48 | Detecção de **produtos MSI** (`MsiGetProductInfoW` importado) | `evidencia/pe-imports-Tu.exe.txt`; `evidencia/strings-funcionalidades.txt` (s17563) | Observado |
| 49 | **Proteção de arquivos do Windows** consultada antes de apagar (`SfcIsFileProtected` importado) | `evidencia/pe-imports-Tu.exe.txt` | Observado |

### 2.4 Auto-run Manager

| # | Funcionalidade | Evidência | Observado ou documentado |
|---|---|---|---|
| 7 | "Auto-run Manager": gerenciador de inicialização | `evidencia/uia-tu-principal.txt`; `evidencia/strings-funcionalidades.txt` (s16735) | Observado |
| 39 | Auto-run: item de inicialização, "Auto-run at logon for", "Startup folder, run at every logon" | `evidencia/strings-funcionalidades.txt` (s15038, s15047, s15061) | Documentado |
| 40 | Auto-run: localizar entrada e alvo ("Locate auto-run entry"), editar/si e propriedades do alvo | `evidencia/strings-funcionalidades.txt` (s16932, s16935) | Documentado |
| 41 | Auto-run: adicionar/remover item, filtro da lista, árvore com "Auto-run views" | `evidencia/strings-funcionalidades.txt` (s16883, s16884, s16440, s16170, s16406) | Documentado |
| 42 | Auto-run gerencia **serviços** e **tarefas agendadas** do Windows | `evidencia/readme-instalador.txt` ("Control which program, service or scheduled task automatically start up"); `evidencia/manual-windows-startup-manager.txt` | Documentado |

### 2.5 Linha de comando, automação e exportação

| # | Funcionalidade | Evidência | Observado ou documentado |
|---|---|---|---|
| 50 | **Agente autônomo** de baixo consumo para notificar instalações em andamento | `evidencia/readme-instalador.txt` ("Stand-alone and low resource usage agent for notification of running installation programs") | Documentado |
| 53 | Exportar lista de programas monitorados; exportar alterações de registro; exportar/importar alterações (XML na Ultimate) | `evidencia/strings-funcionalidades.txt` (s16871); `downloads/martau-buy.html`; `evidencia/readme-instalador.txt` | Documentado |
| 54 | Exportar para arquivo ou **imprimir** as alterações detectadas | `evidencia/readme-instalador.txt` | Documentado |
| 57 | Verificação de atualizações e atualizações web | `evidencia/manual-indice-chm.txt` (`pi_checkforupdates.htm`, `pi_od_webupdates.htm`) | Documentado |
| 59 | Versão **portátil** (sem instalação) | `evidencia/manual-total-uninstall-portable-version.txt` | Documentado |
| 60 | Validação **online de licença** (exige internet; firewall pode bloquear) | `evidencia/strings-funcionalidades.txt` (s51076) | Documentado |
| 62 | Importar logs de versões anteriores (File→Import) após mudança do local dos dados na 7.0.0 | `downloads/martau-download.html` (release history 7.0.0) | Documentado |

### 2.6 Documentação oficial: backup, restauração, transferência e idiomas

Todas as linhas abaixo vêm do manual CHM (`Tu_en.chm`, 38 tópicos) e do site oficial — nenhuma foi observada em execução.

| # | Funcionalidade | Evidência | Observado ou documentado |
|---|---|---|---|
| 28 | **Backup do programa desinstalado** ("Backup uninstalled program", "Create a backup for the program") | `evidencia/strings-funcionalidades.txt` (s15790, s15471); `evidencia/readme-instalador.txt` | Documentado |
| 29 | **Restaurar** programa a partir do backup ("Select a program to restore", "Restore profile") | `evidencia/strings-funcionalidades.txt` (s15530, s15536, s15538); `evidencia/manual-transfer-programs-to-new-pc.txt` | Documentado |
| 30 | Restaurar **só o log de instalação**, sem o programa ("Restore only the install log") | `evidencia/strings-funcionalidades.txt` (s15542) | Documentado |
| 31 | Restauração granular: chaves/valores criados, modificados, deletados; pastas/arquivos criados, deletados, modificados; sobrescrever arquivos bloqueados | `evidencia/strings-funcionalidades.txt` (s16142, s16145, s16147, s16149, s16151, s16154, s16156, s16158, s16160, s16162, s16164, s16166) | Documentado |
| 32 | **Undo/Redo** de uma restauração ("Select a restoration to undo"; "Cannot undo/redo one or more keys or values") | `evidencia/strings-funcionalidades.txt` (s15484, s50940) | Documentado |
| 33 | **Ponto de restauração do Sistema** antes da desinstalação e antes da restauração | `evidencia/strings-funcionalidades.txt` (s15781, s16139) | Documentado |
| 34 | Limite de tamanho da pasta de backup ("Backup folder limit (MB)") | `evidencia/strings-funcionalidades.txt` (s15641) | Documentado |
| 35 | **Transferir programas monitorados para outro PC** via arquivo `.zip` de backup | `evidencia/manual-transfer-programs-to-new-pc.txt`; `evidencia/manual-linha-de-comando.txt` (`/import`) | Documentado |
| 36 | Remover rastros de programa monitorado já desinstalado ("Remove traces of an uninstalled monitored program") | `evidencia/strings-funcionalidades.txt` (s50966) | Documentado |
| 37 | Itens compartilhados: contadores de referência em registro ("Decrement shared file counter from registry") e diálogo "Remove shared file" | `evidencia/strings-funcionalidades.txt` (s51002); `evidencia/manual-indice-chm.txt` (`pi_u_removesharedfiledialog.htm`) | Documentado |
| 38 | Diálogo de exclusão de itens com revisão prévia ("Delete items dialog") | `evidencia/manual-indice-chm.txt` (`pi_u_deleteitemsdialog.htm`) | Documentado |
| 58 | Idiomas de interface (`.lng` em `Translations/`, **21** arquivos medidos, incluindo `PortugueseBrazilian.lng`) + **6** idiomas de ajuda (CHM: en/de/fr/es/pl/cn) | `evidencia/arquivos-instalador.txt`; `app/professional/app/Translations/` (medido) | Observado |

---

## 3. Mecanismos notáveis

| Mecanismo | Evidência | Observado ou documentado |
|---|---|---|
| **Binário nativo Win64 (Delphi/VCL)**, sem .NET, sem driver de kernel no payload (nenhum `.sys` nos 32 arquivos) | `evidencia/pe-Tu.exe.txt`; `evidencia/arquivos-instalador.txt` | Observado |
| **Execução elevada**: token do processo com `TOKEN_ELEVATED=True` (sem prompt de UAC visível na sessão) | medido via `OpenProcessToken`+`GetTokenInformation`; registrado em `evidencia/pe-Tu.exe.txt` | Observado (parcial: manifesto não inspecionado) |
| 20 DLLs importadas na tabela padrão, entre elas `advapi32.dll` (serviços/registro), `msi.dll` (MSI), `sfc.dll` (arquivos protegidos), `shell32.dll`, `wininet.dll` (licença online), `netapi32.dll`/`WTSAPI32.DLL` | `evidencia/pe-imports-Tu.exe.txt` | Observado |
| `MsiGetProductInfoW` importado — **consulta** de produtos MSI instalados (o produto lê o estado do MSI, não o log do instalador) | `evidencia/pe-imports-Tu.exe.txt` | Observado |
| `SfcIsFileProtected` importado — consulta de proteção de arquivos do Windows **antes de apagar** | `evidencia/pe-imports-Tu.exe.txt` | Observado |
| `CloseServiceHandle` importado + fluxo "Delete Service or Device: %s" | `evidencia/pe-imports-Tu.exe.txt`; `evidencia/strings-funcionalidades.txt` (s50979) | Observado |
| `PlaySoundW`/`winmm` + "ScanPlaySound" (perfil de scanning pode tocar som ao terminar) | `evidencia/pe-imports-Tu.exe.txt`; `evidencia/strings-funcionalidades.txt` (s15863, s15868) | Observado |
| 276 descritores **delay-load** (dlls carregadas sob demanda) — não decodificáveis pelo REA (usam VA, não RVA) | `evidencia/pe-imports-Tu.exe.txt` | Observado (parcial) |
| Regra de resíduo como **motor de regras em XML embarcado**: `CleanerDefinitions.xml` com **623 elementos** e **6 categorias** com Include/Exclude (medido: 623 tags, 110 ocorrências de `Include`, 20 de `Exclude`) | `evidencia/cleaner-categorias.txt`; `app/professional/app/CleanerDefinitions.xml` (contagem direta) | Observado |
| **Agente em background** separado para notificar instalações em andamento | `evidencia/readme-instalador.txt` | Documentado (binário não localizado no payload Professional) |
| Formato próprio de snapshot `.zsns` e de backup `.zip` | `evidencia/manual-linha-de-comando.txt` | Documentado |
| Sem rastro de registro/pastas nomeado após execução sem instalação | `evidencia/rastro-no-sistema.txt` | Observado |
| **Sem driver de kernel, sem serviço de background e sem extensão COM** no payload (32 arquivos, nenhum `.sys`) | `evidencia/arquivos-instalador.txt` | Observado |
| Acessibilidade: importa `oleacc.dll` (`LresultFromObject`) | `evidencia/pe-imports-Tu.exe.txt` | Observado |

**As 6 categorias do motor Cleaner**, como medidas em `app/professional/app/CleanerDefinitions.xml`:

| ID | Tipo | Nome | Definições | Include | Exclude |
|---|---|---|---|---|---|
| `CF-WINDOWS` | file | Windows | 7 | 22 | 1 |
| `CF-SYSTEM` | file | System | 4 | 9 | 3 |
| `CF-BROWSER-CACHE` | file | Browser | 5 | 7 | 2 |
| `CF-WINDOWS-EXPLORER` | file | Windows Explorer | 1 | 2 | 0 |
| `CF-PROGRAMS` | file | Programs | 3 | 5 | 0 |
| `CR-INVALID-ENTRIES` | registry | Invalid entries | 7 | 10 | 4 |

Fonte: `evidencia/cleaner-categorias.txt`. Cada `Definition` traz `<Scan><Include><Paths>` com caminhos absolutos ou com variável de ambiente (`%WINDIR%`, `%WINDIR%\Inf`, `%WINDIR%\Logs\DISM`, `%WINDIR%\Logs\DPX`, `%WINDIR%\wbem\Logs`, …) e um `<MaxLevel>` — é um **motor declarativo versionável**, não heurística compilada.

---

## 4. Interface

| Elemento | Evidência | Observado ou documentado |
|---|---|---|
| Janela principal `TfrmTUMain`, 1397x682, título "Total Uninstall Professional Edition [Unregistered]" | `evidencia/uia-tu-janelas.txt`; `evidencia/tu-01-janela-principal.png` | Observado |
| Menu: **File, Edit, View, Tools, Modules, Help** | `evidencia/tu-01-janela-principal.png` | Observado |
| Ribbon "Modules": Installed programs, Windows Apps, Monitored programs, Cleaner, Auto-run Manager (5 botões, ids UIA 36309698/25561366/20777270/3082792/267050) | `evidencia/tu-01-janela-principal.png`; `evidencia/uia-tu-principal.txt` | Observado |
| Ribbon "Actions": Analyze, Uninstall, Save (desabilitados sem seleção) | `evidencia/tu-01-janela-principal.png`; `evidencia/uia-tu-principal.txt` | Observado |
| Ribbon "Layout": Programs, Details, Search | `evidencia/tu-01-janela-principal.png`; `evidencia/uia-tu-principal.txt` | Observado |
| Painel esquerdo: árvore de grupos + lista (com checkbox "Bundle" e filtro de texto) | `evidencia/tu-01-janela-principal.png` | Observado |
| Painel direito: ações de grupo (Add root group..., Add child group..., Edit group..., Remove group, Move to root) + "Details" | `evidencia/tu-01-janela-principal.png`; `evidencia/uia-tu-principal.txt` | Observado |
| Rodapé de estado com contagem de programas/componentes e pendências | `evidencia/tu-01-janela-principal.png` | Observado |
| Estrutura do menu principal (itens medidos nas strings) | `evidencia/strings-funcionalidades.txt` (s16870, s16883, s16928, s17023, s17038, s17121, s17196) | Observado |
| Tópicos do manual oficial (CHM `Tu_en.chm`, 38 tópicos: janela principal, menu, listas, painel de detalhes, perfis, assistente de desinstalação, diálogo de arquivos compartilhados) | `evidencia/manual-indice-chm.txt` | Observado |
| Help por F1, com CHM por idioma | `downloads/martau-download.html` (release history 7.1.0) | Documentado |
| Tema claro/escuro conforme o Windows, ícones vetoriais, multi-monitor e multi-escala (7.2.0/7.4.0/7.5.0) | `downloads/martau-download.html` | Documentado |
| Acessibilidade: importa `oleacc.dll` (LresultFromObject) | `evidencia/pe-imports-Tu.exe.txt` | Observado |

### Limitação de medição desta auditoria

`Tu.exe` roda **elevado** (`TOKEN_ELEVATED=True`) e a máquina tem outras janelas elevadas de outros auditores por cima (Revo maximizado, IObit). **UIPI bloqueou** `SetWindowPos`, `MoveWindow`, `ShowWindow`, `SetForegroundWindow` e `InvokePattern`; o `PrintWindow` falhou, obrigando `CopyFromScreen` — que registra o que está **por cima**. Resultado: existe **1 captura limpa** da janela principal (`evidencia/tu-01-janela-principal.png`) e a **árvore UIA completa** (a leitura UIA funciona em processos elevados, `evidencia/uia-tu-principal.txt`), mas **não há captura do conteúdo dos módulos** Monitored programs / Cleaner / Auto-run Manager / Windows Apps. Evidência do bloqueio: `evidencia/uia-tu-modulo-monitored.txt`, `evidencia/uia-tu-janelas.txt`. Não houve tentativa de contornar a elevação.

---

## 5. Linha de comando

Fonte: `evidencia/manual-linha-de-comando.txt` (página oficial `https://www.martau.com/document/total-uninstall-command-line.php`).

| Item | Valor | Fonte | Estado |
|---|---|---|---|
| Executável | **`TuCmd.exe`** — "part of the Ultimate edition package available only after purchase" | `evidencia/manual-linha-de-comando.txt` | **Documentada** — o `TuCmd.exe` **não está** no payload Professional/Essential medido (32 arquivos sem ele). Busca por `TuCmd*` em toda a árvore da auditoria: **0 ocorrências** |
| `/snapshot` | `[<snapshot_name>\|<file_full_path.zsns> [<scan_profile>]]` — cria snapshot do sistema com perfil de scanning padrão ou especificado | `evidencia/manual-linha-de-comando.txt` | **Documentada / não verificada** |
| `/import` | `<source_file_full_path.zip> [<dest_file_full_path.zip>]` — importa backup de programa/bundle de outro sistema | `evidencia/manual-linha-de-comando.txt` | **Documentada / não verificada** |
| `/restore` | `<file_name.zip>\|<file_full_path.zip> [<restore>]` — restaura backup compatível | `evidencia/manual-linha-de-comando.txt` | **Documentada / não verificada** |
| Formato do snapshot | `.zsns` (formato próprio do produto) | `evidencia/manual-linha-de-comando.txt` | Documentada |
| Formato do backup transferível | `.zip` | `evidencia/manual-linha-de-comando.txt`; `evidencia/manual-transfer-programs-to-new-pc.txt` | Documentada |
| Códigos de retorno | 0 = sucesso; 1 = comando desconhecido; 2 = parâmetros inválidos; 3 = usuário cancelou (ESC) | `evidencia/manual-linha-de-comando.txt` | Documentada |
| Observação | "Current version supports the bellow commands. Others can be implemented at request." | `evidencia/manual-linha-de-comando.txt` | Documentada |

**Registrado como não verificado**: a CLI existe apenas na edição **Ultimate** e nenhum de seus três comandos foi executado. A comparação com a CLI do Tranal está na §6.

---

## 6. O que o Tranal NÃO faz hoje

Cada linha foi conferida por busca real no código de `E:\Projetos\tranal-uninstaller\src\`. Legenda: **✅** tem (confirmado no código) · **🟡** parcial (confirmado no código) · **⬜** não tem (ausência confirmada) · **⟡** não verificado (busca dirigida feita, sem correspondente e sem base para afirmar ausência).

| Recurso do Total Uninstall | Tranal | Onde no Tranal (arquivo) |
|---|---|---|
| Cinco módulos (Installed / Monitored / Windows Apps / Cleaner / Auto-run) | ✅ | `Core/ProgramScanner.cs` (lista), `Core/Modules/TracedPrograms.cs` (monitorados), `Core/Modules/WindowsApps.cs`, `Core/Tools/JunkCleaner.cs`, `Core/Tools/AutorunManager.cs` |
| Colunas da lista e contagem de componentes de sistema | ✅ | `Core/ProgramColumns.cs`; `Core/ProgramScanner.cs` |
| **Snapshot do sistema antes da instalação**, com nome pré/pós customizável e limite de pasta/contagem | 🟡 | `Core/Modules/SystemSnapshot.cs` — `Capture` (136), `DefaultFileRoots` (106), `DefaultRegistryRoots` (125); **sem** nome pré/pós, **sem** limite de tamanho e **sem** contagem de snapshots |
| **Comparação de dois snapshots (A × B)** com perfis de comparação | 🟡 | `Core/Modules/SystemSnapshot.cs:180` — `SystemSnapshot.Diff(before, after)` **existe** e é real; mas `SnapshotDiff` (557) só expõe `NewFiles` (570), `NewFolders` (573) e `NewRegistryKeys` (576): **não cobre** modificados/removidos, e não há fluxo de comparar dois snapshots com perfis |
| **Perfis por operação** (scanning / analysis / uninstall / comparing / restore-redo) com Include/Exclude | ⟡ | **não verificado** — as 109 ocorrências de "Profile" em `Core/` são perfil de navegador (`BrowserExtensions.cs`), de usuário (`UserProfiles.cs`) e de programa; nenhum perfil por operação |
| **Motor de regras de resíduo em XML** (623 elementos, 6 categorias com Include/Exclude) | ⬜ | `Core/Tools/JunkCleaner.cs` e `Core/Tools/TrackCleaner.cs` são heurística compilada; busca por `.xml` / `CleanerDefinitions` / `RuleEngine` em `Core/`: **0 ocorrências** |
| **Varredura de Alternate Data Streams** (ADS) | ⟡ | **não verificado** — busca por "Alternate Data Stream" / "GetAlternateStream" / ":DATA" em `src/`: **0 ocorrências** |
| Detecção de **produtos MSI** instalados | ✅ | `Core/MsiInstallLog.cs` — lê o pacote `.msi` em cache; o Total Uninstall só importa `MsiGetProductInfoW` (consulta) |
| **Proteção de arquivos do Windows** consultada antes de apagar (`SfcIsFileProtected`) | ✅ | `Core/UninstallSupport.cs:192` — `SafetyRules.IsProtectedFolder`, chamada em `Core/DeferredDelete.cs:41`, `Core/BackupManager.cs:229` e `:261`, `Core/Tools/TrackCleaner.cs:409`, `Core/Tools/UnrecoverableDelete.cs:85` |
| Detecção de **serviços** | 🟡 | `Core/Tools/AutorunManager.cs` — WMI `Win32_Service` (`ScanServices`, 116; `SetEnabled`, 130) — mas `Core/UninstallSupport.cs` **não menciona serviço**: a detecção de serviço *instalado pelo programa* não existe |
| Remoção de **dispositivos/drivers** instalados pelo programa | ⟡ | **não verificado** — `Core/UninstallerKind.cs:52` conhece o pacote de driver do Windows Installer (`SDBInst`), mas não há remoção de dispositivo |
| Item de inicialização **editável** (auto-run at logon, startup folder, localizar entrada/alvo) | ✅ | `Core/Tools/AutorunManager.cs` — `SetEnabled` (130) cobre `RunKey`, `StartupFolder`, `Service` e `ScheduledTask` (136-139); `Remove` (155) e `Add` (189) |
| Gerenciador de autoruns com **serviços e tarefas agendadas** no mesmo módulo | ✅ | `Core/Tools/AutorunManager.cs` — `ScanServices` (116), `ScanScheduledTasks` (119); MATRIZ E1 = ✅ (346 itens) |
| **Force uninstall** de programa parcial/corrompido | 🟡 | `Ui/ForcedUninstallWindow.xaml.cs` + `Tranal.Tests/ForcedUninstallTests.cs` — a força existe, mas **sem log de instalação** por trás |
| **Desinstalação em lote** (interface e CLI) | ⬜ | MATRIZ D4 = ⬜; busca por "Batch" em `src/`: **0 ocorrências** |
| **Desinstalar pelo log de instalação**, sem depender do desinstalador nativo | 🟡 | `Core/Modules/TracedPrograms.cs` desinstala a partir do log (foto ANTES → instalador → foto DEPOIS → diff → filtro de plausibilidade → log, linhas 21-33); `Core/MsiInstallLog.cs` só **atribui** sobras, e ainda **depois** do desinstalador nativo |
| Remoção de **sobras** depois do desinstalador nativo | ✅ | `Core/UninstallSupport.cs:389` — `LeftoverScanner` (mesmo arquivo, não é arquivo separado) |
| "Don't use detected changes log" + exclusão de chaves/valores | 🟡 | `Core/ExcludeList.cs` existe; **não** há a flag por operação |
| Item preservado com justificativa ("Not deleted. Contains only files or folders to keep.") | ✅ | `Core/LeftoverScanOptions.cs`; `Core/UninstallSupport.cs` — `LeftoverReason` (11 motivos, coluna "Porquê"; MATRIZ C4 = ✅) |
| **Backup do programa desinstalado** + restauração completa do programa | 🟡 | `Core/BackupManager.cs` — `BackupItem` aceita "Arquivo"/"Pasta"/"Registro" (`AddRegistryKey` 193, `AddFile` 225, `AddFolder` 257) e `Restore` (601) restaura item a item; **não** há "restore profile" do programa a partir do log de instalação |
| **Restaurar apenas o log de instalação** | ⟡ | **não verificado** — `Core/Modules/TraceStore.cs` grava e `Core/Modules/TracedPrograms.cs` consome; nenhuma rotina restaura "só o log" |
| **Undo/Redo** de uma restauração | ⟡ | **não verificado** — o único "Undo" em `src/` é `FofAllowUndo` (`Core/RecycleBin.cs:24`), que é sinalizador da lixeira, não undo de restauração |
| Restauração granular por tipo + **sobrescrever arquivo bloqueado** | 🟡 | `Core/BackupManager.cs` restaura por item e por tipo; `Core/FullRegistryBackup.cs` grava `.reg` por hive + `Restaurar.bat`; o **overwrite de arquivo bloqueado não foi verificado** |
| **Transferir programas monitorados para outro PC** (`.zip` portátil + import) | ⟡ | **não verificado** — busca por `ZipFile`/`ExtractToDirectory` em `src/`: **0 ocorrências**; `BackupManager.Save` grava na pasta própria |
| **CLI de snapshot e restauração** (`/snapshot`, `/import`, `/restore`) | ⬜ | `src/Tranal.Cli/Program.cs` — `/scan` (54), `/mu` (60) e `/export` (963); a própria ajuda (909-964) enumera os comandos existentes e **os três switches do Total Uninstall não estão lá** |
| **Ponto de restauração do Windows** antes de desinstalar | ✅ | `Core/UninstallSupport.cs:1170` — `TryCreateRestorePoint` (WMI `SystemRestore`, `RestorePointType` 12) |
| **Backup do registro** antes de desinstalar | ✅ | `Core/FullRegistryBackup.cs` — `.reg` por hive + script `Restaurar.bat` |
| Limite de tamanho da pasta de backup | 🟡 | `Core/BackupManager.cs` — `DeleteOlderThan` (586) e `DeleteOrphans` (435); **não** há limite em MB |
| **"Pending file rename operations"** — ver e aplicar sem reiniciar | 🟡 | `Core/DeferredDelete.cs` — `MoveFileEx` com `MOVEFILE_DELAY_UNTIL_REBOOT` (47) agenda a remoção para o próximo boot; **não** há UI para listar/aplicar a fila de pendências |
| Contadores de referência de **itens compartilhados** + diálogo "Remove shared file" | ⟡ | **não verificado** — busca por "SharedFile" / "ReferenceCount" em `src/`: **0 ocorrências** |
| Exportar lista / alterações em **XML** | ✅ | `src/Tranal.Cli/ExtraCommands.cs` — `/export` com `/txt|/html|/csv|/xml`; MATRIZ D10 = ✅ (CSV/XML/HTML) |
| Exportar lista de programas monitorados | ✅ | `Core/Modules/TracedPrograms.cs`; `Core/Exporters.cs` |
| **Imprimir** as alterações detectadas | ⟡ | **não verificado** — o `src/` exporta CSV/XML/HTML/TXT; impressão não foi encontrada |
| **Windows Apps** como módulo próprio (Store/MSIX) | ✅ | `Core/Modules/WindowsApps.cs` — MATRIZ A3 = ✅ (122 MSIX) |
| Localizar programa por palavra-chave ou **arrastar e soltar** | ✅ | `Core/Modules/HunterMode.cs` (resolve o alvo arrastado) + `Ui/Modules/HunterView.xaml` (`AllowDrop`/`Drop`/`DragOver`) + `Ui/Tools/TopmostOverlayWindow.xaml` |
| Aviso de **programa monitorado ainda em execução** | ✅ | `Core/ProcessTerminator.cs` |
| **Agente autônomo** de background que notifica instalação em andamento | ⬜ | `Core/Modules/InstallMonitor.cs` é **sob demanda** (timer de 30 s, `DefaultIntervalSeconds`); não há agente autônomo |
| **Driver de kernel** | ⬜ | decisão registrada no `MATRIZ.md` F.4 — não usar driver. O Total Uninstall também **não** usa (32 arquivos, nenhum `.sys`) |
| **Validação online de licença** | ⬜ | não aplicável — o Tranal não tem licença |

### 6.1 Onde o Tranal já é melhor

| Ponto | Tranal | Total Uninstall 7.6.2 |
|---|---|---|
| **Atribuição pela cadeia do `.msi`** | `Core/MsiInstallLog.cs` lê o pacote `.msi` **em cache** e sabe **o que o próprio instalador gravou** (arquivos, diretórios, valores de registro), arquivo por arquivo | importa `MsiGetProductInfoW` — apenas **consulta** o produto MSI instalado; a atribuição real é por nome/árvore de mudanças |
| **Confiança com motivo** | cada sobra tem um **motivo** (`LeftoverReason`, 11 motivos, coluna "Porquê") e nível de confiança | o produto mostra o que detectou, sem justificativa textual por item |
| **Backup do registro com script de restauração** | `Core/FullRegistryBackup.cs` — `.reg` por hive + `Restaurar.bat`; restauração **fora do app** e roteirizável | restauração granular e undo/redo **dentro do app**, mas sem script equivalente |
| **CLI roteirizável** | `src/Tranal.Cli/Program.cs` — `/scan`, `/mu`, `/export` com `/mode builtin|safe|moderate|advanced` | CLI **só na edição Ultimate** e não verificada nesta auditoria |
| **Segurança do motor** | `SafetyRules.IsProtectedFolder` antes de apagar/agendar (`Core/UninstallSupport.cs:192`), `Core/ProcessTerminator.cs`, `Core/DeferredDelete.cs` com a fila oficial do Windows e **o Tranal nunca reinicia a máquina** (`Core/Modules/TracedPrograms.cs:27-29`) | apaga com `SfcIsFileProtected` como consulta; regras próprias não measureadas |
| **Privacidade** | sem cadastro e sem telemetria; a única saída de rede auditada no `src/` é a atualização automática, opcional e **desligada por padrão** (`Core/Updates/AutoUpdateClient.cs`; `Core/Settings/AppSettings.cs:78`) | validação **online de licença** obrigatória; firewall pode bloquear (s51076) |
| **Assinatura do instalador** | o instalador é assinado **automaticamente em toda publicação**, com a regra em **um lugar só** (`tools/publish-tranal.ps1` + `installer/tranal.iss`; `AGENTS.md:309-321`) — mas com certificado **autossinado** (`installer/certificado/TranalUninstallerPro.cer`) | assinatura Authenticode **com CA pública** (`Certum Code Signing 2021 CA`) — cadeia **mais forte** que a do Tranal, e o produto também assina por conta própria |
| **Sem driver de kernel nos dois** | decisão `MATRIZ.md` F.4 | 32 arquivos, nenhum `.sys` |

---

## 7. Fichos de evidência

Bytes e SHA-256 **medidos em disco** (`Get-FileHash`, SHA-256) sobre a árvore da auditoria. Base: `…\workspace\auditoria\totaluninstall\`.

### `downloads/`

| Arquivo | Bytes | SHA-256 | Conteúdo |
|---|---|---|---|
| `Total-Uninstall-Setup-7.6.2.exe` | 15.154.488 | `4C5BA95AE62C175B648B62A63799ED96796B22E1E571B78E35E1E6561CC54FC1` | instalador oficial Professional (Inno Setup 6.0.0) |
| `Total-Uninstall-Essential-Setup-7.6.2.exe` | 15.822.576 | `12FB70A6D1DD98067EF8233EE67357E765D14D50360CDD1FE73BD5BC601FC514` | instalador oficial Essential |
| `martau-home.html` | 5.589 | `0A1ED2D1BEDF85B5BB2AF68F69A54FBD9C37F5A79CEDDD1866F1A0590FF97735` | site oficial `martau.com` |
| `martau-download.html` | 10.087 | `558EDB9AF9CB4042EBB4A689C8E2F761CB217CFC01037E6474AA410CFC4D4612` | página de download + release history |
| `martau-buy.html` | 21.427 | `69038E7D88C66D2D50227D091DD3D3B7FBB08C9FF295CB84D34FE94B2B4462F1` | comparativo oficial das 3 edições |
| `martau-documentation.html` | 5.994 | `BE20FB007AB609534E343D90BBE77F89510054790163874AF697E1B34E98F39A` | índice da documentação oficial |
| `martau-screenshots.html` | 7.840 | `672701B6D1B9C32A93F90BD10DC95299242DD98F7C86BE0EFE445A368194F32C` | página oficial de screenshots |
| `nirsoft-all-utilities.html` | 172.399 | `191DC4FA422DD2B846C86F25D8010E1A31EFA9D4328196A415E98248AFE45CCC` | índice completo de utilitários NirSoft (prova da não-existência) |
| `nirsoft-uninstall_view.zip` | 355 | `CC0B4E42510D49C6DECD464123ECF3B14AE9B47F9B4ED2EE64893E2D6520A264` | resposta 404 do servidor NirSoft |
| `nirsoft-installed_app_view.zip` | 355 | `CC0B4E42510D49C6DECD464123ECF3B14AE9B47F9B4ED2EE64893E2D6520A264` | resposta 404 do servidor NirSoft |
| `nirsoft-reg_from_app.zip` | 355 | `CC0B4E42510D49C6DECD464123ECF3B14AE9B47F9B4ED2EE64893E2D6520A264` | resposta 404 do servidor NirSoft |
| `nirsoft-folder_changes_view.zip` | 355 | `CC0B4E42510D49C6DECD464123ECF3B14AE9B47F9B4ED2EE64893E2D6520A264` | resposta 404 do servidor NirSoft |
| `nirsoft-single_file_extractor.zip` | 355 | `CC0B4E42510D49C6DECD464123ECF3B14AE9B47F9B4ED2EE64893E2D6520A264` | resposta 404 do servidor NirSoft |

### `app/professional/app/` — payload extraído (32 arquivos: 11 no topo + 21 em `Translations/`)

| Arquivo | Bytes | SHA-256 |
|---|---|---|
| `Tu.exe` | 11.324.608 | `D9DF17D164A52C9F80B73870845FB49E42CE3FB1C236813EC07C9D7C88253BD6` |
| `TuRes.dll` | 12.972.720 | `3A02485A06985EA85E6E591F3844122EABEF7D8F866E618F46836C9339B5A120` |
| `CleanerDefinitions.xml` | 28.003 | `DD20CFB2FD02F5DF09016E8C2062D9FD27451FCE4BB2540E3048D61F1B7EF830` |
| `License.rtf` | 34.273 | `E5E2F1D122F155D3F06E5CD5022F8DB93E61E00ABF2D8F7F2E9CAB33CDB81BBE` |
| `ReadMe.txt` | 2.471 | `5D47BD98DFC935B1EDCFE425E386B0A773FA4655839BBBD439595932840ED082` |
| `Tu_en.chm` | 66.791 | `8B5C10C67D2033D81F590A980488FE63890742A52786ADD621A86B7DB5773E47` |
| `Tu_de.chm` | 75.315 | `4AB647C97C79A49228640338A78856DFAB9D0AB314B08D3F7FE0D2A0E3A58461` |
| `Tu_fr.chm` | 78.385 | `6F580ACB6A90C221B7D755366255FAA4C7FB19C36C3CA1B21F9DA770862E253D` |
| `Tu_es.chm` | 76.725 | `B3BD351BAB54C0CB2325D2F4A19E7465F57F6C4B00BEA46E839BE6A73E868E00` |
| `Tu_pl.chm` | 75.615 | `9AC2DA369FA1F04FDCF889F44E7615FD36E6ECB4207767B02FC5F792F210E92E` |
| `Tu_cn.chm` | 56.171 | `B6B2CA53FE66C2523F4D1096A6536371FA581117A09F5F220EA5A92028040382` |
| `Translations/*.lng` (21) | — | Bulgarian, ChineseSimplified, ChineseTraditional, Czech, Dutch, English, French, German, Greek, Hungarian, Italian, Japanese, Korean, Norwegian, Polish, **PortugueseBrazilian**, Romanian, Slovak, Spanish, Swedish, Ukrainian |

### `evidencia/`

| Arquivo | Bytes | SHA-256 |
|---|---|---|
| `RESUMO.md` | 36.587 | `38785BE66E703570B3FEE7EACC66A604C025961C0E22CE73A0745C171478069B` |
| `http-status-direto.txt` | 1.303 | `FFCC3BB0652D91F082E92458972F1B7B9E36ADCD1908AFAD84FE8B73FCFF842F` |
| `nirsoft-sondagem-zips.txt` | 942 | `B373144829BA58A8DAFD300ECE8D09A4753A7FB58B2ABD4356B59DABB9708868` |
| `pe-Tu.exe.txt` | 1.134 | `BE5723E4AB379286C31AA09402F1ECC90EB8D1E60C2A1237C09C2F2F8E15AC66` |
| `pe-imports-Tu.exe.txt` | 2.036 | `AE8EE5D78E73A5E8677FC6BA26D8296D04A73526EA29209D8C0DD496D3A32D7D` |
| `strings-Tu.exe.txt` | 442.009 | `BDD478E29FF5792B640FD580AF627C174E9707FB15ED82522D2AA63EBFFD3BC7` |
| `strings-TuRes.dll.txt` | 78.058 | `773D0767E2FC477BC8AF890DD3E78E4A1EBB6DDC945CCC06C41B59C53DDC311A` |
| `strings-funcionalidades.txt` | 13.868 | `BB0B3ACA6CF5949E8E820DAEC271C76059B0D2A4F8391D979C595048EF1FEC70` |
| `manual-indice-chm.txt` | 2.341 | `2EDB6FB216C284E50B523A06EBEEF23AB28B093D17975A6322643FA878FB1A8B` |
| `manual-linha-de-comando.txt` | 1.169 | `096EC428126D759F51258966ABBF866B407788DB9C89EFC1325319711920CEC7` |
| `manual-total-uninstall-command-line.txt` | 1.164 | `C5814886C665B7D9CB06BEF7297390C6B953EDB23639D86307B87FEE915B7619` |
| `manual-installation-monitor.txt` | 906 | `744315B085C9D817E50C6541CA78C7D9C8B68832244849F5376E07B650DF090C` |
| `manual-windows-startup-manager.txt` | 443 | `657F9FA3A5F535C86791DB94085EBA8823EA8C6A5CC70533A5A02F14CDA4C4F5` |
| `manual-total-uninstall-portable-version.txt` | 1.104 | `62B1269F480C2D4111B71ACEE716B9A5241F1359697D0356CDB20B436B6641C6` |
| `manual-upgrade.txt` | 1.163 | `B6812A36BCD1D0D149DDAD446240A54BC52FE82476EB373258CF39401A1CE80D` |
| `manual-total-uninstall.txt` | 2.385 | `C7205F2257CDAC01B8302FF9B6367F1F0C9DBA8CAE3725F496A14C6D7BE76F89` |
| `manual-accurate-uninstall-programs.txt` | 1.887 | `2436CAFD489EA4DFDF9C2548B7F5BCB8EEF3F50411566DCA1037211D2B62A4EE` |
| `manual-batch-uninstall-multiple-programs.txt` | 1.847 | `05D371C27D914AA3EC6B5FC9EAA75C7C077914CC0ACCC59DD93B4D37FE3294D4` |
| `manual-force-uninstall.txt` | 1.529 | `8032A93A7D03B2E3C8A0EC77CC761D45D61B595BCF14605A2D5DEA6FE853039B` |
| `manual-safe-uninstall.txt` | 2.953 | `47A4F0D503F9D6DF63E307DDC9B07A6B558F6A3ADD4EFB65913EE50F3B60A89E` |
| `manual-transfer-programs-to-new-pc.txt` | 2.687 | `F6C9AF8ABC128CD6FBBB5B7B5BAA34423D765B95C0C0A3804C93C53FC6F012DB` |
| `manual-*.html` (10) | 5.420–10.456 | cópias HTML das páginas do manual |
| `readme-instalador.txt` | 2.471 | `5D47BD98DFC935B1EDCFE425E386B0A773FA4655839BBBD439595932840ED082` |
| `cleaner-categorias.txt` | 575 | `7AD43A9E33E2D897A608336A40D4AA221CEBAA09FEE9D98525D502A57B01401D` |
| `cleaner-definitions.txt` | 173 | `93E4CDF574216869D6719A31C8C1027A4B393EA526A05B225A03991D837BF793` |
| `arquivos-instalador.txt` | 2.591 | `C66924E481051775B8F2646B7FA84EF961DEE0550BC57C84B6301933C3D043EE` |
| `downloads-hashes.txt` | 1.511 | `FB5EE967B7BF58B18E37B5E6ED60FE4233CB9AC38832F68D2FFAE4BC819E4797` |
| `innoextract-lista-essential.txt` | 1.432 | `099A48679FC0DA8A03FCA16A8F5BE9470E794A6B0FB08D7CC84212E7E2119C91` |
| `rastro-no-sistema.txt` | 494 | `DAFCED30B6059C6A9B901646746DE7A9771B5629DEAD5F46ED29AB1F842D3036` |
| `uia-tu-principal.txt` | 1.704 | `A34CD136DE564F7AB4848B6A07FF9AA35DF139A88AF126E90CDBED70FE9FEF6C` |
| `uia-tu-janelas.txt` | 1.792 | `FD8582C27094B1E7B552BB1D517761153E486EF922B8B3B4EFE698D65C6639B9` |
| `uia-tu-modulo-monitored.txt` | 2.991 | `FEA16DE3242A9F3F897A40399ACBA39240E54FC8BE3A2F86FD5D8FE5A119165B` |
| `tu-01-janela-principal.png` | 83.437 | `D002ECA3C6392F7AE9743AFAC85F23F16D9FF0B3CF1AEA5329808A8CD648BFB9` |
| `tu-02-modulo-monitored-20261105-193236.png` | 161.581 | `B22F4CC0599C4AE31B8CA86CB95B81D431E43BD298FA80267CEC506B2BAF0767` |
| `tu-02-modulo-monitored-20261105-193409.png` | 161.603 | `653F9EE8D0999E07FAF7BA8064C56EE20BE993A4D1403C5E0EC557FBAD43EAD5` |
| `tu-02-modulo-monitored-20261105-193524.png` | 152.320 | `4F0CC174F5B6083735E56D4583BB3CBE9E4E7F3D8421576EE8A0500A3B8C0AD3` |
| `tu-02-modulo-monitored-20261105-194020.png` | 610.831 | `F3F4B410B3202B5AEB124ADE77C5CB8E477DC5BBB5FFC569ECFFF84BA06AC74F` |

Também na árvore: `app/chm/` (60 arquivos — manual `Tu_en.chm` descompilado com `hh.exe -decompile`), `app/essential/app/` (payload idêntico), `app/tools/` (`wincap.ps1`, `drive.ps1`, `drive2.ps1` — scripts de captura e automação usados na auditoria).

---

## 8. Lacunas desta auditoria

| O que faltou | Por quê |
|---|---|
| **Conteúdo dos módulos Monitored programs / Cleaner / Auto-run Manager / Windows Apps** | `Tu.exe` roda **elevado** (`TOKEN_ELEVATED=True`) e a máquina tem janelas elevadas de outros auditores por cima. **UIPI bloqueou** `SetWindowPos`, `MoveWindow`, `ShowWindow`, `SetForegroundWindow` e `InvokePattern`; `PrintWindow` falhou. Há **1 captura limpa** da janela principal e a árvore UIA completa, mas **nenhum pixel** do conteúdo dos módulos. Não houve tentativa de contornar a elevação |
| **Item (A) da NirSoft: não há nada para auditar** | `total_uninstall.html`, `total_uninstall.zip` e `total_uninstall_setup.exe` responderam **HTTP 404**, enquanto utilitários NirSoft que existem (`uninstall_view.html`, `installed_app_view.html`, `installed_packages_view.html`, `folder_changes_view.html`) responderam **HTTP 200** na mesma sondagem. O índice completo de utilitários da NirSoft (**172.399 B**) não tem **nenhuma** ocorrência de "Total Uninstall" (grep: **0**), mas tem UninstallView, FolderChangesView e SingleFileExtractor. Os `.zip` NirSoft retornam 404 **para todos** os utilitários, inclusive os que existem — o 404 de `.zip` não discrimina, e por isso a lista de recursos NirSoft do briefing **não foi medida** |
| **Linha de comando** | `TuCmd.exe` **não está** no payload Professional/Essential (32 arquivos) e a busca por `TuCmd*` em toda a árvore deu **0 ocorrências**. O CLI é declarado no manual como **exclusivo da edição Ultimate**: `/snapshot`, `/import` e `/restore` são **documentados, não executados** |
| **Ciclo de instalação monitorada real** | todo o fluxo *snapshot → instalar → comparar → desinstalar* vem do manual e das strings; **não** foi executada uma instalação monitorada de verdade (exigiria snapshot do sistema e instalação de software de teste) |
| **Origem da elevação** | o manifesto do `Tu.exe` **não** foi inspecionado. A elevação foi medida pelo token do processo; não se sabe se vem de `requestedExecutionLevel`, de auto-elevação ou de o UAC estar desativado nesta máquina |
| **Nenhuma desinstalação real executada** | não foi removido nada do sistema do owner: o app foi executado sem instalar e sem tocar no registro |
| **Processo `Tu.exe` (pid 31548) ficou aberto e elevado** | ao fim da coleta o `Tu.exe` **permanecia em execução com privilégios elevados**, sem prompt de UAC visível. Não foi encerrado por esta auditoria e não foi inspecionado em profundidade — registro como pendência operacional do campo |

---

## 9. Decisões que este documento obriga

1. **Criar** um **motor de regras de resíduo em XML** — `Core/Rules/ResidueRules.cs` + `Core/Rules/CleanerDefinitions.xml`, com o **formato** que a auditoria mediu: categorias com `Definition`/`Scan`/`Include`/`Exclude`/`MaxLevel`, caminhos absolutos e por variável de ambiente (`%WINDIR%`), **623 elementos** e **6 categorias** (Windows, System, Browser, Windows Explorer, Programs, Invalid entries). A regra fica **em um lugar só**: `Core/UninstallSupport.cs` (o `LeftoverScanner` da linha 389) e `Core/Tools/JunkCleaner.cs` passam a **consumir** o motor em vez de cada um ter heurística própria — hoje os dois duplicam o julgamento.
2. **Criar** o **fluxo de snapshot pré/pós-instalação com comparação A/B** — `Core/Modules/SystemSnapshot.cs` já tem `Capture` e `Diff(before, after)`, e `Core/Modules/InstallMonitor.cs` já grava o log do que nasceu durante o monitoramento; falta **guardar os dois snapshots** (pré e pós) e **compará-los** — `SnapshotDiff` hoje só expõe `NewFiles`/`NewFolders`/`NewRegistryKeys`, então a comparação **não** cobre modificados e removidos. `Core/Modules/TracedPrograms.cs` passa a ser o consumidor único desse fluxo.
3. **Implementar** a **desinstalação dirigida pelo log de instalação, sem depender do desinstalador nativo** — `Core/MsiInstallLog.cs` já lê o pacote `.msi` em cache e sabe o que o instalador gravou; o que falta é **usar esse log como fonte da remoção**, não apenas como atribuição posterior. O nível de confiança e o motivo (`LeftoverReason`) continuam decididos em `Core/UninstallSupport.cs`.
4. **Criar** o **backup do programa com restauração completa e undo/redo** — `Core/BackupManager.cs` já faz item a item ("Arquivo"/"Pasta"/"Registro") e `Core/FullRegistryBackup.cs` já gera `.reg` + `Restaurar.bat`; falta o **nível de programa** (backup do conjunto instalado, com um "restore profile") e o **histórico de operações** que permita desfazer uma restauração (`Core/BackupManager.cs` + `Ui/BackupManagerWindow.xaml.cs`).
5. **Criar** a **exportação/importação portátil de programas monitorados** — `Core/Modules/TracedPrograms.cs` + `Core/Modules/TraceStore.cs` devem gerar um `.zip` autocontido (programa + log + backup) que outra máquina importe, espelhando `/import` e `/restore` do Total Uninstall; hoje o Tranal não tem `ZipFile`/`ExtractToDirectory` em nenhum lugar do `src/`.
6. **Expor** as **operações pendentes de renomeação sem reiniciar** — `Core/DeferredDelete.cs` já usa a fila oficial (`MoveFileEx` + `MOVEFILE_DELAY_UNTIL_REBOOT`); falta a **camada de leitura e aplicação** dessa fila, para que o usuário veja e resolva as pendências — o mesmo que o rodapé do Total Uninstall mostra ("Pending file rename operations.").

Regra que atravessa as seis: **a regra fica em um lugar só e o resto consome**. Os seis itens acima criam seis módulos canônicos (`Core/Rules`, o par snapshot/diff, o log MSI, o backup de programa, o pacote portátil e a fila de pendências) — e `Core/UninstallSupport.cs`, `Core/Tools/JunkCleaner.cs` e `Core/Tools/TrackCleaner.cs` passam a ser **consumidores** dessas regras, não novas implementações paralelas.
