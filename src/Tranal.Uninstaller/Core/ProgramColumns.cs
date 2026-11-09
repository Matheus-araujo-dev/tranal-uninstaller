using System.IO;
using Tranal.Uninstaller.Core.Appearance;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Uninstaller.Core;

/// <summary>As colunas da lista de programas.</summary>
public enum ProgramColumn
{
    /// <summary>Nome do programa (sempre a primeira coluna).</summary>
    Name,

    /// <summary>Tamanho estimado.</summary>
    Size,

    /// <summary>Versao.</summary>
    Version,

    /// <summary>32 ou 64 bits.</summary>
    Type,

    /// <summary>Data da instalacao.</summary>
    Date,

    /// <summary>Empresa.</summary>
    Company,

    /// <summary>Pagina inicial.</summary>
    HomePage,

    /// <summary>Comentarios do instalador (vem do registro).</summary>
    Comments,

    /// <summary>Comando de desinstalacao.</summary>
    UninstallString,

    /// <summary>
    /// Tipo do desinstalador (Windows Installer, Inno Setup, NSIS...).
    /// O valor e o identificador fixo do tipo, independente do idioma.
    /// </summary>
    UninstallerKind,

    /// <summary>Codigo do produto (GUID) do Windows Installer.</summary>
    ProductCode,

    /// <summary>
    /// Comando de desinstalacao silenciosa, quando existe um jeito conhecido
    /// de silenciar aquele desinstalador.
    /// </summary>
    QuietUninstall,

    /// <summary>
    /// O caminho do executavel que faz a desinstalacao. Vazio quando o
    /// comando de desinstalacao nao nomeia um executavel.
    /// </summary>
    UninstallerLocation,

    /// <summary>
    /// De onde o Windows tirou a entrada do programa. O valor e o
    /// identificador fixo da origem, independente do idioma.
    /// </summary>
    Source,

    /// <summary>
    /// Sim quando a entrada e um componente do sistema (o Windows nao mostra
    /// esses programas na lista de "Todos os Programas").
    /// </summary>
    SystemComponent,

    /// <summary>
    /// Sim quando o programa esta na lista de exclusoes do Tranal — ou seja,
    /// quando o motor se recusa a remove-lo.
    /// </summary>
    Protected,

    /// <summary>
    /// Sim quando o programa tem entrada de inicializacao automatica.
    /// Vazio enquanto a varredura de inicializacao nao aconteceu: sem dado,
    /// a coluna fica vazia — nunca um palpite.
    /// </summary>
    Startup,

    /// <summary>
    /// A classificacao do usuario, de 0 a 5 estrelas.
    /// </summary>
    Rating,

    /// <summary>Grupo criado pelo usuario (15072: "Group").</summary>
    Group,

    /// <summary>Anotacao do usuario sobre o programa (15216: "Add Comment").</summary>
    Note,

    /// <summary>Chave do registro de desinstalacao (a identidade do programa).</summary>
    RegistryKey,

    /// <summary>
    /// O pais do editor, com a bandeira (ex.: "Brasil 🇧🇷").
    ///
    /// O dado vem do proprio executavel: o <c>CountryDetector</c> le o recurso
    /// de versao do arquivo. Vazio quando o executavel nao diz de onde e — um
    /// palpite seria pior do que a celula vazia.
    /// </summary>
    Country,

    /// <summary>
    /// A data da instalacao em tempo relativo ("Hoje", "Ontem", "3 semanas
    /// atras"). O dado e o mesmo da coluna <see cref="Date"/>; a diferenca e
    /// so a forma de falar.
    /// </summary>
    RelativeDate,

    /// <summary>
    /// A plataforma de origem do programa (Steam, Epic, Chocolatey, porta-
    /// til...). Vazio quando o programa nao veio de nenhuma loja ou gerenciador
    /// conhecido.
    /// </summary>
    Platform,
}

/// <summary>
/// De onde o Windows tirou a entrada do programa.
///
/// A origem nao e um palpite: ela sai do proprio registro. A varredura do
/// Tranal so le tres lugares — a chave de desinstalacao, o catalogo do
/// Windows Installer e a Microsoft Store — e por isso nao existe uma quarta
/// origem.
///
/// O valor da coluna e o identificador fixo (<c>WindowsInstaller</c>,
/// <c>UninstallKey</c>, <c>StoreApp</c>), como na coluna do tipo do
/// desinstalador: o arquivo exportado continua legivel por qualquer
/// ferramenta, em qualquer idioma.
/// </summary>
public enum ProgramSource
{
    /// <summary>Origem nao determinada.</summary>
    Unknown,

