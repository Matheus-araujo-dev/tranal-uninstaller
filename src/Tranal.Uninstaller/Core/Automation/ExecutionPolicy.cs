using Tranal.Uninstaller.Core.Settings;

namespace Tranal.Uninstaller.Core.Automation;

/// <summary>
/// O que acontece quando a desinstalacao termina.
/// </summary>
public enum CompletionAction
{
    /// <summary>Nada: o aplicativo continua aberto.</summary>
    Nothing,

    /// <summary>O proprio aplicativo se fecha.</summary>
    CloseApplication,

    /// <summary>A maquina reinicia (para concluir o que ficou pendente).</summary>
    RestartMachine,
}

/// <summary>
/// O resultado do corte de um lote pelo teto de itens.
/// </summary>
/// <param name="Accepted">Os programas que entram no lote.</param>
/// <param name="Deferred">Os programas que ficaram de fora (teto atingido).</param>
public sealed record BatchPlan(
    IReadOnlyList<InstalledProgram> Accepted,
    IReadOnlyList<InstalledProgram> Deferred)
{
    /// <summary>True quando o teto de itens cortou o lote.</summary>
    public bool WasTruncated => Deferred.Count > 0;
}

/// <summary>
/// A REGRA de como a desinstalacao se comporta no fim e no lote.
///
/// Tudo mora aqui, em um lugar so: quem decide e o nucleo, e a interface
/// grafica e a linha de comando consomem estas regras — elas nunca montam as
/// suas.
///
/// Sao tres.decisoes medidas nos concorrentes:
///
///   - o **teto de itens do lote** (Uninstall Tool): um lote nao roda o
///     programa inteiro de uma vez — ele avisa e corta;
///   - a **tentativa de novo** dos silenciosos que falharam (BCU): o que
///     falhou no silencioso pode ser tentado outra vez;
///   - o **fim da tarefa** (Uninstalr): fechar o proprio aplicativo ou
///     reiniciar a maquina para concluir o que ficou pendente.
/// </summary>
public static class ExecutionPolicy
{
    /// <summary>
    /// O teto de itens de um lote (0 = sem teto).
    /// </summary>
    public const int SemTeto = 0;

    /// <summary>
    /// Corta o lote no teto de itens.
    ///
    /// O corte e sempre do FIM da lista: os primeiros programas sao os que
    /// entram, e os ultimos ficam registrados como adiados — nada e
    /// descartado em silencio, porque um programa que some da fila sem
    /// aviso e um programa que o usuario achou que foi removido.
    /// </summary>
    /// <param name="programs">Os programas do lote, na ordem.</param>
    /// <param name="teto">O teto de itens (0 = sem corte).</param>
    public static BatchPlan ApplyBatchLimit(
        IReadOnlyList<InstalledProgram>? programs, int teto)
    {
        if (programs is null || programs.Count == 0)
            return new BatchPlan(Array.Empty<InstalledProgram>(), Array.Empty<InstalledProgram>());

        if (teto <= SemTeto || programs.Count <= teto)
            return new BatchPlan(programs, Array.Empty<InstalledProgram>());

        return new BatchPlan(
            programs.Take(teto).ToList(),
            programs.Skip(teto).ToList());
    }

    /// <summary>
    /// True quando o item da fila deve ser tentado de novo.
    ///
    /// A regra e do BCU: "tentar novamente" os silenciosos que falharam.
    /// So o que FALHOU volta: o que saiu da tarefa (nao desinstalou em
    /// silencio) nao e falha — e um item que o usuario trata, e repetir
    /// seria rodar o desinstalador de novo sem querer.
    /// </summary>
    /// <param name="outcome">O resultado da tentativa anterior.</param>
    /// <param name="tentativa">A tentativa que acabou de acontecer (1 = a primeira).</param>
    /// <param name="maximo">O numero maximo de tentativas (1 = sem repeticao).</param>
    public static bool ShouldRetry(
        UninstallQueueOutcome outcome, int tentativa, int maximo)
    {
        if (maximo <= 1) return false;
        if (tentativa >= maximo) return false;

        return outcome == UninstallQueueOutcome.Failed;
    }

    /// <summary>
    /// O que fazer quando a desinstalacao termina.
    ///
    /// O reinicio tem prioridade sobre o fechamento: quando o usuario pediu
    /// as duas coisas, o que ele quer mesmo e a maquina voltar (e o
    /// aplicativo fecha junto com ela).
    /// </summary>
    /// <param name="reiniciar">A preferencia de reiniciar a maquina.</param>
    /// <param name="fechar">A preferencia de fechar o aplicativo.</param>
    /// <param name="pendente">
    /// True quando sobrou item pendente para o proximo reinicio. Sem item
    /// pendente, reiniciar a maquina nao adianta — e nao ha o que concluir.
    /// </param>
    public static CompletionAction Completion(
        bool reiniciar, bool fechar, bool pendente)
    {
        if (reiniciar && pendente) return CompletionAction.RestartMachine;
        if (fechar) return CompletionAction.CloseApplication;

        return CompletionAction.Nothing;
    }

    /// <summary>
    /// A acao de fim de tarefa segundo as preferencias do usuario.
    /// </summary>
    /// <param name="settings">As preferencias.</param>
    /// <param name="pendente">True quando sobrou item pendente para o proximo reinicio.</param>
    public static CompletionAction Completion(AppSettings? settings, bool pendente)
    {
        if (settings is null) return CompletionAction.Nothing;

        return Completion(settings.RestartWhenFinished, settings.CloseAppWhenFinished, pendente);
    }
}
