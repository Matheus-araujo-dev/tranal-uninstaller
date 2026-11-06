using Tranal.Uninstaller.Core;

namespace Tranal.Uninstaller.Core.Localization;

/// <summary>
/// Os textos localizados dos avisos de desinstalacao.
///
/// A interface grafica e a CLI consomem esta classe. Quando o texto do aviso
/// vivesse em cada uma das duas, elas divergiriam na primeira traducao
/// esquecida — e a regra do projeto e que a interpretacao fique em um lugar so.
/// </summary>
public static class AdvisoryLabels
{
    /// <summary>O titulo curto do aviso.</summary>
    /// <param name="kind">O tipo do aviso.</param>
    public static string Title(UninstallAdvisoryKind kind) => Loc.Get(kind switch
    {
        UninstallAdvisoryKind.SharedInstallLocation => "Advisory.Title.SharedInstallLocation",
        UninstallAdvisoryKind.SystemComponent => "Advisory.Title.SystemComponent",
        UninstallAdvisoryKind.ProtectedByRegistry => "Advisory.Title.ProtectedByRegistry",
        UninstallAdvisoryKind.UninstallerMissing => "Advisory.Title.UninstallerMissing",
        UninstallAdvisoryKind.UninstallerBroken => "Advisory.Title.UninstallerBroken",
        UninstallAdvisoryKind.RelatedPrograms => "Advisory.Title.RelatedPrograms",
        _ => "Advisory.Title.UserData",
    });

    /// <summary>
    /// O aviso de programas que dividem a pasta de instalacao.
    /// </summary>
    /// <param name="programs">Os nomes dos programas vizinhos.</param>
    public static string SharedInstallLocation(IReadOnlyList<string> programs) =>
        Loc.Get("Advisory.SharedInstallLocation", string.Join(", ", programs));

    /// <summary>
    /// O aviso de remocao de componente do sistema.
    /// </summary>
    public static string SystemComponent() => Loc.Get("Advisory.SystemComponent");

    /// <summary>
    /// O aviso de pastas que podem conter dados do usuario.
    /// </summary>
    /// <param name="folders">Os caminhos das pastas.</param>
    public static string UserData(IReadOnlyList<string> folders) =>
        Loc.Get("Advisory.UserData", string.Join(", ", folders));

    /// <summary>
    /// O aviso de entrada protegida pelo registro.
    /// </summary>
    public static string ProtectedByRegistry() => Loc.Get("Advisory.ProtectedByRegistry");

    /// <summary>
    /// O aviso de programa sem desinstalador registrado.
    /// </summary>
    public static string UninstallerMissing() => Loc.Get("Advisory.UninstallerMissing");

    /// <summary>
    /// O aviso de desinstalador corrompido.
    /// </summary>
    public static string UninstallerBroken() => Loc.Get("Advisory.UninstallerBroken");

    /// <summary>
    /// O aviso de programas relacionados.
    /// </summary>
    /// <param name="programs">Os nomes dos programas relacionados.</param>
    public static string RelatedPrograms(IReadOnlyList<string> programs) =>
        Loc.Get("Advisory.RelatedPrograms", string.Join(", ", programs));
}
