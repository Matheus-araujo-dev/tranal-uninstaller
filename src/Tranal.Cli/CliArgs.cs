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

    /// <summary>
    /// O valor que vem na posicao pedida depois da opcao.
    ///
    /// E a regra de leitura de varios valores: a primeira coisa depois da
    /// opcao e o valor 0, a seguinte e o valor 1, e assim por diante.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    /// <param name="name">A opcao procurada (ex.: "/add").</param>
    /// <param name="posicao">0 = o valor logo depois da opcao.</param>
    /// <returns>O valor, ou null quando a opcao nao existe ou nao tem o valor.</returns>
    public static string? ValuesAfter(string[] args, string name, int posicao)
    {
        if (args is null) return null;
        if (posicao < 0) return null;

        for (var i = 0; i < args.Length - 1; i++)
        {
            if (!args[i].Equals(name, StringComparison.OrdinalIgnoreCase)) continue;

            var indice = i + 1 + posicao;
            if (indice >= args.Length) return null;

            // A lista de valores da opcao acaba na proxima opcao: se um dos
            // valores ate a posicao pedida for outra opcao, o valor pedido
            // nao existe.
            for (var j = i + 1; j <= indice; j++)
            {
                if (args[j].StartsWith("/", StringComparison.Ordinal)) return null;
            }

            return args[indice];
        }

        return null;
    }


    public static bool Has(HashSet<string> flags, string name) =>
        flags is not null && flags.Contains(name);
}