    /// <summary>Entrada do Windows Installer (o proprio registro se declara MSI).</summary>
    WindowsInstaller,

    /// <summary>Entrada de uma chave de desinstalacao do registro.</summary>
    UninstallKey,

    /// <summary>Aplicativo empacotado da Microsoft Store (MSIX).</summary>
    StoreApp,
}

/// <summary>
/// A descricao de uma coluna: como ela se chama na tela e como ela se chama
/// no arquivo exportado.
///
/// O rotulo da tela vem do catalogo de idiomas (a interface e traduzida); o
/// nome da exportacao e fixo, para que o arquivo exportado continue legivel
/// por qualquer ferramenta, independentemente do idioma da interface.
///
/// <para><c>PendingLabelKey</c> existe para as colunas cuja chave propria
/// ainda nao chegou ao catalogo de idiomas: enquanto ela nao existe, vale o
/// rotulo de reserva (em portugues); quando o catalogo a tiver, a traducao
/// passa a valer sozinha. A <c>LabelKey</c> dessas colunas e uma chave que o
/// catalogo ja tem — e ela que impede a coluna de aparecer com o nome de uma
/// chave inexistente.</para>
/// </summary>
/// <param name="Id">A coluna.</param>
/// <param name="LabelKey">A chave do rotulo no catalogo de idiomas.</param>
/// <param name="FallbackLabel">O rotulo em portugues, quando a chave nao existe.</param>
/// <param name="ExportName">O nome fixo da coluna no arquivo exportado.</param>
/// <param name="PendingLabelKey">A chave propria da coluna, quando ela ainda nao existe no catalogo.</param>
public sealed record ProgramColumnInfo(
    ProgramColumn Id,
    string LabelKey,
    string FallbackLabel,
    string ExportName,
    string? PendingLabelKey = null);

/// <summary>
/// O catalogo de colunas da lista de programas.
///
/// Este e o unico lugar que sabe quais colunas existem, como elas se chamam
/// e que valor elas tem. A lista da tela e a exportacao consomem este
/// catalogo — nao existe uma segunda lista de colunas em cada um dos dois
/// lugares.
/// </summary>
public static class ProgramColumns
{
    private static readonly ProgramColumnInfo[] Catalogo =
    {
        new(ProgramColumn.Name, "Common.Program", "Programa", "Program"),
        new(ProgramColumn.Size, "Common.Size", "Tamanho", "Size"),
        new(ProgramColumn.Version, "Common.Version", "Versão", "Version"),
        new(ProgramColumn.Type, "Common.Type", "Tipo", "Type"),
        new(ProgramColumn.Date, "Common.Date", "Data", "Date"),
        new(ProgramColumn.Company, "Common.Publisher", "Empresa", "Company"),
        new(ProgramColumn.HomePage, "Common.Homepage", "Página inicial", "HomePage"),
        new(ProgramColumn.Comments, "Common.Comments", "Comentários", "Comments"),
        new(ProgramColumn.UninstallString, "Common.UninstallString", "Desinstalador", "UninstallString"),
        new(ProgramColumn.UninstallerKind, "Common.UninstallerKind", "Tipo de desinstalador", "UninstallerKind"),
        new(ProgramColumn.ProductCode, "Common.ProductCode", "Código do produto", "ProductCode"),
        new(ProgramColumn.QuietUninstall, "Common.QuietUninstall", "Desinstalador silencioso", "QuietUninstall"),
        // As colunas novas tem a chave propria pendente no catalogo de
        // idiomas (fora do escopo deste lote). A LabelKey delas e uma chave
        // que o catalogo ja tem: e ela que impede a coluna de aparecer com o
        // nome de uma chave inexistente. Quando o catalogo ganhar a chave
        // pendente, a traducao passa a valer sozinha.
        new(ProgramColumn.UninstallerLocation, "Common.Path", "Localização do desinstalador", "UninstallerLocation", "Common.UninstallerLocation"),
        new(ProgramColumn.Source, "Common.Installer", "Fonte", "Source", "Common.Source"),
        new(ProgramColumn.SystemComponent, "Common.Status", "Componente do sistema", "SystemComponent", "Common.SystemComponent"),
        new(ProgramColumn.Protected, "Common.Warning", "Protegido", "Protected", "Common.Protected"),
        new(ProgramColumn.Startup, "Common.Run", "Inicialização", "Startup", "Common.Startup"),
        new(ProgramColumn.Rating, "Common.Level", "Estrelas", "Rating", "Common.Rating"),
        new(ProgramColumn.Group, "Common.Group", "Grupo", "Group"),
        new(ProgramColumn.Note, "Common.Note", "Anotação", "Note"),
        new(ProgramColumn.RegistryKey, "Common.RegistryKey", "Chave do registro", "RegistryKey"),
        new(ProgramColumn.Country, "Common.Country", "País", "Country", "Common.Country"),
        new(ProgramColumn.RelativeDate, "Common.Date", "Data (relativa)", "RelativeDate", "Common.RelativeDate"),
        new(ProgramColumn.Platform, "Common.Platform", "Plataforma", "Platform", "Common.Platform"),
    };

