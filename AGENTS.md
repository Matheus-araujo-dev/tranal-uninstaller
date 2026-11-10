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

**`dotnet test` sozinho NÃO compila a interface gráfica.** A suíte referencia o
projeto da CLI (e, por consequência, o `Core` compartilhado), mas o projeto WPF
`Tranal.Uninstaller` só entra no build quando se compila a **solução**. Um
erro de compilação na tela passa os testes e só aparece no CI.

> Antes de dizer que está verde, rode os **dois**:
> `dotnet build Tranal.Uninstaller.slnx -c Release` **e** o `dotnet test`.
> A verificação mais forte é `git archive HEAD` para uma pasta limpa, build da
> solução e teste de lá — ela pega o que a árvore de trabalho esconde.

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

## Atribuição pela cadeia de desinstalação (regra do núcleo)

A varredura de sobras casa por **nome**; a cadeia do instalador casa por
**registro**. São dois níveis de prova, e o segundo é mais forte:

- `Core/MsiInstallLog.cs` guarda a leitura do pacote `.msi` em cache pela API
  do próprio Windows Installer (`msi.dll`), sem dependência externa. O log
  descreve, arquivo por arquivo e chave por chave, o que o instalador gravou.
- **O ciclo é o que importa:** o log é capturado **antes** do desinstalador
  nativo (depois dele o pacote sai do cache e não há mais o que ler) e
  conferido **depois**. O que o instalador gravou e continua no sistema é
  sobra de verdade.
- **A regra que decide se o log vale mora em um lugar só**:
  `MsiInstallLogReader.UsableAfter`. Sem desinstalador nativo bem-sucedido o
  log é descartado — senão ele descreveria o programa inteiro como sobra, e a
  etapa seguinte apagaria o que o usuário queria manter. A interface gráfica e
  a CLI consomem esse mesmo método, para que não possam divergir.
- **O motivo novo é `LeftoverReason.InstallerLog`**, e a tradução continua em
  `Core/Localization/LeftoverLabels.cs`.
- **As regras de leitura do pacote são medidas, não supostas**:
  `DefaultDir` traz `origem|destino` e o que fica em disco é o **destino**; o
  ponto (`.`) significa **a mesma pasta do pai**; um pacote de 32 bits instala
  em `Program Files (x86)` (o `Template` do sumário do pacote diz qual é).
  Há teste que fixa cada uma delas.
- **Falhar em silêncio é o comportamento certo.** Quando o caminho não fecha
  (bundles e payloads: Python, .NET Toolset), o Tranal não reporta nada errado.

## Avisos de segurança da desinstalação (regra do núcleo)

O original avisa sobre três coisas que o Tranal agora também avisa: outro
programa instalado na **mesma pasta**, a remoção de um **componente do
sistema**, e as pastas que podem conter **dados do usuário e documentos**.

A regra mora em **um lugar só**: `Core/UninstallAdvisories.cs`. O texto
traduzido mora em `Core/Localization/AdvisoryLabels.cs`. A interface gráfica
e a CLI consomem o **mesmo objeto** `UninstallAdvisory` — elas nunca
discordam sobre o que precisa ser avisado.

- A comparação de pasta compartilhada é por **igualdade exata** do caminho
  normalizado. "C:\Program Files\App" e "C:\Program Files" **não** são a mesma
  pasta: tratar como compartilhada transformaria o aviso em ruído.
- O aviso de dados do usuário vale para as pastas de **conteúdo**
  (`Documents`, `Desktop`, `Pictures`, `Music`, `Videos`, `Saved Games`,
  `Favorites`, `3D Objects`, `Contacts`, `Links`, `Searches`, `Downloads`) e
  para `C:\Users\Public`. `AppData` **não** entra: ali mora dado de programa,
  que é justamente o que a desinstalação deve apagar — e um aviso constante
  não é aviso nenhum.
- A confirmação que precede a remoção **nunca** é omitida pelas preferências
  de mensagem (`UserMessages.Confirm`). As preferências tratam só das
  mensagens informativas e de aviso.

