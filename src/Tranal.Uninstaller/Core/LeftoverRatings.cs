using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Tranal.Uninstaller.Core;

/// <summary>
/// A nota que a pessoa le da sobra: cinco niveis, do mais seguro ao mais
/// perigoso.
///
/// A confianca (<see cref="LeftoverConfidence"/>) mede a forca do vinculo
/// entre a sobra e o programa: sao tres niveis, e e ela que decide o que
/// fica marcado para remocao. A NOTA e o que a tela mostra, e ela junta a
/// confianca com o motivo do julgamento — e por isso que um item de
/// confianca media com nome exato e "Bom", enquanto o mesmo item com nome
/// so parecido e "Duvidoso".
///
/// O vocabulario e o do Bulk Crap Uninstaller 6.3.0: Muito bom, Bom,
/// Duvidoso, Mau e Desconhecido.
/// </summary>
public enum LeftoverRating
{
    /// <summary>Muito bom: e sobra do programa, com certeza.</summary>
    VeryGood,

    /// <summary>Bom: e sobra do programa, com bom grau de certeza.</summary>
    Good,

    /// <summary>Duvidoso: parece sobra do programa, mas nao e certeza.</summary>
    Doubtful,

    /// <summary>Mau: parece com o programa, mas pode ser outra coisa.</summary>
    Bad,

    /// <summary>Desconhecido: nao ha motivo registrado.</summary>
    Unknown,
}

/// <summary>
/// A regra da nota das sobras.
///
/// A REGRA mora aqui, em um lugar so: a tela, a linha de comando e os
/// avisos consomem esta nota e nunca calculam a sua.
/// </summary>
public static class LeftoverRatings
{
    /// <summary>
    /// Os motivos que, com confianca alta, provam que a sobra e do programa.
    /// </summary>
    private static readonly HashSet<LeftoverReason> MotivosCertos = new()
    {
        LeftoverReason.InstallFolder,
        LeftoverReason.ExplicitTarget,
        LeftoverReason.ExactNameMatch,
    };

    /// <summary>
    /// Os motivos que, com confianca media, deixam duvida: o nome apenas
    /// parece com o do programa.
    /// </summary>
    private static readonly HashSet<LeftoverReason> MotivosFracos = new()
    {
        LeftoverReason.SimilarNameMatch,
        LeftoverReason.SubfolderMatch,
        LeftoverReason.RegistrySecondLevel,
    };

    /// <summary>
    /// A nota de uma sobra.
    /// </summary>
    /// <param name="item">A sobra.</param>
    public static LeftoverRating Rate(LeftoverItem? item)
    {
        if (item is null) return LeftoverRating.Unknown;
        return Rate(item.Confidence, item.Reason);
    }

    /// <summary>
    /// A nota a partir da confianca e do motivo.
    ///
    /// A regra e uma tabela, e a tabela e esta:
    /// sem motivo, nao ha nota; com confianca alta e motivo certo, a nota e
    /// maxima; com confianca alta e outro motivo, a nota e boa; com
    /// confianca media e motivo fraco, a nota e duvidosa; com confianca
    /// media e outro motivo, a nota e boa; e com confianca baixa a nota e
    /// ruim, porque ai o vinculo com o programa e so uma semelhanca de nome.
    /// </summary>
    /// <param name="confidence">A confianca da sobra.</param>
    /// <param name="reason">O motivo do julgamento.</param>
    public static LeftoverRating Rate(LeftoverConfidence confidence, LeftoverReason reason)
    {
        if (reason == LeftoverReason.Unknown) return LeftoverRating.Unknown;

        if (confidence == LeftoverConfidence.High)
            return MotivosCertos.Contains(reason) ? LeftoverRating.VeryGood : LeftoverRating.Good;

        if (confidence == LeftoverConfidence.Medium)
            return MotivosFracos.Contains(reason) ? LeftoverRating.Doubtful : LeftoverRating.Good;

        return LeftoverRating.Bad;
    }

    /// <summary>
    /// True quando a nota dispensa o aviso de perigo.
    /// </summary>
    /// <param name="rating">A nota.</param>
    public static bool IsSafe(LeftoverRating rating) =>
        rating == LeftoverRating.VeryGood || rating == LeftoverRating.Good;

    /// <summary>
    /// True quando a nota e perigosa: o item parece com o programa, mas pode
    /// ser outra coisa.
    /// </summary>
    /// <param name="rating">A nota.</param>
    public static bool IsDangerous(LeftoverRating rating) => rating == LeftoverRating.Bad;

    /// <summary>
    /// Os itens cuja nota nao e segura: os duvidosos e os maus.
    /// </summary>
    /// <param name="leftovers">As sobras varridas.</param>
    public static IReadOnlyList<LeftoverItem> RiskyItems(IEnumerable<LeftoverItem>? leftovers) =>
        (leftovers ?? Enumerable.Empty<LeftoverItem>())
            .Where(i => i is not null && !IsSafe(Rate(i)))
            .ToList();

    /// <summary>
    /// As sobras marcadas cuja nota nao e segura: as que o usuario escolheu
    /// apagar mesmo assim, e que por isso precisam do aviso de perigo.
    /// </summary>
    /// <param name="leftovers">As sobras varridas.</param>
    public static IReadOnlyList<LeftoverItem> MarkedRiskyItems(IEnumerable<LeftoverItem>? leftovers) =>
        (leftovers ?? Enumerable.Empty<LeftoverItem>())
            .Where(i => i is not null && i.Selected && !IsSafe(Rate(i)))
            .ToList();
}
