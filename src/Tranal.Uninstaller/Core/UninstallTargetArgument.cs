namespace Tranal.Uninstaller.Core;

/// <summary>
/// Argumento que entrega ao Tranal o alvo escolhido pelo usuario.
///
/// O menu de contexto do Explorer registra o comando do Tranal como
/// <c>--uninstall-target "&lt;caminho&gt;"</c>. A CLI e a GUI usam esta
/// mesma interpretacao: uma so regra para o mesmo argumento.
/// </summary>
public static class UninstallTargetArgument
{
    /// <summary>Nome do argumento.</summary>
    public const string Name = "--uninstall-target";

    /// <summary>
    /// Extrai o alvo dos argumentos informados. Devolve string vazia quando o
    /// argumento nao existe ou vem sem caminho.
    /// </summary>
    public static string Extract(IReadOnlyList<string>? args)
    {
        if (args is null) return string.Empty;

        for (var i = 0; i < args.Count; i++)
        {
            if (!string.Equals(args[i], Name, StringComparison.OrdinalIgnoreCase)) continue;
            if (i + 1 >= args.Count) return string.Empty;

            return (args[i + 1] ?? string.Empty).Trim().Trim('"');
        }

        return string.Empty;
    }
}