## O que a engenharia reversa decidiu (regra do núcleo)

O lote de funcionalidades que entrou no Tranal a partir da **auditoria binária**
dos concorrentes tem uma regra por assunto, e cada uma mora em **um lugar só**:

| Assunto | Onde mora a regra |
|---|---|
| Saúde do desinstalador (ok / ausente / corrompido) | `Core/UninstallerHealth.cs` |
| Entrada protegida (`NoRemove`) | lido em `Core/ProgramScanner.cs`, avisado em `Core/UninstallAdvisories.cs` |
| Ordem da desinstalação (barulhento primeiro) | `Core/Appearance/SmartOrdering.cs` |
| Programas relacionados | `Core/Discovery/RelatedProgramFinder.cs` |
| Destinos do Windows (Registro, Programas e Recursos, Recuperação) | `Core/Management/SystemNavigator.cs` |
| Trava de desligamento durante a tarefa | `Core/Automation/ShutdownGuard.cs` |
| Fila de desinstalação (um por vez, colisão, saída da tarefa) | `Core/Automation/UninstallQueue.cs` |
| Entradas de inicialização do Windows (`Run`, `RunOnce`) | `Core/Discovery/StartupEntries.cs` |
| Comandos do Windows Installer (desinstalar e reparar) | `Core/Discovery/MsiProductCatalog.cs` |
| Comandos antes/depois por programa | `Core/ProgramCustomization.cs` guarda; `UninstallPlanBuilder.ResolveOptions` junta com o plano |
| Texto dos avisos | `Core/Localization/AdvisoryLabels.cs` (chave em `Lang/pt-BR.json` **e** `Lang/en-US.json`) |

- **O método da auditoria é binário, não documental.** A evidencia e o artefato
  do concorrente: a tabela de idioma do Revo Pro, a metadata .NET do BCU, as
  importacoes da PE e os literais do binario. O metodo esta em
  `docs/auditoria/BINARIO.md`. "Medido vale, inferido nao vale" vale tambem para
  as listas de lacuna: antes de decidir, a alegacao de "ausente" e conferida no
  codigo — e ja corrigiu uma lista errada.
- **A regra mora em um lugar so vale para estes avisos tambem.** O assistente
  consome `UninstallAdvisories.Build`; nao ha uma segunda montagem de aviso na
  tela. Quem mostra uma pasta compartilhada nao mostra o vizinho de novo em
  "programas relacionados".
- **Desinstalador corrompido so quando o caminho e absoluto.** Um comando
  relativo ("MsiExec.exe /X{...}") e resolvido pelo PATH do Windows: marcar
  todo MSI como corrompido seria mentira. O mesmo vale para o comando solto
  ("/S"), que nao e desinstalador nenhum.
- **A trava de desligamento e descartavel.** Ela vale enquanto o desinstalador
  nativo roda e e revogada ao fim: uma maquina que nao pode desligar nunca
  e um defeito, nao uma protecao.
- **A fila de desinstalacao e serial e a trava e de processo inteiro.** Dois
  instaladores ao mesmo tempo disputam os mesmos arquivos e o mesmo banco do
  Windows Installer. A trava (`UninstallQueue`) vale para a janela e para a CLI
  ao mesmo tempo — nao e uma convencao de quem chama.
- **Quem nao desinstala em silencio sai da tarefa, nao trava o lote.** A fila
  confere se a entrada do programa continua no registro depois da desinstalacao
  silenciosa; se continua, o programa deixa a fila e e reportado. E o
  comportamento do Revo.
- **A colisao e declarada, nao escondida.** Dois programas que dividem a pasta
  de instalacao ou o mesmo desinstalador sao marcados no resultado da fila.
- **A entrada de inicializacao e do programa quando o executavel esta na pasta
  dele.** O nome no comando e sinal mais fraco: um nome generico casaria com
  programas que nao sao o programa.
- **Desinstalar e reparar usam o mesmo construtor de comando.**
  `MsiProductCatalog.TryBuildCommand` monta os dois; `/x` desinstala e `/fa`
  repara. Nenhum dos dois monta o texto por conta propria.
