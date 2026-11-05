using System.Diagnostics;

namespace Tranal.Uninstaller.Core;

/// <summary>Processo em execucao que o Tranal consegue identificar.</summary>
public sealed class RunningProcess
{
    /// <summary>Identificador do processo.</summary>
    public int ProcessId { get; init; }

    /// <summary>Nome do executavel (sem extensao).</summary>
    public string ProcessName { get; init; } = string.Empty;

    /// <summary>Caminho completo do executavel em execucao.</summary>
    public string ExecutablePath { get; init; } = string.Empty;

    /// <summary>True quando o executavel esta dentro de uma das pastas informadas.</summary>
    public bool IsInside(params string[] folders) =>
        folders.Any(f => IsUnder(ExecutablePath, f));

    /// <summary>True quando o caminho informado esta dentro da pasta.</summary>
    private static bool IsUnder(string path, string folder)
    {
        var full = PathUtil.Normalize(path);
        var root = PathUtil.Normalize(folder);
        if (full.Length == 0 || root.Length == 0) return false;

        if (string.Equals(full, root, StringComparison.OrdinalIgnoreCase)) return true;

        return full.Length > root.Length
            && full.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            && full[root.Length] == '\\';
    }
}

/// <summary>Resultado da tentativa de encerrar um processo.</summary>
public sealed class ProcessTermination
{
    /// <summary>O processo que foi alvo.</summary>
    public RunningProcess Process { get; init; } = new();

    /// <summary>True quando o processo foi encerrado de fato.</summary>
    public bool Terminated { get; init; }

    /// <summary>Explicacao do resultado (vazia quando o processo foi encerrado).</summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Encerra processos em execucao antes de remover os arquivos deles.
///
/// Corresponde as opcoes do Revo Uninstaller Pro "Forcibly stop running
/// executable files during their uninstall" (15265) e ao aviso "Selected file is
/// a running process! ... it is better to kill (stop) it before you continue"
/// (15126), com o botao "Kill the Process and Continue" (15125).
///
/// Regra de seguranca: o proprio processo do Tranal nunca e encerrado, e um
/// processo so e candidato quando o executavel esta dentro de uma das pastas
/// que serao removidas — nunca "qualquer processo com o mesmo nome".
/// </summary>
public static class ProcessTerminator
{
    /// <summary>Tempo maximo de espera pela saida do processo (ms).</summary>
    private const int ExitTimeoutMs = 5000;

    /// <summary>
    /// Processos em execucao cujo executavel esta dentro de uma das pastas
    /// informadas. A propria pasta conta como dentro de si mesma.
    /// </summary>
    public static IReadOnlyList<RunningProcess> FindUnder(IEnumerable<string> folders)
    {
        var roots = folders
            .Select(PathUtil.Normalize)
            .Where(p => p.Length > 0)
            .ToArray();

        if (roots.Length == 0) return Array.Empty<RunningProcess>();

        return Enumerate()
            .Where(p => p.IsInside(roots))
            .ToList();
    }

    /// <summary>
    /// Processos em execucao cujo executavel e exatamente um dos informados.
    /// </summary>
    public static IReadOnlyList<RunningProcess> FindExact(IEnumerable<string> executablePaths)
    {
        var wanted = executablePaths
            .Select(PathUtil.Normalize)
            .Where(p => p.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (wanted.Count == 0) return Array.Empty<RunningProcess>();

        return Enumerate()
            .Where(p => wanted.Contains(PathUtil.Normalize(p.ExecutablePath)))
            .ToList();
    }

    /// <summary>
    /// Todos os processos em execucao que o Tranal consegue ler. Processos
    /// protegidos (elevados, do sistema ou de sessao diferente) ficam de fora:
    /// sem permissao para ler o executavel, nao ha como afirmar com seguranca
    /// que o processo e o do programa que esta sendo removido.
    /// </summary>
    public static IReadOnlyList<RunningProcess> Enumerate()
    {
        var result = new List<RunningProcess>();
        var self = Environment.ProcessId;

        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch
        {
            return result;
        }

        foreach (var process in processes)
        {
            try
            {
                if (process.Id == self) continue;

                // MainModule lanca quando o processo nao pode ser lido (protegido)
                // ou ja terminou.
                var path = process.MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(path)) continue;

                result.Add(new RunningProcess
                {
                    ProcessId = process.Id,
                    ProcessName = process.ProcessName,
                    ExecutablePath = path,
                });
            }
            catch
            {
                // Processo inacessivel: ignorado.
            }
            finally
            {
                process.Dispose();
            }
        }

        return result;
    }

    /// <summary>
    /// Encerra os processos informados e devolve o resultado de cada um.
    /// </summary>
    public static IReadOnlyList<ProcessTermination> Terminate(IEnumerable<RunningProcess> processes)
    {
        var results = new List<ProcessTermination>();
        var self = Environment.ProcessId;

        foreach (var info in processes)
        {
            if (info.ProcessId == self)
            {
                results.Add(new ProcessTermination
                {
                    Process = info,
                    Terminated = false,
                    Message = "é o próprio Tranal",
                });
                continue;
            }

            try
            {
                using var process = Process.GetProcessById(info.ProcessId);
                if (process.HasExited)
                {
                    results.Add(new ProcessTermination
                    {
                        Process = info,
                        Terminated = true,
                        Message = "já tinha encerrado",
                    });
                    continue;
                }

                // A arvore inteira vai junto: um programa que relanca a si mesmo
                // deixaria o arquivo em uso logo apos a remocao.
                process.Kill(entireProcessTree: true);

                if (!process.WaitForExit(ExitTimeoutMs))
                {
                    results.Add(new ProcessTermination
                    {
                        Process = info,
                        Terminated = false,
                        Message = "não encerrou a tempo",
                    });
                    continue;
                }

                results.Add(new ProcessTermination
                {
                    Process = info,
                    Terminated = true,
                    Message = "encerrado",
                });
            }
            catch (ArgumentException)
            {
                // O processo encerrou entre a enumeracao e o comando: o
                // objetivo (nao estar mais rodando) ja foi alcancado.
                results.Add(new ProcessTermination
                {
                    Process = info,
                    Terminated = true,
                    Message = "já tinha encerrado",
                });
            }
            catch (Exception ex)
            {
                results.Add(new ProcessTermination
                {
                    Process = info,
                    Terminated = false,
                    Message = ex.Message,
                });
            }
        }

        return results;
    }

    /// <summary>
    /// Encerra os processos que estiverem rodando a partir das pastas informadas
    /// e devolve o resultado de cada um.
    /// </summary>
    public static IReadOnlyList<ProcessTermination> TerminateUnder(IEnumerable<string> folders) =>
        Terminate(FindUnder(folders));
}
