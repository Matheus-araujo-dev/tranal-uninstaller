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

## Tema (armadilhas já vencidas)

- **Cores de texto nunca fixas no XAML** fora de `Themes/Dark.xaml` e
  `Themes/Light.xaml`. Use `DynamicResource`. Há teste que reprova cor fixa.
- **Não troque o dicionário dentro de um dicionário aninhado** — isso não
  repinta os `DynamicResource`.
- **Não carregue dicionário XAML em runtime por pack URI** — a segunda carga
  do mesmo arquivo lança `IOException`. Os temas são carregados uma vez, por
  XAML, e a troca escreve as brushes em `Application.Resources`.
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
