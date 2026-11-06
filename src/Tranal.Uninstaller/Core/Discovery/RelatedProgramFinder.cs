using Tranal.Uninstaller.Core;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// Por que um programa foi considerado relacionado a outro.
/// </summary>
public enum RelatedProgramReason
{
    /// <summary>Os dois foram instalados pelo mesmo fabricante.</summary>
    SamePublisher,

    /// <summary>Os dois dividem a mesma pasta de instalacao.</summary>
    SameInstallLocation,

    /// <summary>Os dois apontam para o mesmo executavel de desinstalacao.</summary>
    SameUninstaller,

    /// <summary>
    /// Os dois tem o mesmo nome base (o mesmo produto em versoes diferentes,
    /// ou o mesmo produto em entradas duplicadas do registro).
    /// </summary>
    SameProduct,
}

/// <summary>
/// Um programa relacionado a outro, com o motivo da relacao.
/// </summary>
/// <param name="Name">O nome do programa relacionado.</param>
/// <param name="Reason">O motivo da relacao.</param>
public sealed record RelatedProgram(string Name, RelatedProgramReason Reason);

/// <summary>
/// Encontra os programas que provavelmenteinstalled junto com um outro.
///
/// O Revo Uninstaller Pro 5.5.4 mostra, na tela de detalhes, a lista de
/// "programas relacionados" — os que o usuario provavelmente quer remover na
/// mesma operacao. A evidencia de que esse recurso existe no produto de
/// referencia esta no proprio binario dele: a secao
/// "[Uninstaller Toolbar]" da tabela de idioma do Revo Pro traz a entrada
/// de texto do comando.
///
/// A regra mora aqui, em um lugar so: a tela de detalhes e a CLI consomem
/// este metodo e nunca montam a lista por conta propria.
/// </summary>
public static class RelatedProgramFinder
{
    /// <summary>
    /// Os programas relacionados ao programa informado.
    /// </summary>
    /// <param name="program">O programa de referencia.</param>
    /// <param name="allPrograms">Todos os programas instalados.</param>
    public static IReadOnlyList<RelatedProgram> Find(
        InstalledProgram program, IEnumerable<InstalledProgram>? allPrograms)
    {
        if (program is null || allPrograms is null) return Array.Empty<RelatedProgram>();

        var myPublisher = Normalize(program.Publisher);
        var myLocation = PathUtil.Normalize(program.InstallLocation);
        var myUninstaller = ProgramScanner.ExtractExecutablePath(program.UninstallString);
        var myBase = BaseName(program.DisplayName);

        var result = new List<RelatedProgram>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var other in allPrograms)
        {
            if (other is null) continue;
            if (ReferenceEquals(other, program)) continue;

            // A mesma entrada lida duas vezes (a lista pode vir de varias
            // fontes) nao e um programa vizinho.
            if (!string.IsNullOrEmpty(other.RegistryPath)
                && !string.IsNullOrEmpty(program.RegistryPath)
                && string.Equals(other.RegistryPath, program.RegistryPath, StringComparison.OrdinalIgnoreCase))
                continue;

            var reason = ReasonFor(program, other, myPublisher, myLocation, myUninstaller, myBase);
            if (reason is null) continue;

            var name = string.IsNullOrWhiteSpace(other.DisplayName) ? other.RegistryKey : other.DisplayName.Trim();
            if (name.Length == 0) continue;
            if (!seen.Add(name)) continue;

            result.Add(new RelatedProgram(name, reason.Value));
        }

        // A ordem e estavel: primeiro o motivo mais forte (mesmo produto),
        // depois fabricante, depois pasta, depois desinstalador.
        return result
            .OrderBy(r => (int)r.Reason)
            .ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// O motivo pelo qual os dois programas sao relacionados, ou nulo quando
    /// nao sao.
    /// </summary>
    private static RelatedProgramReason? ReasonFor(
        InstalledProgram program,
        InstalledProgram other,
        string myPublisher,
        string myLocation,
        string myUninstaller,
        string myBase)
    {
        var theirBase = BaseName(other.DisplayName);
        if (myBase.Length >= 3 && theirBase.Length >= 3
            && string.Equals(myBase, theirBase, StringComparison.OrdinalIgnoreCase))
            return RelatedProgramReason.SameProduct;

        var theirPublisher = Normalize(other.Publisher);
        if (myPublisher.Length >= 3 && string.Equals(myPublisher, theirPublisher, StringComparison.OrdinalIgnoreCase))
            return RelatedProgramReason.SamePublisher;

        var theirLocation = PathUtil.Normalize(other.InstallLocation);
        if (myLocation.Length > 0
            && string.Equals(myLocation, theirLocation, StringComparison.OrdinalIgnoreCase))
            return RelatedProgramReason.SameInstallLocation;

        var theirUninstaller = ProgramScanner.ExtractExecutablePath(other.UninstallString);
        if (myUninstaller.Length > 0
            && string.Equals(myUninstaller, theirUninstaller, StringComparison.OrdinalIgnoreCase))
            return RelatedProgramReason.SameUninstaller;

        return null;
    }

    /// <summary>
    /// O nome-base do produto, sem a versao: "Acme Suite 3.1" e "Acme Suite"
    /// sao o mesmo produto.
    /// </summary>
    private static string BaseName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return string.Empty;
        var name = displayName.Trim();

        // remove a versao final: "Nome 3.1.2", "Nome (2)", "Nome v2"
        var version = System.Text.RegularExpressions.Regex.Match(name, @"[\s]+[vV]?\d+(\.\d+)*\s*$");
        if (version.Success) name = name[..version.Index].Trim();

        var parenthesized = System.Text.RegularExpressions.Regex.Match(name, @"\s*\([^)]*\)\s*$");
        if (parenthesized.Success) name = name[..parenthesized.Index].Trim();

        return name;
    }

    /// <summary>Normaliza um texto para comparacao: minusculas e sem espacos extras.</summary>
    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
}
