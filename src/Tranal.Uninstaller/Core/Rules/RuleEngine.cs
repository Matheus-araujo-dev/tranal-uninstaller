using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tranal.Uninstaller.Core.Rules;

/// <summary>
/// O que o motor decidiu sobre um programa.
/// </summary>
public enum RuleDecision
{
    /// <summary>Nenhuma opiniao: o motor nao tem regra que case.</summary>
    None,

    /// <summary>Incluir na desinstalacao.</summary>
    Include,

    /// <summary>Deixar de fora.</summary>
    Exclude,

    /// <summary>Proteger: nem o programa nem as sobras podem ser tocados.</summary>
    Protect,

    /// <summary>Limpar as sobras normalmente.</summary>
    Cleanup,
}

/// <summary>
/// O motor de regras: o arquivo de regras e a base de regras por aplicativo.
///
/// A REGRA FICA AQUI. Quem decide — a varredura de sobras, a limpeza de
/// sujeira, a desinstalacao — apenas CONSUME este julgamento; nenhum outro
/// lugar reinterpreta o arquivo de regras.
///
/// Precedencia (fixa, documentada em <see cref="Decide"/>):
/// 1. a protecao embutida do sistema vence tudo;
/// 2. dentro de uma categoria, <c>Protect</c> &gt; <c>Exclude</c> &gt;
///    <c>Include</c> &gt; <c>Cleanup</c>;
/// 3. dentro da mesma categoria, a regra do aplicativo vence a regra geral;
/// 4. empate final: a primeira regra da lista vence.
/// </summary>
public sealed class RuleEngine
{
    private readonly List<RemovalRule> _rules = new();

    /// <summary>
    /// Substitui a pasta do arquivo de regras.
    ///
    /// Existe para a suite de testes: sem ele, o teste escreveria no arquivo
    /// de regras do usuario.
    /// </summary>
    internal static string? FolderOverride { get; set; }

    /// <summary>
    /// Substitui o caminho do arquivo de regras (so a suite de testes).
    /// </summary>
    internal static string? FilePathOverride { get; set; }

    /// <summary>A pasta onde ficam os dados do Tranal.</summary>
    public static string DataFolder =>
        FolderOverride ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Tranal Group");

    /// <summary>O caminho do arquivo de regras.</summary>
    public static string FilePath =>
        FilePathOverride ?? Path.Combine(DataFolder, "rules.json");

    /// <summary>As regras carregadas.</summary>
    public IReadOnlyList<RemovalRule> Rules => _rules;

    /// <summary>
    /// O motor com as regras embutidas (sem arquivo). E o que o Tranal usa
    /// quando ainda nao existe arquivo de regras.
    /// </summary>
    public static RuleEngine Default => BuiltinRules.Create();

    /// <summary>
    /// O motor em uso: carregado do arquivo de regras uma vez e reaproveitado.
    ///
    /// Quem decide e o consumidor do julgamento — a varredura de sobras, a
    /// limpeza — e nao o motor: o motor nao sabe nada sobre varredura.
    /// </summary>
    public static RuleEngine Current { get; private set; } = Default;

    /// <summary>
    /// Recarrega o motor do arquivo de regras e passa a usar o resultado.
    /// </summary>
    /// <returns>O motor carregado e os avisos do carregamento.</returns>
    public static (RuleEngine Engine, IReadOnlyList<string> Warnings) Reload()
    {
        var (engine, avisos) = Load();
        Current = engine;
        return (engine, avisos);
    }

    /// <summary>
    /// Volta o motor em uso para as regras embutidas (so a suite de testes).
    /// </summary>
    internal static void ResetForTests() => Current = Default;

