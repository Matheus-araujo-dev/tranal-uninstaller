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

| # | Funcionalidade | Evidência |
|---|---|---|
| 1 | **Desinstalação em lote**, inclusive de apps que **não suportam desinstalação silenciosa** | `manual-features.txt` |
| 2 | **Desinstalação não assistida** — "everything is removed without any user interaction" | `manual-features.txt` |
| 3 | **Motor de desinstalação próprio** — "Most other uninstallers simply run the default uninstaller program" | `manual-features.txt`, `Automation_Begin_Finalize` |
| 4 | **2 modos**: Normal e Silent | `Uninstaller.mode-normal`, `Uninstaller.mode-silent` |
| 5 | **Automatiza desinstaladores que não aceitam silent**, tentando ` /passive /norestart /quiet`, `/quiet`, `/silent`, `/passive`, `/verysilent` | `strings-uninstalr-own.txt` |
| 6 | **Remoção de programas SEM desinstalador** (apaga pasta + registro + sobras) | `Uninstaller.apptype-Leftover-Maybe` |
| 7 | Detecção e remoção de **sobras de programas já desinstalados** | `manual-help.txt` |
| 8 | **3 níveis de varredura**: Normal / Minimal / None — mais "não analisar o registro" e "não analisar pastas temporárias" | `ui-settings-1b.png` (texto literal) |
| 9 | **Pré-visualização** de tudo que será removido, com **exclusão item a item** | `TShowDataForm_Uninstalr` |
| 10 | **Exclusão** de arquivos/pastas/unidades, configurável | `ui-settings-2.png` |
| 11 | **Detecção automática de caminhos a ignorar** (`$Recycle.Bin`, `System Volume Information`, `Windows.~BT`, `Windows.~WS`, `Recovery`, pastas de backup do sistema) | `ui-settings-2.png` |
| 12 | **Lista única** com todos os apps, **tamanho em disco calculado em tempo real** (não o valor do Windows) | `manual-features.txt` |
| 13 | **Corrigir o tamanho errado** que o Windows mostra na lista de instalados | `manual-features.txt` |
| 14 | **16 tipos de app** com detecção dedicada: normal, MS Store, portable, sobras, Chocolatey, Ninite, PortableApps.com, Steam, EA App, Epic, Riot, GOG Galaxy, Wargaming.net, Battle.net, itch.io, Big Fish | `manual-features.txt` |
| 15 | Detecção de sobras via **MuiCache / TypeLib / AppX ModelID** | `GLOB_MuiCacheData1..3`, `GLOB_TypeLib_ToApp` |
| 16 | **País de origem** do app, com bandeira | `ui-lista.png` |
| 17 | **Backup da lista de instalados** do Windows antes de alterar (chave `Applistbackup`) | `FindApps_Registry_Applistbackup` |
| 18 | **Ponto de restauração** antes da desinstalação, com opção "só uma vez por sessão" | `ui-settings.png` |
| 19 | **Log de desinstalação** em arquivo (`Uninstalr.log`) | `Uninstaller.options-generate-log` |
| 20 | **Fechar programas e serviços** em execução automaticamente | `KillAllRunningProcesses`, `KillAllRunningServices` |
| 21 | **Reiniciar automaticamente** para finalizar (avisa que pode reiniciar 1 ou 2 vezes) | `ui-settings.png` |
| 22 | **Executar scripts de desinstalação** automaticamente (ligado por padrão) | `Uninstaller.options-auto-script` |
| 23 | **Gerar `.bat`** para desinstalação em lote + `PreStartupDelete.bat` para o próximo arranque | `strings-uninstalr-own.txt` |
| 24 | **Retomar a desinstalação após reinício** via `RunOnce` (`Uninstalr_Unsetup_`, `Uninstalr_Rerun_`) | `strings-uninstalr-own.txt` |
| 25 | **Monitoramento de instalação** — detecta software novo automaticamente ("New software detected") | `ui-monitoring.png` |
| 26 | **Deep Analysis** sob demanda, com aviso de que pode levar minutos | `ui-lista.png` |
| 27 | Tooltip por app com Publisher, Tipo, Versão, Data, **País**, "Automatically starts with Windows", pasta, chave de registro, uninstall key, comando completo, executável principal, atalho principal | `ui-lista.png` |
| 28 | **29 idiomas** embutidos (inclui Português) | `manual-features.txt` |
| 29 | Tema claro/escuro com 3 opções | `ui-settings-1b.png` |
| 30 | Opções de **acessibilidade** (leitor de tela, moldura padrão, botões padrão) | `ui-settings.png` |
| 31 | **Telemetria opt-in** — pergunta explicitamente antes de enviar nomes e caminhos | diálogo observado durante o scan |
| 32 | **Licença Pro embutida** — converte a cópia free em Pro sem reinstalar | `ui-about.png` |
| 33 | Formulário de **adicionar app manualmente** e de **corrigir dados de entrada** | `TNewAppForm_Uninstalr`, `TFixInfoForm_Uninstalr` |

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

| O que faltou | Por quê |
|---|---|
| **Desinstalação real** de um programa | não executada — exigiria instalar e desinstalar de verdade |
| Mapeamento exato programa→flag silenciosa que o motor escolhe | os flags estão medidos; o critério de escolha, não |
| A rotina de "deep analysis" por baixo (o que ela varre) | sem instrumentar o processo |

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