    /// <summary>Todas as colunas, na ordem em que aparecem na lista.</summary>
    public static IReadOnlyList<ProgramColumnInfo> All => Catalogo;

    /// <summary>A descricao de uma coluna.</summary>
    public static ProgramColumnInfo Info(ProgramColumn id) =>
        Catalogo.FirstOrDefault(c => c.Id == id) ?? Catalogo[0];

    /// <summary>
    /// O rotulo traduzido da coluna. O catalogo de idiomas e a fonte: quando
    /// a chave nao existe no idioma ativo, o rotulo de reserva entra.
    ///
    /// Para a coluna que ainda tem a chave propria pendente no catalogo, e a
    /// chave pendente que manda: quando ela chegar, a traducao vale sozinha,
    /// sem ninguem precisar trocar nada aqui.
    /// </summary>
    public static string Label(ProgramColumn id)
    {
        var info = Info(id);

        if (info.PendingLabelKey is not null)
        {
            var pendente = Loc.Get(info.PendingLabelKey);
            return pendente == info.PendingLabelKey ? info.FallbackLabel : pendente;
        }

        var text = Loc.Get(info.LabelKey);
        return text == info.LabelKey ? info.FallbackLabel : text;
    }

    /// <summary>
    /// O valor da coluna para um programa. E o mesmo valor que a lista
    /// mostra e que a exportacao grava — uma coluna, uma interpretacao.
    /// </summary>
    /// <summary>
    /// A largura que a coluna precisa para caber o maior texto que ela mostra.
    ///
    /// A REGRA mora aqui, em um lugar so: o "best fit" de UMA coluna e o
    /// "auto-ajustar TODAS as colunas" usam esta mesma conta. Sao o mesmo
    /// numero calculado de dois jeitos — e nao duas contas parecidas, que e
    /// como as colunas comecam a discordar entre si.
    ///
    /// A conta e uma estimativa de caracteres (8 pixels por caractere, mais
    /// 24 de folga), limitada entre 48 e 600. E a mesma conta que o "Best
    /// Fit" de uma coluna ja usava; agora os dois caminhos usam o mesmo
    /// numero.
    /// </summary>
    /// <param name="id">A coluna.</param>
    /// <param name="rows">As linhas visiveis, com o programa e a personalizacao.</param>
    /// <param name="maxWidth">A largura maxima.</param>
    public static double AutoWidth(
        ProgramColumn id,
        IEnumerable<(InstalledProgram Program, ProgramCustomization? Custom)> rows,
        double maxWidth = 600)
    {
        var maior = Label(id).Length;

        foreach (var linha in rows)
        {
            var texto = Value(id, linha.Program, linha.Custom);
            if (texto.Length > maior) maior = texto.Length;
        }

        return Math.Clamp(maior * 8 + 24, 48, maxWidth);
    }

