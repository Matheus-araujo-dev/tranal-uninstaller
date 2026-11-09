
namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// Uma categoria de programa que veio junto com o Windows e raramente e
/// usada.
/// </summary>
public sealed class BloatwareCategory
{
    /// <summary>O identificador estavel da categoria.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>O nome exibido da categoria.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>A explicacao de por que o programa esta na categoria.</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Os marcadores que identificam a categoria: o nome do programa ou da
    /// pasta precisa conter um deles.
    /// </summary>
    public IReadOnlyList<string> Markers { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Um programa classificado como "instalado junto" (bloatware).
/// </summary>
public sealed class BloatwareEntry
{
    /// <summary>O programa instalado.</summary>
    public InstalledProgram Program { get; init; } = new();

    /// <summary>A categoria em que ele caiu.</summary>
    public BloatwareCategory Category { get; init; } = new();

    /// <summary>True quando o programa e da propria Microsoft.</summary>
    public bool IsMicrosoft { get; init; }
}

/// <summary>
/// Detecta os programas que vieram junto com o Windows e quase nunca sao
/// usados.
///
/// O IObit chama isso de "bloatware" e o ashampoo tambem: programas de
/// jogos, de aprendizado e do Windows que ocupam espaco e raramente
/// interessam a quem so quer desinstalar o que instalou.
///
/// A REGRA mora aqui, em um lugar so: as categorias e os marcadores que
/// identificam cada uma ficam aqui. Quem classifica consome o veredito.
///
/// Um programa so entra na lista quando o nome dele (ou a pasta) casa com um
/// marcador conhecido — e o que separa "veio junto" de "eh do sistema".
/// </summary>
public static class BundlewareDetector
{
    /// <summary>
    /// As categorias conhecidas.
    /// </summary>
    public static IReadOnlyList<BloatwareCategory> Categories { get; } = new[]
    {
        new BloatwareCategory
        {
            Id = "games",
            Name = "Jogos que vem com o Windows",
            Reason = "Jogo instalado com o Windows, quase nunca usado.",
            Markers = new[] { "Solitaire", "Minesweeper", "Jogo de cartas", "Mahjong", "Microsoft Solitaire", "Microsoft Minesweeper", "Microsoft Mahjong", "Microsoft Jogo" },
        },
        new BloatwareCategory
        {
            Id = "teaching",
            Name = "Apps de aprendizado",
            Reason = "App de aprendizado instalado com o Windows.",
            Markers = new[] { "SwiftKey", "Microsoft Family", "Microsoft Tips", "Microsoft Quick", "Get Started", "Microsoft Windows O", "First-run", "Windows Feedback" },
        },
        new BloatwareCategory
        {
            Id = "store",
            Name = "Apps da loja que vem por padrao",
            Reason = "App da loja instalado por padrao no Windows.",
            Markers = new[] { "Xbox", "XboxGameBar", "XboxGip", "XboxIdentity", "ZuneMusic", "ZuneVideo", "MixedReality", "Microsoft.MixedReality", "Bing", "Microsoft Bing" },
        },
        new BloatwareCategory
        {
            Id = "cortana",
            Name = "Assistente do Windows",
            Reason = "Assistente que hoje nao tem uso.",
            Markers = new[] { "Microsoft.Windows.Cortana", "Cortana", "Microsoft.Windows.Search" },
        },
        new BloatwareCategory
        {
            Id = "oem",
            Name = "Programas do fabricante",
            Reason = "Software que veio com o computador e raramente e usado.",
            Markers = new[] { "McAfee", "Norton", "Avast", "AVG", "Kaspersky", "ESET", "ByteDance", "Wondershare", "AOMEI", "Driver Update", "DriverUpdate", "PC Manager", "Lenovo", "Dell", "HP Inc", "Acer", " ASUS", "Realtek Audio Console", "Intel Graphics Command Center" },
        },
    };

    /// <summary>
    /// Classifica os programas instalados.
    /// </summary>
    /// <param name="programs">Os programas instalados.</param>
    public static IReadOnlyList<BloatwareEntry> Detect(IEnumerable<InstalledProgram>? programs)
    {
        var encontrados = new List<BloatwareEntry>();
        if (programs is null) return encontrados;

        foreach (var programa in programs)
        {
            if (programa is null) continue;

            var texto = string.Join(" | ", new[]
            {
                programa.DisplayName ?? string.Empty,
                programa.InstallLocation ?? string.Empty,
                programa.UninstallString ?? string.Empty,
            }).ToLowerInvariant();

            if (texto.Length == 0) continue;

            foreach (var categoria in Categories)
            {
                var casou = categoria.Markers.Any(m => texto.Contains(m.ToLowerInvariant(), StringComparison.Ordinal));
                if (!casou) continue;

                encontrados.Add(new BloatwareEntry
                {
                    Program = programa,
                    Category = categoria,
                    IsMicrosoft = IsMicrosoft(programa),
                });

                break;
            }
        }

        return encontrados;
    }

    /// <summary>
    /// True quando o programa e da propria Microsoft.
    /// </summary>
    /// <param name="program">O programa inspecionado.</param>
    public static bool IsMicrosoft(InstalledProgram? program)
    {
        if (program is null) return false;

        var publisher = program.Publisher ?? string.Empty;
        if (publisher.IndexOf("Microsoft", StringComparison.OrdinalIgnoreCase) >= 0) return true;

        var location = program.InstallLocation ?? string.Empty;
        return location.IndexOf("WindowsApps", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
