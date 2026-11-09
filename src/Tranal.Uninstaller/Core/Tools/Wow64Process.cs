using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Tranal.Uninstaller.Core.Tools;

/// <summary>
/// A execucao de um processo de 32 bits a partir de um Tranal de 64 bits.
///
/// Alguns desinstaladores so funcionam em 32 bits — e sao exatamente os que
/// o Windows lanca por compatibility. O Windows da uma API para isso: o
/// executavel 64 bits pede o equivalente de 32 bits ao Windows, e o Windows
/// lanca o processo de 32 bits.
///
/// O caminho que o Windows usa e <c>C:\\Windows\\SysWOW64</c> — a pasta dos
/// executaveis de 32 bits. Este modulo e quem sabe o caminho.
///
/// A regra esta em um lugar so: este modulo sabe executar um processo de 32
/// bits.
/// </summary>
public static class Wow64Process
{
    /// <summary>
    /// A pasta dos executaveis de 32 bits.
    /// </summary>
    public static string SysWow64Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64");

    /// <summary>
    /// O caminho do executavel de 32 bits equivalente ao informado.
    ///
    /// Devolve o mesmo caminho quando o executavel nao existe na pasta de 32
    /// bits — e nesse caso o chamador deve usar o caminho original.
    /// </summary>
    /// <param name="executablePath">O executavel.</param>
    public static string? X86PathOf(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath)) return null;

        try
        {
            var nome = Path.GetFileName(executablePath);
            if (nome.Length == 0) return null;

            var candidato = Path.Combine(SysWow64Folder, nome);
            return File.Exists(candidato) ? candidato : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Executa um processo de 32 bits.
    /// </summary>
    /// <param name="executablePath">O executavel.</param>
    /// <param name="arguments">Os argumentos.</param>
    /// <param name="workingDirectory">A pasta de trabalho.</param>
    /// <returns>O identificador do processo iniciado, ou zero quando nao deu
    /// para comecar.</returns>
    public static int Start(
        string? executablePath,
        string? arguments = null,
        string? workingDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(executablePath)) return 0;

        try
        {
            var path = X86PathOf(executablePath) ?? executablePath;

            var info = new ProcessStartInfo
            {
                FileName = path,
                Arguments = arguments ?? string.Empty,
                UseShellExecute = true,
            };

            if (!string.IsNullOrWhiteSpace(workingDirectory)) info.WorkingDirectory = workingDirectory;

            using var process = Process.Start(info);
            return process?.Id ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// O processo atual e de 32 bits?
    /// </summary>
    public static bool Is32BitProcess => !Environment.Is64BitProcess;
}
