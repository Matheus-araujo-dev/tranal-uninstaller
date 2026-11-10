using Tranal.Uninstaller.Core.Appearance;

namespace Tranal.Uninstaller.Core.Automation;

/// <summary>
/// O resultado de um programa na fila de desinstalacao.
/// </summary>
public enum UninstallQueueOutcome
{
    /// <summary>Desinstalado.</summary>
    Completed,

    /// <summary>Ja nao estava instalado: nada a fazer.</summary>
    Skipped,

    /// <summary>
    /// Nao desinstalou em silencio: saiu da tarefa e passa a ser tratado
    /// pelo usuario, como o Revo faz.
    /// </summary>
    RemovedFromTask,

    /// <summary>
    /// A desinstalacao falhou.
    /// </summary>
    Failed,

    /// <summary>
    /// A tarefa foi encerrada pelo usuario antes deste programa comecar.
    ///
    /// O desinstalador que ja estava rodando NAO e morto: um programa
    /// meio desinstalado e pior do que um programa desinstalado.
    /// </summary>
    Aborted,
}

/// <summary>
/// O resultado de um programa na fila.
/// </summary>
public sealed class UninstallQueueItem
{
    /// <summary>O programa da fila.</summary>
    public InstalledProgram Program { get; init; } = null!;

    /// <summary>O que aconteceu com ele.</summary>
    public UninstallQueueOutcome Outcome { get; set; } = UninstallQueueOutcome.Skipped;

    /// <summary>A explicacao, em texto, do resultado.</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>
    /// O nome do programa vizinho com quem este colide, quando ha colisao.
    /// </summary>
    public string CollidesWith { get; set; } = string.Empty;

    /// <summary>True quando o programa saiu da tarefa.</summary>
    public bool LeftTheTask => Outcome == UninstallQueueOutcome.RemovedFromTask;

    /// <summary>
    /// Quantas vezes a desinstalacao foi tentada. Vale 1 quando nao houve
    /// repeticao — e mais quando o que falhou foi tentado de novo.
    /// </summary>
    public int Attempts { get; set; } = 1;
}

/// <summary>
/// A fila de desinstalacao: um desinstalador por vez, com colisao declarada e
/// remocao da tarefa para quem nao desinstala em silencio.
///
/// Duas coisas que o Revo Uninstaller Pro 5.5.4 faz e o Tranal nao fazia:
///
///   - **Um desinstalador por vez.** Dois instaladores rodando ao mesmo tempo
///     disputam os mesmos arquivos e o mesmo banco do Windows Installer; a
///     fila serializa e elimina a colisao. A trava e de processo inteiro, e
///     vale para a janela e para a CLI ao mesmo tempo.
///   - **Quem nao desinstala em silencio sai da tarefa.** Um programa cujo
///     desinstalador precisa de janela nao trava o lote: ele deixa a fila e
///     fica para o usuario tratar.
///
/// A regra mora aqui, em um lugar so: a CLI e a interface grafica consomem a
/// mesma fila e nunca montam a delas.
/// </summary>
public static class UninstallQueue
{
    /// <summary>
    /// A trava do processo: garante que so um desinstalador roda por vez.
    /// </summary>
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>
    /// Executa a fila, um programa por vez.
    ///
    /// E a MESMA regra da variante assincrona (<see cref="RunAsync"/>), que e
    /// a implementacao de verdade: a tela e a linha de comando consomem as
    /// duas, e a fila tem uma unica regra.
    /// </summary>
    /// <param name="programs">Os programas da fila.</param>
    /// <param name="uninstallOne">
    /// Executa a desinstalacao silenciosa de um programa. Devolve true quando
    /// a desinstalacao foi feita.
    /// </param>
    /// <param name="stillInstalled">
    /// Diz se o programa continua instalado. E o que separa "desinstalou" de
    /// "nao desinstalou em silencio".
    /// </param>
    /// <param name="maxAttempts">Quantas vezes vale tentar o que falhou.</param>
    /// <returns>O resultado de cada programa, na ordem em que rodaram.</returns>
    public static IReadOnlyList<UninstallQueueItem> Run(
        IReadOnlyList<InstalledProgram> programs,
        Func<InstalledProgram, bool> uninstallOne,
        Func<InstalledProgram, bool>? stillInstalled = null,
        int maxAttempts = 1)
    {
        if (uninstallOne is null) throw new ArgumentNullException(nameof(uninstallOne));

        return RunAsync(
                programs,
                program => Task.FromResult(uninstallOne(program)),
                stillInstalled,
                maxAttempts)
            .GetAwaiter().GetResult();
    }

