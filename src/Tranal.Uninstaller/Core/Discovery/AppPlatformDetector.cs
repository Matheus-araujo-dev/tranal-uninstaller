using System.IO;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// A plataforma de origem de um programa (loja de jogos, gerenciador de
/// pacotes ou aplicacao portatil).
/// </summary>
public sealed class AppPlatform
{
    /// <summary>O identificador estavel da plataforma.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>O nome exibido da plataforma.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>O tipo: "Loja de jogos", "Gerenciador de pacotes" ou "Portatil".</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>True quando o programa veio de uma loja de jogos.</summary>
    public bool IsGameStore => Kind == "Loja de jogos";

    /// <summary>True quando o programa veio de um gerenciador de pacotes.</summary>
    public bool IsPackageManager => Kind == "Gerenciador de pacotes";
}

/// <summary>
/// Descobre de onde veio um programa instalado.
///
/// O Uninstalr mostra a origem de cada jogo (Steam, Epic, Riot, GOG...), e o
/// IObit avisa quando o programa e "bloatware" de loja. O Wise tambem marca
/// os programas que pertencem a um gerenciador de pacotes.
///
/// A REGRA mora aqui, em um lugar so: a lista de plataformas e as regras de
/// deteccao sao declaradas aqui, e quem precisa do dado consome o veredito.
/// </summary>
public static class AppPlatformDetector
{
    /// <summary>
    /// As plataformas conhecidas e os marcadores que as identificam.
    /// Os marcadores sao procurados no caminho de instalacao e no comando de
    /// desinstalacao.
    /// </summary>
    private static readonly (string Id, string Name, string Kind, string[] Markers)[] Platforms =
    {
        ("steam", "Steam", "Loja de jogos", new[] { "steamapps", "steam" }),
        ("epic", "Epic Games", "Loja de jogos", new[] { "epic games", "epicgameslauncher" }),
        ("riot", "Riot Games", "Loja de jogos", new[] { "riot games", "riotclient" }),
        ("gog", "GOG.com", "Loja de jogos", new[] { "gog.com", "gog galaxy", "gogcom" }),
        ("battlenet", "Battle.net", "Loja de jogos", new[] { "battle.net", "battlenet" }),
        ("ubisoft", "Ubisoft Connect", "Loja de jogos", new[] { "ubisoft connect", "ubisoft" }),
        ("origin", "EA app", "Loja de jogos", new[] { "ea app", "origin games", "electronic arts" }),
        ("wargaming", "Wargaming", "Loja de jogos", new[] { "wargaming", "games\\world_of_tanks" }),
        ("itch", "itch.io", "Loja de jogos", new[] { "itch.io", "itch" }),
        ("bigfish", "Big Fish Games", "Loja de jogos", new[] { "big fish games", "bigfishgames" }),
        ("microsoft", "Microsoft Store", "Loja de jogos", new[] { "windowsapps" }),
        ("chocolatey", "Chocolatey", "Gerenciador de pacotes", new[] { "chocolatey", "choco" }),
        ("scoop", "Scoop", "Gerenciador de pacotes", new[] { "scoop", "apps\\current" }),
        ("winget", "WinGet", "Gerenciador de pacotes", new[] { "winget" }),
        ("nuget", "NuGet", "Gerenciador de pacotes", new[] { "nuget", ".nuget\\packages" }),
        ("ninite", "Ninite", "Gerenciador de pacotes", new[] { "ninite" }),
        ("portableapps", "PortableApps", "Portatil", new[] { "portableapps", "portable" }),
    };

    /// <summary>
    /// As plataformas conhecidas.
    /// </summary>
    public static IReadOnlyList<AppPlatform> Known => Platforms
        .Select(p => new AppPlatform { Id = p.Id, Name = p.Name, Kind = p.Kind })
        .ToList();

    /// <summary>
    /// Descobre a plataforma de um programa, ou null quando ele nao veio de
    /// nenhuma loja nem gerenciador conhecido.
    /// </summary>
    /// <param name="program">O programa inspecionado.</param>
    public static AppPlatform? Detect(InstalledProgram? program)
    {
        if (program is null) return null;

        var texto = string.Join(" | ", new[]
        {
            program.InstallLocation ?? string.Empty,
            program.UninstallString ?? string.Empty,
            program.DisplayIcon ?? string.Empty,
            program.RegistryPath ?? string.Empty,
        }).ToLowerInvariant();

        if (texto.Length == 0) return null;

        foreach (var (id, name, kind, markers) in Platforms)
        {
            foreach (var marcador in markers)
            {
                if (texto.Contains(marcador, StringComparison.Ordinal))
                {
                    return new AppPlatform { Id = id, Name = name, Kind = kind };
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Descobre a plataforma de varios programas.
    /// </summary>
    /// <param name="programs">Os programas inspecionados.</param>
    public static IReadOnlyList<(InstalledProgram Program, AppPlatform? Platform)> DetectMany(
        IEnumerable<InstalledProgram>? programs)
    {
        if (programs is null) return Array.Empty<(InstalledProgram, AppPlatform?)>();

        return programs
            .Where(p => p is not null)
            .Select(p => (p, Detect(p)))
            .ToList();
    }
}
