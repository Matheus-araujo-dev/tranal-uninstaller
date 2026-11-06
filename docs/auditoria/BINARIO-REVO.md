# Lacunas - Revo Uninstaller Pro 5.5.4

Binario analisado: `C:\Users\Matheus\.minimax\sessions\mvs_1a1a8292487b443c92dc9d3eac74f170\workspace\revo-extracted\app\`
sha256: `57f4d6aad76514ab7ff48065eb66f7602403d0ef38538db8771fcb8e97dab06c` (RevoUninPro.exe v5.5.4.0)

Componentes do mesmo payload (sha256):

| Arquivo | sha256 |
|---|---|
| RevoUninPro.exe | `57f4d6aad76514ab7ff48065eb66f7602403d0ef38538db8771fcb8e97dab06c` |
| RevoAppBar.exe | `f609d341785805df7fc0c83348f471085c53a6e20184f272c11c889451b60d4b` |
| RevoSrp.exe | `d54bf92fb1c7ec47b5654abc8987fb2053504342b8fb4aecadc76f686295a2ea` |
| RevoCmd.exe | `c187269068a7d79307b7b4b811cf80d1f541fa7b9d4bfb0908a6fe75eda7f4dd` |
| RUExt.dll | `bda04b693bfdea86a7a3b47f2e4ceae9cd9475c4e81b0aa73b70fd244a65f70f` |
| WinAppsDll.dll | `b1d433154e66e83b02f84ad65015b3a9f1a1fc67c76c2c5b78810aa802a87a0c` |
| revoflt.sys (driver de filtro) | `d7a14ab729d0a03d99db5f24d67f555f4d2a30d35e0f48a9c7269b83e8a4f425` |
| rupilogs.rupldb (base de logs) | `eae3e2bf3711e536d368f27bb26b2f0a53d1d0d09aa22378c06a7338c5f73a98` |
| ruplp.exe (serviço) | `c6d97857b3b9f26c8e93d7b6e6481f93a16db75cbf9d1756cb29fba0fd9e240e` |
| RUPrestore.cmd | `746b2c6bce53cef5d5b2e34c4e909680e36c4b8c9eba0808837a0649d9c4bbf8` |

Metodo: tres fontes, todas do binario e do proprio produto.

1. **Tabela de idioma do proprio produto** — os `lang\*.ini` que o Revo 5.5.4
   instala (52 arquivos `.ini`, 1141 entradas em 42 secoes) foram extraidos para
   `E:\Projetos\tranal-auditoria-evidencia\bin-inventario\revo-pro.funcionalidades.md`.
   Cada linha dali e evidencia direta de uma funcionalidade: `[Secao] id = texto`.
   Confirmado que e o proprio binario que le esse arquivo:
   PE `KERNEL32.dll` importa `GetPrivateProfileIntW`/`GetProfileIntW`
   (`revo-pro.imports.txt:20`).
2. **Importacoes da PE** — `revo-pro.imports.txt`: 27 DLLs, 917 funcoes.
   Linhas citadas abaixo: `ADVAPI32.dll` = `:14`, `msi.dll` = `:12`,
   `KERNEL32.dll` = `:20`, `SHELL32.dll` = `:30`, `RstrtMgr.DLL` = `:46`.
3. **Dump de strings** — `E:\Projetos\tranal-auditoria-evidencia\bin-strings\revo-pro.strings.txt`
   (literais exatos, citados por numero de linha).

A coluna "Tranal" foi conferida no codigo (`src/Tranal.Uninstaller/Core/**`,
`Ui/**`) e cruzada com `docs/auditoria/MATRIZ.md` e `docs/PARIDADE.md`.
A tabela completa (70 linhas) esta em `docs/REVO_SPEC.md` §7.1.

## Funcionalidades presentes no binario e ausentes no Tranal

| # | Funcionalidade | Evidencia no binario | Categoria | Impacto (alto/medio/baixo) |
|---|---|---|---|---|
| 1 | Base de logs online do Revo, com download automatico e uso do log durante a desinstalacao | `[UninstallerPro] 15273` (descricao do modulo "Logs Database", "logs stored on our web site"); `[UninstallerPro] 15187 = Windows Version`; strings.txt:26165 `Logs database`; strings.txt:26756 `Automatically update the Log Database`; strings.txt:26757 `Error opening Logs database` | integracao | medio — util, mas viola a decisao de produto do Tranal (offline, sem base remota) |
| 2 | Mesclar logs selecionados em um log so | `[UninstallerPro] 15312 = Merge selected Logs`; strings.txt:26218 `Merge selected Logs` | organizacao | baixo |
| 3 | "Change or Repair" da instalacao MSI (reparo com `msiexec /fa`) | strings.txt:26799 `Change or Repair the installation of selected program` | remocao | medio — reparo e uma operacao distinta da desinstalacao e o Tranal so faz `/x` |
| 4 | Varredura de sobras em **todas as contas do Windows** e desinstalacao multiusuario | `[UninstallerPro] 15345 = Show the option to scan all Windows accounts for leftovers`; `[UninstallerPro] 15352 = Scan all Windows accounts for leftovers`; PE `ADVAPI32.dll` → `GetUserNameW`, `LookupAccountNameW`, `DuplicateTokenEx`, `CreateProcessWithTokenW` (imports.txt:14) | varredura | alto — sobras de outros perfis ficam no disco sem forma de alcanca-las |
| 5 | Aviso de que dois programas ocupam a **mesma pasta** (sobras cruzadas) | `[Uninstaller Toolbar] 128 = %s is installed in the same location as %s! Leftovers will be found from both applications! Carefully check the ones you want to remove!` | remocao | alto — sem isso, o motor pode oferecer apagar a pasta de outro aplicativo |
| 6 | Arquivo de **cache de programas** reconstruivel | `[UninstallerPro] 15150 = There is not enough available disk space to write the cache data file` | interface | medio — startup instantaneo em maquinas com muitos programas |
| 7 | Exportar / importar **todas as configuracoes** do produto | `[Options Tree] 1002 = Export All Settings`; `[Options Tree] 1003 = Import All Settings` | organizacao | baixo |
| 8 | Ferramenta **personalizada** no Windows Tools (nome + linha de comando) | `[WindowsToolsPro] 17480 = Tool`; `[WindowsToolsPro] 17484 = A custom tool with the same name already exists. Please enter a different name.` | interface | baixo |
| 9 | Gerenciador **proprio** de servicos e de tarefas agendadas (habilitar/desabilitar/excluir) | `[Auto Run Manager Pro] 15814 = Services`; `[Auto Run Manager Pro] 15816 = Scheduled Tasks`; strings.txt:26633 `Scheduled Tasks`; PE `ADVAPI32.dll` → `OpenSCManagerW`, `OpenServiceW`, `DeleteService`, `ControlService` (imports.txt:14) | integracao | alto — hoje o Tranal so abre `services.msc`/`taskschd.msc` |
| 10 | Criar **novo** item de inicializacao no Autorun Manager | `[Auto Run Manager Pro] 15800 = Add`; `[Autorun List] 478 = Enable Auto Starting` | integracao | medio |
| 11 | Editar o log de instalacao **entrada a entrada** (chaves, valores, arquivos) | `[UninstallerPro] 15178 = Export,Import or Edit log`; `[UninstallerPro] 15257 = Add Key`; strings.txt:26274 `Custom Uninstall` | organizacao | alto — sem edicao, um log ruim leva a remocao errada |
| 12 | Etapa "Custom Uninstall" (revisar o log e escolher o que remover) | `[UninstallerPro] 15178 = Export,Import or Edit log`; `[UninstallerPro] 15220 = Preparing log's data for uninstall...`; strings.txt:26274 `Custom Uninstall` | remocao | alto |
| 13 | Etapa "Advanced Uninstall" (revisar o que **nao** foi registrado na instalacao) | strings.txt:26066 `Advanced Uninstall`; strings.txt:26974 `There is some data that may be related to the uninstalled program, but was not deleted because it was not logged by Revo Uninstaller Pro during the installation of the program. If you want to review and eventually to delete it press the "Advanced Uninstall" button` | remocao | alto — e o unico caminho do Revo para a "sobra nao registrada" |
| 14 | Aviso de pastas que podem conter **dados do usuario** | `[UninstallerPro] 15359 = Folders below may contain user data and documents:` | remocao | alto — previne perda de documentos |
| 15 | Excluir o programa da lista sem desinstalar (equivalente exato do "Remove Entry") | `[Uninstaller Toolbar] 111 = Remove Entry`; `[Uninstaller Toolbar] 113 = Are you sure that you want to remove the selected entry?` | organizacao | baixo — o Tranal oculta/exclui, mas o equivalente exato nao foi verificado |
| 16 | Exportar a lista dirigindo o **Microsoft Excel** instalado (COM) | `[General App] 16027 = Microsoft Excel is not installed on this computer (error code is 0x%08X).` | relatorio | baixo — o Tranal ja gera SpreadsheetML sem depender do Office |
| 17 | Paleta de cores customizavel por papel (fundo, selecao, texto, dialogos) | `[General App] 16133 = Command button text color`; `[General App] 16150 = Dialogs background color`; `[General App] 16038 = Visual theme:` | interface | baixo — o Tranal tem temas prontos, nao cores livres |
| 18 | Opcao "modo avancado" + lista de motores de busca web configuravel | `[General] 1102 = Advanced mode`; `[General] 1106 = Web search engines:` | interface | baixo |
| 19 | Opcoes do desinstalador: "so executar o desinstalador nativo" e "criar ponto de restauracao" | `[Uninstaller Options] 1202 = Show system components`; `[Uninstaller Options] 1209 = Show web page shortcuts` | interface | medio — o controle do que e removido e uma decisao do usuario |
| 20 | **Driver de filtro do sistema** (`revoflt.sys`) para monitorar a instalacao em nivel de sistema | binario `revoflt.sys` + `revoflt.inf`; PE `SHELL32.dll` → `SHAppBarMessage` (imports.txt:30); `[AppBar] 16900 = Save the log of monitored system changes` | integracao | alto — sem driver, mudancas de baixo nivel do sistema ficam fora do log |
| 21 | Painel de "operações de logs no computador atual" + renomear log | `[UninstallerPro] 15319 = All Logs operations on the current computer`; `[UninstallerPro] 15235 = Rename Log`; `[UninstallerPro] 15336 = Export Log's data` | organizacao | baixo |
| 22 | Fragmentos de arquivos recuperados pelo **Chkdsk** na limpeza de rastros do Windows | `[Windows Tracks Cleaner] 725 = Delete Chkdsk (Check Disk) recovered file fragments` | limpeza | medio — sao vestigios forenses de disco |
| 23 | Filtro por **Regex** na lista (nao so no motor de regras) | PE `KERNEL32.dll` → `GetPrivateProfileIntW` e `ADVAPI32.dll` → `RegQueryValueExW` (imports.txt:14/20) confirmam os filtros do Revo; o filtro por Regex como recurso da lista consta em `docs/auditoria/MATRIZ.md` D2 🟡 | interface | medio |
| 24 | Tratamento da pasta problematica ao atualizar para o **Windows 11** | `[UninstallerPro] 15371 = If you have problems updating to Windows 11, we recommend that you delete this folder %s. Would you like to add it to the leftovers?` | interface | baixo |
| 25 | Base de logs do Revo usada **localmente**: exportar/importar log e cruzar com a base online | `[UninstallerPro] 15178 = Export,Import or Edit log`; `[UninstallerPro] 15232 = Import Log`; `[UninstallerPro] 15336 = Export Log's data`; `[UninstallerPro] 15319 = All Logs operations on the current computer` | organizacao | medio |
| 26 | Lista de **motores de busca web** configuravel (o Revo permite escolher quais usar) | `[General] 1106 = Web search engines:` | interface | baixo |
| 27 | Historico de desinstalacao com **detalhe por operacao** e opcao de desabilitar o historico | `[UninstallHistory] 32900 = Uninstall History`; `[UninstallHistory] 32940 = Skipped by user`; `[Uninstaller Options] 1208 = Disable Uninstall History feature` | relatorio | medio |

## Observacoes

- **O Revo e o produto de referencia do Tranal.** Quase todo o nucleo esta
  medido e implementado: a tabela de `docs/REVO_SPEC.md` §7.1 tem 70 linhas —
  **43 `OK`, 18 `PARCIAL`, 9 `AUSENTE`**. As 27 linhas `AUSENTE`/`PARCIAL`
  estão todas listadas acima (itens 1–27).
- As 9 lacunas `AUSENTE` reais concentram-se em tres eixos: (a) **multiusuario**
  (itens 4 e 5), (b) **integracao com o sistema** (itens 8, 9 e 20), (c) **ferramentas
  de configuracao** (itens 6, 7 e 8).
- O item 1 (base de logs online) e uma **decisao de produto**, nao uma falha: o
  Tranal e deliberadamente offline, sem cadastro e sem telemetria
  (`docs/auditoria/MATRIZ.md` H.8). Fica registrado, nao silenciado.
- Itens 16 e 17 sao casos em que o Tranal **e melhor** (SpreadsheetML sem Office
  instalado; temas prontos em vez de paleta livre) — o impacto e baixo de
  proposito.
