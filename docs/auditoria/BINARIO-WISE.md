# Lacunas - Wise Program Uninstaller

Binario analisado: `workspace/auditoria/wise/app/app/WiseProgramUninstaller.exe` (13.273.496 B)  sha256: `4f7da8050ad12ed500902a18f886b12404ad24f64e719bb819675bc048c64935`
Metodo: inventario estatico do PE32 (Delphi/Embarcadero, **empacotado com 7-Zip**) — classes RTTI, rotulos de interface, **recursos de formulario Delphi** (173 entradas, das quais 22 formularios `TFRM*`), identificadores (inclui as **chaves de legenda que estao dentro do executavel**) e dump bruto de strings; o `UnMonitor.exe` foi separado como binario e lido a parte. Fontes: `bin-inventario/wise.inventario.md`, `bin-inventario/wise.recursos.txt`, `bin-inventario/wise.identificadores.txt`, `bin-inventario/wise-monitor.identificadores.txt`, `bin-strings/wise.strings.txt`.
A coluna `#` abaixo e o **numero da linha na tabela de funcionalidades** de `WISE.md` (secao 2). Lista exata: todas as linhas `AUSENTE` e `PARCIAL` da secao 2, uma a uma.

## Funcionalidades presentes no binario e ausentes no Tranal

