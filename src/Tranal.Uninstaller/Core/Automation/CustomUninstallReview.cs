using Tranal.Uninstaller.Core.Modules;

namespace Tranal.Uninstaller.Core.Automation;

/// <summary>
/// O que a revisao do Custom Uninstall vai fazer com uma entrada do log.
/// </summary>
public enum CustomUninstallAction
{
    /// <summary>A entrada fica no sistema: o usuario pediu para ignora-la.</summary>
    Keep,

    /// <summary>O arquivo (ou a pasta) e apagado.</summary>
    RemoveFile,

    /// <summary>A chave de registro e apagada.</summary>
    RemoveRegistryKey,

    /// <summary>O valor de registro e apagado.</summary>
    RemoveRegistryValue,
}

/// <summary>
/// Uma linha da revisao item a item do log (strings.txt:26274
/// "Custom Uninstall"; 15220 "Preparing log's data for uninstall...").
///
/// A linha e <em>dados</em>: o log, o que a revisao decidiu e o plano de
/// remocao. Quem executa o plano e o motor canonico
/// (<see cref="UninstallEngine.RemoveLeftovers"/>).
/// </summary>
public sealed class CustomUninstallItem
{
    /// <summary>A entrada do log que esta linha revisa.</summary>
    public LogEntry Entry { get; init; } = null!;

    /// <summary>True quando a entrada entra no plano de remocao.</summary>
    public bool WillRemove => Action != CustomUninstallAction.Keep;

    /// <summary>O que a revisao decidiu fazer com a entrada.</summary>
    public CustomUninstallAction Action { get; init; }

    /// <summary>
    /// O motivo da decisao, em portugues. Va sempre junto com a decisao:
    /// uma decisao sem explicacao nao diz nada ao usuario.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// O caminho que o motor recebe quando a entrada for removida. Vazio
    /// quando a entrada e mantida.
    /// </summary>
    public string Target { get; init; } = string.Empty;

    /// <summary>
    /// O nome do valor de registro, quando a entrada e um valor.
    /// Vazio nos demais casos.
    /// </summary>
    public string ValueName { get; init; } = string.Empty;
}

/// <summary>
/// A revisao completa do log: uma linha por entrada, com a decisao e o
/// plano.
/// </summary>
public sealed class CustomUninstallReview
{
    /// <summary>As linhas da revisao, na ordem do log.</summary>
    public IReadOnlyList<CustomUninstallItem> Items { get; init; } = Array.Empty<CustomUninstallItem>();

    /// <summary>Quantas entradas entram no plano de remocao.</summary>
    public int RemoveCount => Items.Count(i => i.WillRemove);

    /// <summary>Quantas entradas ficam no sistema.</summary>
    public int KeepCount => Items.Count(i => !i.WillRemove);

    /// <summary>
    /// As sobras que o motor canonico recebe: as entradas marcadas para
    /// remocao, no mesmo formato que a varredura produz.
    /// </summary>
    public IReadOnlyList<LeftoverItem> Leftovers { get; init; } = Array.Empty<LeftoverItem>();
}

/// <summary>
/// Monta a revisao item a item do log de instalacao — o painel do
/// "Custom Uninstall" (26274) do Revo.
///
/// O Revo abre o log, mostra o que tem nele e deixa o usuario marcar o que
/// sai. Aqui isso vira <em>dados</em>: uma linha por entrada, com a decisao
/// e o plano de remocao. A tela consome; o nucleo decide.
///
/// A REGRA DA DECISAO mora aqui e em nenhum outro lugar:
///
///   - entrada marcada como ignorada fica no sistema;
///   - entrada que o log registrou como removida ja nao existe, e nao entra
///     no plano;
///   - todo o resto entra no plano de remocao.
///
/// A confianca e a do nucleo (<see cref="LeftoverConfidence"/>): o log
/// registra o que o instalador gravou, e isso e confianca alta; o que o
/// usuario acrescentou a mao e confianca media.
/// </summary>
public static class CustomUninstallReviewBuilder
{
    /// <summary>
    /// Monta a revisao do log.
    /// </summary>
    /// <param name="install">O log a revisar.</param>
    /// <param name="decide">
    /// A decisao do usuario sobre cada entrada. Quando nulo, vale a regra
    /// padrao (ignorar o que esta marcado como ignorado).
    /// </param>
    public static CustomUninstallReview Build(
        TracedInstall install,
        Func<LogEntry, bool>? decide = null)
    {
        var entries = LogOperations.Entries(install);

        var items = new List<CustomUninstallItem>();
        var leftovers = new List<LeftoverItem>();

        foreach (var entry in entries)
        {
            var remove = decide is not null ? decide(entry) : DefaultDecision(entry);

            var action = ActionOf(entry, remove);
            var reason = ReasonOf(entry, remove);

            items.Add(new CustomUninstallItem
            {
                Entry = entry,
                Action = action,
                Reason = reason,
                Target = action == CustomUninstallAction.Keep ? string.Empty : entry.Path,
                ValueName = entry.ValueName,
            });

            if (action == CustomUninstallAction.Keep) continue;

            leftovers.Add(new LeftoverItem
            {
                Kind = KindOf(entry),
                Path = entry.Path,
                Detail = entry.Describe(),
                Confidence = entry.Confidence,
                Reason = LeftoverReason.InstallerLog,
            });
        }

        return new CustomUninstallReview
        {
            Items = items,
            Leftovers = leftovers,
        };
    }

    /// <summary>
    /// A decisao padrao: entrada ignorada fica, entrada que o log ja
    /// registrou como removida nao entra no plano, o resto entra.
    /// </summary>
    private static bool DefaultDecision(LogEntry entry)
    {
        if (entry.Ignored) return false;
        if (entry.Operation == LogEntryOperation.Deleted) return false;
        return true;
    }

    private static CustomUninstallAction ActionOf(LogEntry entry, bool remove)
    {
        if (!remove) return CustomUninstallAction.Keep;

        return entry.Kind switch
        {
            LogEntryKind.RegistryKey => CustomUninstallAction.RemoveRegistryKey,
            LogEntryKind.RegistryValue => CustomUninstallAction.RemoveRegistryValue,
            _ => CustomUninstallAction.RemoveFile,
        };
    }

    private static string ReasonOf(LogEntry entry, bool remove)
    {
        if (!remove)
        {
            if (entry.Ignored) return "Voce pediu para ignorar esta entrada.";
            return "O log registra que este item ja foi removido.";
        }

        return entry.Added
            ? "Entrada acrescentada por voce ao log."
            : "Registrado pelo instalador durante a instalacao.";
    }

    private static string KindOf(LogEntry entry) => entry.Kind switch
    {
        LogEntryKind.RegistryKey => "Registro",
        LogEntryKind.RegistryValue => "Registro",
        _ => entry.IsFolder ? "Pasta" : "Arquivo",
    };
}
