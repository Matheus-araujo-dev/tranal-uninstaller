# Geek Uninstaller 1.6.0.200 (Free) — auditoria > O concorrente **mínimo**. Toda a superfície observável do Geek gratuito
> cabe em **60 strings** — contra 1.136 do Revo Pro e 1.066 do BCU. Serve
> como a referência do que um desinstalador enxuto ainda precisa ter, e do
> que ele dispensa. Data da coleta: 05/11/2026 · Versão auditada: **1.6.0.200** (edição **Free**) --- ## 1. Identificação | Campo | Valor | Fonte |
|---|---|---|
| Produto | Geek Uninstaller Free (pessoal, sem suporte) | `manual-download.txt` |
| Versão | **1.6.0.200** | `pe-geek-exe.txt` |
| Editor | Thomas Koen / Geek Uninstaller (Bélgica) | `manual-download.txt` (© 2012–2026) |
| Site | https://geekuninstaller.com/ | `manual-overview.html` |
| Download | `https://geekuninstaller.com/geek.zip` (botão "ZIP (4.64 MB)") | `manual-download.txt` |
| SHA-256 do zip | `B4EED5DFC430ECF74A7B2AB98EDE605D32D51FCF79BB2A0878021EA88671CDC9` | `sha256-geek-zip.txt` |
| Conteúdo do zip | **1 arquivo: `geek.exe`** (11.980.432 B) — sem instalador | `app/geek.exe` |
| Formato | **PE32 nativo, 32 bits, Windows GUI** — C++/MSVC (seções `.text .rdata .data .fptable .rsrc`), UI com o Codejock **XTP** (`XTPDockBar`, `XTPToolBar`, `XTPStatusBar`) | `pe-geek-exe.txt`, `uia-geek-arvore.txt` |
| Assinante | `CN=CrystalBit Solutions, O=CrystalBit Solutions, S=West-Vlaanderen, C=BE` — Sectigo Public Code Signing CA R36, **válido até 13/04/2029** | `pe-geek-exe.txt` |
| Compilação | 2026-10-14 11:20:34Z | `pe-geek-exe.txt` |
| Requisitos | Windows 11/10/7/8/8.1, 32 e 64 bits (inclusive ARM); **exige administrador**; em x64/ARM64 roda como nativo de 64 bits | `manual-download.txt` | ### O launcher de 32 bits (medido) `geek.exe` é um **launcher**: na execução ele extrai `geek64.exe` (4.154.000 B)
em `%TEMP%` e o executa — as strings do binário contêm `geek64.exe` e
`geek-arm64.exe`, e o release notes 1.4.3.102 documenta "geek64.exe is
extracted to the same directory (on 64-bit Windows)". O processo `geek64.exe`
foi observado em execução com a janela "Geek Uninstaller 1.6.0.200". **Nada foi gravado** em `%ProgramFiles%` nem no Menu Iniciar: o Geek não
precisa de instalador. ### O dado mais revelador da auditoria A **superfície observável inteira** do Geek gratuito está em
`evidencia/langstrings-en.txt`: **60 strings**, em 8 blocos
(`MainMenu`, `ListCtrl`, `ExplorerBar`, `UninstallWizard`, `DlgAbout`,
`Messages`, `ProVersion`, `HTMLReport`, `Misc`). | Produto | Strings observáveis |
|---|---|
| Revo Pro 5.5.4 | 1.136 (42 seções) |
| Bulk Crap Uninstaller 6.3.0 | 1.066 (45 telas) |
| IObit Uninstaller 16.1 | 95 telas de diálogo |
| **Geek Uninstaller 1.6.0 Free** | **60** | --- ## 2. Superfície observável ### 2.1 Menu (reconstruído e **capturado**) | Menu | Itens |
|---|---|
| **File** | Export to HTML… · Exit |
| **Action** | Uninstall… · Modify… · **Force Removal** · **Batch Force Removal** · Remove Entry · **Batch Remove Entries** · Registry Entry · Installation Folder · Program Website · Open in Microsoft Store · Browse Location… · Browse Source… · Google for |
| **View** | Desktop Apps · Microsoft Store Apps · Refresh · **Reset Columns** · Color Scheme (Match Windows light/dark · Light · Dark) · **Interface Language** |
| **Help** | About… · Home Page | ### 2.2 Funcionalidades | # | Funcionalidade | Evidência | Tipo |
|---|---|---|---|
| 1 | Lista com **nome, tamanho, data de instalação**; total agregado no rodapé ("%d programs of %s size in total") | `langstrings-en.txt`, `elev-Geek…png` | Observado |
| 2 | Indicação de app 32-bit dentro do nome ("(32-bit)") | `mark32bitItem` | Observado |
| 3 | **Filtro incremental por digitação**, sem campo visível ("Type to find a program") | `itemShortSearchTip`, `uia-geek-arvore.txt` (classe `Edit`) | Observado |
| 4 | Aviso "No matching items, press ESC to reset" | `staticNoItemsMatchingFilterTip` | Documentado |
| 5 | **Desinstalação** pelo desinstalador do programa | `mnuActionUninstall` | Observado |
| 6 | **Modificar/reparar** a instalação | `mnuActionModify` | Documentado |
| 7 | **Force Removal** — remoção sem executar o desinstalador, com confirmação | `mnuActionDelete`, `msgForceRemovalQuestion` | Observado |
| 8 | **Batch Force Removal** | `mnuActionDeleteBatch` | Documentado |
| 9 | **Remove Entry** (entrada do registro) + **Batch Remove Entries** | `mnuActionRemoveEntry`, `mnuActionRemoveEntryBatch`, `msgRemoveEntryQuestion` | Observado |
| 10 | **Assistente de desinstalação** com varredura de sobras em **2 categorias**: File System (%d items) e Registry (%d items), em **árvore** | `treeItemFileSystem`, `treeItemRegistry`, `wiz-wizard-*.png` | **Observado** |
| 11 | Mensagens do assistente, **medidas em execução**: "Alguns itens órfãos encontrados!" → "Gerando lista de traços…" → "Todos os traços removidos" | `wiz-pos-finalizar-*.png`, `wiz-final-*.png` | **Observado** |
| 12 | **Cancelamento** da remoção de sobras com confirmação ("The leftovers found will not be removed. Close the wizard?") | `msgCancelWizard` | Documentado |
| 13 | Diálogo de falha de desinstalação com opção de **limpar a entrada do registro e os arquivos** | `msgUninstallFailed` | Documentado |
| 14 | "Browse..." dentro das sobras encontradas | `treeItemPopopMenuBrowse` | Documentado |
| 15 | Aba de **Microsoft Store Apps** (Ctrl+Tab) | `mnuViewWinStoreApps`, `geek-store-apps.png`, release notes 1.4.0.82 | Observado |
| 16 | **Reset Columns** | `mnuViewListColumnsReset` | Documentado |
| 17 | **Tema** automático (light/dark do Windows) | `mnuViewColorScheme*` | Documentado |
| 18 | **Interface multilíngue** (pt-BR incluído) | `prefs-geek.xml` (`Portuguese_Brazilian`), `mnuViewLanguage` | Observado |
| 19 | **Exportação em HTML** ("Generated by %s on %s", "Installed Programs (%s)") | `HTMLReport`, `dlg-export-*.png` | Observado |
| 20 | Ir para a **chave do registro**, a **pasta de instalação**, o **site do programa**, a **Microsoft Store** | `mnuActionJumpTo*` | Observado |
| 21 | **Browse Location…** e **Browse Source…** | `mnuActionBrowseLocation`, `mnuActionBrowseSource` | Documentado |
| 22 | **Google for** — pesquisar o programa no navegador | `mnuActionGoogleFor` | Documentado |
| 23 | Verificação de **nova versão** | `msgNewVersionAvailable*` | Documentado |
| 24 | Categoria **Windows Updates** | `Misc.WindowsUpdatesText` | Documentado |
| 25 | Aviso quando a remoção da entrada falha por falta de permissão | `msgRemoveEntryFailed` | Documentado | ### 2.3 O que a versão **PRO** promete (banner, não executado) Real-time installation monitor · Thorough and complete removal · Start-up
programs management · Modern interface, technical support · **Batch removal**. > Repare: o Free já tem *Batch Force Removal* e *Batch Remove Entries*; o
> "Batch removal" do PRO é a desinstalação completa em lote — o que o Free
> não faz. --- ## 3. Mecanismos notáveis | Mecanismo | Evidência |
|---|---|
| 18 DLLs importadas: `KERNEL32`, `USER32`, `GDI32`, `MSIMG32`, `ADVAPI32`, `SHELL32`, `COMCTL32`, `SHLWAPI`, **`UxTheme`**, `ole32`, `OLEAUT32`, `oledlg`, `VERSION`, **`gdiplus`**, `ntdll`, `WINMM`, **`OLEACC`**, **`WININET`** | `pe-geek-exe.txt` |
| `OLEACC` → **acessibilidade**; `UxTheme` → temas do Windows; `gdiplus` → gráficos | idem |
| `WININET` → verificação de nova versão pela rede | idem |
| Preferências em formato **`PortableProgramData version="2"`** (XML ao lado do executável) — cor, ordenação inicial, **idioma**, geometria da janela, colunas | `prefs-geek.xml` |
| **Sem driver, sem serviço de fundo** | `pe-geek-exe.txt` | --- ## 4. Interface Codejock XTP. A árvore UIA expõe 7 elementos: `XTPDockBar` (topo),
`XTPToolBar` (barra de menu), `SysListView32` + `SysHeader32` (lista e
cabeçalho), `Edit` (campo de filtro) e `XTPStatusBar` (rodapé). O assistente de desinstalação é um diálogo `#32770` de 480×600 com
`SysTreeView32` (as sobras), `msctls_progress32` (progresso) e os botões
**Finalizar** / **Cancelar**. O diálogo de sobras é `#32770` de 496×288. --- ## 5. Linha de comando O Geek **não tem** interface de linha de comando publicada. O único
comportamento documentado é arrastar um executável sobre a janela. --- ## 6. O que o Tranal NÃO faz hoje | Recurso do Geek | Tranal |
|---|---|
| **Diálogo "Itens órfãos encontrados"** — o Geek_avisa explicitamente sobre itens órfãos e oferece removê-los | ⬜ — o Tranal não tem o conceito de órfão |
| **Ciclo do assistente**: "Gerando lista de traços…" → "Todos os traços removidos" | 🟡 — o Tranal tem as etapas, sem o resumo "todos os traços removidos" |
| **Cancelamento** da remoção de sobras com confirmação | ⟡ — o Tranal cancela o assistente, mas sem essa confirmação |
| **Batch Remove Entries** (remover entradas em lote) | ⬜ |
| **Batch Force Removal** | ⬜ |
| **Modificar/reparar** a instalação | ⬜ |
| **Reset Columns** | ⬜ |
| **Abrir na Microsoft Store** | ⬜ |
| **Browse Source…** (ir à origem do instalador) | ⬜ |
| Diálogo de falha de desinstalação com opção de limpar entrada e arquivos | 🟡 — o Tranal tem modo forçado, mas não esse diálogo |
| Categoria **Windows Updates** | ⬜ | ### Onde o Tranal já é melhor | Ponto | Tranal | Geek |
|---|---|---|
| **Backup do registro antes de desinstalar** | ✅ completo, com script de restauração | ❌ nenhum |
| **Ponto de restauração** antes de desinstalar | ✅ | ❌ nenhum |
| **Confiança das sobras com motivo** | 3 níveis + frase por item | 2 categorias (File System / Registry), sem confiança |
| **Lixeira** para os arquivos | ✅ | ❌ apaga direto |
| **Adiar para o reinício** | ✅ | ❌ |
| **CLI** | `/scan`, `/mu`, `/export`, `/columns` | ❌ nenhuma |
| **Idiomas** | pt-BR e en-US | 30+, melhor em quantidade, pior em qualidade de tradução |
| **Ferramentas** (rastros, backup, registro, autoruns) | ✅ painel completo | ❌ só desinstalação | > **O Geek gratuito é o exemplo mais claro de que a lista de recursos do
> Revo Pro é majoritariamente toolset**: o desinstalador em si cabe em 60
> strings. O que o Revo paga caro é o **painel de manutenção** — e é
> exatamente isso que o Tranal já tem. --- ## 7. Fichos de evidência | Arquivo | Bytes | Conteúdo |
|---|---|---|
| `app/geek.exe` | 11.980.432 | o binário |
| `evidencia/langstrings-en.txt` | 5.829 | **as 60 strings do app** — o inventário completo |
| `evidencia/strings-geek-exe.txt` | 326.188 | strings ASCII + UTF-16 do binário |
| `evidencia/pe-geek-exe.txt` | 878 | cabeçalho PE, assinatura, 18 DLLs importadas |
| `evidencia/manual-download.txt` | 21.373 | página de download + release notes |
| `evidencia/uia-geek-arvore.txt` | 429 | árvore UIA (7 elementos) |
| `evidencia/prefs-geek.xml` | 577 | preferências em PortableProgramData |
| `evidencia/wiz-*.png` | — | 11 capturas do assistente de desinstalação |
| `evidencia/menu-*.png` | — | 8 capturas dos 4 menus |
| `evidencia/elev-Geek_Uninstaller_1_6_0_200-20261105-190211.png` | 42.168 | janela principal em execução | --- ## 8. Lacunas desta auditoria | O que faltou | Por quê |
|---|---|
| **Desinstalação real** de um programa | não executada — exigiria instalar e desinstalar de verdade |
| Comportamento do **PRO** | software pago; só o banner foi medido |
| Como o Geek calcula o **tamanho** de cada programa | não instrumentado |
| O **Windows Updates** na prática (a string existe, o uso não foi observado) | não navigado | --- ## 9. Decisões que este documento obriga 1. **O diálogo de órfãos é uma ideia que vale roubar.** O Geek diz "Alguns itens órfãos encontrados!" — e oferece removê-los. O Tranal hoje nem tem o conceito de órfão. A auditoria do BCU já mostrou que os dois unicos desinstaladores que geram "órfão" são os que **removem programas sem desinstalador** — e o Tranal decidiu não fazer isso (§8 do `PARIDADE.md`). O termo, porém, serve: **sobra de confiança baixa que não pertence a nenhum programa instalado** é informação útil, e hoje o Tranal apenas a lista.
2. **O resumo do ciclo ("Todos os traços removidos") é uma frase que falta.** Depois da varredura, o Tranal mostra a lista; o Geek mostra uma frase de fechamento. Custa pouco e fecha a história na tela.
3. **Cancelar a remoção de sobras precisa de confirmação.** É barato e protege.
4. **A lista de recursos pagos do Revo é majoritariamente toolset.** Isso consolida a aposta do Tranal: desinstalador + painel de manutenção. --- *Auditoria gerada por medição direta dos artefatos e por execução do
programa. Toda linha deste documento aponta para evidência em disco; o que
não pôde ser verificado está marcado como tal na seção 8.*