- **O comando antes/depois do usuario entra no plano; a chamada vence.**
  `UninstallPlanBuilder.ResolveOptions` faz a juncao, e e o unico lugar que
  decide. O gancho (`CustomizationProvider`) e apontado pelo aplicativo no
  arranque — a suite de testes nunca le o arquivo de preferencias de quem
  roda.

## O alvo da desinstalação em lote (regra do núcleo)

A lista da janela principal é `SelectionMode="Extended"`: a desinstalação em
lote usa a **seleção**, e só a seleção.

As caixas de marcação da linha são **apenas indicador visual** (como no
Revo) e **nunca** definem o alvo. A razão é medida: `ProgramRow.IsChecked`
começa em `true` (`Ui/ProgramRow.cs:84`), então toda linha nasce marcada. Se
a caixa definisse o alvo, uma única ação removeria **todos** os programas.

Consequências que precisam ser lembradas quando se mexe na lista:

- O clique direito em uma linha **já selecionada** não descarta as outras
  (`MainWindow.xaml.cs`, `ProgramsList_PreviewMouseRightButtonDown`).
- O menu de contexto age sobre a linha **clicada** (`_contextRow`), e não
  sobre a primeira selecionada — com seleção múltipla, `SelectedItem` não
  serve.
- `ReloadProgramRows` preserva **todas** as linhas escolhidas.

## Remover as entradas da lista do Windows (regra do núcleo)

- **A regra mora em `Core/Management/WindowsListEntry.cs`**: `Remove` (a ação
  avulsa) e `RemoveMany` (a ação em lote). A ação avulsa **delega** para a
  ação em lote — não existe segunda interpretação de "remover entrada".
- **A remoção em si mora em `Core/RegistryEditing/RegistryBatchRemover.cs`**
  (`Remove`): chave protegida não é apagada e sai no relatório como
  *protegida*, e o código de erro do Windows vai no `RemovalOutcome`.
- **Remover entrada não é desinstalar.** O que se apaga é a chave de
  desinstalação; o programa continua instalado. A confirmação da tela diz isso
  na cara do usuário, e a linha de comando avisa antes de remover.
- **O alvo preserva a visão do registro** (32/64 bits) do programa
  (`RegistryTarget.FromProgram`): a mesma subchave existe nas duas visões e são
  entradas diferentes.
- **A tela e a linha de comando removem pelo mesmo código**: o menu "Mais
  comandos" (`CmdRemoveEntriesBatch`) e o comando `/remove-entries`.

## A arvore de alteracoes detectadas e a busca (regra do núcleo)

- **A classificação mora em `Core/Reporting/ChangeTreeReport.cs`**, em `Items`:
  a lista estruturada das alterações. A **tabela** (exportação e impressão) e a
  **árvore** (tela) derivam dela — o que se lê na tela é o que sai no arquivo.
- **A hierarquia e a busca moram em `Core/Reporting/ChangeTreeNodes.cs`**:
  `Build` (`:86`) monta os nós (categoria → grupo de mudança → alteração),
  `Filter` (`:137`) busca, `Total` (`:172`) conta e `Describe` (`:209`) escreve
  a árvore em texto.
- **A busca preserva a hierarquia**: um nó só continua na árvore quando ele tem
  um filho que casa, e ela diz quantas alterações sobraram ("1 de 6").
- **A estrutura é do núcleo; o texto é de quem mostra**: a interface traduz pelo
  catálogo (`Core/Localization/ChangeTreeLabels.cs`), a linha de comando escreve
  em português no código (`ExtraCommands.TituloDoNo`). Um caminho, uma chave ou
  um valor não se traduzem.
- **A tela e a linha de comando comparam pelo mesmo código**: a janela de
  rastreio monta os nós (`PreencherArvore`, `NoVisual`) e a linha de comando
  imprime os mesmos (`/comparar "antes" "depois" /arvore`, `/busca "termo"`).

## As marcas do programa na lista (regra do núcleo)

