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

    public int Total => Outcomes.Count;

    public int RemovedCount => Outcomes.Count(o => o.Status == RemovalStatus.Removed);

    public int AbsentCount => Outcomes.Count(o => o.Status == RemovalStatus.AlreadyAbsent);

    public int FailedCount => Outcomes.Count(o => o.Status == RemovalStatus.Failed);

    public int ProtectedCount => Outcomes.Count(o => o.Status == RemovalStatus.Protected);

    /// <summary>Itens agendados para remocao no proximo reinicio.</summary>
    public int DeferredCount => Outcomes.Count(o => o.Status == RemovalStatus.Deferred);

    /// <summary>Itens que continuam no sistema apos a operacao.</summary>
    public IReadOnlyList<RemovalOutcome> Remaining =>
        Outcomes.Where(o => o.Status is RemovalStatus.Failed or RemovalStatus.Protected
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
                        + $"(com sucesso: {removed}, já inexistente(s): {absent}, falha(s): {failed}"
                        + (deferred > 0 ? $", adiado(s) para o próximo reinício: {deferred}" : "")
                        + ")");
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

    public string Describe()
    {
        if (!Started)
            return $"{ProgramName}: o desinstalador nativo não pôde ser iniciado ({Message}).";
        if (ExitCode == 0)
            return $"{ProgramName}: desinstalador nativo concluído com sucesso.";
        return $"{ProgramName}: o desinstalador nativo retornou o código {ExitCode}.";
    }
}
