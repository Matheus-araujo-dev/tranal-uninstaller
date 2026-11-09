using Microsoft.Win32;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// Uma linha da tabela de caminhos de registro de um programa.
/// </summary>
/// <param name="ProgramName">O nome do programa.</param>
/// <param name="Path">O caminho da chave.</param>
/// <param name="IsRegistryKey">A chave e a chave de desinstalacao do proprio programa?</param>
/// <param name="Exists">A chave existe agora?</param>
/// <param name="ValueCount">Quantos valores a chave tem.</param>
/// <param name="SubKeyCount">Quantas subchaves a chave tem.</param>
public sealed record RegistryPathRow(
    string ProgramName,
    string Path,
    bool IsRegistryKey,
    bool Exists,
    int ValueCount,
    int SubKeyCount);

/// <summary>
/// A tabela de caminhos de registro por programa.
///
/// O Windows Installer e o Uninstall Tool guardam, para cada programa, a
/// lista de chaves de registro que pertencem a ele. E o que permite dizer
/// "este programa tem tres chaves no registro" — e o que permite limpar so
/// o que e dele.
///
/// A tabela aqui e **medida**: cada linha sai da chave de desinstalacao do
/// programa e das subchaves que o proprio Windows Installer criou a partir
/// dela. Nada e inferido do nome do programa.
///
/// A regra esta em um lugar so: este modulo sabe o que conta como caminho
/// de um programa. A tela e a linha de comando consomem a mesma tabela.
/// </summary>
public static class RegistryPathTable
{
    /// <summary>
    /// A tabela de caminhos dos programas informados.
    /// </summary>
    /// <param name="programs">Os programas varridos.</param>
    public static IReadOnlyList<RegistryPathRow> Build(IEnumerable<InstalledProgram>? programs)
    {
        var rows = new List<RegistryPathRow>();
        var lista = (programs ?? Array.Empty<InstalledProgram>()).Where(p => p is not null).ToList();

        foreach (var program in lista)
        {
            foreach (var row in RowsOf(program))
            {
                rows.Add(row);
            }
        }

        return rows;
    }

    /// <summary>
    /// Os caminhos de registro de um programa.
    /// </summary>
    /// <param name="program">O programa.</param>
    public static IReadOnlyList<RegistryPathRow> RowsOf(InstalledProgram? program)
    {
        if (program is null) return Array.Empty<RegistryPathRow>();

        var rows = new List<RegistryPathRow>();
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(program.RegistryPath))
        {
            AddRow(rows, vistos, program.DisplayName, program.RegistryPath, isRegistryKey: true, program.View);
        }

        // As subchaves da chave de desinstalacao: e o que o Windows Installer
        // cria para o proprio produto (ex.: ...\{GUID}).
        foreach (var sub in SubKeysOf(program))
        {
            AddRow(rows, vistos, program.DisplayName, sub, isRegistryKey: false, program.View);
        }

        return rows;
    }

    private static void AddRow(
        List<RegistryPathRow> rows,
        HashSet<string> vistos,
        string programName,
        string path,
        bool isRegistryKey,
        RegistryView view)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!vistos.Add(path)) return;

        var existe = false;
        var valores = 0;
        var subchaves = 0;

        try
        {
            var (hive, caminho) = Split(path);
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(caminho);

            if (key is not null)
            {
                existe = true;
                valores = key.GetValueNames().Length;
                subchaves = key.GetSubKeyNames().Length;
            }
        }
        catch
        {
            // Chave protegida ou inexistente: a linha existe, sem os numeros.
        }

        rows.Add(new RegistryPathRow(programName, path, isRegistryKey, existe, valores, subchaves));
    }

    private static IReadOnlyList<string> SubKeysOf(InstalledProgram program)
    {
        if (string.IsNullOrWhiteSpace(program.RegistryPath)) return Array.Empty<string>();

        var (hive, caminho) = Split(program.RegistryPath);
        var subchaves = new List<string>();

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, program.View);
            using var key = baseKey.OpenSubKey(caminho);
            if (key is null) return subchaves;

            foreach (var sub in key.GetSubKeyNames())
            {
                subchaves.Add(caminho.TrimEnd('\\') + "\\" + sub);
            }
        }
        catch
        {
            // Sem permissao: o programa nao tem linhas de subchave.
        }

        return subchaves;
    }

    /// <summary>
    /// Separa a chave em abelha e caminho. O caminho aceito e o formato do
    /// proprio Windows: <c>HKEY_LOCAL_MACHINE\SOFTWARE\...</c>.
    /// </summary>
    private static (RegistryHive Hive, string Path) Split(string path)
    {
        var text = (path ?? string.Empty).Trim();

        if (text.StartsWith("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase))
            return (RegistryHive.LocalMachine, text["HKEY_LOCAL_MACHINE".Length..].TrimStart('\\'));

        if (text.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase))
            return (RegistryHive.CurrentUser, text["HKEY_CURRENT_USER".Length..].TrimStart('\\'));

        if (text.StartsWith("HKEY_CLASSES_ROOT", StringComparison.OrdinalIgnoreCase))
            return (RegistryHive.ClassesRoot, text["HKEY_CLASSES_ROOT".Length..].TrimStart('\\'));

        if (text.StartsWith("HKEY_USERS", StringComparison.OrdinalIgnoreCase))
            return (RegistryHive.Users, text["HKEY_USERS".Length..].TrimStart('\\'));

        return (RegistryHive.CurrentUser, text);
    }
}
