# TRANAL UNINSTALLER PRO — guia para quem mexer neste repositório

Desinstalador do Windows em C#/.NET 10 + WPF, em português. Reimplementação
independente do [Revo Uninstaller Pro](https://www.revouninstaller.com/) 5.5.4:
mesmo conjunto de funcionalidades, mesmo fluxo de desinstalação e mesmos
resultados — com código, marca e textos próprios.

> O Revo é a **referência de comportamento observável**. Nenhum binário, texto,
> recurso ou código do Revo é distribuído aqui: só os fatos medidos.

## Identidade (uma só interpretação)

| Item | Valor |
|---|---|
| Nome exibido | Tranal Uninstaller Pro |
| Executável / assembly | `TranalUninstaller.exe` / `TranalUninstaller` |
| Namespace CLR | `Tranal.Uninstaller` |
| Projetos | `Tranal.Uninstaller` (GUI), `Tranal.Cli` (CLI), `Tranal.Tests` |
| Solução | `Tranal.Uninstaller.slnx` |
| Configurações | `%LOCALAPPDATA%\Tranal Group\Tranal Uninstaller Pro\settings.json` |
| Backups (pontos de restauração) | `%LOCALAPPDATA%\Tranal Group\Tranal Uninstaller Pro\Backups\` |
| Histórico de desinstalação | `%LOCALAPPDATA%\Tranal Group\Tranal Uninstaller Pro\history.jsonl` |
| Repositório | `https://github.com/Matheus-araujo-dev/tranal-uninstaller` |

O título da janela, o nome do executável e o namespace andam **juntos**: se mudar
um, mude os outros.

## Comandos

```powershell
# build e testes
dotnet build Tranal.Uninstaller.slnx -c Release
dotnet test  Tranal.Uninstaller.slnx -c Release

# publicação (o .NET vai embutido; não precisa instalar runtime no PC do usuário)
powershell -NoProfile -ExecutionPolicy Bypass -File tools\publish-tranal.ps1 -Variant gui      # exige admin
powershell -NoProfile -ExecutionPolicy Bypass -File tools\publish-tranal.ps1 -Variant preview  # sem admin
powershell -NoProfile -ExecutionPolicy Bypass -File tools\publish-tranal.ps1 -Variant cli
powershell -NoProfile -ExecutionPolicy Bypass -File tools\publish-tranal.ps1 -Variant setup    # instalador
```

Baseline de avisos: **0**. Qualquer aviso novo é regressão.

## Regras do projeto

- **Português (pt-BR)** em comentários, XML docs, textos de interface e mensagens.
- **A CLI é pt-BR por decisão**: a saída do `tranal.exe` usa texto em português
  escrito no próprio código, e não o catálogo de idiomas. A GUI é a superfície
  traduzida. Não misture as duas: texto de interface da GUI vem **sempre** do
  catálogo (`Loc.Get`), e o catálogo nunca ganha uma chave que só a CLI usaria.
- **Nenhum PackageReference novo**. O que é do framework, é do framework
  (`System.Management` é a única exceção já existente).
- **`using System.IO` explícito** — em projeto WPF não é implícito.
- **XML doc `/// <summary>`** em toda classe e método público, explicando o *porquê*.
- **Uma só interpretação**: a regra fica em um lugar só e o resto consome.
  Não há duas versões de "nível de varredura", "confiança" ou "backup".
- **Nada de `catch` vazio** sem comentário dizendo por que é seguro.
- **Nunca desinstalar software do PC do usuário sem pedir.** O Tranal remove o
  que o usuário mandou remover — nada além disso.

## Arquitetura que não se deve quebrar

- **O núcleo (`src/Tranal.Uninstaller/Core`) não pode usar WPF, WinForms nem
  `System.Drawing`.** É compartilhado entre a GUI e a CLI: a CLI e os testes
  compilam contra o mesmo código.
- **A GUI e a CLI consomem o mesmo `RemovalReport`.** Não existe segunda
  interpretação do resultado de uma remoção.
- **Um único `UninstallMode` governa toda a desinstalação**
  (`BuiltIn`, `Safe`, `Moderate`, `Advanced`) — o equivalente aos quatro modos
  do Revo. Não introduza um segundo dial de profundidade.
- **Só o que o Revo faz está aqui.** `UninstallMode.BuiltIn` não varre nada;
  os outros três varrem em profundidades crescentes.
- **Confiança do leftovers** (`LeftoverConfidence`): só `High` vem marcado para
  remoção, como o Revo (que apaga em negrito). Mudar isso muda o resultado da
  desinstalação.
- **Arquivos e pastas vão para a Lixeira; chaves de registro são apagadas em
  definitivo.** É o comportamento medido do Revo (`SHFileOperation`).
- **Backup antes de remover.** Toda remoção cria um ponto de restauração no
  `BackupManager`, que sabe restaurar tudo ou item a item.
- **Item em uso → adiado para o próximo reinício** (`MoveFileEx` com
  `MOVEFILE_DELAY_UNTIL_REBOOT`), nunca deixado para trás em silêncio.

## Lista de exclusões, contas e backup do registro (regras do Lote 1)

- **A lista de exclusões (`Core/ExcludeList.cs`) é a guarda final do motor.**
  Ela é consultada em dois lugares e nos dois o item fica protegido: na
  varredura (o item **não aparece**) e em `UninstallEngine.RemoveOne` (o
  motor **recusa** remover, com `RemovalStatus.Protected`). O backup feito
  antes da remoção também pula os itens excluídos. Se um item excluído
  chegar marcado por outro caminho, ele **não** é removido.
- **Excluir um arquivo protege o arquivo mesmo dentro de uma pasta removida.**
  A remoção de pasta é recursiva, e uma remoção recursiva "cega"
  (`Directory.Delete(pasta, true)`) ou o envio da pasta inteira para a
  Lixeira levariam o arquivo protegido junto. Por isso, havendo item
  protegido dentro da pasta, o conteúdo sai **item a item**
  (`HasProtectedDescendant` + `RemoveFolderContent`) e a pasta em si
  permanece. O mesmo vale para o encerramento de processos: um processo que
  roda a partir de um item protegido não é encerrado.
- **O motor nunca atravessa um atalho.** Junction, symbolic link e ponto de
  montagem apontam para fora da pasta removida. Medido: o .NET 10 atravessa
  junction em `SearchOption.AllDirectories` e falha com
  `UnauthorizedAccessException` em `Directory.Delete(pasta, true)`. A regra
  está em `Core/SafeTraversal.cs`: o atalho é apagado **como atalho**
  (`Directory.Delete(link, false)`), o alvo fica intacto. Vale para:
  - a remoção, nos dois caminhos (o que apaga a pasta inteira e o que apaga
    item a item por causa da lista de exclusões);
  - **a raiz**: quando o próprio item de remoção é um atalho, remove-se o
    link e nada mais — enumerar a raiz devolveria os arquivos do alvo;
  - a contagem de tamanho de pasta e o `ProgramScanner`;
  - a cópia do backup (`BackupManager.CopyDirectory`): o backup não leva
    para dentro dele o conteúdo de fora da pasta que o usuário mandou
    fazer backup;
  - a varredura profunda do limpador de arquivos temporários
    (`SafeTraversal.EnumerateFilesDeep`): um limpador não remove arquivo de
    fora do escopo — e o alvo do link pode ser o perfil de outro usuário.
- **Nada fica em silêncio na remoção.** Item em uso dentro de uma pasta
  removida item a item é contado (`FailedInside`/`DeferredInside`) e aparece
  no texto do relatório; o que saiu de dentro da pasta também aparece
  (`RemovedInside`). Uma pasta com item protegido é **um** item na lista —
  mas o relatório diz quantos itens saíram de dentro dela. A GUI e a CLI
  mostram os mesmos quatro números: não pode haver um caso em que um dos
  dois mostre e o outro não.
- **Na deduplicação da varredura vence a ocorrência mais forte.** O mesmo
  caminho pode entrar na lista como pasta de instalação (alta confiança,
  marcada) e como pasta achada pelo nome do programa (confiança baixa, não
  marcada). Vence a marcada — e a desinstalação forçada adiciona o alvo
  informado **primeiro**, para que ele nunca seja sombreado por uma
  ocorrência fraca. Sem isso, a desinstalação forçada terminava sem
  remover o alvo que o usuário apontou.
- **Apontar para um arquivo não autoriza remover a pasta dele.** A
  desinstalação forçada de um arquivo **não** infere "pasta de instalação"
  a partir do diretório do arquivo: inferir faria a desinstalação forçada de
  `%LOCALAPPDATA%\Temp\app.exe` remover a pasta `Temp` inteira. O alvo
  apontado é o único item garantido; a pasta aparece na lista pela varredura
  e o usuário pode marcá-la se quiser. A pasta de instalação continua sendo
  usada quando ela é conhecida de verdade (o registro manda).
- **O casamento da lista é por prefixo com separador.** `C:\App` protege
  `C:\App\arquivo` e **não** protege `C:\App2`. O mesmo vale para chaves de
  registro, e `HKLM\…` precisa casar com `HKEY_LOCAL_MACHINE\…`.
- **Item de outra conta do Windows nunca vem marcado por padrão.**
  `LeftoverItem.OtherAccount` marca a origem; só
  `LeftoverScanOptions.SelectOtherAccountItems` libera a marcação. A opção
  "marcar todas as sobras" **não** marca item de outra conta.
- **As contas vêm do `ProfileList` do registro** (`Core/UserProfiles.cs`),
  não de uma varredura de pastas: pasta de perfil abandonada não é conta.
- **O backup completo do registro usa o `reg.exe` do Windows**
  (`Core/FullRegistryBackup.cs`). Não reescreva o formato `.reg` por conta
  própria: o caminho nativo preserva todos os tipos de valor e um backup com
  tipo errado é pior do que nenhum backup.
- **`ProcessTerminator` nunca encerra o processo do próprio Tranal** e só
  considera processo cujo executável esteja dentro da pasta que será
  removida — nunca "processo com o mesmo nome".
- **As opções de varredura viajam em `LeftoverScanOptions`**, não como
  parâmetros posicionais: quatro parâmetros (modo e três booleanos) fazem
  qualquer chamada errada trocar o modo por um booleano.

## Colunas, grupos e preferências por programa (regras do Lote 2)

- **O catálogo de colunas mora no núcleo** (`Core/ProgramColumns.cs`) e é a
  **única** fonte que sabe quais colunas existem, como elas se chamam na tela e
  qual é o valor de cada uma. A lista da tela (`Ui/ProgramColumnLayout.cs`) e a
  exportação (`Core/Exporters.cs`) consomem essa fonte — não escreva uma segunda
  lista de colunas, nem um segundo mapa de rótulos.
- **O valor de uma coluna é o mesmo na tela e no arquivo exportado.** As duas
  coisas passam por `ProgramColumns.Value`; `ProgramRow` sóaliases para ele.
- **O nome da coluna no arquivo exportado é fixo** (`ProgramColumnInfo.ExportName`):
  o arquivo tem de continuar legível por qualquer ferramenta, independente do
  idioma da interface. O rótulo da tela vem do catálogo de idiomas.
- **A coluna do nome nunca some** — nem na lista nem no "Remover esta coluna".
  Sem ela a lista perde o sentido.
- **Uma coluna, uma escrita.** As colunas escondidas vivem em
  `AppSettings.HiddenColumns`; tanto a janela "Colunas" quanto o "Remover esta
  coluna" passam por `MainWindow.SetVisibleColumns`. Não grave a preferência em
  dois lugares.
- **A grade da lista é montada em código**, não em XAML: o usuário escolhe as
  colunas, e um `GridView` escrito no XAML seria uma segunda cópia do catálogo
  que divergiria do primeiro na primeira alteração.
- **O clique direito seleciona a linha apontada**
  (`ProgramsList_PreviewMouseRightButtonDown`). Sem isso, o menu de contexto age
  sobre a linha selecionada antes e o usuário anota o programa errado.
- **As preferências por programa** (comentário, ícone, grupos) ficam em
  `program-data.json`, via `ProgramCustomizationStore`. Renomear um grupo **leva
  os programas junto**; apagar um grupo **não** apaga programa nenhum — o grupo é
  classificação, não desinstalação.
- **A identidade de um programa é a chave do registro** (`RegistryPath` →
  `RegistryKey` → nome exibido). É ela que casa o programa com a anotação.
- **A anotação do usuário é uma coluna própria** ("Anotação"), separada dos
  "Comentários" que vêm do registro.
- **A galeria de ícones usa o que já existe na máquina** (os ícones dos programas
  instalados) e aceita um caminho de arquivo. Não copie galeria de terceiros.
- **O filtro "Hidden" e as Opções mexem na mesma preferência**
  (`AppSettings.ShowSystemComponents`). Uma regra, dois lugares para chegar nela.
- **A gravação das preferências por programa é atômica** (temporário + troca) e
  deixa uma cópia `.bak`. Um arquivo ilegível é preservado como
  `program-data.json.corrompido` — nunca sobrescrito. Perder comentário e grupo
  do usuário sem aviso não é aceitável.
- **Falha ao gravar é avisada.** `SetComment`/`SetIcon`/`CreateGroup`/… devolvem
  `bool`; quem chama precisa checar e mostrar o erro. Senão o usuário vê o
  comentário na lista e ele sumir ao fechar o aplicativo.
- **A identidade de um programa inclui a visão do registro** (32/64 bits): a
  mesma subchave existe nas duas visões e são entradas diferentes.
- **O conjunto padrão da exportação é fixo e escrito à mão**
  (`Exporters.DefaultColumns`). O formato do arquivo exportado não pode depender
  do catálogo de colunas da tela, que o usuário pode mudar.
- **O CSV e o XML usam o nome fixo da coluna; o HTML usa o título traduzido.**
  CSV e XML são lidos por ferramenta; HTML é lido por gente.
- **O modo de visualização é estado da tela, não preferência.** Ele é reaplicado
  depois de a grade ser reconstruída — senão trocar de idioma devolve a lista ao
  layout "Detalhes".
- **A largura ajustada com "Best Fit" também é estado da tela.** Voltar para o
  modo "Detalhes" não pode reescrever a largura e apagar o ajuste que o usuário
  acabou de fazer.
- **Um `program-data.json` que deserializa mas não serve também é corrompido.**
  `{"Programs": null}` deserializa sem erro e derrubaria a listagem inteira no
  próximo arranque. O guarda vale para a raiz **e** para as listas: lista nula
  ou item nulo na lista tornam o arquivo inutilizável, e inutilizável é
  preservado como `.corrompido` como o ilegível.
- **A exportação é uma operação de leitura.** Ela lê o arquivo do usuário por
  conta própria (`ProgramCustomizationStore.PeekAll`) e não pode renomear o
  arquivo corrompido de quem só queria exportar a lista.
- **Falha de gravação e conflito de nome são coisas diferentes.**
  `CreateGroup`/`RenameGroup`/`DeleteGroup` devolvem `GroupResult`: quem chama
  precisa distinguir "já existe" de "não deu para gravar" — a mensagem errada
  faz o usuário procurar um problema que não existe.
- **Renomear/apagar que falham desfazem a mudança na memória.** Sem isso a tela
  mostraria o nome novo enquanto o disco ainda teria o antigo.
- **O fallback da troca atômica guarda o `.bak` antes de substituir.** Se a
  cópia não der, a gravação é abortada — é melhor avisar do que fingir que
  houve backup. E o temporário nunca sobra na pasta de dados.
- **A suíte não mexe no arquivo do usuário.** Um `Reload()` com o override nulo
  leria o `program-data.json` real — e, se ele estivesse corrompido, o
  renomearia. Use `ProgramCustomizationStore.ResetForTests()`.

## Partida a frio (regras do primeiro arranque)

O primeiro arranque — sem `settings.json` — tem regras que **só aparecem nesse
caminho**. A suíte não referencia o projeto WPF, então elas são conferidas
sobre o fonte e, de ponta a ponta, por `tools/test-cold-start.ps1`.

- **A janela principal ainda não foi exibida quando o diálogo de primeiro
  lançamento aparece.** O WPF recusa um `Owner` que não tenha sido exibido
  (`InvalidOperationException`), e o aplicativo **não abre**. O diálogo abre
  sem `Owner` e se centraliza na tela (`WindowStartupLocation="CenterScreen"`).
- **Durante a pergunta de tema o diálogo é a única janela do app.** Com o modo
  "encerrar na última janela fechada" (`ShutdownMode.OnLastWindowClose`) o
  aplicativo terminaria no fim da pergunta. `App.PerguntarTemaNoPrimeiroLancamento`
  troca o modo por `OnExplicitShutdown` durante a pergunta e o restaura depois.
- **O `Checked` do `RadioButton` dispara durante o `InitializeComponent`**, antes
  de o botão existir: um handler que toca campos ainda não inicializados
  estoura `NullReferenceException`. Os handlers de `Checked`/`Unchecked`
  precisam de guarda (`if (OkButton is null) return;`).
- **O tema é aplicado antes de a janela principal ser exibida** — assim a
  primeira pintura já sai com o tema escolhido, sem "flash".

O teste de ponta a ponta (`tools/test-cold-start.ps1`) sobe o aplicativo de
verdade sem o arquivo de preferências, confere o diálogo, a gravação da
preferência e a janela principal, e devolve as preferências de quem rodou.

## Instalador (regras do Lote 3)

O instalador é o script do Inno Setup (`installer/tranal.iss`), compilado por
`tools/publish-tranal.ps1 -Variant setup`. As regras abaixo foram **medidas**
nesta máquina — não são para serem "deduzidas" de novo.

- **`InitializeSetup` é função, não procedimento.** No Inno Setup 6.7.3 a
  assinatura é `function InitializeSetup(): Boolean;`. Declarar como
  `procedure` dá `Invalid prototype for 'InitializeSetup'`. A tabela de
  eventos fica dentro do próprio compilador (`ISCmplr.dll`): dá para conferir
  os protótipos por bytes em vez de adivinhar.
- **A seção `[Run]` não tem `ignoreexitcode`.** As bandeiras aceitas são
  `nowait`, `waituntilidle`, `shellexec`, `skipifdoesntexist`, `runminimized`,
  `runmaximized`, `showcheckbox`, `postinstall`, `unchecked`, `skipifsilent`,
  `skipifnotsilent`, `hidewizard`, `runhidden`, `waituntilterminated`,
  `32bit`, `64bit`, `runasoriginaluser`, `runascurrentuser`,
  `dontlogparameters`, `logoutput`. Por isso o runtime do .NET é instalado no
  `[Code]` (em `PrepareToInstall`), onde o **código de saída é conferido**:
  `0` = sucesso, `3010` = sucesso mas precisa reiniciar, `1641` = o
  instalador já reiniciou.
- **`PrepareToInstall` que devolve texto aborta a instalação** (código de
  saída 7) — é o jeito de instalar o runtime antes de qualquer arquivo do
  programa e cancelar tudo se ele falhar, sem deixar o computador pela
  metade.
- **`ExtractTemporaryFile` é procedimento** (não devolve valor) e exige que o
  arquivo esteja na seção `[Files]` com `Flags: dontcopy`.
- **`Exec` é `Exec(Filename, Params, WorkingDir, ShowCmd, Wait, var ResultCode)`**,
  com `Wait` = `ewWaitUntilTerminated` e `ShowCmd` = `SW_HIDE`.
- **O `.iss` tem que estar em UTF-8 COM BOM.** Sem BOM o Inno Setup 6 lê o
  script como ANSI (cp1252) e os acentos do menu Iniciar saem errados.
- **A compactação é `zip`, não `lzma2`.** Medido: com LZMA o McAfee desta
  máquina **bloqueava e apagava** o executável do instalador como "programa
  potencialmente indesejável"; com zip o bloqueio sumiu e o tamanho mudou
  0,3%. Se um dia o antivírus voltar a marcar, **a compressão é a primeira
  coisa a investigar** — não a assinatura do script.
- **O `ISCC.exe` não vem no PATH** (o winget instala por usuário). `Get-Iscc`
  procura nos caminhos conhecidos; o CI instala o Inno Setup se não achar.
- **A regra do registro fica em um lugar só.** A entrada do menu de contexto
  do Windows é removida pelo **próprio programa**
  (`TranalUninstaller.exe --unregister-context-menu`), não pelo script do
  instalador: deixar o registro apontando para um executável apagado seria
  referência órfã.
- **`postinstall` faz o instalador rodar o programa como o usuário NÃO
  elevado.** O Tranal exige administrador, então o lançamento no fim da
  instalação falhava com *"CreateProcess falhou; código 740"*. A entrada de
  `[Run]` precisa de `runascurrentuser` para o programa herdar o token
  elevado do instalador — e assim nem aparece um segundo pedido de UAC.
  (Achado na validação real, não na leitura do script.)
- **O desinstalador não apaga os dados do usuário** (comentários, grupos,
  preferências): eles moram em `%LOCALAPPDATA%` e não são do instalador.
- **A assinatura é feita depois da compilação**, nunca antes: o Inno Setup
  regrava o executável e a assinatura seria perdida. `Assinar-Instalador` fica
  no `publish-tranal.ps1` (uma regra, um lugar) e confere o **assinante** — o
  `Status` do Windows virá `UnknownError` mesmo com a assinatura aplicada,
  porque a raiz é autossinada e não está nas autoridades confiáveis da máquina.
  Isso é esperado; o que não pode acontecer é ficar sem assinatura nenhuma.
- **A chave privada do certificado nunca entra no repositório.** No CI ela vem
  do secret `TRANAL_CERT_PFX` (base64) + `TRANAL_CERT_PASSWORD`; na máquina de
  desenvolvimento, do repositório de certificados do usuário. O `.pfx` mora
  **fora** do projeto (hoje em `E:\Projetos\_segredos\`). O `.cer` público é
  versionado, em `installer/certificado/`.
- **A regra de escolher o certificado**: `O=Tranal` no titular. Um `*Tranal*`
  com chave privada, senão o primeiro. Sem isso, um certificado de teste com o
  mesmo nome rouba a assinatura.

## Classificação do desinstalador (regra do núcleo)

O tipo do desinstalador mora em **um lugar só**: o enum `UninstallerKind` e a
função `UninstallerClassifier` em `Core/UninstallerKind.cs`. Nenhum outro
arquivo reimplementa o reconhecimento — quem precisar do tipo consome o enum.

- **O valor da coluna é o identificador fixo** (`MsiExec`, `InnoSetup`,
  `Nsis`…), não um texto traduzido. A coluna exportada continua legível por
  qualquer ferramenta, em qualquer idioma, e a tela não ganha uma segunda
  tradução que possa divergir do código.
- **O reconhecimento casa pelo nome do arquivo do executável**, não por
  substring da linha de comando: `EAUninstall.exe` não é o `uninstall.exe` do
  NSIS. O caso foi medido na máquina e virou teste de regressão.
- **A classificação descreve; ela não decide o que é removido.** Nenhuma
  regra pode, sozinha, marcar um item de sobra para remoção.
- **"Desconhecido" é resposta legítima.** Os desinstaladores de fabricante
  (Chromium, Visual Studio, .NET, Electron) não seguem empacotador
  conhecido; classificá-los como se fossem seria inventar.
- Cada tipo do enum precisa ser produzido por alguma regra — há teste que
  garante isso, para o enum não ganhar membro morto.

## Justificativa da sobra (regra do núcleo)

A **confiança** e o **motivo** são o mesmo julgamento em duas partes, e por
isso nunca são decididos separados:

- O enum `LeftoverReason` e o julgamento `LeftoverScanner.Judge` ficam em
  `Core/UninstallSupport.cs`. Quem precisar do motivo consome o enum.
- **Todo item varrido tem motivo.** Um item sem justificativa não é
  aproveitável pelo usuário; há teste que garante isso.
- **A tradução mora em um lugar só**: `Core/Localization/LeftoverLabels.cs`.
  A interface (`ReasonTextConverter`) e a CLI consomem esse mesmo método —
  quando cada uma tivesse a sua, divergiriam na primeira tradução esquecida.
- **O motivo não é um botão de remoção.** Só a confiança alta entra marcada
  para remoção; o motivo é informação, e não muda a regra de seleção.

## Auditoria de concorrentes (regras da auditoria)

A pasta `docs/auditoria/` guarda a engenharia reversa de cada concorrente do
Revo. Um documento por software, no mesmo formato do `PARIDADE.md`, e a
matriz consolidada em `docs/auditoria/MATRIZ.md`.

- **Medido vale, inferido não vale.** Cada linha de um documento de auditoria
  aponta para uma evidência que existe em disco (strings de interface, seção
  de manual, árvore UIA, cabeçalho PE). O que não deu para verificar é
  escrito como **"não verificado"**, com o motivo.
- **O inventário bruto não é versionado.** O que fica no repositório é o fato
  derivado, como já vale para o `PARIDADE.md`.
- **A matriz muda o backlog.** Uma funcionalidade que só um concorrente tem
  pode ser nicho; se todos têm, vira prioridade máxima.
- **A coleta é feita por `tools/auditoria/coleta.ps1`** (strings / resx / PE),
  sem dependência externa. Não há 7-Zip CLI, Ghidra nem IDA nesta máquina.
- **Nunca desinstalar software do dono por causa da auditoria.** Os
  concorrentes estão autorizados pelo dono; o resto do PC, não.



- **Cores de texto nunca fixas no XAML** fora de `Themes/Dark.xaml` e
  `Themes/Light.xaml`. Use `DynamicResource`. Há teste que reprova cor fixa.
- **Não troque o dicionário dentro de um dicionário aninhado** — isso não
  repinta os `DynamicResource`.
- **Não carregue dicionário XAML em runtime por pack URI** — a segunda carga
  do mesmo arquivo lança `IOException`. Os temas são carregados uma vez, por
  XAML, e a troca escreve as brushes em `Application.Resources`.
- **`[System.IO.File]::Copy` preserva o carimbo de tempo do original.** Depois de
  restaurar um arquivo assim, o MSBuild julga que ele não mudou e **não
  recompila** — o teste seguinte roda contra o binário velho. Ao restaurar,
  atualize o `LastWriteTime`.
- **O texto exibido pelas ferramentas pode omitir caracteres.** O `read` e a
  saída do bash às vezes mostram um texto diferente do conteúdo real do arquivo
  (uma palavra pode aparecer partida por um espaço). Confirme texto crítico
  **por bytes** — busca da sequência exata ou contagem por regex —, não pela
  leitura.
- `GridViewColumn.Header` e `Window.Title` **não são DependencyProperty**: o
  texto vai por código.
- `ItemContainerStyle="{StaticResource {x:Type ListViewItem}}"` só funciona se
  existir um `Style TargetType="ListViewItem"` no dicionário de estilos.
- **Um brush de fundo não pode pintar texto.** `GroupHeaderBrush`, `PanelBrush`,
  `HeaderBrush` etc. são superfícies. Só os brushes de texto (nome com `Text`)
  podem ir no `Foreground`. Há teste que reprova o uso trocado.

## Ferramentas e telas (paridade que não pode quebrar)

- **O catálogo de ferramentas mora no núcleo** (`Core/ToolCatalog.cs`). A barra
  lateral, o registro de telas (`Ui/ViewSetup.cs`) e a suíte de testes consomem
  essa lista — não escreva a lista de novo em outro lugar.
- **Toda ferramenta da barra lateral precisa de uma tela.** Um botão sem tela
  registrada abre um painel vazio, sem aviso: foi exatamente o defeito do
  Otimizador de Registro. `UiParityTests` cobre isso.
- **A suíte de testes não referencia o projeto WPF** (o núcleo é compilado
  também na CLI; referenciar os dois criaria dois tipos `Core` diferentes).
  Por isso a conferência da camada de interface é feita sobre o fonte em disco.

## Publicar

```powershell
git push origin main                      # CI monta a release "latest"
git tag -a v1.0 -m "..." && git push origin v1.0   # CI monta a release da versão
```

O `.github/workflows/publicar.yml` roda os testes, publica a GUI (com
`requireAdministrator`), a variante de pré-visualização (sem admin) e a CLI,
e atualiza a release "latest".

## Medição (o que vale como verdade)

Nada aqui é suposição. O comportamento do Revo foi medido e o que está
documentado em `docs/REVO_SPEC.md` veio de:

- **strings** do payload do Revo Pro (`lang\english.ini` — 42 seções, 1136
  strings), que descrevem o texto e o fluxo de cada tela;
- **imports do PE** (`SHFileOperation`, `MoveFileEx`, `DeviceIoControl`, …),
  que revelam o mecanismo real por trás de cada tela;
- **comparação de listas** entre a CLI do Revo e a do Tranal (paridade
  210/210 entradas, 194/194 nomes, 0 divergências de versão);
- **comparação de pixels** para tema e layout.

As ferramentas de medição ficam em `tools/`. O payload do Revo **não** é
versionado: é material de análise, não do projeto.