| # | Funcionalidade | Evidencia no binario | Categoria | Impacto (alto/medio/baixo) |
|---|---|---|---|---|
| 2 | Visao separada de **Apps da Area de Trabalho** (Win32) e de **Apps do Metro** | `wise.identificadores.txt:70-73` -> `FrmMain.BtnForce.Caption` / `FrmMain.BtnNormal.Caption` / `FrmMain.btnLog.Caption` / `FrmMain.btnuninstall.Caption`; `wise.recursos.txt:165` -> `TFRMWPUMAIN/0` | interface | medio |
| 4 | Lista separada de **"Componentes do Sistema"** | `wise.recursos.txt:167` -> `TMPTYPES/2052` | interface | medio |
| 6 | **Desinstalacao Forcada** — modo explicito no painel principal | `wise.identificadores.txt:70` -> `FrmMain.BtnForce.Caption` | remocao | alto |
| 7 | "Modificar" — reaproveita o instalador do programa | `wise.recursos.txt:144` -> `TFRMBEFOREUNISTALL/0` | remocao | medio |
| 8 | "Remover Entradas" — tira o programa da lista **sem** desinstalar | `wise.recursos.txt:144` -> `TFRMBEFOREUNISTALL/0` | remocao | medio |
| 9 | Aviso para **nao usar a Forcada antes da Segura** | `wise.identificadores.txt:70` -> `FrmMain.BtnForce.Caption` | interface | baixo |
| 10 | **Desinstalacao em lote** (N programas) | `wise.recursos.txt:144` -> `TFRMBEFOREUNISTALL/0` | automacao | alto |
| 12 | Extensao de navegador, com aviso de **sincronizacao** | `wise.recursos.txt:151` -> `TFRMPLUGDELCOMP/0` | interface | baixo |
| 13 | Aviso quando o **navegador esta em execucao** | `wise.recursos.txt:145` -> `TFRMCATCH/0` | interface | medio |
| 14 | Recusa de **desinstalar a si mesmo** | `wise.recursos.txt:165` -> `TFRMWPUMAIN/0` | interface | baixo |
| 15 | Aviso de que um programa **nao aceita** desinstalacao silenciosa | `wise.recursos.txt:144` -> `TFRMBEFOREUNISTALL/0` | interface | medio |
| 16 | Aviso de **desinstalacao anterior em andamento** | `wise.recursos.txt:144` -> `TFRMBEFOREUNISTALL/0` | interface | baixo |
| 24 | **Expiracao do backup de residuos** (apaga os com mais de 1 mes) | `wise.recursos.txt:158` -> `TFRMRESTORE/0` | recuperacao | medio |
| 25 | Tela de **Restauracao do Sistema**, com "Restaurar" e "Apagar" | `wise.recursos.txt:158` -> `TFRMRESTORE/0` | recuperacao | alto |
| 27 | Remocao de **ponto de restauracao** selecionado | `wise.recursos.txt:158` -> `TFRMRESTORE/0` | recuperacao | medio |
| 28 | **Historico de desinstalacoes** com a coluna "Residuos" | `wise.recursos.txt:163` -> `TFRMUNLOG/0`; `wise.identificadores.txt:212` -> `TfrmUnlog.head.ItemName` | relatorio | alto |
| 29 | **Botao "Recuperar"** no historico | `wise.strings.txt:71271` -> `TfrmUnlog.recover`; `:71272` -> `Recovered`; `:71273` -> `TfrmUnlog.Recovered` | recuperacao | alto |
| 32 | **Desinstalacao Personalizada** — varre por palavra-chave e deixa explorar a pasta | `wise.recursos.txt:162` -> `TFRMUNLESSDEL/0` | varredura | alto |
| 33 | Botao **"Explorar"** para apagar o que for encontrado | `wise.recursos.txt:162` -> `TFRMUNLESSDEL/0` | interface | medio |
| 35 | Deteccao da desinstalacao feita por **outro** desinstalador | `wise.recursos.txt:144` -> `TFRMBEFOREUNISTALL/0` | integracao | medio |
| 37 | Software Health com **4 categorias**: sobras, arquivos redundantes, permissoes, notificacoes | `wise.recursos.txt:146` -> `TFRMCLEANPAGE/0` | organizacao | alto |
| 38 | "Analyze" que varre **todos** os aplicativos de uma vez | `wise.recursos.txt:146` -> `TFRMCLEANPAGE/0` | organizacao | medio |
| 39 | "Stop" para **interromper** a varredura | `wise.recursos.txt:146` -> `TFRMCLEANPAGE/0` | interface | baixo |
| 41 | Bloqueio de **notificacoes pop-up** e ads unwanted (dentro do Software Health) | `wise.strings.txt:62862` -> `"defaultname": "Popup notifications",` | integracao | alto |
| 43 | Dialogo de **permissoes** com "Action" por plug-in | `wise.identificadores.txt:74` -> `FrmPermissionsDialog.lvlist.Option`; `wise.recursos.txt:150` -> `TFRMPERMISSIONSDIALOG/0` | interface | medio |
| 44 | Dialogo de **lixeira** com tamanho por aplicativo | `wise.recursos.txt:160` -> `TFRMTRASDIALOG/0` | interface | medio |
| 45 | Dialogo de **notificacoes** com remetente e tipo | `wise.recursos.txt:149` -> `TFRMNOTIFYDIALOG/0` | interface | medio |
| 46 | Registro de "**ultima analise**" | `wise.recursos.txt:146` -> `TFRMCLEANPAGE/0` | interface | baixo |
| 47 | Gerenciador de extensoes, com **abas por navegador** | `wise.strings.txt:33129` -> `btVivaldi`; `:33130` -> `btYandex` | remocao | alto |
| 48 | Base de **11 navegadores** com caminho de perfil | `wise.strings.txt:58193` -> `"YandexPath": {`; `:62853` -> `"VivaldiPath": {`; `:62854` -> `"key": "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Vivaldi"`; `:62856` -> `...Uninstall\\Comodo Dragon"`; `:62858` -> `...Uninstall\\SogouExplorer"`; `:62873` -> `%LOCALAPPDATA%\\Yandex\\YandexBrowser\\User Data\\Default\\Preferences`; `:62874` -> `%LOCALAPPDATA%\\Vivaldi\\User Data\\Default\\Preferences`; `:62878` -> `%APPDATA%\\SogouExplorer\\Webkit\\Default\\Preferences` | remocao | alto |
| 49 | Leitura do arquivo de **preferencias** do perfil do navegador | `wise.strings.txt:62873` -> `%LOCALAPPDATA%\\Yandex\\YandexBrowser\\User Data\\Default\\Preferences`; `:62878` -> `%APPDATA%\\SogouExplorer\\Webkit\\Default\\Preferences` | remocao | medio |
| 50 | Bloqueio de notificacoes pop-up (regra de limpeza `PopNotify`) | `wise.strings.txt:62861` -> `"tranname": "PopNotify",`; `:62885` -> `"type": "PopNotify",`; `:62892` -> `"type": "PopNotify",` | integracao | alto |
| 52 | **Base embutida de regras**: **595 perfis** nomeados e **2.379 caminhos** | `wise.strings.txt:62853-62892` (bloco JSON de regras: `tranname`, `defaultname`, `type`, `key`, `path0`); `wise.inventario.md:1248` -> `Rule` - Motor de regras | organizacao | alto |
| 53 | Tipos de regra: **File**, **RegKey**, **RegValue**, **Reg**, **Ini**, **PopNotify** | `wise.strings.txt:62885` -> `"type": "PopNotify",`; `:62856` -> `"key": "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Comodo Dragon"` | organizacao | alto |
| 54 | Perfis de conhecidos: Epic Games Launcher, Discord, Slack, AnyDesk, WeChat Cache, Bilibili, Razer Software, Adobe Acrobat… | `wise.strings.txt:62853-62892` (bloco JSON de perfis nomeados) | organizacao | alto |
| 55 | Tabela de caminhos de registro por app, com marcacao `(Reg)`: **48 entradas, 14 com `(Reg)`** | `wise.strings.txt:62854` -> `"key": "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Vivaldi"`; `:62858` -> `...Uninstall\\SogouExplorer"` | organizacao | medio |
| 58 | Colunas: Nome, Publicador, Tamanho, Data de instalacao, Versao, **Ultimo uso** | `wise.recursos.txt:165` -> `TFRMWPUMAIN/0`; `wise-monitor.identificadores.txt` -> `uUninstallHelper.TUseAssist` | interface | medio |
| 59 | Contagem e **tamanho total** dos programas | `wise.inventario.md:1244` -> `Size` - Mede tamanho em disco; `wise.recursos.txt:165` -> `TFRMWPUMAIN/0` | interface | medio |
| 60 | Datas **relativas** no historico ("Dentro de 1 semana", "1 ano atras") | `wise.recursos.txt:163` -> `TFRMUNLOG/0` | interface | medio |
| 62 | Detalhes da entrada: Path, Uninstall Command, Details, **Trust/Unknown/App site** | `wise.recursos.txt:165` -> `TFRMWPUMAIN/0` | interface | medio |
| 66 | Ativacao de **licenca PRO** online, com validade e renovação | `wise.recursos.txt:156` -> `TFRMREG/0` | interface | baixo |
| 67 | **Avaliacao** pelo usuario, com "Software Recomendado" | `wise.recursos.txt:153` -> `TFRMRATE/0`; `:155` -> `TFRMRATELIST/0` | interface | baixo |
| 68 | "Traduzir pro…" — convida a traduzir | `wise.recursos.txt:147` -> `TFRMFEEDBACK/0` | interface | baixo |
| 69 | Programa de **melhoria da experiencia** do usuario (opt-in) | `wise.recursos.txt:147` -> `TFRMFEEDBACK/0` | interface | baixo |
| 70 | Central de **suporte, forum e novidades** | `wise.recursos.txt:168` -> `TWISESUPPORTFORM/0` | interface | baixo |
| 72 | Menu de edicao: Cut, Copy, Paste, Delete, Select all, Undo | `wise.recursos.txt:165` -> `TFRMWPUMAIN/0` | interface | baixo |
| 73 | Tela **"My View"** (visao do usuario) | `wise.recursos.txt:148` -> `TFRMMYVIEW/0` | interface | medio |
| 74 | Dialogo **"Catch"** (captura do estado do sistema antes da acao) | `wise.recursos.txt:145` -> `TFRMCATCH/0` | interface | medio |
| 75 | Formulario de **feedback** | `wise.recursos.txt:147` -> `TFRMFEEDBACK/0` | interface | baixo |
| 76 | **Lista de avaliacao** e edicao da avaliacao | `wise.recursos.txt:153` -> `TFRMRATE/0`; `:154` -> `TFRMRATEEDIT/0`; `:155` -> `TFRMRATELIST/0` | interface | baixo |
| 77 | **Renovacao** de licenca PRO | `wise.recursos.txt:157` -> `TFRMRENEW/0` | interface | baixo |
| 78 | **Pagina de desinstalacao** dedicada (separada do dialogo) | `wise.recursos.txt:161` -> `TFRMUNINSTALLPAGE/0` | interface | baixo |
| 79 | **Desinstalar componente de plugin** | `wise.recursos.txt:151` -> `TFRMPLUGDELCOMP/0` | remocao | medio |
| 80 | Formulario de **suporte ao cliente** Wise | `wise.recursos.txt:168` -> `TWISESUPPORTFORM/0` | interface | baixo |
| 82 | Acesso ao **gerenciador de credenciais** do Windows | `wise.identificadores.txt:228` -> `Winapi.Security.Credentials` | integracao | baixo |
| 83 | Deteccao de **geolocalizacao** | `wise.identificadores.txt:227` -> `Winapi.Devices.Geolocation` | integracao | baixo |
| 84 | **Client HTTP** generico para rede | `wise.identificadores.txt:194` -> `System.Net.HttpClient`; `:199` -> `System.Net.URLClient` | integracao | baixo |
| 85 | Modelo de sobra **persistido** (`uUninstallHelper.TUninstallObject`) pelo processo separado | `wise-monitor.identificadores.txt:223` -> `TDictionary<System.string,uUninstallHelper.TUninstallObject>` | recuperacao | alto |

