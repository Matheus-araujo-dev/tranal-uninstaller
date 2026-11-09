using System.Diagnostics;

namespace Tranal.Uninstaller.Core.Modules;

/// <summary>
/// Um processo que parece ser um desinstalador.
/// </summary>
/// <param name="ProcessId">O identificador do processo.</param>
/// <param name="Name">O nome do executavel.</param>
/// <param name="MainWindowTitle">O titulo da janela (quando ha).</param>
/// <param name="IsSilent">O processo roda sem janela — e o caso silencioso.</param>
/// <param name="StartTime">Quando o processo comecou.</param>
public sealed record UninstallerProcess(
    int ProcessId,
    string Name,
    string MainWindowTitle,
    bool IsSilent,
    DateTime StartTime);

/// <summary>
/// O monitor de desinstaladores.
///
/// Alguns desinstaladores rodam sem mostrar nada: o programa aparece como
/// um processo sem janela. E o caso que o BCU chama de "desinstalador
/// silencioso" — e o que mais atrapalha, porque o usuario nao sabe que a
/// desinstalacao esta acontecendo.
///
/// Este modulo lista os processos que sao desinstaladores, e diz quais deles
/// estao rodando sem janela.
///
/// A regra esta em um lugar so: este modulo sabe reconhecer um desinstalador
/// e dizer se ele esta silencioso.
/// </summary>
public static class UninstallerWatcher
{
    /// <summary>
    /// Os nomes de executavel que sao desinstaladores conhecidos.
    ///
    /// A lista vem do que o proprio Tranal ja mede: e o mesmo conjunto de
    /// detectores de desinstalador do nucleo (Inno Setup, NSIS, InstallShield,
    /// Windows Installer, Squirrel, InstallAware, Advanced Installer,
    /// Setup Factory e SFX do 7-Zip).
    /// </summary>
    private static readonly string[] KnownUninstallers =
    {
        "unins000.exe",
        "uninstall.exe",
        "uninstaller.exe",
        "msiexec.exe",
        "setup.exe",
        "install.exe",
        "uninstall",
    };

    /// <summary>
    /// Os processos que sao desinstaladores, e se estao silenciosos.
    /// </summary>
    public static IReadOnlyList<UninstallerProcess> List()
    {
        var encontrados = new List<UninstallerProcess>();

        Process[] processos;

        try
        {
            processos = Process.GetProcesses();
        }
        catch
        {
            return Array.Empty<UninstallerProcess>();
        }

        foreach (var processo in processos)
        {
            try
            {
                var nome = processo.ProcessName;
                if (string.IsNullOrWhiteSpace(nome)) continue;
                if (!IsUninstallerName(nome)) continue;

                var titulo = processo.MainWindowTitle ?? string.Empty;

                var inicio = DateTime.MinValue;
                try
                {
                    inicio = processo.StartTime;
                }
                catch
                {
                    // Processo protegido: sem data de inicio.
                }

                encontrados.Add(new UninstallerProcess(
                    processo.Id,
                    nome,
                    titulo,
                    titulo.Length == 0,
                    inicio));
            }
            catch
            {
                // Processo que morreu no meio da leitura.
            }
            finally
            {
                processo.Dispose();
            }
        }

        return encontrados;
    }

    /// <summary>
    /// Ha um desinstalador rodando em silencio agora?
    /// </summary>
    public static bool HasSilentUninstaller() => List().Any(p => p.IsSilent);

    /// <summary>
    /// O nome do executavel e de um desinstalador conhecido?
    /// </summary>
    /// <param name="processName">O nome do processo.</param>
    public static bool IsUninstallerName(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return false;

        return KnownUninstallers.Any(u =>
            string.Equals(u, processName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// O texto do aviso para o usuario.
    /// </summary>
    public static string Describe()
    {
        var silenciosos = List().Where(p => p.IsSilent).ToList();

        if (silenciosos.Count == 0)
        {
            return "Nenhum desinstalador rodando em silencio.";
        }

        return $"{silenciosos.Count} desinstalador(es) rodando sem janela: "
             + string.Join(", ", silenciosos.Select(p => p.Name));
    }
}
