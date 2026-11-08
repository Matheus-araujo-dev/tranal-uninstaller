using System.IO;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Uninstaller.Core.Modules;

/// <summary>
/// Um atalho do Windows e o estado dele.
/// </summary>
public sealed class ShortcutIssue
{
    /// <summary>O caminho do arquivo .lnk.</summary>
    public string ShortcutPath { get; init; } = string.Empty;

    /// <summary>O nome do atalho (sem extensao).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>O alvo gravado no atalho.</summary>
    public string TargetPath { get; init; } = string.Empty;

    /// <summary>O tipo do problema encontrado.</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>Onde o atalho foi encontrado.</summary>
    public string Location { get; init; } = string.Empty;

    /// <summary>True quando existe um programa instalado que pode ser o alvo correto.</summary>
    public bool CanRepair { get; init; }

    /// <summary>O alvo candidato para o reparo.</summary>
    public string RepairTarget { get; init; } = string.Empty;

    /// <summary>True quando o atalho esta marcado para a acao escolhida.</summary>
    public bool Selected { get; set; } = true;
}

/// <summary>
/// Resultado do reparo de um atalho.
/// </summary>
public sealed class ShortcutRepairResult
{
    /// <summary>O atalho tratado.</summary>
    public string ShortcutPath { get; init; } = string.Empty;

    /// <summary>True quando o atalho foi reparado.</summary>
    public bool Repaired { get; init; }

    /// <summary>True quando o atalho foi removido.</summary>
    public bool Removed { get; init; }

    /// <summary>Explicacao do resultado.</summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Varredura e reparo de atalhos quebrados.
///
/// O HiBit tem um "Broken Shortcut Fixer" e o Geek Uninstaller faz a varredura
/// de atalhos do Menu Iniciar e da Area de Trabalho para achar o icone e a
/// localizacao de um programa. O Revo tambem lista os atalhos do programa
/// installado.
///
/// A REGRA mora aqui, em um lugar so: quem precisa do alvo de um atalho
/// consome o <see cref="ShortcutMap"/> (que ja le atalhos) e este modulo cuida
/// do diagnostico e do reparo.
///
/// O alvo do atalho e lido pelo <c>WScript.Shell</c>, que e a mesma API que o
/// Windows usa para resolver atalhos — inclusive os atalhos do Windows Store,
/// que nao gravam caminho de arquivo e por isso aparecem como "alvo vazio".
/// </summary>
public static class ShortcutFixer
{
    /// <summary>Problema: o atalho aponta para um arquivo que nao existe mais.</summary>
    public const string KindMissingTarget = "Alvo ausente";

    /// <summary>Problema: o atalho aponta para uma pasta que nao existe mais.</summary>
    public const string KindMissingFolder = "Pasta ausente";

    /// <summary>Problema: o atalho nao grava alvo (atalho do Windows Store).</summary>
    public const string KindEmptyTarget = "Alvo vazio";

    /// <summary>
    /// As pastas onde os atalhos do Windows ficam.
    /// </summary>
    public static IReadOnlyList<(string Location, string Folder)> Locations()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var publicProfile = Environment.GetFolderPath(Environment.SpecialFolder.CommonPictures);

        // A area de trabalho publica fica em "Public\Desktop", nao em "Public\Pictures".
        var publicFolder = Path.Combine(publicProfile, "..", "Desktop");

        return new List<(string, string)>
        {
            ("Menu Iniciar", Path.Combine(appData, "Microsoft", "Windows", "Start Menu", "Programs")),
            ("Menu Iniciar (todos)", Path.Combine(programData, "Microsoft", "Windows", "Start Menu", "Programs")),
            ("Area de Trabalho", Path.Combine(userProfile, "Desktop")),
            ("Area de Trabalho (publica)", Path.GetFullPath(publicFolder)),
        };
    }

