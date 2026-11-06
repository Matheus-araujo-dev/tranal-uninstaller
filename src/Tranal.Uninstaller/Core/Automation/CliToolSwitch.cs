namespace Tranal.Uninstaller.Core.Automation;

/// <summary>
/// O switch de linha de comando de uma ferramenta.
///
/// Uma ferramenta (limpeza de disco, limpador de registro, desinstalador,
/// backup, snapshot, restauração) tem duas formas de ser chamada na linha
/// de comando: o switch curto ("-j") e o nome longo ("--temporarios"). A
/// forma longa com o sufixo <c>RUN</c> ("--temporarios-run") é a que
/// **executa** a ferramenta — o resto só a descreve.
///
/// O catálogo é dado, não código: ele mora no núcleo e a linha de comando
/// consome. Escrever a lista de switches dentro do programa da CLI faria a
/// ajuda e a execução discordarem na primeira troca.
/// </summary>
public sealed class CliToolSwitch
{
    /// <summary>
    /// A identidade da ferramenta. Vem do catálogo de ferramentas do núcleo
    /// (<see cref="ToolCatalog"/>) quando a ferramenta é uma da barra
    /// lateral.
    /// </summary>
    public string ToolId { get; init; } = string.Empty;

    /// <summary>
    /// O switch curto, com o hífen ("-j").
    /// </summary>
    public string ShortSwitch { get; init; } = string.Empty;

    /// <summary>
    /// O nome longo, com os dois hífens ("--temporarios").
    /// </summary>
    public string LongName { get; init; } = string.Empty;

    /// <summary>
    /// O que a ferramenta faz, em uma frase. É o que a ajuda da linha de
    /// comando mostra.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// O sufixo da forma que executa a ferramenta. É o <c>RUN</c> do HiBit.
    /// </summary>
    public string RunSuffix { get; init; } = "RUN";

    /// <summary>
    /// A forma longa que executa a ferramenta ("--temporarios-run").
    /// </summary>
    public string LongRunName => LongName + "-" + RunSuffix.ToLowerInvariant();

    /// <summary>
    /// O switch curto sem o hífen, para a comparação.
    /// </summary>
    public string ShortName => ShortSwitch.TrimStart('-', '/');

    /// <summary>
    /// O nome longo sem os hífens, para a comparação.
    /// </summary>
    public string LongNameOnly => LongName.TrimStart('-', '/');
}

/// <summary>
/// O catálogo de switches de linha de comando por ferramenta.
///
/// A CLI consome esta lista; nada aqui depende da CLI. É o mesmo motivo do
/// catálogo de ferramentas do núcleo: uma ferramenta é uma ferramenta, e o
/// lugar de dizer isso é um lugar só.
/// </summary>
public static class CliToolSwitchCatalog
{
    /// <summary>
    /// O sufixo da forma que executa a ferramenta. É o <c>RUN</c> do HiBit:
    /// o nome da ferramenta em versal, como comando.
    /// </summary>
    public const string RunSuffix = "RUN";

