using Tranal.Uninstaller.Core;

namespace Tranal.Uninstaller.Core.Localization;

/// <summary>
/// Os rotulos localizados da lista de sobras.
///
/// A interface grafica e a CLI consomem este metodo. Quando o texto do
/// motivo vivesse em cada uma das duas, elas divergiriam na primeira
/// traducao esquecida — e a regra do projeto e que a interpretacao fique
/// em um lugar so.
/// </summary>
public static class LeftoverLabels
{
    /// <summary>O rotulo do nivel de confianca ("Alta", "Media", "Baixa").</summary>
    /// <param name="confidence">A confianca da sobra.</param>
    public static string Confidence(LeftoverConfidence confidence) => Loc.Get(confidence switch
    {
        LeftoverConfidence.High => "Common.High",
        LeftoverConfidence.Medium => "Common.Medium",
        _ => "Common.Low",
    });

    /// <summary>
    /// A frase que explica por que o item e sobra do programa.
    ///
    /// A chave e derivada do nome do membro do enum: um motivo novo no enum
    /// sem a frase correspondente no catalogo apareceria em branco na lista.
    /// </summary>
    /// <param name="reason">O motivo do julgamento.</param>
    public static string Reason(LeftoverReason reason) => Loc.Get(reason switch
    {
        LeftoverReason.InstallFolder => "Common.Reason.InstallFolder",
        LeftoverReason.ExplicitTarget => "Common.Reason.ExplicitTarget",
        LeftoverReason.ExactNameMatch => "Common.Reason.ExactNameMatch",
        LeftoverReason.VariantNameMatch => "Common.Reason.VariantNameMatch",
        LeftoverReason.SimilarNameMatch => "Common.Reason.SimilarNameMatch",
        LeftoverReason.SubfolderMatch => "Common.Reason.SubfolderMatch",
        LeftoverReason.RegistrySecondLevel => "Common.Reason.RegistrySecondLevel",
        LeftoverReason.ObsoleteStartMenu => "Common.Reason.ObsoleteStartMenu",
        LeftoverReason.ObsoleteShortcut => "Common.Reason.ObsoleteShortcut",
        LeftoverReason.History => "Common.Reason.History",
        LeftoverReason.ProcessTermination => "Common.Reason.ProcessTermination",
        LeftoverReason.InstallerLog => "Common.Reason.InstallerLog",
        LeftoverReason.StartupEntry => "Common.Reason.StartupEntry",
        LeftoverReason.Unknown => "Common.Reason.Unknown",
        _ => "Common.Reason.Unknown",
    });
}
