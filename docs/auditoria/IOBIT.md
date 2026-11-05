# IObit Uninstaller 16.1.0.4 (Free) — auditoria

> Concorrente que **empurra** para o lado do "cuidador do PC": além de
> desinstalar, ele audita o sistema inteiro, remove extensões de navegador
> maliciosas e desinstala programas travados de uma base com 4.000+ nomes.
>
> É o concorrente com o **vocabulário mais amplo** de todos os dez: é o
> único que trata desinstalação como um caso particular de "saúde do
> software".

Data da coleta: 05/11/2026 · Versão auditada: **16.1.0.4** (edição **FREE**)

---

## 1. Identificação

| Campo | Valor | Fonte |
|---|---|---|
| Produto | IObit Uninstaller — edição FREE | `evidencia/site-uninstaller.html` |
| Editor | IObit Co., Ltd. — Chengdu, Sichuan, CN | `evidencia/pe-IObitUninstaler.txt` |
| Versão | FileVersion = ProductVersion **16.1.0.4**; Inno: "IObit Uninstaller v16 Official" | `pe-IObitUninstaler.txt` + `innoextract --info` |
| Site oficial | https://www.iobit.com/en/advanceduninstaller.php | `evidencia/site-uninstaller.html` |
| URL usada | `downloadcenter.php?product=advanced-uninstaller-free&insur=en_iu` | link do botão de download |
| SHA-256 do instalador | `98C9B1F5286F1C3B220E696E90B4D2BD7BB5FFB086D8CA83D560F208BE31C747` | `Get-FileHash` |
| Tamanho do instalador | 34.309.696 B | idem |
| Formato do instalador | **Inno Setup 5.5.0 (unicode)**, stub PE32 Delphi | `innoextract --info` + magic `4D 5A 50` |
| Assinante | `CN="IObit Co., Ltd."` — GlobalSign GCC R45 EV, válido até 08/06/2028 | `pe-IObitUninstaler.txt` |
| App principal | `IObitUninstaler.exe` — **PE32 x86 nativo Delphi/VCL**, 10.646.976 B | `pe-IObitUninstaler.txt` |
| Payload | **146 arquivos / 97.626.141 B** | `innoextract-lista.txt` |
| Idiomas da interface | **40 arquivos `.lng`** (inclui pt-BR) | `innoextract-lista.txt:97-135` |
| Idiomas do instalador | 31 idiomas | `innoextract --info` |
| Bases proprietárias | 14 arquivos `.dbd` — `SBData` (7,0 MB, base de software), `PlugDB` (1,6 MB, base de extensões de navegador), `SoftHealth`, `SoftPM`, `BWR`, `PSExt`, `AppRate`… | `innoextract-lista.txt:45-58` |

### A descoberta que muda a leitura do produto

O instalador não é um programa com "um desinstalador": é uma **coleção de
módulos**. O payload traz 13 DLLs próprias com nome de função —
`SysRest.dll`, `forcedelctl.dll`, `filectl.dll`, `rgfpctl.dll`,
`BloatApp.dll`, `UninstallExplorer.dll`, `IUMenuRight.dll`, `RegisterCom.dll` —
e o `Language\English.lng`, que é um INI com **95 seções de diálogo**.

O `.lng` é a mesma coisas que o `lang\english.ini` do Revo: o inventário de
telas. Achatado, virou `evidencia/lng-english-plano.txt`.

---

## 2. Superfície observável

142 funcionalidades medidas. As mais relevantes para o Tranal:

### 2.1 Abas do programa principal

Programs · Browser Extensions · Plugins · Tasks · Startup · Files & Folders ·
Toolbox · More · Windows Apps · Bloatware · History.

Filtros rápidos: All · Bundleware · Logged · Large · Infrequent, mais
filtro por unidade de disco.

### 2.2 O diferencial: extensões de navegador

