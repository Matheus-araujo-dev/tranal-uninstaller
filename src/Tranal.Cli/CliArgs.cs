namespace Tranal.Cli;

/// <summary>
/// Ajuda da linha de comando.
///
/// A REGRA mora aqui, em um lugar so: a leitura dos argumentos e a
/// identificacao das opcoes sao feitas por estes metodos, e nenhum comando
/// reescreve a leitura por conta propria.
/// </summary>
public static class CliArgs
{
    /// <summary>
    /// O valor que vem logo depois de uma opcao.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    /// <param name="name">A opcao procurada (ex.: "/export").</param>
    /// <returns>O valor, ou null quando a opcao nao existe ou nao tem valor.</returns>
    public static string? ValueAfter(string[] args, string name)
    {
        if (args is null) return null;

        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }

    /// <summary>
    /// True quando a opcao foi pedida na linha de comando.
    /// </summary>
    /// <param name="flags">As opcoes reconhecidas.</param>
    /// <param name="name">A opcao procurada.</param>
    public static bool Has(HashSet<string> flags, string name) =>
        flags is not null && flags.Contains(name);
}
