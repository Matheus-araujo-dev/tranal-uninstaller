# Lacunas - Ashampoo UnInstaller Pro 27 (27.0.1)

Binario analisado: `C:\Users\Matheus\.minimax\sessions\mvs_1a1a8292487b443c92dc9d3eac74f170\workspace\auditoria\ashampoo\downloads\ashampoo_uninstaller_pro_27.exe`
sha256: `08a36652800b6b608041d6a445c38079c66239b2751fa4041e07132b9dc9184d` (49.098.296 bytes)

Metodo: (a) identificadores/RTTI e inventario extraidos do PE em
`E:\Projetos\tranal-auditoria-evidencia\bin-inventario\ashampoo-instalador.identificadores.txt`
e `ashampoo-instalador.inventario.md`; (b) dump de strings em
`E:\Projetos\tranal-auditoria-evidencia\bin-strings\ashampoo-instalador.strings.txt`
(168.998 literais unicos); (c)PE imports do instalador.

## Limitacao da medicao (ler antes da tabela)

O executavel analisado **e o instalador**, nao o aplicativo. O `UI27.exe` real
esta **comprimido em LZMA dentro do instalador**, e o `innoextract 1.9` recusa o
arquivo:

```
Unexpected setup loader revision: 2
```

Consequencias honestas:

- As classes e metodos do **aplicativo** Ashampoo (`UI27.exe`) **nao sao
  acessiveis**: nao ha tabela de RTTI do produto, nao ha dump de strings do
  produto, nao ha imports do produto.
- As 829 classes RTTI detectadas (`ashampoo-instalador.inventario.md:9`) sao
  **do runtime do Inno Setup** (`TApplication`, `TCompressedBlockReader`,
  `TCustomDecompressor`, `TChaCha20Context`, `TSetupEncryptionKey`…), nao do
  Ashampoo UnInstaller.
- Portanto, **toda** funcionalidade do aplicativo Ashampoo esta marcada como
  `nao confirmado no binario` na tabela de `docs/auditoria/ASHAMPOO.md` §2.1–§2.6.
  O que la aparece como evidencia documental (manual/site) continua valendo como
  documentacao — mas **nao** e prova de binario, e a coluna "Evidencia no
  binario" diz isso explicitamente, linha a linha.
- O que **e** provavel do binario: a superficie do instalador (runtime do Inno
  Setup, manifesto, chaves de registro gravadas, manifesto de DLLs, deteccao de
  sistema) e a identidade do produto (versao, empresa, titulo, versao do setup).
  Isso virou a secao §2.6.1 do `ASHAMPOO.md` (itens 67–76).

### Identidade confirmada no binario

| Item | Evidencia |
|---|---|
| Empresa | `ashampoo-instalador.strings.txt:6` `Ashampoo GmbH & Co. KG` |
| Titulo | `ashampoo-instalador.strings.txt:7` `Ashampoo UnInstaller Pro 27 Setup` |
| Versao do produto | `ashampoo-instalador.strings.txt:169002` `Ashampoo UnInstaller Pro 27`; `ashampoo-instalador.inventario.md:6` → `27.0.1` |
| Tecnologia do instalador | `ashampoo-instalador.strings.txt:3467` `Inno Setup Setup Data (7.0.0.3)` |
| Idioma das mensagens | `ashampoo-instalador.strings.txt:3468` `Inno Setup Messages (6.5.0) (u)` |
| Compilador | `ashampoo-instalador.inventario.md` → `Embarcadero Delphi for Win32 compiler version 36.0 (29.0.55362.2017)` |
| Payload | `ashampoo-instalador.strings.txt` → `lzma1smalldecompressor`, `Compressed block is corrupted` (payload comprimido, inacessivel) |

## Funcionalidades presentes no binario e ausentes no Tranal

