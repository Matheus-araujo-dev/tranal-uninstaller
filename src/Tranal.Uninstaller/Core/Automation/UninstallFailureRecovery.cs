using System.IO;
using System.Text;
using System.Text.Json;
using Tranal.Uninstaller.Core.RegistryEditing;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Uninstaller.Core.Automation;

/// <summary>
/// Uma desinstalacao que falhou e ficou registrada.
/// </summary>
public sealed class FailedUninstall
{
    /// <summary>O nome do programa.</summary>
    public required string ProgramName { get; set; }

    /// <summary>O comando de desinstalacao que foi tentado.</summary>
    public string UninstallString { get; set; } = string.Empty;

    /// <summary>A pasta de instalacao do programa.</summary>
    public string InstallLocation { get; set; } = string.Empty;

    /// <summary>A chave de registro que descreve o programa.</summary>
    public string RegistryPath { get; set; } = string.Empty;

    /// <summary>O codigo de saida do desinstalador nativo.</summary>
    public int ExitCode { get; set; }

    /// <summary>A mensagem da falha.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Quando a falha aconteceu.</summary>
    public DateTime FailedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// O registro das desinstalacoes que falharam, e a limpeza que o usuario
/// pode pedir depois.
///
/// O Geek Uninstaller mostra um dialogo de falha de desinstalacao com a
/// opcao de limpar a entrada do registro e os arquivos. Aqui a falha e
/// GRAVADA no momento em que acontece, e a limpeza fica disponivel depois,
/// na tela e na CLI.
///
/// A REGRA mora aqui, em um lugar so: quem registra a falha e quem limpa
/// usam esta classe.
/// </summary>
public static class UninstallFailureRecovery
{
    /// <summary>A pasta onde o registro das falhas e gravado.</summary>
    public static string DataDirectory => System.IO.Path.Combine(
        RegistryBackup.DataDirectory, "failures");

    /// <summary>O arquivo do registro das falhas.</summary>
    public static string FilePath => System.IO.Path.Combine(DataDirectory, "failed-uninstalls.json");

    /// <summary>
    /// Grava a falha de uma desinstalacao.
    ///
    /// A mesma entrada do mesmo programa e atualizada: a falha de hoje
    /// substitui a de ontem, e a lista nao cresce a cada tentativa.
    /// </summary>
    /// <param name="program">O programa que nao pode ser desinstalado.</param>
    /// <param name="result">O resultado do desinstalador nativo.</param>
    public static void Record(InstalledProgram program, BuiltInUninstallResult result)
    {
        if (program is null || result is null) return;
        if (result.Succeeded) return;

        try
        {
            var lista = List().ToList();

            var existente = lista.FirstOrDefault(
                f => string.Equals(f.ProgramName, program.DisplayName, StringComparison.OrdinalIgnoreCase));

            if (existente is null)
            {
                existente = new FailedUninstall { ProgramName = program.DisplayName };
                lista.Add(existente);
            }

            existente.UninstallString = program.UninstallString;
            existente.InstallLocation = program.InstallLocation;
            existente.RegistryPath = program.RegistryPath;
            existente.ExitCode = result.ExitCode;
            existente.Message = result.Message.Length > 0 ? result.Message : result.Describe();
            existente.FailedAt = DateTime.Now;

            Save(lista);
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao registrar a desinstalacao com erro: {ex.Message}");
        }
    }

    /// <summary>
    /// As desinstalacoes que falharam, da mais recente para a mais antiga.
    /// </summary>
    public static IReadOnlyList<FailedUninstall> List()
    {
        try
        {
            if (!File.Exists(FilePath)) return Array.Empty<FailedUninstall>();

            var json = File.ReadAllText(FilePath, Encoding.UTF8);
            if (json.Trim().Length == 0) return Array.Empty<FailedUninstall>();

            var lista = JsonSerializer.Deserialize<List<FailedUninstall>>(json);
            if (lista is null) return Array.Empty<FailedUninstall>();

            return lista.OrderByDescending(f => f.FailedAt).ToList();
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao ler o registro de desinstalacoes com erro: {ex.Message}");
            return Array.Empty<FailedUninstall>();
        }
    }

    /// <summary>
    /// Esquece uma falha (sem limpar nada do sistema).
    /// </summary>
    /// <param name="programName">O nome do programa.</param>
    public static bool Forget(string? programName)
    {
        if (string.IsNullOrWhiteSpace(programName)) return false;

        var lista = List().Where(f => !string.Equals(f.ProgramName, programName, StringComparison.OrdinalIgnoreCase)).ToList();
        Save(lista);
        return true;
    }

    /// <summary>
    /// Limpa a entrada do registro e os arquivos do programa que falhou.
    ///
    /// E a opcao que o Geek Uninstaller oferece no dialogo de falha: quando
    /// o desinstalador nativo nao conseguiu, o usuario ainda quer tirar o
    /// programa do sistema.
    /// </summary>
    /// <param name="failed">A falha registrada.</param>
    /// <param name="removeRegistry">Apagar a entrada de registro do programa.</param>
    /// <param name="removeFiles">Apagar os arquivos e pastas que sobraram.</param>
    public static (bool Ok, string Message) Cleanup(
        FailedUninstall failed, bool removeRegistry = true, bool removeFiles = true)
    {
        if (failed is null) return (false, "falha nao informada");

        var partes = new List<string>();
        var ok = true;

        if (removeRegistry && failed.RegistryPath.Length > 0)
        {
            var resultado = RegistryEditor.DeleteKey(failed.RegistryPath);
            if (resultado.Success)
            {
                partes.Add("entrada de registro removida");
            }
            else
            {
                ok = false;
                partes.Add($"entrada de registro preservada ({resultado.Message})");
            }
        }

        if (removeFiles)
        {
            var program = new InstalledProgram
            {
                DisplayName = failed.ProgramName,
                UninstallString = failed.UninstallString,
                InstallLocation = failed.InstallLocation,
                RegistryPath = failed.RegistryPath,
            };

            var opcoes = new LeftoverScanOptions { Mode = UninstallMode.Advanced };
            var sobras = LeftoverScanner.Scan(program, opcoes)
                .Where(i => i.Kind is "Arquivo" or "Pasta")
                .ToList();

            if (sobras.Count == 0)
            {
                partes.Add("nenhum arquivo sobrando");
            }
            else
            {
                var relatorio = UninstallEngine.RemoveLeftovers(sobras, UninstallRemovalOptions.FromSettings());
                partes.Add($"{relatorio.RemovedCount} item(ns) removido(s)");
                if (relatorio.FailedCount > 0)
                {
                    ok = false;
                    partes.Add($"{relatorio.FailedCount} item(ns) com falha");
                }
            }
        }

        if (ok) Forget(failed.ProgramName);

        return (ok, string.Join("; ", partes));
    }

    private static void Save(List<FailedUninstall> lista)
    {
        try
        {
            System.IO.Directory.CreateDirectory(DataDirectory);
            var json = JsonSerializer.Serialize(lista, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json, new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao gravar o registro de desinstalacoes com erro: {ex.Message}");
        }
    }
}
