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

    /// <summary>A desinstalacao falhou.</summary>
    Failed,
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
    /// <returns>O resultado de cada programa, na ordem em que rodaram.</returns>
    public static IReadOnlyList<UninstallQueueItem> Run(
        IReadOnlyList<InstalledProgram> programs,
        Func<InstalledProgram, bool> uninstallOne,
        Func<InstalledProgram, bool>? stillInstalled = null)
    {
        if (programs is null || programs.Count == 0) return Array.Empty<UninstallQueueItem>();
        if (uninstallOne is null) throw new ArgumentNullException(nameof(uninstallOne));

        var ordered = SmartOrdering.Order(programs);
        var collisions = DetectCollisions(ordered);
        var results = new List<UninstallQueueItem>(ordered.Count);

        Gate.Wait();
        try
        {
            foreach (var program in ordered)
            {
                var item = new UninstallQueueItem { Program = program };

                // A tupla e valor, nao referencia: nao da para comparar com
                // null. A colisao e procurada na lista.
                foreach (var collision in collisions)
                {
                    if (collision.Second != program) continue;
                    item.CollidesWith = collision.First.DisplayName;
                    break;
                }

                bool done;
                try
                {
                    done = uninstallOne(program);
                }
                catch (Exception ex)
                {
                    item.Outcome = UninstallQueueOutcome.Failed;
                    item.Detail = ex.Message;
                    results.Add(item);
                    continue;
                }

                if (done)
                {
                    item.Outcome = UninstallQueueOutcome.Completed;
                    item.Detail = "desinstalado";
                    results.Add(item);
                    continue;
                }

                // Nao desinstalou. Se ainda esta instalado, e porque o
                // desinstalador nao roda em silencio: ele sai da tarefa em
                // vez de travar o lote.
                if (stillInstalled is not null && stillInstalled(program))
                {
                    item.Outcome = UninstallQueueOutcome.RemovedFromTask;
                    item.Detail = "não desinstalou em silêncio: saiu da tarefa";
                }
                else
                {
                    item.Outcome = UninstallQueueOutcome.Completed;
                    item.Detail = "desinstalado";
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