    /// <summary>
    /// Carrega o motor do arquivo de regras, juntando as regras embutidas.
    ///
    /// Um arquivo ilegivel nunca derruba o motor: as regras embutidas
    /// continuam valendo e o arquivo problematico e preservado com a
    /// extensão <c>.corrompido</c> — o mesmo cuidado do
    /// <see cref="ProgramCustomizationStore"/>.
    /// </summary>
    /// <param name="path">O caminho do arquivo. Nulo: o caminho canonico.</param>
    /// <returns>O motor carregado e a lista de avisos do carregamento.</returns>
    public static (RuleEngine Engine, IReadOnlyList<string> Warnings) Load(string? path = null)
    {
        var engine = Default;
        var avisos = new List<string>();
        var arquivo = path ?? FilePath;

        string texto;
        try
        {
            if (!File.Exists(arquivo)) return (engine, avisos);
            texto = File.ReadAllText(arquivo, EncodingUtf8);
        }
        catch (Exception ex)
        {
            avisos.Add($"O arquivo de regras nao pode ser lido: {ex.Message}");
            return (engine, avisos);
        }

        if (string.IsNullOrWhiteSpace(texto)) return (engine, avisos);

        List<RemovalRule>? lidas;
        try
        {
            lidas = JsonSerializer.Deserialize<List<RemovalRule>>(texto, JsonOptions);
        }
        catch (JsonException ex)
        {
            Preservar(arquivo);
            avisos.Add($"O arquivo de regras esta ilegivel e foi preservado como .corrompido: {ex.Message}");
            return (engine, avisos);
        }

        if (lidas is null) return (engine, avisos);

        foreach (var regra in lidas)
        {
            if (regra is null) continue;

            if (regra.IsRegex && !RegexValida(regra.Pattern))
            {
                avisos.Add($"Expressao regular invalida ignorada: {regra.Pattern}");
                continue;
            }

            engine._rules.Add(regra);
        }

        return (engine, avisos);
    }

    private static bool RegexValida(string? padrao)
    {
        if (string.IsNullOrWhiteSpace(padrao)) return false;
        try
        {
            _ = new System.Text.RegularExpressions.Regex(padrao);
            return true;
        }
        catch (ArgumentException) { return false; }
    }

    /// <summary>
    /// Salva as regras no arquivo (gravacao atomica: temporario e troca).
    /// </summary>
    /// <param name="path">O caminho do arquivo. Nulo: o caminho canonico.</param>
    public bool Save(string? path = null)
    {
        var arquivo = path ?? FilePath;

        try
        {
            var pasta = Path.GetDirectoryName(arquivo);
            if (!string.IsNullOrEmpty(pasta)) Directory.CreateDirectory(pasta);

            var texto = JsonSerializer.Serialize(_rules, JsonOptions);
            var temporario = arquivo + ".tmp";

            File.WriteAllText(temporario, texto, EncodingUtf8);
            if (File.Exists(arquivo)) File.Delete(arquivo);
            File.Move(temporario, arquivo);

            return true;
        }
        catch (Exception)
        {
            Preservar(arquivo);
            return false;
        }
    }

    /// <summary>
    /// Acrescenta uma regra ao motor.
    /// </summary>
    /// <param name="rule">A regra nova.</param>
    public void Add(RemovalRule rule)
    {
        if (rule is null) return;
        _rules.Add(rule);
    }

    /// <summary>
    /// Decide o que fazer com o programa.
    ///
    /// </summary>
    /// <param name="program">O programa a julgar.</param>
    /// <param name="fallback">
    /// O que fazer quando nenhuma regra casa. Por padrao, a limpeza normal
    /// das sobras.
    /// </param>
    public RuleDecision Decide(InstalledProgram program, RuleDecision fallback = RuleDecision.Cleanup)
    {
        if (program is null) return RuleDecision.None;

        // A protecao do sistema vence tudo: nao e opiniao do arquivo de
        // regras, e a regra do proprio Windows.
        if (IsProtectedBySystem(program)) return RuleDecision.Protect;

        RemovalRule? vencedora = null;
        foreach (var regra in _rules)
        {
            if (!regra.Matches(program)) continue;
            if (vencedora is null || Vence(regra, vencedora)) vencedora = regra;
        }

        return vencedora is null ? fallback : DecisaoDe(vencedora.Category);
    }

    /// <summary>
    /// True quando o programa esta protegido: pelo sistema ou por uma regra
    /// de protecao.
    /// </summary>
    /// <param name="program">O programa a julgar.</param>
    public bool IsProtected(InstalledProgram program) => Decide(program) == RuleDecision.Protect;

