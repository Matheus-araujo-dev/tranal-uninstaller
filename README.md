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
powershell -NoProfile -ExecutionPolicy Bypass -File tools\publish-tranal.ps1 -Variant setup    # instalador (setup)
```

---

## Instalador (setup)

Baixe o **`TranalUninstaller-Setup.exe`** da release e execute. É um instalador de verdade, com
wizard em português, escolha da pasta e entrada em **"Adicionar e remover programas"** — como o
Revo faz.

| | Medido |
|---|---|
| Tamanho do setup | **62,07 MB** (`TranalUninstaller-Setup.exe`) |
| O que fica instalado | **1,09 MB** (5 arquivos) |
| O que é instalado junto, se faltar | o **.NET Desktop Runtime 10** (60,04 MB) |
| Pasta padrão | `C:\Program Files\Tranal Group\Tranal Uninstaller Pro` (o usuário escolhe outra) |
| Entrada em "Adicionar e remover programas" | ✅ gravada pelo instalador |
| Desinstalador | apaga o programa, os atalhos e a entrada do menu de contexto |

### Por que o setup tem 62 MB e o programa instalado tem 1 MB

O Tranal é feito em WPF, e WPF precisa do **.NET Desktop Runtime** na máquina. Há duas saídas:

- **autocontido** — o runtime viaja dentro do executável (132 MB em um arquivo só);
- **leve** — o programa instalado ocupa 1 MB e o runtime é instalado pelo setup, **só quando o
  computador não o tem**.

O Tranal escolheu a segunda: o download do setup é menor que o do autocontido, o que fica
instalado ocupa **1/123 do espaço**, e o setup **funciona sem internet** — o runtime está dentro
dele. Quem já tem o .NET 10 Desktop Runtime (caso do Windows 11 mais novo) só paga a extração do
programa: a instalação leva **segundos**.

O runtime é instalado **antes** de qualquer arquivo do programa e o código de saída é conferido
(`0` = sucesso, `3010` = sucesso mas precisa reiniciar). Se ele falhar, a instalação é
**cancelada** e o computador fica como estava.

### Aviso do antivírus e assinatura digital

O instalador é **assinado digitalmente** com um certificado **autossinado** da Tranal
(`installer/certificado/TranalUninstallerPro.cer`). Autossinado significa que a autoridade não é
uma empresa de certificados — então, até o certificado ser instalado na máquina, o Windows mostra
**"editor desconhecido"** em vez de "Tranal Uninstaller Pro".

Para o Windows mostrar o nome da Tranal (e o antivírus confiar no arquivo):

1. baixe o `TranalUninstallerPro.cer` (está no repositório, em `installer/certificado`);
2. clique duas vezes nele → **Instalar Certificado** → **Autoridades de Certificação Raiz
   Confiáveis** → **Instalar**;
3. abra o instalador de novo.

Sem esse passo o instalador continua funcionando: o Windows só avisa antes de abrir.

A via sem custo extra é essa. A via definitiva é comprar um certificado de código de uma
autoridade (DigiCert, SSL.com, Sectigo…): **a publicação já assina todo instalador**, então
basta trocar o certificado do secret `TRANAL_CERT_PFX` e o aviso some para todo mundo.

**Medido nesta máquina**: com compactação LZMA o McAfee bloqueava e **apagava** o instalador como
"programa potencialmente indesejável" (falso positivo conhecido em instaladores com carga LZMA).
Com `zip` o bloqueio desaparece e o tamanho muda 0,3% (o runtime já vem compactado). Assinar
não piorou o resultado — o instalador assinado continua sendo aceito.

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
| Layout principal (sidebar de módulos, lista com 12 colunas, grupos, painel de descrição, status bar) | ✅ idêntico ao Revo |
| Busca em tempo real por nome / editora / comentários | ✅ |
| Agrupamento "Novos Programas" / "Outros Programas" | ✅ (19 = 19 do Revo, 18 coincidentes) |
| Visões Detalhes / Lista / Ícones | ✅ (troca de colunas) |
| **Colunas customizáveis** (janela "Colunas" + "Remover esta coluna" no cabeçalho + "Ajustar ao conteúdo") | ✅ a escolha fica nas preferências |
| Exportação TXT / CSV / HTML / XML **com escolha de colunas** | ✅ |
| **Grupos de programas** (gerenciador + "Adicionar ao grupo" / "Remover do grupo") | ✅ renomear leva os programas junto |
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
| **Tradução de 2 idiomas** (pt-BR, en-US, 601 chaves cada) | ✅ os outros 46 com fallback honesto |
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
| **Lista de exclusões** (pastas, arquivos e chaves que o Tranal nunca toca) | ✅ gerenciador próprio + `/exclude` na CLI |
| **Encerrar os executáveis em execução** antes de remover os arquivos deles | ✅ |
| **Varredura em todas as contas do Windows** (outros perfis do `ProfileList`) | ✅ (itens de outra conta não vêm marcados) |
| **Backup completo do registro** (todos os hives em `.reg` + script de restauração) | ✅ antes de qualquer remoção |
| **Remoção que nunca atravessa atalho** (junction, symlink, ponto de montagem) | ✅ o alvo do link fica intacto |
| **Comentário e ícone por programa** (comentário do usuário e galeria de ícones da própria máquina) | ✅ em `program-data.json` |
| **Filtro "Hidden"** (componentes do sistema na listagem) | ✅ a mesma preferência das Opções |
| CLI do app gráfico (`/mu`, `/scan`, `/mode`, `/backup`, `/export`, `/wa`, `/be`, `/autorun`, `/tools`, `/hunter`, `/lang`, `/theme`, `/new`, `/exclude`, `--uninstall-target`) | ✅ |
| Exportação pela CLI com escolha de colunas (`/export "caminho" /csv /columns "Program,Version,Note"`) | ✅ os nomes são os do arquivo, fixos em qualquer idioma |

O mapa completo de paridade, seção a seção do produto original, está em
[`docs/PARIDADE.md`](docs/PARIDADE.md). A especificação que serviu de base está em
[`docs/REVO_SPEC.md`](docs/REVO_SPEC.md).

### Preferências persistidas

`%LOCALAPPDATA%\Tranal Group\Tranal Uninstaller Pro\settings.json` — tema, idioma, mostrar
componentes de sistema, mostrar Windows Apps, janela de "Novos Programas", programas recentes,
colunas escondidas da lista, ponto de restauração do sistema antes de desinstalar, somente o
desinstalador nativo, desativar o histórico, atalhos de páginas web, modo avançado, atualização
automática, Lixeira, adiamento para o próximo reinício, backup antes de remover, pasta de dados
portátil, carregamento rápido e mecanismo de busca na web.

A lista de exclusões mora em um arquivo próprio,
`%LOCALAPPDATA%\Tranal Group\Tranal Uninstaller Pro\exclude-list.json`, e é editada tanto pela
janela do gerenciador quanto pela CLI (`/exclude`).

As preferências por programa (comentário, ícone e grupos) ficam em
`%LOCALAPPDATA%\Tranal Group\Tranal Uninstaller Pro\program-data.json`.

---

## Testes

`src/Tranal.Tests` (xUnit): **302 testes, todos verdes**.

Incluem verificações adversariais que valem mais que cobertura de linha:

- toda chave `{loc:...}` usada em qualquer XAML existe no catálogo pt-BR;
- **Dark.xaml e Light.xaml declaram exatamente as mesmas chaves**;
- toda chave `DynamicResource`/`StaticResource` usada no XAML existe nos temas;
- **nenhuma cor de texto fixa fora dos dicionários de tema** (regressão do defeito do
  `Foreground="White"`, que sumia no tema claro);
- a resolução de nomes `__MSG_*` das extensões de navegador, com `default_locale` e busca sem
  diferenciar caixa, em árvore temporária isolada;
- os quatro modos de desinstalação, a regra de confiança e o modo "Somente" não varrendo nada;
- **a restauração revalidando a proteção**: um manifesto adulterado não vira escrita arbitrária em disco;
- **a raiz de uma unidade nunca é alvo**, nem com filtro curinga;
- **os testes isolados do ambiente**: rodam em diretório temporário e não tocam os pontos de
  restauração nem as preferências do usuário;
- **a integridade do `program-data.json`**: arquivo ilegível ou inutilizável é preservado como
  `.corrompido` (e o aplicativo avisa), a cópia de segurança não é descartada, o temporário não
  sobra na pasta de dados, e a exportação — que é uma leitura — não mexe no arquivo do usuário;
- **a partida a frio**: o diálogo de primeiro lançamento não pode apontar como `Owner` uma janela
  que ainda não foi exibida, e o modo de encerramento do app é trocado durante a pergunta de tema;
- ida e volta de valores de registro no backup (String, ExpandString, DWord, QWord, Binary,
  MultiString) e restauração de arquivo, pasta e chave.

O primeiro arranque também é conferido **de ponta a ponta**, com o aplicativo de verdade:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-cold-start.ps1
```

O script guarda as preferências de quem roda, sobe o aplicativo sem o `settings.json`, confere o
diálogo de tema, a gravação da preferência e a janela principal, e devolve as preferências no final.

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
│   │   ├── Lang/                    # pt-BR.json, en-US.json (601 chaves cada)
│   │   ├── Ui/                      # painel genérico, assistente, backups, ferramentas, módulos
│   │   └── Core/                    # scanner, exportadores, backup, sobras, ferramentas, módulos
│   ├── Tranal.Cli/                  # tranal.exe (CLI)
│   └── Tranal.Tests/                # xUnit (302 testes)
├── docs/                            # especificação medida e mapa de paridade
├── installer/                       # instalador (script do Inno Setup)
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
| `publish-tranal.ps1` | publica GUI/preview/CLI/**setup** e **verifica o manifesto embutido** |
| `installer/tranal.iss` | o instalador: wizard pt-BR, escolha de pasta, entrada em "Adicionar e remover programas" |
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