| # | Funcionalidade | Evidencia no binario | Categoria | Impacto (alto/medio/baixo) |
|---|---|---|---|---|
| 1 | **Analise forense**: criar log de desinstalacao para programa **ja instalado** (sem log de instalacao) | nao confirmado no binario (payload LZMA inacessivel); doc.: `manual-pro27.txt:43-46` | remocao | alto — e a lacuna estrutural numero 1 do Tranal (`docs/auditoria/MATRIZ.md` H.8): para programa ja instalado nao existe fotografia do "antes" |
| 2 | Analise forense **em lote** (todas as aplicacoes compativeis de uma vez) | nao confirmado no binario; doc.: `site-produto-uninstaller.html:5429` | remocao | alto — escala o item 1 para a maquina inteira |
| 3 | Biblioteca de **snapshots nomeados, listaveis e apagaveis** (comparar sob demanda) | nao confirmado no binario; doc.: `manual-pro27.txt:246-288` | organizacao | alto — o Tranal so captura o diff "ao vivo" (`Core/Modules/SystemSnapshot.cs`, `Core/Modules/TracedPrograms.cs`) |
| 4 | Comparacao de snapshots que mostra **o que mudou**, nao so o que nasceu | nao confirmado no binario; doc.: `manual-pro27.txt:281-285` | organizacao | alto — `SystemSnapshot.Diff` devolve `NewFiles`/`NewFolders`/`NewRegistryKeys` |
| 5 | **Gerenciador proprio de servicos** (tipo de inicializacao, iniciar/parar, dependencias) | nao confirmado no binario; doc.: `manual-free.txt:330-402` | integracao | alto — o Tranal so abre `services.msc` (`Core/Tools/WindowsToolsCatalog.cs`) |
| 6 | **Gerenciador proprio de tarefas agendadas** (habilitar/desabilitar/excluir) | nao confirmado no binario; doc.: `manual-pro27.txt:333-343` | integracao | alto — o Tranal so abre `taskschd.msc` |
| 7 | **Editar manualmente o log de instalacao** (entrada a entrada) | nao confirmado no binario; doc.: `manual-pro27.txt:144-146` | organizacao | alto — `Core/Modules/TraceStore.cs` grava/importa/exporta/apaga, mas nao edita entrada |
| 8 | Exportar alteracoes de registro do log para **`.reg`** | nao confirmado no binario; doc.: `manual-free.txt:97-99` | relatorio | medio — hoje o log exporta JSON |
| 9 | Exportar logs de desinstalacao em **HTML, CSV e TXT** | nao confirmado no binario; doc.: `site-produto-uninstaller.html:5405` | relatorio | medio — a lista sai nesses formatos (`Core/Exporters.cs`), o log nao |
| 10 | **Importar lista de programas** exportada (marcando o que nao esta instalado) | nao confirmado no binario; doc.: `manual-pro27.txt:104-114` | organizacao | medio — no Tranal so existe o importador de log do Tranal |
| 11 | **Unlocker**: desbloquear arquivo em uso, reiniciar o Explorer, desregistrar DLL | nao confirmado no binario; doc.: `manual-pro27.txt:437-455` | remocao | alto — resolver "arquivo em uso" e o problema central do desinstalador profissional (`Core/ProcessTerminator.cs` so encerra processo) |
| 12 | **Undeleter**: recuperar arquivo apagado acidentalmente | nao confirmado no binario; doc.: `manual-free.txt:381-382` | recuperacao | medio — casa com o `Core/Recovery/ProgramBackup.cs` do Tranal |
| 13 | **Crash Analyzer**: relatorios de falha do Windows Event Log agrupados por programa | nao confirmado no binario; doc.: `manual-pro27.txt:457-458` | relatorio | medio |
| 14 | **File Wiper** com sobrescrita de espaco livre por unidade | nao confirmado no binario; doc.: `manual-pro27.txt:512-580` | limpeza | medio — o Tranal tem `Core/Tools/UnrecoverableDelete.cs` e `Core/Tools/EvidenceRemover.cs`, mas o item do binario nao foi conferido |
| 15 | **File Associator** (tipos de arquivo sem programa associado, editar/undo) | nao confirmado no binario; doc.: `manual-free.txt:414-457` | organizacao | medio |
| 16 | **Hibernation**: colocar um programa para dormir e acorda-lo depois | nao confirmado no binario; doc.: `manual-pro27.txt:177-181` | automacao | baixo |
| 17 | **Impact**: ranking de programas por impacto em performance (CPU/memoria) | nao confirmado no binario; doc.: `manual-pro27.txt:212-213` | interface | baixo |
| 18 | **Move / Revert relocation** de um programa instalado | nao confirmado no binario; doc.: `manual-pro27.txt:163-165`, `manual-free.txt:84-86` | organizacao | medio |
| 19 | **Software Updater** de programas de terceiros, em lote, com lista negra | nao confirmado no binario; doc.: `manual-pro27.txt:172-175` | automacao | medio — `Core/Updates/AutoUpdateClient.cs` atualiza so o proprio Tranal |
| 20 | **Ratings por estrelas** com base online + "rate online" | nao confirmado no binario; doc.: `manual-pro27.txt:51-53`, `128-137` | interface | baixo — recurso de rede; o Tranal e deliberadamente offline |
| 21 | **Desfragmentacao** de drives | nao confirmado no binario; doc.: `manual-free.txt:270` | manutencao | baixo |
| 22 | Filtro da lista **por tamanho** (subtopicos) | nao confirmado no binario; doc.: `manual-free.txt:60`, `manual-pro27.txt:39` | interface | medio — nenhum filtro por tamanho/ano localizado no `src/` |
| 23 | Comparacao de tamanho **declarado vs. real**, sincronizavel com o Windows | nao confirmado no binario; doc.: `site-produto-uninstaller.html:5412` | interface | baixo |
| 24 | **Pais do editor** com bandeira | nao confirmado no binario; doc.: `site-produto-uninstaller.html:5414` | interface | baixo |
| 25 | Categoria automatica com **reset para "Unsorted"** | nao confirmado no binario; doc.: `site-produto-uninstaller.html:5413`, `5425` | organizacao | baixo |
| 26 | **Whitelist / "cookies seguros"** no Internet Cleaner | nao confirmado no binario; doc.: `manual-free.txt:281-283` | limpeza | medio |
| 27 | **Registry Optimizer 2** com "Super Safe Mode" | nao confirmado no binario; doc.: `site-produto-uninstaller.html:5400-5401`, `5432-5433` | limpeza | medio |
| 28 | **Exclusao de backups por idade** (7/14/30 dias) no gerenciador de backups | nao confirmado no binario; doc.: `manual-free.txt:463-509` | recuperacao | medio |
| 29 | Filtro de tamanho no **Junk/Cache cleaner** e no **Junk Files Cleaner** (padroes com curingas, extensoes) | nao confirmado no binario; doc.: `manual-pro27.txt:295-309` | limpeza | baixo |
| 30 | **Instal guard**: definir como o programa identifica setups e quais propriedades de arquivo usar | nao confirmado no binario; doc.: `manual-pro27.txt:489-490` | automacao | medio |
| 31 | **Alerta de programa instalado na mesma pasta de outro** (sobras cruzadas) | nao confirmado no binario; doc.: `manual-free.txt:60` | remocao | alto — mesma lacuna medida no Revo (item 5 de `BINARIO-REVO.md`) |
| 32 | **Restauracao dos apps padrao** do Windows depois da remocao de Windows-Apps | nao confirmado no binario; doc.: `manual-free.txt:261-263` | recuperacao | alto — `Core/Modules/WindowsApps.cs` remove, mas nao restaura |
| 33 | Remocao de **add-ons e toolbars** de navegador em um clique | nao confirmado no binario; doc.: `site-produto-uninstaller.html:5226` | remocao | medio — `Core/Modules/BrowserExtensions.cs` cobre plugins, nao toolbars |
| 34 | Desinstalacao do **proprio produto** pelo "Apps & Features" | nao confirmado no binario; doc.: `manual-pro27.txt:513-521` | integracao | baixo |
| 35 | Resumo de **tempo de boot** no Boot Center | nao confirmado no binario; doc.: `manual-pro27.txt:311-343` | interface | baixo |
| 36 | Atalho de area de trabalho "**Drag & Drop UnInstall**" | nao confirmado no binario; doc.: `manual-pro27.txt:161` | interface | baixo — o Tranal tem arrastar-e-soltar (`Ui/Modules/HunterView.xaml`), mas nao o atalho de area de trabalho |
| 37 | Enumeracao de dispositivos/drivers por **SetupDi** no instalador | `ashampoo-instalador.inventario.md:409-410` → `**SetupDi** - Enumera dispositivos/Drivers` | integracao | baixo — o Tranal remove drivers (`MATRIZ.md` E10 ✅) mas nao os enumera |
| 38 | Instalador com **senha** (criptografia ChaCha20 do setup) | `ashampoo-instalador.inventario.md:9` → classe RTTI `TChaCha20Context`; `ashampoo-instalador.strings.txt` → `TSetupEncryptionKey`, `The password you specified is not correct. Please try again.` | integracao | baixo |
| 39 | Instalador que **fecha aplicacoes** que travam a instalacao (e as reinicia) | `ashampoo-instalador.inventario.md` §"Rotulos de interface relevantes" → `Instructs Setup to attempt closing applications using files that need to be updated…`, `…force close when closing applications…`, `…attempt restarting applications…` | automacao | medio — e um problema real de instalador, nao so um recurso do concorrente |
| 40 | Manifesto que **redireciona DLLs de sistema** | `ashampoo-instalador.strings.txt:66-71` → `file name="comctl32.dll" loadFrom="%SystemRoot%\system32\" /` e equivalentes (`mpr`, `netapi32`, `netutils`, `textshaping`, `version`) | integracao | baixo |
| 41 | Visao unica de **instalacoes/desinstalacoes recentes**, com acao de desinstalar | nao confirmado no binario; doc.: `manual-free.txt:33-35`, `manual-free.txt:198-200` | interface | medio — o Tranal tem historico de operacoes (`Core/Recovery/ProgramBackup.cs`) e log por programa (`Core/Modules/TraceStore.cs`), mas nao a tela "recentes" |
| 42 | **Icone na bandeja** do sistema para o monitor de instalacao, com acionamento manual | nao confirmado no binario; doc.: `manual-pro27.txt:13-14` | interface | baixo — o monitor existe (`Core/Modules/InstallMonitor.cs`), a bandeja nao foi verificada |
| 43 | **Separar instalacao de atualizacao** no log, reduzindo deteccoes falsas | nao confirmado no binario; doc.: `site-produto-uninstaller.html:5515` | organizacao | medio — a classificacao do desinstalador existe (`Core/UninstallerKind.cs`), a distincao dentro do log nao |
| 44 | Exportar a **lista de arquivos do log em .txt** | nao confirmado no binario; doc.: `manual-free.txt:90-92` | relatorio | baixo — hoje o log exporta JSON (`Core/Modules/TraceStore.cs`) |
| 45 | Abrir a **pasta de logs** no Explorer | nao confirmado no binario; doc.: `manual-free.txt:184-186` | interface | baixo |
| 46 | **Editar os parametros** da linha de comando de desinstalacao do programa-alvo | nao confirmado no binario; doc.: `manual-free.txt:121-123` | automacao | medio — o Tranal deriva o comando (`Core/Discovery/MsiProductCatalog.cs`, `Core/Automation/ManualRemoval.cs`), mas nao o expoe editavel |
| 47 | Remover a **entrada do programa** da lista do Windows sem desinstalar | nao confirmado no binario; doc.: `manual-free.txt:114-116` | organizacao | baixo — `Core/ProgramCustomization.cs` e `Core/ExcludeList.cs` ocultam/excluem; equivalente exato nao verificado |
| 48 | Filtrar a lista por texto e **ocultar categorias comuns** (ex.: .NET Framework) | nao confirmado no binario; doc.: `manual-free.txt:69-71` | interface | baixo — filtro por texto e filtros rapidos existem (`docs/auditoria/MATRIZ.md` A16 ✅) |
| 49 | **Atalhos rapidos por modulo** (tiles) na tela de visao geral | nao confirmado no binario; doc.: `manual-free.txt:28` | interface | baixo — modulos no painel (`docs/PARIDADE.md` §"Modulos do painel") |
| 50 | Atalho "**2 snapshots now and show changes**" (diff automatico sob demanda) | nao confirmado no binario; doc.: `manual-pro27.txt:271-273` | interface | medio — o diff automatico existe no fluxo de rastreio (`Core/Modules/TracedPrograms.cs`) |
| 51 | **Wizard de snapshots** por caso de uso, com nomes automaticos | nao confirmado no binario; doc.: `manual-pro27.txt:287-288` | interface | medio |
| 52 | Snapshot com **selecao de drives incluidos e pastas excluidas** | nao confirmado no binario; doc.: `manual-pro27.txt:492-493` | organizacao | medio — a captura do Tranal (`Core/Modules/SystemSnapshot.cs`) nao e configuravel |
| 53 | **Autorun**: adicionar e editar item de inicializacao (o Tranal so habilita/desabilita/remove) | nao confirmado no binario; doc.: `manual-free.txt:288-318`, `manual-pro27.txt:316-331` | integracao | medio — `Core/Tools/AutorunManager.cs` |
| 54 | Importar logs do **UnInstaller 4** (formato de terceiro) | nao confirmado no binario; doc.: `manual-free.txt:177-179` | integracao | medio — o importador (`Core/Modules/TraceStore.cs`) so le o formato do Tranal |
| 55 | Instalacao via **"Ashampoo App"** (loja de aplicativos propria) | nao confirmado no binario; doc.: `manual-troubleshooting.txt:13-15` | integracao | baixo — decisao de produto: o Tranal e distribuido por instalador proprio (`installer/tranal.iss`) |
| 56 | Instalador Inno Setup 7.0.0.3 como **tecnologia de empacotamento** | `ashampoo-instalador.strings.txt:3467` `Inno Setup Setup Data (7.0.0.3)`; `:3468` `Inno Setup Messages (6.5.0) (u)` | integracao | baixo — decisao de stack: o Tranal ja usa Inno Setup (`installer/tranal.iss`) |
| 57 | Conjunto de **switches do instalador** (silencioso, log, tarefas, senha, load/save de settings) | `ashampoo-instalador.inventario.md` §"Rotulos de interface relevantes" (`Instructs Setup to be silent or very silent.`, `Same as /LOG, except it allows you to specify a fixed path/filename to use for the log file.`, `Specifies a list of tasks that should be initially selected.`, `Please specify the password using the /PASSWORD= command-line parameter.`) | automacao | medio — o Tranal tem CLI propria (`src/Tranal.Cli/Program.cs`), mas nao um conjunto de switches de instalador verificado |
| 58 | Registro do **produto para desinstalacao pelo Windows** (chaves `UninstallDisplayName`, `UninstallDisplayIcon`, `UninstallFilesDir`) | `ashampoo-instalador.strings.txt:3023-3097` → `UninstallFilesDir`, `UninstallDisplayName`, `UninstallDisplayIcon`, `CreateUninstallRegKey`, `Uninstallable`, `UninstallLogMode`, `UninstallDisplaySize` | integracao | medio — o instalador do Tranal e Inno Setup, o que implica o mesmo registro; nao verificado no script |
| 59 | Deteccao da **versao do Windows suportada** (XP → 11, Server 2003 → 2025) | `ashampoo-instalador.inventario.md` §"Rotulos de interface relevantes" (`Windows 10`, `Windows 11`, `Windows XP`, `Windows Vista`, `Windows Server 2025`, `Windows Server 2022`, …) | integracao | medio — a matriz de sistemas suportados do Tranal nao foi verificada no `src/` |

