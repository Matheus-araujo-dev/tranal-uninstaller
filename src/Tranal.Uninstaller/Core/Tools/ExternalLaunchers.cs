using System.Diagnostics;
using System.IO;

namespace Tranal.Uninstaller.Core.Tools;

/// <summary>
/// Uma ferramenta externa que o Tranal sabe abrir.
/// </summary>
/// <param name="Id">O identificador fixo da ferramenta.</param>
/// <param name="DisplayName">O nome que a tela mostra.</param>
/// <param name="ExecutablePath">O executavel. Vazio quando a ferramenta e um comando do proprio Windows.</param>
/// <param name="Arguments">Os argumentos do comando.</param>
/// <param name="Available">O executavel foi encontrado nesta maquina?</param>
public sealed record ExternalTool(
    string Id,
    string DisplayName,
    string ExecutablePath,
    string Arguments,
    bool Available);

/// <summary>
/// As ferramentas de fora que o Tranal sabe abrir.
///
/// Sao dois casos que aparecem em varios concorrentes: abrir a pasta do
/// programa em um gerenciador de arquivos alternativo (o Total Commander, no
/// caso do Uninstall Tool) e abrir o gerenciador de credenciais do Windows
/// (o Wise).
///
/// Este e o unico lugar que sabe onde essas ferramentas ficam. A tela e a
/// linha de comando consultam a mesma lista — e o que a tela mostra e o que
/// a linha de comando executa.
/// </summary>
public static class ExternalLaunchers
{
    /// <summary>
    /// Os caminhos onde o Total Commander costuma ficar. A lista e uma
    /// **pesquisa**, nao um palpite: o executavel e procurado em cada um
    /// deles, e o primeiro que existir vence.
    /// </summary>
    private static IEnumerable<string> TotalCommanderPaths()
    {
        var roots = new List<string>();

        foreach (var special in new[]
        {
            Environment.SpecialFolder.ProgramFiles,
            Environment.SpecialFolder.ProgramFilesX86,
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolder.ApplicationData,
        })
        {
            var root = Environment.GetFolderPath(special);
            if (!string.IsNullOrWhiteSpace(root)) roots.Add(root);
        }

        foreach (var root in roots)
        {
            yield return Path.Combine(root, "Total Commander", "TotalCommander.exe");
            yield return Path.Combine(root, "Total Commander", "TOTALCMD.EXE");
            yield return Path.Combine(root, "Total Commander", "Total Commander.exe");
        }
    }

    /// <summary>
    /// O caminho do Total Commander nesta maquina. Nulo quando ele nao esta
    /// instalado — e a tela diz isso, em vez de prometer um atalho que nao
    /// abre nada.
    /// </summary>
    public static string? FindTotalCommander()
    {
        foreach (var candidate in TotalCommanderPaths())
        {
            try
            {
                if (File.Exists(candidate)) return candidate;
            }
            catch
            {
                // Um caminho inacessivel e um caminho que nao tem o programa.
            }
        }

        return null;
    }

    /// <summary>
    /// Abre uma pasta no Total Commander.
    /// </summary>
    /// <param name="path">A pasta (ou o arquivo) a abrir.</param>
    /// <returns>Verdadeiro quando o Total Commander foi encontrado e chamado.</returns>
    public static bool OpenInTotalCommander(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        var exe = FindTotalCommander();
        if (exe is null) return false;

        try
        {
            var folder = Directory.Exists(path) ? path : Path.GetDirectoryName(path) ?? string.Empty;
            if (folder.Length == 0) folder = path;

            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = "\"" + folder + "\"",
                UseShellExecute = true,
            });

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Abre o Gerenciador de Credenciais do Windows.
    /// </summary>
    /// <remarks>
    /// O gerenciador nao tem executavel proprio: ele e uma biblioteca do
    /// Windows (<c>keymgr.dll</c>) mostrada pelo <c>rundll32</c>. E esse o
    /// caminho que o proprio Windows usa.
    /// </remarks>
    public static bool OpenCredentialManager()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "rundll32.exe",
                Arguments = "keymgr.dll,KRShowKeyMgr",
                UseShellExecute = true,
            });

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Abre uma pasta no Explorer.
    /// </summary>
    /// <param name="path">A pasta a abrir.</param>
    public static bool OpenFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        try
        {
            var folder = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(folder)) return false;

            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true,
            });

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// As ferramentas externas conhecidas e o estado delas nesta maquina.
    /// </summary>
    public static IReadOnlyList<ExternalTool> Known()
    {
        var totalCommander = FindTotalCommander();

        return new[]
        {
            new ExternalTool(
                "TotalCommander",
                "Total Commander",
                totalCommander ?? string.Empty,
                string.Empty,
                totalCommander is not null),
            new ExternalTool(
                "CredentialManager",
                "Gerenciador de Credenciais",
                "rundll32.exe",
                "keymgr.dll,KRShowKeyMgr",
                true),
        };
    }
}