- **As marcas moram em `Core/ProgramMarks.cs`**, em um lugar só: `Detect`
  (`:96`) devolve o que o Tranal entendeu sobre o programa — 32 bits
  (`:51`), atualização (`:54`), órfão (`:57`), inválido (`:60`) e protegido
  (`:63`). A lista, a exportação e a linha de comando consomem esse
  resultado; ninguém monta a marca por conta própria.
- **Cada marca é um fato medido**, nunca um palpite: a máquina do executável
  (`program.Bits`), o detector de atualização (`UpdateMarker.Detect`), a
  ausência de chave de registro (órfão), a saúde do desinstalador
  (`UninstallerHealth`) e a tag `NoRemove` do próprio programa. Quando o
  Tranal não sabe, a marca não existe.
- **A regra do órfão mora em um lugar só** (`ProgramMarks.IsOrphan`, `:141`):
  a entrada que não veio do registro e não é MSI nem StoreApp. A coluna
  "Origem" consome essa mesma regra (`ProgramColumns.SourceOf`, `:429`) —
  uma regra, dois consumidores.
- **A coluna "Protegido" da lista considera as duas coisas**: a lista de
  exclusão do Tranal e a tag `NoRemove` do próprio programa
  (`ProgramColumns.IsProtected`, `:451`).
- **O nome exibido carrega a marca de 32 bits** (`ProgramMarks.WithBits`,
  `:182`): "Nome (32-bit)", como no Uninstall Tool e no Geek. A
  **identidade** do programa (`ProgramRow.DisplayName`, `:41`) continua crua —
  é o que a seleção, a cópia e a coluna de exportação usam.
- **O texto é do catálogo**: `ProgramMarks.Label` (`:171`) lê as chaves
  `ProgramMark.*`; o sufixo `(32-bit)` é `ProgramMark.BitsSuffix`; o nome da
  coluna é `Common.Marks`. O Core não escreve texto de interface.

## O que já existia e não tinha dono (regra do núcleo)

- **Módulo órfão não conta.** Um módulo do núcleo que nenhuma linha do
  produto chama não existe para quem usa o programa. Antes de inventar
  feature, **procure o que já está pronto**: no lote 29 a varredura achou 16
  classes estáticas sem consumidor de produto.
- **A ligação acontece no núcleo, não nas telas.** A interface e a linha de
  comando chamam o **mesmo** método: `UninstallEngine.BackupLeftovers`
  (`Core/UninstallSupport.cs:1620`) é quem grava o backup **e** quem
  registra a operação no Centro de mudanças
  (`Core/Recovery/ProgramBackup.cs:212`, `Register`). Nenhuma tela monta
  regra por conta própria.
- **O Centro de mudanças tem tela, comando e produtor**: tela em
  `Ui/Tools/ToolViews.cs:632` (`CreateUndoCenter`), comando
  `/undo-center` em `src/Tranal.Cli/AssociationCommands.cs:30`, e produtor em
  `Core/UninstallSupport.cs:1620`. O perfil do programa (o que ele gravou no
  computador) é gravado em `Core/Recovery/ProgramBackup.cs` e mostrado na
  linha do centro.
- **Estado que muda em memória precisa ser gravado por quem mudou.** Ler o
  arquivo de novo e regravá-lo perde o estado novo — foi assim que "desfazer"
  devolvia o conteúdo e deixava a mudança marcada como não desfeita
  (`ProgramBackup.GravarOperacoes`, que agora recebe a operação alterada).
- **O aviso do Centro de mudanças sai quando há backup**: sem backup não há o
  que desfazer. O texto vem do catálogo (`UndoCenter.Notice`).
- **A regra do ícone do atalho mora no módulo de atalhos**:
  `ShortcutFixer.ReadIconLocation` (`:303`) e `ReadTarget` (`:262`) tratam
  atalho inexistente do mesmo jeito — sem alvo e sem ícone. A varredura que
  descobre programas pelos atalhos é `Core/Discovery/ShortcutDiscovery.cs`
  (quatro pastas: Menu Iniciar do usuário e de todos, Área de Trabalho do
  usuário e pública).

## O log de remoção por item (regra do núcleo)

