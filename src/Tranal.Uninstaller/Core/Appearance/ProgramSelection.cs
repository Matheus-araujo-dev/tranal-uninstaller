using System;
using System.Collections.Generic;
using System.Linq;

namespace Tranal.Uninstaller.Core.Appearance;

/// <summary>
/// A selecao em massa por pais.
///
/// O Uninstalr permite marcar (ou desmarcar) todos os programas de um
/// mesmo pais de uma vez. E o jeito de responder "eu quero tirar tudo do
/// pais X" sem clicar em cada linha.
///
/// A REGRA mora aqui, em um lugar so: quem mostra o menu e quem marca a
/// lista consomem estes metodos — a lista nunca decide sozinha o que e
/// "o mesmo pais".
/// </summary>
public static class ProgramSelection
{
    /// <summary>
    /// Os pais que aparecem na lista, em ordem alfabetica.
    ///
    /// O nome vem da tabela unica no idioma do app
    /// (<see cref="Localization.CountryNames"/>), e nao do `DisplayName`
    /// da cultura do Windows: o usuario ve o pais no mesmo idioma da
    /// interface.
    /// </summary>
    /// <param name="programas">Os programas da lista.</param>
    public static IReadOnlyList<string> Countries(IEnumerable<InstalledProgram>? programas)
    {
        if (programas is null) return Array.Empty<string>();

        return programas
            .Select(CountryOf)
            .Where(pais => pais.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(pais => pais, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Os programas de um mesmo pais.
    /// </summary>
    /// <param name="programas">Os programas da lista.</param>
    /// <param name="pais">O pais.</param>
    public static IReadOnlyList<InstalledProgram> ByCountry(
        IEnumerable<InstalledProgram>? programas, string? pais)
    {
        var nome = (pais ?? string.Empty).Trim();
        if (nome.Length == 0 || programas is null) return Array.Empty<InstalledProgram>();

        return programas
            .Where(p => string.Equals(CountryOf(p), nome, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// O pais de um programa, no idioma da interface (vazio quando nao ha).
    /// </summary>
    /// <param name="programa">O programa.</param>
    private static string CountryOf(InstalledProgram? programa)
    {
        if (programa is null) return string.Empty;

        var nome = ProgramColumns.CountryOf(programa);
        return nome ?? string.Empty;
    }
}
