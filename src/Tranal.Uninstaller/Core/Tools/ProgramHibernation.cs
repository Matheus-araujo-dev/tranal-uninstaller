using System.IO;
using System.Text;
using System.Text.Json;
using Tranal.Uninstaller.Core.Management;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Uninstaller.Core.Tools;

/// <summary>
/// Um item de inicializacao que foi desligado quando o programa dormiu.
/// </summary>
public sealed class HibernatedAutorun
{
    /// <summary>O nome do item.</summary>
    public required string Name { get; set; }

    /// <summary>A chave de registro ou a pasta do item.</summary>
    public required string Location { get; set; }
}

/// <summary>
/// Um servico que foi parado quando o programa dormiu.
/// </summary>
public sealed class HibernatedService
{
    /// <summary>O nome interno do servico.</summary>
    public required string Name { get; set; }

    /// <summary>O modo de inicializacao que o servico tinha.</summary>
    public string StartMode { get; set; } = string.Empty;

    /// <summary>O estado que o servico tinha ("Running", "Stopped"...).</summary>
    public string State { get; set; } = string.Empty;
}

/// <summary>
/// Um programa colocado para dormir: a inicializacao automatica desligada e
/// os servicos parados.
/// </summary>
public sealed class HibernatedProgram
{
    /// <summary>O nome do programa.</summary>
    public required string ProgramName { get; set; }

    /// <summary>A pasta de instalacao do programa.</summary>
    public string InstallLocation { get; set; } = string.Empty;

    /// <summary>Os itens de inicializacao que foram desligados.</summary>
    public List<HibernatedAutorun> Autorun { get; set; } = new();

    /// <summary>Os servicos que foram parados.</summary>
    public List<HibernatedService> Services { get; set; } = new();

    /// <summary>Quando o programa foi dormir.</summary>
    public DateTime HibernatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// Coloca um programa para dormir e acorda depois.
///
/// O Ashampoo tem o "Hibernation": o programa para de se iniciar com o
/// Windows e seus servicos param, mas o programa continua instalado. E o
/// meio termo entre "deixar como esta" e "desinstalar".
///
/// A REGRA mora aqui, em um lugar so: este modulo consome o
/// <see cref="AutorunManager"/> e o <see cref="ServiceManager"/> e guarda o
/// estado anterior, que e o que permite acordar o programa.
/// </summary>
public static class ProgramHibernation
{
    /// <summary>A pasta onde o estado dos programas dormentes e gravado.</summary>
    public static string DataDirectory => Path.Combine(
        RegistryBackup.DataDirectory, "hibernation");

    /// <summary>O arquivo do estado dos programas dormentes.</summary>
    public static string FilePath => Path.Combine(DataDirectory, "hibernated.json");

    /// <summary>Os programas que estao dormindo.</summary>
    public static IReadOnlyList<HibernatedProgram> List()
    {
        try
        {
            if (!File.Exists(FilePath)) return Array.Empty<HibernatedProgram>();

            var json = File.ReadAllText(FilePath, Encoding.UTF8);
            if (json.Trim().Length == 0) return Array.Empty<HibernatedProgram>();

            var lista = JsonSerializer.Deserialize<List<HibernatedProgram>>(json);
            if (lista is null) return Array.Empty<HibernatedProgram>();

            return lista.OrderByDescending(h => h.HibernatedAt).ToList();
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao ler os programas dormentes: {ex.Message}");
            return Array.Empty<HibernatedProgram>();
        }
    }