Toda operação de remoção grava um log com **uma linha por item**: tipo,
caminho, resultado e **código de erro**. A regra mora em
`Core/RemovalLog.cs`; o código de erro entra em `RemovalOutcome.ErrorCode`
(`Core/Removal.cs`), alimentado pelo bloco de captura da remoção
(`Core/UninstallSupport.cs`).

- O código de erro é o **do Windows** (a parte baixa do HResult): 5 é acesso
  negado, 32 é arquivo em uso. `RemovalOutcome.ErrorCodeOf` faz a extração.
- Dois códigos são **do Tranal** e estão no mesmo lugar: **403** = barrado
  pelas regras de segurança do Tranal (o Windows recusou *e* o Tranal protegeu
  são coisas diferentes) e **3010** = adiado para o próximo reinício (o mesmo
  número que o instalador do Windows usa).
- A pasta é `%LOCALAPPDATA%\Tranal Group\Tranal Uninstaller Pro\removal-logs`,
  ao lado dos logs de rastreio. A propriedade se chama `LogDirectory` e **não**
  `Directory`: uma propriedade chamada `Directory` esconderia o
  `System.IO.Directory` dentro da classe, e `Directory.CreateDirectory(...)`
  passaria a tentar criar uma pasta chamada "CreateDirectory".

## O plano de desinstalação e a retomada (regra do núcleo)

- **O plano mora em `Core/Automation/UninstallPlan.cs`** e a ordem das etapas
  é a do Revo: comando antes → desinstalador nativo → varredura → remoção →
  comando depois. O `UninstallPlanBuilder` monta; o `UninstallPlanExecutor`
  percorre. Ninguém mais escreve essa ordem.
- **A falha de uma etapa não engole as outras.** O executor devolve o
  resultado de cada etapa; quem chama decide o que fazer com a que falhou.
- **A retomada mora em `Core/Automation/UninstallSession.cs`**, e a gravação é
  **atômica** (temporário + troca), como a das preferências por programa. A
  sessão grava o passo **concluído**; a retomada começa pelo passo seguinte
  e não repete o que já deu certo.
- **O que não foi feito**: dirigir a interface do desinstalador de terceiros
  (o sentido do Uninstalr) exigiria automação de interface. Está declarado no
  `RELATORIO` do lote e no item 5 da `MATRIZ.md` — não é esquecimento.

## Os três comandos do usuário (regra do núcleo)

- **O modelo é `Core/Automation/RemovalProgress.cs`** e é testável sem
  interface: quem decide o que "pular" significa é o modelo, não a tela.
- **Pular** = pular a etapa atual. No assistente, pular a varredura remove
  **menos** (só o que a análise inicial já conhecia) — é a ação mais segura
  que existe.
- **Encerrar** = abortar a operação. No assistente, o assistente fecha e nada
  mais é removido.
- **Rodar agora** = tirar o adiamento para o próximo reinício
  (`RemovalProgress.ApplyRunNow`). As demais opções da remoção permanecem as
  que o usuário escolheu.
- **A janela do painel é sem dono modal**: o assistente continua vivo
  enquanto ela está aberta, porque é o assistente que avança as etapas e
  fecha a janela no fim. Se o assistente fechar, a janela vai junto.

## A lista em arquivo e a desinstalação em lote (regra do núcleo)

- **`Core/Automation/BatchList.cs`** é a única porta: uma regra por linha,
  nome exato ou `regex:` seguido de expressão regular.
- **Regex inválida é recusada com a mensagem e o número da linha** — nunca
  com exceção crua.
- **A CLI faz dry-run por padrão**: `tranal /batch "arquivo"` só lista o que
  casou; `tranal /batch "arquivo" /remove` executa. A listagem sem efeito é o
  que permite conferir a expressão regular antes de ela rodar.
- **A desinstalação em lote usa o mesmo fluxo da desinstalação avulsa**
  (`Program.UninstallProgram`): avisos, ponto de restauração, backup do
  registro, desinstalador nativo, varredura, remoção e histórico. As duas não
  podem divergir em nada.