## Observacoes

- **A base de regras e a maior lacuna do Wise.** As 595 linhas do documento
  canonico (numero que vem de contagem do dump) nao sao um volume que o Tranal
  precise copiar: o que importa e o **formato**. O bloco JSON em
  `wise.strings.txt:62853-62892` mostra o schema completo — `tranname`,
  `defaultname`, `type`, `key`, `path0..N` — e o `Core/Rules/RuleEngine.cs` do
  Tranal ja suporta a leitura. Faltam as regras.
- **O navegador e a segunda maior lacuna**, e a mais barata: sao seis
  navegadores, cada um com um caminho de perfil e um arquivo `Preferences`
  (ou `extensions.json`). O Tranal ja tem o codigo pronto para quatro.
- **Fechadas nesta rodada por existirem no codigo do Tranal:** permissoes por
  tipo (`Core/Management/AppPermissionManager.cs`), varredura de sobras
  (`Core/UninstallSupport.cs`), backup de residuos
  (`Core/Recovery/LeftoverBackup.cs`), recuperacao
  (`Core/Recovery/RecoverOperation.cs`), historico
  (`Core/UninstallHistory.cs`), ponto de restauracao
  (`Core/UninstallSupport.cs:1210`), Store/MSIX
  (`Core/Modules/WindowsApps.cs`), silencioso (`Core/QuietUninstall.cs`),
  menu de contexto (`Core/Topmost/ExplorerContextMenu.cs`), exportacao
  (`Core/Exporters.cs`), skins (`Theming/ThemeManager.cs`), portatil
  (`Core/PortableMode.cs`), autoatualizacao (`Core/Updates/AutoUpdateClient.cs`),
  Software Health (`Core/Management/SoftwareHealth.cs`).
- **Divergencias intencionais:** as linhas 82 (credenciais), 83
  (geolocalizacao) e 66/77 (licenca) nao sao backlog — o Tranal nao
  comercializa e nao coleta dados do usuario. Aparecem aqui so para registro.
- **Limitacao do metodo:** por estar **empacotado com 7-Zip**, o conteudo
  interno do executavel do Wise nao e integralmente extraivel por estatica.
  Ausencia de literal **nao prova** ausencia de recurso; as linhas marcadas
  como confirmadas por formulario sao as de confianca mais alta.
