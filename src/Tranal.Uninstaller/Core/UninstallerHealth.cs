using System.IO;

namespace Tranal.Uninstaller.Core;

/// <summary>
/// A saude do desinstalador registrado de um programa.
///
/// Tres estados, e a distincao importa porque cada um leva a uma acao
/// diferente:
///
///   - <see cref="Ok"/>: ha desinstalador e o arquivo apontado existe;
///   - <see cref="Missing"/>: nao ha comando de desinstalador — a entrada so
///     pode ser limpa na mao;
///   - <see cref="Broken"/>: ha comando, mas o arquivo apontado nao existe —
///     o desinstalador foi removido ou nunca foi instalado.
///
/// O estado e um dado, nao um texto: a interface grafica e a CLI recebem o
/// mesmo objeto e cada uma escreve do seu jeito.
/// </summary>
public enum UninstallerHealth
{
    /// <summary>Ha desinstalador registrado e o arquivo apontado existe.</summary>
    Ok,

    /// <summary>Nao ha comando de desinstalador registrado.</summary>
    Missing,

    /// <summary>
    /// Ha comando de desinstalador, mas o arquivo apontado nao existe no disco.
    /// </summary>
    Broken,
}

/// <summary>
/// Avalia a saude do desinstalador registrado.
///
/// A regra mora aqui, em um lugar so: quem precisa saber se o desinstalador
/// funciona chama este metodo, e nao recalcula a conta por conta própria.
///
/// Um comando so e considerado quebrado quando aponta para um caminho
/// ABSOLUTO que nao existe. Comando sem caminho absoluto ("MsiExec.exe /X{...}")
/// e resolvido pelo proprio Windows e nao prova nada sobre o disco — marcar
/// todo MSI como quebrado seria mentira.
/// </summary>
public static class UninstallerHealthEvaluator
{
    /// <summary>
    /// Avalia o desinstalador do programa.
    /// </summary>
    /// <param name="program">O programa inspecionado.</param>
    /// <param name="exists">
    /// Como perguntar se um arquivo existe. Por padrao, <see cref="File.Exists"/>.
    /// Existe para os testes nao dependerem do disco.
    /// </param>
    public static UninstallerHealth Evaluate(InstalledProgram program, Func<string, bool>? exists = null)
    {
        if (program is null) return UninstallerHealth.Missing;

        var command = (program.UninstallString ?? string.Empty).Trim();
        if (command.Length == 0) return UninstallerHealth.Missing;

        var executable = ProgramScanner.ExtractExecutablePath(command);
        if (executable.Length == 0) return UninstallerHealth.Missing;

        // Caminho sem raiz e resolvido pelo PATH do Windows: nao prova
        // ausencia de arquivo. Mas so quando parece mesmo um programa
        // ("MsiExec.exe"); um switch solto ("/S") nao e desinstalador.
        if (!IsRooted(executable)) return LooksLikeProgram(executable) ? UninstallerHealth.Ok : UninstallerHealth.Missing;

        var check = exists ?? File.Exists;
        return check(executable) ? UninstallerHealth.Ok : UninstallerHealth.Broken;
    }

    /// <summary>
    /// Verdadeiro quando o texto parece o nome de um executavel (tem extensao
    /// de programa) em vez de um parametro solto.
    /// </summary>
    private static bool LooksLikeProgram(string path)
    {
        var extension = Path.GetExtension(path);
        if (extension.Length < 2) return false;

        return extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".com", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bat", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verdadeiro quando o desinstalador registrado nao pode ser executado
    /// porque o arquivo apontado nao existe.
    /// </summary>
    /// <param name="program">O programa inspecionado.</param>
    /// <param name="exists">Como perguntar se um arquivo existe.</param>
    public static bool IsBroken(InstalledProgram program, Func<string, bool>? exists = null) =>
        Evaluate(program, exists) == UninstallerHealth.Broken;

    /// <summary>
    /// Verdadeiro quando o caminho tem raiz de unidade (ex.: "C:\Program Files\...").
    /// </summary>
    private static bool IsRooted(string path) =>
        path.Length >= 3 && path[1] == ':' && char.IsLetter(path[0]);
}