## A correlação das permissões que um programa deixa (regra do núcleo)

- **`Core/Management/AppPermissionManager.cs`** é o único lugar que junta
  serviço, inicialização, menu de contexto e tarefa agendada **de um mesmo
  programa**. A pergunta do usuário é uma só — "o que este programa deixa
  rodando?" — e a resposta é uma lista só.
- **A correlação é pelo caminho do executável**, e o casamento é por
  **prefixo com separador**: `C:\App` casa com `C:\App\app.exe` e **não** com
  `C:\App2\app.exe`.
- **A extração do executável mora em `Core/Management/ExecutablePath.cs`** e
  é a mesma para serviço, tarefa, inicialização e menu de contexto. Caminho
  sem aspas e com espaço no nome só é resolvido pelo **prefixo mais longo
  que existe no disco** — cortar no primeiro espaço daria `C:\Program`.
- **A pasta de instalação vem do registro mesmo que ela não exista mais**: um
  programa cujos arquivos foram apagados ainda tem a pasta registrada, e é
  por ela que se descobre o que ele deixou no sistema.
- **Serviço protegido não é removido.** Os serviços dos quais o Windows
  depende para subir a sessão, a rede, a agenda e a proteção do sistema são
  recusados antes da tentativa — e o serviço do Windows é marcado pelo
  **fabricante**, não pelo assinante (o WHCP assina driver de todo mundo).

## Listagem que não depende do idioma do sistema (regra do núcleo)

- **Serviços** vêm do WMI (`Win32_Service`), não do `sc.exe`: a saída do
  `sc.exe` está em português nesta máquina e um catálogo que só funciona em
  inglês está quebrado.
- **Tarefas agendadas** vêm da leitura dos XML de
  `%SystemRoot%\System32\Tasks`, não do `schtasks.exe`: o `schtasks` traz o
  cabeçalho no idioma do sistema; o XML da tarefa é a definição oficial e
  está sempre em inglês. **A ação** é com o `schtasks.exe`, que é o Windows
  tratando o próprio arquivo.
- **Pacotes de driver** vêm do `pnputil.exe`, lidos pelo **nome publicado**
  (`oemNN.inf`), que tem formato fixo. O rótulo das colunas muda com o
  idioma; o nome do pacote, não.

## O catálogo de switches por ferramenta (regra do núcleo)

- **`Core/Automation/CliToolSwitch.cs`** é a fonte única: nome curto, nome
  longo e a forma longa que executa a ferramenta (o `RUN` do HiBit).
- **A CLI lista com `tranal /switches`** e a ajuda usa a mesma lista. Um
  switch que existe em um lugar existe nos dois.

## A suíte de testes não compila o projeto WPF (verificação obrigatória)

A suíte **não referencia** o projeto gráfico: o Core é compilado por **fonte
compartilhada** na CLI e na suíte. Isso é deliberado (referenciar os dois
criaria dois tipos `Core` diferentes), mas tem uma consequência que já
custou um commit quebrado:

- A CLI e a suíte recebem `System.IO` **implícito**. O projeto WPF **não**
  recebe (o conjunto dele é `System`, `System.Collections.Generic`,
  `System.Linq`, `System.Threading`, `System.Threading.Tasks`). Um arquivo do
  Core que use `Path.`, `File.` ou `Directory.` sem declarar
  `using System.IO;` passa na suíte — **431 testes verdes** — e **não
  compila** no aplicativo.
- Por isso, **toda** rodada que toca o Core compila o projeto gráfico
  **direto**:
  `dotnet build src\Tranal.Uninstaller\Tranal.Uninstaller.csproj -c Release`
- O teste `ImplicitUsingsTests` (`src\Tranal.Tests`) fecha essa porta: ele
  varre os arquivos do projeto gráfico e reprova quem usa `System.IO` sem
  declarar o using.

