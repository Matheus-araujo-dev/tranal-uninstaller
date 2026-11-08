using System.Diagnostics;
using System.IO;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Uninstaller.Core.Automation;

/// <summary>
/// Um item que existe no disco mas que nao pode ser apagado agora.
/// </summary>
public sealed class LockedItem
{
    /// <summary>Caminho completo do item.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>True quando o item e uma pasta.</summary>
    public bool IsDirectory { get; init; }

    /// <summary>Tamanho do arquivo (0 quando e pasta ou o tamanho nao pode ser lido).</summary>
    public long SizeBytes { get; init; }

    /// <summary>Os processos que estao segurando o item.</summary>
    public IReadOnlyList<LockingProcess> Lockers { get; init; } = Array.Empty<LockingProcess>();

    /// <summary>True quando ha pelo menos um processo segurando o item.</summary>
    public bool HasLockers => Lockers.Count > 0;
}

/// <summary>
/// Resultado de uma tentativa de destravar um item.
/// </summary>
public sealed class UnlockOutcome
{
    /// <summary>O caminho que foi tratado.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>True quando o item deixou de estar em uso.</summary>
    public bool Unlocked { get; init; }

    /// <summary>True quando a remocao ficou agendada para o proximo reinicio.</summary>
    public bool DeferredToRestart { get; init; }

    /// <summary>Explicacao do resultado.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Os processos que foram encerrados.</summary>
    public IReadOnlyList<string> TerminatedProcesses { get; init; } = Array.Empty<string>();

    /// <summary>True quando o Explorer foi reiniciado.</summary>
    public bool ExplorerRestarted { get; init; }

    /// <summary>True quando a DLL foi desregistrada.</summary>
    public bool DllUnregistered { get; init; }
}

/// <summary>
/// Destrava arquivos e pastas que estao em uso.
///
/// O ashampoo Uninstaller e o Bullseye NortonCleanup tratam isso como uma
/// ferramenta separada ("Unlocker"): o arquivo existe, o Windows diz que esta
/// em uso, e nenhuma acao de desinstalacao adianta ate que o processo que o
/// segura seja encerrado.
///
/// A REGRA mora aqui, em um lugar so: quem precisa saber quem segura um
/// arquivo consome o <see cref="RestartManager"/> (a API do Windows) e quem
/// precisa encerrar um processo consome o <see cref="ProcessTerminator"/>. Esta
/// classe so orquestra os dois e nunca reimplementa a deteccao nem o
/// encerramento.
///
/// A remocao em si NAO acontece aqui: quando o item e destravado, quem apaga
/// e o motor canonico <see cref="UninstallEngine.RemoveLeftovers"/>, que
/// respeita as regras de seguranca e sabe adiar o que nao couber.
/// </summary>
public static class Unlocker
{
    /// <summary>
    /// Verifica se o item esta em uso e devolve quem o segura.
    /// Devolve <c>null</c> quando o item nao existe ou esta livre.
    /// </summary>
    /// <param name="path">O caminho do arquivo ou da pasta.</param>
    public static LockedItem? Scan(string? path)
    {
        var full = PathUtil.Normalize(path);
        if (full.Length == 0) return null;

        bool isDirectory;
        long size = 0;

        try
        {
            if (Directory.Exists(full))
            {
                isDirectory = true;
            }
            else if (File.Exists(full))
            {
                isDirectory = false;
                try { size = new FileInfo(full).Length; } catch { size = 0; }
            }
            else
            {
                return null;
            }
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao inspecionar '{full}': {ex.Message}");
            return null;
        }

        if (!IsHeld(full, isDirectory)) return null;

        IReadOnlyList<LockingProcess> lockers;
        try
        {
            lockers = RestartManager.WhoIsLocking(full);
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao consultar quem segura '{full}': {ex.Message}");
            lockers = Array.Empty<LockingProcess>();
        }

        return new LockedItem
        {
            Path = full,
            IsDirectory = isDirectory,
            SizeBytes = size,
            Lockers = lockers,
        };
    }

    /// <summary>
    /// True quando o item esta em uso por algum processo.
    /// </summary>
    /// <param name="path">O caminho do arquivo ou da pasta.</param>
    public static bool IsLocked(string? path) => Scan(path) is not null;

