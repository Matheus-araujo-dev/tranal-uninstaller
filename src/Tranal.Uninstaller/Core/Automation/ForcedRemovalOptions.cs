namespace Tranal.Uninstaller.Core.Automation;

/// <summary>
/// As duas desinstalações que o usuário aponta com o dedo.
///
/// A **forçada** é o alvo apontado: a pasta ou o arquivo que o usuário
/// escolheu é o item garantido da operação, e a varredura acha o resto.
///
/// A **personalizada** é a seleção por palavra-chave: o usuário digita
/// "Adobe" e quer os programas cujo nome tem a palavra "Adobe" — não os
/// que apenas contêm essas letras. "AdobeFresco" não é "Adobe".
///
/// O casamento por palavra é o do núcleo (<see cref="NameMatch.ContainsWord"/>),
/// o mesmo que protege a varredura de sobras de falsos positivos. Aqui ele
/// não é reimplementado: seria a segunda interpretação do mesmo casamento.
/// </summary>
public sealed class ForcedRemovalOptions
{
    /// <summary>
    /// O caminho apontado pelo usuário (desinstalação forçada).
    /// Vazio quando a operação não é forçada.
    /// </summary>
    public string? TargetPath { get; init; }

    /// <summary>
    /// A palavra-chave usada para escolher os programas (desinstalação
    /// personalizada). Vazio quando a operação não é por palavra-chave.
    /// </summary>
    public string? Keyword { get; init; }

    /// <summary>True quando o alvo foi apontado pelo usuário.</summary>
    public bool IsForced => !string.IsNullOrWhiteSpace(TargetPath);

    /// <summary>True quando a seleção é por palavra-chave.</summary>
    public bool IsCustom => !string.IsNullOrWhiteSpace(Keyword);

    /// <summary>
    /// Opções de uma desinstalação forçada pelo alvo apontado.
    /// </summary>
    /// <param name="targetPath">O caminho apontado pelo usuário.</param>
    public static ForcedRemovalOptions ForTarget(string? targetPath) => new()
    {
        TargetPath = (targetPath ?? string.Empty).Trim(),
    };

    /// <summary>
    /// Opções de uma desinstalação personalizada por palavra-chave.
    /// </summary>
    /// <param name="keyword">A palavra-chave.</param>
    public static ForcedRemovalOptions ForKeyword(string? keyword) => new()
    {
        Keyword = (keyword ?? string.Empty).Trim(),
    };

    /// <summary>
    /// True quando o nome do programa tem a palavra-chave.
    ///
    /// O casamento é por palavra inteira: "Adobe" casa com "Adobe Reader" e
    /// com "Reader Adobe", mas não com "AdobeFresco". A decisão é a do
    /// núcleo, para que a lista de sobras e a seleção por palavra não
    /// discordem sobre o que é a mesma palavra.
    /// </summary>
    /// <param name="programName">O nome do programa.</param>
    /// <param name="keyword">A palavra-chave.</param>
    public static bool MatchesKeyword(string? programName, string? keyword)
    {
        if (string.IsNullOrWhiteSpace(programName) || string.IsNullOrWhiteSpace(keyword)) return false;
        return NameMatch.ContainsWord(programName.Trim(), keyword.Trim());
    }

    /// <summary>
    /// Os programas cujo nome tem a palavra-chave.
    /// </summary>
    /// <param name="programs">Os programas instalados.</param>
    /// <param name="keyword">A palavra-chave.</param>
    public static IReadOnlyList<InstalledProgram> SelectByKeyword(
        IEnumerable<InstalledProgram> programs,
        string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return Array.Empty<InstalledProgram>();

        return programs.Where(p => MatchesKeyword(p.DisplayName, keyword)).ToList();
    }

    /// <summary>
    /// Os itens que esta operação remove.
    ///
    /// Na desinstalação forçada, o alvo apontado é o item garantido — e a
    /// varredura (`ScanForced`) o traz marcado. Na desinstalação
    /// personalizada, os itens são os da varredura normal do programa.
    /// </summary>
    /// <param name="program">O programa sendo desinstalado.</param>
    /// <param name="scanOptions">As opções da varredura.</param>
    public IReadOnlyList<LeftoverItem> Resolve(
        InstalledProgram program,
        LeftoverScanOptions? scanOptions = null)
    {
        var opts = scanOptions ?? LeftoverScanOptions.Default;

        if (IsForced)
            return LeftoverScanner.ScanForced(TargetPath!, opts);

        return LeftoverScanner.Scan(program, opts);
    }
}
