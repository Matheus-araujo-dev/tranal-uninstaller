# Ashampoo UnInstaller 27.0.1 Professional Edition — auditoria

> O **Ashampoo UnInstaller** é o concorrente com a maior **superfície funcional**
> já levantada: **66 capacidades** inventariadas, 38 presentes nas duas edições
> e **28 exclusivas do PRO 27**. Ele não é só um desinstalador — é também um
> **monitor de instalação** (grava o log do que o instalador criou), um
> **forense pós-instalação** (cria log de desinstalação para programas que já
> estavam instalados), um **laboratório de snapshots** (compara o estado do
> sistema entre dois pontos no tempo) e um **pacote de manutenção do Windows**
> (registro, disco, rastros, serviços, autorun, plugins de navegador,
> recuperação de arquivos, atualização de programas). São duas edições: o
> **FREE 9.00.00**, de 2019, um **stub de 1,6 MB** que só baixa o produto; e o
> **PRO 27.0.1**, de outubro de 2026, instalador **Inno Setup 7.0.0.3** de
> 46,8 MB por **EUR 40**.

Data da coleta: 05/11/2026 · Versão auditada: **27.0.1** (edição **Professional**,
liberada em 16/10/2026; o **FREE 9.00.00**, de 20/12/2019, é a porta de entrada do
produto e foi o único instalador efetivamente executado nesta auditoria)

---

## 1. Identificação

| Campo | Valor | Fonte |
|---|---|---|
| Produto | **Ashampoo® UnInstaller Pro 27** (catálogo Ashampoo, `product_id` 19116, idstring `2903`) | `evidencia/ash-api-uninstaller-products.json` |
| Variante FREE | **Ashampoo® UnInstaller FREE** (catálogo Ashampoo, `product_id` 10318, idstring `2203`) | `evidencia/ash-api-uninstaller-products.json` |
| Versão PRO | `27.0.1`, `version_release_date` 2026-10-16, `trial_days` 20, `license_type` perpetual, `product_type` trial | `evidencia/ash-api-uninstaller-products.json` |
| Versão FREE | `9.00.00`, release 2019-12-20, `trial_days` 0, `license_type` free | `evidencia/ash-api-uninstaller-products.json` |
| Versão do instalador FREE | `9.00.00-eid.33904` (FileVersion `9.0.0.0`, ProductName "Ashampoo UnInstaller FREE") | `evidencia/pe-installer-free.txt` |
| Versão do instalador PRO | `27.0.1` (FileVersion `27.0.1`, ProductName "Ashampoo UnInstaller Pro 27 Setup") | `evidencia/pe-installer-pro.txt` |
| Editor | **Ashampoo GmbH & Co. KG** — Schafjückenweg 2, 26180 Rastede, Niedersachsen, Alemanha — `info@ashampoo.com` | `evidencia/pe-installer-free.txt` (certificado), `evidencia/padfile-uninstaller.xml` |
| Domínio oficial | `ashampoo.com` (www.ashampoo.com) — API de produtos, manifesto e página do produto | `evidencia/ash-api-uninstaller-products.json`, `evidencia/site-produto-uninstaller.html` |
| Site do produto | https://www.ashampoo.com/en-us/uninstaller | `evidencia/site-produto-uninstaller.html` |
| URL de download FREE | `https://www.ashampoo.com/ashampoo_uninstaller_free_sm.exe` (`eternal_download_url`) | `evidencia/ash-api-uninstaller-products.json` |
| URL de download PRO | `https://www.ashampoo.com/ashampoo_uninstaller_pro_27.exe` (`eternal_download_url`) | `evidencia/ash-api-uninstaller-products.json` |
| SHA-256 do instalador FREE | `b06f9899da6e7eba4ede16ab0d04439a693106c29920e66dee5a0b55c5029f62` | `evidencia/pe-installer-free.txt` |
| Tamanho do instalador FREE | 1.676.704 bytes (1,60 MiB) | `evidencia/pe-installer-free.txt` |
| SHA-256 do instalador PRO | `08a36652800b6b608041d6a445c38079c66239b2751fa4041e07132b9dc9184d` | `evidencia/pe-installer-pro.txt` |
| Tamanho do instalador PRO | 49.098.296 bytes (46,83 MiB) | `evidencia/pe-installer-pro.txt` |
| Formato do instalador FREE | **stub UPX-packed** — PE32+ / x86-64, seções `UPX0`/`UPX1`/`.rsrc`, 11 DLLs / **14 imports**; overlay de 12.192 B = **só o certificado Authenticode** | `evidencia/pe-installer-free.txt`, `evidencia/pea-imports-instaladores.txt` |
| Formato do instalador PRO | wrapper próprio Ashampoo (PE32 / i386, seções `.itext`/`.didata`) + **Inno Setup 7.0.0.3** no overlay (LZMA, 47.966.264 B); 5 DLLs / **150 imports** + 3 delay-load | `evidencia/pe-installer-pro.txt`, `evidencia/pea-imports-instaladores.txt` |
| Assinatura | Authenticode PKCS#7 (`signedData`), SHA-256, cadeia GlobalSign → `Ashampoo GmbH & Co. KG`, validade 2024-05-06 → **2027-06-24** | `evidencia/pe-installer-free.txt` |
| Preço e canal (documentado) | **EUR 40**, shareware, **20 dias de trial**; release "Major Update" em 16/10/2026 | `evidencia/padfile-uninstaller.xml` |
| FREE é porta de entrada do PRO | `upgrades_to: "2903"` no catálogo de produtos | `evidencia/ash-api-uninstaller-products.json` |
| Executáveis do produto | `UIFREE.exe` (FREE), `UI27.exe` (PRO), `UI15.exe` e `UI15Guard.exe` (v15) | `evidencia/ash-api-uninstaller-products.json`, `evidencia/manual-troubleshooting.txt` |
| Destino de instalação | `C:\Program Files\Ashampoo\Ashampoo App` + `products\` (via Ashampoo App) ou `C:\Program Files (x86)\Ashampoo\Ashampoo UnInstaller 15` (v15) | `evidencia/manual-troubleshooting.txt` |
| Logs em disco | `%PUBLIC%\Documents\Ashampoo\Ashampoo UnInstaller\` → `\logs`, `\logs_raw` | `evidencia/manual-logfolders.txt` |
| Tipos de instalador reconhecidos | Wise, Inno Setup, InstallShield (e por nome de arquivo) | `evidencia/manual-troubleshooting.txt` |

### O domínio e a armadilha do link intermediário

`https://www.ashampoo.com/en/uninstaller/download` responde **404** — o site
migrou de `/en/` para `/en-us/`. E o link intermediário
`https://www.ashampoo.com/en-us/dld/2203/uninstaller-free` **não entrega o
binário**: devolve uma **página HTML de 285.018 bytes** (via
`<meta http-equiv="refresh">`) cujo link real é
`https://www.ashampoo.com/ashampoo_uninstaller_free_sm.exe`.
Evidência: `evidencia/site-dld-2203.html` (primeiros bytes
`3C 21 44 4F 43 54 59 50 45` = `<!DOCTYPE`), cópia idêntica em
`downloads/ashampoo-uninstaller-free.bin`.

### Correção da rodada anterior (revisão de 2026-11-05)

A §1 escrita na primeira rodada afirmava que o instalador PRO **não** era Inno
Setup ("innoextract: Unexpected setup loader revision: 2"). Isso está **errado**:
a varredura do arquivo inteiro encontrou as assinaturas
`Inno Setup Setup Data (7.0.0.3)` no offset **740.624** (dentro de `.data` do
próprio wrapper) e **47.558.923** (no overlay). A falha do `innoextract` 1.9
(suporta até 6.0.5) é **compatibilidade da ferramenta**, não ausência de Inno
Setup. O payload é Inno Setup 7.0.0.3 em LZMA e **não pôde ser extraído** com o
que existe nesta máquina (§8).

---

## 2. Superfície observável

Inventário de **funcionalidades** (não de telas). **66 itens**, numerados de 1 a
66 sem lacuna. A última coluna diz em qual edição a capacidade está documentada:
**FREE + PRO** = existe nas duas; **só PRO** = não existe na edição FREE.

> **Todos os 66 itens são documentais** (manuais oficiais FREE e Pro 27, site
> oficial e manifesto PAD). O que foi **medido** nesta máquina está na §3
> (imports, empacotamento, assinatura) e na §4.1 (instalador FREE) — a
> §4.2 inteira é documental, por causa da limitação de medição registrada.

