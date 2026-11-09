using System.IO;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Tools;
using Tranal.Uninstaller.Core.Management;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// Um programa encontrado por um atalho, que nao esta na lista de instalados.
/// </summary>
public sealed class ShortcutApp
{
    /// <summary>O nome do atalho (sem extensao).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>O caminho do executavel alvo.</summary>
    public string TargetPath { get; init; } = string.Empty;

    /// <summary>O caminho do atalho.</summary>
    public string ShortcutPath { get; init; } = string.Empty;

    /// <summary>Onde o atalho foi encontrado.</summary>
    public string Location { get; init; } = string.Empty;

    /// <summary>O tamanho do executavel alvo.</summary>
    public long SizeBytes { get; init; }

    /// <summary>True quando o alvo esta em uma pasta de programa.</summary>
    public bool LooksInstalled { get; init; }
}

/// <summary>
/// Descobre programas pelos atalhos do Windows.
///
/// O Uninstalr usa os atalhos como uma das fontes de descoberta: um programa
/// que nao tem chave de desinstalacao no registro (aplicacao portatil, jogo
/// de loja) aparece no Menu Iniciar mesmo assim. O Geek tambem lista os
/// atalhos do Menu Iniciar e da Area de Trabalho.
///
/// A REGRA mora aqui, em um lugar so: a varredura dos atalhos e o filtro do
/// que ja esta na lista de instalados ficam aqui. O alvo do atalho e lido pelo
/// modulo de atalhos, que ja existe no nucleo.
/// </summary>
public static class ShortcutDiscovery
{
    /// <summary>
    /// As pastas onde o Windows guarda os atalhos.
    /// </summary>
    public static IReadOnlyList<(string Location, string Folder)> Locations()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var common = Environment.GetFolderPath(Environment.SpecialFolder.CommonPictures);
        var publica = Path.GetFullPath(Path.Combine(common, "..", "Desktop"));

        return new List<(string, string)>
        {
            ("Menu Iniciar", Path.Combine(appData, "Microsoft", "Windows", "Start Menu", "Programs")),
            ("Menu Iniciar (todos)", Path.Combine(programData, "Microsoft", "Windows", "Start Menu", "Programs")),
            ("Area de Trabalho", Path.Combine(userProfile, "Desktop")),
            ("Area de Trabalho (publica)", publica),
        };
    }

    /// <summary>
    /// Os programas encontrados pelos atalhos que ainda nao estao na lista de
    /// instalados.
    /// </summary>
    /// <param name="jaInstalados">
    /// Os programas que ja estao na lista: os atalhos deles sao ignorados.
    /// </param>
    public static IReadOnlyList<ShortcutApp> Discover(IEnumerable<InstalledProgram>? jaInstalados)
    {
        var conhecidos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (jaInstalados is not null)
        {
            foreach (var programa in jaInstalados)
            {
                if (programa is null) continue;

                foreach (var caminho in CandidatesOf(programa))
                {
                    if (caminho.Length > 0) conhecidos.Add(caminho);
                }
            }
        }

        var achados = new List<ShortcutApp>();
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (location, folder) in Locations())
        {
            if (!Directory.Exists(folder)) continue;

            IEnumerable<string> atalhos;
            try
            {
                atalhos = Directory.EnumerateFiles(folder, "*.lnk", SearchOption.AllDirectories).ToList();
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao listar os atalhos de '{folder}': {ex.Message}");
                continue;
            }

            foreach (var atalho in atalhos)
            {
                try
                {
                    var alvo = ShortcutFixer.ReadTarget(atalho);
                    if (alvo.Length == 0) continue;

                    var normalizado = PathUtil.Normalize(alvo);
                    if (conhecidos.Contains(normalizado)) continue;
                    if (!vistos.Add(normalizado)) continue;

                    achados.Add(new ShortcutApp
                    {
                        Name = Path.GetFileNameWithoutExtension(atalho),
                        TargetPath = normalizado,
                        ShortcutPath = atalho,
                        Location = location,
                        SizeBytes = SafeLength(normalizado),
                        LooksInstalled = LooksInstalled(normalizado),
                    });
                }
                catch (Exception ex)
                {
                    ToolLog.Error($"Falha ao inspecionar o atalho '{atalho}': {ex.Message}");
                }
            }
        }

        return achados.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Os caminhos pelos quais um programa instalado pode ser reconhecido
    /// quando o atalho aponta para ele.
    /// </summary>
    private static IEnumerable<string> CandidatesOf(InstalledProgram programa)
    {
        if (!string.IsNullOrWhiteSpace(programa.InstallLocation))
        {
            yield return PathUtil.Normalize(programa.InstallLocation);
        }

        var icone = ExecutablePathResolver.Extract(programa.DisplayIcon ?? string.Empty);
        if (icone.Length > 0) yield return PathUtil.Normalize(icone);

        var executavel = UsageSignals.ExecutableOf(programa);
        if (executavel.Length > 0) yield return PathUtil.Normalize(executavel);
    }

    /// <summary>
    /// True quando o alvo parece estar instalado (dentro de uma pasta de
    /// programa, e nao na area de trabalho ou na pasta de downloads).
    /// </summary>
    private static bool LooksInstalled(string path)
    {
        if (path.Length == 0) return false;

        var pasta = Path.GetDirectoryName(path) ?? string.Empty;
        if (pasta.Length == 0) return false;

        var marcas = new[]
        {
            "\\program files",
            "\\program files (x86)",
            "\\programdata",
            "\\apps\\",
            "\\games\\",
        };

        var pastaBaixa = pasta.ToLowerInvariant();

        foreach (var marca in marcas)
        {
            if (pastaBaixa.Contains(marca, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    /// <summary>
    /// O tamanho de um arquivo, quando ele pode ser lido.
    /// </summary>
    private static long SafeLength(string path)
    {
        try
        {
            if (path.Length == 0) return 0;
            if (!File.Exists(path)) return 0;
            return new FileInfo(path).Length;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao medir '{path}': {ex.Message}");
            return 0;
        }
    }
}
