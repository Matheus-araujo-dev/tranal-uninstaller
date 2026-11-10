using Tranal.Uninstaller.Core.Settings;

namespace Tranal.Uninstaller.Core.Appearance;

/// <summary>
/// O formato da lista de programas.
///
/// Ha dois formatos: a lista completa, com as colunas que o usuario escolheu,
/// e a lista compacta, que cabe mais programas na tela porque mostra so o
/// essencial.
///
/// Este e o unico lugar que decide quais colunas aparecem em cada formato. A
/// tela, a pre-visualizacao de impressao e a exportacao leem a mesma resposta
/// — e nao podem acabar discordando sobre o que e "compacto".
/// </summary>
public static class ProgramListLayout
{
    /// <summary>
    /// As colunas da lista compacta: nome, tamanho, versao e empresa. Sao as
    /// que respondem "o que e isso, quanto ocupa e de quem e".
    /// </summary>
    public static IReadOnlyList<ProgramColumn> CompactColumns { get; } = new[]
    {
        ProgramColumn.Name,
        ProgramColumn.Size,
        ProgramColumn.Version,
        ProgramColumn.Company,
    };

    /// <summary>A lista esta em modo compacto?</summary>
    public static bool IsCompact => AppSettingsStore.Current.CompactProgramList;

    /// <summary>
    /// As colunas do formato pedido.
    ///
    /// No completo, sao as colunas do catalogo menos as que o usuario
    /// escondeu. No compacto, sao as colunas essenciais — e uma coluna que o
    /// usuario escondeu continua escondida: o compacto reduz a lista, nao
    /// desobedece a escolha.
    /// </summary>
    /// <param name="compact">Verdadeiro para a lista compacta.</param>
    public static IReadOnlyList<ProgramColumn> ColumnsFor(bool compact)
    {
        var escondidas = AppSettingsStore.Current.HiddenColumns;

        if (!compact) return ProgramColumns.Visible(escondidas);

        return CompactColumns
            .Where(c => c == ProgramColumn.Name || !EstaEscondida(c, escondidas))
            .ToList();
    }

    /// <summary>
    /// As colunas que a lista mostra agora: o formato que esta valendo.
    ///
    /// E esta a resposta que a tela consome: existe uma so interpretacao de
    /// "quais colunas aparecem".
    /// </summary>
    public static IReadOnlyList<ProgramColumn> VisibleColumns() => ColumnsFor(IsCompact);

    /// <summary>
    /// Liga ou desliga o modo compacto.
    /// </summary>
    /// <param name="compact">Verdadeiro para ligar.</param>
    public static void SetCompact(bool compact) =>
        AppSettingsStore.Update(s => s.CompactProgramList = compact);

    /// <summary>True quando a coluna esta entre as escondidas.</summary>
    private static bool EstaEscondida(ProgramColumn coluna, IReadOnlyCollection<string>? escondidas) =>
        escondidas is not null
        && escondidas.Any(h => string.Equals(h, coluna.ToString(), StringComparison.OrdinalIgnoreCase));
}
