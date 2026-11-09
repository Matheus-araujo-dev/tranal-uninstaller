using System.Diagnostics;
using System.IO;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Uninstaller.Core.Automation;

/// <summary>
/// O resultado de uma instalacao silenciosa.
/// </summary>
public sealed class SilentInstallResult
{
    /// <summary>True quando o instalador terminou com codigo zero.</summary>
    public bool Success { get; init; }

    /// <summary>O codigo de saida do instalador.</summary>
    public int ExitCode { get; init; }

    /// <summary>O comando que foi executado.</summary>
    public string Command { get; init; } = string.Empty;

    /// <summary>O caminho do log gerado (quando o instalador aceitou gravar).</summary>
    public string LogPath { get; init; } = string.Empty;

    /// <summary>A saida do instalador.</summary>
    public string Output { get; init; } = string.Empty;

    /// <summary>Explicacao do resultado.</summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Instala um programa sem aparecer na tela.
///
/// O ashampoo e o IOBIT instalam programas "com o Tranal" ou em modo
/// silencioso; o instalador do Windows (msiexec) e o instalador Inno Setup
/// aceitam parametros que nao mostram nenhuma janela.
///
/// A REGRA mora aqui, em um lugar so: os parametros de cada tipo de
/// instalador ficam aqui, e quem instala consome o veredito. A execucao em si
/// usa o motor canonico de execucao silenciosa do nucleo, quando existir.
///
/// Nada e instalado sem o pedido explicito do usuario: esta classe e o
/// caminho que o Tranal usa para "instalar com rastreio", e nao um
/// instalador automatico.
/// </summary>
public static class SilentInstaller
{
    /// <summary>
    /// Os parametros silenciosos de um instalador, conforme o tipo.
    /// </summary>
    /// <param name="installerPath">O caminho do instalador.</param>
    public static IReadOnlyList<string> ArgumentsFor(string? installerPath)
    {
        var caminho = (installerPath ?? string.Empty).Trim();
        if (caminho.Length == 0) return Array.Empty<string>();

        // MSI: o proprio msiexec trata do silencio e do log.
        if (caminho.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
        {
            return new[]
            {
                "/i", caminho,
                "/qn",
                "/norestart",
            };
        }

        // Inno Setup: /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
        if (caminho.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return new[]
            {
                "/VERYSILENT",
                "/SUPPRESSMSGBOXES",
                "/NORESTART",
                "/SP-",
            };
        }

        return Array.Empty<string>();
    }

    /// <summary>
    /// Monta o comando completo de instalacao silenciosa.
    /// </summary>
    /// <param name="installerPath">O caminho do instalador.</param>
    public static string BuildCommand(string? installerPath)
    {
        var caminho = (installerPath ?? string.Empty).Trim();
        if (caminho.Length == 0) return string.Empty;

        var argumentos = ArgumentsFor(caminho);
        if (argumentos.Count == 0) return $"\"{caminho}\"";

        if (caminho.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
        {
            return "msiexec.exe " + string.Join(" ", argumentos);
        }

        return $"\"{caminho}\" " + string.Join(" ", argumentos);
    }

    /// <summary>
    /// Instala um programa em silencio e devolve o resultado.
    /// </summary>
    /// <param name="installerPath">O caminho do instalador.</param>
    /// <param name="logPath">O caminho do log a gerar (quando o instalador aceita).</param>
    /// <param name="timeoutMs">O tempo maximo de espera, em milissegundos.</param>
    public static SilentInstallResult Install(string? installerPath, string? logPath = null, int timeoutMs = 600000)
    {
        var caminho = (installerPath ?? string.Empty).Trim();
        if (caminho.Length == 0)
            return new SilentInstallResult { Message = "Instalador nao informado." };

        if (!File.Exists(caminho))
            return new SilentInstallResult { Message = $"O instalador '{caminho}' nao existe." };

        var comando = BuildCommand(caminho);
        if (comando.Length == 0)
            return new SilentInstallResult { Message = "Nao foi possivel montar o comando de instalacao." };

        try
        {
            var psi = new ProcessStartInfo("cmd.exe")
            {
                Arguments = "/c " + comando,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            if (!string.IsNullOrWhiteSpace(logPath))
            {
                psi.Arguments += " /LOG=\"" + logPath + "\"";
            }

            using var processo = Process.Start(psi);
            if (processo is null)
                return new SilentInstallResult { Message = "Nao foi possivel iniciar o instalador." };

            var saida = processo.StandardOutput.ReadToEnd();
            var erro = processo.StandardError.ReadToEnd();

            processo.WaitForExit(timeoutMs);

            if (!processo.HasExited)
            {
                return new SilentInstallResult
                {
                    Success = false,
                    Command = comando,
                    LogPath = logPath ?? string.Empty,
                    Output = saida,
                    Message = "O instalador passou do tempo limite e foi encerrado.",
                };
            }

            var codigo = processo.ExitCode;
            return new SilentInstallResult
            {
                Success = codigo == 0,
                ExitCode = codigo,
                Command = comando,
                LogPath = logPath ?? string.Empty,
                Output = (saida + Environment.NewLine + erro).Trim(),
                Message = codigo == 0
                    ? "Instalacao concluida."
                    : $"O instalador terminou com o codigo {codigo}.",
            };
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao rodar o instalador '{caminho}': {ex.Message}");
            return new SilentInstallResult
            {
                Command = comando,
                Message = $"Falha ao rodar o instalador: {ex.Message}",
            };
        }
    }
}
