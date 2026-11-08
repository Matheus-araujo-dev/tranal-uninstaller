using Microsoft.Win32;
using Tranal.Uninstaller.Core.Tools;
using System.IO;
using Tranal.Uninstaller.Core.Management;
using Tranal.Uninstaller.Core.RegistryEditing;

namespace Tranal.Uninstaller.Core.Modules;

/// <summary>
/// Uma biblioteca compartilhada registrada pelo Windows.
/// </summary>
public sealed class SharedDllEntry
{
    /// <summary>O nome do arquivo (o valor da chave).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>O caminho completo da DLL.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// Quantos programas usam a biblioteca, segundo o proprio Windows.
    /// O valor gravado no registro e o contador oficial.
    /// </summary>
    public int ReferenceCount { get; init; }

    /// <summary>Os programas instalados que ainda usam a biblioteca.</summary>
    public IReadOnlyList<string> UsedBy { get; init; } = Array.Empty<string>();

    /// <summary>Tamanho da biblioteca, quando ela existe no disco.</summary>
    public long SizeBytes { get; init; }

    /// <summary>
    /// True quando nenhum programa instalado usa mais a biblioteca.
    /// E o que autoriza a remocao.
    /// </summary>
    public bool IsOrphan => UsedBy.Count == 0;

    /// <summary>
    /// True quando a biblioteca ainda e usada por algum programa instalado.
    /// </summary>
    public bool IsShared => UsedBy.Count > 0;
}

/// <summary>
/// Catalogo de componentes compartilhados (DLLs compartilhadas).
///
/// O HiBit tem um "gerenciador de componentes de programas": ele lista as
/// bibliotecas que o Windows registra como compartilhadas e mostra quantos
/// programas ainda dependem de cada uma. O Total Uninstall pergunta
/// "Remove shared file?" antes de apagar um item compartilhado.
///
/// A REGRA mora aqui, em um lugar so: o contador oficial do Windows (a chave
/// <c>SharedDLLs</c>) diz quantas referencias existem, e a checagem contra os
/// programas instalados diz se ainda ha alguem usando. Quem remove consome
/// esta analise — nunca apaga uma biblioteca compartilhada por conta propria.
///
/// A remocao e conservadora: so acontece quando o contador do Windows e 1
/// (ou menos) E nenhum programa instalado referencia a biblioteca.
/// </summary>
public static class SharedDllCatalog
{
    /// <summary>
    /// As chaves onde o Windows registra as bibliotecas compartilhadas.
    /// </summary>
    private static readonly string[] CatalogKeys =
    {
        @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\SharedDLLs",
        @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\SharedDLLs",
    };

    /// <summary>
    /// Lista as bibliotecas compartilhadas registradas.
    /// </summary>
    /// <param name="programs">
    /// Os programas instalados, para dizer quem ainda usa cada biblioteca.
    /// </param>
    public static IReadOnlyList<SharedDllEntry> List(IEnumerable<InstalledProgram>? programs = null)
    {
        var entradas = new List<SharedDllEntry>();
        var vistas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var chave in CatalogKeys)
        {
            try
            {
                var (hive, sub) = SafetyRules.SplitRegistryPath(chave);
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                using var key = baseKey.OpenSubKey(sub);
                if (key is null) continue;

                foreach (var nome in key.GetValueNames())
                {
                    if (nome.Length == 0) continue;

                    var valor = key.GetValue(nome);
                    var caminho = valor as string ?? string.Empty;
                    var contador = valor is int numero ? numero : 0;

                    // A mesma DLL aparece nas duas chaves (64 e 32 bits).
                    if (!vistas.Add(caminho)) continue;

                    var users = UsersOf(caminho, programs);

                    entradas.Add(new SharedDllEntry
                    {
                        Name = nome,
                        Path = PathUtil.Normalize(caminho),
                        ReferenceCount = contador,
                        UsedBy = users,
                        SizeBytes = SafeLength(caminho),
                    });
                }
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao ler o catalogo de componentes compartilhados ('{chave}'): {ex.Message}");
            }
        }

