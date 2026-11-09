using System.IO;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Uninstaller.Core.Modules;

/// <summary>
/// Um arquivo de dump de falha.
/// </summary>
public sealed class CrashDump
{
    /// <summary>O caminho do arquivo .dmp.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>O nome do arquivo.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>O tamanho do arquivo, em bytes.</summary>
    public long SizeBytes { get; init; }

    /// <summary>A data de gravacao do dump.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// O programa que gerou o dump, quando da para saber pelo nome do
    /// arquivo (o Windows grava o nome do processo no dump).
    /// </summary>
    public string Suspect { get; init; } = string.Empty;
}

/// <summary>
/// Coleta os dumps de falha do Windows.
///
/// O ashampoo tem um "Crash Analyzer" que le o Event Log; o IObit e o
/// ashampoo tambem limpam os dumps de falha (arquivos .dmp) que ocupam
/// espaco e nao servem para nada depois que o problema foi resolvido.
///
/// A REGRA mora aqui, em um lugar so: onde o Windows guarda os dumps
/// (MiniDump e FullDump) e a leitura desses arquivos ficam aqui.
///
/// A limpeza dos dumps usa o motor canonico de remocao, que respeita as
/// regras de seguranca e sabe adiar o que estiver travado.
/// </summary>
public static class CrashDumpCollector
{
    /// <summary>
    /// As pastas onde o Windows grava os dumps de falha.
    /// </summary>
    public static IReadOnlyList<string> Folders()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        return new List<string>
        {
            Path.Combine(windows, "Minidump"),
            Path.Combine(windows, "LiveKernelReports"),
            Path.Combine(local, "CrashDumps"),
            Path.Combine(programData, "Microsoft", "Windows", "WER", "ReportQueue"),
        };
    }

    /// <summary>
    /// Os dumps de falha encontrados.
    /// </summary>
    /// <param name="limit">O numero maximo de dumps.</param>
    public static IReadOnlyList<CrashDump> List(int limit = 500)
    {
        var dumps = new List<CrashDump>();

        foreach (var pasta in Folders())
        {
            try
            {
                if (!Directory.Exists(pasta)) continue;

                foreach (var arquivo in Directory.EnumerateFiles(pasta, "*.dmp", SearchOption.AllDirectories))
                {
                    var info = SafeInfo(arquivo);
                    if (info is null) continue;

                    dumps.Add(new CrashDump
                    {
                        Path = PathUtil.Normalize(arquivo),
                        Name = Path.GetFileName(arquivo),
                        SizeBytes = info.Length,
                        CreatedAt = info.LastWriteTime,
                        Suspect = GuessProcess(arquivo),
                    });

                    if (dumps.Count >= limit) return dumps;
                }
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao varrer os dumps de '{pasta}': {ex.Message}");
            }
        }

        return dumps.OrderByDescending(d => d.CreatedAt).ToList();
    }

    /// <summary>
    /// O total ocupado pelos dumps de falha.
    /// </summary>
    public static long TotalSize() => List().Sum(d => d.SizeBytes);

    /// <summary>
    /// Apaga os dumps de falha e devolve o relatorio canonico.
    /// </summary>
    /// <param name="dumps">Os dumps a apagar (vazio = todos).</param>
    public static RemovalReport Clear(IEnumerable<CrashDump>? dumps = null)
    {
        var alvos = (dumps ?? List()).Where(d => d is not null).ToList();
        if (alvos.Count == 0) return new RemovalReport(new List<RemovalOutcome>());

        var sobras = alvos.Select(d => new LeftoverItem
        {
            Kind = "Arquivo",
            Path = d.Path,
            Detail = "Dump de falha",
            Selected = true,
        }).ToList();

        try
        {
            return UninstallEngine.RemoveLeftovers(sobras, new UninstallRemovalOptions
            {
                Backup = false,
                UseRecycleBin = true,
                DeferToNextRestart = true,
            });
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao apagar os dumps de falha: {ex.Message}");
            return new RemovalReport(new List<RemovalOutcome>());
        }
    }

    /// <summary>
    /// O nome do processo que gerou o dump, lido do nome do arquivo.
    ///
    /// O Windows grava o dump como "nome-do-programa.exe.1234.dmp" ou
    /// "WER-1234-abc.dump" — no primeiro caso o nome do processo esta no
    /// proprio arquivo.
    /// </summary>
    /// <param name="path">O caminho do dump.</param>
    public static string GuessProcess(string? path)
    {
        var nome = Path.GetFileNameWithoutExtension(PathUtil.Normalize(path));
        if (nome.Length == 0) return string.Empty;

        // "app.exe.1234" -> "app.exe"
        var ponto = nome.LastIndexOf('.');
        if (ponto > 0)
        {
            var parte = nome[..ponto];
            if (parte.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return parte;
            if (parte.EndsWith(".sys", StringComparison.OrdinalIgnoreCase)) return parte;
        }

        return nome;
    }

    /// <summary>
    /// Os dados do arquivo, quando ele pode ser lido.
    /// </summary>
    private static FileInfo? SafeInfo(string path)
    {
        try { return new FileInfo(path); }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao ler os dados de '{path}': {ex.Message}");
            return null;
        }
    }
}