    /// <summary>
    /// Todos os switches, na ordem em que a ajuda deve mostrá-los.
    /// </summary>
    public static IReadOnlyList<CliToolSwitch> All { get; } = new[]
    {
        new CliToolSwitch
        {
            ToolId = ToolCatalog.JunkCleaner,
            ShortSwitch = "-j",
            LongName = "--temporarios",
            Description = "Limpeza de arquivos temporários e lixo do disco.",
        },
        new CliToolSwitch
        {
            ToolId = ToolCatalog.RegistryOptimizer,
            ShortSwitch = "-r",
            LongName = "--registro",
            Description = "Limpeza do registro (entradas obsoletas do Menu Iniciar).",
        },
        new CliToolSwitch
        {
            ToolId = "Desinstalador",
            ShortSwitch = "-u",
            LongName = "--desinstalar",
            Description = "Desinstalador de programas, com varredura de sobras.",
        },
        new CliToolSwitch
        {
            ToolId = "DesinstalacaoForcada",
            ShortSwitch = "-f",
            LongName = "--forcado",
            Description = "Desinstalação forçada pelo alvo apontado pelo usuário.",
        },
        new CliToolSwitch
        {
            ToolId = "Backup",
            ShortSwitch = "-b",
            LongName = "--backup",
            Description = "Gerenciador de backups (pontos de restauração).",
        },
        new CliToolSwitch
        {
            ToolId = "Restaurar",
            ShortSwitch = "-x",
            LongName = "--restaurar",
            Description = "Restauração do sistema a partir de um backup.",
        },
        new CliToolSwitch
        {
            ToolId = "Snapshot",
            ShortSwitch = "-s",
            LongName = "--snapshot",
            Description = "Fotografia do sistema (snapshot), para comparar depois.",
        },
        new CliToolSwitch
        {
            ToolId = ToolCatalog.AutorunManager,
            ShortSwitch = "-a",
            LongName = "--inicializacao",
            Description = "Programas que sobem junto com o Windows.",
        },
        new CliToolSwitch
        {
            ToolId = ToolCatalog.HistoryCleaner,
            ShortSwitch = "-h",
            LongName = "--historico",
            Description = "Limpeza de rastros (histórico, cookies e caches).",
        },
        new CliToolSwitch
        {
            ToolId = ToolCatalog.EvidenceRemover,
            ShortSwitch = "-e",
            LongName = "--evidencias",
            Description = "Remoção de rastros e evidências do uso do computador.",
        },
        new CliToolSwitch
        {
            ToolId = ToolCatalog.UnrecoverableDelete,
            ShortSwitch = "-d",
            LongName = "--irrecuperavel",
            Description = "Exclusão irrecuperável de arquivos.",
        },
        new CliToolSwitch
        {
            ToolId = ToolCatalog.WindowsTools,
            ShortSwitch = "-w",
            LongName = "--ferramentas",
            Description = "Ferramentas do Windows (painéis e utilitários).",
        },
    };

    /// <summary>
    /// Descobre a ferramenta de um switch.
    ///
    /// Aceita o switch curto ("-j", "/j"), o nome longo ("--temporarios") e a
    /// forma longa que executa ("--temporarios-run", "--temporarios RUN").
    /// A comparação não distingue maiúsculas de minúsculas.
    ///
    /// Ferramenta desconhecida não é exceção: devolve <c>false</c> e uma
    /// mensagem que diz o que existe, porque o erro de digitação é do
    /// usuário e a ajuda faz parte do conserto.
    /// </summary>
    /// <param name="token">O switch informado na linha de comando.</param>
    /// <param name="tool">A ferramenta encontrada (nula quando não há).</param>
    /// <param name="run">True quando o switch pede para executar a ferramenta.</param>
    /// <param name="error">A mensagem de erro (vazia quando o switch é válido).</param>
    public static bool TryResolve(
        string? token,
        out CliToolSwitch? tool,
        out bool run,
        out string error)
    {
        tool = null;
        run = false;
        error = string.Empty;

        var texto = (token ?? string.Empty).Trim();
        if (texto.Length == 0)
        {
            error = "nenhum switch informado";
            return false;
        }

        // A forma "--temporarios RUN" chega em dois pedaços.
        var partes = texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length > 1
            && string.Equals(partes[^1], RunSuffix, StringComparison.OrdinalIgnoreCase))
        {
            run = true;
            texto = string.Join(' ', partes[..^1]);
        }

        var nome = texto.Trim().TrimStart('-', '/');
        if (nome.Length == 0)
        {
            error = $"switch inválido: '{token}'";
            return false;
        }

        if (nome.EndsWith("-" + RunSuffix.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)
            || nome.EndsWith("_" + RunSuffix.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
        {
            run = true;
            nome = nome[..(nome.Length - RunSuffix.Length - 1)];
        }

        foreach (var item in All)
        {
            if (string.Equals(nome, item.ShortName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(nome, item.LongNameOnly, StringComparison.OrdinalIgnoreCase))
            {
                tool = item;
                return true;
            }
        }

        error = $"ferramenta desconhecida: '{token}'. "
              + $"Ferramentas disponíveis: {string.Join(", ", All.Select(t => t.LongName))}";

        return false;
    }

    /// <summary>
    /// A ajuda das ferramentas, uma linha por ferramenta, com o switch
    /// curto, o nome longo e a forma que executa.
    /// </summary>
    public static IReadOnlyList<string> DescribeAll() =>
        All.Select(t => $"{t.ShortSwitch} / {t.LongName} / {t.LongRunName} — {t.Description}").ToList();
}
