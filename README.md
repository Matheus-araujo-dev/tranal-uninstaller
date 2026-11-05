# Tranal Uninstaller Pro

Reimplementação independente, em **C# / .NET 10 / WPF**, do Revo Uninstaller Pro 5.5.4,
construída a partir da engenharia reversa conduzida com o **REA** (análise estática de PE,
recursos, imports e comportamento observável) e da leitura do payload original do produto.

> Código próprio, marca própria. Nenhum binário, recurso, texto protegido ou código do Revo foi
> copiado — apenas comportamento observável e fatos funcionais reproduzidos foram levados para cá.

---

## O que o Tranal faz

- **Lista** todos os programas instalados (paridade medida com o Revo: **210/210** entradas pela
  CLI, **77/77** programas pela GUI, **0** divergências de versão).
- **Desinstala** em quatro modos de profundidade, com análise antes, varredura depois, escolha
  item a item das sobras e **backup restaurável** de tudo que foi removido.
- **Limpa** rastros: lixo do sistema, histórico, evidências, inicialização automática, extensões
  de navegador, pacotes MSIX, programas monitorados.
- **Ajuda** a desinstalar o que não está registrado: arraste o executável para o Tranal e ele
  resolve o que pertence ao programa.

---

## Build e execução

```powershell
# solução completa (GUI + CLI + testes)
dotnet build Tranal.Uninstaller.slnx -c Release
dotnet test  Tranal.Uninstaller.slnx -c Release

# GUI
dotnet run --project src\Tranal.Uninstaller -c Release

# CLI
dotnet run --project src\Tranal.Cli -c Release -- /m "*"
```