## Observacoes

- **A lista acima e majoritariamente documental, por limitacao do binario.**
  Das 59 linhas, so 4 (37–40) tem evidencia no binario do instalador; as 55
  restantes vem do manual/site e estao marcadas `nao confirmado no binario`.
  Isso e uma limitacao de medicao, **nao** um achado sobre o produto: para
  responder a pergunta "o que o Ashampoo faz que o Tranal nao faz", seria
  preciso extrair o payload (reinstalar o setup com `innoextract` >= 1.10 ou
  uma versao do setup em formato suportado).
- As 60 linhas `AUSENTE`/`PARCIAL` da tabela `docs/auditoria/ASHAMPOO.md`
  (§2.1–§2.6.1) estao todas representadas aqui: 27 `AUSENTE` + 33 `PARCIAL`;
  as de maior impacto abriram item proprio, as variacoes menores do mesmo eixo
  foram agrupadas em um item so.
- Varias lacunas **coincidem** com as do Revo ja medidas
  (`docs/auditoria/BINARIO-REVO.md`): desbloqueio de arquivo em uso, gerenciador
  de servicos/tarefas, edicao do log, exportacao do log em `.reg`, filtro por
  tamanho, importacao de lista, remocao de toolbars. Sao os **eixos universais**
  do desinstalador profissional — o backlog do Tranal deve priorizar por eixo,
  nao por concorrente.
- Os itens que dependem de **rede ou base online** (13, 18, 19, 20) sao uma
  **decisao de produto** do Tranal, nao uma falha: o Tranal e offline, sem
  cadastro e sem telemetria (`docs/auditoria/MATRIZ.md` H.8). Registrados, nao
  silenciados.