    /// <summary>
    /// Destrava o item: encerra quem o segura, reinicia o Explorer quando
    /// solicitado e desregistra a DLL quando o item e uma biblioteca.
    ///
    /// Devolve o resultado; o item **nao** e apagado aqui.
    /// </summary>
    /// <param name="path">O caminho do arquivo ou da pasta.</param>
    /// <param name="restartExplorer">Reinicia o Explorer depois de encerrar os processos.</param>
    /// <param name="unregisterDll">Desregistra a DLL antes de tentar de novo.</param>
    public static UnlockOutcome Unlock(string? path, bool restartExplorer = false, bool unregisterDll = false)
    {
        var full = PathUtil.Normalize(path);
        if (full.Length == 0)
            return new UnlockOutcome { Message = "Caminho nao informado." };

        var item = Scan(full);
        if (item is null)
            return new UnlockOutcome { Path = full, Unlocked = true, Message = "O item nao esta em uso." };

        var terminated = TerminateLockers(item.Lockers);

        var explorerRestarted = false;
        if (restartExplorer && terminated.Count > 0)
            explorerRestarted = RestartExplorer();

        var dllUnregistered = false;
        if (unregisterDll && !item.IsDirectory && full.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            dllUnregistered = UnregisterDll(full);

        // O Restart Manager responde a leitura; o teste real e tentar abrir o
        // item sem compartilhamento.
        var stillLocked = IsHeld(full, item.IsDirectory);

        return new UnlockOutcome
        {
            Path = full,
            Unlocked = !stillLocked,
            DeferredToRestart = stillLocked,
            TerminatedProcesses = terminated,
            ExplorerRestarted = explorerRestarted,
            DllUnregistered = dllUnregistered,
            Message = stillLocked
                ? "O item continua em uso; a remocao pode ser adiada para o proximo reinicio."
                : $"O item foi destravado ({terminated.Count} processo(s) encerrado(s)).",
        };
    }

    /// <summary>
    /// Encerra os processos que seguram o item e devolve os nomes dos que
    /// sairam de verdade.
    ///
    /// O encerramento e feito pelo motor canonico
    /// (<see cref="ProcessTerminator"/>), que se recusa a matar o proprio
    /// processo e trata a arvore de processos junto.
    /// </summary>
    /// <param name="lockers">Os processos que seguram o item.</param>
    public static IReadOnlyList<string> TerminateLockers(IReadOnlyList<LockingProcess> lockers)
    {
        var encerrados = new List<string>();
        if (lockers is null || lockers.Count == 0) return encerrados;

        var alvos = new List<RunningProcess>();
        foreach (var locker in lockers)
        {
            if (locker is null) continue;
            if (locker.ProcessId <= 0) continue;

            alvos.Add(new RunningProcess
            {
                ProcessId = locker.ProcessId,
                ProcessName = locker.ProcessName,
                ExecutablePath = string.Empty,
            });
        }

        if (alvos.Count == 0) return encerrados;

        IReadOnlyList<ProcessTermination> resultados;
        try
        {
            resultados = ProcessTerminator.Terminate(alvos);
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao encerrar os processos que seguram o item: {ex.Message}");
            return encerrados;
        }

        foreach (var resultado in resultados)
        {
            if (resultado.Terminated) encerrados.Add(resultado.Process.ProcessName);
        }

        return encerrados;
    }

    /// <summary>
    /// Reinicia o Explorer. O Windows o relanca sozinho depois que ele sai.
    /// </summary>
    public static bool RestartExplorer()
    {
        var explorer = ProcessTerminator.Enumerate()
            .Where(p => string.Equals(p.ProcessName, "explorer", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (explorer.Count == 0) return false;

        try
        {
            var resultados = ProcessTerminator.Terminate(explorer);
            return resultados.Any(r => r.Terminated);
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao reiniciar o Explorer: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Desregistra a DLL, para que ela nao fique carregada por nenhum
    /// processo depois de removida do disco.
    /// </summary>
    /// <param name="path">O caminho da DLL.</param>
    public static bool UnregisterDll(string? path)
    {
        var full = PathUtil.Normalize(path);
        if (full.Length == 0) return false;
        if (!full.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) return false;
        if (!File.Exists(full)) return false;

        try
        {
            var psi = new ProcessStartInfo("regsvr32.exe")
            {
                Arguments = $"/u /s \"{full}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var processo = Process.Start(psi);
            if (processo is null) return false;

            processo.WaitForExit(15000);
            return processo.HasExited && processo.ExitCode == 0;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao desregistrar '{full}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Testa se o item esta em uso: tenta abrir o item sem compartilhar
    /// nenhum acesso com os outros processos.
    /// </summary>
    private static bool IsHeld(string full, bool isDirectory)
    {
        try
        {
            if (isDirectory)
            {
                // Uma pasta esta em uso quando o sistema nao deixa criar
                // arquivos dentro dela (ex.: a pasta de trabalho de outro
                // processo).
                var teste = Path.Combine(full, ".tranal-lock-" + Guid.NewGuid().ToString("N"));
                using (var stream = new FileStream(teste, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.WriteByte(0);
                }

                try { File.Delete(teste); } catch { /* o teste ja cumpriu o papel */ }
                return false;
            }

            using var file = new FileStream(full, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            // Sem permissao para abrir: o item existe e esta protegido, mas
            // nao esta "em uso" no sentido do Unlocker.
            return false;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao testar o uso de '{full}': {ex.Message}");
            return false;
        }
    }
}