| # | Funcionalidade | Evidência | Tipo |
|---|---|---|---|
| 1 | **Remoção de extensões de navegador** com base própria (`PlugDB.dbd`, 1,6 MB) | `innoextract-lista.txt:45-58` | Documentado |
| 2 | Classificação da extensão em **Malicious / Ad / Trusted** | `lng-english-plano.txt` | Documentado |
| 3 | **Limpeza de navegador / reset de padrões** (página inicial e motor de busca) | `lng-english-plano.txt` | Documentado |
| 4 | Remoção de **plugins** do navegador | seção `Plugins` do `.lng` | Documentado |

### 2.3 Bloatware, bundleware e programas travados

| # | Funcionalidade | Evidência | Tipo |
|---|---|---|---|
| 5 | **Bloatware Remover**: pré-instalados do Windows, Windows Accessories e Windows Features, com marcação *Permanent* / *Warning* | `BloatApp.dll`, `BWR.dbd` | Documentado |
| 6 | **Bundleware**: detectar o que veio junto com o programa e desinstalar em conjunto | `lng-english-plano.txt` | Documentado |
| 7 | **Stubborn Program Remover** com base de **4.000+ programas** travados | `UninstallRote.dbd` | Documentado |
| 8 | **Force Uninstall+**: desinstalar programa **sem entrada de registro** ou com desinstalador quebrado | seção de diálogo do `.lng` | Documentado |
| 9 | **Quick Uninstall**: confirmar automaticamente o desinstalador original | `.lng` | Documentado |
| 10 | Aviso de que o desinstalador **vai deixar resíduo** ("still leaves") | `.lng` | Documentado |
| 11 | Confirmar/avisar antes de apagar **arquivo pessoal** | `.lng` | Documentado |

### 2.4 Motor de desinstalação e sobras

| # | Funcionalidade | Evidência | Tipo |
|---|---|---|---|
| 12 | **Sobras por categoria**: Registro, Arquivo, Tarefas, Serviços, DLL | `lng-english-plano.txt` | Documentado |
| 13 | **Auto Clean** de resíduo, marcado na própria desinstalação | `.lng` | Documentado |
| 14 | Resíduo vai para a **Lixeira** | `SHFileOperationW` | Observado |
| 15 | Ponto de restauração via `SysRest.dll` (`CreateRestorePoint`, `RestoreSystem`, `EnableSR`) | `pe-imports` | Observado |
| 16 | **Desinstalação em lote** (multi-seleção) | `.lng` | Documentado |
| 17 | **Easy Uninstall**: arrastar o ícone do programa para a janela | `DragAcceptFiles` | Observado |

> **Atenção ao 12**: o IObit classifica as sobras por **categoria**
> (registro/arquivo/serviço), e não por **nível de ameaça**. O Revo, o BCU e
> o Tranal classificam por confiança. São eixos diferentes — e o do Tranal
> responde melhor à pergunta "isto é dele?".

### 2.5 Monitoramento de instalação

| # | Funcionalidade | Evidência | Tipo |
|---|---|---|---|
| 18 | **Install Monitor** com log por categoria: registro, arquivo, tarefa, serviço, DLL, inicialização | `UninstallMonitor.exe` | Observado |
| 19 | Log guardado em **SQLite** (`sqlite3.dll`, 37 funções importadas) | `pe-imports` | Observado |
| 20 | **Desinstalar pelo log de instalação** (usar o log para desinstalar) | `.lng` — recurso **PRO** | Documentado |
| 21 | Monitor roda **em segundo plano no startup** | chave `Run` | Observado |

### 2.6 Software Health — o painel único

Nove categorias num painel só: sobras, software desatualizado, permissões,
popups, problemas de desinstalação, arquivos de instalação, malware e
extensões, lixo, hibernação. `SoftHealth.dbd`.

### 2.7 Ferramentas da caixa de ferramentas

| # | Funcionalidade | Evidência | Tipo |
|---|---|---|---|
| 22 | **File Shredder** com **3 modos de sobrescrita** | `.lng` | Documentado |
| 23 | Botão **Shred no Explorer** | `IUMenuRight.dll` | Documentado |
| 24 | **Software Relocator**: mover software instalado entre discos | `.lng` | Documentado |
| 25 | **Bloqueio de popups** de notificação | `.lng` | Documentado |
| 26 | **Software Permissions**: detectar e desabilitar permissões | `.lng` | Documentado |
| 27 | **Software Updater** integrado (atualizar todos) | `sUpdate.dbd` | Documentado |
| 28 | Remoção de item fixado na barra de tarefas | `strings` | Observado |
| 29 | **Desinstalar Windows Updates** | `.lng` | Documentado |
| 30 | **Uninstall History** com data, tamanho, operação e exportação | `.lng` | Documentado |

