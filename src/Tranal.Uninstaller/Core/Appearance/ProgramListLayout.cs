using Tranal.Uninstaller.Core.Settings;

namespace Tranal.Uninstaller.Core.Appearance;

/// <summary>
/// O formato da lista de programas.
///
/// Ha dois formatos: a lista completa, com todas as colunas, e a lista
/// compacta, que cabe mais programas na tela porque mostra so o essencial.
///
/// Este e o unico lugar que decide quais colunas aparecem em cada formato. A
/// tela e a exportacao leem a mesma resposta — e nao podem acabar discordando
/// sobre o que e "compacto".
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
    /// As colunas do formato pedido. No compacto, a resposta e sempre a
    /// mesma — e a completa, as colunas do catalogo.
    /// </summary>
    /// <param name="compact">Verdadeiro para a lista compacta.</param>
    public static IReadOnlyList<ProgramColumn> ColumnsFor(bool compact) =>
        compact ? CompactColumns : ProgramColumns.All.Select(c => c.Id).ToList();

    /// <summary>
    /// O modo compacto liga ou desliga o modo compacto.
    /// </summary>
    /// <param name="compact">Verdadeiro para ligar.</param>
    public static void SetCompact(bool compact) =>
        AppSettingsStore.Update(s => s.CompactProgramList = compact);
}