    /// <summary>
    /// Varre os atalhos e devolve os que estao com problema.
    /// </summary>
    /// <param name="programs">
    /// Os programas instalados: quando vem informado, o modulo procura o alvo
    /// correto para cada atalho quebrado.
    /// </param>
    public static IReadOnlyList<ShortcutIssue> Scan(IEnumerable<InstalledProgram>? programs = null)
    {
        var problemas = new List<ShortcutIssue>();

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
                    var issue = Analyze(atalho, location, programs);
                    if (issue is not null) problemas.Add(issue);
                }
                catch (Exception ex)
                {
                    ToolLog.Error($"Falha ao inspecionar o atalho '{atalho}': {ex.Message}");
                }
            }
        }

        return problemas;
    }

    /// <summary>
    /// Repara o atalho: quando existe um alvo candidato, o atalho e apontado
    /// para ele; quando nao existe, o atalho quebrado e removido.
    /// </summary>
    /// <param name="issue">O atalho a reparar.</param>
    /// <param name="removeWhenUnrepairable">Remove o atalho quando nao ha alvo candidato.</param>
    public static ShortcutRepairResult Repair(ShortcutIssue issue, bool removeWhenUnrepairable = false)
    {
        if (issue is null)
            return new ShortcutRepairResult { Message = "Atalho nao informado." };

        if (!issue.CanRepair || issue.RepairTarget.Length == 0)
        {
            if (!removeWhenUnrepairable)
            {
                return new ShortcutRepairResult
                {
                    ShortcutPath = issue.ShortcutPath,
                    Message = "Nenhum programa instalado corresponde a este atalho.",
                };
            }

            try
            {
                File.Delete(issue.ShortcutPath);
                return new ShortcutRepairResult
                {
                    ShortcutPath = issue.ShortcutPath,
                    Removed = true,
                    Message = "Atalho quebrado removido.",
                };
            }
            catch (Exception ex)
            {
                return new ShortcutRepairResult
                {
                    ShortcutPath = issue.ShortcutPath,
                    Message = $"Nao foi possivel remover o atalho: {ex.Message}",
                };
            }
        }

        try
        {
            if (!WriteTarget(issue.ShortcutPath, issue.RepairTarget))
            {
                return new ShortcutRepairResult
                {
                    ShortcutPath = issue.ShortcutPath,
                    Message = "Nao foi possivel gravar o novo alvo do atalho.",
                };
            }

            return new ShortcutRepairResult
            {
                ShortcutPath = issue.ShortcutPath,
                Repaired = true,
                Message = $"Atalho apontado para '{issue.RepairTarget}'.",
            };
        }
        catch (Exception ex)
        {
            return new ShortcutRepairResult
            {
                ShortcutPath = issue.ShortcutPath,
                Message = $"Falha ao reparar o atalho: {ex.Message}",
            };
        }
    }

    /// <summary>
    /// Remove os atalhos marcados e devolve o relatorio canonico.
    /// </summary>
    /// <param name="issues">Os atalhos marcados para remocao.</param>
    public static RemovalReport Remove(IEnumerable<ShortcutIssue> issues)
    {
        var outcomes = new List<RemovalOutcome>();
        var sobras = new List<LeftoverItem>();

        foreach (var issue in issues)
        {
            if (!issue.Selected) continue;

            sobras.Add(new LeftoverItem
            {
                Kind = "Arquivo",
                Path = issue.ShortcutPath,
                Detail = issue.Kind,
                Selected = true,
            });
        }

        if (sobras.Count == 0) return new RemovalReport(outcomes);

        try
        {
            outcomes.AddRange(UninstallEngine.RemoveLeftovers(sobras, new UninstallRemovalOptions
            {
                Backup = false,
                UseRecycleBin = true,
                DeferToNextRestart = true,
            }).Outcomes);
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao remover os atalhos quebrados: {ex.Message}");
        }

        return new RemovalReport(outcomes);
    }

    /// <summary>
    /// Le o alvo de um atalho. Devolve string vazia quando o atalho nao tem
    /// alvo gravado.
    /// </summary>
    /// <param name="shortcutPath">O caminho do .lnk.</param>
    public static string ReadTarget(string? shortcutPath)
    {
        if (string.IsNullOrWhiteSpace(shortcutPath)) return string.Empty;

        try
        {
            dynamic? shell = CreateShell();
            if (shell is null) return string.Empty;

            dynamic? shortcut = null;
            try
            {
                shortcut = shell.CreateShortcut(shortcutPath);
                return (shortcut.TargetPath as string) ?? string.Empty;
            }
            finally
            {
                Release(shortcut);
                Release(shell);
            }
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao ler o alvo do atalho '{shortcutPath}': {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Grava um novo alvo no atalho.
    /// </summary>
    /// <param name="shortcutPath">O caminho do .lnk.</param>
    /// <param name="target">O novo alvo.</param>
    public static bool WriteTarget(string? shortcutPath, string? target)
    {
        if (string.IsNullOrWhiteSpace(shortcutPath)) return false;
        if (string.IsNullOrWhiteSpace(target)) return false;

        try
        {
            dynamic? shell = CreateShell();
            if (shell is null) return false;

            dynamic? shortcut = null;
            try
            {
                shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = target;
                shortcut.Save();
                return true;
            }
            finally
            {
                Release(shortcut);
                Release(shell);
            }
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao gravar o alvo do atalho '{shortcutPath}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Analisa um atalho e devolve o problema (ou <c>null</c> quando esta
    /// tudo certo).
    /// </summary>
    private static ShortcutIssue? Analyze(string shortcutPath, string location, IEnumerable<InstalledProgram>? programs)
    {
        var target = ReadTarget(shortcutPath);

        if (target.Length == 0)
        {
            return new ShortcutIssue
            {
                ShortcutPath = shortcutPath,
                Name = Path.GetFileNameWithoutExtension(shortcutPath),
                TargetPath = string.Empty,
                Kind = KindEmptyTarget,
                Location = location,
            };
        }

        var full = PathUtil.Normalize(target);
        var existe = File.Exists(full) || Directory.Exists(full);
        if (existe) return null;

        var kind = Directory.Exists(Path.GetDirectoryName(full) ?? string.Empty)
            ? KindMissingFolder
            : KindMissingTarget;

        var candidato = FindCandidate(Path.GetFileNameWithoutExtension(shortcutPath), programs);

        return new ShortcutIssue
        {
            ShortcutPath = shortcutPath,
            Name = Path.GetFileNameWithoutExtension(shortcutPath),
            TargetPath = full,
            Kind = kind,
            Location = location,
            CanRepair = candidato.Length > 0,
            RepairTarget = candidato,
        };
    }

    /// <summary>
    /// Procura, entre os programas instalados, o executavel que combina com
    /// o nome do atalho.
    /// </summary>
    private static string FindCandidate(string name, IEnumerable<InstalledProgram>? programs)
    {
        if (programs is null) return string.Empty;
        if (name.Length == 0) return string.Empty;

        foreach (var programa in programs)
        {
            if (programa is null) continue;

            var location = programa.InstallLocation;
            if (string.IsNullOrWhiteSpace(location)) continue;

            if (!Directory.Exists(location)) continue;

            string[] candidatos;
            try
            {
                candidatos = Directory.EnumerateFiles(location, "*.exe", SearchOption.TopDirectoryOnly).ToList().ToArray();
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao listar os executaveis de '{location}': {ex.Message}");
                continue;
            }

            var exato = candidatos.FirstOrDefault(c =>
                string.Equals(Path.GetFileNameWithoutExtension(c), name, StringComparison.OrdinalIgnoreCase));

            if (exato is not null) return PathUtil.Normalize(exato);

            // Sem correspondencia exata: o primeiro executavel do programa, quando
            // o nome do atalho comeca com o nome do programa.
            if (NameMatch.StartsWithWord(name, programa.DisplayName))
            {
                var primeiro = candidatos.FirstOrDefault();
                if (primeiro is not null) return PathUtil.Normalize(primeiro);
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Cria o objeto COM do shell, ou <c>null</c> quando o Windows nao
    /// oferece o componente.
    /// </summary>
    private static object? CreateShell()
    {
        var type = Type.GetTypeFromProgID("WScript.Shell");
        if (type is null) return null;
        return Activator.CreateInstance(type);
    }

    /// <summary>
    /// Libera o objeto COM, se ele for um objeto COM.
    /// </summary>
    private static void Release(object? instance)
    {
        if (instance is null) return;

        try
        {
            if (System.Runtime.InteropServices.Marshal.IsComObject(instance))
            {
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(instance);
            }
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao liberar o objeto COM: {ex.Message}");
        }
    }
}