    /// <summary>
    /// Executa a fila, um programa por vez, de forma assincrona.
    ///
    /// E a implementacao de verdade da regra da fila:
    ///
    ///   - **um desinstalador por vez** (a trava e de processo inteiro, e
    ///     vale para a janela e para a linha de comando ao mesmo tempo);
    ///   - **a colisao declarada** (dois programas que dividem a pasta de
    ///     instalacao ou o mesmo executavel de desinstalacao);
    ///   - **quem nao desinstala em silencio sai da tarefa**, em vez de
    ///     travar o lote;
    ///   - **a parada da tarefa**: quando o usuario manda encerrar, o que
    ///     ainda nao comecou nao comeca. O desinstalador que ja esta rodando
    ///     NAO e morto.
    /// </summary>
    /// <param name="programs">Os programas da fila.</param>
    /// <param name="uninstallOne">
    /// Executa a desinstalacao de um programa. Devolve true quando a
    /// desinstalacao foi feita.
    /// </param>
    /// <param name="stillInstalled">
    /// Diz se o programa continua instalado. E o que separa "desinstalou" de
    /// "nao desinstalou em silencio".
    /// </param>
    /// <param name="maxAttempts">Quantas vezes vale tentar o que falhou.</param>
    /// <param name="stopRequested">
    /// Diz se o usuario mandou encerrar a tarefa. Pode ser nulo: sem ele, a
    /// fila roda do começo ao fim.
    /// </param>
    /// <returns>O resultado de cada programa, na ordem em que rodaram.</returns>
    public static async Task<IReadOnlyList<UninstallQueueItem>> RunAsync(
        IReadOnlyList<InstalledProgram> programs,
        Func<InstalledProgram, Task<bool>> uninstallOne,
        Func<InstalledProgram, bool>? stillInstalled = null,
        int maxAttempts = 1,
        Func<bool>? stopRequested = null)
    {
        if (programs is null || programs.Count == 0) return Array.Empty<UninstallQueueItem>();
        if (uninstallOne is null) throw new ArgumentNullException(nameof(uninstallOne));

        var ordered = SmartOrdering.Order(programs);
        var collisions = DetectCollisions(ordered);
        var results = new List<UninstallQueueItem>(ordered.Count);

        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (var program in ordered)
            {
                // A tarefa foi encerrada: o que ainda nao comecou nao comeca.
                // O desinstalador que ja estava rodando NAO e morto — um
                // programa meio desinstalado e pior do que um programa
                // desinstalado.
                if (stopRequested is not null && stopRequested())
                {
                    results.Add(new UninstallQueueItem
                    {
                        Program = program,
                        Outcome = UninstallQueueOutcome.Aborted,
                        Detail = "tarefa encerrada antes de comecar",
                    });
                    continue;
                }

                var item = new UninstallQueueItem { Program = program };

                // A tupla e valor, nao referencia: nao da para comparar com
                // null. A colisao e procurada na lista.
                foreach (var collision in collisions)
                {
                    if (collision.Second != program) continue;
                    item.CollidesWith = collision.First.DisplayName;
                    break;
                }

                var tentativa = 0;

                // A repeticao do que falhou mora na REGRA
                // (<see cref="ExecutionPolicy.ShouldRetry"/>): quem decide se
                // vale tentar de novo e o nucleo, e nao a fila.
                while (true)
                {
                    tentativa++;
                    item.Attempts = tentativa;

                    bool done;
                    try
                    {
                        done = await uninstallOne(program).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        item.Outcome = UninstallQueueOutcome.Failed;
                        item.Detail = ex.Message;

                        if (!ExecutionPolicy.ShouldRetry(item.Outcome, tentativa, maxAttempts))
                            break;

                        continue;
                    }

                    if (done)
                    {
                        item.Outcome = UninstallQueueOutcome.Completed;
                        item.Detail = "desinstalado";
                        break;
                    }

                    // Nao desinstalou e o chamador nao diz se ainda esta
                    // instalado: nao ha o que repetir nem o que concluir.
                    if (stillInstalled is null || !stillInstalled(program))
                    {
                        item.Outcome = UninstallQueueOutcome.Completed;
                        item.Detail = "desinstalado";
                        break;
                    }

                    // Ainda esta instalado: a tentativa falhou, e vale tentar
                    // de novo — e a regra do BCU.
                    item.Outcome = UninstallQueueOutcome.Failed;
                    item.Detail = "não desinstalou em silêncio";

                    if (!ExecutionPolicy.ShouldRetry(item.Outcome, tentativa, maxAttempts))
                    {
                        // As tentativas acabaram: o programa sai da tarefa
                        // para o usuario tratar, como o Revo faz.
                        item.Outcome = UninstallQueueOutcome.RemovedFromTask;
                        item.Detail = "não desinstalou em silêncio: saiu da tarefa";
                        break;
                    }
                }

                results.Add(item);
            }
        }
        finally
        {
            Gate.Release();
        }

        return results;
    }

    /// <summary>
    /// As colisoes entre os programas da fila: dois programas que dividem a
    /// pasta de instalacao ou o mesmo executavel de desinstalacao.
    ///
    /// O Revo avisa sobre isso antes de remover: as sobras de um sao
    /// encontradas dentro do outro, e apagar a pasta de um apaga a do outro.
    /// </summary>
    /// <param name="programs">Os programas da fila.</param>
    public static IReadOnlyList<(InstalledProgram First, InstalledProgram Second)> DetectCollisions(
        IReadOnlyList<InstalledProgram> programs)
    {
        if (programs is null || programs.Count < 2) return Array.Empty<(InstalledProgram, InstalledProgram)>();

        var collisions = new List<(InstalledProgram, InstalledProgram)>();

        for (int i = 0; i < programs.Count; i++)
        {
            for (int j = i + 1; j < programs.Count; j++)
            {
                if (Collide(programs[i], programs[j]))
                    collisions.Add((programs[i], programs[j]));
            }
        }

        return collisions;
    }

    /// <summary>
    /// Verdadeiro quando os dois programas dividem a pasta de instalacao ou o
    /// mesmo executavel de desinstalacao.
    /// </summary>
    public static bool Collide(InstalledProgram first, InstalledProgram second)
    {
        if (first is null || second is null) return false;
        if (ReferenceEquals(first, second)) return false;

        var locationA = PathUtil.Normalize(first.InstallLocation);
        var locationB = PathUtil.Normalize(second.InstallLocation);
        if (locationA.Length > 0 && string.Equals(locationA, locationB, StringComparison.OrdinalIgnoreCase))
            return true;

        var uninstallerA = ProgramScanner.ExtractExecutablePath(first.UninstallString);
        var uninstallerB = ProgramScanner.ExtractExecutablePath(second.UninstallString);
        if (uninstallerA.Length > 0 && string.Equals(uninstallerA, uninstallerB, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
