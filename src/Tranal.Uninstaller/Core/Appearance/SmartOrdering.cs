using Tranal.Uninstaller.Core;

namespace Tranal.Uninstaller.Core.Appearance;

/// <summary>
/// A ordem em que os programas sao desinstalados.
///
/// A ordem "barulhenta primeiro" vem do Revo Uninstaller Pro 5.5.4: os
/// desinstaladores que abrem janela vao PRIMEIRO, enquanto o usuario ainda
/// esta attentive, e os silenciosos depois, em lote.
///
/// A regra mora aqui, em um lugar so: a fila da CLI e o assistente da
/// interface grafica consomem esta classe.
/// </summary>
public static class SmartOrdering
{
    /// <summary>
    /// Ordena os programas para desinstalacao.
    /// </summary>
    /// <param name="programs">Os programas a ordenar.</param>
    /// <param name="loudFirst">
    /// Quando true (padrao), os desinstaladores que exigem interacao vem
    /// primeiro. Quando false, o contrario.
    /// </param>
    public static IReadOnlyList<InstalledProgram> Order(
        IEnumerable<InstalledProgram> programs, bool loudFirst = true)
    {
        if (programs is null) return Array.Empty<InstalledProgram>();

        return programs
            .Where(p => p is not null)
            .OrderBy(p => loudFirst ? LoudRank(p) : -LoudRank(p))
            .ThenByDescending(p => p.SizeBytes)
            .ThenBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Verdadeiro quando a desinstalacao do programa vai mostrar janela: nao
    /// ha comando silencioso nem derivado nem registrado.
    /// </summary>
    /// <param name="program">O programa inspecionado.</param>
    public static bool IsLoud(InstalledProgram program)
    {
        if (program is null) return false;

        if (!string.IsNullOrWhiteSpace(program.QuietUninstallCommand)) return false;
        if (!string.IsNullOrWhiteSpace(program.QuietUninstallString)) return false;

        // Um desinstalador derivado do registro que roda sem interface
        // (msiexec /qn) tambem e silencioso.
        return program.Kind != UninstallerKind.MsiExec;
    }

    /// <summary>
    /// A posicao do programa na ordem: 0 e barulhento (vai primeiro), 1 e
    /// silencioso (vai depois).
    /// </summary>
    private static int LoudRank(InstalledProgram program) => IsLoud(program) ? 0 : 1;
}