Publicação self-contained (exe único, sem exigir runtime na máquina). **Use o script** — ele
valida o manifesto embutido e falha alto se o `requestedExecutionLevel` não for o esperado
(no publish single-file o apphost com o `RT_MANIFEST` fica *cached* no `obj/`; sem limpar o
`obj/`, o exe seguinte sai com o manifesto do publish anterior):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\publish-tranal.ps1 -Variant gui      # requireAdministrator
powershell -NoProfile -ExecutionPolicy Bypass -File tools\publish-tranal.ps1 -Variant preview  # asInvoker (só inspeção visual)
powershell -NoProfile -ExecutionPolicy Bypass -File tools\publish-tranal.ps1 -Variant cli
```

---

## Desinstalação: os quatro modos

O Revo Uninstaller Pro oferece quatro modos. No Tranal eles viraram **um único enum**
(`UninstallMode`), que governa toda a operação — não existe um segundo dial de profundidade
espalhado pela interface:

| Modo | O que faz | Custo |
|---|---|---|
| **Somente** | Roda apenas o desinstalador nativo. Não varre nada. | o mais rápido |
| **Seguro** | Roda o desinstalador e procura sobras seguras de apagar (pasta de instalação, chave de desinstalação). | rápido |
| **Moderado** | Inclui o Seguro e varre os lugares mais comuns do Registro e do disco. | médio |
| **Avançado** | Inclui o Moderado e varre fundo, inclusive o segundo nível das árvores de fornecedores. | o mais lento |

O fluxo continua em cinco etapas, como no Revo: **Analisar → Desinstalador nativo → Verificar
sobras → Remoção → Resumo**.

### Sobras com confiança

O Revo destaca em negrito apenas os itens de alta confiança e avisa que *"somente os itens
marcados em negrito e seus subitens serão excluídos"*. O Tranal reproduz isso com um campo
explícito (`LeftoverConfidence`): **só a confiança alta vem marcada para remoção**. A confiança
alta é a evidência mais forte de que o item pertence ao programa — a pasta de instalação
registrada e a chave de desinstalação.

### O que acontece com cada sobra

| Tipo de sobra | Destino | Por quê |
|---|---|---|
| Arquivo / pasta | **Lixeira** (`SHFileOperation`) | é o que o Revo faz — medido no binário dele |
| Chave de registro | apagada em definitivo | o Revo apaga registro em definitivo |
| Item em uso | **adiado para o próximo reinício** (`MoveFileEx` + `MOVEFILE_DELAY_UNTIL_REBOOT`) | o Revo avisa que "os arquivos restantes serão excluídos durante a próxima reinicialização" |
| Item protegido | **nunca é tocado** (chave estrutural, pasta de sistema) | segurança |

### Gerenciador de Backups

Antes de remover qualquer coisa, o Tranal cria um **ponto de restauração** com o conteúdo real do
que vai sumir (chaves exportadas com tipo e dado, arquivos e pastas copiados). Depois:

- **Restaurar tudo** — devolve o ponto inteiro ao sistema;
- **Restauração personalizada** — devolve só os itens que o usuário marcar;
- **Excluir**, **Excluir todos**, **Excluir por data** (uma semana, duas, 30, 90, 180 dias, um ano
  ou data escolhida).

Os pontos ficam em `%LOCALAPPDATA%\Tranal Group\Tranal Uninstaller Pro\Backups\`.

---

## Paridade de resultados (medida nesta máquina)

Comparação feita entre o `RevoCmd.exe` (Revo 5.5.4) e o `tranal.exe`, usando
`tools/compare_lists.py` (normaliza acentos e a codepage do console do Revo).

| Métrica | Revo | Tranal | Resultado |
|---|---|---|---|
| Entradas listadas (CLI, inclui componentes de sistema) | **210** | **210** | ✅ igual |
| Nomes únicos | **194** | **194** | ✅ igual |
| Divergências de versão | — | — | ✅ **0** |
| Programas na GUI (sem componentes de sistema) | **77** | **77** | ✅ igual |
| Grupo "Novos Programas" (janela de 7 dias) | **18** | **18** | ✅ contagem igual |
| ...entradas com o mesmo nome | — | — | ✅ **17 de 18** |

As 5 diferenças de nome detectadas pelo comparador bruto são **artefato de codepage**
(o Revo escreve em cp850: `™` vira `?` e `português` perde o `ê`). As versões batem todas.

### Regras de listagem descobertas (medidas, não adivinhadas)

1. **Fonte**: apenas as chaves de desinstalação
   (`HKLM|HKCU\SOFTWARE\...\Uninstall`, visões 32 e 64 bits).
   O catálogo `Installer\Products` do MSI (144 produtos na máquina) **não** é usado pelo Revo.
2. **Filtro obrigatório**: entrada sem `UninstallString` é invisível
   (AMD DVR, AMD Settings, AMD WVR64, AMD User Experience Program, RyzenMasterSDK,
   e a entrada duplicada de Battlefield 2042 — 6 casos nesta máquina).
3. **Componentes de sistema** (`SystemComponent=1`) **aparecem** na CLI do Revo
   (129 dos 210 registros) e ficam ocultos na GUI por padrão.
4. **Duplicatas**: entradas homônimas em chaves diferentes são mantidas (EA app aparece 2×).
5. **"Novos Programas"**: janela de **7 dias** (`New Days = 7`), lida do próprio registro do Revo
   em `HKCU\Software\VS Revo Group\Revo Uninstaller\Uninstaller`.

### Regra de data do "Novos Programas" (calibrada)

A cadeia de resolução da data efetiva foi **calibrada exaustivamente** contra o Revo
(~1000 regras avaliadas). A regra que fecha a contagem é:

```
data da pasta instalada (mtime)  →  InstallDate do registro  →  InstallerDate (MSI, FILETIME)
                                →  data de modificação/criação do executável de desinstalação