### 2.1 Núcleo do desinstalador e lista de programas

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 17 | Desinstalar vários programas de uma vez (caixas de seleção) | nao confirmado no binario — doc.: `manual-free.txt:60` | PARCIAL — fila em lote por arquivo (`Core/Automation/BatchList.cs`, `/batch`); falta a marcação por caixas na lista principal |
| 18 | Limpeza em profundidade após a desinstalação (in-depth cleaning), habilitada por padrão | nao confirmado no binario — doc.: `manual-free.txt:191-193`, `manual-pro27.txt:156-157` | OK — `Core/UninstallSupport.cs` (`LeftoverScanner`, `LeftoverReason`) |
| 19 | Procedimento de desinstalação em 5 etapas, reversível mesmo sem log | nao confirmado no binario — doc.: `site-produto-uninstaller.html:5226` | OK — 5 etapas em `docs/PARIDADE.md` §"Modos de desinstalação"; reversão em `Core/Recovery/ProgramBackup.cs` |
| 20 | Histórico das instalações/desinstalações recentes, com botão de desinstalar | nao confirmado no binario — doc.: `manual-free.txt:33-35`, `manual-free.txt:198-200` | PARCIAL — histórico de operações (`Core/Recovery/ProgramBackup.cs`) e log por programa (`Core/Modules/TraceStore.cs`) existem; falta a visão única "recentes" com ação de desinstalar |
| 22 | Remover a entrada da lista do Windows de programas instalados, sem desinstalar | nao confirmado no binario — doc.: `manual-free.txt:114-116` | PARCIAL — `Core/ProgramCustomization.cs` e `Core/ExcludeList.cs`; equivalente exato não verificado |
| 23 | Filtrar a lista por texto e ocultar categorias comuns (ex.: .NET Framework) | nao confirmado no binario — doc.: `manual-free.txt:69-71` | PARCIAL — filtro por texto e filtros rápidos existem (`docs/auditoria/MATRIZ.md` A16 ✅); falta ocultar categorias como .NET Framework |
| 24 | Filtrar a lista **por tamanho** (subtópicos) | nao confirmado no binario — doc.: `manual-free.txt:60`, `manual-pro27.txt:39` | AUSENTE — nenhum filtro por tamanho/ano localizado no `src/` |
| 25 | Criar relatório (tabela) de todos os programas instalados, aberto no navegador | nao confirmado no binario — doc.: `manual-free.txt:170-172` | OK — `Core/Exporters.cs` (`ToHtml`) |
| 26 | Exportar a lista de programas para arquivo e **importar** lista exportada (marcar não instalados) | nao confirmado no binario — doc.: `manual-pro27.txt:104-114` | PARCIAL — exportar existe (`Core/Exporters.cs`); **importar** lista de programas não existe (só o importador de log do Tranal, `Core/Modules/TraceStore.cs`) |
| 28 | Instalar um programa com monitoramento (arrastar o setup ou "Browse") | nao confirmado no binario — doc.: `manual-free.txt:205-211`, `manual-pro27.txt:167-170` | OK — `Core/Modules/InstallMonitor.cs`, `Core/Modules/TracedPrograms.cs` |
| 29 | Atalhos de acesso rápido por módulo na tela de visão geral (tiles) | nao confirmado no binario — doc.: `manual-free.txt:28` | PARCIAL — módulos no painel (`docs/PARIDADE.md` §"Módulos do painel"); tiles de atalho rápido por módulo não foram localizados |
| 59 | Ratings por estrelas (1–5) com base online e download de database | nao confirmado no binario — doc.: `manual-free.txt:51-53`, `manual-pro27.txt:142-144` | AUSENTE — nenhum match para estrelas/avaliação no `src/` (`Core/WebSearch.cs` é busca, não avaliação) |
| 60 | Busca online do programa e "Rate online" | nao confirmado no binario — doc.: `manual-pro27.txt:128-137` | PARCIAL — a busca existe (`Core/WebSearch.cs`); "rate online" não |
| 62 | Comparação de tamanho declarado vs. real do programa, sincronizável com o Windows | nao confirmado no binario — doc.: `site-produto-uninstaller.html:5412` | PARCIAL — o Tranal mede o tamanho real em disco (`Core/Discovery/ProgramScanner.cs`); falta comparar com o "tamanho declarado" e sincronizar com o Windows |
| 63 | Categoria automática expandida + reset para "Unsorted" | nao confirmado no binario — doc.: `site-produto-uninstaller.html:5413`, `5425` | PARCIAL — agrupamento automático pela coluna de ordenação existe (`docs/auditoria/MATRIZ.md` D19 ✅); falta o reset explícito para "Unsorted" |
| 64 | País do editor com bandeira e nome | nao confirmado no binario — doc.: `site-produto-uninstaller.html:5414` | AUSENTE — nenhum dado de país/bandeira no `src/` |
| 66 | Desinstalar o próprio produto pelo Windows "Apps & Features" | nao confirmado no binario — doc.: `manual-pro27.txt:513-521` | AUSENTE — não verificado no `src/`; o instalador próprio `installer/tranal.iss` provavelmente registra a entrada, mas isso não foi medido |

### 2.2 Monitor de instalação e log de instalação

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 1 | Monitor de instalação integrado que grava log detalhado de cada instalação | nao confirmado no binario — doc.: `manual-free.txt:5`, `manual-pro27.txt:6` | OK — `Core/Modules/InstallMonitor.cs`, `Core/Modules/TraceStore.cs` |
| 2 | Detecção automática do início/fim da instalação (com aviso "Installation completed" manual como fallback) | nao confirmado no binario — doc.: `manual-free.txt:211`, `manual-pro27.txt:170` | OK — `Core/Modules/TracedPrograms.cs` (fotografia antes → instalador → fotografia depois) |
| 3 | Ícone na bandeja do sistema para o monitor de instalação, com acionamento manual | nao confirmado no binario — doc.: `manual-pro27.txt:13-14` | PARCIAL — o monitor existe (`Core/Modules/InstallMonitor.cs`); não foi verificado ícone de bandeja com acionamento manual |
| 4 | Reconhecimento de tipos de instalador (Wise, Inno Setup, InstallShield) e por nome de arquivo | nao confirmado no binario — doc.: `manual-troubleshooting.txt:5` | OK — `Core/UninstallerKind.cs` (12 tipos, `docs/auditoria/MATRIZ.md` A2 ✅) |
| 5 | Separação entre instalação e atualização, reduzindo detecções falsas | nao confirmado no binario — doc.: `site-produto-uninstaller.html:5515` | PARCIAL — a classificação do desinstalador existe (`Core/UninstallerKind.cs`); a distinção instalação × atualização dentro do log não foi medida |
| 6 | Log por programa com arquivos e alterações de registro | nao confirmado no binario — doc.: `manual-free.txt:76-86` | OK — `Core/Modules/TraceStore.cs` |
| 7 | Mostrar detalhe do log (entradas de registro modificadas etc.) | nao confirmado no binario — doc.: `manual-free.txt:83-85` | OK — `Core/Modules/TraceStore.cs` |
| 8 | Exportar lista de arquivos do log (txt) | nao confirmado no binario — doc.: `manual-free.txt:90-92` | PARCIAL — o log exporta em JSON (`Core/Modules/TraceStore.cs`); falta export txt |
| 9 | Exportar alterações de registro do log (.reg) | nao confirmado no binario — doc.: `manual-free.txt:97-99` | AUSENTE — o exportador do log grava JSON, não `.reg` |
| 10 | Editar manualmente o log de instalação (remover entradas) | nao confirmado no binario — doc.: `manual-pro27.txt:144-146` | AUSENTE — nenhum método de edição de entrada em `Core/Modules/TraceStore.cs` |
| 11 | Apagar o log sem desinstalar (o programa passa a ser "unmonitored") | nao confirmado no binario — doc.: `manual-free.txt:104-106` | OK — `Core/Modules/TraceStore.cs` (apaga o log) |
| 12 | Importar logs do UnInstaller 4 | nao confirmado no binario — doc.: `manual-free.txt:177-179` | AUSENTE — o importador (`Core/Modules/TraceStore.cs`) só lê o formato do Tranal |
| 13 | Abrir a pasta de logs no Explorer | nao confirmado no binario — doc.: `manual-free.txt:184-186` | PARCIAL — o shell é acionado (`ShellExecuteW` em `Core/RegistryEditing/RegistryEditor.cs`); não foi verificado um comando específico para a pasta de logs |
| 14 | **Análise forense / log pós-instalação** para programas instalados sem monitoramento ("Create post-install log") | nao confirmado no binario — doc.: `manual-pro27.txt:43-46`, `site-produto-uninstaller.html:5428-5430` | AUSENTE — não existe fotografia do "antes" de uma instalação já concluída (`docs/auditoria/MATRIZ.md` H.8) |
| 15 | Análise forense em lote (processar todas as aplicações compatíveis de uma vez) | nao confirmado no binario — doc.: `site-produto-uninstaller.html:5429` | AUSENTE — nem o fluxo individual existe |
| 61 | Instal guard: definir como o programa identifica setups e quais propriedades de arquivo usar | nao confirmado no binario — doc.: `manual-pro27.txt:489-490` | AUSENTE — não há motor de regras de detecção de setup configurável |

### 2.3 Snapshots e análise forense

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 16 | Log de instalação **criado a partir da diferença entre dois snapshots** | nao confirmado no binario — doc.: `manual-pro27.txt:275-279` | OK — `Core/Modules/TracedPrograms.cs` + `Core/Modules/SystemSnapshot.cs` (`Diff`) |
| 33 | **Snapshots** do estado do sistema (criar, nomear, listar, apagar) | nao confirmado no binario — doc.: `manual-pro27.txt:246-269` | PARCIAL — o motor existe (`Core/Modules/SystemSnapshot.cs`), mas a fotografia só vive durante a operação: sem snapshots persistidos, nome ou comparação sob demanda |
| 34 | **Comparar dois snapshots** e visualizar as diferenças por categoria, com "Generate report" | nao confirmado no binario — doc.: `manual-pro27.txt:281-285` | PARCIAL — `SystemSnapshot.Diff` devolve o que **nasceu** (`NewFiles`/`NewFolders`/`NewRegistryKeys`), não o que mudou |
| 35 | "Create 2 snapshots now and show changes" (fluxo automático de diff) | nao confirmado no binario — doc.: `manual-pro27.txt:271-273` | PARCIAL — o diff automático existe no fluxo de rastreio; falta o atalho "2 snapshots agora" |
| 36 | Wizard de snapshots por caso de uso, com nomes automáticos | nao confirmado no binario — doc.: `manual-pro27.txt:287-288` | AUSENTE — nenhum wizard de snapshot no `src/` |
| 37 | Snapshot: seleção de drives incluídos e pastas excluídas | nao confirmado no binario — doc.: `manual-pro27.txt:492-493` | AUSENTE — a captura não é configurável por drive/pasta |

