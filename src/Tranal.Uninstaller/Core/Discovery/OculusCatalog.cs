using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// Um aplicativo conhecido do ecossistema Oculus/Meta VR.
/// </summary>
/// <param name="Name">O nome do aplicativo.</param>
/// <param name="PackageId">O identificador do pacote, quando existe.</param>
/// <param name="Publisher">O editor.</param>
public sealed record OculusApp(string Name, string PackageId, string Publisher);

/// <summary>
/// O catalogo de aplicativos Oculus/Meta VR conhecidos.
///
/// A classificacao do desinstalador (<see cref="UninstallerKind.Oculus"/>)
/// so diz COMO o aplicativo foi instalado. Este catalogo diz QUEM ele e:
/// o Revo Uninstaller Pro reconhece os aplicativos do Oculus e os trata
/// como um grupo proprio, porque eles nao se comportam como um programa
/// comum — a loja do Oculus e o "Oculus Client" controlam a instalacao.
///
/// A regra mora aqui, em um lugar so: a lista e o relatorio consomem este
/// catalogo e nunca montam a lista por conta propria.
/// </summary>
public static class OculusCatalog
{
    /// <summary>
    /// Os aplicativos conhecidos do ecossistema Oculus/Meta VR.
    /// </summary>
    public static IReadOnlyList<OculusApp> All { get; } = new[]
    {
        new OculusApp("Oculus", "com.oculus.quest", "Oculus VR, LLC"),
        new OculusApp("Oculus Client", "com.oculus.quest", "Oculus VR, LLC"),
        new OculusApp("Oculus Store", "com.oculus.quest", "Oculus VR, LLC"),
        new OculusApp("Oculus Tray", "com.oculus.quest", "Oculus VR, LLC"),
        new OculusApp("Oculus Media", "com.oculus.quest", "Oculus VR, LLC"),
        new OculusApp("Oculus Remote Desktop", "com.oculus.quest", "Oculus VR, LLC"),
        new OculusApp("Oculus Link", "com.oculus.quest", "Oculus VR, LLC"),
        new OculusApp("Oculus Dash", "com.oculus.quest", "Oculus VR, LLC"),
        new OculusApp("Oculus Mirror", "com.oculus.quest", "Oculus VR, LLC"),
        new OculusApp("Meta Quest Link", "com.meta.questlink", "Meta Platforms, Inc."),
        new OculusApp("Meta Quest Remote Desktop", "com.meta.questremotedesktop", "Meta Platforms, Inc."),
        new OculusApp("Meta Quest Link (PC)", "com.meta.questlink", "Meta Platforms, Inc."),
        new OculusApp("Meta Horizon Link", "com.meta.horizon", "Meta Platforms, Inc."),
        new OculusApp("Meta Viewer", "com.meta.viewer", "Meta Platforms, Inc."),
        new OculusApp("Meta XR Simulator", "com.meta.xrsimulator", "Meta Platforms, Inc."),
        new OculusApp("Meta XR Simulator Hub", "com.meta.xrsimulator", "Meta Platforms, Inc."),
        new OculusApp("Meta Quest Developer Hub", "com.meta.questdevhub", "Meta Platforms, Inc."),
        new OculusApp("Meta Presence Platform", "com.meta.presence", "Meta Platforms, Inc."),
        new OculusApp("Meta Haptics SDK", "com.meta.haptics", "Meta Platforms, Inc."),
    };

    /// <summary>
    /// As expressoes que identificam um aplicativo do ecossistema Oculus/Meta
    /// VR pelo nome, quando o nome nao esta no catalogo.
    /// </summary>
    private static readonly (string Pattern, string BaseApp)[] NamePatterns =
    {
        (@"\boculus\b", "Oculus"),
        (@"\bmeta\s+quest\b", "Meta Quest Link"),
        (@"\bmeta\s+xr\b", "Meta XR Simulator"),
        (@"\bmeta\s+viewer\b", "Meta Viewer"),
        (@"\bmeta\s+horizon\b", "Meta Horizon Link"),
        (@"\bquest\s+link\b", "Meta Quest Link"),
    };

    /// <summary>
    /// True quando o nome e de um aplicativo do ecossistema Oculus/Meta VR.
    /// </summary>
    /// <param name="name">O nome do programa.</param>
    public static bool IsOculusApp(string? name) => Match(name) is not null;

    /// <summary>
    /// O aplicativo do catalogo que corresponde ao nome, ou nulo quando o
    /// nome nao e de nenhum aplicativo conhecido.
    ///
    /// O casamento ignora a caixa e aceita tanto o nome do catalogo quanto o
    /// nome tecnico do pacote ("com.oculus.quest").
    /// </summary>
    /// <param name="name">O nome do programa.</param>
    public static OculusApp? Match(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        var text = name.Trim();

        foreach (var app in All)
        {
            if (string.Equals(app.Name, text, StringComparison.OrdinalIgnoreCase)) return app;

            if (app.PackageId.Length > 0
                && text.Contains(app.PackageId, StringComparison.OrdinalIgnoreCase))
                return app;
        }

        foreach (var (pattern, baseApp) in NamePatterns)
        {
            if (!Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                continue;

            foreach (var app in All)
            {
                if (string.Equals(app.Name, baseApp, StringComparison.OrdinalIgnoreCase)) return app;
            }

            return new OculusApp(baseApp, string.Empty, "Meta Platforms, Inc.");
        }

        return null;
    }
}
