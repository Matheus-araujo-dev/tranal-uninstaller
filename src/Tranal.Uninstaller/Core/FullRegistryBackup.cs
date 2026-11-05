using System.Diagnostics;
using System.IO;
using System.Text;

namespace Tranal.Uninstaller.Core;

/// <summary>Resultado da criacao de um backup completo do registro.</summary>
public sealed class FullRegistryBackupResult
{
    /// <summary>True quando todos os hives foram exportados.</summary>
    public bool Success { get; init; }

    /// <summary>Pasta onde o backup foi gravado.</summary>
    public string Directory { get; init; } = string.Empty;

    /// <summary>Arquivos .reg gerados.</summary>
    public IReadOnlyList<string> Files { get; init; } = Array.Empty<string>();

    /// <summary>Explicacao do resultado (vazia quando deu certo).</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Script de restauracao do backup.</summary>
    public string RestoreScript { get; init; } = string.Empty;
}

/// <summary>
/// Backup completo do registro do Windows.
///
/// Corresponde as opcoes do Revo Uninstaller Pro "Make a System Restore Point
/// and a full Registry backup before uninstalling" (15162), "Create a full
/// Registry Backup before uninstall" (15307) e as mensagens "Creating a full
/// Registry backup..." (15163), "... - done!" (15165) e "... - failed!" (15166).
///
/// A exportacao usa o proprio <c>reg.exe</c> do Windows: e o caminho nativo,
/// preserva todos os tipos de valor (inclusive REG_BINARY, REG_QWORD e
/// REG_MULTI_SZ) e nao depende de o Tranal saber reescrever cada tipo sem
/// erro — que e exatamente o tipo de bug que deixa um backup inutilizavel.
///
/// O backup e um arquivo .reg por hive, mais um script .bat que importa todos
/// eles de volta.
/// </summary>
public static class FullRegistryBackup
{
    /// <summary>
    /// Hives exportados. A ordem nao importa para a restauracao (cada script
    /// importa o seu), mas HKLM e HKU vem primeiro por serem os maiores.
    /// </summary>
    public static IReadOnlyList<string> Hives { get; } = new[]
    {
        "HKLM",
        "HKU",
        "HKCU",
        "HKCR",
        "HKCC",
    };

    /// <summary>
    /// Substitui a lista de hives exportados. Existe para os testes: exportar
    /// o registro inteiro levaria minutos e centenas de megabytes, e um teste
    /// nao pode fazer isso na maquina de quem roda a suite.
    /// Nulo em uso normal.
    /// </summary>
    internal static IReadOnlyList<string>? HivesOverride { get; set; }

    /// <summary>Nome do script de restauracao.</summary>
    public const string RestoreScriptName = "Restaurar.bat";

    /// <summary>
    /// Pasta padrao do backup completo: uma subpasta com data e hora, para que
    /// cada desinstalacao tenha o proprio backup.
    ///
    /// A resolucao e de milissegundos: duas desinstalacoes no mesmo segundo
    /// nao podem compartilhar a mesma pasta (a segunda sobrescreveria a
    /// primeira, e o backup anterior sumiria sem aviso).
    /// </summary>
    public static string DefaultDirectory =>
        Path.Combine(RegistryBackup.DataDirectory, "RegBackup", "Full",
            DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff"));

    /// <summary>
    /// Cria o backup completo do registro e devolve o resultado.
    /// </summary>
    /// <param name="destination">Pasta de destino. Vazio usa <see cref="DefaultDirectory"/>.</param>
    public static FullRegistryBackupResult Create(string? destination = null)
    {
        var dir = destination ?? DefaultDirectory;
        var files = new List<string>();
        var failures = new List<string>();

        try
        {
            Directory.CreateDirectory(dir);
        }
        catch (Exception ex)
        {
            return new FullRegistryBackupResult
            {
                Success = false,
                Directory = dir,
                Message = $"não foi possível criar a pasta do backup: {ex.Message}",
            };
        }

        foreach (var hive in HivesOverride ?? Hives)
        {
            var file = Path.Combine(dir, SafeFileName(hive));
            var outcome = ExportHive(hive, file);

            if (outcome.Ok && File.Exists(file))
            {
                files.Add(file);
            }
            else
            {
                failures.Add($"{hive}: {outcome.Message}");
            }
        }

        var restoreScript = Path.Combine(dir, RestoreScriptName);
        if (files.Count > 0)
        {
            try
            {
                File.WriteAllText(restoreScript, BuildRestoreScript(files), Encoding.ASCII);
            }
            catch (Exception ex)
            {
                failures.Add($"{RestoreScriptName}: {ex.Message}");
            }
        }

        return new FullRegistryBackupResult
        {
            // Um backup completo so e considered valido quando TODOS os hives
            // foram exportados: um backup pela metade daria uma falsa
            // seguranca ao usuario.
            Success = failures.Count == 0 && files.Count == (HivesOverride ?? Hives).Count,
            Directory = dir,
            Files = files,
            RestoreScript = files.Count > 0 ? restoreScript : string.Empty,
            Message = failures.Count == 0
                ? $"backup completo do registro criado em {dir}"
                : string.Join("; ", failures),
        };
    }

    /// <summary>Exporta um hive com o reg.exe do Windows.</summary>
    private static (bool Ok, string Message) ExportHive(string hive, string file)
    {
        var reg = Path.Combine(Environment.SystemDirectory, "reg.exe");
        if (!File.Exists(reg))
            return (false, "reg.exe não encontrado");

        try
        {
            var info = new ProcessStartInfo(reg, $"export {hive} \"{file}\" /y")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var process = Process.Start(info);
            if (process is null) return (false, "reg.exe não pôde ser iniciado");

            // As duas saidas sao lidas em paralelo: ler uma depois da outra
            // pode travar o processo quando o buffer do outro enche.
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(10 * 60 * 1000))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return (false, "tempo esgotado na exportação");
            }

            var stderr = error.GetAwaiter().GetResult().Trim();
            if (process.ExitCode != 0)
                return (false, string.IsNullOrEmpty(stderr) ? $"reg.exe saiu com o código {process.ExitCode}" : stderr);

            return (true, output.GetAwaiter().GetResult());
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Nome de arquivo seguro para o hive exportado. Um caminho de subchave
    /// (usado apenas pelos testes) nao pode virar nome de arquivo com barras.
    /// </summary>
    private static string SafeFileName(string hive)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(hive.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return safe + ".reg";
    }

    /// <summary>Monta o script que restaura o backup (equivalente ao Restore.dat do Revo).</summary>
    private static string BuildRestoreScript(IEnumerable<string> files)
    {
        var sb = new StringBuilder();
        sb.AppendLine("@echo off");
        sb.AppendLine("echo Restaurando o backup completo do registro do Tranal Uninstaller...");
        sb.AppendLine("echo.");

        foreach (var file in files)
            sb.AppendLine($"reg import \"{file}\"");

        sb.AppendLine("echo.");
        sb.AppendLine("echo Concluido.");
        return sb.ToString();
    }
}