        return entradas.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Os programas instalados que ainda referenciam a biblioteca.
    /// </summary>
    private static IReadOnlyList<string> UsersOf(string path, IEnumerable<InstalledProgram>? programs)
    {
        if (programs is null) return Array.Empty<string>();
        if (string.IsNullOrWhiteSpace(path)) return Array.Empty<string>();

        var alvo = PathUtil.Normalize(path);
        var donos = new List<string>();

        foreach (var programa in programs)
        {
            if (programa is null) continue;
            if (string.IsNullOrWhiteSpace(programa.DisplayName)) continue;

            if (ProgramUses(programa, alvo))
            {
                var nome = programa.DisplayName.Trim();
                if (!donos.Contains(nome, StringComparer.OrdinalIgnoreCase)) donos.Add(nome);
            }
        }

        return donos;
    }

    /// <summary>
    /// True quando o programa ainda aponta para a biblioteca.
    /// </summary>
    private static bool ProgramUses(InstalledProgram programa, string target)
    {
        var location = programa.InstallLocation;
        if (!string.IsNullOrWhiteSpace(location) && EstaDentro(target, location)) return true;

        var icone = programa.DisplayIcon;
        if (!string.IsNullOrWhiteSpace(icone))
        {
            var caminho = ExecutablePathResolver.Extract(icone);
            if (caminho.Length > 0 && EstaDentro(target, caminho)) return true;
        }

        return false;
    }

    /// <summary>
    /// True quando o caminho esta dentro da pasta informada.
    /// </summary>
    private static bool EstaDentro(string path, string folder)
    {
        var full = PathUtil.Normalize(path);
        var root = PathUtil.Normalize(folder);

        if (full.Length == 0 || root.Length == 0) return false;
        if (string.Equals(full, root, StringComparison.OrdinalIgnoreCase)) return true;

        return full.Length > root.Length
            && full.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            && full[root.Length] == '\\';
    }

    /// <summary>
    /// Remove a biblioteca compartilhada, quando as regras autorizam.
    ///
    /// As regras sao: contador do Windows menor ou igual a 1, nenhum programa
    /// instalado usando e o arquivo fora de pasta protegida.
    /// </summary>
    /// <param name="entry">A biblioteca a remover.</param>
    public static ManagementResult Remove(SharedDllEntry entry)
    {
        if (entry is null) return ManagementResult.Fail("Biblioteca nao informada.");

        if (entry.IsShared)
        {
            return ManagementResult.Fail(
                $"A biblioteca ainda e usada por {entry.UsedBy.Count} programa(s) instalado(s), "
                + $"e o Windows registra {entry.ReferenceCount} referencia(s).");
        }

        if (entry.ReferenceCount > 1)
        {
            return ManagementResult.Fail(
                $"O Windows registra {entry.ReferenceCount} referencia(s) para esta biblioteca.");
        }

        if (entry.Path.Length > 0 && SafetyRules.IsProtectedFolder(entry.Path))
        {
            return ManagementResult.Fail("A biblioteca fica em uma pasta protegida pelo Windows.");
        }

        // A chave do registro e o contador oficial: sem ela, o Windows perde a
        // informacao de quantos programas usam a biblioteca.
        foreach (var chave in CatalogKeys)
        {
            try
            {
                var (hive, sub) = SafetyRules.SplitRegistryPath(chave);
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                using var key = baseKey.OpenSubKey(sub, writable: true);
                if (key is null) continue;

                if (key.GetValueNames().Contains(entry.Name, StringComparer.OrdinalIgnoreCase))
                {
                    key.DeleteValue(entry.Name, throwOnMissingValue: false);
                }
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao remover a referencia de registro de '{entry.Name}': {ex.Message}");
            }
        }

        if (entry.Path.Length == 0 || !File.Exists(entry.Path))
        {
            return ManagementResult.Ok($"A referencia de '{entry.Name}' foi removida do registro.");
        }

        var remocao = UninstallEngine.RemoveLeftovers(new List<LeftoverItem>
        {
            new()
            {
                Kind = "Arquivo",
                Path = entry.Path,
                Detail = "Componente compartilhado",
                Selected = true,
            },
        }, new UninstallRemovalOptions
        {
            Backup = false,
            UseRecycleBin = true,
            DeferToNextRestart = true,
        });

        var removido = remocao.Outcomes.Any(o => o.Gone);
        return removido
            ? ManagementResult.Ok($"A biblioteca '{entry.Name}' foi removida.")
            : ManagementResult.Fail($"A biblioteca '{entry.Name}' nao pode ser removida agora.");
    }

    /// <summary>
    /// O tamanho de um arquivo, quando ele pode ser lido.
    /// </summary>
    private static long SafeLength(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return 0;
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
