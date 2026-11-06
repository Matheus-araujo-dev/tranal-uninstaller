using System.Text.RegularExpressions;

namespace Tranal.Uninstaller.Core.Rules;

/// <summary>
/// O que a regra decide sobre um programa.
/// </summary>
/// <remarks>
/// A ordem de precedencia entre as categorias e fixa e esta no motor
/// (<see cref="RuleEngine.Decide"/>): <c>Protect</c> vence <c>Exclude</c>,
/// que vence <c>Include</c>, que vence <c>Cleanup</c>. Duas regras que dizem
/// coisas opostas nunca podem deixar o resultado a criterio de quem leu o
/// arquivo antes.
/// </remarks>
public enum RuleCategory
{
    /// <summary>
    /// Inclui: o programa entra na desinstalacao mesmo sem o desinstalador
    /// nativo deixar rastro.
    /// </summary>
    Include,

    /// <summary>
    /// Exclui: o programa fica de fora da desinstalacao e da limpeza.
    /// </summary>
    Exclude,

    /// <summary>
    /// Protege: nem o programa nem as sobras dele podem ser tocados.
    /// </summary>
    Protect,

    /// <summary>
    /// Limpeza: as sobras do programa podem ser limpas normalmente.
    /// </summary>
    Cleanup,
}

/// <summary>
/// A quem a regra se aplica.
/// </summary>
public enum RuleScope
{
    /// <summary>A todos os programas.</summary>
    General,

    /// <summary>
    /// So ao programa nomeado em <see cref="RemovalRule.Application"/> — e a
    /// "base de regras por aplicativo" que o Wise mantem a mao.
    /// </summary>
    Application,
}

/// <summary>
/// Uma regra do arquivo de regras.
/// </summary>
/// <remarks>
/// O padrao e comparado, sem diferenciar maiusculas/minusculas, com o nome do
/// programa. Quando o padrao parece um caminho (tem barra invertida ou dois
/// pontos), ele e comparado com a pasta de instalacao e com o comando de
/// desinstalacao — e assim que se escreve uma regra por pasta.
/// </remarks>
public sealed class RemovalRule
{
    /// <summary>
    /// O texto comparado. Vazio: a regra nunca casa.
    /// </summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary>O que a regra decide.</summary>
    public RuleCategory Category { get; set; } = RuleCategory.Cleanup;

    /// <summary>A quem a regra se aplica.</summary>
    public RuleScope Scope { get; set; } = RuleScope.General;

    /// <summary>
    /// O nome do programa, quando a regra e por aplicativo. Vazio numa regra
    /// geral.
    /// </summary>
    public string Application { get; set; } = string.Empty;

    /// <summary>
    /// True quando o padrao e uma expressao regular.
    ///
    /// Uma expressao regular invalida nunca e aceita: a regra e ignorada e o
    /// motor avisa — nunca o programa inteiro deixa de ser julgado por causa
    /// de uma linha escrita errada.
    /// </summary>
    public bool IsRegex { get; set; }

    /// <summary>False deixa a regra desligada sem precisar apaga-la do arquivo.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Uma nota livre para o usuario saber por que a regra existe.</summary>
    public string Note { get; set; } = string.Empty;

    /// <summary>
    /// True quando a regra se aplica ao programa.
    /// </summary>
    /// <param name="program">O programa a julgar.</param>
    public bool Matches(InstalledProgram program)
    {
        if (!Enabled || program is null) return false;

        if (Scope == RuleScope.Application)
        {
            var nome = (Application ?? string.Empty).Trim();
            if (nome.Length == 0) return false;
            if (!string.Equals(nome, (program.DisplayName ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
                return false;
        }

        var padrao = (Pattern ?? string.Empty).Trim();
        if (padrao.Length == 0) return false;

        if (IsRegex)
        {
            try
            {
                return Regex.IsMatch(program.DisplayName ?? string.Empty, padrao,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }
            catch (ArgumentException)
            {
                // Expressao regular invalida: a regra nao casa. O erro e
                // reportado no carregamento, nao na hora de apagar algo.
                return false;
            }
        }

        if (LooksLikePath(padrao))
        {
            if (Contains(program.InstallLocation, padrao)) return true;
            if (Contains(program.UninstallString, padrao)) return true;
            return false;
        }

        return Contains(program.DisplayName, padrao);
    }

    /// <summary>True quando o texto parece um caminho (e nao um nome de programa).</summary>
    private static bool LooksLikePath(string value) =>
        value.Contains('\\') || value.Contains('/') || (value.Length > 1 && value[1] == ':');

    private static bool Contains(string? haystack, string needle)
    {
        if (string.IsNullOrWhiteSpace(haystack)) return false;
        return haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Uma linha que descreve a regra (para a lista da interface).</summary>
    public string Describe()
    {
        var alvo = Scope == RuleScope.Application && (Application ?? string.Empty).Length > 0
            ? Application
            : Pattern;

        var extra = IsRegex ? " (regex)" : string.Empty;
        return $"{Category}: {alvo}{extra}";
    }
}
