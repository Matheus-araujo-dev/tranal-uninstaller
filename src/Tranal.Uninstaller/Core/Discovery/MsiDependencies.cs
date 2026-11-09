using Microsoft.Win32;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// Uma dependencia declarada entre pacotes do Windows Installer.
/// </summary>
public sealed class MsiDependency
{
    /// <summary>
    /// O pacote de que se depende (o "provider" do registro).
    ///
    /// Ex.: "aspnetcore_runtime_80.56.61678_x64".
    /// </summary>
    public string RequiredPackage { get; init; } = string.Empty;

    /// <summary>
    /// O produto que declara a dependencia — o que precisa do pacote acima.
    ///
    /// Ex.: "VS.{AEF703B8-D2CC-4343-915C-F54A30B90937}".
    /// </summary>
    public string DependentPackage { get; init; } = string.Empty;

    /// <summary>
    /// O codigo do produto dependente, quando o registro o informa.
    /// </summary>
    public string DependentProductCode { get; init; } = string.Empty;

    /// <summary>A chave de registro onde a dependencia foi encontrada.</summary>
    public string RegistryPath { get; init; } = string.Empty;
}

/// <summary>
/// As dependencias entre pacotes do Windows Installer.
///
/// O Total Uninstall mostra "Depends on packages": o que um pacote exige
/// para funcionar. A informacao esta no proprio registro do Windows
/// Installer, em <c>HKLM\SOFTWARE\Classes\Installer\Dependencies</c>, e o
/// Tranal so nao a mostrava.
///
/// A estrutura do registro, medida nesta maquina:
///
/// <code>
/// Dependencies\\<pacigo exigido>\\Dependents\\<produto que depende>
/// </code>
///
/// O primeiro nivel e o pacote de que se depende; o ultimo e o produto que
/// declara a dependencia. Quando o valor da ultima chave existe, ele e o
/// codigo do produto dependente.
///
/// A REGRA mora aqui, em um lugar so: a leitura do registro e a traducao
/// dos nomes.
/// </summary>
public static class MsiDependencies
{
    /// <summary>A chave de registro das dependencias do Windows Installer.</summary>
    public const string DependenciesKeyPath = @"SOFTWARE\Classes\Installer\Dependencies";

    /// <summary>
    /// O nome da chave que agrupa os produtos dependentes.
    /// </summary>
    public const string DependentsKeyName = "Dependents";

    /// <summary>
    /// Todas as dependencias declaradas entre pacotes.
    /// </summary>
    public static IReadOnlyList<MsiDependency> List()
    {
        var dependencias = new List<MsiDependency>();

        foreach (var (hive, isMachine) in new[]
                 {
                     (RegistryHive.LocalMachine, true),
                     (RegistryHive.CurrentUser, false),
                 })
        {
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    using var chave = baseKey.OpenSubKey(DependenciesKeyPath);
                    if (chave is null) continue;

                    foreach (var exigido in chave.GetSubKeyNames())
                    {
                        using var chaveExigido = chave.OpenSubKey(exigido);
                        if (chaveExigido is null) continue;

                        foreach (var dependente in DependentsOf(chaveExigido))
                        {
                            dependencias.Add(new MsiDependency
                            {
                                RequiredPackage = exigido,
                                DependentPackage = dependente.Nome,
                                DependentProductCode = dependente.Codigo,
                                RegistryPath = RootPathOf(hive) + @"\" + DependenciesKeyPath
                                              + @"\" + exigido + @"\" + DependentsKeyName
                                              + @"\" + dependente.Nome,
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    ToolLog.Error($"Falha ao ler as dependencias do Windows Installer: {ex.Message}");
                }
            }
        }

        return dependencias;
    }

    /// <summary>
    /// As dependencias de um produto especifico: o que ele exige.
    /// </summary>
    /// <param name="dependentPackage">O produto que declara a dependencia.</param>
    public static IReadOnlyList<MsiDependency> ListFor(string? dependentPackage)
    {
        if (string.IsNullOrWhiteSpace(dependentPackage)) return Array.Empty<MsiDependency>();

        return List()
            .Where(d => string.Equals(d.DependentPackage, dependentPackage, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Os produtos dependentes de um pacote exigido.
    /// </summary>
    /// <param name="requiredPackage">O pacote exigido.</param>
    public static IReadOnlyList<MsiDependency> RequiredBy(string? requiredPackage)
    {
        if (string.IsNullOrWhiteSpace(requiredPackage)) return Array.Empty<MsiDependency>();

        return List()
            .Where(d => string.Equals(d.RequiredPackage, requiredPackage, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Os dependentes de um pacote exigido, lidos da chave "Dependents".
    /// </summary>
    private static IReadOnlyList<(string Nome, string Codigo)> DependentsOf(RegistryKey chaveExigido)
    {
        var lista = new List<(string, string)>();

        try
        {
            using var dependentes = chaveExigido.OpenSubKey(DependentsKeyName);
            if (dependentes is null) return lista;

            foreach (var nome in dependentes.GetSubKeyNames())
            {
                var codigo = string.Empty;

                try
                {
                    using var chave = dependentes.OpenSubKey(nome);
                    codigo = (chave?.GetValue(null) as string ?? string.Empty).Trim();
                }
                catch (Exception) { /* o codigo e opcional */ }

                lista.Add((nome, codigo));
            }
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao ler os dependentes: {ex.Message}");
        }

        return lista;
    }

    private static string RootPathOf(RegistryHive hive) => hive switch
    {
        RegistryHive.LocalMachine => @"HKEY_LOCAL_MACHINE",
        RegistryHive.CurrentUser => @"HKEY_CURRENT_USER",
        _ => @"HKEY_LOCAL_MACHINE",
    };
}