### 2.4 Ferramentas de manutenção, limpeza e recuperação

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 38 | Backups do Registry Optimizer, com restauração, exclusão por idade (7/14/30 dias) e visão detalhada | nao confirmado no binario — doc.: `manual-free.txt:463-509` | PARCIAL — backup e restauração existem (`Core/BackupManager.cs`, `Core/FullRegistryBackup.cs`); falta a exclusão por idade |
| 39 | Registry Optimizer (limpeza de entradas obsoletas, com relatório e exceções) | nao confirmado no binario — doc.: `manual-free.txt:300-301` | OK — `Core/Tools/RegistryOptimizer.cs` |
| 40 | Registry Optimizer 2 + "Super Safe Mode" (limpeza mais cautelosa) | nao confirmado no binario — doc.: `site-produto-uninstaller.html:5400-5401`, `5432-5433` | PARCIAL — o otimizador existe; falta o modo "Super Safe Mode" |
| 41 | Drive Cleaner (arquivos desnecessários, com seleção por categoria e relatório) | nao confirmado no binario — doc.: `manual-pro27.txt:295-298`, `manual-free.txt:270` | OK — `Core/Tools/JunkCleaner.cs` |
| 42 | Internet Cleaner (cookies, histórico, exceções e "cookies seguros" / whitelist) | nao confirmado no binario — doc.: `manual-free.txt:281-283`, `manual-pro27.txt:303-309` | PARCIAL — limpeza existe (`Core/Tools/HistoryCleaner.cs`, `Core/Tools/TrackCleaner.cs`); falta a whitelist/"cookies seguros" |
| 43 | File Wiper (sobrescrita de espaço livre e de arquivos/pastas) | nao confirmado no binario — doc.: `manual-pro27.txt:512-580` | OK — `Core/Tools/UnrecoverableDelete.cs`, `Core/Tools/EvidenceRemover.cs` |
| 44 | File Associator (tipos de arquivo sem programa associado, editar/undo) | nao confirmado no binario — doc.: `manual-free.txt:414-457` | AUSENTE — nenhum match para `FileAssociat` no `src/` |
| 45 | Desfragmentação de drives (citada entre as ferramentas) | nao confirmado no binario — doc.: `manual-free.txt:270` | AUSENTE — o Tranal não desfragmenta |
| 46 | Gerenciador de serviços: alterar tipo de inicialização, iniciar/parar, ver dependências | nao confirmado no binario — doc.: `manual-free.txt:330-402` | PARCIAL — só abre `services.msc` (`Core/Tools/WindowsToolsCatalog.cs`); não há `ServiceController`/`StartType` |
| 47 | Autorun / StartUp Tuner: habilitar, desabilitar, adicionar, editar e remover entradas de inicialização | nao confirmado no binario — doc.: `manual-free.txt:288-318`, `manual-pro27.txt:316-331` | PARCIAL — habilita, desabilita e remove (`Core/Tools/AutorunManager.cs`); **falta** adicionar/editar item |
| 48 | Boot Center: resumo de tempo de boot, autorun e tarefas agendadas (enable/disable/delete) | nao confirmado no binario — doc.: `manual-pro27.txt:311-343` | PARCIAL — autorun e o *launcher* de `taskschd.msc` existem (`Core/Tools/AutorunManager.cs`, `Core/Tools/WindowsToolsCatalog.cs`); falta o resumo de tempo de boot |
| 49 | Privacy Traces Cleaner + limpeza de jump lists | nao confirmado no binario — doc.: `manual-pro27.txt:440-441` | OK — `Core/Tools/EvidenceRemover.cs` (jump lists, prefetch, shellbags) |
| 50 | **Unlocker**: desbloquear arquivos em uso, reiniciar o Explorer, desregistrar DLL, encerrar processo | nao confirmado no binario — doc.: `manual-pro27.txt:437-455` | PARCIAL — encerrar processo existe (`Core/ProcessTerminator.cs`); falta desbloquear arquivo em uso, reiniciar o Explorer e desregistrar DLL |
| 51 | **Crash Analyzer**: relatórios de falha do Windows Event Log, agrupados por programa, com export e busca online | nao confirmado no binario — doc.: `manual-pro27.txt:457-458`, `site-produto-uninstaller.html:5402`, `5435-5436` | AUSENTE — nenhum match para `crash` no `src/` |
| 52 | **Undeleter**: recuperar arquivos apagados acidentalmente | nao confirmado no binario — doc.: `manual-free.txt:381-382` | AUSENTE — nenhum match para `undelet` no `src/` |
| 53 | **Software Updater**: atualizar programas instalados, em lote, com lista negra de atualizações | nao confirmado no binario — doc.: `manual-pro27.txt:172-175`, `site-produto-uninstaller.html:5407-5408`, `5419-5420` | AUSENTE — `Core/Updates/AutoUpdateClient.cs` atualiza o próprio Tranal, não programas de terceiros |
| 54 | **Hibernation**: colocar um programa para dormir (desabilita autostart e serviços) e acordá-lo depois | nao confirmado no binario — doc.: `manual-pro27.txt:177-181` | AUSENTE — nenhum match para `hibernat` no `src/` |
| 55 | **Impact**: programas com maior impacto na performance (CPU e memória) | nao confirmado no binario — doc.: `manual-pro27.txt:212-213` | AUSENTE — nenhum ranking de performance no `src/` |
| 56 | **Move**: mover um programa para outro local, recriando o atalho no lugar original | nao confirmado no binario — doc.: `manual-pro27.txt:163-165` | AUSENTE — nenhum match para `reloc` no `src/` |
| 57 | **Revert relocation**: devolver o programa movido à posição original | nao confirmado no binario — doc.: `manual-free.txt:84-86` | AUSENTE — o "Move" não existe |
| 58 | Atalho de área de trabalho "Drag & Drop UnInstall" (arrastar atalhos para desinstalar) | nao confirmado no binario — doc.: `manual-pro27.txt:161` | PARCIAL — arrastar e soltar existe (`Ui/Modules/HunterView.xaml`, `Ui/Tools/TopmostOverlayWindow.xaml`), mas não é o atalho de área de trabalho "Drag & Drop UnInstall" |