```

e o corte conta o 7º dia inteiro (meia-noite de hoje − 7 dias).

Com essa regra a contagem ficou em paridade (**18 = 18**) com **17 de 18** nomes coincidentes.
Os 2 desvios residuais têm causas externas à regra de data:

| Desvio | Onde aparece | Causa medida |
|---|---|---|
| **Microsoft GameInput** | só no Revo | Não é MSIX (`MsiGetProductInfoW` = erro 234). O Revo lê a fonte de metadados da Store; o Tranal ainda não a reproduz. |
| **Core Keeper** | só no Tranal | Instalado pelo **Steam**; o Revo o data pelo Steam e o Tranal pela data da pasta. |

### Elevação (medida no manifesto embutido)

| Artefato | `requestedExecutionLevel` | Medido com |
|---|---|---|
| `publish\gui\TranalUninstaller.exe` | **requireAdministrator** | `tools/dump_manifest.py` |
| `publish\preview\TranalUninstaller.exe` | asInvoker | idem (build de inspeção visual) |
| `publish\cli\tranal.exe` | asInvoker | idem |

O Revo Pro também exige elevação na GUI. A CLI do Tranal roda sem elevação e **autoeleva sob
demanda** só quando a operação exige (ex.: remoção em `Program Files`).

---

## Funcionalidades

| Área | Status |
|---|---|
| Layout principal (sidebar de módulos, lista com 9 colunas, grupos, painel de descrição, status bar) | ✅ idêntico ao Revo |
| Busca em tempo real por nome / editora / comentários | ✅ |
| Agrupamento "Novos Programas" / "Outros Programas" | ✅ (18 = 18 do Revo, 17 coincidentes) |
| Visões Detalhes / Lista / Ícones | ✅ (troca de colunas) |
| Exportação TXT / CSV / HTML / XML | ✅ |
| Assistente de desinstalação em 5 etapas | ✅ |
| **Quatro modos de desinstalação** (Somente / Seguro / Moderado / Avançado) | ✅ |
| **Varredura antes e depois** da desinstalação, com confiança por item | ✅ |
| **Arquivos e pastas para a Lixeira**; registro em definitivo | ✅ |
| **Remoção adiada para o próximo reinício** quando o item está em uso | ✅ |
| **Gerenciador de Backups** (restaurar tudo / item a item / excluir por data) | ✅ |
| Ponto de restauração do sistema (WMI `SystemRestore.Create`) | ✅ (requer admin) |
| Backup do registro (.reg + `Restore.dat`) em `%LOCALAPPDATA%\Tranal Group\Tranal Uninstaller Pro\RegBackup\Last` | ✅ |
| Desinstalação forçada (caminho do executável → análise → lista → remoção real) | ✅ |
| Elevação `requireAdministrator` na GUI | ✅ (manifesto verificado no publish) |
| **Temas claro / escuro** com troca em runtime | ✅ (verificado nos dois sentidos) |
| **48 idiomas** no seletor (mesmos do Revo) | ✅ catálogo completo |
| **Tradução de 2 idiomas** (pt-BR, en-US, 480 chaves cada) | ✅ os outros 46 com fallback honesto |
| **Filtros** por tipo / tamanho / unidade | ✅ |
| Menu "Mais Comandos" completo + menu principal (Opções / Tema / Idioma / Sair) | ✅ |
| **Janela Principal Flutuante** (overlay arrastável sobre janela/ícone/barra de tarefas) | ✅ |
| **Menu de contexto do Windows Explorer** | ✅ |
| **Verificação de atualização** (inicialização e automática em segundo plano) | ✅ |
| **Modo portátil** (pasta de dados própria + carregamento rápido) | ✅ |
| **Módulo Programas Monitorados** | ✅ com dados reais (TraceStore) |
| **Módulo Windows Apps** | ✅ 122 pacotes MSIX reais |
| **Módulo Extensões do Navegador** | ✅ Chrome / Edge / Firefox / Opera, com `__MSG_*` resolvido |
| **Módulo Base de Logs** | ✅ |
| **Módulo Modo Caçador** | ✅ arrastar-e-soltar |
| **Ferramenta Gerenciador de Autorun** | ✅ 346 itens de inicialização automática reais |
| **Ferramenta Limpeza de Arquivos Temporários** | ✅ |
| **Ferramenta Ferramentas do Windows** | ✅ |
| **Ferramenta Limpeza de Histórico** (agora com os 3 limpadores de rastros do original: navegadores, Office e Windows) | ✅ |
| **Ferramenta Otimizador de Registro** (entradas obsoletas do Menu Iniciar) | ✅ |
| **Ferramenta Removedor de Evidências** | ✅ |
| **Ferramenta Exclusão Irrecuperável** | ✅ |
| CLI do app gráfico (`/mu`, `/scan`, `/mode`, `/backup`, `/export`, `/wa`, `/be`, `/autorun`, `/tools`, `/hunter`, `/lang`, `/theme`, `/new`, `--uninstall-target`) | ✅ |

O mapa completo de paridade, seção a seção do produto original, está em
[`docs/PARIDADE.md`](docs/PARIDADE.md). A especificação que serviu de base está em
[`docs/REVO_SPEC.md`](docs/REVO_SPEC.md).

### Preferências persistidas

`%LOCALAPPDATA%\Tranal Group\Tranal Uninstaller Pro\settings.json` — tema, idioma, mostrar
componentes de sistema, mostrar Windows Apps, janela de "Novos Programas", programas recentes,
ponto de restauração do sistema antes de desinstalar, somente o desinstalador nativo,
desativar o histórico, atalhos de páginas web, modo avançado, atualização automática, Lixeira,
adiamento para o próximo reinício, backup antes de remover, pasta de dados portátil, carregamento
rápido e mecanismo de busca na web.

---

## Testes

`src/Tranal.Tests` (xUnit): **91 testes, todos verdes**.

Incluem verificações adversariais que valem mais que cobertura de linha:

- toda chave `{loc:...}` usada em qualquer XAML existe no catálogo pt-BR;
- **Dark.xaml e Light.xaml declaram exatamente as mesmas chaves**;
- toda chave `DynamicResource`/`StaticResource` usada no XAML existe nos temas;
- **nenhuma cor de texto fixa fora dos dicionários de tema** (regressão do defeito do
  `Foreground="White"`, que sumia no tema claro);
- a resolução de nomes `__MSG_*` das extensões de navegador, com `default_locale` e busca sem
  diferenciar caixa, em árvore temporária isolada;
- os quatro modos de desinstalação, a regra de confiança e o modo "Somente" não varrendo nada;
- ida e volta de valores de registro no backup (String, ExpandString, DWord, QWord, Binary,
  MultiString) e restauração de arquivo, pasta e chave.

---

## Estrutura

```
tranal-uninstaller/
├── Tranal.Uninstaller.slnx
├── src/
│   ├── Tranal.Uninstaller/          # GUI WPF
│   │   ├── MainWindow.xaml(.cs)     # layout equivalente ao Revo
│   │   ├── App.xaml(.cs)            # catálogo de temas + estilos
│   │   ├── Themes/                  # Dark.xaml, Light.xaml (cores) + Styles.xaml (estilos)
│   │   ├── Theming/ThemeManager.cs  # troca de tema em runtime
│   │   ├── Lang/                    # pt-BR.json, en-US.json (480 chaves cada)
│   │   ├── Ui/                      # painel genérico, assistente, backups, ferramentas, módulos
│   │   └── Core/                    # scanner, exportadores, backup, sobras, ferramentas, módulos
│   ├── Tranal.Cli/                  # tranal.exe (CLI)
│   └── Tranal.Tests/                # xUnit (83 testes)
├── docs/                            # especificação medida e mapa de paridade
├── tools/                           # publicação e ferramentas de medição
└── publish/
```

O núcleo (`Core/**/*.cs`) é compartilhado entre a GUI e a CLI por fonte compartilhada do MSBuild
(`<Compile Include="..\Tranal.Uninstaller\Core\**\*.cs" />`) e **não** pode usar WPF, WinForms
nem `System.Drawing`.

### Troca de tema (por que é assim)

Os dois temas são carregados **uma vez**, por XAML, em `App.xaml` (`ThemeCatalog` com as entradas
`Dark` e `Light`); os estilos são comuns e ficam em `Themes/Styles.xaml`. A troca de tema escreve
as brushes do tema escolhido em `Application.Resources` — o caminho que o WPF notifica, e que
reavalia todos os `DynamicResource` sem recarregar a janela.

Duas armadilhas medidas nesta máquina e que motivaram esse desenho:

1. **Trocar o dicionário de tema dentro de um dicionário intermediário não repinta.** O
   conteúdo dos recursos muda, mas a notificação de "os recursos mudaram" não sobe do nível
   aninhado até os `DynamicResource` da janela: o `settings.json` confirmava a troca e a tela
   continuava com as cores do tema anterior.
2. **Carregar um dicionário XAML por pack URI em runtime é instável**: a primeira carga funciona,
   a seguinte do mesmo arquivo falha com *"Não é possível localizar o recurso 'themes/dark'"*.
   Por isso nenhum dicionário é carregado em runtime.

---

## Ferramentas de medição

Em `tools/`:

| Ferramenta | Para quê |
|---|---|
| `publish-tranal.ps1` | publica GUI/preview/CLI e **verifica o manifesto embutido** |
| `compare_lists.py` | compara a saída da CLI do Revo com a do Tranal (normaliza codepage) |
| `compare_removal.py` | compara as sobras deixadas pelo Revo e pelo Tranal |
| `dump_manifest.py` | inspeciona o `requestedExecutionLevel` de um PE |
| `capture-live.ps1` | captura de janela que funciona mesmo **encoberta** (o `PrintWindow` devolve branco) |
| `capture-window.ps1` | captura de janela por `PrintWindow` |
| `switch-theme.ps1` | alterna o tema em runtime pelo menu (UIA) e captura |
| `ui-drive.ps1` | dirige a UI por `AutomationId` e captura |
| `lang-adicionar.ps1` + `lang-novas.txt` | mescla chaves novas nos catálogos de idioma dos dois idiomas |

O payload do Revo usado na análise **não** é versionado: é material de análise, não do projeto.

---

## Licença

MIT — veja [`LICENSE`](LICENSE).
