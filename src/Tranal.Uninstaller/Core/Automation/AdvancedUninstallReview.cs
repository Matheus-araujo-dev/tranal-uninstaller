using Tranal.Uninstaller.Core.Modules;

namespace Tranal.Uninstaller.Core.Automation;

/// <summary>
/// Por que um candidato ficou fora do log de instalacao.
/// </summary>
public enum AdvancedUninstallReason
{
    /// <summary>
    /// O caminho nao aparece no log: o instalador nao registrou este item
    /// (26066, 26974 do Revo).
    /// </summary>
    NotLogged,

    /// <summary>
    /// O candidato esta dentro de um item que o log registrou: e conteudo do
    /// que o instalador gravou, e o log nao lista cada arquivo por dentro.
    /// </summary>
    InsideLoggedItem,
}

/// <summary>
/// Um candidato que o log de instalacao NAO registrou.
///
/// E o que o Revo avisa (strings.txt:26974): "There is some data that may
/// be related to the uninstalled program, but was not deleted because it
/// was not logged by Revo Uninstaller Pro during the installation of the
/// program. If you want to review and eventually to delete it press the
/// 'Advanced Uninstall' button".
/// </summary>
public sealed class AdvancedUninstallCandidate
{
    /// <summary>O item candidato encontrado no sistema.</summary>
    public LeftoverItem Item { get; init; } = null!;

    /// <summary>Por que o item nao esta no log.</summary>
    public AdvancedUninstallReason Reason { get; init; }

    /// <summary>
    /// A explicacao em portugues, para a tela. Va sempre junto com o
    /// motivo: um motivo sem frase nao diz nada ao usuario.
    /// </summary>
    public string Detail { get; init; } = string.Empty;

    /// <summary>
    /// O item que do log deu origem a este candidato (quando o motivo e
    /// "dentro de um item registrado"). Vazio nos demais casos.
    /// </summary>
    public LogEntry? RelatedEntry { get; init; }
}

/// <summary>
/// O resultado da revisao avancada: o que ficou de fora do log.
/// </summary>
public sealed class AdvancedUninstallReview
{
    /// <summary>
    /// Os candidatos que o log de instalacao nao registrou. E a lista que o
    /// "Advanced Uninstall" do Revo mostra para o usuario revisar.
    /// </summary>
    public IReadOnlyList<AdvancedUninstallCandidate> Unlogged { get; init; } = Array.Empty<AdvancedUninstallCandidate>();

    /// <summary>
    /// As entradas do log que casaram com algum candidato. E o contra
    /// ponto: o que o instalador gravou e que a varredura tambem achou.
    /// </summary>
    public IReadOnlyList<LogEntry> Logged { get; init; } = Array.Empty<LogEntry>();

    /// <summary>Quantos candidatos ficaram fora do log.</summary>
    public int UnloggedCount => Unlogged.Count;
}

/// <summary>
/// Cruza o log de instalacao com a lista de sobras candidatas e devolve o
/// que ficou FORA do log.
///
/// O Revo separa as duas coisas com clareza:
///
///   - o que o instalador registrou e o log manda apagar;
///   - o que sobrou e nao foi registrado nao entra no log — e por isso
///     nao e apagado automaticamente. O "Advanced Uninstall" (26066) existe
///     para o usuario revisar essa parte.
///
/// A regra do cruzamento mora aqui e em nenhum outro lugar: um candidato
/// esta no log quando o caminho e igual ao de uma entrada, ou quando esta
/// dentro de uma pasta/ chave que o log registrou.
/// </summary>
public static class AdvancedUninstallReviewBuilder
{
    /// <summary>
    /// Revisa os candidatos contra o log e devolve o que ficou de fora.
    /// </summary>
    /// <param name="install">O log de instalacao.</param>
    /// <param name="candidates">Os candidatos achados no sistema.</param>
    public static AdvancedUninstallReview Build(
        TracedInstall install,
        IEnumerable<LeftoverItem> candidates)
    {
        var entries = LogOperations.Entries(install);
        var unlogged = new List<AdvancedUninstallCandidate>();
        var logged = new List<LogEntry>();

        foreach (var candidate in candidates ?? Array.Empty<LeftoverItem>())
        {
            if (candidate is null) continue;

            var exact = MatchExact(entries, candidate.Path);
            if (exact is not null)
            {
                // O log registra este item: ele ja entra no plano de
                // remocao do log, e nao e sobra.
                logged.Add(exact);
                continue;
            }

            var parent = MatchInside(entries, candidate.Path);
            if (parent is not null)
            {
                // O log nao lista este arquivo, mas a pasta que o contem
                // esta no log: e conteudo do que o instalador gravou.
                unlogged.Add(new AdvancedUninstallCandidate
                {
                    Item = candidate,
                    Reason = AdvancedUninstallReason.InsideLoggedItem,
                    Detail = "Este item esta dentro de algo que o log ja registrou.",
                    RelatedEntry = parent,
                });
                continue;
            }

            // Nada no log corresponde a este candidato: o instalador nao
            // registrou nada sobre ele (26066, 26974 do Revo).
            unlogged.Add(new AdvancedUninstallCandidate
            {
                Item = candidate,
                Reason = AdvancedUninstallReason.NotLogged,
                Detail = "O instalador nao registrou este item durante a instalacao, "
                    + "entao ele nao faz parte do log que sera apagado.",
            });
        }

        return new AdvancedUninstallReview
        {
            Unlogged = unlogged,
            Logged = logged,
        };
    }

    /// <summary>
    /// Procura a entrada do log que e exatamente este caminho.
    /// </summary>
    private static LogEntry? MatchExact(IReadOnlyList<LogEntry> entries, string path)
    {
        var target = Normalize(path);
        if (target.Length == 0) return null;

        foreach (var entry in entries)
        {
            if (string.Equals(Normalize(entry.Path), target, StringComparison.OrdinalIgnoreCase))
                return entry;
        }

        return null;
    }

    /// <summary>
    /// Procura a entrada do log que contem este caminho (a pasta ou a chave
    /// que o instalador gravou e que guarda o candidato por dentro).
    /// </summary>
    private static LogEntry? MatchInside(IReadOnlyList<LogEntry> entries, string path)
    {
        var target = Normalize(path);
        if (target.Length == 0) return null;

        foreach (var entry in entries)
        {
            if (entry.Kind == LogEntryKind.RegistryValue) continue;

            var parent = Normalize(entry.Path);
            if (parent.Length == 0) continue;

            if (target.StartsWith(parent + "\\", StringComparison.OrdinalIgnoreCase))
                return entry;
        }

        return null;
    }

    /// <summary>
    /// Normaliza o caminho para a comparacao: sem barra final, com barras
    /// iguais.
    /// </summary>
    private static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        var text = path.Trim().Trim('"');
        while (text.Length > 3 && (text.EndsWith("\\") || text.EndsWith("/")))
            text = text[..^1];

        return text.Replace('/', '\\');
    }
}