### 2.5 Exportação, integração e Windows Apps

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 27 | Exportar logs de desinstalação em HTML, CSV e TXT | nao confirmado no binario — doc.: `site-produto-uninstaller.html:5405` | PARCIAL — a **lista** sai em CSV/TXT/HTML/XML (`Core/Exporters.cs`); o **log de instalação** sai só em JSON (`Core/Modules/TraceStore.cs`) |
| 30 | Remoção de Windows-Apps / system apps, com remoção parcial e restauração dos apps padrão | nao confirmado no binario — doc.: `manual-free.txt:216-228`, `manual-free.txt:261-263` | PARCIAL — a remoção existe (`Core/Modules/WindowsApps.cs`); falta a restauração dos apps padrão |
| 31 | Listar e remover **plugins de navegador** | nao confirmado no binario — doc.: `manual-pro27.txt:215-218` | OK — `Core/Modules/BrowserExtensions.cs` |
| 32 | Remover add-ons e toolbars com um clique | nao confirmado no binario — doc.: `site-produto-uninstaller.html:5226` | PARCIAL — plugins cobertos (`Core/Modules/BrowserExtensions.cs`); o caso "toolbar" não foi verificado |
| 65 | Instalação via "Ashampoo App" (`C:\Program Files\Ashampoo\Ashampoo App`, produtos em `products\`) | nao confirmado no binario — doc.: `manual-troubleshooting.txt:13-15` | AUSENTE — decisão de produto: o Tranal é distribuído por instalador próprio (`installer/tranal.iss`), sem loja de aplicativos |

### 2.6 Linha de comando do desinstalador

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 21 | Editar os parâmetros da linha de comando de desinstalação do programa | nao confirmado no binario — doc.: `manual-free.txt:121-123` | PARCIAL — a remoção manual com o comando derivado existe (`Core/Automation/ManualRemoval.cs`, `Core/Discovery/MsiProductCatalog.cs`); falta o comando de desinstalação editável |

> O item 21 **não** é uma CLI do Ashampoo: ele edita os parâmetros do comando de
> desinstalação do **programa-alvo** (§5.4).

### 2.6.1 Funcionalidades descobertas na superfície do binário (não constavam no documento)

Tudo o que o binário **permite provar** está na superfície do **instalador**
(`ashampoo_uninstaller_pro_27.exe`): runtime do Inno Setup e manifesto. O
aplicativo em si (`UI27.exe`) está comprimido em LZMA dentro do instalador e
**não é acessível** — ver §8.1.

| # | Funcionalidade | Evidência no binário | Tranal |
|---|---|---|---|
| 67 | Instalador Inno Setup 7.0.0.3 com mensagens em inglês | `ashampoo-instalador.strings.txt:3467` `Inno Setup Setup Data (7.0.0.3)`; `:3468` `Inno Setup Messages (6.5.0) (u)`; `:5517` `<description>Inno Setup</description>` | PARCIAL — o instalador do Tranal também é Inno Setup (`installer/tranal.iss`); o conjunto de switches não foi verificado no script |
| 68 | Instalador silencioso (`/SILENT`, `/VERYSILENT`), com log (`/LOG`), tarefas (`/TASKS`), senha (`/PASSWORD`), save/load de settings (`/LOADINF`, `/SAVEINF`) | `ashampoo-instalador.inventario.md` §"Rotulos de interface relevantes" (`Instructs Setup to be silent or very silent.`, `Same as /LOG, except it allows you to specify a fixed path/filename to use for the log file.`, `Specifies a list of tasks that should be initially selected.`, `Please specify the password using the /PASSWORD= command-line parameter.`, `Instructs Setup to load the settings from the specified file after having checked the command line, or to save settings to it.`) | PARCIAL — o Tranal tem CLI própria (`src/Tranal.Cli/Program.cs`), mas não um conjunto de switches de instalador equivalente verificado |
| 69 | Registro do produto para desinstalação pelo Windows | `ashampoo-instalador.strings.txt:3023` `UninstallFilesDir`; `:3024` `UninstallDisplayName`; `:3025` `UninstallDisplayIcon`; `:3034` `CreateUninstallRegKey`; `:3035` `Uninstallable`; `:3088` `UninstallLogMode`; `:3097` `UninstallDisplaySize` | PARCIAL — o instalador do Tranal é Inno Setup, o que implica o mesmo registro; não foi verificado no script |
| 70 | Detecção da versão do Windows suportada (Windows XP → Windows 11, Server 2003 → Server 2025) | `ashampoo-instalador.inventario.md` §"Rotulos de interface relevantes" (`Windows 10`, `Windows 11`, `Windows XP`, `Windows Vista`, `Windows Server 2025`, `Windows Server 2022`, …) | PARCIAL — a matriz de sistemas suportados do Tranal não foi verificada no `src/` |
| 71 | Enumeração de dispositivos/drivers no instalador (SetupDi) | `ashampoo-instalador.inventario.md:409-410` → `**SetupDi** - Enumera dispositivos/Drivers` | AUSENTE — o Tranal remove drivers (`docs/auditoria/MATRIZ.md` E10 ✅) mas não os enumera por SetupDi |
| 72 | Acesso HTTP do instalador (WinHttp) | `ashampoo-instalador.inventario.md:409-411` → `**WinHttp** - Acesso HTTP/FTP` | PARCIAL — `Core/Updates/AutoUpdateClient.cs` fala HTTP com o servidor do Tranal; não há acesso HTTP no instalador |
| 73 | Instalador protegido por senha (criptografia ChaCha20 do setup) | `ashampoo-instalador.inventario.md:9` → classe RTTI `TChaCha20Context`; `ashampoo-instalador.strings.txt` → `TSetupEncryptionKey`, `The password you specified is not correct. Please try again.` | AUSENTE — o instalador do Tranal não tem senha |
| 74 | Fechamento automático de aplicações que travam a instalação (e reinício delas) | `ashampoo-instalador.inventario.md` §"Rotulos de interface relevantes" (`Instructs Setup to attempt closing applications using files that need to be updated, or prevents it from doing so.`, `Instructs Setup to force close when closing applications, or prevents it from doing so.`, `Instructs Setup to attempt restarting applications, or prevents it from doing so.`) | AUSENTE — o instalador do Tranal não fecha aplicações em uso |
| 75 | Manifesto que redireciona DLLs de sistema (`comctl32`, `mpr`, `netapi32`, `netutils`, `textshaping`, `version`) | `ashampoo-instalador.strings.txt:66-71` → `file name="comctl32.dll" loadFrom="%SystemRoot%\system32\" /` e equivalentes | AUSENTE — o manifesto do Tranal não redireciona DLLs |
| 76 | Compilador Delphi (Embarcadero) do instalador e detecção de sistema | `ashampoo-instalador.inventario.md` → `Embarcadero Delphi for Win32 compiler version 36.0 (29.0.55362.2017)`; `ashampoo-instalador.identificadores.txt:22` → `Winapi.Windows.PkgHelper` | AUSENTE — o Tranal é C#/.NET/WPF; decisão de stack, não é lacuna funcional |

### 2.7 Delta FREE × PRO — as 28 capacidades exclusivas do PRO 27

Capacidades que **só existem no PRO 27** (não documentadas no manual do FREE):

1. Monitor de instalação com ícone na bandeja e acionamento manual.
2. **Análise forense** — criar log de desinstalação para instalações já
   existentes ("Create post-install log"), inclusive em lote.
3. Criar **log de instalação a partir do diff entre dois snapshots**.
4. Módulo **Snapshots**: criar, nomear, listar, apagar, comparar, "2 snapshots
   now", Wizard, relatório de diferenças.
5. Configuração de snapshot (drives incluídos, pastas excluídas).
6. **Software Updater** (atualizar programas instalados, em lote, com lista negra).
7. **Crash Analyzer** (análise de relatórios de falha do Windows Event Log).
8. **Unlocker** (desbloquear arquivos em uso / encerrar processo / desregistrar
   DLL / reiniciar Explorer).
9. **Undeleter** (recuperação de arquivos).
10. **Privacy Traces Cleaner** + limpeza de jump lists.
11. **Hibernation** (colocar programa para dormir).
12. **Impact** (ranking de programas por impacto em performance).
13. **Move** de programa e **Revert relocation**.
14. Módulo **Boot Center** (tempo de boot + Autorun + Tarefas agendadas com
    enable/disable).
15. Listagem e remoção de **plugins de navegador**.
16. **Export/Import de lista de programas** e exclusão de entradas importadas.
17. **Edit installation log** (edição manual de entradas do log).
18. **Drag & Drop UnInstall** (atalho de área de trabalho).
19. **Registry Optimizer 2** com **Super Safe Mode**.
20. Exportar logs de desinstalação em **HTML, CSV e TXT**.
21. Comparação de tamanho declarado vs. real + sincronização com o Windows.
22. Categoria automática expandida + reset para "Unsorted".
23. País do editor com bandeira.
24. Módulo **Windows Tools** consolidado (Drive Cleaner nomeado, Safe cookies,
    painel de Boot Center).
25. Remoção de add-ons e toolbars em um clique.
26. Detecção de programas instalados mais rápida + análise de registro
    pós-instalação mais precisa.
27. Comparação de snapshots acelerada.
28. Instal guard (configuração de como setups são detectados).

Observação de método: a comparação automática de títulos está em
`evidencia/manual-delta-free-vs-pro27.txt` (FREE: 88 títulos, PRO: 144,
comuns: 96, só no PRO: 48). Esse arquivo é **derivado de renomeações
incluídas** — por exemplo `StartUp Tuner` (FREE) virou `Boot Center > Autorun`
(PRO) —, por isso a lista acima foi filtrada manualmente para separar renomeação
de capacidade nova.

### 2.8 Alegações do owner — verificadas

| Alegação | Veredito | Evidência |
|---|---|---|
| "monitoramento avançado" | **Confirmado** | `manual-pro27.txt:13-14` (ícone na bandeja, trigger manual), `site-produto-uninstaller.html:5226` ("thoroughly monitors and logs every installation"), `5515` (distingue tipos de instalador, separa atualização de instalação) — **Documentado** |
| "análise forense" | **Confirmado — é o nome oficial do recurso** | `site-produto-uninstaller.html:5399` ("Forensic analysis: Create uninstall logs for existing installations"), `5428-5430`; `manual-pro27.txt:43-46` ("Create post-install log") — **Documentado** |
| "snapshots" | **Confirmado** | `manual-pro27.txt:246-288` (módulo completo com comparação), `site-produto-uninstaller.html:5226` ("the snapshot feature tracks and shows every modification made to your system between two points in time") — **Documentado** |
| "solução mais complexa" | **Confirmado em extensão** | 66 funcionalidades inventariadas; 28 exclusivas do PRO; 5 módulos de limpeza/recuperação (Drive Cleaner, Registry Optimizer, Internet Cleaner, File Wiper, Undeleter) + Unlocker + Crash Analyzer + Privacy Traces + Boot Center + Snapshots — **Documentado** |
| "remoção de sobras" | **Confirmado** | `manual-free.txt:191-193` (in-depth cleaning), `site-produto-uninstaller.html:5226` (five-stage uninstall procedure) — **Documentado** |
| "log de instalação" | **Confirmado** | `manual-free.txt:5`, `manual-pro27.txt:6` — **Documentado** |
| "desinstalação silenciosa" | **Não verificável** | não aparece no manual FREE nem no PRO, nem no site; o manual só documenta "Edit uninstall command line parameters" (que edita o comando do *programa-alvo*) — **Não verificado** (§8) |
| "backup antes de apagar" | **Parcial** | existe o módulo **Backups** (`manual-free.txt:463-509`), mas ele está documentado como backup do **Registry Optimizer**, não como backup de arquivos do programa antes da remoção — **Documentado** |
| "filtro por data/tamanho" | **Parcial** | filtro **por tamanho** documentado (`manual-free.txt:60`); filtro **por data** não documentado — **Documentado** |
| "agendamento" | **Não verificável** | não há agendamento de desinstalação no manual; o PRO tem Boot Center com **Tarefas agendadas do Windows** (habilitar/desabilitar/excluir), que é gestão de tarefas existentes, não agendamento próprio — `manual-pro27.txt:333-343` — **Documentado** |

---

## 3. Mecanismos notáveis

### 3.1 Imports do PE (medido com REA `inspect_pe_imports`, 2026-11-05)

Evidência completa: `evidencia/pea-imports-instaladores.txt`.

**Instalador FREE (stub)** — PE32+ x64, 11 DLLs, 14 imports, sem delay-load:

```
advapi32 : RegCloseKey            comctl32 : ImageList_Add
comdlg32 : GetOpenFileNameW       gdi32    : Arc
KERNEL32 : LoadLibraryA, ExitProcess, GetProcAddress, VirtualProtect
ole32    : CoCreateGuid           oleaut32 : VariantCopy
shell32  : DragFinish             shlwapi  : PathFindExtensionA
user32   : GetDC                  version  : VerQueryValueA
imphash (pefile) : md5 c6665f8c245c8059cfa11faa4145781c
```

14 imports para uma janela de setup completa só podem ser um **stub/loader**:
`LoadLibraryA` + `GetProcAddress` + `VirtualProtect` são o trio clássico de
descompactação e redirecionamento de execução. `GetOpenFileNameW` (botão
"Explorar") e `DragFinish` (arrastar e soltar) explicam a única tela de setup
observada. **Observado.**

**Instalador PRO** — PE32 i386, 5 DLLs normais + 3 delay-load, 150 imports + 3 delay:

```
kernel32 (127) : CreateProcessW, GetExitCodeProcess, LoadLibraryA/W/ExW, GetProcAddress,
                 GetCommandLineW, AdjustTokenPrivileges, ExitWindowsEx,
                 GetVolumeInformationW, GetDriveTypeW, GetDiskFreeSpaceW,
                 FindFirstFileW, CreateDirectoryW, ...
comctl32 (1)   : InitCommonControls
user32   (18)  : CreateWindowExW, MessageBoxW, LoadStringW, ExitWindowsEx,
                 MsgWaitForMultipleObjects, TranslateMessage, DispatchMessageW, ...
oleaut32 (15)  : SafeArray* (8), VariantInit/Clear/Copy/ChangeType, Sys*String
advapi32 (15)  : ConvertStringSecurityDescriptorToSecurityDescriptorW, OpenProcessToken,
                 OpenThreadToken, AdjustTokenPrivileges, LookupPrivilegeValueW,
                 GetTokenInformation, AllocateAndInitializeSid, EqualSid, FreeSid,
                 ConvertSidToStringSidW, RegOpenKeyExW, RegQueryValueExW, RegCloseKey
delay: kernel32!GetLogicalProcessorInformation, user32!MessageBoxA, kernel32!GetNativeSystemInfo
imphash (pefile) : md5 88016fcdef7f227c62171d0afad9aae4
```

### 3.2 O que a tabela de imports prova — e o que ela não prova

| Mecanismo | Como se observa | Situação |
|---|---|---|
| Instalador **elevado** (pede privilégio) | `AdjustTokenPrivileges` + `LookupPrivilegeValueW` + `OpenProcessToken`/`OpenThreadToken` + `ConvertStringSecurityDescriptorToSecurityDescriptorW` | **Observado** (PRO) |
| Reinício do Windows possível ao final da instalação | `ExitWindowsEx` | **Observado** (PRO) |
| O instalador **lê a linha de comando** | `GetCommandLineW` | **Observado** (PRO) — o stub FREE **não** importa |
| Execução de processo filho (payload / Inno Setup) | `CreateProcessW` + `GetExitCodeProcess` | **Observado** (PRO) |
| Leitura do registro do sistema | `RegOpenKeyExW`, `RegQueryValueExW` | **Observado** (PRO) |
| Enumeração de disco/unidades | `GetVolumeInformationW`, `GetDriveTypeW`, `GetDiskFreeSpaceW`, `FindFirstFileW` | **Observado** (PRO) |
| Resolução dinâmica de DLLs em runtime | `LoadLibraryA/W/ExW` + `GetProcAddress` | **Observado** (PRO) |
| **`shell32.dll` NÃO está na tabela de imports do PRO** | ⇒ qualquer uso de shell extension / COM de shell é feito por `LoadLibrary` em runtime e **não é observável por PE** | **Não verificado** por PE |
| **WMI** | `ole32.dll` ausente na tabela de imports do PRO e `WbemLocator` **ausente nos dois binários** (varredura de bytes, 05/11/2026) | **Não verificado** para o app; **improvável** no instalador |
| **`MoveFileEx`** | varredura de bytes nos dois instaladores (`MoveFileEx`, `MoveFileExW`, `MoveFileExA`): **nenhuma ocorrência** | **Ausente** nos dois instaladores (medido) |
| Driver de kernel, serviço do Windows, extensão COM de shell | — | **Não verificado**: o app instalado não foi extraído nem executado (§8) |

Sobre a ausência de driver/serviço: `running_in_tray: false` no catálogo
(`evidencia/ash-api-uninstaller-products.json`) indica que o app **não fica em
bandeja por padrão**; o ícone de bandeja documentado no manual aparece apenas
quando o monitoramento é ligado (`manual-pro27.txt:14`). Nada foi observado
quanto a driver, serviço ou extensão de shell — e nada foi **negado**: o
payload do PRO não pôde ser extraído (§8).

### 3.3 Empacotamento

| Artefato | Observação | Fonte |
|---|---|---|
| FREE | Seções `UPX0`/`UPX1`/`.rsrc` → **UPX-packed**; overlay de 12.192 bytes = **somente o certificado Authenticode**, sem payload embutido (o instalador baixa o produto real) | `pe-installer-free.txt` |
| PRO | Wrapper Ashampoo (`.itext`, `.didata`) + overlay de 47.966.264 bytes com **Inno Setup 7.0.0.3 / LZMA** | `pe-installer-pro.txt`, `pea-imports-instaladores.txt` |
| Assinatura | Authenticode válido, GlobalSign EV, validade até 2027-06-24 | `pe-installer-free.txt` |

### 3.4 Snapshot de disco: o que se sabe e o que não se sabe

O manual só permite escolher **quais drives** entram no snapshot e **quais pastas
excluir** (`manual-pro27.txt:492-493`). Isso é consistente com snapshot de
arquivos/registro por drive, **não** com imagem de disco — mas o **nível exato
não é verificável pelo manual**: marcado como **Documentado (parcial)**.

### 3.5 Onde o produto guarda estado (documentado)

| Item | Caminho | Fonte |
|---|---|---|
| Pastas de log e registros de instalação | `C:\Users\Public\Documents\Ashampoo\Ashampoo UnInstaller\`, `\logs`, `\logs_raw` | `manual-logfolders.txt:5-8` |
| Instalação via Ashampoo App | `C:\Program Files\Ashampoo\Ashampoo App` + `products\Ashampoo UnInstaller 15` | `manual-troubleshooting.txt:13-15` |
| Instalação direta (v15) | `C:\Program Files (x86)\Ashampoo\Ashampoo UnInstaller 15` | `manual-troubleshooting.txt:11` |
| Executáveis | `UIFREE.exe` (FREE), `UI27.exe` (PRO), `UI15.exe` + `UI15Guard.exe` (v15) | `ash-api-uninstaller-products.json`, `manual-troubleshooting.txt:9` |

### 3.6 O "Ashampoo App" como modelo de distribuição

É a peça mais transferível da auditoria. O instalador FREE **não embute o
produto**: ele é um stub UPX de 1,6 MB cujo overlay tem só o certificado, e o
programa é baixado pelo **"Ashampoo App"**, um contêiner instalado em
`C:\Program Files\Ashampoo\Ashampoo App`, com cada produto em `products\`
(`manual-troubleshooting.txt:13-15`). A tela medida do instalador pede
exatamente esse diretório (§4.1).

O modelo é: **instalador pequeno e assinado → contêiner único → produtos
baixados depois**. Para o Tranal isso é uma decisão de produto (§9-h), não uma
técnica de desinstalação.

---

## 4. Interface

### 4.1 Interface observada (instalador FREE)

Capturas: `evidencia/installer-free-01-20261105-194145.png`,
`installer-free-02-20261105-194226.png`, `tmp-state-20261105-194815.png`. Árvore
UIA: `evidencia/uia-installer-free.txt` e `uia-installer-free-2.txt`. Registro
da condução do wizard: `evidencia/installer-wizard-log.txt`.

Janela única, classe `Window`, título **"Ashampoo UnInstaller FREE"** (UI em
português):

```
Pane  name='Instalação'                          class='Static'  rect=[1211,524,435,40]
Pane  name='Escolha aqui a localização de armazenamento:' class='Static' rect=[1211,645,435,17]
Pane  name='C:\Program Files\Ashampoo\Ashampoo App' class='Edit' rect=[1211,672,350,25]
Pane  name='Explorar'                            class='Button' rect=[1571,672,75,25]
Pane  name='Seguinte'                            class='Button' rect=[1541,878,105,26]
Pane  name='Cancelar'                            class='Button' rect=[1428,878,105,26]
```

O instalador pede o **diretório de armazenamento do "Ashampoo App"**, com default
`C:\Program Files\Ashampoo\Ashampoo App` — **Observado**
(`installer-wizard-log.txt`, `uia-installer-free.txt`).

### 4.2 Interface documentada (aplicativo) — **inteiramente documental**

- Barra lateral de navegação com atalhos para todos os módulos + tela
  "Overview" com tiles de status ("Feature quick access") e lista "History" das
  instalações recentes (`manual-free.txt:19-35`).
- Estrutura comum dos módulos, em 4 faixas: **(1)** resumo das entradas
  selecionadas + filtro e busca no topo; **(2)** entradas e detalhes no centro;
  **(3)** ações aplicáveis na base; **(4)** "More functions" (mais ações)
  (`manual-pro27.txt:22-31`).
- "Program module structure" idêntico no FREE e no PRO
  (`manual-free.txt:40-49`).
- Estrelas de rating azuis/amarelas conforme a base online
  (`manual-free.txt:51-53`).
- Atalhos de teclado: **não verificado** (nenhuma captura da interface do app).

> **Limitação de medição registrada (Observado).** A janela do instalador abre
> **atrás** das outras; `SetForegroundWindow`/`AppActivate`/`SetWindowPos`
> retornam `ERROR_ACCESS_DENIED` para **todas** as janelas do desktop; o
> instalador **elevado** bloqueia o UIPI, então o clique não chega. Foram
> tentados clique por coordenada real via `GetWindowRect`, `WM_COMMAND` e
> `LegacyIAccessible` — sem efeito por causa da elevação. O log do wizard
> (`installer-wizard-log.txt`) mostra **10 passes** em que o botão "Seguinte" foi
> acionado sem mudança de estado: **a instalação não avançou**.
> → **A interface do aplicativo (`UIFREE.exe`/`UI27.exe`) NÃO foi observada.**
> Toda a §4.2 acima é documental, e nenhuma captura de tela do app existe.

---

## 5. Linha de comando

**Regra desta seção: switch do *instalador* não é switch do *aplicativo*.** Os
dois são listados separadamente e nada foi inventado.

### 5.1 Instalador FREE (stub `ashampoo_uninstaller_free_sm.exe`)

| Switch | Situação | Base |
|---|---|---|
| qualquer switch | **Não verificado** | o stub tem apenas 14 imports e **não importa `GetCommandLineW`** (`pea-imports-instaladores.txt`); o binário está UPX-packed e a varredura de strings (`evidencia/strings-installer-sm.txt`) não expõe nenhum switch. Sem documentação pública de switches para o instalador FREE |

### 5.2 Instalador PRO (`ashampoo_uninstaller_pro_27.exe`)

| Switch | Situação | Base |
|---|---|---|
| qualquer switch | **Não verificado** | o wrapper **importa `GetCommandLineW`** (`pea-imports-instaladores.txt`), ou seja, ele lê a linha de comando, mas **nenhum switch é documentado publicamente** e o payload Inno Setup 7.0.0.3 não pôde ser extraído para ler os switches do instalador Inno (`/SILENT`, `/VERYSILENT`, `/DIR` etc. são switches do Inno Setup, não do Ashampoo) |

### 5.3 Aplicativo (`UIFREE.exe` / `UI27.exe`)

| Switch | Situação | Base |
|---|---|---|
| qualquer switch | **Não verificado** | nenhum switch do aplicativo está documentado no manual FREE (`manual-free.txt`), no manual Pro 27 (`manual-pro27.txt`) ou no site oficial (`site-produto-uninstaller.html`). O app não pôde ser executado (§8) |

### 5.4 O que **não** é um switch do Ashampoo (armadilha de leitura)

O manual documenta "**Edit uninstall command line parameters**"
(`manual-free.txt:121-123`, `manual-pro27.txt:76-78`). Isso **edita os parâmetros
do comando de desinstalação do programa-alvo** — é um recurso do log, não uma
opção de linha de comando do Ashampoo. Por isso nenhum `/S` ou `/silent` foi
atribuído ao produto: **não há CLI verificável**.

---

## 6. O que o Tranal NÃO faz hoje

Base: greps reais em `E:\Projetos\tranal-uninstaller\src\` (165 arquivos `.cs`).
Cada linha "Onde no Tranal" aponta para `arquivo:linha` já visto no grep.
Legenda: ✅ existe e cobre · 🟡 existe parcialmente · ⬜ não existe · ⟡ não
verificado por falta de equivalente claro.

| Recurso do Ashampoo | Tranal | Onde no Tranal (arquivo) |
|---|---|---|
| Monitor de instalação com log detalhado | ✅ | `Core/Modules/InstallMonitor.cs:28` (`InstallMonitor`) — captura periódica + diff em `:201-207` |
| Log por programa (arquivos, pastas, chaves de registro) | ✅ | `Core/Modules/TraceStore.cs:68` (`TraceStore`); payload `Files`/`Folders`/`RegistryKeys` em `:496-520` |
| **Log de instalação a partir do diff entre dois snapshots** | ✅ | `Core/Modules/TracedPrograms.cs:44-56` — fotografia ANTES → executa o instalador → fotografia DEPOIS → `SystemSnapshot.Diff` → filtro de plausibilidade → `TraceStore.Save` |
| **Análise forense**: criar log de desinstalação para programa **já instalado** | ⬜ | nenhum match para `post-install`/`forense`/`forensic`/`retrospective` — o único hit é um comentário de `Core/Tools/UnrecoverableDelete.cs:39` ("ferramentas de forense" de disco). Não existe fotografia do "antes" de uma instalação já concluída |
| Snapshots **nomeados, listáveis e apagáveis** (biblioteca de snapshots) | 🟡 | o motor existe (`Core/Modules/SystemSnapshot.cs:136` `Capture`, `:180` `Diff`), mas a fotografia "só vive durante a operação" (`Core/Modules/TracedPrograms.cs:43`) — sem snapshots persistidos, sem nome, sem comparação sob demanda |
| Comparar dois snapshots | ✅ | `Core/Modules/SystemSnapshot.cs:180` → `SnapshotDiff` (`:557`): devolve `NewFiles`, `NewFolders`, `NewRegistryKeys` — **só o que nasceu**, não o que mudou |
| Limpeza de sobras com **motivo por item** | ✅ | `Core/UninstallSupport.cs:389` (`LeftoverScanner`), `enum LeftoverReason` em `:46` |
| Desinstalação silenciosa por família | ✅ | `Core/QuietUninstall.cs:26` (`QuietUninstall`); classificação em `Core/UninstallerKind.cs` |
| Backup antes de apagar (registro + script de restauração) | ✅ | `Core/BackupManager.cs`, `Core/FullRegistryBackup.cs` |
| Gerenciador de serviços (tipo de inicialização, iniciar/parar, dependências) | 🟡 | `Core/Tools/WindowsToolsCatalog.cs:85` — **launcher de `services.msc`**; sem `ServiceController`/`StartType` no código |
| Tarefas agendadas (habilitar/desabilitar/excluir) | 🟡 | `Core/Tools/WindowsToolsCatalog.cs:78` — **launcher de `taskschd.msc`**; sem `TaskScheduler` |
| Autorun / StartUp Tuner | ✅ | `Core/Tools/AutorunManager.cs` |
| Plugins de navegador | ✅ | `Core/Modules/BrowserExtensions.cs` |
| Remoção de vestígios de uso (jump lists, prefetch, shellbags) | ✅ | `Core/Tools/EvidenceRemover.cs:72,182` (`ScanJumpLists`), `Core/Tools/TrackCleaner.cs` |
| Otimizador de registro | ✅ | `Core/Tools/RegistryOptimizer.cs` |
| Limpeza de disco / arquivos temporários | ✅ | `Core/Tools/JunkCleaner.cs` |
| Internet Cleaner (histórico, cookies, rastros) | ✅ | `Core/Tools/HistoryCleaner.cs`, `Core/Tools/TrackCleaner.cs` |
| File Wiper (exclusão irrecuperável) | ✅ | `Core/Tools/UnrecoverableDelete.cs` |
| Windows-Apps / system apps | ✅ | `Core/Modules/WindowsApps.cs` |
| Exportar lista de programas | ✅ | `Core/Exporters.cs` — `ToText`/`ToCsv`/`ToHtml`/`ToXml` (`Ui/MainWindow.xaml.cs:1169-1172`) |
| Importar lista exportada (marcar não instalados) | ⬜ | nenhum importador de lista em `src/` — `TraceStore.Import` (`Core/Modules/TraceStore.cs:222`) importa um **log do Tranal**, não uma lista de programas |
| Editar manualmente o log de instalação | ⬜ | `TraceStore` grava (`:143`), importa (`:222`), exporta (`:279`) e apaga o log inteiro (`:191`); **nenhum** método de edição de entrada |
| Exportar alterações de registro do log para `.reg` | ⬜ | `TraceStore.Export` (`Core/Modules/TraceStore.cs:279`) grava **JSON** (`Tranal.TraceLog.v1`, `:71`); não há exportador `.reg` de log |
| Exportar logs de desinstalação em HTML/CSV/TXT | 🟡 | a **lista** sai em CSV/TXT/HTML/XML (`Core/Exporters.cs`); o **log de instalação** sai só em JSON (`Core/Modules/TraceStore.cs:279`) |
| Remover entrada da lista de programas sem desinstalar | 🟡 | `Core/ProgramCustomization.cs`, `Core/ExcludeList.cs` — o equivalente exato do Ashampoo **não foi verificado** |
| Filtro por tamanho na lista | ⟡ | filtro por texto existe; filtro por tamanho/ano **não foi verificado** por grep |
| Remover add-ons e toolbars em um clique | ⟡ | `Core/Modules/BrowserExtensions.cs` cobre plugins; o caso "toolbar" **não foi verificado** |
| "Drag & Drop UnInstall" (atalho de área de trabalho) | 🟡 | há `AllowDrop`/`Drop` em `Ui/Modules/HunterView.xaml:5` (arrastar arquivo → `Resolve`, resolve o programa para a tela de sobras) e em `Ui/Tools/TopmostOverlayWindow.xaml:10,53` (arrastar arquivo → `SetTargetFromFiles`, `Ui/Tools/TopmostOverlayWindow.xaml.cs:88`) — mas **não** é o atalho de área de trabalho "Drag & Drop UnInstall" do Ashampoo |
| Software Updater de programas de terceiros | ⬜ | `Core/Updates/AutoUpdateClient.cs` é auto-atualização **do próprio Tranal** |
| Crash Analyzer (Event Log) | ⬜ | nenhum match para `crash`/`CrashAnalyzer` |
| Unlocker (desbloquear arquivo em uso) | ⬜ | nenhum match para `unlock`/`desbloque`; `Core/ProcessTerminator.cs:61` encerra processo, mas não desbloqueia arquivo |
| Undeleter (recuperar arquivo apagado) | ⬜ | nenhum match para `undelet` |
| File Associator | ⬜ | nenhum match para `FileAssociat`/`associat` |
| Hibernation (dormir programa) | ⬜ | nenhum match para `hibernat` |
| Impact (ranking de performance) | ⬜ | nenhum match para `Impact` (o único hit é "Ope**rating**System") |
| Ratings por estrelas com base online | ⬜ | nenhum match para `estrelas`/`avaliacao`; `Core/WebSearch.cs` é busca de programa na web, não avaliação |
| Move / Revert relocation | ⬜ | nenhum match para `reloc` |
| Agendamento de desinstalação | ⬜ | nenhum mecanismo de agendamento próprio no `src/` |
| Desinstalar o próprio produto pelo "Apps & Features" | ⟡ | não verificado |

### 6.1 Onde o Tranal já é melhor

- **Atribuição pela cadeia de desinstalação**: o Tranal lê o **pacote `.msi`**
  em cache (`Core/MsiInstallLog.cs`, `MsiInstallLogReader` em `:63`) e sabe que o
  item **é** do programa — **149 de 149** pacotes MSI desta máquina
  (`NOVIDADES.md`, `docs/PARIDADE.md`). O Ashampoo depende do nome e do log do
  próprio instalador, que só existe se o monitoramento estava ligado.
- **Confiança com motivo**: cada sobra tem um **motivo** (`LeftoverReason`,
  `Core/UninstallSupport.cs:46`), não só um negrito.
- **Backup do registro com script de restauração** antes de apagar
  (`Core/BackupManager.cs`, `Core/FullRegistryBackup.cs`); o Ashampoo só
  documenta backup do *Registry Optimizer*, não do conteúdo removido.
- **CLI roteirizável** (`/scan`, `/mu`, `/export`, `--uninstall-target` —
  `src/Tranal.Cli/Program.cs:19-20,54,60,66`) — o Ashampoo não expõe nenhuma
  CLI verificável (§5).
- **Desinstalação silenciosa por família**, com a classificação do desinstalador
  em `Core/UninstallerKind.cs` e a execução em `Core/QuietUninstall.cs`; o
  Ashampoo apenas avisa que um programa "não pode ser desinstalado no modo
  silencioso" (`manual-free.txt:33-35`).
- **Segurança do motor**: varredura segura de caminhos (`Core/SafeTraversal.cs`),
  exclusão diferida (`Core/DeferredDelete.cs`) e lixeira (`Core/RecycleBin.cs`).
- **Privacidade**: sem cadastro, sem telemetria. O Ashampoo, em troca, tem base
  online de ratings, busca online de programa, Software Updater e Crash
  Analyzer com busca online — todos recursos de rede que o Tranal não tem e não
  quer.
- **Instalador próprio assinado**, com script de build versionado
  (`installer/tranal.iss`) e certificado versionado
  (`installer/certificado/TranalUninstallerPro.cer`).

---

## 7. Fichos de evidência

### 7.1 Downloads

| Ficheiro | Bytes | SHA-256 | Conteúdo |
|---|---|---|---|
| `downloads/ashampoo_uninstaller_free_sm.exe` | 1.676.704 | `b06f9899da6e7eba4ede16ab0d04439a693106c29920e66dee5a0b55c5029f62` | PE32+ x64, UPX, stub do instalador FREE |
| `downloads/ashampoo_uninstaller_pro_27.exe` | 49.098.296 | `08a36652800b6b608041d6a445c38079c66239b2751fa4041e07132b9dc9184d` | PE32 i386, wrapper Ashampoo + Inno Setup 7.0.0.3 no overlay |
| `downloads/ashampoo-uninstaller-free.bin` | 285.018 | — | HTML do link intermediário (falso download) |
| `downloads/padfile-uninstaller.xml` | 82.499 | — | metadados PAD do PRO |
| `downloads/padfile-uninstaller-free.xml` | 0 | — | download vazio (não usado) |

### 7.2 Extraídos / derivados

| Ficheiro | Bytes | Conteúdo |
|---|---|---|
| `app/free-instalador-secao-rsrc.bin` | 311.808 | seção `.rsrc` do stub FREE (recursos `BTN_*`) |
| `app/pro-instalador-bloco-inno-setup-0.bin` | 1.200 | bloco do header Inno Setup extraído do PRO |

### 7.3 Evidência

| Ficheiro | Bytes | Conteúdo |
|---|---|---|
| `evidencia/RESUMO.md` | 45.106 | inventário bruto desta auditoria (8 seções, 66 funcionalidades) |
| `evidencia/pea-imports-instaladores.txt` | 3.539 | imports dos dois PEs (REA, 2026-11-05) |
| `evidencia/pe-installer-free.txt` | 1.270 | cabeçalho/seções/versões/certificado do stub FREE |
| `evidencia/pe-installer-pro.txt` | 2.375 | cabeçalho/seções/versões do instalador PRO |
| `evidencia/pe-scan-pro.txt` | 148 | varredura de PEs embutidos no PRO (1 PE, offset 0) |
| `evidencia/strings-installer-sm.txt` | 68.530 | strings do stub FREE (sem switches) |
| `evidencia/ash-api-uninstaller-products.json` | 7.002 | API oficial de produtos (FREE e PRO) |
| `evidencia/ash-api-products.json` | 703.018 | API oficial de produtos (catálogo completo) |
| `evidencia/ash-ua.js` | 5.426 | script do site coletado na auditoria |
| `evidencia/site-produto-uninstaller.html` | 562.264 | página oficial do Pro 27 (marketing + release notes) |
| `evidencia/site-free-downloads.html` | 939.145 | página de downloads grátis (catálogo FREE, idstring 2203) |
| `evidencia/site-dld-2203.html` | 285.018 | página HTML do link intermediário (falso download) |
| `evidencia/manual-free.txt` | 12.796 | manual oficial FREE (índice integral) |
| `evidencia/manual-pro27.txt` | 23.922 | manual oficial Pro 27 (índice integral) |
| `evidencia/manual-v10.txt` | 19.853 | manual da versão 10 |
| `evidencia/manual-v14.txt` | 22.662 | manual da versão 14 |
| `evidencia/manual-v15.txt` | 22.964 | manual da versão 15 |
| `evidencia/manual-v16.txt` | 24.526 | manual da versão 16 |
| `evidencia/manual-16-full.htm` | 25.481.516 | manual completo em HTML (v16) |
| `evidencia/manual-indice.txt` | 18.152 | índice comparativo dos 6 manuais |
| `evidencia/manual-delta-free-vs-pro27.txt` | 3.340 | delta automático de títulos FREE × PRO 27 |
| `evidencia/manual-troubleshooting.txt` | 1.454 | detecção de instaladores, `UIxxGuard.exe`, exceções de AV |
| `evidencia/manual-logfolders.txt` | 887 | pastas de log esperadas |
| `evidencia/manual-no-leftovers-tutorial.txt` | 262 | tutorial "No more leftovers" (vazio de texto) |
| `evidencia/api-manual-free.json` | 15.872 | Zendesk API do manual FREE (bruto) |
| `evidencia/api-manual-pro27.json` | 39.709 | Zendesk API do manual Pro 27 (bruto) |
| `evidencia/api-manual-troubleshooting.json` | 2.562 | Zendesk API do troubleshooting (bruto) |
| `evidencia/api-manual-logfolders.json` | 1.780 | Zendesk API dos log folders (bruto) |
| `evidencia/api-manual-no-leftovers-tutorial.json` | 1.462 | Zendesk API do tutorial (bruto) |
| `evidencia/api-manual-v10.json` | 26.051 | Zendesk API do manual v10 (bruto) |
| `evidencia/api-manual-v14.json` | 34.980 | Zendesk API do manual v14 (bruto) |
| `evidencia/api-manual-v15.json` | 35.331 | Zendesk API do manual v15 (bruto) |
| `evidencia/api-manual-v16.json` | 39.443 | Zendesk API do manual v16 (bruto) |
| `evidencia/installer-wizard-log.txt` | 4.663 | condução do wizard do instalador FREE (10 passes) |
| `evidencia/uia-installer-free.txt` | 1.368 | árvore UIA do instalador FREE |
| `evidencia/uia-installer-free-2.txt` | 1.368 | segunda árvore UIA do instalador FREE |
| `evidencia/installer-free-01-20261105-194145.png` | 1.897.679 | captura do instalador FREE |
| `evidencia/installer-free-02-20261105-194226.png` | 28.223 | captura do instalador FREE |
| `evidencia/tmp-state-20261105-194815.png` | 1.571.808 | captura de estado da tela |
| `evidencia/tmp-focus-20261105-194538.png` | 30.458 | captura de foco |
| `evidencia/tmp-focus2-20261105-194545.png` | 30.458 | captura de foco (2ª tentativa) |

### 7.4 Ferramentas de captura reusáveis (já funcionam)

`_tools/drive-wizard.ps1` (6.002 B), `_tools/win-act.ps1` (5.637 B),
`_tools/focus-shot.ps1` (4.314 B), `_tools/uia-act.ps1` (4.300 B),
`_tools/uia-dump.ps1` (1.838 B), `_tools/rsrc-dump.ps1` (3.626 B),
`_tools/extract-res.ps1` (3.710 B), `_tools/scan-pe.ps1` (2.178 B).

---

## 8. Lacunas desta auditoria

1. **Aplicativo não instalado e não executado.** O instalador FREE abre
   **elevado** e atrás de outras janelas; `SetForegroundWindow`/`AppActivate`/
   `SetWindowPos` retornam `ERROR_ACCESS_DENIED` para todas as janelas do
   desktop e o UIPI bloqueia o clique. Já foram tentados clique por coordenada
   real (`GetWindowRect`), `WM_COMMAND` e `LegacyIAccessible` — sem efeito
   (`installer-wizard-log.txt`, 10 passes sem mudança de estado).
   **Consequência:** a interface do `UIFREE.exe`/`UI27.exe`, as strings de tela,
   a árvore UIA real e o comportamento em execução do monitor de instalação
   **não foram observados**. A §4.2 é toda documental.
2. **Payload do PRO não extraído.** `innoextract` 1.9 (máx. 6.0.5) falha com
   "Unexpected setup loader revision: 2" porque o instalador é **Inno Setup
   7.0.0.3**. Nenhuma ferramenta nova foi instalada (sem autorização do owner).
   **Consequência:** os executáveis reais (`UI27.exe` e companhia), seus
   imports, recursos, extensões de shell e serviços **não foram medidos**.
3. **Extensões de shell / driver / serviço do Windows** — **não verificado**. O
   wrapper do instalador PRO não importa `shell32.dll`, e o app não foi
   extraído; logo, registro de `ContextMenuHandlers`,
   `ShellIconOverlayIdentifiers` e CLSID de driver **não puderam ser conferidos
   nem negated**.
4. **Uso de WMI** — **não verificado** para o aplicativo (nenhum import de
   `ole32.dll` no instalador e `WbemLocator` ausente nos dois binários; o app
   não foi analisado).
5. **Switches de linha de comando** — **não verificados** para instalador FREE,
   instalador PRO e aplicativo. Nenhum switch é documentado publicamente; o stub
   FREE está UPX-packed e não expõe `GetCommandLineW` na tabela de imports; o
   PRO importa `GetCommandLineW` mas o switch é lido em código que não foi
   extraído.
6. **Nível exato do snapshot (arquivo/registro por drive × imagem de disco)** —
   não determinável pelo manual; só se sabe que existem ajustes de "drives
   incluídos" e "pastas excluídas".
7. **Desinstalação silenciosa / agendamento próprios** — sem documentação e sem
   execução: não dá para afirmar que o Ashampoo faz ou não faz.
8. **Preço e canal** verificados apenas pelo PAD (EUR 40) e pela API
   (`trial_days: 20`); o checkout da loja não foi exercitado.
9. **FREE × PRO por execução real** — o delta da §2.7 é 100% documental
   (manuais + site). A comparação binária real exigiria instalar o PRO (trial de
   20 dias), o que não foi feito.
10. **Errata na §1 da rodada anterior** — a afirmação "o instalador PRO não é
    Inno Setup" foi refutada por medição e corrigida no topo deste documento
    (offset 740.624 e 47.558.923).

---

## 9. Decisões que este documento obriga

1. **Análise forense pós-instalação vira item de produto.** O "Create post-install
   log" do Ashampoo (item 14, o nome oficial é *forensic analysis*) resolve um
   problema que o Tranal hoje **não tem**: o Tranal só registra o que o
   instalador gravou **enquanto o instalador rodava** (`InstallMonitor.cs:201`,
   `TracedPrograms.cs:44-56`). Para um programa **já instalado**, não existe
   fotografia do "antes" — logo, o caminho de fluxos do Ashampoo é o **log de
   instalação** (item 16) mais a varredura pós-desinstalação. **Decisão:** criar
   no Tranal um fluxo de **log de desinstalação para programa já instalado**,
   explicitamente marcado como *inferido*, nunca como *medido*.
2. **O motor de snapshot já existe — falta a biblioteca.** `SystemSnapshot.Capture`
   e `SystemSnapshot.Diff` (`Core/Modules/SystemSnapshot.cs:136,180`) já produzem
   o diff que o Ashampoo usa para gerar o log de instalação
   (`TracedPrograms.cs:53`), e o `Diff` só devolve **o que nasceu**
   (`NewFiles`/`NewFolders`/`NewRegistryKeys`, `SystemSnapshot.cs:557-577`).
   **Decisão:** manter o motor como fonte única e acrescentar **snapshots
   persistidos e nomeados** (criar, listar, apagar, comparar) — hoje a
   fotografia "só vive durante a operação" (`TracedPrograms.cs:43`).
3. **Gerenciador próprio de serviços e de tarefas agendadas.** O Ashampoo tem os
   dois (`manual-free.txt:330-402`, `manual-pro27.txt:311-343`). O Tranal hoje
   só **abre** `services.msc` (`Core/Tools/WindowsToolsCatalog.cs:85`) e
   `taskschd.msc` (`:78`). **Decisão:** implementar edição de serviço
   (tipo de inicialização, iniciar/parar) e gestão de tarefas agendadas, em vez
   de continuar delegando ao MMC.
4. **Importar lista de programas exportada.** O Ashampoo exporta e importa a
   lista, marcando o que não está instalado (item 26). O Tranal só exporta
   (`Core/Exporters.cs`); `TraceStore.Import` (`Core/Modules/TraceStore.cs:222`)
   importa um **log do Tranal**, não uma lista. **Decisão:** importar a lista
   exportada em CSV/XML do próprio Tranal (e, se fizer sentido, de concorrentes),
   com o mesmo caminho de "não instalado".
5. **Desinstalar pelo log de instalação, sem depender do desinstalador nativo.**
   É a conversa direta com `Core/MsiInstallLog.cs`: quando o log do instalador
   está disponível (149 de 149 pacotes MSI desta máquina), a remoção pode ser
   dirigida pelo log, e o desinstalador nativo passa a ser um caminho
   alternativo. **Decisão:** tratar o log como fonte primária de
   responsabilidade, como o Tranal já faz com `LeftoverReason`.
6. **Edição manual do log de instalação.** O Ashampoo permite remover entradas do
   log (item 10). O `TraceStore` grava, importa, exporta e apaga o log inteiro
   (`Core/Modules/TraceStore.cs:143,191,222,279`), mas **não** edita entrada.
   **Decisão:** dar edição de entrada ao `TraceStore`, porque é o único jeito de
   o log deixar de ser apenas histórico.
7. **"Drag & Drop UnInstall".** O Ashampoo põe um atalho na área de trabalho para
   arrastar e desinstalar (item 58). O Tranal tem `AllowDrop`/`Drop` em
   `Ui/Modules/HunterView.xaml:5` e `Ui/Tools/TopmostOverlayWindow.xaml:10,53` —
   mas o arrastar do Tranal **resolve um alvo** (arquivo → `Resolve` /
   `SetTargetFromFiles`), e **não** é o atalho "desinstalar o que foi arrastado"
   da área de trabalho do Ashampoo. **Decisão:** decidir se o Tranal ganha esse
   atalho; se ganhar, nasce do `TopmostTargetResolver`, não de um `Drop` novo.
8. **Exportar logs em HTML/CSV/TXT.** O Tranal já exporta a **lista** em
   CSV/TXT/HTML/XML (`Core/Exporters.cs`); o **log de instalação** sai só em JSON
   (`Core/Modules/TraceStore.cs:279`). **Decisão:** o log ganha os mesmos formatos
   da lista — a regra fica em um lugar só.
9. **Modelo de distribuição "Ashampoo App".** Stub de 1,6 MB assinado que baixa
   o produto para um contêiner único (`C:\Program Files\Ashampoo\Ashampoo App`,
   §3.6). É uma decisão de **produto** para o Tranal: instalador pequeno, canal
   único de atualização, produtos em subpastas. **Decisão:** avaliar o modelo
   contra a reality de distribuição do Tranal antes de qualquer código.

---

*Fonte da verdade: `auditoria/ashampoo/evidencia/RESUMO.md` (45.106 bytes,
500 linhas, 8 seções, 66 funcionalidades). Método: `auditoria/METODO.md`.
Regra do documento: **medido vale, inferido não vale** — cada afirmação deste
arquivo aponta para evidência em `evidencia/` ou para um grep real em
`src/`.*