    public static string Value(ProgramColumn id, InstalledProgram program, ProgramCustomization? custom)
    {
        switch (id)
        {
            case ProgramColumn.Name:
                // O nome que o usuario deu ao programa tem a preferencia; sem
                // nome novo, vale o nome que o Windows registrou.
                return !string.IsNullOrWhiteSpace(custom?.DisplayName)
                    ? custom!.DisplayName
                    : program.DisplayName;
            case ProgramColumn.Size: return program.SizeText;
            case ProgramColumn.Version: return program.VersionOrNoVersion;
            case ProgramColumn.Type: return program.Bits;
            case ProgramColumn.Date: return program.InstallDate;
            case ProgramColumn.Company: return program.Publisher;
            case ProgramColumn.HomePage: return program.UrlInfoAbout;
            case ProgramColumn.Comments: return program.Comments;
            case ProgramColumn.UninstallString: return program.UninstallString;
            case ProgramColumn.UninstallerKind: return program.Kind.ToString();
            case ProgramColumn.ProductCode: return program.ProductCode;
            case ProgramColumn.QuietUninstall: return program.QuietUninstallCommand;
            case ProgramColumn.UninstallerLocation: return UninstallerLocationOf(program);
            case ProgramColumn.Source: return SourceOf(program).ToString();
            case ProgramColumn.SystemComponent: return YesNo(program.IsSystemComponent);
            case ProgramColumn.Protected: return YesNo(IsProtected(program));
            case ProgramColumn.Startup: return StartupValueOf(program);
            case ProgramColumn.Rating: return ProgramRating.Stars(custom?.Rating ?? 0);
            case ProgramColumn.Group: return GroupOf(custom);
            case ProgramColumn.Note: return custom?.Comment ?? string.Empty;
            case ProgramColumn.RegistryKey: return program.RegistryKey;
            case ProgramColumn.Country: return CountryOf(program);
            case ProgramColumn.RelativeDate: return Modules.RelativeDate.Format(program.InstallDateValue);
            case ProgramColumn.Platform: return PlatformOf(program);
            default: return string.Empty;
        }
    }

    /// <summary>
    /// A plataforma de origem do programa (Steam, Epic, Chocolatey...).
    ///
    /// A deteccao e feita pelos marcadores do caminho de instalacao e do
    /// comando de desinstalacao — a unica pista que existe sem depender de
    /// rede.
    /// </summary>
    /// <param name="program">O programa instalado.</param>
    public static string PlatformOf(InstalledProgram program)
    {
        if (program is null) return string.Empty;

        var plataforma = Discovery.AppPlatformDetector.Detect(program);
        return plataforma?.Name ?? string.Empty;
    }

    /// <summary>
    /// O pais do editor, com a bandeira.
    ///
    /// A leitura e feita do executavel do programa (o recurso de versao do
    /// arquivo), e nao do nome do publisher: o nome vem da empresa que
    /// registrou o programa, que nem sempre e de onde o software foi feito.
    /// </summary>
    /// <param name="program">O programa instalado.</param>
    public static string CountryOf(InstalledProgram program)
    {
        if (program is null) return string.Empty;

        var caminho = ExecutablePathOf(program);
        if (caminho.Length == 0) return string.Empty;

        var pais = Appearance.CountryDetector.Detect(caminho);
        return pais?.Label ?? string.Empty;
    }

