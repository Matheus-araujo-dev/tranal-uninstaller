using System.IO;
using Microsoft.Win32;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// Uma DLL que esta em uma pasta de sistema.
/// </summary>
public sealed class SystemDllInfo
{
    /// <summary>O nome do arquivo.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>O caminho completo.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>O tamanho em bytes.</summary>
    public long SizeBytes { get; init; }

    /// <summary>
    /// True quando o Windows ja a carrega pelo nome (a lista de KnownDLLs).
    /// </summary>
    public bool IsKnownDll { get; init; }

    /// <summary>O nome da empresa que assina o arquivo.</summary>
    public string CompanyName { get; init; } = string.Empty;

    /// <summary>O nome do produto do arquivo.</summary>
    public string ProductName { get; init; } = string.Empty;
}

/// <summary>
/// O rastreamento das DLLs que estao nas pastas de sistema.
///
/// O SoftOrganizer rastreia as DLLs colocadas em pastas do sistema: e o
/// que faz um programa "limpo" deixar bibliotecas pelo caminho. No Tranal,
/// nada media isso.
///
/// A REGRA mora aqui, em um lugar so: a lista de KnownDLLs do Windows e a
/// leitura das pastas de sistema sao decididas aqui.
///
/// O que a lista diz e medido: uma DLL que o Windows NAO carrega pelo nome
/// e uma candidata a biblioteca deixada por um programa — nao e prova, e
/// por isso a coluna diz "candidata".
/// </summary>
public static class SystemDllTracker
{
    /// <summary>
    /// A chave de registro com as DLLs que o Windows carrega pelo nome.
    /// </summary>
    public const string KnownDllsKeyPath =
        @"SYSTEM\CurrentControlSet\Control\Session Manager\KnownDLLs";

    /// <summary>As pastas de sistema varridas por padrao.</summary>
    public static IReadOnlyList<string> DefaultFolders
    {
        get
        {
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (windows.Length == 0) return Array.Empty<string>();

            return new[]
            {
                Path.Combine(windows, "System32"),
                Path.Combine(windows, "SysWOW64"),
            };
        }
    }

    /// <summary>
    /// As DLLs de uma pasta de sistema.
    /// </summary>
    /// <param name="folder">A pasta. Vazio usa as pastas de sistema.</param>
    public static IReadOnlyList<SystemDllInfo> List(string? folder = null)
    {
        var pastas = string.IsNullOrWhiteSpace(folder)
            ? DefaultFolders
            : new[] { PathUtil.Normalize(folder) };

        var conhecidas = KnownDlls();

        var dlls = new List<SystemDllInfo>();

        foreach (var pasta in pastas)
        {
            if (pasta.Length == 0 || !Directory.Exists(pasta)) continue;

            try
            {
                foreach (var arquivo in Directory.EnumerateFiles(pasta, "*.dll"))
                {
                    var nome = Path.GetFileName(arquivo);
                    var meta = ExecutableMetadataReader.Read(arquivo);

                    dlls.Add(new SystemDllInfo
                    {
                        Name = nome,
                        Path = arquivo,
                        SizeBytes = SafeLength(arquivo),
                        IsKnownDll = conhecidas.Contains(nome),
                        CompanyName = meta?.CompanyName ?? string.Empty,
                        ProductName = meta?.ProductName ?? string.Empty,
                    });
                }
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao varrer '{pasta}': {ex.Message}");
            }
        }

        return dlls
            .OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// As DLLs que o Windows NAO carrega pelo nome: as candidatas a
    /// biblioteca deixada por um programa.
    /// </summary>
    /// <param name="folder">A pasta. Vazio usa as pastas de sistema.</param>
    public static IReadOnlyList<SystemDllInfo> Candidates(string? folder = null) =>
        List(folder).Where(d => !d.IsKnownDll).ToList();

    /// <summary>
    /// Os nomes das DLLs que o Windows carrega pelo nome.
    /// </summary>
    public static IReadOnlySet<string> KnownDlls()
    {
        var nomes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default);
            using var chave = baseKey.OpenSubKey(KnownDllsKeyPath);
            if (chave is null) return nomes;

            foreach (var nome in chave.GetValueNames())
            {
                var valor = chave.GetValue(nome) as string;
                if (string.IsNullOrWhiteSpace(valor)) continue;

                foreach (var parte in valor.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    nomes.Add(parte.Trim());
                }
            }
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao ler a lista de KnownDLLs: {ex.Message}");
        }

        return nomes;
    }

    private static long SafeLength(string caminho)
    {
        try
        {
            return new FileInfo(caminho).Length;
        }
        catch (Exception)
        {
            return 0;
        }
    }
}
