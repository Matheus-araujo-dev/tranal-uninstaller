namespace Tranal.Uninstaller.Core;

/// <summary>
/// Estado final de um item de sobra apos a tentativa de remocao.
/// A distincao entre "removido", "ja inexistente" e "falhou" e essencial:
/// o Revo Pro apresenta essas tres contagens separadamente no resumo.
/// </summary>
public enum RemovalStatus
{
    /// <summary>O item existia e foi removido.</summary>
    Removed,

    /// <summary>O item ja nao existia (normal apos o desinstalador nativo agir).</summary>
    AlreadyAbsent,

    /// <summary>A remocao foi tentada e falhou (permissao, arquivo em uso, etc.).</summary>
    Failed,

    /// <summary>A remocao foi barrada pelas regras de seguranca do Tranal.</summary>
    Protected,

    /// <summary>
    /// O item esta em uso e foi agendado para ser removido no proximo
    /// reinicio do Windows. O Revo Pro avisa que "os arquivos restantes da
    /// lista serao excluidos durante a proxima reinicializacao do sistema".
    /// </summary>
    Deferred,
}

/// <summary>Resultado individual de um item de sobra.</summary>
public sealed class RemovalOutcome
{
    public required LeftoverItem Item { get; init; }
    public required RemovalStatus Status { get; init; }
    public string Message { get; init; } = string.Empty;

    public bool Removed => Status == RemovalStatus.Removed;

    /// <summary>Verdadeiro quando o item nao esta mais no sistema (removido ou ja ausente).</summary>
    public bool Gone => Status is RemovalStatus.Removed or RemovalStatus.AlreadyAbsent;

    /// <summary>
    /// Quantos itens foram removidos DENTRO deste item.
    ///
    /// Só a pasta preenchida: quando uma pasta contém item protegido, o
    /// conteúdo sai item a item e a pasta permanece. O relatório conta a
    /// pasta como um item só — mas o que saiu de dentro dela precisa
    /// aparecer, senão o resumo diria "0 removidos" com arquivos apagados.
    /// </summary>
    public int RemovedInside { get; init; }

    /// <summary>Quantos itens protegidos foram mantidos dentro da pasta.</summary>
    public int ProtectedInside { get; init; }

    /// <summary>Quantos itens dentro da pasta nao puderam ser removidos.</summary>
    public int FailedInside { get; init; }

    /// <summary>Quantos itens dentro da pasta foram adiados para o proximo reinicio.</summary>
    public int DeferredInside { get; init; }
}

/// <summary>
/// Relatorio canonico de uma operacao de remocao de sobras.
/// A GUI e a CLI consomem ESTE objeto: nao existe segunda interpretacao do resultado.
/// </summary>
public sealed class RemovalReport
{
    public RemovalReport(IEnumerable<RemovalOutcome> outcomes) =>
        Outcomes = outcomes.ToList();

    public IReadOnlyList<RemovalOutcome> Outcomes { get; }

    /// <summary>
    /// Identificador do ponto de restauracao criado nesta operacao, quando o
    /// backup automatico estava habilitado. Permite recuperar tudo o que foi
    /// removido pelo Gerenciador de Backups.
    /// </summary>
    public string? RestorePointId { get; init; }

    /// <summary>
    /// Pasta do backup completo do registro criado nesta operacao, quando a
    /// opcao estava habilitada. Vazio quando nenhum backup foi feito.
    /// </summary>
    public string FullRegistryBackupPath { get; init; } = string.Empty;

    /// <summary>True quando o backup completo do registro foi criado com sucesso.</summary>
    public bool FullRegistryBackupOk { get; init; }

    /// <summary>
    /// Tipos de item que contam como remocao.
    ///
    /// "Processo" (encerrar um executavel em execucao) e uma operacao
    /// auxiliar: nao e um item removido da maquina. Sem esta separacao, o
    /// relatorio diria "3 itens removidos" quando so um foi removido — e um
    /// numero que nao corresponde a nada que o usuario possa conferir.
    /// </summary>
    private static readonly string[] RemovalKinds = { "Arquivo", "Pasta", "Registro" };

    /// <summary>True quando o resultado corresponde a um item removido da maquina.</summary>
    private static bool IsRemovalItem(RemovalOutcome outcome) =>
        RemovalKinds.Contains(outcome.Item.Kind, StringComparer.OrdinalIgnoreCase);

    public int Total => Outcomes.Count(IsRemovalItem);

    public int RemovedCount => Outcomes.Count(o => IsRemovalItem(o) && o.Status == RemovalStatus.Removed);

    public int AbsentCount => Outcomes.Count(o => IsRemovalItem(o) && o.Status == RemovalStatus.AlreadyAbsent);

    public int FailedCount => Outcomes.Count(o => IsRemovalItem(o) && o.Status == RemovalStatus.Failed);

    public int ProtectedCount => Outcomes.Count(o => IsRemovalItem(o) && o.Status == RemovalStatus.Protected);