A verificação mais forte é `git archive HEAD` + build da solução numa pasta
limpa: é o que pega o que a árvore de trabalho esconde (e foi assim que os
dois commits quebrados apareceram).

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
- **O 🟡 do Revo é medido item a item, não por leitura de PE.** As duas seções
  que faltavam foram abertas contra o `src/`: `UninstallerPro` (331 strings) e
  `General App` (147). O inventário bruto saiu de `strings-en.json` (1.136
  strings em 43 seções) e o resultado está em `docs/PARIDADE.md`, seção
  *"Fechamento das 🟡 do Revo"*. Cada linha tem **string, arquivo e linha** —
  a próxima rodada confere com grep, não com leitura de binário.
- **Auditorias que se sobrepõem viram um item só.** "Recuperar" (Wise),
  "reverter a instalação rastreada" (Uninstall Tool) e "Undo Changes Center"
  (Soft Organizer) são a mesma peça: **um** item no backlog, com as três
  fontes. O backlog é do produto, não da lista de auditorias.
- **A numeração do backlog é única e sem lacuna.** F.1, F.2 e F.3 formam uma
  sequência contínua. Acrescentar item renumera a sequência inteira.
- **O que não se aplica sai para o F.4, com o motivo.** "Install Programs"
  (é o outro lado do produto), "Logs Database" (serviço online proprietário) e
  "pular o UAC" (o caminho do Windows é o que evita a marcação do Avast e do
  McAfee) estão registrados como decisão, não como esquecimento.
- **Driver de kernel continua fora do plano — e a contraprova cresceu.** De dez
  concorrentes auditados, **dois** usam driver (IObit com 4 drivers × 7
  arquiteturas; Uninstall Tool com um minifiltro) e os outros **não** — o
  Soft Organizer faz rastreio de disco em tempo real **sem** driver, o que
  prova que o caminho de usuário basta.
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

## O filtro da lista (regra do núcleo)

- **O estado dos filtros mora no núcleo** (`Core/Appearance/ProgramFilters.cs`,
  a classe `ProgramFilterSet`). O popup de filtros da barra lateral, a caixa de
  busca e o painel lateral de configurações **editam o mesmo objeto** — a
  janela principal só o instancia e o passa para os dois lados.
- **O julgamento do filtro nunca é reescrito na interface.** Se a tela precisa
  de um filtro novo, ele vai para `ProgramFilterSet.Matches`; se o painel
  precisa mostrar o estado, ele chama `Load(filters)`. Duas implementações do
  mesmo filtro divergem no primeiro caso em que o usuário usa as duas telas.
- **O realce não filtra**: ele só pinta o nome da linha
  (`ProgramRow.HighlightTextBrush`, com a cor em `HighlightPalette`).

## O motor de regras (regra do núcleo)

- **O arquivo de regras é lido em um lugar só** (`Core/Rules/RuleEngine.cs`).
  Quem decide é o **consumidor** do julgamento, nunca o motor:
  - a varredura de sobras (`Core/UninstallSupport.cs`) — `Exclude` não varre,
    `Protect` desmarca tudo;
  - a limpeza de lixo (`Core/Tools/JunkCleaner.cs`) — um caminho protegido
    pelas regras não é lixo, não importa o que a varredura achou.
- **A precedência é fixa e documentada**: a proteção do sistema vence tudo;
  dentro das regras do usuário, `Protect` > `Exclude` > `Include` > `Cleanup`;
  dentro da mesma categoria, a regra do aplicativo vence a regra geral; empate
  final: a primeira da lista.
- **O motor é carregado uma vez no arranque** (`RuleEngine.Reload()`), não a
  cada varredura.

## O histórico e a recuperação (regra do núcleo)

- **O histórico é um só**: `Core/UninstallHistory.cs` (`UninstallHistory`). Não
  crie um segundo log de desinstalação — este já carrega o identificador, a
  versão, a editora, os itens removidos e o ponteiro do backup (`RecoveryId`).
- **O backup das sobras acontece antes de apagar** (`UninstallEngine.BackupLeftovers`).
  O backup completo do registro cobre o registro, **não** os arquivos.
- **O conteúdo volta pelo backup, não pela memória da operação**: o manifesto
  (`LeftoverBackup.LoadManifest`) é a fonte da verdade do que foi guardado.

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