    /// <summary>
    /// O caminho do executavel do programa, quando ele existe.
    ///
    /// A regra e uma so: e o proprio <see cref="Discovery.UsageSignals"/>
    /// que sabe onde o executavel esta (o desinstalador, quando existe; senao,
    /// o executavel dentro da pasta de instalacao). Sem executavel, nao ha
    /// pais.
    /// </summary>
    /// <param name="program">O programa instalado.</param>
    private static string ExecutablePathOf(InstalledProgram program)
    {
        try
        {
            return Discovery.UsageSignals.ExecutableOf(program);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>Os grupos do programa, separados por virgula.</summary>
    public static string GroupOf(ProgramCustomization? custom)
    {
        if (custom is null || custom.Groups.Count == 0) return string.Empty;
        return string.Join(", ", custom.Groups);
    }

    /// <summary>
    /// O texto de "sim" e de "nao" das colunas de estado. Vem do catalogo de
    /// idiomas, porque e texto de tela.
    /// </summary>
    private static string YesNo(bool value) => Loc.Get(value ? "Common.Yes" : "Common.No");

    /// <summary>
    /// O caminho do executavel que faz a desinstalacao.
    ///
    /// E o proprio comando de desinstalacao do registro, lido pelo mesmo
    /// extrator que a varredura usa. Quando o comando nao nomeia um
    /// executavel (o caso do "winget uninstall", por exemplo), o valor fica
    /// vazio: melhor vazio do que um caminho inventado.
    /// </summary>
    /// <param name="program">O programa instalado.</param>
    public static string UninstallerLocationOf(InstalledProgram program)
    {
        if (program is null) return string.Empty;

        var command = program.UninstallString;
        if (string.IsNullOrWhiteSpace(command)) return string.Empty;

        return ProgramScanner.ExtractExecutablePath(command).Trim();
    }

    /// <summary>
    /// De onde o Windows tirou a entrada do programa.
    ///
    /// A origem sai do proprio dado: a entrada se declara Windows Installer
    /// (ou veio do catalogo de produtos MSI), ou o programa vive em
    /// WindowsApps (aplicativo da Store), ou veio de uma chave de
    /// desinstalacao — que e de onde a varredura do Tranal le tudo. Uma
    /// entrada que nao veio de lugar nenhum e "desconhecida", nao
    /// "chave de desinstalacao".
    /// </summary>
    /// <param name="program">O programa instalado.</param>
    public static ProgramSource SourceOf(InstalledProgram program)
    {
        if (program is null) return ProgramSource.Unknown;

        if (program.IsWindowsInstaller || program.IsMsi) return ProgramSource.WindowsInstaller;
        if (program.Kind == UninstallerKind.StoreApp) return ProgramSource.StoreApp;

        if (string.IsNullOrWhiteSpace(program.RegistryPath)
            && string.IsNullOrWhiteSpace(program.RegistryKey))
        {
            return ProgramSource.Unknown;
        }

        return ProgramSource.UninstallKey;
    }

    /// <summary>
    /// True quando o programa esta na lista de exclusoes do Tranal.
    ///
    /// A lista de exclusoes e a guarda final do motor: um item excluido nunca
    /// e removido. A coluna mostra essa sitacao para o usuario saber, antes
    /// de pedir a remocao, que o motor vai recusar.
    /// </summary>
    /// <param name="program">O programa instalado.</param>
    public static bool IsProtected(InstalledProgram program)
    {
        if (program is null) return false;

        var lista = ExcludeList.Current;
        if (lista.IsEmpty) return false;

        if (!string.IsNullOrWhiteSpace(program.InstallLocation)
            && lista.ContainsPath(program.InstallLocation)) return true;

        if (!string.IsNullOrWhiteSpace(program.RegistryPath)
            && lista.ContainsRegistryKey(program.RegistryPath)) return true;

        return false;
    }

    /// <summary>
    /// O valor da coluna "Inicializacao".
    ///
    /// Sem varredura nao ha resposta: a coluna fica vazia. A varredura e
    /// declarada por quem tem o tempo de faze-la (a tela, a CLI) — o modelo
    /// nao inventa a resposta.
    /// </summary>
    /// <param name="program">O programa instalado.</param>
    private static string StartupValueOf(InstalledProgram program)
    {
        if (!StartupEntryIndex.IsKnown) return string.Empty;
        return YesNo(StartupEntryIndex.HasEntryFor(program));
    }

    /// <summary>
    /// As colunas pedidas por nome.
    ///
    /// O nome aceito é o do arquivo exportado (<see cref="ProgramColumnInfo.ExportName"/>),
    /// fixo e independente do idioma: é o que torna o comando roteirizável.
    /// A ordem do catálogo manda — o arquivo sai sempre na mesma ordem.
    ///
    /// Devolve false quando algum nome não existe no catálogo.
    /// </summary>
    public static bool TryParse(
        IEnumerable<string> names,
        out IReadOnlyList<ProgramColumn> columns,
        out string unknown)
    {
        columns = Array.Empty<ProgramColumn>();
        unknown = string.Empty;

        var escolhidas = new List<ProgramColumn>();

        foreach (var nome in names)
        {
            var texto = (nome ?? string.Empty).Trim();
            if (texto.Length == 0) continue;

            var coluna = Catalogo.FirstOrDefault(c =>
                string.Equals(c.ExportName, texto, StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.Id.ToString(), texto, StringComparison.OrdinalIgnoreCase));

            if (coluna is null)
            {
                unknown = texto;
                return false;
            }

            if (!escolhidas.Contains(coluna.Id)) escolhidas.Add(coluna.Id);
        }

        if (escolhidas.Count == 0)
        {
            unknown = string.Empty;
            return false;
        }

        columns = Catalogo.Where(c => escolhidas.Contains(c.Id)).Select(c => c.Id).ToList();
        return true;
    }

    /// <summary>
    /// As colunas que aparecem na lista: todas menos as escondidas nas
    /// preferencias. A coluna do nome nunca some — sem ela a lista perde o
    /// sentido.
    /// </summary>
    public static IReadOnlyList<ProgramColumn> Visible(IReadOnlyCollection<string>? hidden)
    {
        var result = new List<ProgramColumn>();
        foreach (var info in Catalogo)
        {
            if (info.Id == ProgramColumn.Name) { result.Add(info.Id); continue; }

            // A comparacao ignora a caixa: a preferencia pode ter sido
            // editada a mao, e "size" tem de valer como "Size".
            if (hidden is not null
                && hidden.Any(h => string.Equals(h, info.Id.ToString(), StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            result.Add(info.Id);
        }

        return result;
    }
}

/// <summary>
/// O indice das entradas de inicializacao automatica usado pela coluna
/// "Inicializacao".
///
/// A varredura de inicializacao e cara (registro, servicos por WMI, tarefas
/// agendadas por COM) e nao cabe dentro do desenho de uma linha da lista.
/// Por isso o indice e **declarado** por quem tem o tempo de varrer: a tela
/// ou a CLI chamam <see cref="Refresh"/> uma vez e as linhas passam a
/// responder. Antes disso, a coluna fica vazia — sem dado, a coluna fica
/// vazia.
///
/// O casamento entre a entrada de inicializacao e o programa e por caminho:
/// o executavel que a entrada roda mora dentro da pasta de instalacao do
/// programa, ou e o proprio desinstalador dele.
/// </summary>
public static class StartupEntryIndex
{
    private static IReadOnlyList<AutorunItem> _items = Array.Empty<AutorunItem>();
    private static bool _known;

    /// <summary>As entradas de inicializacao conhecidas.</summary>
    public static IReadOnlyList<AutorunItem> Items => _items;

    /// <summary>
    /// True quando a varredura ja aconteceu. Enquanto for false, a coluna
    /// "Inicializacao" fica vazia.
    /// </summary>
    public static bool IsKnown => _known;

    /// <summary>
    /// Declara o resultado de uma varredura de inicializacao.
    /// </summary>
    /// <param name="items">As entradas encontradas na varredura.</param>
    public static void Refresh(IReadOnlyList<AutorunItem>? items)
    {
        _items = items ?? Array.Empty<AutorunItem>();
        _known = true;
    }

    /// <summary>
    /// Esquece a varredura. A coluna volta a ficar vazia, em vez de passar a
    /// mentir que o programa nao tem inicializacao.
    /// </summary>
    public static void Clear()
    {
        _items = Array.Empty<AutorunItem>();
        _known = false;
    }

    /// <summary>
    /// True quando o programa tem entrada de inicializacao automatica.
    /// </summary>
    /// <param name="program">O programa instalado.</param>
    public static bool HasEntryFor(InstalledProgram program)
    {
        if (program is null) return false;
        if (!_known || _items.Count == 0) return false;

        var pasta = Normalizado(program.InstallLocation);
        var desinstalador = Normalizado(ProgramScanner.ExtractExecutablePath(program.UninstallString));

        foreach (var item in _items)
        {
            var executavel = Normalizado(ProgramScanner.ExtractExecutablePath(item.Command));
            if (executavel.Length == 0) continue;

            if (desinstalador.Length > 0
                && string.Equals(executavel, desinstalador, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (pasta.Length == 0) continue;

            // A pasta do executavel da entrada de inicializacao tem que estar
            // dentro da pasta de instalacao do programa.
            var exePasta = PastaDoExecutavel(executavel);
            if (exePasta.Length == 0) continue;

            if (string.Equals(exePasta, pasta, StringComparison.OrdinalIgnoreCase)) return true;
            if (exePasta.StartsWith(pasta + "\\", StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    /// <summary>
    /// A pasta que contem o executavel.
    /// </summary>
    private static string PastaDoExecutavel(string caminho)
    {
        try
        {
            var pasta = Path.GetDirectoryName(caminho);
            return (pasta ?? string.Empty).TrimEnd('\\');
        }
        catch (ArgumentException)
        {
            // Caminho invalido: nao ha pasta que casar, e a entrada de
            // inicializacao simplesmente nao casa com este programa.
            return string.Empty;
        }
    }

    /// <summary>
    /// O caminho sem aspas em volta e sem barra solta no fim.
    /// </summary>
    private static string Normalizado(string? caminho)
    {
        if (string.IsNullOrWhiteSpace(caminho)) return string.Empty;
        return caminho.Trim().Trim('"').TrimEnd('\\');
    }
}