    /// <summary>
    /// A regra que venceu o julgamento (para a interface mostrar o porque).
    /// </summary>
    /// <param name="program">O programa a julgar.</param>
    public RemovalRule? Winner(InstalledProgram program)
    {
        if (program is null) return null;

        RemovalRule? vencedora = null;
        foreach (var regra in _rules)
        {
            if (!regra.Matches(program)) continue;
            if (vencedora is null || Vence(regra, vencedora)) vencedora = regra;
        }

        return vencedora;
    }

    /// <summary>
    /// True quando uma regra protege ou exclui este caminho.
    ///
    /// E o MESMO arquivo de regras, lido pelo limpador de sujeira: uma regra
    /// escrita como caminho protege a pasta e o que esta dentro dela. Sem
    /// esta porta, o limpador teria o proprio julgamento de "isto e lixo" e
    /// as regras do usuario nao valeriam para ele.
    /// </summary>
    /// <param name="path">O caminho do item.</param>
    public bool IsPathProtected(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        foreach (var regra in _rules)
        {
            if (!regra.Enabled) continue;
            if (regra.Category is not (RuleCategory.Protect or RuleCategory.Exclude)) continue;

            var padrao = (regra.Pattern ?? string.Empty).Trim();
            if (padrao.Length == 0) continue;

            if (path.Contains(padrao, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    /// <summary>Uma linha que explica a decisao (para a interface e para o log).</summary>
    public string Describe(InstalledProgram program)
    {
        if (program is null) return RuleDecision.None.ToString();

        var regra = Winner(program);
        if (regra is null)
        {
            return IsProtectedBySystem(program)
                ? "Protegido pelas regras do sistema"
                : "Sem regra que case";
        }

        return $"{regra.Describe()}" + (regra.Note.Length > 0 ? $" — {regra.Note}" : string.Empty);
    }

    /// <summary>
    /// True quando o Windows proibe tocar no programa.
    /// </summary>
    private static bool IsProtectedBySystem(InstalledProgram program)
    {
        var local = (program.InstallLocation ?? string.Empty).Trim();
        if (local.Length > 0 && SafetyRules.IsProtectedFolder(local)) return true;

        var chave = (program.RegistryPath ?? string.Empty).Trim();
        if (chave.Length > 0 && SafetyRules.IsProtectedRegistryKey(chave)) return true;

        return false;
    }

    /// <summary>
    /// True quando a regra candidata vence a atual.
    /// </summary>
    private static bool Vence(RemovalRule candidata, RemovalRule atual)
    {
        var prioridadeCandidata = Prioridade(candidata.Category);
        var prioridadeAtual = Prioridade(atual.Category);
        if (prioridadeCandidata != prioridadeAtual) return prioridadeCandidata > prioridadeAtual;

        // Mesma categoria: a regra do aplicativo vence a regra geral.
        if (candidata.Scope != atual.Scope)
            return candidata.Scope == RuleScope.Application;

        // Empate: fica a primeira da lista.
        return false;
    }

    private static int Prioridade(RuleCategory category) => category switch
    {
        RuleCategory.Protect => 4,
        RuleCategory.Exclude => 3,
        RuleCategory.Include => 2,
        RuleCategory.Cleanup => 1,
        _ => 0,
    };

    private static RuleDecision DecisaoDe(RuleCategory category) => category switch
    {
        RuleCategory.Protect => RuleDecision.Protect,
        RuleCategory.Exclude => RuleDecision.Exclude,
        RuleCategory.Include => RuleDecision.Include,
        RuleCategory.Cleanup => RuleDecision.Cleanup,
        _ => RuleDecision.None,
    };

    private static void Preservar(string arquivo)
    {
        try
        {
            if (!File.Exists(arquivo)) return;
            var corrompido = arquivo + ".corrompido";
            if (File.Exists(corrompido)) File.Delete(corrompido);
            File.Move(arquivo, corrompido);
        }
        catch { /* preservar e o maximo que se pode fazer aqui */ }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly System.Text.Encoding EncodingUtf8 = new UTF8Encoding(false);
}