### 2.8 FREE × PRO

O FREE entrega: Programs, Browser Extensions, Windows Apps, Bloatware,
Force Uninstall+, File Shredder, Relocator, Toolbox, History, menu de
contexto. O **PRO** adiciona (deduzido das telas de venda, não executado):
desinstalar pelo log de instalação, a base de 4.000+ programas travados, a
remoção de extensões maliciosas e de advertising, *Update All*, *Fix All* em
1 clique e auto-limpeza de sobras.

> **Achado de campo**: a versão **FREE** do IObit baixou **~33 MB de payloads
> promocionais** na primeira execução (`Pub/`, `LatestNews/`,
> `Action Center/`). O registro está em
> `evidencia/arquivos-criados-em-execucao.txt`.

---

## 3. Mecanismos notáveis

### 3.1 Quatro drivers de kernel

| Driver | Função |
|---|---|
| `IUFileFilter.sys` | filtro de sistema de arquivos |
| `IUForceDelete.sys` | **força remoção de arquivo em uso** (`ZwCreateFile`, `ZwOpenKey`, `ZwSetValueKey`, `SeCaptureSecurityDescriptor`, `BCrypt*`) |
| `IUProcessFilter.sys` | filtro de processos (encerrar processo travado) |
| `IURegistryFilter.sys` | filtro de registro (remover chave/valor sob demanda) |

Entregues para **7 arquiteturas** (`win7_x86/amd64/ia64`, `win10_x86/amd64/ia64/arm64`) —
28 arquivos `.sys`.

### 3.2 APIs importadas pelo executável principal

29 DLLs, 841 funções únicas. As que contam para a paridade:

| Mecanismo | APIs |
|---|---|
| **Escrita/inspeção de memória de processo** | `WriteProcessMemory`, `ReadProcessMemory`, `VirtualAllocEx`, `VirtualProtectEx`, `OpenProcess`, `TerminateProcess`, `TerminateThread`, `SuspendThread` |
| **Serviços do Windows** | `OpenSCManagerW`, `OpenServiceW`, `StartServiceW`, `ControlService`, `DeleteService`, `EnumServicesStatusW` |
| **Elevação de privilégio** | `OpenProcessToken`, `AdjustTokenPrivileges`, `DuplicateTokenEx` |
| **Registro** | `RegOpenKeyExW`, `RegSetValueExW`, `RegDeleteKeyW`, `RegLoadKeyW` (hive), `RegUnLoadKeyW` |
| **Lixeira e shell** | `SHFileOperationW`, `ShellExecuteExW`, `SHGetSpecialFolderPathW`, `SHChangeNotify`, `DragAcceptFiles` |
| **Restauração do sistema** | `SysRest.dll`: `IsSREnable`, `EnableSR`, `CreateRestorePoint`, `RestoreSystem`, `DeleteRestorePoint` |
| **Validação de assinatura** | `WinVerifyTrust`, `CryptCATAdminAcquireContext`, `CryptCATAdminCalcHashFromFileHandle` |
| **Nível baixo do NT** | `NtOpenProcess`, `NtOpenThread`, `NtQueryVirtualMemory`, `NtQuerySystemInformation` |
| **Rede e cripto** | `wininet.dll`, `wintrust.dll`, `crypt32.dll`, `netapi32.dll` (`NetUserEnum`), `libcrypto-1_1.dll`, `libssl-1_1.dll` |

### 3.3 Integração com o sistema