    /// <summary>Itens agendados para remocao no proximo reinicio.</summary>
    public int DeferredCount => Outcomes.Count(o => IsRemovalItem(o) && o.Status == RemovalStatus.Deferred);

    /// <summary>
    /// Itens removidos dentro de pastas que ficaram no sistema (pasta com
    /// item protegido). Sao itens de verdade, mas nao constam da contagem de
    /// itens da lista: a lista tinha uma pasta, e a pasta e o item.
    /// </summary>
    public int RemovedInsideCount => Outcomes.Sum(o => o.RemovedInside);

    /// <summary>Itens protegidos mantidos dentro de pastas.</summary>
    public int ProtectedInsideCount => Outcomes.Sum(o => o.ProtectedInside);

    /// <summary>Itens que nao puderam ser removidos dentro de pastas.</summary>
    public int FailedInsideCount => Outcomes.Sum(o => o.FailedInside);

    /// <summary>Itens adiados para o proximo reinicio dentro de pastas.</summary>
    public int DeferredInsideCount => Outcomes.Sum(o => o.DeferredInside);

    /// <summary>Itens que continuam no sistema apos a operacao.</summary>
    public IReadOnlyList<RemovalOutcome> Remaining =>
        Outcomes.Where(o => IsRemovalItem(o)
                         && o.Status is RemovalStatus.Failed or RemovalStatus.Protected
                                      or RemovalStatus.Deferred).ToList();

    public int CountOf(string kind, RemovalStatus status) =>
        Outcomes.Count(o => string.Equals(o.Item.Kind, kind, StringComparison.OrdinalIgnoreCase)
                            && o.Status == status);

    /// <summary>Resumo textual no formato de contagens por tipo (usado na etapa 5 do assistente).</summary>
    public string Describe()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var kind in new[] { "Arquivo", "Pasta", "Registro" })
        {
            var removed = CountOf(kind, RemovalStatus.Removed);
            var absent = CountOf(kind, RemovalStatus.AlreadyAbsent);
            var deferred = CountOf(kind, RemovalStatus.Deferred);
            var failed = CountOf(kind, RemovalStatus.Failed) + CountOf(kind, RemovalStatus.Protected);
            if (removed + absent + deferred + failed == 0) continue;

            sb.AppendLine($"• {kind}: {removed + absent} removido(s) "
                        + $"(com sucesso: {removed}, já inexistente(s): {absent}, falha(s)/protegido(s): {failed}"
                        + (deferred > 0 ? $", adiado(s) para o próximo reinício: {deferred}" : "")
                        + ")");
        }

        // O que saiu de dentro das pastas que ficaram no sistema. Sem esta
        // linha o resumo diria "0 removidos" com arquivos de fato apagados —
        // e esconderia a falha de um item que nao pode ser removido agora.
        if (RemovedInsideCount > 0 || ProtectedInsideCount > 0
            || FailedInsideCount > 0 || DeferredInsideCount > 0)
        {
            var parts = new List<string>();
            if (RemovedInsideCount > 0) parts.Add($"{RemovedInsideCount} removido(s)");
            if (ProtectedInsideCount > 0) parts.Add($"{ProtectedInsideCount} protegido(s) mantido(s)");
            if (FailedInsideCount > 0) parts.Add($"{FailedInsideCount} com falha");
            if (DeferredInsideCount > 0) parts.Add($"{DeferredInsideCount} adiado(s) para o próximo reinício");

            sb.AppendLine();
            sb.AppendLine("Dentro das pastas que permaneceram: " + string.Join(", ", parts));
        }

        if (Remaining.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Itens que permaneceram no sistema:");
            foreach (var outcome in Remaining)
                sb.AppendLine($"  - [{outcome.Item.Kind}] {outcome.Item.Path}  ({outcome.Message})");
        }

        return sb.ToString().TrimEnd();
    }
}

/// <summary>Resultado da execucao do desinstalador nativo de um programa.</summary>
public sealed class BuiltInUninstallResult
{
    public required string ProgramName { get; init; }
    public required string Command { get; init; }
    public bool Started { get; init; }
    public int ExitCode { get; init; }
    public string Message { get; init; } = string.Empty;

    public bool Succeeded => Started && ExitCode == 0;

    /// <summary>
    /// True quando o desinstalador nativo rodou e o programa saiu do sistema.
    ///
    /// O Windows Installer devolve 3010 quando a desinstalacao foi feita mas
    /// ainda precisa de reinicio: do ponto de vista das sobras, ela deu
    /// certo — e e por isso que a conferencia das sobras pelo log do
    /// instalador so vale com esta condicao.
    /// </summary>
    public bool Uninstalled => Started && (ExitCode == 0 || ExitCode == 3010);

    public string Describe()
    {
        if (!Started)
            return $"{ProgramName}: o desinstalador nativo não pôde ser iniciado ({Message}).";
        if (ExitCode == 0)
            return $"{ProgramName}: desinstalador nativo concluído com sucesso.";
        return $"{ProgramName}: o desinstalador nativo retornou o código {ExitCode}.";
    }
}
