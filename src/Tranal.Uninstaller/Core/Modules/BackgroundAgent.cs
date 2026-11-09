using System.Diagnostics;
using System.IO;

namespace Tranal.Uninstaller.Core.Modules;

/// <summary>
/// O agente de fundo do Tranal.
///
/// E um processo leve que fica de olho na maquina e avisa quando algo muda —
/// um programa novo, uma instalacao em andamento. Ele nao desenha nada: por
/// isso o consumo e baixo.
///
/// O agente e o proprio Tranal com o argumento <c>--agent</c>: nao ha um
/// segundo executavel para instalar, e o agente sabe exatamente a mesma
/// coisa que a janela.
///
/// A regra esta em um lugar so: este modulo sabe como o agente e Started e
/// parado.
/// </summary>
public static class BackgroundAgent
{
    /// <summary>O argumento que transforma o Tranal em agente.</summary>
    public const string AgentArgument = "--agent";

    /// <summary>A pasta onde fica a marca de que o agente esta rodando.</summary>
    public static string StateFolder =>
        Path.Combine(RegistryBackup.DataDirectory, "agent");

    /// <summary>O arquivo com o identificador do processo do agente.</summary>
    public static string StateFilePath => Path.Combine(StateFolder, "agent.pid");

    /// <summary>
    /// O processo esta rodando agora?
    /// </summary>
    public static bool IsRunning()
    {
        var id = RunningProcessId();
        return id > 0;
    }

    /// <summary>
    /// O identificador do processo do agente, ou zero quando ele nao roda.
    /// </summary>
    public static int RunningProcessId()
    {
        try
        {
            if (!File.Exists(StateFilePath)) return 0;

            var texto = File.ReadAllText(StateFilePath).Trim();
            if (!int.TryParse(texto, out var id) || id <= 0) return 0;

            using var process = Process.GetProcessById(id);
            return process.HasExited ? 0 : id;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Inicia o agente (o proprio executavel do Tranal com o argumento).
    /// </summary>
    /// <param name="executablePath">O executavel do Tranal.</param>
    /// <returns>O identificador do processo iniciado, ou zero.</returns>
    public static int Start(string? executablePath = null)
    {
        try
        {
            var caminho = string.IsNullOrWhiteSpace(executablePath)
                ? Environment.ProcessPath
                : executablePath;

            if (string.IsNullOrWhiteSpace(caminho)) return 0;

            var info = new ProcessStartInfo
            {
                FileName = caminho,
                Arguments = AgentArgument,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(info);
            if (process is null) return 0;

            Directory.CreateDirectory(StateFolder);
            File.WriteAllText(StateFilePath, process.Id.ToString());

            return process.Id;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Para o agente.
    /// </summary>
    public static bool Stop()
    {
        try
        {
            var id = RunningProcessId();
            if (id > 0)
            {
                using var process = Process.GetProcessById(id);
                process.Kill();
            }

            if (File.Exists(StateFilePath)) File.Delete(StateFilePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// O agente esta ligado nas preferencias?
    /// </summary>
    public static bool IsEnabled() => Settings.AppSettingsStore.Current.BackgroundAgent;
}
