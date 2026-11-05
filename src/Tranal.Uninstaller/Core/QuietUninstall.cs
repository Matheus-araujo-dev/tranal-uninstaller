using System.Text.RegularExpressions;

namespace Tranal.Uninstaller.Core;

/// <summary>
/// O comando de **desinstalação silenciosa** de um programa.
///
/// Nem todo desinstalador aceita ser silencioso, e cada família de
/// empacotador usa um switch diferente. Como o Tranal já classifica o tipo
/// do desinstalador (<see cref="UninstallerKind"/>), o switch pode ser
/// escolhido pelo tipo em vez de ser adivinhado.
///
/// A tabela é o comportamento **observável** de três concorrentes que
/// mantêm a mesma lista de switches por família — o Bulk Crap Uninstaller
/// 6.3.0, o Uninstalr 3.2 e o HiBit Uninstaller 4.0.10 (ver
/// <c>docs/auditoria/</c>). Nenhum deles publica a tabela; ela foi
/// reconstruída das strings dos binários e do comportamento documentado
/// de cada empacotador.
///
/// **O que a tabela NÃO é:** não é um volume de "desinstalar sem perguntar"
/// para qualquer programa. Um switch errado não é neutro — pode fazer o
/// desinstalador rodar com opções que o usuário não pediu. Por isso o
/// Tranal **não** troca o desinstalador nativo por conta própria: ele
/// <em>oferece</em> o comando silencioso, e o usuário escolhe.
/// </summary>
public static class QuietUninstall
{
    /// <summary>
    /// O InstallShield desinstala por <c>setup.exe /x{GUID}</c>; a forma
    /// silenciosa é outra, <c>setup.exe /s /v"/qn"</c>. Não é um switch
    /// para acrescentar — é um comando para substituir.
    /// </summary>
    private static readonly Regex InstallShieldUninstall =
        new(@"/x(\{[0-9A-Fa-f-]{36}\})?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// O switch silencioso da família do desinstalador, ou <c>null</c>
    /// quando a família não tem um switch conhecido.
    ///
    /// | Família | Switch |
    /// |---|---|
    /// | Inno Setup | <c>/VERYSILENT /SUPPRESSMSGBOXES /NORESTART</c> |
    /// | NSIS | <c>/S</c> |
    /// | Windows Installer | <c>/quiet /qn /norestart</c> |
    /// | InstallShield | <c>/s /v"/qn"</c> (substitui o <c>/x</c>) |
    /// | SDBInst | <c>/S</c> |
    /// </summary>
    /// <param name="kind">O tipo do desinstalador.</param>
    public static string? SwitchFor(UninstallerKind kind) => kind switch
    {
        UninstallerKind.InnoSetup => "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART",
        UninstallerKind.Nsis => "/S",
        UninstallerKind.MsiExec => "/quiet /qn /norestart",
        UninstallerKind.InstallShield => "/s /v\"/qn\"",
        UninstallerKind.SdbInst => "/S",
        _ => null,
    };

    /// <summary>
    /// Monta o comando silencioso do programa, ou devolve string vazia
    /// quando não há um jeito conhecido de silenciar aquele desinstalador.
    ///
    /// A ordem de escolha é:
    /// <list type="number">
    /// <item>O valor <c>QuietUninstallString</c> do registro — quando
    /// existe, ele já é a resposta certa e não há por que montar nada.</item>
    /// <item>O switch da família, acrescentado ao comando registrado.</item>
    /// </list>
    /// </summary>
    /// <param name="program">O programa.</param>
    public static string Build(InstalledProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);

        // O proprio registro sabe: quando existe QuietUninstallString, ela
        // e a resposta certa.
        if (!string.IsNullOrWhiteSpace(program.QuietUninstallString))
            return program.QuietUninstallString.Trim();

        var command = (program.UninstallString ?? string.Empty).Trim();
        if (command.Length == 0) return string.Empty;

        var kind = program.Kind == UninstallerKind.Unknown
            ? UninstallerClassifier.Classify(program)
            : program.Kind;

        var silent = SwitchFor(kind);
        if (silent is null) return string.Empty;

        // O InstallShield nao aceita o switch em conjunto: o "/x{GUID}" do
        // comando de desinstalacao e substituido pela forma silenciosa.
        if (kind == UninstallerKind.InstallShield)
        {
            if (!InstallShieldUninstall.IsMatch(command)) return string.Empty;

            return InstallShieldUninstall.Replace(command, "/s /v\"/qn\"");
        }

        // Ja silencioso? Nao acrescentar nada.
        foreach (var token in silent.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (command.Contains(token, StringComparison.OrdinalIgnoreCase)) return command;
        }

        return $"{command} {silent}";
    }

    /// <summary>
    /// True quando o programa tem um comando silencioso conhecido.
    /// </summary>
    /// <param name="program">O programa.</param>
    public static bool Supports(InstalledProgram program) => Build(program).Length > 0;
}