    /// <summary>True quando o programa esta dormindo.</summary>
    /// <param name="programName">O nome do programa.</param>
    public static bool IsHibernated(string? programName)
    {
        if (string.IsNullOrWhiteSpace(programName)) return false;

        return List().Any(h => string.Equals(h.ProgramName, programName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Coloca o programa para dormir: desliga a inicializacao automatica dele
    /// e para os servicos.
    /// </summary>
    /// <param name="program">O programa instalado.</param>
    public static (bool Ok, string Message) Hibernate(InstalledProgram? program)
    {
        if (program is null) return (false, "programa nao informado");

        var local = PathUtil.Normalize(program.InstallLocation);
        if (local.Length == 0)
            return (false, "o programa nao informa a pasta de instalacao");

        if (IsHibernated(program.DisplayName))
            return (false, $"'{program.DisplayName}' ja esta dormindo");

        var registro = new HibernatedProgram
        {
            ProgramName = program.DisplayName,
            InstallLocation = local,
        };

        var partes = new List<string>();

        foreach (var item in AutorunManager.Scan())
        {
            if (item.IsSystem || !item.Enabled) continue;
            if (!PertenceAoPrograma(item, local)) continue;

            if (AutorunManager.SetEnabled(item, false))
            {
                registro.Autorun.Add(new HibernatedAutorun
                {
                    Name = item.Name,
                    Location = item.Location,
                });
            }
        }

        foreach (var servico in ServiceManager.ListFor(local))
        {
            if (ServiceManager.IsCritical(servico)) continue;

            var parou = ServiceManager.Stop(servico);
            var desativou = ServiceManager.Disable(servico);

            if (parou.Success || desativou.Success)
            {
                registro.Services.Add(new HibernatedService
                {
                    Name = servico.Name,
                    StartMode = servico.StartMode,
                    State = servico.State,
                });
            }
        }

        // Nada desligado e nada gravado: um registro de programa dormente
        // que nao tem nada desligado faz o programa parecer dormente sem
        // estar — e o "acordar" nao teria o que reverter.
        if (registro.Autorun.Count == 0 && registro.Services.Count == 0)
        {
            return (false, $"'{program.DisplayName}' nao tem inicializacao automatica nem servico para desligar");
        }

        var lista = List().ToList();
        lista.Add(registro);
        Save(lista);

        partes.Add($"{registro.Autorun.Count} item(ns) de inicializacao desligado(s)");
        partes.Add($"{registro.Services.Count} servico(s) parado(s)");

        return (true, $"'{program.DisplayName}' dormiu: {string.Join("; ", partes)}");
    }

    /// <summary>
    /// EsquECE um programa dormente, sem reverter nada.
    ///
    /// E o counterpart de "acordar" para o caso em que o registro ficou
    /// errado: o programa volta a ser tratado como acordado.
    /// </summary>
    /// <param name="programName">O nome do programa.</param>
    public static bool Forget(string? programName)
    {
        if (string.IsNullOrWhiteSpace(programName)) return false;

        var lista = List()
            .Where(h => !string.Equals(h.ProgramName, programName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Save(lista);
        return true;
    }

    /// <summary>
    /// Acorda o programa: religa a inicializacao automatica e devolve os
    /// servicos ao estado que estavam.
    /// </summary>
    /// <param name="programName">O nome do programa.</param>
    public static (bool Ok, string Message) Wake(string? programName)
    {
        if (string.IsNullOrWhiteSpace(programName)) return (false, "programa nao informado");

        var registro = List().FirstOrDefault(
            h => string.Equals(h.ProgramName, programName, StringComparison.OrdinalIgnoreCase));

        if (registro is null) return (false, $"'{programName}' nao esta dormindo");

        var religados = 0;

        foreach (var item in AutorunManager.Scan())
        {
            var guardado = registro.Autorun.FirstOrDefault(
                a => string.Equals(a.Location, item.Location, StringComparison.OrdinalIgnoreCase));

            if (guardado is null) continue;
            if (item.Enabled) continue;

            if (AutorunManager.SetEnabled(item, true)) religados++;
        }

        var acordados = 0;
        foreach (var servico in ServiceManager.ListFor(registro.InstallLocation))
        {
            var guardado = registro.Services.FirstOrDefault(s => s.Name == servico.Name);
            if (guardado is null) continue;

            var ativou = ServiceManager.SetStartMode(servico, guardado.StartMode);
            if (!ativou.Success) continue;

            if (string.Equals(guardado.State, "Running", StringComparison.OrdinalIgnoreCase))
            {
                ServiceManager.Start(servico);
            }

            acordados++;
        }

        var lista = List().Where(h => !string.Equals(h.ProgramName, programName, StringComparison.OrdinalIgnoreCase)).ToList();
        Save(lista);

        return (true, $"'{programName}' acordou: {religados} item(ns) religado(s), {acordados} servico(s) de volta");
    }

    /// <summary>
    /// True quando o item de inicializacao pertence ao programa.
    /// </summary>
    private static bool PertenceAoPrograma(AutorunItem item, string local)
    {
        foreach (var caminho in new[] { item.Command, item.Location })
        {
            if (string.IsNullOrWhiteSpace(caminho)) continue;

            var normalizado = PathUtil.Normalize(caminho);
            if (normalizado.Length == 0) continue;

            if (normalizado.Equals(local, StringComparison.OrdinalIgnoreCase)) return true;

            if (normalizado.StartsWith(local + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void Save(List<HibernatedProgram> lista)
    {
        try
        {
            Directory.CreateDirectory(DataDirectory);
            var json = JsonSerializer.Serialize(lista, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json, new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao gravar os programas dormentes: {ex.Message}");
        }
    }
}