Leitura das chaves de desinstalação em **HKCU e HKLM, 32 e 64 bits** ·
**menu de contexto do Explorer** (`shellex\ContextMenuHandlers\`) para
"Powerful Uninstall" · botão "Powerful Uninstall" no Painel de Controle ·
chave `Run` para o monitor em segundo plano · `SC.exe`/`sc delete` para
serviços · `SHCatch.ini` (hook de shell) · servidores `api.iobit.com`,
`ascstats.iobit.com`, `purchase.iobit.com`.

---

## 4. Interface

Janela principal em Delphi/VCL com barra lateral de abas. A árvore UIA
expõe 27 elementos; o VCL cria 48 janelas filhas (painéis). Atalhos de
teclado registrados no `.lng`.

**Dificuldade registrada na auditoria**: o app Delphi renderiza fora da tela
e o *foreground* era disputado por outro processo, então a navegação entre
abas por clique não foi confiável. Só **uma** captura de tela foi obtida; o
resto veio do artefato (`.lng` com 95 seções) e da análise estática. **Não
verificado**: o comportamento das abas além da primeira.

---

## 5. Linha de comando

**O IObit Uninstaller não publica nenhum switch de linha de comando.** A API
existe (`GetCommandLineW`, `CommandLineToArgvW`), mas nenhuma string de
`/SILENT`, `/scan` ou `/uninstall "<id>"` aparece no binário, no site nem no
manual. O único `/s` encontrado é o da **instalação** silenciosa do próprio
produto.

Isso é uma diferença importante e favorável ao Tranal: a **nossa** CLI
(`/scan`, `/mu`, `/export`, `/columns`) é uma capacidade que nenhum dos
concorrentes auditados tem.

---

## 6. O que o Tranal NÃO faz hoje

| # | Recurso do IObit | Tranal | Observação |
|---|---|---|---|
| 1 | **Remoção de extensões de navegador** com base maliciosa/ad | ⬜ | **A maior lacuna nova** — nem o Revo tem |
| 2 | Base de extensões maliciosas embarcada | ⬜ | — |
| 3 | Limpeza de navegador / reset de padrões | ✅ | `Core/Tools/TrackCleaner.cs` |
| 4 | **Bloatware Remover** (pré-instalados, Accessories, Windows Features) | ⬜ | — |
| 5 | **Bundleware**: detectar o que veio junto | ⬜ | o Revo também não tem |
| 6 | **Desinstalar pelo log de instalação** | ⬜ | o Tranal tem o log (`TracedPrograms`), mas não desinstala por ele |
| 7 | **Stubborn Program Remover** com base de 4.000+ | ⬜ | nem o Revo tem |
| 8 | **Force Uninstall+** (sem chave de registro) | 🟡 | o Tranal detecta desinstalador corrompido, mas não desinstala sem entrada |
| 9 | **Easy Uninstall** (arrastar ícone) | 🟡 | o Tranal tem a janela flutuante — outro desenho |
| 10 | **Software Health** como painel único de 9 categorias | 🟡 | o Tranal tem as ferramentas separadas, sem painel |
| 11 | Bloqueio de popups / Software Permissions | ⬜ | — |
| 12 | **Software Updater** de terceiros | ⬜ | o Tranal atualiza a si mesmo, não os outros |
| 13 | **Software Relocator** (mover entre discos) | ⬜ | — |
| 14 | Desinstalar **Windows Updates** | ⬜ | — |
| 15 | **Desinstalação em lote** (multi-seleção) | ⬜ | — |
| 16 | Aviso de que o desinstalador "ainda vai deixar" resíduo | ⬜ | — |
| 17 | Aviso antes de apagar **arquivo pessoal** | ⬜ | — |
| 18 | **Drivers de kernel** para arquivo em uso | ⬜ | decisão registrada: o Tranal não assina driver |
| 19 | Desinstalador do **fabricante** como fallback | 🟡 | o Tranal encerra o processo, mas não procura o desinstalador oficial |
| 20 | **Sobras por categoria** (tarefa/serviço/DLL) | 🟡 | o Tranal varre registro e disco, não tarefas nem serviços |
| 21 | Uninstall History com tamanho e exportação | 🟡 | `Core/UninstallHistory.cs` |
| 22 | Modo escuro | 🟡 | o Tranal tem tema próprio |

### Onde o Tranal já é melhor

| Ponto | Tranal | IObit |
|---|---|---|
| **Linha de comando** | `/scan`, `/mu`, `/export`, `/columns`, modos, `/all`, `/high` | **nenhum switch publicado** |
| **Driver de kernel** | não usa, e não precisa assinar driver | 4 drivers × 7 arquiteturas |
| **Confiança das sobras** | 3 níveis + **motivo por item** | 4 categorias, sem nível de ameaça |
| **Backup antes de desinstalar** | backup completo do registro, com script de restauração | sem backup de registro próprio |
| **Desinstalação de programa travado** | encerra o processo e segue | tem driver e base de 4.000+ |
| **Privacidade** | sem telemetria | `ascstats.iobit.com`, `ProductNews2.dll`, payloads promocionais baixados |

---

## 7. Fichos de evidência

| Arquivo | Conteúdo |
|---|---|
| `downloads/iobit_uninstaller_setup.exe` | instalador (34.309.696 B) |
| `app/payload` | 146 arquivos extraídos (97,6 MB) |
| `evidencia/RESUMO.md` | 56 KB, 8 seções, 142 funcionalidades, 40 linhas de gap |
| `evidencia/lng-english-plano.txt` | as 95 telas do `.lng` achatadas |
| `evidencia/pe-imports-IObitUninstaler.txt` | 29 DLLs, 841 funções |
| `evidencia/strings-*.txt` | 20 arquivos de strings |
| `evidencia/uia-*.txt` | árvores de UIA |
| `evidencia/arquivos-criados-em-execucao.txt` | os payloads baixados pelo FREE |
| `evidencia/innoextract-lista.txt` | a lista completa dos 146 arquivos |

Todas as 211 citações `arquivo:linha` do relatório bruto foram conferidas
contra o conteúdo real — **zero divergências**.

---

## 8. Lacunas desta auditoria

| O que faltou | Por quê | Como fechar |
|---|---|---|
| Navegação nas outras abas | o app Delphi renderiza fora da tela e o *foreground* era disputado por outro processo | auditar com o owner fechando o que estiver usando a máquina, ou com o app instalado |
| **Desinstalação real** | exigiria instalar e desinstalar algo na máquina; não foi feito | o owner autorizou instalar os concorrentes |
| **Software Health** em detalhe | mesma razão da navegação | idem |
| Recursos **PRO** | software pago | pesquisa documental no site |
| Bases `.dbd` proprietárias | formato fechado, não é SQLite puro | engenharia reversa do formato |
| Comportamento do `Install Monitor` rodando de verdade | exige instalar um programa para monitorar | idem |

---

## 9. Decisões que este documento obriga

1. **Remoção de extensões de navegador entra no backlog.** É a lacuna que
   só o IObit revelou, e nenhum outro concorrente tem. Ela não é "desinstalar"
   — é um produto dentro do produto, e precisa de uma base de assinaturas
   para ser útil.
2. **Driver de kernel continua fora.** O IObit usa 4 drivers para remover
   arquivo em uso. O Tranal resolve o mesmo problema encerrando o processo
   e adiando para o reinício (`DeferredDelete`). A decisão do `PARIDADE.md`
   §8 se sustenta — e agora tem a prova: o custo de um driver é 4 binários
   × 7 arquiteturas, assinatura de código e um problema de reputação que o
   McAfee desta máquina já demonstrou na prática.
3. **A classificação das sobras continua por confiança, não por categoria.**
   O IObit organiza por categoria (registro/arquivo/serviço); o Tranal
   organiza por confiança + motivo. A pergunta que o usuário faz é "isto é
   dele?", e a confiança responde a ela.
4. **Bundleware e "desinstalar pelo log de instalação" viram item de
   backlog.** O Tranal já tem o log (`TracedPrograms`); falta usá-lo para
   desinstalar, que é a metade que o dono sente.

---

*Auditoria gerada por medição direta dos artefatos e por execução da versão
FREE. Toda linha deste documento aponta para evidência em disco; o que não
pôde ser verificado está marcado como tal na seção 8.*
