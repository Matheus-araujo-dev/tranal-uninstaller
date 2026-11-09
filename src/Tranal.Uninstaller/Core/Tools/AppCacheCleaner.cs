using System.IO;

namespace Tranal.Uninstaller.Core.Tools;

/// <summary>
/// Uma pasta de cache de um programa.
/// </summary>
/// <param name="ProgramName">O programa dono do cache.</param>
/// <param name="Path">A pasta.</param>
/// <param name="SizeBytes">O que ocupa a pasta.</param>
/// <param name="Kind">O tipo de cache.</param>
public sealed record AppCacheEntry(
    string ProgramName,
    string Path,
    long SizeBytes,
    string Kind);

/// <summary>
/// A limpeza do cache que cada programa deixa atras.
///
/// Todo programa guarda cache: a tela de Miniaturas, o cache do navegador, o
/// cache do proprio aplicativo. Sao pastas que podem ser apagadas e que o
/// programa recria quando precisa — por isso a limpeza e segura.
///
/// A regra esta em um lugar so: este modulo sabe quais pastas contam como
/// cache e como medir o que elas ocupam. A tela e a linha de comando
/// consultam a mesma lista.
///
/// A limpeza nunca apaga nada sem confirmacao: quem apaga e o motor, com o
/// caminho marcado.
/// </summary>
public static class AppCacheCleaner
{
    /// <summary>
    /// Os nomes de pasta que o Windows e os navegadores usam para cache.
    /// </summary>
    private static readonly string[] CacheFolderNames =
    {
        "Cache",
        "Code Cache",
        "GPUCache",
        "ShaderCache",
        "cache",
        "Caches",
        "Temp",
        "Logs",
    };

    /// <summary>
    /// As pastas de cache dos programas instalados, na pasta local do
    /// usuario.
    /// </summary>
    /// <param name="programs">Os programas varridos.</param>
    public static IReadOnlyList<AppCacheEntry> Scan(IEnumerable<InstalledProgram>? programs)
    {
        var roots = RootsOf(programs);

        var entries = new List<AppCacheEntry>();

        foreach (var (programName, root) in roots)
        {
            foreach (var entry in ScanFolder(programName, root))
            {
                entries.Add(entry);
            }
        }

        return entries
            .OrderByDescending(e => e.SizeBytes)
            .ToList();
    }

    /// <summary>
    /// As pastas de cache de um programa.
    /// </summary>
    /// <param name="program">O programa.</param>
    public static IReadOnlyList<AppCacheEntry> ScanOf(InstalledProgram? program)
    {
        if (program is null) return Array.Empty<AppCacheEntry>();

        return RootsOf(new[] { program })
            .SelectMany(r => ScanFolder(r.ProgramName, r.Root))
            .ToList();
    }

    /// <summary>
    /// Mede o que uma pasta ocupa.
    /// </summary>
    /// <param name="path">A pasta.</param>
    public static long Measure(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return 0;

        try
        {
            if (!Directory.Exists(path)) return 0;

            long total = 0;
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    total += new FileInfo(file).Length;
                }
                catch
                {
                    // Arquivo trancado ou sumido no meio da conta.
                }
            }

            return total;
        }
        catch
        {
            return 0;
        }
    }

    private static IReadOnlyList<(string ProgramName, string Root)> RootsOf(IEnumerable<InstalledProgram>? programs)
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        var roots = new List<(string ProgramName, string Root)>();

        foreach (var program in (programs ?? Array.Empty<InstalledProgram>()).Where(p => p is not null))
        {
            var publisher = (program.Publisher ?? string.Empty).Trim();
            var name = (program.DisplayName ?? string.Empty).Trim();

            if (publisher.Length == 0 || name.Length == 0) continue;

            foreach (var basePath in new[] { local, roaming })
            {
                if (string.IsNullOrWhiteSpace(basePath)) continue;

                var folder = Path.Combine(basePath, publisher, name);
                if (Directory.Exists(folder)) roots.Add((name, folder));
            }
        }

        return roots;
    }

    private static IEnumerable<AppCacheEntry> ScanFolder(string programName, string root)
    {
        var entries = new List<AppCacheEntry>();

        try
        {
            foreach (var folder in Directory.EnumerateDirectories(root))
            {
                var nome = Path.GetFileName(folder);

                if (!CacheFolderNames.Contains(nome, StringComparer.OrdinalIgnoreCase)) continue;

                entries.Add(new AppCacheEntry(
                    programName,
                    folder,
                    Measure(folder),
                    nome));
            }
        }
        catch
        {
            // Pasta inacessivel: sem cache mensurado.
        }

        return entries;
    }
}
